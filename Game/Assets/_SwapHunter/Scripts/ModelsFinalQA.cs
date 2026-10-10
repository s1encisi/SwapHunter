using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace SwapHunter
{
 // Only checks affected by the new model integration; fixture saves are isolated.
 public sealed class ModelsFinalQA:MonoBehaviour
 {
  [Serializable] public sealed class Check{public string name,detail;public bool passed;}
  [Serializable] public sealed class Report{
   public string version,scope="New model sources, previews, bone contacts, native reload, actors and chapter visual construction. Scripted window captures, not human playtesting.";
   public bool passed,batchMode,completed;public int focusedCaptures;public List<Check> checks=new List<Check>();public List<string> images=new List<string>();}
  readonly Report report=new Report();string output;float started;bool done;
  DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
  void Awake(){started=Time.realtimeSinceStartup;var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-qaOutput");output=i>=0?Path.GetFullPath(a[i+1]):RunStorage.Root;Directory.CreateDirectory(output);report.version=Application.version;report.batchMode=Application.isBatchMode;Application.logMessageReceived+=OnLog;}
  void OnLog(string m,string s,LogType t){if(!done&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))Add("runtime_error",false,m+"\n"+s);}
  void Update(){if(!done&&Time.realtimeSinceStartup-started>240){Add("models_timeout",false,"240 seconds");Finish(false);}}
  void Add(string n,bool p,string d=""){report.checks.Add(new Check{name=n,passed=p,detail=d});Debug.Log("MODELS_FINAL "+n+" "+p+" "+d);}
  IEnumerator Capture(string name){if(Application.isBatchMode)yield break;yield return new WaitForSecondsRealtime(.3f);ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));report.images.Add(name+".png");if(Application.isFocused)report.focusedCaptures++;yield return new WaitForSecondsRealtime(.2f);}
  IEnumerator Start()
  {
   if(!Application.isBatchMode)while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
   Add("isolated_fixture",RunStorage.IsValidation&&G.qaMode,"No normal/legacy saves");G.qaSuppressAI=true;
   string folder=Path.Combine(RunStorage.Root,"ModelsFixture");Directory.CreateDirectory(folder);
   File.WriteAllText(Path.Combine(folder,"expedition-v6.json"),JsonUtility.ToJson(TacticalDemoProfile.Create(0),true));
   G.expeditionStore=new ExpeditionStore(folder,true);G.OpenExpeditionBoard();G.SetExpeditionTabForReview(1);yield return new WaitForSecondsRealtime(.7f);
   var preview=G.WeaponLibraryPreview;var sources=new HashSet<int>();
   for(int id=0;id<WeaponCatalog.Count;id++){
    G.SelectWeaponForLibraryReview(id);float until=Time.realtimeSinceStartup+3;
    while(Time.realtimeSinceStartup<until&&(!preview||preview.RenderedId!=id||!preview.GetIcon(id)))yield return null;
    bool ready=preview&&preview.RenderedId==id&&preview.SelectedTexture&&preview.GetIcon(id)&&string.IsNullOrEmpty(preview.GetError(id));
    Add("new_weapon_preview_"+id,ready&&WeaponModelPreview.SourceKey(id).Contains("VisualV2"),WeaponModelPreview.SourceKey(id));
    if(ready)sources.Add(preview.SelectedSourceInstanceId);if(id==3||id==10)yield return Capture("weapon-library-"+id);
   }
   Add("twelve_distinct_weapon_sources",sources.Count==12,"Distinct imported assets="+sources.Count);G.SetExpeditionTabForReview(2);
   for(int id=0;id<4;id++){G.expeditionStore.Profile.activeClass=id;yield return new WaitForSecondsRealtime(.3f);var portrait=G.GetComponent<ClassModelPreview>();Add("new_class_portrait_"+id,portrait&&portrait.Texture&&string.IsNullOrEmpty(portrait.Error),portrait?portrait.Error:"missing");if(id==0||id==2)yield return Capture("class-"+id);}
   G.StartRun(false);G.qaSuppressAI=true;P.SetFeet(new Vector3(0,.035f,3));P.SetLook(0,0);
   for(int id=0;id<WeaponCatalog.Count;id++){
    P.SetLoadout(id,id==0?1:0);yield return new WaitForSeconds(.22f);float grip=P.RightGripError,support=P.SupportHandGripError;
    Add("new_weapon_contacts_"+id,P.CurrentWeaponVisualSource.Contains("VisualV2")&&grip>=0&&grip<.015f&&support>=0&&support<.02f,P.CurrentWeaponVisualSource+"; right="+grip+"; support="+support);
    Add("muzzle_forward_"+id,Vector3.Dot(P.CurrentMuzzleForward,P.cameraView.transform.forward)>.85f,"dot="+Vector3.Dot(P.CurrentMuzzleForward,P.cameraView.transform.forward));
    if(id==0||id==5||id==10)yield return Capture("first-person-"+id);
   }
   foreach(int id in new[]{0,5}){
    P.SetLoadout(id,id==0?1:0);P.ammo[id]=0;yield return new WaitForSeconds(.25f);bool began=P.Reload(),captured=false;float largestError=0,travel=0;int samples=0;float deadline=Time.realtimeSinceStartup+10;
    while(P.reloadLeft>0&&Time.realtimeSinceStartup<deadline){if(P.ReloadGripWeight>.95f){samples++;largestError=Mathf.Max(largestError,P.ReloadGripError);}travel=Mathf.Max(travel,P.MagazineSeparation);if(!captured&&P.ReloadProgress>.42f){captured=true;yield return Capture("reload-"+id);}yield return null;}
    Add("new_model_reload_"+id,began&&P.reloadLeft<=0&&P.ammo[id]==P.Capacity(id)&&samples>0&&largestError<.025f,"contactSamples="+samples+"; maxContactError="+largestError+"; magazineTravel="+travel);
   }
   yield return ModelsActorChecks.Run(Add,output);if(File.Exists(Path.Combine(output,"actor-lineup.png")))report.images.Add("actor-lineup.png");
   G.SetState(RunState.Menu);G.OpenExpeditionBoard();
   for(int level=0;level<12;level++){
    if(G.expeditionActive){G.AbandonExpedition();G.OpenExpeditionBoard();}bool began=G.BeginExpedition(level);yield return new WaitForSeconds(.15f);
    int models=G.world?G.world.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("New environment / ",StringComparison.Ordinal)):0;
    Add("new_environment_chapter_"+(level+1),began&&models>20,"Imported visual groups="+models+"; collision/navigation unchanged");
    if(began&&(level==4||level==6||level==11)){P.SetFeet(new Vector3(0,.035f,level==11?14:8));P.SetLook(0,-7);yield return Capture("chapter-"+(level+1));}
   }
   yield return new WaitForSecondsRealtime(.3f);Add("capture_files_present",report.images.All(n=>File.Exists(Path.Combine(output,n))),"Images="+report.images.Count+"; focused="+report.focusedCaptures);Finish(true);
  }
  void Finish(bool completed){if(done)return;done=true;report.completed=completed;report.passed=completed&&report.checks.Count>0&&report.checks.TrueForAll(c=>c.passed);File.WriteAllText(Path.Combine(output,"models-final-report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);}
  void OnDestroy(){Application.logMessageReceived-=OnLog;}
 }
}
