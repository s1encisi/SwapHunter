using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.AI.Navigation;
namespace SwapHunter
{
 public sealed class DeploymentQA:MonoBehaviour
 {
  [Serializable] public class Check{public string name,detail;public bool passed;}
  [Serializable] public class Report{public string kind="Native warned deployment, blocked-location retry, proximity safety and enemy-budget retention";public bool passed;public List<Check> checks=new List<Check>();}
  Report report=new Report();bool done;float started;string output;Transform fixture;NavMeshSurface surface;
  DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
  void Awake(){started=Time.realtimeSinceStartup;Application.logMessageReceived+=Error;}
  void Error(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)Add("runtime_error",false,m);}
  void Add(string n,bool ok,string d){report.checks.Add(new Check{name=n,passed=ok,detail=d});Debug.Log("DEPLOYMENT_QA "+n+" "+ok+" "+d);}
  void Update(){if(!done&&Time.realtimeSinceStartup-started>90){Add("watchdog",false,"90s");Finish();}}
  int Actual(){int count=0;foreach(var e in G.enemies)if(e&&e.Alive)count++;return count;}
  void Clear(){G.ClearDeployments();foreach(var e in G.enemies.ToArray())if(e){e.gameObject.SetActive(false);Destroy(e.gameObject);}G.enemies.Clear();P.SetFeet(new Vector3(100,.03f,0));P.SetLook(0,0);}
  IEnumerator WaitForSpawn(){float end=Time.time+8;while(G.PendingDeployments>0&&Time.time<end)yield return null;}
  IEnumerator Start()
  {
   var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-qaOutput");output=i>=0?a[i+1]:RunStorage.Root;Directory.CreateDirectory(output);
   while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
   G.StartRun(false);G.qaSuppressAI=true;yield return new WaitForSeconds(.2f);Clear();
   fixture=new GameObject("Deployment test arena").transform;Shapes.Box("Floor",fixture,new Vector3(100,-.5f,0),new Vector3(40,1,40),1,true,Layers.World);
   surface=fixture.gameObject.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.layerMask=Layers.WorldMask;surface.useGeometry=UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();
   G.QueueDeployment(EnemyKind.Assault,new Vector3(100,0,-10),0);int warnings=G.DeploymentWarnings;
   Add("queued_budget_counts_as_remaining_enemy",Actual()==0&&G.PendingDeployments==1&&G.AliveEnemies==1,"actual="+Actual()+";pending="+G.PendingDeployments);
   yield return new WaitForSeconds(1.3f);Add("rear_deployment_never_instant",Actual()==0&&G.PendingDeployments==1&&G.DeploymentWarnings>0,"warnings="+G.DeploymentWarnings);
   yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"deployment-direction-warning.png"));yield return null;
   yield return WaitForSpawn();Add("warned_rear_enemy_eventually_deploys_safely",Actual()==1&&G.PendingDeployments==0&&Vector3.Distance(G.enemies[G.enemies.Count-1].transform.position,P.transform.position)>=7,"actual="+Actual());
   Clear();var wall=Shapes.Box("Occupied entrance",fixture,new Vector3(100,1.5f,10),new Vector3(4,3,4),1,true,Layers.World);Physics.SyncTransforms();G.QueueDeployment(EnemyKind.Assault,new Vector3(100,0,10),0);yield return WaitForSpawn();var e=G.enemies.Find(x=>x&&x.Alive);
   Add("blocked_requested_entrance_replaced_before_spawn",e&&EnemyMotion.ClearVolume(e.transform.position,e.Radius,e.Height,e.transform),e?e.transform.position.ToString():"no spawn");wall.SetActive(false);Destroy(wall);
   Clear();G.QueueDeployment(EnemyKind.Assault,new Vector3(100,0,10),0);warnings=G.DeploymentWarnings;Vector3 marked=G.NextDeploymentPoint;yield return new WaitForSeconds(.5f);P.SetFeet(marked);yield return new WaitForSeconds(2);
   Add("walking_into_marker_rewarns_without_telefrag",Actual()==0&&G.PendingDeployments==1&&G.DeploymentWarnings>warnings,"warnings="+G.DeploymentWarnings+";pending="+G.PendingDeployments);yield return WaitForSpawn();e=G.enemies.Find(x=>x&&x.Alive);Add("relocated_deployment_retains_enemy_budget",e&&G.PendingDeployments==0&&Vector3.Distance(e.transform.position,P.transform.position)>=4.5f,"actual="+Actual());
   Clear();int completed=G.CompletedDeployments;G.totalSwaps++;yield return new WaitForSeconds(.2f);Add("swap_does_not_create_punishment_spawn",G.PendingDeployments==0&&G.CompletedDeployments==completed,"completed="+completed);
   G.QueueDeployment(EnemyKind.Assault,new Vector3(100,50,10),0);yield return new WaitForSeconds(1.6f);Add("unavailable_nav_preserves_pending_budget",G.PendingDeployments==1&&G.AliveEnemies==1&&Actual()==0,"pending="+G.PendingDeployments);
   Shapes.Box("Newly available deck",fixture,new Vector3(100,49.5f,10),new Vector3(10,1,10),1,true,Layers.World);surface.BuildNavMesh();yield return WaitForSpawn();Add("retry_deploys_once_when_entrance_becomes_valid",Actual()==1&&G.PendingDeployments==0,"actual="+Actual());
   Clear();Add("transition_clears_stale_deployments",G.PendingDeployments==0&&Actual()==0,"clean stage transition");Finish();
  }
  void Finish(){if(done)return;done=true;report.passed=report.checks.Count>0&&report.checks.TrueForAll(x=>x.passed);File.WriteAllText(Path.Combine(output,"deployment-report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);}
  void OnDestroy(){Application.logMessageReceived-=Error;}
 }
}
