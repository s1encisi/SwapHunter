using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace SwapHunter
{
 public static class MapV2GeometryChecks
 {
  [Serializable] public sealed class Geometry
  {
   public int chapter;public string state,scope="Isolated authored-map geometry fixture. Passage states are opened explicitly; this is not a played completion or combat balance result.";
   public Vector3 spawn;public Vector3[] vertices;public int[] indices,walkReachableTriIndices,anchorReachableTriIndices;
   public List<JumpPad> jumpPads=new List<JumpPad>();public List<Anchor> anchors=new List<Anchor>();public List<Target> targets=new List<Target>();public List<Feature> features=new List<Feature>();public List<Region> regions=new List<Region>();public List<Floor> floors=new List<Floor>();public List<AnchorEdge> validatedAnchorEdges=new List<AnchorEdge>();public List<View> views=new List<View>();public List<Configuration> configurations=new List<Configuration>();
  }
  [Serializable] public sealed class JumpPad {public Vector3 launch,landing;public float horizontalMetres;public bool sourceGrounded,landingGrounded,pathClear;public string reason;}
  [Serializable] public sealed class Anchor {public int instance;public Vector3 position;public float radius,height;public bool sampled,walkReachable,anchorReachable;}
  [Serializable] public sealed class Target {public string id,kind;public Vector3 requested,standingPoint;public bool sampled,walkReachable,anchorReachable,clearance;}
  [Serializable] public sealed class Feature {public string id,purpose,spatialRole,interaction,adjacency;public Vector3 position;}
  [Serializable] public sealed class Region {public string id;public Bounds bounds;}
  [Serializable] public sealed class Floor {public string zone;public Vector2 center,size;public float height;}
  [Serializable] public sealed class AnchorEdge {public int anchorInstance,fromComponent,toComponent;public Vector3 source,destination;public float distance;public string validation;}
  [Serializable] public sealed class View {public string file;public Vector3 feet,lookAt;}
  [Serializable] public sealed class Configuration {public int classId,skillId,generalId;public bool validSnapshot,taskRoutes;public int carriedConsumables;public string scope="Domain Begin freezes this real loadout; same actual navigation/anchor-route graph is checked. No simulated fight or claimed victory.";}
  sealed class Graph
  {
   public NavMeshTriangulation nav;public int[] component;public Vector3[] centers;public HashSet<int> walk=new HashSet<int>(),reachable=new HashSet<int>();
   int[] parent;
   int Find(int a){while(parent[a]!=a){parent[a]=parent[parent[a]];a=parent[a];}return a;}
   void Join(int a,int b){a=Find(a);b=Find(b);if(a!=b)parent[a]=b;}
   static string Key(Vector3 p)=>Mathf.RoundToInt(p.x*1000)+":"+Mathf.RoundToInt(p.y*1000)+":"+Mathf.RoundToInt(p.z*1000);
   public Graph(Vector3 spawn)
   {
    nav=NavMesh.CalculateTriangulation();int n=nav.indices.Length/3;parent=new int[n];component=new int[n];centers=new Vector3[n];var ids=new Dictionary<string,int>();var edges=new Dictionary<long,int>();int next=0;var vertexIds=new int[nav.vertices.Length];
    for(int i=0;i<vertexIds.Length;i++){string k=Key(nav.vertices[i]);if(!ids.TryGetValue(k,out int id)){id=next++;ids.Add(k,id);}vertexIds[i]=id;}
    for(int i=0;i<n;i++)parent[i]=i;
    for(int i=0;i<n;i++)
    {
     var a=nav.vertices[nav.indices[i*3]];var b=nav.vertices[nav.indices[i*3+1]];var c=nav.vertices[nav.indices[i*3+2]];centers[i]=(a+b+c)/3;
     for(int k=0;k<3;k++){int x=vertexIds[nav.indices[i*3+k]],y=vertexIds[nav.indices[i*3+(k+1)%3]];if(x>y){int z=x;x=y;y=z;}long key=((long)x<<32)|(uint)y;if(edges.TryGetValue(key,out int other))Join(i,other);else edges.Add(key,i);}
    }
    for(int i=0;i<n;i++)component[i]=Find(i);
    if(NavMesh.SamplePosition(spawn,out var start,2,NavMesh.AllAreas))
    {
     var representatives=new Dictionary<int,Vector3>();for(int i=0;i<n;i++)if(!representatives.ContainsKey(component[i]))representatives.Add(component[i],centers[i]);
     foreach(var pair in representatives){var path=new NavMeshPath();if(NavMesh.CalculatePath(start.position,pair.Value,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)walk.Add(pair.Key);}
    }
    reachable.UnionWith(walk);
   }
   public int Component(Vector3 p)
   {
    float best=float.PositiveInfinity;int result=-1;
    for(int i=0;i<centers.Length;i++)
    {
     Vector3 a=nav.vertices[nav.indices[i*3]],b=nav.vertices[nav.indices[i*3+1]],c=nav.vertices[nav.indices[i*3+2]];Vector3 normal=Vector3.Cross(b-a,c-a);float n=normal.magnitude;if(n<.00001f)continue;normal/=n;float h=Vector3.Dot(p-a,normal);Vector3 q=p-h*normal;
     float s0=Vector3.Dot(Vector3.Cross(b-a,q-a),normal),s1=Vector3.Dot(Vector3.Cross(c-b,q-b),normal),s2=Vector3.Dot(Vector3.Cross(a-c,q-c),normal);float distance=(s0>=-.002f&&s1>=-.002f&&s2>=-.002f)?Mathf.Abs(h):(p-centers[i]).magnitude;
     if(distance<best){best=distance;result=component[i];}
    }
    return result;
   }
   public int[] Triangles(HashSet<int> set){var output=new List<int>();for(int i=0;i<component.Length;i++)if(set.Contains(component[i]))output.Add(i);return output.ToArray();}
  }
  static DemoGame G=>DemoGame.I;
  static bool Stand(Vector3 requested,out Vector3 point,float radius=2.8f)
  {
   point=requested;
   for(int ring=0;ring<3;ring++)for(int i=0;i<(ring==0?1:12);i++)
   {
    Vector3 candidate=requested+new Vector3(Mathf.Cos(i*Mathf.PI/6),0,Mathf.Sin(i*Mathf.PI/6))*(ring==0?0:ring==1?1.8f:2.5f);
    if(!NavMesh.SamplePosition(candidate,out var hit,ring==0?.6f:.45f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-requested.y)>.7f)continue;
    if(!EnemyMotion.ClearVolume(hit.position,.32f,1.8f,G.player.transform))continue;point=hit.position+Vector3.up*.035f;return true;
   }
   return false;
  }
  static void CloseAnchorGraph(Graph graph,Geometry data)
  {
   var player=G.player;Vector3 original=player.transform.position;float cooldown=player.cooldown;bool progress=true;
   while(progress)
   {
    progress=false;
    foreach(var anchor in G.expeditionMap.anchors)
    {
     if(!anchor || !anchor.Alive || !NavMesh.SamplePosition(anchor.transform.position,out var landing,1,NavMesh.AllAreas))continue;
     int target=graph.Component(landing.position);if(target<0||graph.reachable.Contains(target))continue;
     var candidates=new List<int>();for(int i=0;i<graph.centers.Length;i++)if(graph.reachable.Contains(graph.component[i])&&Vector3.Distance(graph.centers[i],anchor.transform.position)<=50)candidates.Add(i);
     candidates.Sort((a,b)=>(graph.centers[a]-anchor.transform.position).sqrMagnitude.CompareTo((graph.centers[b]-anchor.transform.position).sqrMagnitude));
     foreach(int index in candidates)
     {
      Vector3 source=graph.centers[index]+Vector3.up*.035f;if(!EnemyMotion.ClearVolume(source,.32f,1.8f,player.transform))continue;
      player.SetFeet(source);player.cooldown=0;Physics.SyncTransforms();var check=player.ValidateSwap(anchor);
      if(!check.IsValid)continue;
      graph.reachable.Add(target);data.validatedAnchorEdges.Add(new AnchorEdge{anchorInstance=anchor.GetInstanceID(),fromComponent=graph.component[index],toComponent=target,source=source,destination=anchor.transform.position,distance=Vector3.Distance(source,anchor.transform.position),validation="Actual PlayerMotor.ValidateSwap passed: range, LOS, two endpoint volumes, interference. Edge feasibility only; swap not executed."});progress=true;break;
     }
    }
   }
   player.SetFeet(original);player.cooldown=cooldown;Physics.SyncTransforms();
  }
  static Geometry CaptureGraph(int level,string state)
  {
   var map=G.expeditionMap;var layout=map.root.GetComponent<MissionLayout>();var graph=new Graph(map.spawn);var data=new Geometry{chapter=level+1,state=state,spawn=map.spawn,vertices=graph.nav.vertices,indices=graph.nav.indices};CloseAnchorGraph(graph,data);data.walkReachableTriIndices=graph.Triangles(graph.walk);data.anchorReachableTriIndices=graph.Triangles(graph.reachable);
   void TargetAt(string id,string kind,Vector3 position,bool mini=false)
   {
    bool found=Stand(position,out var feet);if(kind=="entry")found&=EnemyMotion.GroundAt(position,out var actualGround)&&Mathf.Abs(actualGround.y-position.y)<.2f;int component=found?graph.Component(feet):-1;
    bool volume=!mini||!Physics.CheckBox(position+Vector3.up*1.25f,new Vector3(1.6f,1.2f,1.6f),Quaternion.identity,Layers.WorldMask,QueryTriggerInteraction.Ignore);
    data.targets.Add(new Target{id=id,kind=kind,requested=position,standingPoint=feet,sampled=found,walkReachable=found&&graph.walk.Contains(component),anchorReachable=found&&graph.reachable.Contains(component),clearance=volume});
   }
   TargetAt("spawn","entry",map.spawn);
   foreach(var pad in map.root.GetComponentsInChildren<V2JumpPad>())
   {
    Vector3 launch=pad.LaunchFeet+Vector3.up*.035f;bool grounded=EnemyMotion.GroundAt(launch,out var ground)&&Mathf.Abs(ground.y-launch.y)<.2f;
    bool landingGrounded=EnemyMotion.GroundAt(pad.Landing+Vector3.up*.035f,out var landingGround)&&Mathf.Abs(landingGround.y-pad.Landing.y)<.2f;
    bool clear=G.player.CanLaunchTo(launch,pad.Landing,out string reason);data.jumpPads.Add(new JumpPad{launch=launch,landing=pad.Landing,horizontalMetres=Vector3.ProjectOnPlane(pad.Landing-launch,Vector3.up).magnitude,sourceGrounded=grounded,landingGrounded=landingGrounded,pathClear=clear,reason=reason});
   }

   foreach(var n in map.nodes)if(n && (n.kind==ExpeditionNodeKind.Objective || n.kind==ExpeditionNodeKind.Extraction))TargetAt(layout.TryObjective(n,out string stable)?stable:"extraction",n.kind.ToString(),n.transform.position);
   foreach(var anchor in map.anchors)if(anchor){bool sampled=NavMesh.SamplePosition(anchor.transform.position,out var landing,1,NavMesh.AllAreas);int component=sampled?graph.Component(landing.position):-1;data.anchors.Add(new Anchor{instance=anchor.GetInstanceID(),position=anchor.transform.position,radius=anchor.Radius,height=anchor.Height,sampled=sampled,walkReachable=sampled&&graph.walk.Contains(component),anchorReachable=sampled&&graph.reachable.Contains(component)});}
   for(int i=0;i<map.walkingCheckpoints.Count;i++)TargetAt("permanent-route-"+i,"checkpoint",map.walkingCheckpoints[i]);
   for(int i=0;i<layout.MiniBossPosts.Length;i++)TargetAt("mini-post-"+i,"miniBoss",layout.MiniBossPosts[i],true);
   foreach(var f in map.root.GetComponentsInChildren<MapV2WorldFeature>(true))data.features.Add(new Feature{id=f.featureId,purpose=f.purpose,spatialRole=f.spatialRole,interaction=f.interaction,adjacency=f.adjacency,position=f.transform.position});
   foreach(var r in layout.Regions)data.regions.Add(new Region{id=r.Key,bounds=r.Value});
   foreach(var f in map.root.GetComponentsInChildren<MapV2Footprint>(true))if(f.gameObject.activeInHierarchy)data.floors.Add(new Floor{zone=f.zone,center=f.center,size=f.size,height=f.height});
   return data;
  }
  static void Configurations(Geometry data,string folder)
  {
   bool routes=true;foreach(var target in data.targets)if(target.kind=="Objective"||target.kind=="Extraction"||target.kind=="entry")routes&=target.anchorReachable&&target.clearance;
   var original=G.expeditionStore;var player=G.player;var oldWeapons=(int[])player.EquippedWeapons.Clone();
   try
   {
    for(int cls=0;cls<4;cls++)for(int skill=0;skill<2;skill++)for(int general=0;general<2;general++)
    {
     string path=Path.Combine(folder,"loadout-"+cls+"-"+skill+"-"+general);Directory.CreateDirectory(path);var profile=TacticalDemoProfile.Create(cls,true);for(int i=0;i<8;i++)profile.classSkillLevels[i]=1;profile.generalLevels[0]=profile.generalLevels[1]=1;
     var loadout=profile.classLoadouts[cls];loadout.classSkillId=TacticalPrototypeRules.SkillIds[cls*2+skill];loadout.tacticalSkillId=TacticalPrototypeRules.GeneralSkillIds[general];loadout.itemTypes=new[]{-1,-1};loadout.itemCounts=new[]{0,0};loadout.medkitCarry=0;profile.consumables=new[]{0,0,0};
     File.WriteAllText(Path.Combine(path,"expedition-v6.json"),JsonUtility.ToJson(profile));var store=new ExpeditionStore(path,true);string run=store.Begin(data.chapter-1);var deployment=store.Profile.deployment;G.expeditionStore=store;player.SetLoadout(deployment.primary,deployment.secondary);player.classSkill.ResetCombat();
     bool valid=deployment.classId==cls&&deployment.classSkillId==loadout.classSkillId&&deployment.tacticalSkillId==loadout.tacticalSkillId&&deployment.classSkillLevel==1&&deployment.generalSkillLevel==1;int consumables=0;foreach(int count in deployment.carriedConsumables)consumables+=count;
     data.configurations.Add(new Configuration{classId=cls,skillId=cls*2+skill,generalId=general,validSnapshot=valid&&consumables==0,taskRoutes=routes,carriedConsumables=consumables});store.Settle(run,false,false,0);
    }
   }
   finally{G.expeditionStore=original;player.SetLoadout(oldWeapons[0],oldWeapons[1]);player.classSkill.ResetCombat();}
  }
  static IEnumerator ViewAt(Geometry data,string output,string name,Vector3 preferred,Vector3 target)
  {
   bool found=Stand(preferred,out var feet);if(!found)foreach(var offset in new[]{Vector3.left*10,Vector3.right*10,Vector3.forward*10,Vector3.back*10}){if(Stand(target-Vector3.up+offset,out feet)){found=true;break;}}
   if(!found){data.views.Add(new View{file=name+"-NO-LEGAL-FEET",feet=preferred,lookAt=target});yield break;}
   var player=G.player;player.SetFeet(feet);player.verticalSpeed=0;Vector3 d=target-player.Eye.position;player.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);Physics.SyncTransforms();yield return null;
   string file="chapter-"+data.chapter.ToString("00")+"-"+name+".png";var camera=player.cameraView;var texture=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);Texture2D pixels=null;var previous=RenderTexture.active;
   try{texture.Create();var request=new RenderPipeline.StandardRequest{destination=texture};RenderPipeline.SubmitRenderRequest(camera,request);RenderTexture.active=texture;pixels=new Texture2D(1600,900,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,file),pixels.EncodeToPNG());data.views.Add(new View{file=file,feet=feet,lookAt=target});}
   finally{RenderTexture.active=previous;texture.Release();UnityEngine.Object.Destroy(texture);if(pixels)UnityEngine.Object.Destroy(pixels);}
  }
  // selectedLevels uses stable zero-based IDs (0 = chapter 1, 11 = chapter 12).
  // Omit it to retain the original twelve-chapter run.
  public static IEnumerator Run(Action<string,bool,string> add,string output,int[] selectedLevels=null)
  {
   Directory.CreateDirectory(output);if(!RunStorage.IsIsolated){add("mapv2_geometry_isolated",false,"Geometry fixture cannot run against formal saves");yield break;}
   int[] levels=selectedLevels??new[]{0,1,2,3,4,5,6,7,8,9,10,11};if(levels.Length==0)throw new ArgumentException("selectedLevels must contain at least one zero-based chapter ID.");
   var seenLevels=new HashSet<int>();int expectedBudget=0,actualBudget=0,deployedLevels=0;
   foreach(int level in levels){if(level<0||level>=12||!seenLevels.Add(level))throw new ArgumentException("selectedLevels must contain distinct IDs in 0..11.");expectedBudget+=MissionCatalog.Get(level).TotalBudget;}
   bool oldSuppress=G.qaSuppressAI;G.qaSuppressAI=true;
   foreach(int level in levels)
   {
    if(G.StoryModal)G.CloseStoryModal();if(G.expeditionActive&&G.expeditionReceipt==null)G.AbandonExpedition();G.OpenExpeditionBoard();string folder=Path.Combine(RunStorage.Root,"mapv2-geometry-"+level);Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"expedition-v6.json"),JsonUtility.ToJson(TacticalDemoProfile.Create(0,true)));G.expeditionStore=new ExpeditionStore(folder,true);
    bool began=G.BeginExpedition(level);add("mapv2_ch"+(level+1)+"_deploy",began,G.expeditionNotice);if(!began)continue;deployedLevels++;actualBudget+=G.mission.Members.Count;G.mission.Stop("geometry_fixture_cancel_deployment");foreach(var enemy in G.enemies)if(enemy){enemy.gameObject.SetActive(false);UnityEngine.Object.Destroy(enemy.gameObject);}G.enemies.Clear();yield return null;
    var initial=CaptureGraph(level,"initial authored state");File.WriteAllText(Path.Combine(output,"chapter-"+(level+1).ToString("00")+"-initial.json"),JsonUtility.ToJson(initial));
    var layout=G.expeditionMap.root.GetComponent<MissionLayout>();foreach(var o in MissionCatalog.Get(level).objectives)layout.Complete(o.id);layout.Complete("$calibrated");layout.Complete("$mini");layout.Complete("$main");layout.MarkNavigationDirty();yield return null;
    float deadline=Time.realtimeSinceStartup+90;while(layout.NavigationUpdating&&Time.realtimeSinceStartup<deadline)yield return null;add("mapv2_ch"+(level+1)+"_nav_updated",!layout.NavigationUpdating,"Fixture-opened permanent routes; no gameplay victory implied");
    var data=CaptureGraph(level,"all permanent passages opened by isolated fixture");Configurations(data,folder);
    foreach(var t in data.targets)add("mapv2_ch"+(level+1)+"_route_"+t.id,t.anchorReachable&&t.clearance&&((level==2||level==4||level==6||level==8)?t.walkReachable:true),"kind="+t.kind+";target="+t.requested+";stand="+t.standingPoint+";walk="+t.walkReachable+";anchor="+t.anchorReachable+";clear3.2="+t.clearance);
    for(int i=0;i<data.jumpPads.Count;i++){var pad=data.jumpPads[i];add("mapv2_ch"+(level+1)+"_actual_pad_"+i,pad.sourceGrounded&&pad.landingGrounded&&pad.pathClear&&Mathf.Abs(pad.horizontalMetres-20)<.05f,"launch="+pad.launch+";landing="+pad.landing+";horizontal="+pad.horizontalMetres+";source_ground="+pad.sourceGrounded+";landing_ground="+pad.landingGrounded+";same_production_32_capsule_sweep="+pad.pathClear+";reason="+pad.reason);}
    bool matrix=data.configurations.TrueForAll(c=>c.validSnapshot&&c.taskRoutes);add("mapv2_ch"+(level+1)+"_16_loadouts",matrix,"4 classes × 2 class skills × 2 general skills; real level-1 domain snapshots; no consumables; same actual routes; not combat clears");
    var focused=G.expeditionMap.root.GetComponent<FocusedArenaDefinition>();int expectedViews=4;
    if(focused&&focused.cameras!=null)
    {
     expectedViews=focused.cameras.Length;
     for(int i=0;i<expectedViews;i++){var view=focused.cameras[i];yield return ViewAt(data,output,(i+1).ToString("00")+"-focused",view.feet,view.lookAt);}
    }
    else
    {
     Vector3 spawn=G.expeditionMap.spawn;Vector3 mechanism=spawn+Vector3.forward*30;foreach(var n in G.expeditionMap.nodes)if(n.kind==ExpeditionNodeKind.Objective){mechanism=n.transform.position;break;}Vector3 high=spawn;foreach(var point in G.expeditionMap.walkingCheckpoints)if(point.y>high.y)high=point;Vector3 boss=layout.MiniBossPosts[0];
     yield return ViewAt(data,output,"01-entry",spawn,spawn+Vector3.forward*35+Vector3.up*3);yield return ViewAt(data,output,"02-mechanism",mechanism+Vector3.back*10,mechanism+Vector3.up);yield return ViewAt(data,output,"03-height",high,high+Vector3.forward*25+Vector3.down*4);yield return ViewAt(data,output,"04-miniboss",boss+Vector3.back*14,boss+Vector3.up*2);
    }
    File.WriteAllText(Path.Combine(output,"chapter-"+(level+1).ToString("00")+".json"),JsonUtility.ToJson(data));add("mapv2_ch"+(level+1)+"_views",data.views.Count==expectedViews&&data.views.TrueForAll(v=>v.file.EndsWith(".png")),"Actual runtime camera render at collision-checked feet; no HUD, no human-play claim");
   }
   add(selectedLevels==null?"mapv2_all_budget_counts":"mapv2_selected_budget_counts",deployedLevels==levels.Length&&actualBudget==expectedBudget,"zeroBasedLevels="+string.Join(",",levels)+";deployed="+deployedLevels+";plannedMembers="+actualBudget+";expectedSelectedSum="+expectedBudget+";no unselected chapter deployments");
   if(G.expeditionActive&&G.expeditionReceipt==null)G.AbandonExpedition();G.OpenExpeditionBoard();G.qaSuppressAI=oldSuppress;
  }
 }
}
