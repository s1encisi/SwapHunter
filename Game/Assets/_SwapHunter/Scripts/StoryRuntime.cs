using System;
using System.Collections.Generic;
using UnityEngine;
namespace SwapHunter
{
 public sealed partial class DemoGame
 {
  readonly HashSet<string> storyEvents=new HashSet<string>();
  readonly Queue<StoryLine> storyQueue=new Queue<StoryLine>();
  readonly List<StoryLine> storyTranscript=new List<StoryLine>();
  StoryLine storyRadio; float storyRadioLeft; int storyLevel=-1;
  bool storyPrologueSeen,storyArchiveOpen,storyCardEnding;
  int storyArchiveChapter=-1,storyCardIndex; Vector2 storyScroll;
  StoryLine[] storyCards; string storyCardTitle; RunState storyReturnState;
  public bool StoryRadioActive=>storyRadio!=null&&IsPlaying&&!StoryModal;
  public bool StoryEndingVisible=>storyCardEnding&&storyCards!=null;
  public bool StoryModal=>storyCards!=null||storyArchiveOpen;
  public int StoryEventCount=>storyEvents.Count;
  public bool StoryEventTriggered(string key)=>storyEvents.Contains(key);
  public IReadOnlyList<StoryLine> StoryTranscript=>storyTranscript;
  public bool StoryChapterUnlocked(int level)=>expeditionStore!=null&&level>=0&&level<12&&expeditionStore.Profile.cleared[level];
  public bool StoryAppendixUnlocked(int level){if(expeditionStore==null)return false;foreach(var r in expeditionStore.Profile.receipts)if(r.level==level&&r.success&&r.bonus)return true;return false;}
  public bool StoryOptionalComplete{get{for(int i=3;i<12;i++)if(!StoryAppendixUnlocked(i))return false;return true;}}
  void BeginStoryAttempt()
  {
   storyLevel=expeditionLevel;storyEvents.Clear();storyQueue.Clear();storyTranscript.Clear();storyRadio=null;storyRadioLeft=0;
   if(RunStorage.IsDirectedTrial)return;
   expeditionMap.root.gameObject.AddComponent<StoryScenery>().Build(this,storyLevel);StoryEmit("start");
   Record("story_attempt","N1 chapter="+(storyLevel+1));
  }
  void ClearStoryAttempt(){storyLevel=-1;storyQueue.Clear();storyRadio=null;storyRadioLeft=0;}
  void StoryEmit(string key)
  {
   if(storyLevel<0||RunStorage.IsDirectedTrial||!storyEvents.Add(key))return;
   foreach(var line in StoryContent.Event(storyLevel,key))QueueStoryLine(line);
   Record("story_event",storyLevel+":"+key);
  }
  void QueueStoryLine(StoryLine line){storyQueue.Enqueue(line);storyTranscript.Add(line);}
  public void SkipStoryRadio(){storyRadio=null;storyRadioLeft=0;AdvanceStoryRadio();}
  void AdvanceStoryRadio(){if(storyQueue.Count==0)return;storyRadio=storyQueue.Dequeue();storyRadioLeft=Mathf.Clamp(2+storyRadio.text.Length/8f,4,10);if(sound)sound.RadioCue();}
  internal void TickStory()
  {
   if(StoryModal)
   {
    if((!qaMode||qaInputEnabled)&&KeyboardDevice!=null)
    {if(KeyboardDevice.escapeKey.wasPressedThisFrame)CloseStoryModal();else if(storyCards!=null&&(KeyboardDevice.enterKey.wasPressedThisFrame||KeyboardDevice.numpadEnterKey.wasPressedThisFrame))NextStoryCard();}
    return;
   }
   if(!expeditionActive||mission==null||!IsPlaying||RunStorage.IsDirectedTrial||storyLevel!=expeditionLevel)return;
   if((!qaMode||qaInputEnabled)&&KeyboardDevice!=null)
   {if(KeyboardDevice.f9Key.wasPressedThisFrame)SkipStoryRadio();if(KeyboardDevice.f10Key.wasPressedThisFrame){OpenStoryArchive(true);return;}}
   if(storyLevel==0&&totalSwaps>0)StoryEmit("swap");
   if(storyLevel==1)foreach(var m in mission.Members)if(m.group.id=="first"&&m.Defeated){StoryEmit("first-guard");break;}
   if(storyLevel==2&&mission.RegionVisited("upper"))StoryEmit("region-upper");
   foreach(var o in mission.Definition.objectives)
   {
    if(!mission.ObjectiveComplete(o.id))continue;string key="objective-"+o.id;bool first=!storyEvents.Contains(key);StoryEmit(key);
    if(first&&storyLevel==9)
    {
     int cancelled=0,deployed=0;foreach(var m in mission.Members)if(m.group.cancelOnObjective==o.id){if(m.state==EncounterBudgetState.Cancelled)cancelled++;if(m.state==EncounterBudgetState.Deployed)deployed++;}
     QueueStoryLine(new StoryLine(key+"-budget","沈乔","该站已关闭。取消 "+cancelled+" 名尚未落地的增援；已落地 "+deployed+" 名，仍需避开他们的枪线。"));
    }
   }
   if(storyLevel==7){if(mission.CompletedObjectives>=2)StoryEmit("data-two");if(mission.CompletedObjectives>=3)StoryEmit("data-three");}
   if(storyLevel==11)foreach(var m in mission.Members)if(m.role.kind==EnemyKind.Elite&&m.Defeated){StoryEmit("boss-down");break;}
   if(mission.CanExtract)StoryEmit("ready");if(expeditionBonus)StoryEmit("bonus");
   if(storyRadio==null)AdvanceStoryRadio();else{storyRadioLeft-=Time.deltaTime;if(storyRadioLeft<=0)SkipStoryRadio();}
  }
  void StorySettlement(ExpeditionReceipt receipt)
  {
   storyQueue.Clear();storyRadio=null;if(!receipt.success||RunStorage.IsDirectedTrial)return;
   Record("story_committed","chapter="+receipt.level+";appendix="+receipt.bonus+";receipt="+receipt.runId);
   if(qaMode)return;
   if(receipt.level==11){var lines=new List<StoryLine>(StoryContent.Epilogue);if(StoryOptionalComplete)lines.AddRange(StoryContent.OptionalRecording);ShowStoryCards("归航 · 清晨",lines.ToArray(),true);}
   else{var lines=StoryContent.Interlude(receipt.level);if(lines.Length>0)ShowStoryCards("归航行动 · "+StoryContent.Chapters[receipt.level].subtitle,lines,false);}
  }
  public void OpenHomecoming(){OpenExpeditionBoard();expeditionTab=0;if(!storyPrologueSeen&&!StoryChapterUnlocked(0)){storyPrologueSeen=true;ShowStoryCards("归航行动 · 黄昏",StoryContent.Prologue,false);}}
  public void ShowStoryCards(string title,StoryLine[] lines,bool ending)
  {
   if(lines==null||lines.Length==0)return;storyReturnState=state;storyCards=lines;storyCardIndex=0;storyCardTitle=title;storyCardEnding=ending;storyArchiveOpen=false;
   if(IsPlaying)SetState(RunState.Paused);
  }
  void NextStoryCard(){if(storyCards==null)return;if(++storyCardIndex>=storyCards.Length)CloseStoryModal();else if(sound)sound.RadioCue();}
  public void CloseStoryModal(){storyCards=null;storyArchiveOpen=false;storyCardEnding=false;if(storyReturnState==RunState.Playing&&state==RunState.Paused&&!expeditionPending)SetState(RunState.Playing);}
  public void OpenStoryArchive(bool currentAttempt=false){storyReturnState=state;storyArchiveOpen=true;storyCards=null;storyScroll=Vector2.zero;storyArchiveChapter=currentAttempt?-2:-1;if(IsPlaying)SetState(RunState.Paused);}
  void DrawStoryRadio()
  {
   if(!StoryRadioActive)return;float x=600,w=viewWidth-x-32;
   Fill(new Rect(x,548,w,187),new Color(.012f,.035f,.055f,.94f));Fill(new Rect(x,548,4,187),Cyan);
   Text("救援专线 / "+storyRadio.speaker,new Rect(x+20,559,w-40,28),17,Cyan);Text(storyRadio.text,new Rect(x+20,593,w-40,110),22,Paper);
   Text("F9 下一句 · F10 通讯记录",new Rect(x+20,710,w-40,20),12,Muted,TextAnchor.MiddleRight);
  }
  bool DrawStoryModal()
  {
   if(!StoryModal)return false;Fill(FullScreenGuiRect,new Color(.007f,.019f,.035f,.98f));
   for(int i=0;i<12;i++)Fill(new Rect(viewWidth*.45f+i*64,350-(i%3)*35,42,550),new Color(.02f,.06f,.085f));
   Fill(new Rect(0,760,viewWidth,140),new Color(.02f,.075f,.10f));
   if(storyCards!=null)
   {
    float x=Mathf.Max(60,viewWidth*.12f),w=viewWidth-2*x;Text(storyCardTitle,new Rect(x,110,w,54),37,Paper);
    Text("岬湾港 / "+(storyCardEnding?"最后一班补给船":"救援通讯"),new Rect(x,178,w,32),18,Cyan);
    var line=storyCards[storyCardIndex];Text(line.speaker,new Rect(x,290,w,40),24,Cyan);Text(line.text,new Rect(x,354,w,245),29,Paper);
    Text((storyCardIndex+1)+" / "+storyCards.Length,new Rect(x,663,w,30),18,Muted);
    if(Button(storyCardIndex+1==storyCards.Length?"继续行动 / Enter":"下一段 / Enter",new Rect(x,710,330,55),true))NextStoryCard();
    if(Button("跳过阅读 / Esc",new Rect(x+354,710,290,55)))CloseStoryModal();
    Text("可在行动地图的“归航档案”中回看；阅读不推进行动时间。",new Rect(x,805,w,35),17,Muted);return true;
   }
   Text("归航档案",new Rect(48,42,650,64),40,Paper);Text("已完成章节可回看全文 · 当前尝试只作临时通讯记录",new Rect(50,112,viewWidth-100,35),18,Muted);
   if(Button("返回 / Esc",new Rect(viewWidth-235,42,185,49),true))CloseStoryModal();
   float navW=285,bodyX=366,bodyW=viewWidth-bodyX-50;
   if(Button("序章 / 行动背景",new Rect(48,177,navW,39),storyArchiveChapter==-1)){storyArchiveChapter=-1;storyScroll=Vector2.zero;}
   if(Button("本次通讯记录",new Rect(48,225,navW,39),storyArchiveChapter==-2)){storyArchiveChapter=-2;storyScroll=Vector2.zero;}
   for(int i=0;i<12;i++)
   {
    bool enabled=GUI.enabled;GUI.enabled=enabled&&StoryChapterUnlocked(i);
    if(Button((i+1).ToString("00")+" "+ExpeditionCatalog.Levels[i].name,new Rect(48,278+i*42,navW,36),storyArchiveChapter==i)){storyArchiveChapter=i;storyScroll=Vector2.zero;}
    GUI.enabled=enabled;
   }
   string heading;var display=new List<StoryLine>();
   if(storyArchiveChapter==-2){heading="本次通讯 / 不写入永久章节进度";display.AddRange(storyTranscript);}
   else if(storyArchiveChapter<0){heading="归航行动 / 序章";display.AddRange(StoryContent.Prologue);}
   else
   {
    int level=storyArchiveChapter;var chapter=StoryContent.Chapters[level];heading=chapter.subtitle;
    display.Add(new StoryLine("brief","行动简报",chapter.brief));display.AddRange(chapter.lines);display.Add(new StoryLine("ending","行动结果",chapter.ending));
    display.AddRange(level==11?StoryContent.Epilogue:StoryContent.Interlude(level));
    if(level==7&&StoryAppendixUnlocked(7))display.Add(new StoryLine("optional","班组附件","第三份数据匣已成功带回。许宁留下的回程标注和班组交接信息已归档。"));
    if(level==11&&StoryOptionalComplete)display.AddRange(StoryContent.OptionalRecording);
   }
   Text(heading,new Rect(bodyX,178,bodyW,52),27,Cyan);textStyle.fontSize=22;textStyle.alignment=TextAnchor.UpperLeft;
   float height=16;foreach(var line in display)height+=40+textStyle.CalcHeight(new GUIContent(line.text),bodyW-38)+28;
   storyScroll=GUI.BeginScrollView(new Rect(bodyX,241,bodyW,560),storyScroll,new Rect(0,0,bodyW-22,Mathf.Max(560,height)));float y=8;
   foreach(var line in display){Text(line.speaker,new Rect(8,y,bodyW-46,28),17,Cyan);y+=35;textStyle.fontSize=22;float h=textStyle.CalcHeight(new GUIContent(line.text),bodyW-38);Text(line.text,new Rect(8,y,bodyW-38,h),22,Paper);y+=h+33;}
   if(display.Count==0)Text("本次尚无通讯记录。",new Rect(8,15,bodyW-40,60),22,Muted);GUI.EndScrollView();
   Text("完整救援结局由主线保证；额外资料只补充人物，不改变救援人数。",new Rect(bodyX,822,bodyW,45),16,Muted);return true;
  }
 }
}


