using UnityEngine;
namespace SwapHunter
{
    public sealed partial class PlayerMotor
    {
        sealed class ReloadV2State
        {
            public bool pending,suspended,committed,empty,pump,finishing,soundOut;
            public float elapsed,total,speed=1;
            public int wanted,inserted;
        }
        readonly ReloadV2State[] reloadV2=new ReloadV2State[WeaponCatalog.Count];
        ReloadV2State ReloadV2Current=>reloadV2[weapon];
        public bool ReloadV2Pending=>ReloadV2Current!=null&&ReloadV2Current.pending;
        public bool ReloadV2Committed=>ReloadV2Current!=null&&ReloadV2Current.committed;
        public bool ReloadV2MechanicalReady=>!ReloadV2Pending;
        public float ReloadV2Elapsed=>ReloadV2Current!=null?ReloadV2Current.elapsed:0;
        public float ReloadV2Total=>ReloadV2Current!=null?ReloadV2Current.total:0;
        public int ReloadV2Inserted=>ReloadV2Current!=null?ReloadV2Current.inserted:0;
        public string ReloadV2Stage=>!ReloadV2Pending?"ready":ReloadV2Current.pump?(ReloadV2Current.finishing?"return":"single_shell"):ReloadV2Current.elapsed/ReloadV2Current.total<.09375f?"reach":ReloadV2Current.elapsed/ReloadV2Current.total<.2375f?"extract":!ReloadV2Current.committed?"fetch_insert":"chamber_return";
        public static float ReloadV2CommitFraction(int id)=>id==5?1.65f/2.30f:id==8?2.35f/3.40f:1.18f/1.60f;
        public bool BeginReloadV2()
        {
            if(!G.IsPlaying||health<=0||IsVaulting||SwapActionsLocked)return false;
            if(ReloadV2Pending){if(!ReloadV2Current.suspended)return false;ResumeReloadV2();return true;}
            if(ammo[weapon]>=Capacity(weapon)||reserve[weapon]<=0)return false;
            CancelFireSequence();IsAiming=false;sprintLatched=IsSprinting=false;BeginReloadPresentation();
            var state=new ReloadV2State{pending=true,empty=ammo[weapon]==0,pump=weapon==1,wanted=Mathf.Min(Capacity(weapon)-ammo[weapon],reserve[weapon])};
            state.total=state.pump?.5f+state.wanted*.4f:EffectiveWeaponStats.Read(weapon,G.config).Reload;
            state.speed=1/Mathf.Max(.05f,G.ModuleReload*G.ExpeditionReloadMultiplier);reloadV2[weapon]=state;
            reloadSoundA=reloadSoundB=false;SyncReloadV2(state);G.Record("weapon_reload_begin",weapon+":staged");return true;
        }
        public void TickReloadV2(float dt)
        {
            var state=ReloadV2Current;if(state==null||!state.pending||state.suspended||preparing||IsVaulting)return;
            state.elapsed=Mathf.Min(state.total,state.elapsed+Mathf.Max(0,dt)*state.speed);
            if(!state.soundOut&&state.elapsed>=state.total*.09375f){state.soundOut=true;G.sound.Play("reload_out");}
            if(state.pump)
            {
                while(!state.finishing&&state.inserted<state.wanted&&state.elapsed>=.25f+(state.inserted+1)*.4f)
                {
                    int take=Mathf.Min(1,Mathf.Min(Capacity(weapon)-ammo[weapon],reserve[weapon]));
                    if(take<=0){state.finishing=true;state.total=state.elapsed+.25f;break;}
                    ammo[weapon]+=take;reserve[weapon]-=take;state.inserted++;state.committed=true;G.sound.Play("reload_in");G.Record("weapon_reload_shell",weapon+":"+state.inserted);
                }
            }
            else if(!state.committed&&state.elapsed>=state.total*ReloadV2CommitFraction(weapon))
            {
                int take=Mathf.Min(Capacity(weapon)-ammo[weapon],reserve[weapon]);ammo[weapon]+=take;reserve[weapon]-=take;state.inserted=take;state.committed=true;
                G.sound.Play("reload_in");G.Record("weapon_reload_commit",weapon+":"+take);
            }
            if(state.elapsed>=state.total)
            {
                state.pending=false;state.suspended=false;reloadLeft=0;
                if(state.empty||weapon==5||weapon==9)G.sound.Play("reload_bolt");G.Record("weapon_reload_complete",weapon+":"+state.inserted);ResetReloadPresentation();return;
            }
            SyncReloadV2(state);
        }
        void SyncReloadV2(ReloadV2State state)
        {
            reloadDuration=state.total/Mathf.Max(.01f,state.speed);reloadLeft=state.suspended?0:(state.total-state.elapsed)/Mathf.Max(.01f,state.speed);reloadWasEmpty=state.empty;
        }
        public void SuspendReloadV2()
        {
            var state=ReloadV2Current;if(state!=null&&state.pending)state.suspended=true;reloadLeft=0;
        }
        public void ResumeReloadV2()
        {
            var state=ReloadV2Current;if(state==null||!state.pending){reloadLeft=0;return;}state.suspended=false;SyncReloadV2(state);
        }
        public void ResetAllReloadV2()
        {
            for(int i=0;i<reloadV2.Length;i++)reloadV2[i]=null;reloadLeft=0;ResetReloadPresentation();
        }
        public bool AccelerateReloadV2(float remainingMultiplier)
        {
            var state=ReloadV2Current;
            if(!G.IsPlaying||health<=0||SwapActionsLocked||state==null||!state.pending||state.suspended||remainingMultiplier<=0||remainingMultiplier>=1)return false;
            // Speed up only remaining mechanical time. Commit event thresholds do not change.
            state.speed/=remainingMultiplier;SyncReloadV2(state);G.Record("weapon_reload_accelerate",weapon+":"+remainingMultiplier);return true;
        }
        public bool EnsureReloadV2Ready()
        {
            if(!ReloadV2Pending)return true;
            if(ReloadV2Current.suspended)ResumeReloadV2();
            if(ReloadV2Current.pump&&ammo[weapon]>0&&!ReloadV2Current.finishing)
            {ReloadV2Current.finishing=true;ReloadV2Current.total=ReloadV2Current.elapsed+.25f;SyncReloadV2(ReloadV2Current);}
            return false;
        }
    }
}
