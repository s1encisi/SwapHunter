using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace SwapHunter
{
 public sealed partial class ReviewFixQA
 {
  IEnumerator CoreChecks()
  {
   G.qaSuppressAI=true;
   G.StartTutorial();yield return new WaitForSeconds(.1f);
   G.SetState(RunState.Paused);
   Check("tutorial_pause_preserves_lesson",G.tutorialCourse&&G.tutorialCourse.Active,"Pause must keep the current lesson available to resume");
   G.SetState(RunState.Menu);
   Check("tutorial_menu_ends_lesson",G.tutorialCourse&&!G.tutorialCourse.Active,"Actual return-to-menu callback; no manual TutorialCourse.End in this test");
   G.OpenCampaignBoard();bool began=G.BeginContract(G.catalog.contracts[0].id);
   ClearActors();int wave=G.wave;
   yield return new WaitForSeconds(4.2f);
   Check("tutorial_to_contract_wave_advances",began&&!G.tutorialCourse.Active&&G.wave>wave,"First wave removed as fixture; before="+wave+";after="+G.wave);
   var station=G.bonusStation;
   if(station)
   {
    P.SetFeet(station.transform.position+Vector3.back*2+Vector3.up*.03f);P.verticalSpeed=0;Aim(station.Point);
    yield return new WaitForSeconds(.1f);
    bool reachable=G.FindCampaignStation()==station,interacted=G.Interact();
    Check("tutorial_to_contract_interaction_restored",reachable&&interacted&&G.campaignBonus,"reachable="+reachable+"; interact="+interacted+"; bonus="+G.campaignBonus);
    yield return Capture("core-contract-after-teaching-exit");
   }
   else Check("tutorial_to_contract_interaction_restored",false,"Contract bonus station missing");
   ClearActors();if(G.campaignActive)G.FinishContract(false);

   // BeginContract is also a public entry point; it must end a live lesson even
   // when a caller did not pass through the menu first.
   G.StartTutorial();began=G.BeginContract(G.catalog.contracts[0].id);
   Check("direct_contract_entry_ends_lesson",began&&G.tutorialCourse&&!G.tutorialCourse.Active,"Public BeginContract called during an active tutorial");
   ClearActors();if(G.campaignActive)G.FinishContract(false);

   G.StartRun(true);ClearActors();G.combatComplete=true;G.qaSuppressAI=true;
   var fixture=new GameObject("Review fix shield descent fixture").transform;
   var center=new Vector3(100,0,0);
   Shapes.Box("Descent floor",fixture,center+Vector3.down*.5f,new Vector3(36,1,36),1,true,Layers.World);
   var nav=fixture.gameObject.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;
   nav.layerMask=Layers.WorldMask;nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders;nav.BuildNavMesh();
   // Deliberately post-bake: capsule and shield must obey real geometry independently of nav baking.
   Shapes.Box("Shield-first ledge",fixture,center+Vector3.up*.575f,new Vector3(2.6f,1.15f,1.8f),1,true,Layers.World);
   Physics.SyncTransforms();
   yield return CoreShieldDescent(center,true);
   yield return CoreShieldDescent(center,false);
   ClearActors();fixture.gameObject.SetActive(false);Destroy(fixture.gameObject);G.SetState(RunState.Menu);
  }

  IEnumerator CoreShieldDescent(Vector3 center,bool shieldOverLedge)
  {
   ClearActors();G.qaSuppressAI=true;
   string id=shieldOverLedge?"shield_first_descent":"clear_descent_control";
   var guard=EnemyActor.Spawn(EnemyKind.Shield,center+new Vector3(0,.03f,-6),G.stage);
   if(!guard){Check(id+"_setup",false,"Guard spawn failed");yield break;}
   guard.transform.rotation=Quaternion.Euler(0,180,0);
   P.ResetAt(center+new Vector3(0,3,shieldOverLedge?1.35f:2.1f));P.verticalSpeed=0;P.cooldown=0;Aim(guard.AimPoint);
   Physics.SyncTransforms();string validation=P.ValidateSwap(guard);int swaps=P.swapCount;
   bool requested=P.RequestSwap(guard);float until=Time.time+.3f;
   while(P.swapCount==swaps&&Time.time<until)yield return null;
   Vector3 start=guard.transform.position;
   var shield=guard.transform.Find("Shield").GetComponent<BoxCollider>();
   bool initialClear=!Physics.CheckBox(shield.transform.TransformPoint(shield.center),Vector3.Scale(shield.size,shield.transform.lossyScale)*.5f,shield.transform.rotation,Layers.WorldMask,QueryTriggerInteraction.Ignore);
   Check(id+"_setup",requested&&P.swapCount>swaps&&start.y>2&&initialClear,"validate="+validation+";feet="+start+";initialShieldClear="+initialClear);
   P.SetFeet(center+new Vector3(5,.03f,4));P.verticalSpeed=0;Aim(guard.AimPoint);
   int shieldOverlaps=0,bodyOverlaps=0;bool shieldStayedEnabled=true;float maxHorizontalStep=0;
   Vector3 previous=start;until=Time.time+2.5f;
   while(Time.time<until)
   {
    Physics.SyncTransforms();shieldStayedEnabled&=guard.HasPhysicalShield;
    if(Physics.CheckBox(shield.transform.TransformPoint(shield.center),Vector3.Scale(shield.size,shield.transform.lossyScale)*.5f,shield.transform.rotation,Layers.WorldMask,QueryTriggerInteraction.Ignore))shieldOverlaps++;
    Vector3 p=guard.transform.position;
    if(Physics.CheckCapsule(p+Vector3.up*.42f,p+Vector3.up*1.38f,.30f,Layers.WorldMask,QueryTriggerInteraction.Ignore))bodyOverlaps++;
    maxHorizontalStep=Mathf.Max(maxHorizontalStep,Vector3.ProjectOnPlane(p-previous,Vector3.up).magnitude);previous=p;
    yield return null;
   }
   float retreat=Vector3.ProjectOnPlane(guard.transform.position-start,Vector3.up).magnitude;
   bool landed=guard.Grounded&&!guard.motion.Airborne&&guard.agent.enabled&&guard.agent.isOnNavMesh&&guard.transform.position.y<.15f;
   Check(id+"_no_penetration",shieldStayedEnabled&&shieldOverlaps==0&&bodyOverlaps==0,"shieldOverlapFrames="+shieldOverlaps+";bodyOverlapFrames="+bodyOverlaps+";shieldEnabled="+shieldStayedEnabled);
   Check(id+"_lands_with_navigation",landed,"feet="+guard.transform.position+";airborne="+guard.motion.Airborne+";grounded="+guard.Grounded+";navEnabled="+guard.agent.enabled);
   Check(id+"_bounded_clearance_move",retreat<(shieldOverLedge?.8f:.05f)&&(!shieldOverLedge||retreat>.05f),"horizontalRetreat="+retreat+";maximumSampleStep="+maxHorizontalStep);
   Aim(guard.AimPoint);yield return Capture("core-"+id+"-landed");
  }
 }
}
