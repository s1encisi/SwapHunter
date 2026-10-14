using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;
using Debug=UnityEngine.Debug;
namespace SwapHunter
{
    [Serializable] public sealed class DirectedTrialRequest
    {
        public string sessionId,kind,smokeOutcome="";
        public int productId,classId,level,scenario,formalLevel,purchaseLevel,purchasePrice,returnTab,selectedWeapon,rulesVersion;
        public float scrollY;
        public bool equalTier;
    }
    [Serializable] public sealed class DirectedTrialResult
    {
        public string sessionId,outcome,mode,endedUtc,isolatedRoot;
        public int rulesVersion,shots,hits,reloads,completedReloads,controls,swaps;
        public float damage,healthLost,activeSeconds;
        public bool complete;
    }
    public static class DirectedTrialBootstrap
    {
        static DirectedTrialRequest request;
        public static string Folder {get;private set;}
        public static DirectedTrialRequest Request
        {
            get
            {
                if(request!=null)return request;
                if(!RunStorage.IsDirectedTrial)return null;
                var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-swapHunterTrial");
                if(at+1>=args.Length)throw new ArgumentException("试用请求路径缺失");
                Folder=Path.GetFullPath(args[at+1]);var candidate=JsonUtility.FromJson<DirectedTrialRequest>(File.ReadAllText(Path.Combine(Folder,"request.json")));
                Validate(candidate);
                string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"SHT"))+Path.DirectorySeparatorChar;
                if(!Folder.StartsWith(root,StringComparison.OrdinalIgnoreCase)||new DirectoryInfo(Folder).Name!=candidate.sessionId)throw new ArgumentException("试用会话目录不匹配");
                request=candidate;return request;
            }
        }
        public static void Validate(DirectedTrialRequest r)
        {
            if(r==null||!Guid.TryParseExact(r.sessionId,"N",out _)||r.rulesVersion!=TacticalPrototypeRules.Version||!TacticalPrototypeRules.ValidClass(r.classId)||r.level<1||r.level>3||r.scenario<0||r.scenario>1)throw new ArgumentException("试用请求或规则版本不匹配");
            if(r.kind=="weapon"){if(!TacticalPrototypeRules.ClassCanUseWeapon(r.classId,r.productId))throw new ArgumentException("试用武器与职业不匹配");}
            else if(r.kind=="class"){if(!TacticalPrototypeRules.ClassCanUseSkill(r.classId,r.productId))throw new ArgumentException("试用技能与职业不匹配");}
            else if(r.kind=="general"){if(!TacticalPrototypeRules.ValidSkill(r.productId,true))throw new ArgumentException("通用技能不存在");}
            else throw new ArgumentException("试用商品类型不存在");
        }
        public static ExpeditionProfile CreateProfile()
        {
            var r=Request;var p=TacticalDemoProfile.Create(r.classId);
            for(int i=0;i<p.classSkillLevels.Length;i++)p.classSkillLevels[i]=1;
            p.generalLevels[0]=p.generalLevels[1]=1;
            var l=p.classLoadouts[r.classId];l.primary=0;l.secondary=TacticalPrototypeRules.StarterWeapon(r.classId);
            l.classSkillId=TacticalPrototypeRules.SkillIds[r.classId==0?1:r.classId*2];l.tacticalSkillId=TacticalPrototypeRules.GeneralSkillIds[0];
            if(r.kind=="weapon"){l.primary=r.productId;l.secondary=r.productId==0?TacticalPrototypeRules.StarterWeapon(r.classId):0;}
            else if(r.kind=="class"){l.classSkillId=TacticalPrototypeRules.SkillIds[r.productId];p.classSkillLevels[r.productId]=r.level;}
            else{l.tacticalSkillId=TacticalPrototypeRules.GeneralSkillIds[r.productId];p.generalLevels[r.productId]=r.level;}
            // Fixed comparison conditions: base attachments and no carried consumables.
            l.attachments=new int[WeaponCatalog.Count];l.itemTypes=new[]{-1,-1};l.itemCounts=new int[2];l.medkitCarry=0;
            p.primary=l.primary;p.secondary=l.secondary;p.mobilityLevel=p.classSkillLevels[1];p.shieldLevel=p.generalLevels[0];
            ExpeditionStore.Validate(p);return p;
        }
    }
    public sealed partial class DemoGame
    {
        Process trialProcess;DirectedTrialRequest trialRequest;DirectedTrialResult childTrialResult;
        bool trialDialog,trialChildClosed,trialResultVisible;float trialChildStarted=-1;
        int trialScenario;bool trialEqualTier;
        public bool TrialSessionActive=>trialProcess!=null;
        public bool TrialDialogOpen=>trialDialog;
        public bool TrialAccountReadBlocked {get;private set;}
        public DirectedTrialResult LastTrialResult {get;private set;}
        public string LastTrialSessionDirectory {get;private set;}="";
        public string TrialReturnNotice {get;private set;}="";
        public DirectedTrialRequest CurrentTrialRequest=>trialRequest;
        public DirectedTrialRequest MakeTrialRequest(string kind,int product,int scenario=0,bool equalTier=false)
        {
            if(!TacticalExpedition)throw new InvalidOperationException("试用仅用于四职业整备");
            var p=expeditionStore.Profile;int c=kind=="class"?product/2:p.activeClass;
            if(kind=="weapon"&&!TacticalPrototypeRules.ClassCanUseWeapon(c,product))for(int i=0;i<4;i++)if(TacticalPrototypeRules.ClassCanUseWeapon(i,product)){c=i;break;}
            int owned=kind=="weapon"?(p.weapons[product]?1:0):kind=="class"?p.classSkillLevels[product]:p.generalLevels[product];
            int purchase=kind=="weapon"?1:Mathf.Min(3,owned+1);
            var r=new DirectedTrialRequest{sessionId=Guid.NewGuid().ToString("N"),kind=kind,productId=product,classId=c,
                level=equalTier&&kind!="weapon"?3:purchase,scenario=scenario,formalLevel=owned,purchaseLevel=purchase,equalTier=equalTier,
                purchasePrice=kind=="weapon"?(owned>0?0:TacticalPrototypeRules.WeaponPrice(product)):owned>=3?0:owned==0?100:TacticalPrototypeRules.SkillUpgradePrice(owned),
                returnTab=expeditionTab,selectedWeapon=expeditionSelectedWeapon,rulesVersion=TacticalPrototypeRules.Version};
            DirectedTrialBootstrap.Validate(r);return r;
        }
        public void OpenWeaponTrial(int weapon){OpenTrial("weapon",weapon);}
        public void OpenSkillTrial(int index,bool general){OpenTrial(general?"general":"class",index);}
        void OpenTrial(string kind,int product)
        {
            if(!expeditionBoard||state!=RunState.Menu||TrialSessionActive||!string.IsNullOrEmpty(expeditionStore.Profile.activeRun)){expeditionNotice="请先返回关外整备再试用";return;}
            trialScenario=0;trialEqualTier=false;trialRequest=MakeTrialRequest(kind,product);trialDialog=true;trialResultVisible=false;
        }
        public bool StartDirectedTrial(DirectedTrialRequest r,string smokeOutcome="")
        {
            if(!expeditionBoard||state!=RunState.Menu||TrialSessionActive||TrialAccountReadBlocked||!string.IsNullOrEmpty(expeditionStore.Profile.activeRun))return false;
            if(!string.IsNullOrEmpty(smokeOutcome)&&!RunStorage.IsValidation)throw new InvalidOperationException("自动结束只允许隔离验收父进程");
            DirectedTrialBootstrap.Validate(r);
            if(Application.isEditor){expeditionNotice="请在本地 Windows 试玩包中使用独立窗口试用；编辑器中可使用快速演示。";return false;}
            try
            {
                r.smokeOutcome=smokeOutcome;string folder=Path.Combine(Path.GetTempPath(),"SHT",r.sessionId);Directory.CreateDirectory(folder);
                using(var stream=new FileStream(Path.Combine(folder,"request.json"),FileMode.CreateNew))using(var writer=new StreamWriter(stream))writer.Write(JsonUtility.ToJson(r,true));
                string exe=Path.GetFullPath(Path.Combine(Application.dataPath,"..","SwapHunter.exe"));
                var info=new ProcessStartInfo(exe,"-swapHunterTrial \""+folder+"\" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile \""+Path.Combine(folder,"player.log")+"\""){
                    UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(exe),WindowStyle=string.IsNullOrEmpty(smokeOutcome)?ProcessWindowStyle.Normal:ProcessWindowStyle.Hidden};
                if(!string.IsNullOrEmpty(smokeOutcome))info.Arguments+=" -batchmode";
                trialProcess=Process.Start(info);trialRequest=r;LastTrialSessionDirectory=folder;LastTrialResult=null;TrialReturnNotice="试用窗口已启动，结束后返回当前商品";
                trialDialog=false;trialResultVisible=false;return true;
            }
            catch(Exception e)when(e is IOException||e is UnauthorizedAccessException||e is System.ComponentModel.Win32Exception)
            {expeditionNotice="试用未启动："+e.Message;trialProcess?.Dispose();trialProcess=null;return false;}
        }
        public void TickTrialSession()
        {
            if(RunStorage.IsDirectedTrial)
            {
                if(!demonstrationPrepared||!expeditionActive)return;
                EnsureChildTrialResult();if(IsPlaying)childTrialResult.activeSeconds+=Time.unscaledDeltaTime;
                var r=DirectedTrialBootstrap.Request;
                if(!string.IsNullOrEmpty(r.smokeOutcome)&&Time.realtimeSinceStartup-trialChildStarted>1.2f)
                {
                    if(r.smokeOutcome=="death"&&state!=RunState.Dead){player.health=0;Die();}
                    FinishDirectedTrial(r.smokeOutcome=="death"?"death":r.smokeOutcome=="success"?"sample_complete":"returned");return;
                }
                if(state==RunState.Dead)FinishDirectedTrial("death");else if(state==RunState.Victory)FinishDirectedTrial("success");
                else if(Pressed(16)||(KeyboardDevice!=null&&KeyboardDevice.escapeKey.wasPressedThisFrame))FinishDirectedTrial("returned");
                return;
            }
            if(trialProcess==null||!trialProcess.HasExited)return;
            int exit=trialProcess.ExitCode;trialProcess.Dispose();trialProcess=null;LastTrialResult=null;
            try
            {
                var path=Path.Combine(LastTrialSessionDirectory,"result.json");
                if(File.Exists(path))
                {
                    var result=JsonUtility.FromJson<DirectedTrialResult>(File.ReadAllText(path));
                    if(result!=null&&result.complete&&result.sessionId==trialRequest.sessionId&&result.rulesVersion==TacticalPrototypeRules.Version)LastTrialResult=result;
                }
                // Read current state only. Never restore the account snapshot from before the trial.
                expeditionStore.ReloadFromDisk();TrialAccountReadBlocked=false;
                expeditionTab=trialRequest.returnTab;expeditionSelectedWeapon=trialRequest.selectedWeapon;
                TrialReturnNotice=LastTrialResult==null?"试用已关闭，结果未记录（退出码 "+exit+"）；可重新试用。":"试用已返回："+TrialOutcomeLabel(LastTrialResult.outcome)+"；请按当前价格自主采购。";
                trialResultVisible=true;
            }
            catch(Exception e)when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException)
            {TrialAccountReadBlocked=true;TrialReturnNotice="已返回，但无法读取当前档案或结果："+e.Message;expeditionNotice=TrialReturnNotice;trialResultVisible=true;}
        }
        public bool RetryTrialAccountRead()
        {
            try{expeditionStore.ReloadFromDisk();TrialAccountReadBlocked=false;TrialReturnNotice="当前档案已重新读取；可以回到商品自主采购。";return true;}
            catch(Exception e)when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException){TrialAccountReadBlocked=true;TrialReturnNotice="当前档案仍无法读取，采购和部署保持锁定："+e.Message;return false;}
        }
        void EnsureChildTrialResult()
        {
            if(childTrialResult!=null)return;var r=DirectedTrialBootstrap.Request;trialChildStarted=Time.realtimeSinceStartup;
            childTrialResult=new DirectedTrialResult{isolatedRoot=RunStorage.Root,sessionId=r.sessionId,rulesVersion=r.rulesVersion,mode=string.IsNullOrEmpty(r.smokeOutcome)?"Trial":"QA"};
        }
        public void ObserveTrialEvent(string evt,string detail)
        {
            if(!RunStorage.IsDirectedTrial||!demonstrationPrepared)return;EnsureChildTrialResult();
            if(evt=="enemy_effective_damage"&&float.TryParse(detail,NumberStyles.Float,CultureInfo.InvariantCulture,out float damage))childTrialResult.damage+=damage;
            if(evt=="enemy_silenced"||evt=="enemy_staggered"||evt=="boss_basic_burst_interrupted")childTrialResult.controls++;
            if(evt=="weapon_reload_begin")childTrialResult.reloads++;
            if(evt=="weapon_reload_complete")childTrialResult.completedReloads++;
            if(evt=="player_effective_damage"&&float.TryParse(detail,NumberStyles.Float,CultureInfo.InvariantCulture,out float loss))childTrialResult.healthLost+=loss;
        }
        public void FinishDirectedTrial(string outcome)
        {
            if(!RunStorage.IsDirectedTrial||trialChildClosed)return;EnsureChildTrialResult();trialChildClosed=true;
            childTrialResult.outcome=outcome;childTrialResult.endedUtc=DateTime.UtcNow.ToString("o");childTrialResult.shots=totalShots;childTrialResult.hits=totalHits;childTrialResult.swaps=totalSwaps;childTrialResult.complete=true;
            try{File.WriteAllText(Path.Combine(DirectedTrialBootstrap.Folder,"result.json"),JsonUtility.ToJson(childTrialResult,true));}
            catch(Exception e)when(e is IOException||e is UnauthorizedAccessException){Debug.LogWarning("试用摘要未保存："+e.Message);}
            Application.Quit(0);
        }
        public void CloseTrialSession()
        {
            if(RunStorage.IsDirectedTrial&&demonstrationPrepared&&!trialChildClosed){FinishDirectedTrial("quit");return;}
            if(trialProcess!=null){if(!trialProcess.HasExited)trialProcess.Kill();trialProcess.Dispose();trialProcess=null;}
        }
        internal void TerminateTrialForReview(){if(!RunStorage.IsValidation)throw new InvalidOperationException();if(trialProcess!=null&&!trialProcess.HasExited)trialProcess.Kill();}
        static string TrialOutcomeLabel(string v)=>v=="death"?"死亡结束":v=="success"?"完成章节":v=="sample_complete"?"验收短场景完成":v=="quit"?"关闭窗口":"主动返回";
        string TrialProductName(DirectedTrialRequest r)=>r.kind=="weapon"?WeaponCatalog.Get(r.productId).name:r.kind=="class"?TacticalPrototypeRules.ClassSkillNames[r.productId]:TacticalPrototypeRules.GeneralSkillNames[r.productId];
        void DrawDirectedTrialUI()
        {
            if(RunStorage.IsDirectedTrial&&demonstrationPrepared)
            {
                var r=DirectedTrialBootstrap.Request;
                Fill(new Rect(18,14,Mathf.Min(viewWidth-36,1020),108),new Color(.015f,.035f,.065f,.94f));
                Text("定向试用 / "+TrialProductName(r)+" / "+TacticalPrototypeRules.ClassNames[r.classId]+(r.kind=="weapon"?" / 基础参数":" / "+r.level+"级"),new Rect(34,24,770,31),23,Paper);
                Text(r.kind=="weapon"?"武器基础参数；固定其它配置、基础配件与零道具，正常伤害和AI。":(r.equalTier?"同阶比较":"采购结果预览")+"；正式拥有 "+r.formalLevel+"级，采购结果 "+r.purchaseLevel+"级。固定其它配置，正常伤害与AI。",new Rect(34,63,780,48),17,Muted);
                if(Button("结束试用",new Rect(viewWidth-198,25,175,44)))FinishDirectedTrial("returned");return;
            }
            if(TrialSessionActive)
            {
                Fill(FullScreenGuiRect,new Color(0,0,0,.86f));Text("试用进行中",new Rect(100,300,viewWidth-200,70),38,Paper);
                Text("在独立试用窗口按 Esc 或结束按钮返回。当前整备暂时锁定。",new Rect(100,400,viewWidth-200,70),22,Muted);
                if(Button("关闭试用并返回",new Rect(100,530,360,52))){if(!trialProcess.HasExited)trialProcess.Kill();}return;
            }
            if(!trialDialog&&!trialResultVisible)return;
            Fill(FullScreenGuiRect,new Color(0,0,0,.89f));float x=100,w=viewWidth-200;
            DrawUiPanel(new Rect(x,160,w,574),true);
            if(trialResultVisible)
            {
                Text(TrialReturnNotice,new Rect(x+24,190,w-48,82),25,Paper);
                var a=LastTrialResult;if(a!=null)Text("有效用时 "+a.activeSeconds.ToString("0.0")+" 秒；出弹 "+a.shots+"；有效命中 "+a.hits+"；实际伤害 "+a.damage.ToString("0.0")+
                    "\n装填开始/完成 "+a.reloads+" / "+a.completedReloads+"；记录的沉默/震荡事件 "+a.controls+"；换位 "+a.swaps+"；承受生命伤害 "+a.healthLost.ToString("0.0")+
                    "\n各指标用途不同，不合成强度分。扫描等信息能力请结合实际选路判断。",new Rect(x+24,310,w-48,170),23,Paper);
                Text("本次不发放正式货币、装备或通关奖励。采购使用已重新读取的当前余额和拥有状态。",new Rect(x+24,534,w-48,80),19,Muted);
                if(Button(TrialAccountReadBlocked?"重新读取当前档案":"回到原商品",new Rect(x+24,651,w-48,50),true)){if(TrialAccountReadBlocked)RetryTrialAccountRead();else trialResultVisible=false;}return;
            }
            Text("定向试用 / "+TrialProductName(trialRequest),new Rect(x+24,184,w-48,45),30,Paper);
            Text("选择场景：固定其它装备和基础配件，改变本次比较的商品。",new Rect(x+24,246,w-48,40),20,Muted);
            if(Button("近距与转场 / 第1章",new Rect(x+24,300,(w-64)/2,49),trialScenario==0))trialScenario=0;
            if(Button("长视线与火力间歇 / 第5章",new Rect(x+40+(w-64)/2,300,(w-64)/2,49),trialScenario==1))trialScenario=1;
            if(trialRequest.kind!="weapon")
            {
                if(Button("按本次采购结果等级",new Rect(x+24,372,(w-64)/2,46),!trialEqualTier))trialEqualTier=false;
                if(Button("明确比较3级效果",new Rect(x+40+(w-64)/2,372,(w-64)/2,46),trialEqualTier))trialEqualTier=true;
            }
            var preview=MakeTrialRequest(trialRequest.kind,trialRequest.productId,trialScenario,trialEqualTier);
            Text((preview.kind=="weapon"?"武器基础参数；"+(preview.formalLevel>0?"正式已拥有":"正式未拥有")+" / 采购标价 "+preview.purchasePrice+" 信用点。\n":"试用 "+preview.level+"级；正式拥有 "+preview.formalLevel+"级；本次采购结果 "+preview.purchaseLevel+"级 / 标价 "+preview.purchasePrice+" 信用点。\n")+
                (preview.kind!="weapon"&&preview.formalLevel==0?"购买基础1级为100；从1升到3另需260。可领取的保障请先在商品页领取。":"购买与强化仍需在返回后自行点击；武器须满足上架和职业资格。"),new Rect(x+24,457,w-48,105),22,Paper);
            if(Button("启动独立试用窗口",new Rect(x+24,645,(w-64)/2,52),true))StartDirectedTrial(preview);
            if(Button("取消",new Rect(x+40+(w-64)/2,645,(w-64)/2,52)))trialDialog=false;
        }
    }
}
