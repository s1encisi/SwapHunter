using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace SwapHunter
{
 public sealed class TacticalUIQA:MonoBehaviour
 {
  [Serializable] public sealed class Check{public string name,detail;public bool passed;}
  [Serializable] public sealed class Report
  {
   public string scope="Windowed native keyboard navigation and rendered captures; scripted fixtures, not manual mouse or first-time human testing";
   public bool passed,batchMode;public int focusedCaptures;public List<Check> checks=new List<Check>();public List<string> images=new List<string>();
  }
  [Serializable] public sealed class Performance
  {
   public string scope="Single current-build windowed chapter12 stationary combat fixture; high player health; no before/after speedup claim";
   public string device,cpu,gpu,quality;public int width,height,samples,livingEnemies,focusedSamples,vSync,targetFrameRate;public bool bossActive;public float meanMs,medianMs,p95Ms,maximumMs;public bool foreground;
  }
  GUIStyle reviewStyle;readonly Report report=new Report();string output;Keyboard keys;bool done,clicked;float started;
  DemoGame G=>DemoGame.I;
  void Awake()
  {
   started=Time.realtimeSinceStartup;report.batchMode=Application.isBatchMode;var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-qaOutput");
   output=at>=0&&at+1<args.Length?args[at+1]:RunStorage.Root;Directory.CreateDirectory(output);
   Application.logMessageReceived+=OnLog;
  }
  void OnLog(string m,string s,LogType t){if(!done&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))Add("runtime_error",false,m);}
  void Update(){if(!done&&Time.realtimeSinceStartup-started>180){Add("ui_timeout",false,G?G.FocusedButton:"no game");Finish();}}
  void Add(string n,bool p,string d=""){report.checks.Add(new Check{name=n,passed=p,detail=d});Debug.Log("TACTICAL_UI "+n+" "+p+" "+d);}
  IEnumerator Tap(Key key)
  {
   InputSystem.QueueStateEvent(keys,new KeyboardState(key));yield return null;yield return null;
   InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;
  }
  static float ButtonX(string id)
  {int at=id.LastIndexOf('@');if(at<0)return -1;return float.TryParse(id.Substring(at+1).Split(',')[0],out float x)?x:-1;}
  IEnumerator Click(string label,float minX=-1,float maxX=99999)
  {
   clicked=false;yield return Tap(Key.Tab);
   for(int i=0;i<85;i++)
   {
    string focus=G.FocusedButton;
    if(focus.StartsWith(label+"@",StringComparison.Ordinal)&&ButtonX(focus)>minX&&ButtonX(focus)<maxX)
    {yield return Tap(Key.Enter);clicked=true;yield break;}
    yield return Tap(Key.Tab);
   }
   Add("button_not_reached",false,label+";last="+G.FocusedButton);
  }
  IEnumerator Capture(string name)
  {
   yield return new WaitForSecondsRealtime(.45f);
   ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));
   report.images.Add(name+".png");if(Application.isFocused)report.focusedCaptures++;yield return new WaitForSecondsRealtime(.2f);
  }
  IEnumerator Start()
  {
   if(Application.isBatchMode){Add("requires_foreground",false,"Run without -batchmode");Finish();yield break;}
   while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
   keys=InputSystem.AddDevice<Keyboard>();G.qaKeyboard=keys;G.qaInputEnabled=true;G.qaSuppressAI=true;
   G.OpenExpeditionBoard();G.SetExpeditionTabForReview(2);yield return new WaitForSecondsRealtime(.6f);
   yield return Click("重装兵");Add("class_switch_via_keyboard",clicked&&G.expeditionStore.ActiveClass==1);
   yield return Click("免费领取本职业武器");Add("weapon_guarantee_button",clicked&&G.expeditionStore.HasWeaponStarter);
   yield return Click("领取保障");Add("class_skill_guarantee_button",clicked&&G.expeditionStore.EquippedClassSkill==2);
   yield return Click("领取保障");Add("general_guarantee_button",clicked&&G.expeditionStore.EquippedGeneralSkill==0);
   G.expeditionStore.Profile.credits=1000;G.Record("ui_fixture","1000 test credits injected into isolated UI fixture; not earned progress");
   yield return Click("购买 / 100");Add("skill_purchase_button",clicked&&G.expeditionStore.SkillLevel(3)==1&&G.expeditionStore.Profile.credits==900);
   yield return Click("装配");Add("skill_equip_button",clicked&&G.expeditionStore.EquippedClassSkill==3);
   yield return Click("强化 / 100");Add("skill_upgrade_button",clicked&&G.expeditionStore.SkillLevel(2)==2&&G.expeditionStore.Profile.credits==800);
   yield return Capture("class-heavy");
   yield return Click("补给采购");
   yield return Click("购买 1 个",-1,300);yield return Click("购买 1 个",-1,300);
   yield return Click("购买 1 个",300,900);
   Add("three_type_shop_purchase",G.expeditionStore.ConsumableStock(0)==2&&G.expeditionStore.ConsumableStock(1)==1&&G.expeditionStore.Profile.credits==735);
   yield return Click("医疗包",-1,700);yield return Click("＋",-1,800);
   yield return Click("弹药包",800,99999);
   Add("two_distinct_slots_ui",G.expeditionStore.ConsumableCarry(0)==2&&G.expeditionStore.ConsumableCarry(1)==1);
   yield return Capture("procurement");
   yield return Click("武器装配");yield return Click("重型突击步枪");yield return new WaitForSecondsRealtime(.7f);
   yield return Click("右转");
   Add("weapon_preview_navigation",clicked&&G.WeaponLibraryPreview&&G.WeaponLibraryPreview.SelectedId==3&&G.WeaponLibraryPreview.Yaw>0);
   yield return Capture("weapon-model-and-bars");
   yield return Click("职业技能");yield return Click("远程兵");yield return Capture("class-ranged");
   Add("class_preview_render_target",G.GetComponent<ClassModelPreview>()&&G.GetComponent<ClassModelPreview>().Texture);
   G.qaInputEnabled=false;G.qaKeyboard=null;InputSystem.RemoveDevice(keys);keys=null;
   string folder=Path.Combine(RunStorage.Root,"PresentationFixture");Directory.CreateDirectory(folder);
   File.WriteAllText(Path.Combine(folder,"expedition-v6.json"),JsonUtility.ToJson(TacticalDemoProfile.Create(0),true));
   G.expeditionStore=new ExpeditionStore(folder,true);
   foreach(int level in new[]{4,6,11})
   {
    if(G.expeditionActive){G.AbandonExpedition();G.OpenExpeditionBoard();}
    bool began=G.BeginExpedition(level);Add("representative_scene_"+(level+1),began,G.expeditionNotice);
    if(!began)continue;
    G.player.SetFeet(new Vector3(0,.03f,level==11?14:8));G.player.SetLook(0,-7);
    yield return new WaitForSecondsRealtime(.8f);yield return Capture("chapter-"+(level+1));
   }
   G.player.health=10000;G.qaSuppressAI=false;
   yield return new WaitForSecondsRealtime(3.5f);
   bool bossActive=false;foreach(var enemy in G.enemies)if(enemy&&enemy.boss){enemy.Damage(BossEncounter.ShieldMaximum+BossEncounter.HealthMaximum*.55f);bossActive=enemy.Alive;break;}
   Add("performance_boss_fixture",bossActive,"stationary camera; player health raised only to complete sample");
   yield return new WaitForSecondsRealtime(3);
   var samples=new List<float>(2048);int focusedSamples=0;float until=Time.realtimeSinceStartup+12;
   while(Time.realtimeSinceStartup<until){samples.Add(Time.unscaledDeltaTime*1000);if(Application.isFocused)focusedSamples++;yield return null;}
   if(samples.Count>0)
   {
    float total=0;foreach(float value in samples)total+=value;samples.Sort();
    var perf=new Performance{device=SystemInfo.deviceModel,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,width=Screen.width,height=Screen.height,
     quality=QualitySettings.names[QualitySettings.GetQualityLevel()],samples=samples.Count,focusedSamples=focusedSamples,foreground=focusedSamples==samples.Count,vSync=QualitySettings.vSyncCount,targetFrameRate=Application.targetFrameRate,bossActive=bossActive,livingEnemies=G.enemies.FindAll(e=>e&&e.Alive).Count,meanMs=total/samples.Count,medianMs=samples[samples.Count/2],
     p95Ms=samples[Mathf.Min(samples.Count-1,(int)(samples.Count*.95f))],maximumMs=samples[samples.Count-1]};
    File.WriteAllText(Path.Combine(output,"foreground-performance.json"),JsonUtility.ToJson(perf,true));
   }
   yield return Capture("boss-combat");Finish();
  }
  void Finish()
  {
   if(done)return;done=true;Time.timeScale=1;
   report.passed=report.checks.Count>0&&report.checks.TrueForAll(c=>c.passed);
   File.WriteAllText(Path.Combine(output,"tactical-ui-report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);
  }
  void OnGUI(){if(!done&&G&&G.font){if(reviewStyle==null){reviewStyle=new GUIStyle(GUI.skin.label){font=G.font,fontSize=14};reviewStyle.normal.textColor=Color.cyan;}GUI.Label(new Rect(12,Screen.height-25,780,24),"脚本驱动窗口验收 · 独立临时档 · 物品与测试资金预置",reviewStyle);}}
  void OnDestroy(){Application.logMessageReceived-=OnLog;if(keys!=null)InputSystem.RemoveDevice(keys);}
 }
}
