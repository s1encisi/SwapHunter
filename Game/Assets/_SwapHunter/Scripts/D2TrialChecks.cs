using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace SwapHunter
{
    // Only new D2 routing/seeds and three real child-scene startups; old trial failure contracts stay separate.
    public static class D2TrialChecks
    {
        public static IEnumerator Run(Action<string,bool,string> add)=>new Fixture(add).Run();
        sealed class Fixture
        {
            readonly Action<string,bool,string> add;DemoGame G=>DemoGame.I;
            public Fixture(Action<string,bool,string> add){this.add=add;}
            void C(string id,bool ok,string detail="")=>add(id,ok,detail);
            static DirectedTrialRequest Request(string kind,int product,int cls,int scenario,int level=2)=>new DirectedTrialRequest{
                sessionId=Guid.NewGuid().ToString("N"),kind=kind,productId=product,classId=cls,scenario=scenario,level=level,
                formalLevel=level-1,purchaseLevel=level,purchasePrice=100,rulesVersion=TacticalPrototypeRules.Version};
            static bool Reject(DirectedTrialRequest r){try{DirectedTrialBootstrap.Validate(r);return false;}catch(ArgumentException){return true;}}
            public IEnumerator Run()
            {
                C("T_D2_routes_stable",D2TrialCatalog.LevelFor(0)==0&&D2TrialCatalog.LevelFor(1)==4&&D2TrialCatalog.LevelFor(2)==0&&D2TrialCatalog.LevelFor(3)==4&&D2TrialCatalog.LevelFor(4)==4);
                C("T_D2_incompatible_requests_rejected",Reject(Request("class",0,0,2))&&Reject(Request("class",6,3,4))&&Reject(Request("general",0,3,4))&&Reject(Request("class",7,3,5)));
                foreach(var r in new[]{Request("class",6,3,2),Request("class",7,3,4,3),Request("weapon",10,3,2,1),Request("general",1,3,2)})
                {
                    var p=DirectedTrialBootstrap.CreateProfile(r);var l=p.classLoadouts[r.classId];
                    bool product=r.kind=="weapon"?l.primary==r.productId||l.secondary==r.productId:r.kind=="class"?
                        l.classSkillId==TacticalPrototypeRules.SkillIds[r.productId]&&p.classSkillLevels[r.productId]==r.level:
                        l.tacticalSkillId==TacticalPrototypeRules.GeneralSkillIds[r.productId]&&p.generalLevels[r.productId]==r.level;
                    bool legal=TacticalPrototypeRules.ClassCanUseWeapon(r.classId,l.primary)&&TacticalPrototypeRules.ClassCanUseWeapon(r.classId,l.secondary)
                        &&l.primary!=l.secondary&&(r.scenario!=2||l.primary==11||l.secondary==11);
                    bool clean=p.credits==0&&p.receipts.Count==0&&Array.TrueForAll(p.cleared,c=>!c)&&Array.TrueForAll(l.itemCounts,n=>n==0);
                    C("T_D2_seed_keeps_product_"+r.kind+"_"+r.productId,product&&legal&&clean,"scenario="+r.scenario+";tier="+r.level+";weapons="+l.primary+"/"+l.secondary);
                }
                if(!RunStorage.IsValidation||Application.isEditor){C("T_D2_native_isolated_parent",false);yield break;}
                if(G.expeditionActive&&G.expeditionReceipt==null)G.AbandonExpedition();
                string folder=Path.Combine(RunStorage.Root,"D2Trials");Directory.CreateDirectory(folder);
                string file=Path.Combine(folder,"expedition-v6.json");var seed=TacticalDemoProfile.Create(3,true);
                seed.classSkillLevels[6]=1;seed.classSkillLevels[7]=0;ExpeditionStore.Validate(seed);
                File.WriteAllText(file,JsonUtility.ToJson(seed));G.expeditionStore=new ExpeditionStore(folder,true);G.OpenExpeditionBoard();yield return null;
                try
                {
                    foreach(int scenario in new[]{2,3,4})
                    {
                        var request=scenario==2?G.MakeTrialRequest("class",6,scenario):scenario==3?G.MakeTrialRequest("weapon",10,scenario):G.MakeTrialRequest("class",7,scenario);
                        string before=File.ReadAllText(file);bool started=G.StartDirectedTrial(request,"success");
                        C("T_D2_child_started_"+scenario,started,"requested product="+request.kind+":"+request.productId+";tier="+request.level);
                        if(!started)continue;
                        float until=Time.realtimeSinceStartup+60;
                        while(G.TrialSessionActive&&Time.realtimeSinceStartup<until)yield return null;
                        if(G.TrialSessionActive){G.TerminateTrialForReview();C("T_D2_child_timeout_"+scenario,false,G.LastTrialSessionDirectory);yield return new WaitForSecondsRealtime(.3f);G.TickTrialSession();}
                        var result=G.LastTrialResult;
                        C("T_D2_scene_result_"+scenario,result!=null&&result.complete&&result.scenarioId==scenario&&result.outcome=="sample_complete"
                            &&!string.IsNullOrEmpty(result.scenarioSummary),result==null?"missing result":result.scenarioSummary);
                        C("T_D2_formal_profile_unchanged_"+scenario,File.ReadAllText(file)==before&&string.IsNullOrEmpty(G.expeditionStore.Profile.activeRun)&&G.state==RunState.Menu);
                        if(result!=null)
                        {
                            string profilePath=Path.Combine(result.isolatedRoot,"Expedition-Tactical-v6","expedition-v6.json");
                            var actual=JsonUtility.FromJson<ExpeditionProfile>(File.ReadAllText(profilePath));var l=actual.classLoadouts[request.classId];
                            C("T_D2_real_child_product_"+scenario,request.kind=="weapon"?(l.primary==request.productId||l.secondary==request.productId):
                                l.classSkillId==TacticalPrototypeRules.SkillIds[request.productId]&&actual.classSkillLevels[request.productId]==request.level,
                                "real isolated profile; " +profilePath);
                        }
                    }
                }
                finally{if(G.TrialSessionActive)G.CloseTrialSession();G.OpenExpeditionBoard();}
            }
        }
    }
}
