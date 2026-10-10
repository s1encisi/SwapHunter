using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SwapHunter
{
    // Focused T01 acceptance: real runtime actions plus explicit storage/mission fixtures.
    // Controlled positioning and required-enemy defeat do not represent human combat testing.
    public sealed class TacticalT01QA : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, detail; public bool passed; }
        [Serializable] public sealed class Report
        {
            public string scope = "T01 isolated purchase/snapshot/settlement and native skill flow; controlled mission completion; no human playtest or performance claim";
            public bool passed;
            public List<Check> checks = new List<Check>();
        }
        readonly Report report = new Report();
        string output; bool finished; float started;
        DemoGame G => DemoGame.I;
        PlayerMotor P => G.player;
        void Awake()
        {
            started = Time.realtimeSinceStartup;
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-qaOutput");
            output = index >= 0 && index + 1 < args.Length ? args[index + 1] : RunStorage.Root;
            Directory.CreateDirectory(output); Application.logMessageReceived += OnLog;
        }
        void OnLog(string message, string trace, LogType type)
        { if (!finished && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) Add("runtime_error", false, message); }
        void Update() { if (!finished && Time.realtimeSinceStartup - started > 100) { Add("timeout", false); Finish(); } }
        void Add(string name, bool pass, string detail = "")
        { report.checks.Add(new Check { name=name, passed=pass, detail=detail }); Debug.Log("T01_QA " + name + " " + pass + " " + detail); }
        bool Reject(Action action) { try { action(); return false; } catch (Exception e) when (e is InvalidOperationException || e is ArgumentException || e is IOException || e is UnauthorizedAccessException) { return true; } }
        void Aim(Vector3 point)
        {
            Vector3 d = point - P.Eye.position;
            P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);
        }
        void Place(Vector3 feet) { P.CancelActions(); P.SetFeet(feet); P.verticalSpeed=0; P.cooldown=0; Physics.SyncTransforms(); }
        IEnumerator Swap()
        {
            var anchor=G.expeditionMap.anchors[0]; Place(new Vector3(0,.03f,5)); Aim(anchor.AimPoint);
            int before=G.totalSwaps; bool requested=P.RequestSwap(anchor);
            yield return new WaitForSeconds(.16f);
            Add("real_q_committed",requested&&G.totalSwaps==before+1,P.ValidateSwap(anchor).ToString());
        }
        IEnumerator CompleteCalibration()
        {
            // Enter the real trigger, let deployment occur, then explicitly defeat its required fixture.
            Place(new Vector3(0,.03f,22)); G.mission.Tick(0);
            yield return new WaitForSeconds(3.1f);
            foreach(var member in G.mission.Members)
                if(member.role.required&&member.actor&&member.actor.Alive)member.actor.Damage(100000);
            G.mission.Tick(0);
            Add("mission_gate_ready",G.ExpeditionMainComplete,G.ExpeditionObjective);
        }
        IEnumerator Start()
        {
            yield return null;
            Add("isolated_t01",RunStorage.IsTacticalValidation&&RunStorage.IsIsolated);
            if(!RunStorage.IsTacticalValidation){Finish();yield break;}
            var store=G.expeditionStore;
            Add("new_tactical_zero_money",store!=null&&store.IsTactical&&store.Profile.credits==0&&store.MedkitStock==0);
            if(store==null){Finish();yield break;}
            Add("no_paid_purchase_without_funds",Reject(()=>store.BuyMedkit("insufficient"))&&store.Profile.credits==0&&store.MedkitStock==0);
            store.ClaimAssaultStarter();store.ClaimAssaultStarter();
            Add("starter_once_and_legal",store.Primary==0&&store.Secondary==1&&store.HasMobility&&store.HasShield&&store.Profile.credits==0);
            Add("qualification_and_distinct_guns",!store.CanEquip(3)&&Reject(()=>store.Equip(1,3))&&store.Primary!=store.Secondary);
            store.Equip(0,1);Add("slot_exchange",store.Primary==1&&store.Secondary==0);store.Equip(0,0);
            // Storage-only completed-run fixtures provide a known test wallet, not fake playthrough evidence.
            string seed=store.Begin(0);var first=store.Settle(seed,true,false,10);
            Add("first_reward_no_free_unlock",first.reward==TacticalPrototypeRules.FirstClearTotal(0)&&!store.Profile.weapons[4]);
            seed=store.Begin(1);store.Settle(seed,true,false,10);
            int money=store.Profile.credits;
            store.BuyMedkit("purchase-five",5);store.BuyMedkit("purchase-five",5);
            Add("atomic_purchase_and_dedup",store.MedkitStock==5&&store.Profile.credits==money-5*TacticalPrototypeRules.MedkitPrice);
            string temporary=store.SavePath+".tmp";Directory.CreateDirectory(temporary);
            int beforeStock=store.MedkitStock,beforeMoney=store.Profile.credits;
            bool rejected=Reject(()=>store.BuyMedkit("write-failure"));Directory.Delete(temporary);
            Add("write_failure_keeps_state",rejected&&store.MedkitStock==beforeStock&&store.Profile.credits==beforeMoney);
            store.SetMedkitCarry(2);store.SetAttachment(0,1);
            G.OpenExpeditionBoard();G.qaSuppressAI=true;
            Add("deploy_snapshot",G.BeginExpedition(0)&&G.expeditionMedkits==2&&store.MedkitStock==5);
            if(!G.expeditionActive){Finish();yield break;}
            string run=store.Profile.activeRun;
            Add("loadout_and_purchase_locked",Reject(()=>store.SetAttachment(0,0))&&Reject(()=>store.BuyMedkit("in-run")));
            Add("legacy_specializations_inactive",Mathf.Approximately(G.ExpeditionDamageMultiplier,1)&&Mathf.Approximately(G.ExpeditionDamageTaken,1)&&Mathf.Approximately(G.ExpeditionCooldownMultiplier,1));
            yield return new WaitForSeconds(.2f);
            Place(new Vector3(0,.03f,3));P.SetLook(0,0);
            var keys=InputSystem.AddDevice<Keyboard>();G.qaKeyboard=keys;G.qaInputEnabled=true;
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.W));
            yield return new WaitForSeconds(.6f);
            float baselineSpeed=Vector3.ProjectOnPlane(P.controller.velocity,Vector3.up).magnitude;
            Add("mobility_activate",P.classSkill.ActivateMobility()&&P.classSkill.MobilityActive);
            yield return new WaitForSeconds(.4f);
            float boostedSpeed=Vector3.ProjectOnPlane(P.controller.velocity,Vector3.up).magnitude;
            Add("native_ground_speed_bonus",baselineSpeed>0&&Mathf.Abs(boostedSpeed/baselineSpeed-1.25f)<.03f,"baseline="+baselineSpeed+";boosted="+boostedSpeed);
            InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;
            G.qaInputEnabled=false;G.qaKeyboard=null;InputSystem.RemoveDevice(keys);
            float remaining=P.classSkill.MobilityLeft,cd=P.classSkill.CooldownLeft;
            G.SetState(RunState.Paused);yield return new WaitForSecondsRealtime(.15f);
            Add("pause_freezes_skill",Mathf.Abs(P.classSkill.MobilityLeft-remaining)<.001f&&Mathf.Abs(P.classSkill.CooldownLeft-cd)<.001f);
            G.SetState(RunState.Playing);yield return Swap();
            Add("q_keeps_mobility",P.classSkill.MobilityActive);
            bool fired=P.Fire();Add("actual_shot_ends_mobility",fired&&!P.classSkill.MobilityActive&&P.classSkill.CooldownLeft>0);
            Place(new Vector3(0,.03f,5));Aim(P.Eye.position+Vector3.forward*10);
            Add("shield_and_independent_melee",P.tactics.ActivateShield()&&P.combatTools.Melee());
            Add("knife_blocked_by_general_slot",!P.combatTools.Throw()&&!P.combatTools.Recall());
            P.health=G.config.playerHealth-50;
            Add("consume_runtime_only",G.UseExpeditionMedkit()&&G.expeditionMedkits==1&&store.MedkitStock==5);
            P.health=0;G.Die();
            Add("death_keeps_deployment",G.ExpeditionAwaitingRetry&&store.Profile.activeRun==run&&store.Profile.deployment.deaths==1&&store.MedkitStock==5);
            Add("retry_restores_snapshot",G.RetryExpedition()&&G.expeditionMedkits==2&&store.Profile.activeRun==run&&store.Profile.deployment.attempts==2&&G.deaths==1&&P.health==G.config.playerHealth&&!P.classSkill.MobilityActive);
            P.health=G.config.playerHealth-50;G.UseExpeditionMedkit();
            beforeMoney=store.Profile.credits;
            Add("abandon_restores_post_purchase",G.AbandonExpedition()&&store.MedkitStock==5&&store.Profile.credits==beforeMoney&&store.Profile.deployment==null);
            G.OpenExpeditionBoard();store.SetAttachment(0,0);
            Add("redeploy",G.BeginExpedition(0));
            P.health=G.config.playerHealth-50;G.UseExpeditionMedkit();
            yield return new WaitForSeconds(.15f);yield return Swap();yield return CompleteCalibration();
            run=store.Profile.activeRun;beforeMoney=store.Profile.credits;
            bool settled=G.FinishExpedition(true);
            Add("success_commits_actual_consumption",settled&&store.MedkitStock==4&&store.Profile.credits==beforeMoney+TacticalPrototypeRules.BaseReward(0));
            int settledMoney=store.Profile.credits;
            var receipt=store.Settle(run,true,false,999,1);
            Add("success_receipt_idempotent",store.MedkitStock==4&&store.Profile.credits==settledMoney&&receipt.runId==run);
            var reloaded=new ExpeditionStore(Path.GetDirectoryName(store.SavePath),true);
            Add("reload_committed_state",reloaded.MedkitStock==4&&reloaded.Profile.credits==settledMoney&&reloaded.Profile.deployment==null);
            string interrupted=reloaded.Begin(0);var recovered=new ExpeditionStore(Path.GetDirectoryName(store.SavePath),true);
            Add("interruption_returns_to_preparation",recovered.Profile.deployment==null&&string.IsNullOrEmpty(recovered.Profile.activeRun)&&recovered.MedkitStock==4&&recovered.Profile.credits==settledMoney);
            G.OpenExpeditionBoard();G.expeditionStore=recovered;G.SetExpeditionTabForReview(3);
            yield return new WaitForSecondsRealtime(.3f);
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"procurement.png"));
            yield return new WaitForSecondsRealtime(.3f);
            Finish();
        }
        void Finish()
        {
            if(finished)return;finished=true;Time.timeScale=1;
            report.passed=report.checks.Count>0&&report.checks.TrueForAll(c=>c.passed);
            File.WriteAllText(Path.Combine(output,"t01-report.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.passed?0:1);
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}
