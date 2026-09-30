using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace SwapHunter
{
 public sealed partial class ReviewFixQA:MonoBehaviour
 {
  [Serializable] public class CheckResult { public string name,detail; public bool passed; }
  [Serializable] public class Report { public string version,group,kind="Native regression fixtures for the independent review findings; scripted setup is not human playtesting";public bool passed,batchMode;public string visualEvidence;public List<CheckResult> checks=new List<CheckResult>(); }
  readonly Report report=new Report();string output;float started;bool done;
  DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
  void Awake(){started=Time.realtimeSinceStartup;Application.logMessageReceived+=Error;}
  void Error(string m,string trace,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)Check("runtime_error",false,m+"\n"+trace);}
  void Check(string name,bool passed,string detail){report.checks.Add(new CheckResult{name=name,passed=passed,detail=detail});Debug.Log("REVIEW_FIX_QA "+name+" "+passed+" "+detail);}
  void Update(){float limit=report.group=="flow"?650:240;if(!done&&Time.realtimeSinceStartup-started>limit){Check("watchdog",false,limit+" seconds");Finish();}}
  IEnumerator Capture(string name){if(Application.isBatchMode){yield return null;yield break;}yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;}
  void Aim(Vector3 target){var d=target-P.Eye.position;P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);}
  void ClearActors(){G.ClearDeployments();foreach(var e in G.enemies.ToArray())if(e){e.gameObject.SetActive(false);Destroy(e.gameObject);}G.enemies.Clear();foreach(var k in FindObjectsByType<ThrownKnife>(FindObjectsSortMode.None)){k.gameObject.SetActive(false);Destroy(k.gameObject);}foreach(var b in FindObjectsByType<Bolt>(FindObjectsSortMode.None)){b.gameObject.SetActive(false);Destroy(b.gameObject);}P.combatTools.ResetCombat();P.tactics.ResetCombat();}
  IEnumerator Start(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-qaOutput");output=i>=0?Path.GetFullPath(args[i+1]):RunStorage.Root;Directory.CreateDirectory(output);i=Array.IndexOf(args,"-reviewGroup");report.group=i>=0?args[i+1]:"all";report.version=Application.version;report.batchMode=Application.isBatchMode;report.visualEvidence=report.batchMode?"No screenshots or foreground performance evidence in batch mode":"Rendered player screenshots";while(!Application.isBatchMode&&!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
   Check("validation_storage_isolated",RunStorage.IsValidation&&G.campaignStore!=null&&G.campaignStore.SavePath.StartsWith(output,StringComparison.OrdinalIgnoreCase),"runtime="+RunStorage.Root+"; campaign="+(G.campaignStore==null?"missing":G.campaignStore.SavePath));
   if(report.group=="storage"){Check("interactive_input_mode_preserved",!G.qaMode,"qaMode="+G.qaMode);Finish();yield break;}
   if(report.group=="flow"){yield return FlowChecks();Finish();yield break;}
   if(report.group!="all"&&report.group!="core"&&report.group!="systems"&&report.group!="levels"){Check("known_review_group",false,report.group);Finish();yield break;}
   if(report.group=="core"||report.group=="all")yield return CoreChecks();
   if(report.group=="systems"||report.group=="all")yield return SystemsChecks();
   if(report.group=="levels"||report.group=="all")yield return LevelChecks();
   Finish();
  }
  void Finish(){if(done)return;done=true;report.passed=report.checks.Count>0&&report.checks.TrueForAll(c=>c.passed);File.WriteAllText(Path.Combine(output,"review-fixes-report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);}
  void OnDestroy(){Application.logMessageReceived-=Error;}
 }
}
