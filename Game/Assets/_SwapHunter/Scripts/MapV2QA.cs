using System;using System.Collections;using System.Collections.Generic;using System.IO;using UnityEngine;
namespace SwapHunter{
 public sealed class MapV2QA:MonoBehaviour{
  [Serializable]public class Check{public string name,detail;public bool passed;}
  [Serializable]public class Report{public string version,group,scope="Map/enemy/player v2 focused native fixtures; route checks are not human campaign playthroughs.";public bool completed,passed,batchMode;public List<Check> checks=new List<Check>();}
  Report report=new Report();string output;bool done;float began;
  void Awake(){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-qaOutput");output=i>=0?Path.GetFullPath(a[i+1]):RunStorage.Root;Directory.CreateDirectory(output);i=Array.IndexOf(a,"-mapV2Group");report.group=i>=0?a[i+1]:"all";report.version=Application.version;report.batchMode=Application.isBatchMode;began=Time.realtimeSinceStartup;Application.logMessageReceived+=Log;}
  void Log(string m,string t,LogType k){if(!done&&(k==LogType.Error||k==LogType.Exception||k==LogType.Assert))Add("runtime_error",false,m+"\n"+t);}
  void Add(string name,bool pass,string detail=""){report.checks.Add(new Check{name=name,passed=pass,detail=detail});Debug.Log("MAPV2_QA "+name+" "+pass+" "+detail);}
  bool Group(string g)=>report.group=="all"||report.group==g;
  IEnumerator Start(){if(!Application.isBatchMode)while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;yield return null;Add("isolated",RunStorage.IsValidation&&DemoGame.I.qaMode);if(!RunStorage.IsValidation){Finish(false);yield break;}DemoGame.I.qaSuppressAI=true;
   if(Group("maps"))yield return Guard(MapV2GeometryChecks.Run(Add,output));
   if(Group("player"))yield return Guard(PlayerRulesV2Checks.Run(Add));
   if(Group("equipment")){
    var g=DemoGame.I;if(g.expeditionActive&&g.expeditionReceipt==null)g.AbandonExpedition();g.OpenExpeditionBoard();
    string dir=Path.Combine(RunStorage.Root,"equipment-v2");Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"expedition-v6.json"),JsonUtility.ToJson(TacticalDemoProfile.Create(0,true)));g.expeditionStore=new ExpeditionStore(dir,true);g.BeginExpedition(0);
    yield return Guard(PlayerEquipmentV2Checks.Run(Add));
    yield return Guard(CaptureArmory());
   }
   if(Group("enemies"))yield return Guard(EnemyV2Checks.Run(Add));
   if(Group("showcase"))yield return Guard(EnemyV2Showcase.Run(Add,output));
   if(Group("performance"))yield return Guard(MapV2PerformanceSample.Run(Add,output));
   Finish(true);
  }
  IEnumerator CaptureArmory(){
   if(Application.isBatchMode)yield break;var g=DemoGame.I;if(g.expeditionActive&&g.expeditionReceipt==null)g.AbandonExpedition();g.OpenExpeditionBoard();
   string dir=Path.Combine(RunStorage.Root,"armory-visual");Directory.CreateDirectory(dir);var seed=TacticalDemoProfile.Create(0,true);seed.meleeKnives=new[]{true,true,true,true,true,true};
   File.WriteAllText(Path.Combine(dir,"expedition-v6.json"),JsonUtility.ToJson(seed));g.expeditionStore=new ExpeditionStore(dir,true);g.SetExpeditionTabForReview(5);yield return new WaitForSecondsRealtime(.6f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"equipment-six-knife-armory.png"));yield return null;
   Add("knife_armory_rendered",g.GetComponent<KnifeArmoryPreviewV2>()!=null,"Actual six source meshes; isolated preview ownership only.");g.SetExpeditionTabForReview(0);
  }
  IEnumerator Guard(IEnumerator operation){while(true){bool more;object current;try{more=operation.MoveNext();current=more?operation.Current:null;}catch(Exception e){Add("fixture_exception",false,e.ToString());yield break;}if(!more)yield break;if(current is IEnumerator nested)yield return Guard(nested);else yield return current;}}
  void Update(){if(!done&&Time.realtimeSinceStartup-began>900){Add("timeout",false,"900 seconds");Finish(false);}}
  void Finish(bool complete){if(done)return;done=true;report.completed=complete;report.passed=complete&&report.checks.Count>0&&report.checks.TrueForAll(c=>c.passed);File.WriteAllText(Path.Combine(output,"map-v2-report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);}
  void OnDestroy(){Application.logMessageReceived-=Log;}
 }
}

