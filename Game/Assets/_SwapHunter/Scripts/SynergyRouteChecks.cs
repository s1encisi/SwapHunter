using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace SwapHunter
{
    public static class SynergyRouteChecks
    {
        static bool Reject(Action action){try{action();return false;}catch(InvalidOperationException){return true;}}
        public static IEnumerator Run(Action<string,bool,string> add,string output)
        {
            var g=DemoGame.I;g.qaSuppressAI=true;
            if(g.expeditionActive&&g.expeditionReceipt==null)g.AbandonExpedition();g.OpenExpeditionBoard();
            var folder=Path.Combine(RunStorage.Root,"SynergyRoutes");Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,"expedition-v6.json"),JsonUtility.ToJson(TacticalDemoProfile.Create(0,true)));
            var store=new ExpeditionStore(folder,true);g.expeditionStore=store;int money=store.Profile.credits;
            bool legal=true;foreach(var r in TacticalLoadoutRoutes.All)
            {
                store.ApplyTacticalRoute(r.classId,r.primary,r.secondary,r.classSkill,r.general);
                legal&=store.ActiveClass==r.classId&&store.Primary==r.primary&&store.Secondary==r.secondary&&store.EquippedClassSkill==r.classSkill&&store.EquippedGeneralSkill==r.general&&store.Profile.credits==money&&store.Profile.purchases.Count==0;
            }
            add("L1_eight_owned_routes_apply_without_purchase",legal&&TacticalLoadoutRoutes.All.Length==8,"Actual store transactions; all eight distinct legal routes.");
            string before=JsonUtility.ToJson(store.Profile);bool cross=Reject(()=>store.ApplyTacticalRoute(0,10,0,1,0));
            add("L1_illegal_cross_class_rejected",cross&&before==JsonUtility.ToJson(store.Profile),"No partial class/equipment mutation.");
            Directory.CreateDirectory(store.SavePath+".tmp");bool failed=false;
            try{store.ApplyTacticalRoute(0,4,5,1,0);}catch(Exception e)when(e is IOException||e is UnauthorizedAccessException){failed=true;}
            Directory.Delete(store.SavePath+".tmp");add("L1_route_write_failure_atomic",failed&&before==JsonUtility.ToJson(store.Profile),"One copied-profile commit, not separate slot writes.");
            string run=store.Begin(0);add("L1_active_deployment_blocks_route",Reject(()=>store.ApplyTacticalRoute(0,4,5,1,0)),"Idle-only configuration.");store.Settle(run,false,false,0);
            var missing=new ExpeditionStore(Path.Combine(RunStorage.Root,"SynergyMissingRoute"),true);missing.ClaimAssaultStarter();before=JsonUtility.ToJson(missing.Profile);
            var desired=TacticalLoadoutRoutes.All[0];bool rejected=Reject(()=>missing.ApplyTacticalRoute(desired.classId,desired.primary,desired.secondary,desired.classSkill,desired.general));
            add("L1_missing_unlisted_items_do_not_autobuy",rejected&&!desired.Owned(missing.Profile)&&!desired.Listed(missing.Profile)&&desired.MissingListPrice(missing.Profile)>0&&before==JsonUtility.ToJson(missing.Profile),"Missing prices are purchase list prices; starter guarantees are separately shown.");
            g.SetExpeditionTabForReview(4);g.SelectRouteForReview(-1);
            if(!Application.isBatchMode)
            {
                yield return new WaitForSecondsRealtime(.3f);ScreenCapture.CaptureScreenshot(Path.Combine(output,"synergy-routes.png"));yield return new WaitForSecondsRealtime(.2f);
                g.SelectRouteForReview(6);yield return new WaitForSecondsRealtime(.2f);ScreenCapture.CaptureScreenshot(Path.Combine(output,"synergy-route-detail.png"));yield return new WaitForSecondsRealtime(.2f);
                add("L1_native_route_images",File.Exists(Path.Combine(output,"synergy-routes.png"))&&File.Exists(Path.Combine(output,"synergy-route-detail.png")),"Native window captures, not OS mouse or human testing.");
            }
            g.SelectRouteForReview(-1);g.SetExpeditionTabForReview(1);
        }
    }
}
