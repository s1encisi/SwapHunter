using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace SwapHunter
{
 public sealed class ExpeditionRouteRunner:MonoBehaviour
 {
  [Serializable] public sealed class Check {public string name,detail;public bool passed;}
  [Serializable] public sealed class Report {public string version="0.4.0",kind="Native virtual keyboard locomotion and real swap route verification; AI combat isolated, not human playtesting";public bool passed;public List<Check> checks=new List<Check>();}
  readonly Report report=new Report();float start;string output;bool done;
  void Awake(){start=Time.realtimeSinceStartup;Application.logMessageReceived+=Error;}
  void Update(){if(!done&&Time.realtimeSinceStartup-start>1800){CheckThat("watchdog",false,"route check exceeded 1800 seconds");Finish();}}
  void Error(string message,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)CheckThat("runtime_error",false,message);}
  void CheckThat(string name,bool passed,string detail){report.checks.Add(new Check{name=name,passed=passed,detail=detail});Debug.Log("ROUTE_QA "+name+" "+passed+" "+detail);}
  IEnumerator Start()
  {
   var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-qaOutput");output=i>=0&&i+1<args.Length?args[i+1]:RunStorage.Root;Directory.CreateDirectory(output);
   while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
   yield return new WaitForSecondsRealtime(.5f);
   yield return ExpeditionRouteQA.Run(DemoGame.I,CheckThat);Finish();
  }
  void Finish(){if(done)return;done=true;Time.timeScale=1;report.passed=report.checks.Count>0&&report.checks.TrueForAll(c=>c.passed);File.WriteAllText(Path.Combine(output,"route-report.json"),JsonUtility.ToJson(report,true));Debug.Log("ROUTE_QA_RESULT="+report.passed);Application.Quit(report.passed?0:1);}
  void OnDestroy(){Application.logMessageReceived-=Error;}
 }
}

