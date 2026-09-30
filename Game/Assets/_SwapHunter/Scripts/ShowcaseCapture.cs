using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace SwapHunter
{
 public sealed partial class ShowcaseCapture:MonoBehaviour
 {
  [Serializable] public class ActionEvent{public string type,detail;public float seconds;}
  [Serializable] public class SoundEvent{public string id;public float seconds,volume,pitch,pan;}
  [Serializable] public class Clip{public string id,description,setup,disclosure;public int width,height,fps=30,frames,swaps,shots,shieldBlocks,returnHits,unexpectedTeleports;public float duration,healthStart,healthEnd,wallCaptureSeconds;public bool passed;public List<SoundEvent> sounds=new List<SoundEvent>();public List<ActionEvent> actions=new List<ActionEvent>();}
  [Serializable] public class Report{public string version,kind="Native Unity dynamic frame capture with scripted input. Setup occurs between clips; no test teleport or direct kill inside a clip. Audio is reconstructed from actual emitted game sound events.";public bool passed;public List<Clip> clips=new List<Clip>();public List<string> errors=new List<string>();}
  public static ShowcaseCapture Active;
  Report report=new Report();Clip clip;Keyboard keyboard;string output,only;float clipBegan,started;bool done;
  DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
  void Awake(){started=Time.realtimeSinceStartup;Application.logMessageReceived+=Error;}
  void Error(string m,string trace,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)report.errors.Add(m);}
  void Update(){if(!done&&Time.realtimeSinceStartup-started>1800){report.errors.Add("Capture watchdog 1800s");Finish();}}
  public static void Event(string type,string detail)
  {if(Active&&Active.clip!=null)Active.clip.actions.Add(new ActionEvent{type=type,detail=detail,seconds=Time.time-Active.clipBegan});}
  public static void Sound(string id,float volume,float pitch,Vector3? source)
  {
   if(!Active||Active.clip==null)return;float pan=0,gain=volume;
   if(source.HasValue&&Active.P)
   {Vector3 offset=source.Value-Active.P.Eye.position;gain*=Mathf.Clamp01(2.5f/Mathf.Max(2.5f,offset.magnitude));pan=Vector3.Dot(offset.normalized,Active.P.Eye.right)*.65f;}
   Active.clip.sounds.Add(new SoundEvent{id=id,seconds=Time.time-Active.clipBegan,volume=gain,pitch=pitch,pan=pan});
  }
  void Keys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
  void Look(Vector3 point,bool smooth=true)
  {
   Vector3 d=point-P.Eye.position;float yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,pitch=-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg;
   P.SetLook(smooth?Mathf.MoveTowardsAngle(P.yaw,yaw,330*Time.deltaTime):yaw,smooth?Mathf.MoveTowards(P.pitch,pitch,200*Time.deltaTime):pitch);
  }
  IEnumerator Press(Key key,float seconds=.04f){Keys(key);yield return new WaitForSeconds(seconds);Keys();yield return null;}
  IEnumerator Record(string id,float seconds,string description,string setup,string disclosure,Action<float> drive)
  {
   if(!Need(id))yield break;
   Keys();yield return new WaitForSeconds(.2f);
   var record=new Clip{id=id,description=description,setup=setup,disclosure=disclosure,width=Screen.width,height=Screen.height,duration=seconds,frames=Mathf.RoundToInt(seconds*30),healthStart=P.health};
   int swaps=P.swapCount,shots=G.totalShots,blocks=P.tactics.ShieldBlocks,returns=P.combatTools.ReturnHits;
   Vector3 previous=P.transform.position;int previousSwaps=P.swapCount;float wallStart=Time.realtimeSinceStartup;
   string frames=Path.Combine(output,id);Directory.CreateDirectory(frames);clip=record;clipBegan=Time.time;Active=this;
   for(int frame=0;frame<record.frames;frame++)
   {
    drive(frame/30f);yield return new WaitForEndOfFrame();
    if(Vector3.Distance(previous,P.transform.position)>2&&P.swapCount==previousSwaps)record.unexpectedTeleports++;
    previous=P.transform.position;previousSwaps=P.swapCount;
    ScreenCapture.CaptureScreenshot(Path.Combine(frames,frame.ToString("D5")+".png"));
    if(frame==95){var compare=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,id+"-texture-compare.png"),compare.EncodeToPNG());Destroy(compare);}
    yield return null;
   }
   clip=null;Active=null;Keys();yield return new WaitForSecondsRealtime(.4f);record.healthEnd=P.health;record.wallCaptureSeconds=Time.realtimeSinceStartup-wallStart;
   record.swaps=P.swapCount-swaps;record.shots=G.totalShots-shots;record.shieldBlocks=P.tactics.ShieldBlocks-blocks;record.returnHits=P.combatTools.ReturnHits-returns;
   record.passed=record.unexpectedTeleports==0&&P.health>0&&Directory.GetFiles(frames,"*.png").Length==record.frames;
   if(id=="02-shoot-swap")record.passed&=record.swaps>0&&record.shots>0;
   if(id=="03-air-swap")record.passed&=record.actions.Exists(e=>e.type=="swap_mode"&&e.detail=="air");
   if(id=="04-launch")record.passed&=record.swaps>0&&record.actions.Exists(e=>e.type=="launch");
   if(id=="05-directional-shield")record.passed&=record.shieldBlocks>0;
   if(id=="06-three-point-knife")record.passed&=record.returnHits>0&&record.swaps>0;
   if(id.Contains("boss-"))record.passed&=record.actions.Exists(e=>e.type=="boss_skill_execute");
   report.clips.Add(record);File.WriteAllText(Path.Combine(output,"capture-manifest.json"),JsonUtility.ToJson(report,true));Debug.Log("SHOWCASE_CLIP "+id+" frames="+record.frames+" health="+P.health+" passed="+record.passed);
  }
  void EndScenario()
  {
   Keys();G.qaSuppressAI=false;
   if(G.expeditionActive&&G.expeditionReceipt==null)G.FinishExpedition(false);
   if(G.expeditionActive||G.expeditionBoard)G.OpenExpeditionBoard();
   G.expeditionBoard=false;if(G.tutorialCourse)G.tutorialCourse.End();G.SetState(RunState.Menu);
  }
  IEnumerator Start()
  {
   string[] a=Environment.GetCommandLineArgs();int ix=Array.IndexOf(a,"-qaOutput");output=ix>=0?a[ix+1]:RunStorage.Root;ix=Array.IndexOf(a,"-captureOnly");only=ix>=0?a[ix+1]:"";Directory.CreateDirectory(output);
   while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
   Time.captureFramerate=30;QualitySettings.vSyncCount=0;Application.targetFrameRate=30;G.options.shake=.25f;G.options.weaponMotion=.65f;G.options.fov=65;G.fov=65;P.ApplyViewSettings();
   keyboard=InputSystem.AddDevice<Keyboard>();G.qaKeyboard=keyboard;G.qaInputEnabled=true;report.version=Application.version;G.sound.ExportReview(Path.Combine(output,"audio"));
   if(only=="gunplay"){yield return RecordGunplay();Finish();yield break;}
   for(int level=0;level<11;level++){var run=G.expeditionStore.Begin(level);G.expeditionStore.Settle(run,true,false,35);}
   
   yield return Record("01-menu",3,"原创主视觉与真实主菜单","正常启动主菜单","概念主视觉 / 游戏菜单",t=>{});
   yield return RecordGroundSwap();yield return RecordAirSwap();yield return RecordLaunch();yield return RecordShield();yield return RecordKnife();yield return RecordCover();
   EndScenario();G.OpenExpeditionBoard();G.SetExpeditionTabForReview(3);
   bool purchased=false;yield return Record("08-shop",4,"在战斗之间选择技能成长与配装","独立演示档提供研究点；画面中实际购买第一级研究并保存","游戏原生商店 / 自动演示",t=>{if(t>.9f&&!purchased){G.expeditionStore.UpgradeKnife();G.sound.Play("confirm");G.Record("shop_upgrade","knife rank="+G.expeditionStore.Profile.knifeRank);purchased=true;}});
   if(only=="boss"&&G.expeditionStore.Profile.knifeRank==0)G.expeditionStore.UpgradeKnife();
   yield return RecordBossScenarios();Finish();
  }
  void Finish()
  {
   if(done)return;done=true;Active=null;Time.captureFramerate=0;Time.timeScale=1;
   report.passed=report.errors.Count==0&&report.clips.Count>0&&report.clips.TrueForAll(c=>c.passed);File.WriteAllText(Path.Combine(output,"capture-manifest.json"),JsonUtility.ToJson(report,true));
   if(keyboard!=null){G.qaKeyboard=null;G.qaInputEnabled=false;InputSystem.RemoveDevice(keyboard);}Application.Quit(report.passed?0:1);
  }
  void OnDestroy(){Application.logMessageReceived-=Error;Active=null;Time.captureFramerate=0;}
 }
}
