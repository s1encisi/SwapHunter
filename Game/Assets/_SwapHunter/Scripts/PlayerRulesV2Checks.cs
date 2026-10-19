using System;using System.Collections;using System.IO;using UnityEngine;using Unity.AI.Navigation;
namespace SwapHunter{public static class PlayerRulesV2Checks{
 public static IEnumerator Run(Action<string,bool,string> add)=>new Fixture(add).Run();
 sealed class Fixture{
  readonly Action<string,bool,string> add;DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;Transform root;PhaseAnchor anchor;readonly Vector3 origin=new Vector3(2200,0,0);
  public Fixture(Action<string,bool,string> add){this.add=add;}void C(string n,bool p,string d="")=>add(n,p,d);
  IEnumerator RunChecks(){
   var raw=new[]{24f,12,19,38,13,14,62,118,21,95,145,16};bool scaled=true;for(int i=0;i<12;i++)scaled&=Mathf.Abs(EffectiveWeaponStats.Read(i,G.config).Damage-raw[i]*3)<.001f;
   C("V2_damage_baseline_once",scaled&&ClassSkillTuning.RemoteDamage(1)==192&&ClassSkillTuning.DenialDps(1)==48);
   P.enabled=false;P.SetFeet(origin+new Vector3(0,.03f,0));P.SetLook(0,0);P.cooldown=0;P.CancelActions();
   foreach(float distance in new[]{49.9f,50f,50.1f}){anchor.MoveTo(P.Eye.position+Vector3.forward*distance-Vector3.up*.95f);Physics.SyncTransforms();var result=P.ValidateSwap(anchor);C("V2_Q_distance_"+distance,result.IsValid==(distance<=50),"code="+result.Code+";actualAimDistance="+Vector3.Distance(P.Eye.position,anchor.AimPoint));}
   anchor.MoveTo(origin+new Vector3(0,12,49));Physics.SyncTransforms();C("V2_Q_uses_3d",P.ValidateSwap(anchor).Code==SwapFailureReason.OutOfRange);
   var shield=DirectionalBarrier.Spawn(origin+new Vector3(0,1.2f,5),Vector3.forward,4,P.transform);
   C("V2_Ashield_direction_all_sides",DirectionalBarrier.BlocksSegment(origin+new Vector3(0,1.2f,8),origin+new Vector3(0,1.2f,2))&&!DirectionalBarrier.BlocksSegment(origin+new Vector3(0,1.2f,2),origin+new Vector3(0,1.2f,8)));
   C("V2_Ashield_explosion_direction",DirectionalBarrier.ExplosionMultiplier(origin+new Vector3(0,1,8),origin+new Vector3(0,1,2))==.35f&&DirectionalBarrier.ExplosionMultiplier(origin+new Vector3(0,1,2),origin+new Vector3(0,1,8))==1);
   P.SetFeet(origin+new Vector3(6,.03f,0));C("V2_Ashield_stays_world",Vector3.Distance(shield.transform.position,origin+new Vector3(0,1.2f,5))<.001f);UnityEngine.Object.Destroy(shield.gameObject);
   P.SetFeet(origin+Vector3.up*.03f);P.SetLook(0,0);P.enabled=true;yield return null;Vector3 start=P.transform.position;
   bool began=P.classSkill.ActivateEquipped();yield return new WaitForSeconds(.5f);
   float moved=Vector3.ProjectOnPlane(P.transform.position-start,Vector3.up).magnitude;
   C("V2_advance_8m_ground",began&&moved>7.5f&&moved<8.6f,"moved="+moved+";cooldown="+P.classSkill.CooldownLeft);
   float before=P.classSkill.CooldownLeft;var actor=EnemyActor.Spawn(EnemyKind.Assault,origin+new Vector3(5,.03f,15),0);actor.Damage(100000);yield return null;
   C("V2_advance_kill_no_refresh",P.classSkill.CooldownLeft>before-.2f&&P.classSkill.CooldownLeft<=before);
   P.StopV2Travel();P.SetFeet(origin+new Vector3(0,.03f,-20));P.verticalSpeed=0;yield return null;
   Vector3 departure=P.transform.position,landing=departure+Vector3.forward*20;bool launched=P.LaunchTo(landing);
   yield return new WaitForSeconds(1.4f);
   C("V2_jump_horizontal20",launched&&Vector3.Distance(Vector3.ProjectOnPlane(P.transform.position-departure,Vector3.up),Vector3.forward*20)<1,"actual="+P.transform.position);
   P.StopV2Travel();P.enabled=false;
   // Store transaction proof: new knife ownership is success-only, independent of world visuals.
   string folder=Path.Combine(RunStorage.Root,"knife-store");Directory.CreateDirectory(folder);var seed=TacticalDemoProfile.Create(0,true);File.WriteAllText(Path.Combine(folder,"expedition-v6.json"),JsonUtility.ToJson(seed));var store=new ExpeditionStore(folder,true);
   string run=store.Begin(0);store.Settle(run,false,false,1,0,0,0,new[]{2},2);C("V2_knife_failure_not_owned",!store.Profile.meleeKnives[2]);
   run=store.Begin(0);store.Settle(run,true,false,1,0,0,0,new[]{2},2);int balance=store.Profile.credits;store.Settle(run,true,false,1,0,0,0,new[]{2},2);
   C("V2_knife_success_once_and_equipped",store.Profile.meleeKnives[2]&&store.Profile.classLoadouts[0].knifeId==2&&store.Profile.credits==balance);
  }
  public IEnumerator Run(){
   if(!RunStorage.IsValidation)throw new InvalidOperationException("isolated only");
   if(G.StoryModal)G.CloseStoryModal();if(G.expeditionActive&&G.expeditionReceipt==null)G.AbandonExpedition();G.OpenExpeditionBoard();
   string folder=Path.Combine(RunStorage.Root,"player-rules");Directory.CreateDirectory(folder);var seed=TacticalDemoProfile.Create(0,true);seed.classLoadouts[0].classSkillId=TacticalPrototypeRules.SkillIds[0];seed.classSkillLevels[0]=1;
   File.WriteAllText(Path.Combine(folder,"expedition-v6.json"),JsonUtility.ToJson(seed));G.expeditionStore=new ExpeditionStore(folder,true);G.BeginExpedition(0);
   root=new GameObject("V2 player rule fixture").transform;Shapes.Box("Fixture floor",root,origin+Vector3.down*.5f,new Vector3(120,1,140),1,true,Layers.World);
   var nav=root.gameObject.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;nav.layerMask=Layers.WorldMask;RuntimeNavigationOwner.BuildFresh(nav);
   var go=new GameObject("50m Q fixture");go.layer=Layers.Actor;go.transform.SetParent(root,false);anchor=go.AddComponent<PhaseAnchor>();var body=go.AddComponent<CapsuleCollider>();body.height=1.4f;body.radius=.32f;body.center=Vector3.up*.7f;G.anchors.Add(anchor);
   try{yield return RunChecks();}
   finally{P.enabled=true;P.StopV2Travel();if(anchor)G.anchors.Remove(anchor);root.gameObject.SetActive(false);UnityEngine.Object.Destroy(root.gameObject);if(G.expeditionActive&&G.expeditionReceipt==null)G.AbandonExpedition();G.OpenExpeditionBoard();}
  }
 }
}}

