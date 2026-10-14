using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace SwapHunter
{
    // Contract fixtures for SSU-01 only. Controlled positioning/targets are not a human playthrough.
    public sealed class SynergyQA : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, detail; public bool passed; }
        [Serializable] public sealed class Report
        {
            public string version, scope="SSU-01 actual action/Boss contracts, isolated trial/route application, effective stats and attempt accounting. Scripted fixtures, no balance or human-playtest claim.";
            public bool passed, completed, batchMode;
            public int rulesVersion=TacticalPrototypeRules.Version;
            public List<Check> checks=new List<Check>();
        }
        readonly Report report=new Report(); string output; bool done; float started;
        DemoGame G=>DemoGame.I; PlayerMotor P=>G.player;
        void Awake()
        {
            started=Time.realtimeSinceStartup;var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-qaOutput");
            output=i>=0?Path.GetFullPath(args[i+1]):RunStorage.Root;Directory.CreateDirectory(output);
            report.version=Application.version;report.batchMode=Application.isBatchMode;Application.logMessageReceived+=OnLog;
        }
        void OnLog(string m,string trace,LogType t)
        {if(!done&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))Add("runtime_error",false,m+"\n"+trace);}
        void Update(){if(!done&&Time.realtimeSinceStartup-started>420){Add("synergy_timeout",false,"420 seconds");Finish(false);}}
        void Add(string n,bool pass,string detail="")
        {report.checks.Add(new Check{name=n,passed=pass,detail=detail});Debug.Log("SYNERGY_QA "+n+" "+pass+" "+detail);}
        IEnumerator Start()
        {
            if(!Application.isBatchMode)while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            yield return null;Add("synergy_isolated",RunStorage.IsValidation&&G.qaMode);
            if(!RunStorage.IsValidation){Finish(false);yield break;}
            G.qaSuppressAI=true;
            yield return StatsAndAttempts();
            yield return SynergyActionChecks.Run(Add);
            yield return SynergyBossChecks.Run(Add);
            yield return SynergyRouteChecks.Run(Add,output);
            yield return SynergyTrialChecks.Run(Add,output);
            Finish(true);
        }
        IEnumerator StatsAndAttempts()
        {
            var folder=Path.Combine(RunStorage.Root,"SynergyAccounting");Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,"expedition-v6.json"),JsonUtility.ToJson(TacticalDemoProfile.Create(0,true)));
            G.expeditionStore=new ExpeditionStore(folder,true);var store=G.expeditionStore;G.OpenExpeditionBoard();
            var original=G.config;var variant=Instantiate(original);variant.rifleMagazine=30;
            try
            {
                store.SetAttachment(0,2);G.config=variant;Add("P1_deploy_config_capacity",G.BeginExpedition(0)&&P.Capacity(0)==30&&P.ammo[0]==30&&P.reserve[0]==180);
                P.reserve[0]=0;P.reserve[1]=WeaponCatalog.Get(1).reserve;G.expeditionInventory=true;G.SetState(RunState.Paused);
                bool granted=G.UseExpeditionAmmo();var display=WeaponDisplayStats.Build(0,variant);
                Add("P1_display_and_supply_same_effective_magazine",granted&&P.reserve[0]==60&&display.Magazine==30&&G.CurrentAttemptMetrics.actualAmmoGranted==60,
                    "Temporary config clone only: actual capacity=30, display=30, actual supply="+P.reserve[0]);
                G.expeditionInventory=false;G.SetState(RunState.Playing);G.AbandonExpedition();G.OpenExpeditionBoard();
            }
            finally{G.config=original;Destroy(variant);}
            store.SetAttachment(0,0);store.SetConsumableSlot(1,2,1); // medical + armor; separate test from ammo clone
            Add("E1_new_attempt",G.BeginExpedition(0));string run=store.Profile.activeRun;
            P.health=G.config.playerHealth-15;bool healed=G.UseExpeditionMedkit();bool armor=G.UseExpeditionArmor();
            int stock=store.MedkitStock;P.Hurt(10,P.Eye.position+Vector3.back*3,false,PlayerDamageKind.Normal);
            yield return new WaitForSeconds(.16f);G.SetState(RunState.Paused);yield return new WaitForSecondsRealtime(.12f);G.SetState(RunState.Playing);
            var failed=G.CurrentAttemptMetrics;float active=failed.activeSeconds;
            Add("E1_actual_effects_not_nominal",healed&&armor&&Mathf.Abs(failed.effectiveHealing-15)<.01f&&failed.armorAbsorbed>0&&failed.pausedSeconds>.05f,
                "heal="+failed.effectiveHealing+";absorb="+failed.armorAbsorbed+";pause="+failed.pausedSeconds);
            P.health=0;G.Die();Add("E1_failed_attempt_not_permanent_debit",failed.complete&&failed.outcome=="death"&&store.MedkitStock==stock);
            Add("E1_retry_same_run_new_attempt",G.RetryExpedition()&&store.Profile.activeRun==run&&G.CurrentAttemptMetrics.attempt==2&&G.expeditionMedkits==2);
            P.health=G.config.playerHealth-25;G.UseExpeditionMedkit();
            // Enter the chapter's actual trigger, complete a real Q, and explicitly defeat required QA targets.
            P.SetFeet(new Vector3(0,.035f,5));P.verticalSpeed=0;P.cooldown=0;Physics.SyncTransforms();
            var anchor=G.expeditionMap.anchors[0];bool requested=P.RequestSwap(anchor);yield return new WaitForSeconds(.16f);
            Add("E1_real_calibration_q",requested&&G.totalSwaps>0);
            P.SetFeet(new Vector3(0,.035f,22));G.mission.Tick(0);yield return new WaitForSeconds(3.1f);
            foreach(var member in G.mission.Members)if(member.role.required&&member.actor&&member.actor.Alive)member.actor.Damage(100000);
            G.mission.Tick(0);bool settled=G.FinishExpedition(true);var final=G.CurrentAttemptMetrics;
            Add("E1_success_commits_only_final_use",settled&&store.MedkitStock==stock-1&&G.expeditionReceipt.medkitsUsed==1&&G.expeditionReceipt.attempts==2&&G.expeditionReceipt.rulesVersion==TacticalPrototypeRules.Version);
            Add("E1_cumulative_includes_failed_attempt",final.complete&&final.cumulativeActiveSeconds>=active+final.activeSeconds-.001f&&final.cumulativeActiveSeconds>G.expeditionReceipt.seconds,
                "cumulative="+final.cumulativeActiveSeconds+"; finalReceipt="+G.expeditionReceipt.seconds);
            string journal=File.ReadAllText(G.AttemptMetricsPath);Add("E1_separate_start_end_committed_records",journal.Contains("attempt_start")&&journal.Contains("attempt_end")&&journal.Contains("settlement_committed")&&journal.Contains("effectiveHealing"));
            var blocked=Path.Combine(RunStorage.Root,"metrics-parent-file");File.WriteAllText(blocked,"occupied");var fault=new AttemptMetricsJournal(blocked);
            fault.Begin("fault-fixture",1,1,"QA",TacticalPrototypeRules.Version,null);fault.Tick(1,true,false);fault.End("abandon",null);
            Add("E1_metrics_io_failure_explicit_non_transactional",!fault.Current.evidenceComplete&&!string.IsNullOrEmpty(fault.Notice)&&store.MedkitStock==stock-1);
            G.OpenExpeditionBoard();
            var eight=MissionCatalog.Get(7);var ten=MissionCatalog.Get(9);var twelve=MissionCatalog.Get(11);
            bool waves=true;foreach(int second in new[]{15,30,45}){bool found=false;foreach(var group in ten.groups)if(group.trigger==EncounterTrigger.Elapsed&&Mathf.Approximately(group.triggerSeconds,second)&&group.warningSeconds>=4)found=true;waves&=found;}
            Add("M1_preserved_mission_contracts",eight.requiredObjectives==2&&waves&&twelve.maximumEscorts==2,
                "Chapter 8 pick two, chapter 10 waves at 15/30/45 with >=4s warning, Boss escort budget cap two; no new route/performance claim.");
        }
        void Finish(bool complete)
        {
            if(done)return;done=true;report.completed=complete;report.passed=complete&&report.checks.Count>0&&report.checks.TrueForAll(c=>c.passed);
            File.WriteAllText(Path.Combine(output,"synergy-report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}
