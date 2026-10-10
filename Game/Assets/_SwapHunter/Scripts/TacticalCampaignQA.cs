using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace SwapHunter
{
    public sealed class TacticalCampaignQA : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name,detail;public bool passed; }
        [Serializable] public sealed class Report
        {
            public string scope="D1.1 focused native data/economy/migration and skill interactions; explicit isolated fixtures; not human combat, whole-campaign balance or FPS evidence";
            public bool passed;public int rulesVersion=TacticalPrototypeRules.Version;
            public List<Check> checks=new List<Check>();
        }
        readonly Report report=new Report();string output;float started;bool done;
        void Awake()
        {
            started=Time.realtimeSinceStartup;var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-qaOutput");
            output=at>=0&&at+1<args.Length?args[at+1]:RunStorage.Root;Directory.CreateDirectory(output);
            Application.logMessageReceived+=OnLog;
        }
        void OnLog(string message,string stack,LogType type)
        {if(!done&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))Add("runtime_error",false,message+"\n"+stack);}
        void Update(){if(!done&&Time.realtimeSinceStartup-started>360){Add("timeout",false,"360 seconds");Finish();}}
        void Add(string name,bool passed,string detail="")
        {report.checks.Add(new Check{name=name,passed=passed,detail=detail});Debug.Log("TACTICAL_QA "+name+" "+passed+" "+detail);}
        IEnumerator Start()
        {
            yield return null;Add("isolated_full_validation",RunStorage.IsFullValidation&&RunStorage.IsIsolated);
            if(!RunStorage.IsFullValidation){Finish();yield break;}
            bool setup=false;
            try
            {
                TacticalStorageChecks.Run(Path.Combine(output,"storage-fixtures"),Add);
                var g=DemoGame.I;string folder=Path.Combine(RunStorage.Root,"FullRuntimeFixture");Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder,"expedition-v6.json"),JsonUtility.ToJson(TacticalDemoProfile.Create(0,true),true));
                g.expeditionStore=new ExpeditionStore(folder,true);g.OpenExpeditionBoard();g.qaSuppressAI=true;setup=true;
                Add("runtime_fixture_explicit",g.expeditionStore.Profile.credits==10000,"all weapons/skills granted only in isolated fixture; no completion records");
            }
            catch(Exception error){Add("setup_exception",false,error.ToString());}
            if(setup)yield return TacticalSkillChecks.Run(DemoGame.I,Add);
            Finish();
        }
        void Finish()
        {
            if(done)return;done=true;Time.timeScale=1;
            report.passed=report.checks.Count>0&&report.checks.TrueForAll(c=>c.passed);
            File.WriteAllText(Path.Combine(output,"tactical-campaign-report.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.passed?0:1);
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}
