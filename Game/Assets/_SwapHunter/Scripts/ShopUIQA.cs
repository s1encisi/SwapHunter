using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace SwapHunter
{
 public sealed class ShopUIQA:MonoBehaviour
 {
  [Serializable] public class Check{public string name,detail;public bool passed;}
  [Serializable] public class Report{public string kind="Native keyboard-driven menu/shop plus persistent research cost, write failure, migration and active-run protection";public bool passed;public List<Check> checks=new List<Check>();}
  Report report=new Report();bool done;float started;string output;Keyboard keys;DemoGame G=>DemoGame.I;
  void Awake(){started=Time.realtimeSinceStartup;Application.logMessageReceived+=Error;}
  void Error(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)Add("runtime_error",false,m);}
  void Add(string n,bool ok,string d){report.checks.Add(new Check{name=n,passed=ok,detail=d});Debug.Log("SHOP_UI_QA "+n+" "+ok+" "+d);}
  void Update(){if(!done&&Time.realtimeSinceStartup-started>90){Add("watchdog",false,"90s");Finish();}}
  IEnumerator Press(Key k){InputSystem.QueueStateEvent(keys,new KeyboardState(k));yield return null;yield return null;InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;yield return null;}
  IEnumerator Focus(string text){int tries=0;while(!G.FocusedButton.StartsWith(text)&&tries++<35)yield return Press(Key.Tab);Add("focus_"+text,G.FocusedButton.StartsWith(text),G.FocusedButton);}
  IEnumerator Capture(string n){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,n+".png"));yield return null;}
  IEnumerator Start()
  {
   var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-qaOutput");output=i>=0?a[i+1]:RunStorage.Root;Directory.CreateDirectory(output);
   while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
   keys=InputSystem.AddDevice<Keyboard>();G.qaKeyboard=keys;G.qaInputEnabled=true;yield return new WaitForSeconds(.3f);
   yield return Focus("开始相位行动");yield return Capture("01-menu-keyboard-focus");yield return Press(Key.Enter);Add("keyboard_opens_action_board",G.expeditionBoard,"board="+G.expeditionBoard);
   var store=G.expeditionStore;string run=store.Begin(0);store.Settle(run,true,false,30);
   yield return Focus("战术工具");yield return Press(Key.Enter);yield return Capture("02-tool-shop");
   yield return Focus("研究 / 80");yield return Press(Key.Enter);
   Add("keyboard_purchase_spends_exact_cost",store.Profile.knifeRank==1&&store.Profile.credits==0,"rank="+store.Profile.knifeRank+";credits="+store.Profile.credits);
   int credits=store.Profile.credits;bool rejected=false;try{store.UpgradeKnife();}catch(InvalidOperationException){rejected=true;}
   Add("insufficient_funds_preserve_rank_and_credits",rejected&&store.Profile.knifeRank==1&&store.Profile.credits==credits,"rank="+store.Profile.knifeRank);
   var reloaded=new ExpeditionStore(Path.GetDirectoryName(store.SavePath));Add("research_survives_reload",reloaded.Profile.knifeRank==1&&reloaded.Profile.credits==0,store.SavePath);
   yield return Capture("03-disabled-insufficient-credit");
   for(int level=1;level<5;level++){run=store.Begin(level);store.Settle(run,true,false,40);}
   int before=store.Profile.credits;store.UpgradeKnife();store.UpgradeKnife();Add("sequential_three_tier_costs",store.Profile.knifeRank==3&&store.Profile.credits==before-140-220,"credits="+store.Profile.credits);
   rejected=false;try{store.UpgradeKnife();}catch(InvalidOperationException){rejected=true;}Add("full_rank_cannot_charge_again",rejected&&store.Profile.credits==before-360,"rank="+store.Profile.knifeRank);
   // An independent store tests locked writes and old files without the newly added rank field.
   string folder=Path.Combine(output,"shop-data-fixture");var s=new ExpeditionStore(folder);run=s.Begin(0);s.Settle(run,true,false,30);string original=File.ReadAllText(s.SavePath);bool failed=false;
   using(var locked=new FileStream(s.SavePath,FileMode.Open,FileAccess.Read,FileShare.None)){try{s.UpgradeKnife();}catch(IOException){failed=true;}}
   Add("failed_purchase_keeps_memory_and_original",failed&&s.Profile.knifeRank==0&&s.Profile.credits==80&&File.ReadAllText(s.SavePath)==original,"atomic write preserved");
   run=s.Begin(1);rejected=false;try{s.UpgradeKnife();}catch(InvalidOperationException){rejected=true;}Add("no_permanent_purchase_during_combat",rejected&&s.Profile.knifeRank==0,"active run locked");s.Settle(run,false,false,0);
   string legacy=File.ReadAllText(s.SavePath).Replace("\"knifeRank\": 0,","");File.WriteAllText(s.SavePath,legacy);s=new ExpeditionStore(folder);Add("v4_profile_defaults_new_knife_research",s.Profile.knifeRank==0&&s.Profile.credits==80,"old profile remains valid");
   G.qaInputEnabled=false;G.qaKeyboard=null;InputSystem.RemoveDevice(keys);Finish();
  }
  void Finish(){if(done)return;done=true;report.passed=report.checks.Count>0&&report.checks.TrueForAll(x=>x.passed);File.WriteAllText(Path.Combine(output,"shop-ui-report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);}
  void OnDestroy(){Application.logMessageReceived-=Error;}
 }
}
