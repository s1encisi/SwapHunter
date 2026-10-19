using System;using System.Collections;using System.Collections.Generic;using System.IO;
using UnityEngine;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;
namespace SwapHunter
{
 public sealed class FocusRefinementQA:MonoBehaviour
 {
  [Serializable]public sealed class Check{public string name,detail;public bool passed;}
  [Serializable]public sealed class Report{public string scope="Focused chapter 1/12 navigation and native views, health/range change, wheel binding and new hand/knife motion. Scripted checks, not a human playthrough.";public string group;public bool completed,passed;public List<Check> checks=new List<Check>();}
  Report report=new Report();string output;bool done;float began;DemoGame G=>DemoGame.I;
  void Awake(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-qaOutput");output=i>=0?Path.GetFullPath(args[i+1]):RunStorage.Root;Directory.CreateDirectory(output);i=Array.IndexOf(args,"-focusGroup");report.group=i>=0?args[i+1]:"all";began=Time.realtimeSinceStartup;Application.logMessageReceived+=Log;}
  void Log(string m,string trace,LogType type){if(!done&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))Add("runtime_error",false,m+"\n"+trace);}
  void Add(string name,bool passed,string detail=""){report.checks.Add(new Check{name=name,passed=passed,detail=detail});Debug.Log("FOCUS_QA "+name+" "+passed+" "+detail);}
  bool Group(string group)=>report.group=="all"||report.group==group||(report.group=="polish"&&group!="systems");
  IEnumerator Start()
  {
   if(!Application.isBatchMode)while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;yield return null;
   Add("isolated",RunStorage.IsValidation&&G.qaMode);if(!RunStorage.IsValidation){Finish(false);yield break;}G.qaSuppressAI=true;
   if(report.group=="polish")yield return Guard(WheelInput());
   if(Group("systems")){yield return Guard(WheelInput());yield return Guard(HealthRangeRebalanceChecks.Run(Add));}
   if(Group("maps"))yield return Guard(MapV2GeometryChecks.Run(Add,output,new[]{0,11}));
   if(Group("equipment"))
   {
    if(G.StoryModal)G.CloseStoryModal();if(G.expeditionActive&&G.expeditionReceipt==null)G.AbandonExpedition();G.OpenExpeditionBoard();
    string dir=Path.Combine(RunStorage.Root,"focused-equipment");Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"expedition-v6.json"),JsonUtility.ToJson(TacticalDemoProfile.CreateFullPreview()));G.expeditionStore=new ExpeditionStore(dir,true);G.BeginExpedition(0);
    yield return Guard(FocusedEquipmentChecks.Run(Add,output));
   }
   if(Group("combat"))yield return Guard(MapV2PerformanceSample.Run(Add,output,new[]{11}));
   Finish(true);
  }
  IEnumerator WheelInput()
  {
   if(G.StoryModal)G.CloseStoryModal();G.OpenExpeditionBoard();var oldOptions=G.options;bool oldInput=G.qaInputEnabled;var oldKeys=G.qaKeyboard;var oldMouse=G.qaMouse;
   var keys=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();G.qaKeyboard=keys;G.qaMouse=mouse;G.qaInputEnabled=true;
   try
   {
    G.options=oldOptions.Clone();G.options.actions=GameOptions.DefaultBindings();G.options.resolutionWidth=Screen.width;G.options.resolutionHeight=Screen.height;
    G.OpenSettings();G.SelectSettingsTab(1);G.BeginBindingCapture(4,1);
    // Test the user's quick downward tick immediately after selecting a slot.
    InputSystem.QueueStateEvent(mouse,new MouseState{scroll=new Vector2(0,-120)});yield return null;
    Add("wheel_down_captured_immediately",G.BindingConflictVisible,"Default next-weapon binding correctly requests explicit exchange.");
    yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"settings-wheel-conflict.png"));yield return null;
    G.ResolveBindingConflict(true);Add("wheel_down_rebind_exchanges",G.PendingOptions.actions[4].secondary=="W:Down"&&string.IsNullOrEmpty(G.PendingOptions.actions[15].primary));
    G.ApplyDraft();var saved=SettingsStore.LoadFromPath(SettingsStore.FilePath);Add("wheel_down_binding_persisted",saved.actions[4].secondary=="W:Down");
    yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"settings-wheel-bound.png"));yield return null;G.CancelSettings();
    InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;InputSystem.QueueStateEvent(mouse,new MouseState{scroll=new Vector2(0,-120)});yield return null;
    Add("wheel_down_triggers_bound_action",G.Pressed(4)&&!G.Pressed(15),"InputSystem wheel impulse reaches jump and no longer switches weapon.");
    InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;
   }
   finally{G.CancelSettings();G.options=oldOptions;G.ApplySettings(false);G.qaInputEnabled=oldInput;G.qaKeyboard=oldKeys;G.qaMouse=oldMouse;InputSystem.RemoveDevice(keys);InputSystem.RemoveDevice(mouse);}
  }
  IEnumerator Guard(IEnumerator operation){while(true){bool more;object current;try{more=operation.MoveNext();current=more?operation.Current:null;}catch(Exception error){Add("fixture_exception",false,error.ToString());yield break;}if(!more)yield break;if(current is IEnumerator child)yield return Guard(child);else yield return current;}}
  void Update(){if(!done&&Time.realtimeSinceStartup-began>600){Add("timeout",false);Finish(false);}}
  void Finish(bool complete){if(done)return;done=true;report.completed=complete;report.passed=complete&&report.checks.TrueForAll(c=>c.passed);File.WriteAllText(Path.Combine(output,"focus-refinement-report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);}
  void OnDestroy(){Application.logMessageReceived-=Log;}
 }
}