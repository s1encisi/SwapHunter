using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SwapHunter
{
    public sealed partial class ShowcaseCapture
    {
        [Serializable] public sealed class GunplayClipCheck
        {
            public string id,detail;
            public bool passed,reloadStarted,reloadCompletedInClip,earlyAmmoChange,cameraFixed;
            public int ammoBefore,reserveBefore,ammoAfter,reserveAfter,discarded,projectiles;
            public float requestedReloadSeconds,actualReloadSeconds,peakMagazineSeparation,peakHingeAngle,healthBefore,healthAfter;
        }
        [Serializable] public sealed class GunplayCheckReport
        {
            public string kind="Native frame capture at the existing 30 capture FPS. Fixed inspection camera; finite ammo and scene setup occur before clips. No per-frame pose, health, ammo or time edits. Automatic checks validate capture/action invariants only; visual approval remains human. This is not a performance benchmark or natural combat playthrough.";
            public bool passed;public List<GunplayClipCheck> clips=new List<GunplayClipCheck>();
        }
        readonly GunplayCheckReport gunplayChecks=new GunplayCheckReport();

        IEnumerator RecordGunplay()
        {
            if(Application.isBatchMode){report.errors.Add("Gunplay frame capture requires a rendered non-batch player; use GunplayPresentationQA batch mode for assertions without PNGs.");yield break;}
            bool oldInput=G.qaInputEnabled,oldSuppress=G.qaSuppressAI;
            EndScenario();G.StartRun(true);G.qaSuppressAI=true;G.qaInputEnabled=false;
            G.ClearDeployments();G.combatComplete=true;
            foreach(var enemy in G.enemies.ToArray())if(enemy){enemy.gameObject.SetActive(false);Destroy(enemy.gameObject);}
            G.enemies.Clear();
            // One quiet inspection setup for the whole collection, before any Record call.
            P.SetFeet(new Vector3(0,.035f,34));P.SetLook(0,0);
            yield return new WaitForSeconds(.25f);
            try
            {
                yield return RecordGunplayReload("gunplay-01-carbine-empty",0,true,"卡宾枪 · 空仓更换弹匣");
                yield return RecordGunplayReload("gunplay-02-carbine-tactical",0,false,"卡宾枪 · 保留余弹的战术换弹");
                yield return RecordGunplayReload("gunplay-03-shotgun-empty",1,true,"霰弹枪 · 空仓更换供弹匣");
                yield return RecordGunplayReload("gunplay-04-break-action",5,true,"双管枪 · 折开、退壳与装填");
                yield return RecordGunplayReload("gunplay-05-revolver",9,true,"转轮发射器 · 侧摆与装填");
                yield return RecordGunplayIncoming();
            }
            finally {G.qaInputEnabled=oldInput;G.qaSuppressAI=oldSuppress;WriteGunplayChecks();}
        }

        Transform GunplayWeaponRoot(int weapon)
        {
            string suffix=" / weapon_"+weapon.ToString("00");
            foreach(var t in P.GetComponentsInChildren<Transform>(false))if(t.name.EndsWith(suffix,StringComparison.Ordinal))return t;
            return null;
        }
        IEnumerator RecordGunplayReload(string id,int weapon,bool empty,string title)
        {
            if(!Need(id))yield break;
            P.SetLoadout(weapon,weapon==0?1:0);yield return new WaitForSeconds(.18f);
            int capacity=P.Capacity(weapon),before=empty?0:Mathf.Max(1,capacity/2),reserve=capacity*2;
            P.ammo[weapon]=before;P.reserve[weapon]=reserve;
            var root=GunplayWeaponRoot(weapon);string hingeName=weapon==5?"break_action":weapon==9?"cylinder_hinge":"";
            var hinge=root&&hingeName.Length>0?ImportedModels.Find(root,hingeName):null;
            Quaternion hingeRest=hinge?hinge.localRotation:Quaternion.identity;
            float seconds=(weapon==0?G.config.rifleReload:weapon==1?G.config.shotgunReload:WeaponCatalog.Get(weapon).reload)*G.ModuleReload*G.ExpeditionReloadMultiplier;
            var check=new GunplayClipCheck{id=id,ammoBefore=before,reserveBefore=reserve,requestedReloadSeconds=seconds,healthBefore=P.health,cameraFixed=true};
            Vector3 feet=P.transform.position,eye=P.Eye.position;Quaternion view=P.Eye.rotation;
            bool requested=false,completed=false;string stage="";int lastDiscard=0;bool tacticalChamber=false;
            string setup="静态场景与镜头；片前设定弹匣 "+before+" / 备弹 "+reserve+"；片内仅调用真实Reload，未补弹或重设姿态";
            yield return Record(id,seconds+.8f,title,setup,"原生装填自动演示 / 30帧录制，非性能测量",t=>
            {
                // Read state throughout the clip. Only the normal action is issued once.
                if(!requested&&t>=.10f)
                {
                    requested=true;check.reloadStarted=P.Reload();check.actualReloadSeconds=P.reloadLeft;
                    G.Record("gunplay_reload_start","weapon="+weapon+"; empty="+empty+"; accepted="+check.reloadStarted+"; duration="+P.reloadLeft.ToString("F3"));
                }
                if(!check.reloadStarted)return;
                check.cameraFixed&=Vector3.Distance(feet,P.transform.position)<.025f&&Vector3.Distance(eye,P.Eye.position)<.025f&&Quaternion.Angle(view,P.Eye.rotation)<.1f;
                check.peakMagazineSeparation=Mathf.Max(check.peakMagazineSeparation,P.MagazineSeparation);
                if(hinge)check.peakHingeAngle=Mathf.Max(check.peakHingeAngle,Quaternion.Angle(hingeRest,hinge.localRotation));
                check.discarded=Mathf.Max(check.discarded,P.DiscardedMagazineCount);
                if(P.reloadLeft>0&&(P.ammo[weapon]!=before||P.reserve[weapon]!=reserve))check.earlyAmmoChange=true;
                tacticalChamber|=!empty&&P.ReloadStageTag=="chamber";
                if(P.ReloadStageTag!=stage){stage=P.ReloadStageTag;G.Record("gunplay_reload_stage",stage+"; remaining="+P.reloadLeft.ToString("F3")+"; separation="+P.MagazineSeparation.ToString("F3"));}
                if(P.DiscardedMagazineCount!=lastDiscard){lastDiscard=P.DiscardedMagazineCount;G.Record("gunplay_discarded_magazine",lastDiscard.ToString());}
                if(!completed&&P.reloadLeft<=0)
                {completed=true;check.reloadCompletedInClip=true;G.Record("gunplay_reload_complete","ammo="+P.ammo[weapon]+"; reserve="+P.reserve[weapon]);}
            });
            check.ammoAfter=P.ammo[weapon];check.reserveAfter=P.reserve[weapon];check.healthAfter=P.health;
            int take=Mathf.Min(capacity-before,reserve);bool special=weapon==5||weapon==9;
            bool action=special?hinge&&check.peakHingeAngle>15:check.peakMagazineSeparation>=.18f&&(empty?check.discarded==1:check.discarded==0&&!tacticalChamber);
            bool restored=P.MagazineSeparation<.005f&&(!hinge||Quaternion.Angle(hingeRest,hinge.localRotation)<1);
            check.passed=check.reloadStarted&&check.reloadCompletedInClip&&!check.earlyAmmoChange&&check.cameraFixed&&action&&restored&&
                check.ammoAfter==before+take&&check.reserveAfter==reserve-take&&Mathf.Abs(check.healthAfter-check.healthBefore)<.001f;
            check.detail="Actual Reload completed inside recorded frames; supply at completion; finite reserve debit; "+(special?hingeName+" opens/closes":"magazine separates/restores and empty/tactical discard semantics")+"; fixed camera="+check.cameraFixed+"; final separation="+P.MagazineSeparation.ToString("F4");
            FinishGunplayClip(check);
        }

        IEnumerator RecordGunplayIncoming()
        {
            const string id="gunplay-06-incoming-tracers";if(!Need(id))yield break;
            P.SetLoadout(0,1);yield return new WaitForSeconds(.18f);
            var source=EnemyActor.Spawn(EnemyKind.Assault,new Vector3(7,0,48),G.stage);
            if(!source){report.errors.Add(id+": source actor fixture could not spawn");yield break;}
            source.suppressAI=true;
            Vector3 feet=P.transform.position,eye=P.Eye.position,forward=P.Eye.forward,right=P.Eye.right;Quaternion view=P.Eye.rotation;
            var check=new GunplayClipCheck{id=id,healthBefore=P.health,cameraFixed=true};bool velocities=true;
            bool lanesClear=true;
            foreach(float side in new[]{-1f,1f})
                lanesClear&=!Physics.Linecast(eye+forward*10+right*(.85f*side),eye-forward*2+right*(.85f*side),Layers.CombatMask,QueryTriggerInteraction.Ignore);
            float[] shotTimes={.35f,.80f,1.25f,1.70f,2.15f};int next=0;
            yield return Record(id,3.2f,"普通来袭弹丸 · 细曳光掠过肩侧",
                "片前设置普通敌人来源；五发真实Bolt.Spawn，38米/秒、6伤害，固定在左右0.85米肩侧通过；不补血",
                "原生弹道视觉夹具 / 无AI实战结论",t=>
            {
                check.cameraFixed&=Vector3.Distance(feet,P.transform.position)<.025f&&Vector3.Distance(eye,P.Eye.position)<.025f&&Quaternion.Angle(view,P.Eye.rotation)<.1f;
                if(next>=shotTimes.Length||t<shotTimes[next])return;
                float side=next%2==0?-1:1;
                Vector3 origin=eye+forward*10+right*(.85f*side);
                var bolt=Bolt.Spawn(source,origin,-forward,38,6);
                velocities&=bolt.owner==source&&Mathf.Abs(bolt.velocity.magnitude-38)<.001f&&bolt.damage==6;
                G.sound.Play("rifle",origin);G.Record("gunplay_incoming_bolt","lane="+side+"; speed=38; damage=6; lateral offset=.85; origin="+origin.ToString("F3"));
                next++;check.projectiles++;
            });
            check.healthAfter=P.health;
            check.passed=lanesClear&&check.projectiles==shotTimes.Length&&velocities&&check.cameraFixed&&Mathf.Abs(check.healthAfter-check.healthBefore)<.001f;
            check.detail="Five normal-speed, normal-damage ray-tested projectiles; owner preserved; clear shoulder lanes="+lanesClear+"; HP unchanged; no per-frame camera/HP edits";
            FinishGunplayClip(check);source.gameObject.SetActive(false);Destroy(source.gameObject);
        }
        void FinishGunplayClip(GunplayClipCheck check)
        {
            var record=report.clips.FindLast(c=>c.id==check.id);
            if(record==null){check.passed=false;check.detail+="; no recorded clip (check Need selector integration)";}
            else record.passed&=check.passed;
            gunplayChecks.clips.Add(check);if(!check.passed)report.errors.Add("Gunplay clip invariant failed: "+check.id+"; "+check.detail);
            Debug.Log("GUNPLAY_CAPTURE "+check.id+" passed="+check.passed+" "+JsonUtility.ToJson(check));WriteGunplayChecks();
        }
        void WriteGunplayChecks()
        {
            gunplayChecks.passed=gunplayChecks.clips.Count==6&&gunplayChecks.clips.TrueForAll(c=>c.passed);
            File.WriteAllText(Path.Combine(output,"gunplay-capture-checks.json"),JsonUtility.ToJson(gunplayChecks,true));
            File.WriteAllText(Path.Combine(output,"capture-manifest.json"),JsonUtility.ToJson(report,true));
        }
    }
}
