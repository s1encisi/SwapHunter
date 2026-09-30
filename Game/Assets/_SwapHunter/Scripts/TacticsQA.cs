using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace SwapHunter {
public sealed class TacticsQA:MonoBehaviour {
 [Serializable] public sealed class Check{public string name,detail;public bool passed;}
 [Serializable] public sealed class Report{public string kind="Native directional A-shield projectile, input, swap and persistence integration; knife/melee covered separately";public bool passed;public List<Check> checks=new List<Check>();}
 Report report=new Report();string output;bool done;float began;DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
 void Awake(){began=Time.realtimeSinceStartup;Application.logMessageReceived+=Error;}
 void Error(string m,string s,LogType t){if(t==LogType.Exception||t==LogType.Error||t==LogType.Assert)Add("runtime_error",false,m);}
 void Add(string n,bool ok,string d){report.checks.Add(new Check{name=n,passed=ok,detail=d});Debug.Log("TACTICS_QA "+n+" "+ok+" "+d);}
 void Update(){if(!done&&Time.realtimeSinceStartup-began>90){Add("watchdog",false,"90s");Finish();}}
 void Aim(Vector3 p){var d=p-P.Eye.position;P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);}
 IEnumerator Start(){
 var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-qaOutput");output=i>=0?a[i+1]:RunStorage.Root;Directory.CreateDirectory(output);
 while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
 G.StartRun(false);G.qaSuppressAI=true;yield return new WaitForSeconds(.3f);var e=G.enemies[0];P.SetFeet(new Vector3(0,.03f,8));P.SetLook(0,0);yield return new WaitForSeconds(.2f);
 Aim(e.AimPoint);Physics.SyncTransforms();
 Add("fixture_target_in_range_and_visible",Vector3.Distance(P.Eye.position,e.AimPoint)<G.config.swapRange&&P.FindTarget()==e&&P.ValidateSwap(e)=="可换位","distance="+Vector3.Distance(P.Eye.position,e.AimPoint)+";validation="+P.ValidateSwap(e));
 P.SetLook(0,0);
 var t=P.tactics;var keys=InputSystem.AddDevice<Keyboard>();G.qaKeyboard=keys;G.qaInputEnabled=true;
 InputSystem.QueueStateEvent(keys,new KeyboardState(Key.C));yield return null;yield return null;InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;
 Add("keyboard_shield_activation",t.ShieldActive&&t.ShieldCooldownLeft>9,"duration="+t.ShieldLeft);Vector3 shield=t.ShieldPosition;float hp=P.health;
 Bolt.Spawn(e,P.Eye.position+Vector3.forward*6,Vector3.back,30,12);yield return new WaitForSeconds(.3f);
 Add("front_actual_projectile_blocked",P.health==hp&&t.ShieldBlocks>0,"hp="+P.health+";blocks="+t.ShieldBlocks);
 Bolt.Spawn(e,P.Eye.position+Vector3.right*5,Vector3.left,30,12);yield return new WaitForSeconds(.3f);
 Add("side_actual_projectile_damages",P.health<hp,"hp="+P.health);hp=P.health;
 Bolt.Spawn(e,P.Eye.position+Vector3.back*4,Vector3.forward,30,12);yield return new WaitForSeconds(.3f);
 Add("rear_actual_projectile_damages",P.health<hp,"hp="+P.health);
 hp=P.health;P.Hurt(10,P.Eye.position+Vector3.forward*7);Add("front_direct_attack_blocked",P.health==hp,"hp="+P.health);
 yield return null;yield return new WaitForEndOfFrame();var shieldRenderer=G.effects.Find("A-shield directional barrier").GetComponent<Renderer>();var shieldTint=new MaterialPropertyBlock();shieldRenderer.GetPropertyBlock(shieldTint);
 Add("hit_feedback_keeps_battlefield_visible",shieldRenderer.sharedMaterial==Shapes.Mat(9)&&shieldRenderer.sharedMaterial.GetFloat("_Surface")==1&&shieldTint.GetColor("_BaseColor").a<=.3f,"alpha="+shieldTint.GetColor("_BaseColor").a);
 float enemyHp=e.health;Aim(e.AimPoint);
 bool clearShot=EnemyActor.CastWeaponRay(P.Eye.position,P.Eye.forward,30,P.transform,out var sight)&&sight.collider.GetComponentInParent<EnemyActor>()==e;
 Add("shield_does_not_occlude_weapon_ray",clearShot&&t.ShieldActive,"firstHit="+(sight.collider?sight.collider.name:"none"));
 P.Fire();yield return new WaitForSeconds(.16f);Add("player_shoots_through_own_shield",e.health<enemyHp,"enemyHp="+e.health);
 P.cooldown=0;Aim(e.AimPoint);Vector3 oldPlayer=P.transform.position,oldEnemy=e.transform.position;
 Add("shield_does_not_occlude_swap_target",P.FindTarget()==e&&P.ValidateSwap(e)=="可换位",P.ValidateSwap(e));
 bool requested=P.RequestSwap(e);yield return new WaitForSeconds(G.config.swapWindup+.12f);
 Add("swap_exchanges_both_endpoints",requested&&Vector3.Distance(P.transform.position,oldEnemy)<.12f&&Vector3.Distance(e.transform.position,oldPlayer)<.12f,"player="+P.transform.position+";enemy="+e.transform.position);
 Add("swap_keeps_world_directional_shield",P.transform.position.z>12&&Vector3.Distance(t.ShieldPosition,shield)<.001f&&t.ShieldActive,"shield="+t.ShieldPosition+";player="+P.transform.position);
 P.SetLook(180,0);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"a-shield-swap.png"));yield return null;
 Add("cannot_reactivate_during_cooldown",!t.ActivateShield(),"No omnidirectional refresh");
 yield return new WaitForSeconds(t.ShieldLeft+.15f);Add("shield_expires_before_cooldown",!t.ShieldActive&&t.ShieldCooldownLeft>5,"cooldown="+t.ShieldCooldownLeft);
 G.qaInputEnabled=false;G.qaKeyboard=null;InputSystem.RemoveDevice(keys);
 var old=new GameOptions{version=4,fov=73,renderScale=.95f,uiScale=.9f};var oldKeys=new ActionBinding[20];Array.Copy(old.actions,oldKeys,20);old.actions=oldKeys;old.actions[0].primary="K:C";
 string legacy=JsonUtility.ToJson(old);File.WriteAllText(SettingsStore.LegacyFilePath,legacy);if(File.Exists(SettingsStore.FilePath))File.Delete(SettingsStore.FilePath);
 var migrated=SettingsStore.Load();Add("v4_values_preserved",migrated.fov==73&&migrated.renderScale==.95f&&migrated.uiScale==.9f,"v4 display preferences not reset");
 Add("new_action_does_not_steal_old_key",migrated.actions[0].primary=="K:C"&&migrated.actions[20].primary=="","existing custom C remains movement");
 SettingsStore.Save(migrated);Add("v5_file_preserves_v4_original",File.ReadAllText(SettingsStore.LegacyFilePath)==legacy&&File.Exists(SettingsStore.FilePath),SettingsStore.FilePath);
 Finish();
 }
 void Finish(){if(done)return;done=true;report.passed=report.checks.Count>0&&report.checks.TrueForAll(x=>x.passed);File.WriteAllText(Path.Combine(output,"tactics-report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);}
 void OnDestroy(){Application.logMessageReceived-=Error;}
}}
