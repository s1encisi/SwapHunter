using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace SwapHunter
{
    // T1-T3 use real isolated child processes. "sample_complete" is a short QA sample, not a chapter clear.
    public static class SynergyTrialChecks
    {
        public static IEnumerator Run(Action<string,bool,string> add,string output) => new Suite(add,output).Run();
        sealed class Suite
        {
            readonly Action<string,bool,string> add;
            readonly string output;
            string formalRoot,formalFile;
            DemoGame G => DemoGame.I;
            ExpeditionStore Store => G.expeditionStore;
            public Suite(Action<string,bool,string> add,string output){this.add=add;this.output=output;}
            void C(string name,bool passed,string detail="")=>add(name,passed,detail);
            public IEnumerator Run()
            {
                if(!RunStorage.IsValidation||Application.isEditor){C("T_isolated_native_parent",false);yield break;}
                if(G.expeditionActive&&G.expeditionReceipt==null)G.AbandonExpedition();
                formalRoot=Path.Combine(RunStorage.Root,"SynergyTrials");Directory.CreateDirectory(formalRoot);
                formalFile=Path.Combine(formalRoot,"expedition-v6.json");
                var seed=TacticalDemoProfile.Create(0,true);
                // Unowned alternate skills do not invalidate any equipped/guaranteed starter.
                seed.classSkillLevels[0]=0;seed.generalLevels[1]=0;seed.classSkillLevels[2]=1;
                ExpeditionStore.Validate(seed);File.WriteAllText(formalFile,JsonUtility.ToJson(seed));
                G.expeditionStore=new ExpeditionStore(formalRoot,true);G.OpenExpeditionBoard();yield return null;
                try
                {
                    var unowned=G.MakeTrialRequest("class",0,0,false);
                    var upgrade=G.MakeTrialRequest("class",2,0,false);
                    var equal=G.MakeTrialRequest("class",2,1,true);
                    var general=G.MakeTrialRequest("general",1,0,false);
                    var weapon=G.MakeTrialRequest("weapon",8,0,false);
                    C("T2_unowned_previews_level_one",unowned.formalLevel==0&&unowned.level==1&&unowned.purchaseLevel==1&&unowned.purchasePrice==100&&!unowned.equalTier);
                    C("T2_upgrade_previews_next_level",upgrade.formalLevel==1&&upgrade.level==2&&upgrade.purchaseLevel==2&&!upgrade.equalTier);
                    C("T2_equal_three_keeps_real_purchase_level",equal.equalTier&&equal.level==3&&equal.formalLevel==1&&equal.purchaseLevel==2&&equal.purchasePrice==upgrade.purchasePrice&&equal.scenario==1);
                    C("T2_general_and_weapon_legal_request",general.level==1&&general.formalLevel==0&&general.purchaseLevel==1&&weapon.classId==1&&TacticalPrototypeRules.ClassCanUseWeapon(weapon.classId,weapon.productId));

                    G.OpenSkillTrial(0,false);
                    C("T2_dialog_defaults_to_purchase_tier",G.TrialDialogOpen&&G.CurrentTrialRequest!=null&&G.CurrentTrialRequest.productId==0&&G.CurrentTrialRequest.level==1&&!G.CurrentTrialRequest.equalTier);
                    yield return Capture("trial-configuration.png");
                    yield return CompletedChild(unowned,"return","returned","unowned");
                    yield return Capture("trial-return-summary.png");
                    yield return CompletedChild(upgrade,"death","death","upgrade");
                    yield return CompletedChild(equal,"success","sample_complete","equal");
                    yield return KilledChild();
                    yield return ExternalPurchase();
                    yield return ReadFailure();
                }
                finally
                {
                    if(G.TrialSessionActive)G.CloseTrialSession();
                    G.OpenExpeditionBoard();
                }
            }
            IEnumerator WaitForChild(string label)
            {
                float deadline=Time.realtimeSinceStartup+60;
                while(G.TrialSessionActive&&Time.realtimeSinceStartup<deadline)yield return null;
                if(G.TrialSessionActive)
                {
                    C("T1_child_timeout_"+label,false,"Native child did not return within 60 real seconds; folder="+G.LastTrialSessionDirectory);
                    G.TerminateTrialForReview();float cleanup=Time.realtimeSinceStartup+5;
                    while(G.TrialSessionActive&&Time.realtimeSinceStartup<cleanup)yield return null;
                    if(G.TrialSessionActive)G.CloseTrialSession();
                }
            }
            IEnumerator CompletedChild(DirectedTrialRequest request,string smoke,string expected,string label)
            {
                string before=File.ReadAllText(formalFile);int balance=Store.Profile.credits;
                bool started=G.StartDirectedTrial(request,smoke);
                C("T1_child_started_"+label,started,"session="+request.sessionId+";scenario="+request.scenario+";tier="+request.level);
                if(!started)yield break;
                yield return WaitForChild(label);
                var result=G.LastTrialResult;
                bool valid=!G.TrialSessionActive&&result!=null&&result.complete&&result.sessionId==request.sessionId&&result.outcome==expected&&result.mode=="QA"&&result.activeSeconds>0;
                C("T1_child_result_"+label,valid,"expected="+expected+";actual="+(result==null?"missing":result.outcome)+";activeSeconds="+(result==null?-1:result.activeSeconds));
                C("T1_formal_bytes_unchanged_"+label,File.ReadAllText(formalFile)==before&&Store.Profile.credits==balance&&string.IsNullOrEmpty(Store.Profile.activeRun),"Formal parent profile compared byte-for-byte; no trial rewards imported");
                C("T1_returned_to_original_product_"+label,G.ExpeditionTabForReview==request.returnTab&&G.ExpeditionWeaponForReview==request.selectedWeapon&&G.state==RunState.Menu&&G.expeditionBoard);
                if(valid)InspectChildProfile(request,result,label);
            }
            void InspectChildProfile(DirectedTrialRequest request,DirectedTrialResult result,string label)
            {
                string allowed=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"SHD"))+Path.DirectorySeparatorChar;
                string child=string.IsNullOrEmpty(result.isolatedRoot)?"":Path.GetFullPath(result.isolatedRoot);
                bool separate=child.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)&&!string.Equals(child,Path.GetFullPath(formalRoot),StringComparison.OrdinalIgnoreCase);
                C("T2_child_storage_isolated_"+label,separate,"reported child root="+child);
                if(!separate)return;
                string path=Path.Combine(child,"Expedition-Tactical-v6","expedition-v6.json");
                if(!File.Exists(path)){C("T2_child_profile_exists_"+label,false,path);return;}
                var profile=JsonUtility.FromJson<ExpeditionProfile>(File.ReadAllText(path));
                bool validated=true;string error="";
                try{ExpeditionStore.Validate(profile);}catch(Exception e){validated=false;error=e.Message;}
                if(!validated){C("T2_child_profile_valid_"+label,false,error);return;}
                var loadout=profile.classLoadouts[request.classId];
                int expectedClass=request.kind=="class"?request.productId:request.classId==0?1:request.classId*2;
                int expectedGeneral=request.kind=="general"?request.productId:0;
                int expectedPrimary=request.kind=="weapon"?request.productId:0;
                int expectedSecondary=request.kind=="weapon"&&request.productId!=0?0:TacticalPrototypeRules.StarterWeapon(request.classId);
                bool fixedConditions=profile.activeClass==request.classId&&loadout.primary==expectedPrimary&&loadout.secondary==expectedSecondary
                    &&loadout.classSkillId==TacticalPrototypeRules.SkillIds[expectedClass]&&loadout.tacticalSkillId==TacticalPrototypeRules.GeneralSkillIds[expectedGeneral]
                    &&profile.credits==0;
                for(int i=0;i<loadout.attachments.Length;i++)fixedConditions&=loadout.attachments[i]==0;
                for(int i=0;i<loadout.itemTypes.Length;i++)fixedConditions&=loadout.itemTypes[i]==-1&&loadout.itemCounts[i]==0;
                for(int i=0;i<profile.classSkillLevels.Length;i++)fixedConditions&=profile.classSkillLevels[i]==(request.kind=="class"&&i==request.productId?request.level:1);
                for(int i=0;i<profile.generalLevels.Length;i++)fixedConditions&=profile.generalLevels[i]==(request.kind=="general"&&i==request.productId?request.level:1);
                if(profile.deployment!=null)
                {
                    fixedConditions&=profile.deployment.classId==request.classId&&profile.deployment.primary==expectedPrimary&&profile.deployment.secondary==expectedSecondary;
                    foreach(int carried in profile.deployment.carriedConsumables)fixedConditions&=carried==0;
                }
                C("T2_real_child_profile_fixed_"+label,fixedConditions,"Read and Validate child-generated profile; requested="+request.kind+"/"+request.productId+"/level"+request.level+";primary="+loadout.primary+";secondary="+loadout.secondary);
            }
            IEnumerator KilledChild()
            {
                string before=File.ReadAllText(formalFile);var request=G.MakeTrialRequest("weapon",8,0,false);
                bool started=G.StartDirectedTrial(request,"return");C("T1_kill_child_started",started);
                if(!started)yield break;
                // Kill before the 1.2s smoke completion; no invented result file is supplied.
                string folder=G.LastTrialSessionDirectory;G.TerminateTrialForReview();yield return WaitForChild("killed");
                C("T1_missing_result_recovers_parent",!G.TrialSessionActive&&G.LastTrialResult==null&&!File.Exists(Path.Combine(folder,"result.json"))&&G.state==RunState.Menu&&G.expeditionBoard,
                    "notice="+G.TrialReturnNotice);
                C("T1_kill_formal_bytes_unchanged",File.ReadAllText(formalFile)==before);
            }
            IEnumerator ExternalPurchase()
            {
                var request=G.MakeTrialRequest("class",0,0,false);int oldBalance=Store.Profile.credits;
                bool started=G.StartDirectedTrial(request,"return");C("T3_child_started_before_external_purchase",started);
                if(!started)yield break;
                var external=new ExpeditionStore(formalRoot,true);string purchaseId="ssu-trial-"+Guid.NewGuid().ToString("N");
                int receiptCount=external.Profile.purchases.Count;
                var receipt=external.BuySkill(purchaseId,0);int newBalance=external.Profile.credits;
                external.BuySkill(purchaseId,0);
                string committed=File.ReadAllText(formalFile);
                C("T3_external_purchase_once",newBalance==oldBalance-receipt.totalCost&&external.Profile.credits==newBalance&&external.Profile.purchases.Count==receiptCount+1&&external.Profile.classSkillLevels[0]==1,
                    "before="+oldBalance+";after="+newBalance+";cost="+receipt.totalCost);
                C("T3_parent_was_not_silently_updated",Store.Profile.credits==oldBalance,"Independent store committed while parent still held its old in-memory balance");
                yield return WaitForChild("external-purchase");
                C("T3_return_reads_current_account",!G.TrialSessionActive&&G.LastTrialResult!=null&&Store.Profile.credits==newBalance&&Store.Profile.classSkillLevels[0]==1&&File.ReadAllText(formalFile)==committed,
                    "currentBalance="+Store.Profile.credits+";externalBalance="+newBalance);
                var retried=Store.BuySkill(purchaseId,0);
                C("T3_parent_retry_does_not_debit_twice",retried.requestId==purchaseId&&Store.Profile.credits==newBalance&&Store.Profile.purchases.Count==receiptCount+1&&File.ReadAllText(formalFile)==committed);
                var next=G.MakeTrialRequest("class",0,0,false);
                C("T3_next_quote_uses_new_owned_level",next.formalLevel==1&&next.purchaseLevel==2&&next.level==2,"The completed level-1 trial does not fix the later purchase quote");
            }
            IEnumerator ReadFailure()
            {
                var request=G.MakeTrialRequest("weapon",0);string before=File.ReadAllText(formalFile);
                bool started=G.StartDirectedTrial(request,"return");C("T3_read_failure_child_started",started);if(!started)yield break;
                using(var held=new FileStream(formalFile,FileMode.Open,FileAccess.Read,FileShare.None))
                {
                    yield return WaitForChild("locked-account");
                    C("T3_read_failure_blocks_stale_deployment",G.TrialAccountReadBlocked&&!G.BeginExpedition(0),G.TrialReturnNotice);
                }
                C("T3_read_retry_restores_current_account",G.RetryTrialAccountRead()&&!G.TrialAccountReadBlocked&&File.ReadAllText(formalFile)==before,"Retry reads, never writes an entry-time account snapshot.");
            }
            IEnumerator Capture(string name)
            {
                Directory.CreateDirectory(output);string path=Path.Combine(output,name);
                // Native framebuffer capture only; no synthetic reconstruction and no batch WaitForEndOfFrame.
                ScreenCapture.CaptureScreenshot(path);float until=Time.realtimeSinceStartup+3;
                do{yield return null;}while(!File.Exists(path)&&Time.realtimeSinceStartup<until);
                C("T_native_capture_"+Path.GetFileNameWithoutExtension(name),File.Exists(path)&&new FileInfo(path).Length>0,path+";native parent framebuffer, not a human/front-window playtest");
            }
        }
    }
}
