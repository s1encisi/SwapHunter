using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace SwapHunter
{
 public sealed class StoryD2QA:MonoBehaviour
 {
  [Serializable] public sealed class Check{public string name,detail;public bool passed;}
  [Serializable] public sealed class Report
  {
   public string version,scope="N1 story dispatch, receipt-derived archive, imported audio, D2 new combat/relay/facility/AI/trial/Boss rules. Scripted fixtures, not human playtesting or balance evidence.";
   public bool passed,completed,batchMode;public List<Check> checks=new List<Check>();
  }
  readonly Report report=new Report();string output;bool done;float started;
  DemoGame G=>DemoGame.I;
  void Awake()
  {
   started=Time.realtimeSinceStartup;var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-qaOutput");
   output=i>=0?Path.GetFullPath(args[i+1]):RunStorage.Root;Directory.CreateDirectory(output);report.version=Application.version;report.batchMode=Application.isBatchMode;Application.logMessageReceived+=OnLog;
  }
  void OnLog(string m,string trace,LogType t){if(!done&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))Add("runtime_error",false,m+"\n"+trace);}
  void Update(){if(!done&&Time.realtimeSinceStartup-started>480){Add("timeout",false,"480 seconds");Finish(false);}}
  void Add(string name,bool pass,string detail=""){report.checks.Add(new Check{name=name,passed=pass,detail=detail});Debug.Log("STORY_D2_QA "+name+" "+pass+" "+detail);}
  IEnumerator Start()
  {
   if(!Application.isBatchMode)while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
   yield return null;Add("isolated",RunStorage.IsValidation&&G.qaMode);if(!RunStorage.IsValidation){Finish(false);yield break;}G.qaSuppressAI=true;
   yield return StoryAndAudio();
   yield return D2CombatChecks.Run(Add);
   yield return D2AIAndGeometryChecks.Run(Add);
   yield return MissionSetup();yield return D2MissionChecks.Run(Add);
   if(G.expeditionActive&&G.expeditionReceipt==null)G.AbandonExpedition();G.OpenExpeditionBoard();
   yield return D2BossChecks.Run(Add);
   yield return D2TrialChecks.Run(Add);
   Finish(true);
  }
  void Profile(string name)
  {
   if(G.StoryModal)G.CloseStoryModal();
   if(G.expeditionActive&&G.expeditionReceipt==null)G.AbandonExpedition();
   G.OpenExpeditionBoard();string folder=Path.Combine(RunStorage.Root,name);Directory.CreateDirectory(folder);
   File.WriteAllText(Path.Combine(folder,"expedition-v6.json"),JsonUtility.ToJson(TacticalDemoProfile.Create(0,true)));
   G.expeditionStore=new ExpeditionStore(folder,true);
  }
  IEnumerator Capture(string file)
  {
   if(Application.isBatchMode)yield break;
   yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,file));yield return new WaitForSecondsRealtime(.2f);
  }
  IEnumerator StoryAndAudio()
  {
   Profile("N1-story");
   Add("N1_content_complete",StoryContent.Chapters.Length==12&&StoryContent.Prologue.Length>0&&StoryContent.Epilogue.Length>0);
   G.ShowStoryCards("归航行动 · 黄昏",StoryContent.Prologue,false);yield return Capture("01-prologue.png");G.CloseStoryModal();
   Add("N1_new_profile_no_story_success",!G.StoryChapterUnlocked(6)&&!G.StoryAppendixUnlocked(7));
   Add("N1_ch7_start",G.BeginExpedition(6));
   foreach(var id in new[]{"c","a","b"}){G.CompleteMissionObjective(id);G.TickStory();}
   Add("N1_ch7_identity_order",G.StoryEventTriggered("objective-a")&&G.StoryEventTriggered("objective-b")&&G.StoryEventTriggered("objective-c")&&G.StoryEventTriggered("ready"));
   int events=G.StoryEventCount;G.CompleteMissionObjective("a");G.TickStory();G.TickStory();
   Add("N1_attempt_deduplication",G.StoryEventCount==events);
   yield return Capture("02-radio.png");
   G.AbandonExpedition();Add("N1_failure_no_archive",!G.StoryChapterUnlocked(6));G.OpenExpeditionBoard();
   Add("N1_ch7_retry_reset",G.BeginExpedition(6)&&!G.StoryEventTriggered("objective-c"));
   foreach(var id in new[]{"b","c","a"})G.CompleteMissionObjective(id);
   G.TickStory();Add("N1_success_archives_after_commit",G.FinishExpedition(true)&&G.StoryChapterUnlocked(6));G.OpenExpeditionBoard();
   foreach(var pair in new[]{"ab","bc","ca"})
   {
    G.BeginExpedition(7);foreach(char c in pair)G.CompleteMissionObjective(c.ToString());G.TickStory();
    Add("N1_any_two_"+pair,G.StoryEventTriggered("data-two")&&!G.StoryEventTriggered("data-three")&&G.ExpeditionMainComplete);
    bool saved=G.FinishExpedition(true);Add("N1_two_no_optional_"+pair,saved&&!G.StoryAppendixUnlocked(7));G.OpenExpeditionBoard();
   }
   G.BeginExpedition(7);foreach(var id in new[]{"c","b","a"})G.CompleteMissionObjective(id);G.TickStory();
   Add("N1_third_pending_not_permanent",G.StoryEventTriggered("data-three")&&!G.StoryAppendixUnlocked(7));G.AbandonExpedition();G.OpenExpeditionBoard();
   G.BeginExpedition(7);foreach(var id in new[]{"a","c","b"})G.CompleteMissionObjective(id);G.TickStory();G.FinishExpedition(true);
   Add("N1_third_success_appendix",G.StoryAppendixUnlocked(7));G.OpenExpeditionBoard();
   G.BeginExpedition(9);G.CompleteMissionObjective("a");G.TickStory();
   bool budget=false;foreach(var l in G.StoryTranscript)if(l.key=="objective-a-budget"&&l.text.Contains("取消 2"))budget=true;
   Add("N1_budget_feedback_actual",budget,"Early control cancels two undeployed members; text is derived from member states.");G.AbandonExpedition();G.OpenExpeditionBoard();
   G.BeginExpedition(11);G.CompleteMissionObjective("b");G.CompleteMissionObjective("c");G.TickStory();
   Add("N1_core_not_premature_shutdown",G.StoryEventTriggered("objective-c")&&!G.StoryEventTriggered("ready"));
   yield return new WaitForSeconds(3.3f);
   foreach(var m in G.mission.Members)if(m.role.kind==EnemyKind.Elite&&m.actor)m.actor.Damage(100000);
   G.mission.Tick(0);G.TickStory();Add("N1_boss_alone_no_shutdown",G.StoryEventTriggered("boss-down")&&!G.StoryEventTriggered("ready"));
   G.CompleteMissionObjective("a");G.TickStory();Add("N1_all_conditions_shutdown",G.StoryEventTriggered("ready")&&G.ExpeditionMainComplete);
   G.FinishExpedition(true);Add("N1_ending_without_collection",G.StoryChapterUnlocked(11)&&!G.StoryOptionalComplete);
   G.ShowStoryCards("归航 · 清晨",StoryContent.Epilogue,true);yield return new WaitForSecondsRealtime(.5f);
   Add("N1_ending_music",G.sound.Director.CurrentState==HomecomingMusicState.Ending);yield return Capture("03-homecoming.png");G.CloseStoryModal();
   G.OpenExpeditionBoard();G.OpenStoryArchive();yield return Capture("04-archive.png");G.CloseStoryModal();
   Add("AUDIO_imports",G.sound.ImportedEffectCount>=17&&G.sound.AmbientImported&&G.sound.Director.ImportedMusicTrackCount==3,"effects="+G.sound.ImportedEffectCount+"; music="+G.sound.Director.ImportedMusicTrackCount);
   bool sampleOK=true;foreach(var track in G.sound.Director.ReviewTracks){var samples=new float[256*track.Value.channels];sampleOK&=track.Value.GetData(samples,Mathf.Min(48000,track.Value.samples-256));foreach(float f in samples)sampleOK&=!float.IsNaN(f)&&!float.IsInfinity(f);}
   Add("AUDIO_tracks_decode",sampleOK);
   float old=G.options.musicVolume;G.options.musicVolume=0;yield return new WaitForSecondsRealtime(1.8f);
   Add("AUDIO_music_volume",G.sound.Director.AudibleMusicGain<.001f);G.options.musicVolume=old;
   G.sound.RadioCue();yield return new WaitForSecondsRealtime(.25f);Add("AUDIO_radio_duck",G.sound.Director.DuckGain<.5f);
  }
  IEnumerator MissionSetup()
  {
   Profile("D2-mission");G.BeginExpedition(4);yield return new WaitForSeconds(3.6f);G.TickStory();yield return Capture("05-d2-encounter.png");
  }
  void Finish(bool completed)
  {
   if(done)return;done=true;report.completed=completed;report.passed=completed&&report.checks.Count>0&&report.checks.TrueForAll(c=>c.passed);
   File.WriteAllText(Path.Combine(output,"story-d2-report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);
  }
  void OnDestroy(){Application.logMessageReceived-=OnLog;}
 }
}

