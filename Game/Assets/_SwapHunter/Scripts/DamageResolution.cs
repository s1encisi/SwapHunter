using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SwapHunter
{
    public enum DamageSource { PlayerBullet, PlayerMelee, PlayerGrenade, RemoteCharge, Denial, KnifeOutbound, KnifeReturn, EnemyBullet, EnemyGrenade, Environment }
    public readonly struct DamageContext
    {
        public readonly DamageSource source;
        public readonly long resolutionBatchId,damageEventId;
        public readonly int instigatorId;
        public readonly Vector3 origin;
        public DamageContext(DamageSource source,long batch,long damageEvent,Transform instigator,Vector3 origin)
        {this.source=source;resolutionBatchId=batch;damageEventId=damageEvent;instigatorId=instigator?instigator.GetInstanceID():0;this.origin=origin;}
    }
    public struct DamageResult
    {
        public bool applied,killed,blocked;
        public float healthLoss,energyLoss,markMultiplier;
        public string receiverKind;
        public EnemyHitRegion region;
    }
    // One sample is one event/receiver pair; geometry is captured before any damage is applied.
    public sealed class DamageSample
    {
        internal EnemyActor enemy;
        internal RelayDevice device;
        internal DamageableObjective objective;
        internal float amount;
        internal EnemyHitRegion region;
        internal DamageContext context;
        public Vector3 point;
        public DamageResult Result {get;internal set;}
        public Action<DamageResult> after;
        internal int ReceiverId=>device?device.GetInstanceID():enemy?enemy.GetInstanceID():objective?objective.GetInstanceID():0;
    }
    public sealed class DamageBatch
    {
        readonly List<DamageSample> samples=new List<DamageSample>();
        readonly HashSet<string> sampled=new HashSet<string>();
        readonly HashSet<EnemyActor> deaths=new HashSet<EnemyActor>();
        readonly DamageSource source;readonly Transform instigator;readonly Vector3 origin;
        bool applied;
        public readonly long Id;
        public DamageBatch(DamageSource source,Transform instigator,Vector3 origin)
        {this.source=source;this.instigator=instigator;this.origin=origin;Id=DamageResolution.NextBatchId();}
        public long NewEvent()=>DamageResolution.NextEventId();
        DamageSample Add(DamageSample sample,long eventId)
        {
            if(applied)throw new InvalidOperationException("Cannot sample into an already applied damage batch.");
            if(sample.ReceiverId==0||sample.amount<=0||float.IsNaN(sample.amount)||float.IsInfinity(sample.amount))return null;
            if(!sampled.Add(eventId+":"+sample.ReceiverId))return null;
            sample.context=new DamageContext(source,Id,eventId,instigator,origin);samples.Add(sample);return sample;
        }
        public DamageSample Enemy(EnemyActor enemy,float amount,long eventId,EnemyHitRegion region=EnemyHitRegion.Torso)
        {return enemy&&enemy.Alive?Add(new DamageSample{enemy=enemy,amount=amount,region=region,point=enemy.AimPoint},eventId):null;}
        public DamageSample Device(RelayDevice device,float amount,long eventId)
        {return device&&device.Alive&&DamageResolution.IsPlayer(source)?Add(new DamageSample{device=device,amount=amount,point=device.AimPoint},eventId):null;}
        public DamageSample Objective(DamageableObjective objective,float amount,long eventId)
        {return objective&&objective.Alive?Add(new DamageSample{objective=objective,amount=amount,point=objective.AimPoint},eventId):null;}
        public DamageSample Direct(Collider collider,float amount,Vector3 point,long eventId,bool bodyRegions=true)
        {
            if(!collider)return null;
            // An independent child receiver must win before the parent EnemyActor.
            var device=collider.GetComponentInParent<RelayDevice>();
            DamageSample sample;
            if(device)sample=Device(device,amount,eventId);
            else
            {
                var enemy=collider.GetComponentInParent<EnemyActor>();
                if(enemy)
                {
                    var region=enemy.RegionOf(collider);
                    sample=Enemy(enemy,amount,eventId,region==EnemyHitRegion.Shield||bodyRegions?region:EnemyHitRegion.Torso);
                }
                else sample=Objective(collider.GetComponentInParent<DamageableObjective>(),amount,eventId);
            }
            if(sample!=null)sample.point=point;return sample;
        }
        internal void CarrierDied(EnemyActor enemy){deaths.Add(enemy);}
        public void Apply()
        {
            if(applied)throw new InvalidOperationException("A damage batch may only be applied once.");
            applied=true;var previous=DamageResolution.Current;DamageResolution.Current=this;
            try
            {
                foreach(var sample in samples)
                {
                    DamageResult result=default;
                    if(sample.enemy)result=sample.enemy.ReceiveDamage(sample.context,sample.amount,sample.region);
                    else if(sample.device)
                    {
                        float before=sample.device.Health;
                        result.applied=sample.device.ApplyDamage(sample.amount,sample.context.origin);
                        result.healthLoss=Mathf.Max(0,before-sample.device.Health);result.markMultiplier=1;result.receiverKind="device";
                        DamageResolution.Record(sample.context,sample.device.GetInstanceID(),sample.amount,result);
                    }
                    else if(sample.objective)
                    {
                        float before=sample.objective.Health;
                        result.applied=sample.objective.ApplyDamage(sample.amount,sample.context.origin);
                        result.healthLoss=Mathf.Max(0,before-sample.objective.Health);result.markMultiplier=1;result.receiverKind="objective";
                        DamageResolution.Record(sample.context,sample.objective.GetInstanceID(),sample.amount,result);
                    }
                    sample.Result=result;sample.after?.Invoke(result);
                }
            }
            finally
            {
                DamageResolution.Current=previous;
                foreach(var enemy in deaths)
                    if(previous!=null)previous.CarrierDied(enemy);else DamageResolution.FinalizeCarrierDeath(enemy);
            }
        }
    }
    public static class DamageResolution
    {
        public static bool ExtendedPlayerMarking=true;
        static long nextBatch,nextEvent;
        internal static DamageBatch Current;
        public static long NextBatchId()=>++nextBatch;
        public static long NextEventId()=>++nextEvent;
        public static bool IsPlayer(DamageSource source)=>source<=DamageSource.KnifeReturn;
        public static float MarkMultiplier(DamageSource source,bool marked)
        {
            if(!marked)return 1;
            bool legacy=source==DamageSource.PlayerBullet||source==DamageSource.PlayerMelee;
            return legacy||(ExtendedPlayerMarking&&IsPlayer(source))?1.2f:1;
        }
        public static void CarrierDied(EnemyActor enemy)
        {if(Current!=null)Current.CarrierDied(enemy);else FinalizeCarrierDeath(enemy);}
        internal static void FinalizeCarrierDeath(EnemyActor enemy)
        {
            foreach(var device in RelayDevice.ActiveSnapshot())if(device&&device.Carrier==enemy)device.ResolveCarrierDeath();
        }
        public static DamageResult Single(EnemyActor enemy,float amount,DamageSource source,Transform instigator,Vector3 origin,EnemyHitRegion region=EnemyHitRegion.Torso)
        {
            var batch=new DamageBatch(source,instigator,origin);var sample=batch.Enemy(enemy,amount,batch.NewEvent(),region);batch.Apply();return sample!=null?sample.Result:default;
        }
        internal static void Record(DamageContext context,int receiver,float beforeMark,DamageResult result)
        {
            var game=DemoGame.I;if(!game)return;
            string N(float n)=>n.ToString("R",CultureInfo.InvariantCulture);
            game.Record("d2_damage","batch="+context.resolutionBatchId+";event="+context.damageEventId+";source="+context.source+";instigator="+context.instigatorId+";receiver="+receiver+";kind="+result.receiverKind+";beforeMark="+N(beforeMark)+";mark="+N(result.markMultiplier)+";healthLoss="+N(result.healthLoss)+";energyLoss="+N(result.energyLoss)+";blocked="+result.blocked);
        }
    }
}
