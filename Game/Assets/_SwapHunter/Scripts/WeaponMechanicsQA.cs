using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace SwapHunter
{
    public static class WeaponMechanicsQA
    {
        public static IEnumerator Run(DemoGame g,Action<string,bool,string> check)
        {
            var p=g.player;var oldOptions=g.options;var oldKeys=g.qaKeyboard;var oldMouse=g.qaMouse;
            bool oldInput=g.qaInputEnabled,oldSuppress=g.qaSuppressAI,oldExpedition=g.expeditionActive;
            var oldState=g.state;var oldFeet=p.transform.position;float oldYaw=p.yaw,oldPitch=p.pitch,oldHealth=p.health;
            int[] oldLoad=(int[])p.EquippedWeapons.Clone(),oldAmmo=(int[])p.ammo.Clone(),oldReserve=(int[])p.reserve.Clone();
            var fixture=new GameObject("Weapon QA / isolated off-map fixture");var actors=new List<EnemyActor>();Keyboard keys=null;Mouse mouse=null;
            Vector3 origin=new Vector3(0,.03f,-135);
            try
            {
                g.expeditionActive=false;g.qaSuppressAI=true;g.qaInputEnabled=true;g.options=new GameOptions();g.SyncLegacyBindings();g.ApplySettings(false);
                keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();g.qaKeyboard=keys;g.qaMouse=mouse;
                Shapes.Box("QA floor",fixture.transform,new Vector3(0,-.5f,-120),new Vector3(50,1,50),0,true,Layers.World);
                var nav=fixture.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;nav.layerMask=Layers.WorldMask;nav.useGeometry=UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;nav.BuildNavMesh();
                g.SetState(RunState.Playing);p.ResetAt(origin);yield return new WaitForSeconds(.25f);
                var target=EnemyActor.Spawn(EnemyKind.Training,origin+Vector3.forward*10-Vector3.up*.03f,0);actors.Add(target);
                target.health=target.maximumHealth=100000;int smgRounds=0,heavyRounds=0;
                for(int id=0;id<WeaponCatalog.Count;id++)
                {
                    p.ResetAt(origin);p.SetLoadout(id,id==0?1:0);Aim(p,target.AimPoint);yield return new WaitForSeconds(.30f);
                    int ammo=p.ammo[id],shots=p.shots;float hp=target.health;
                    InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return new WaitForSeconds(id==10?.78f:.56f);
                    InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.16f);
                    int rounds=p.shots-shots,used=ammo-p.ammo[id];
                    bool expected=WeaponCatalog.Get(id).Automatic?rounds>=2:id==2?rounds==3:rounds==1;
                    check("weapon_actual_trigger_"+id,expected&&used==rounds,"real hold rounds="+rounds+", ammo="+used);
                    if(id==3)heavyRounds=rounds;if(id==4)smgRounds=rounds;
                    if(id==9){yield return new WaitForSeconds(1.1f);check("launcher_ballistic_damage",target.health<hp,"blast damage="+(hp-target.health));}
                    else check("weapon_actual_damage_"+id,target.health<hp,"damage="+(hp-target.health));
                    if(id==11)check("marker_actual_hit",target.Marked,"Actual shot applies timed mark");
                }
                check("smg_vs_heavy_cadence",smgRounds>heavyRounds,"SMG="+smgRounds+", heavy="+heavyRounds);
                p.ResetAt(origin);p.SetLoadout(10,0);Aim(p,target.AimPoint);yield return new WaitForSeconds(.3f);int chargeAmmo=p.ammo[10];
                InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return new WaitForSeconds(.2f);
                InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.6f);
                check("charge_release_cancels",p.ammo[10]==chargeAmmo&&p.ChargeProgress==0,"Early release spends no round");
                p.SetLoadout(5,0);yield return new WaitForSeconds(.3f);
                for(int n=0;n<2;n++){InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return new WaitForSeconds(.06f);InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.34f);}
                check("double_barrel_two_clicks",p.ammo[5]==0,"Two triggers exhaust two chambers");
                check("double_barrel_real_reload",p.Reload()&&p.reloadLeft>2,"Empty double barrel reload");
                InputSystem.QueueStateEvent(keys,new KeyboardState(Key.Digit2));yield return null;yield return null;
                InputSystem.QueueStateEvent(keys,new KeyboardState());yield return new WaitForSeconds(.08f);
                check("loadout_key_switch_and_reload_cancel",p.weapon==0&&p.reloadLeft==0,"Digit2 selects secondary, cancels reload");
                p.SwitchWeapon(7);check("unequipped_switch_rejected",p.weapon==0,"Cannot select a third weapon");

                var regionTarget=EnemyActor.Spawn(EnemyKind.Training,origin+new Vector3(5,-.03f,10),0);actors.Add(regionTarget);regionTarget.health=regionTarget.maximumHealth=1000;
                foreach(var pair in new[]{new {region=EnemyHitRegion.Head,height=1.59f,damage=15f},new {region=EnemyHitRegion.Torso,height=1.05f,damage=10f},new {region=EnemyHitRegion.Limb,height=.37f,damage=8f}})
                {
                    Vector3 aim=regionTarget.transform.position+new Vector3(pair.region==EnemyHitRegion.Limb?.15f:0,pair.height,0),from=aim-Vector3.forward*4;
                    bool hit=EnemyActor.CastWeaponRay(from,Vector3.forward,6,p.transform,out var rayHit);
                    Vector3 point=hit?rayHit.point:aim;Collider collider=hit?regionTarget.ResolveHitCollider(new Ray(from,Vector3.forward),rayHit.collider,ref point):null;
                    float before=regionTarget.health;if(hit)regionTarget.Hit(10,point,collider,from);
                    check("body_region_"+pair.region,hit&&regionTarget.RegionOf(collider)==pair.region&&Mathf.Abs(before-regionTarget.health-pair.damage)<.01f,"damage="+(before-regionTarget.health)+", "+regionTarget.LastHitRegionLabel);
                }
                var shield=EnemyActor.Spawn(EnemyKind.Shield,origin+new Vector3(-5,-.03f,7),0);actors.Add(shield);
                p.ResetAt(origin+Vector3.left*5);p.SetLoadout(0,11);Aim(p,shield.AimPoint);yield return new WaitForSeconds(.3f);float shieldHP=shield.health;
                InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return new WaitForSeconds(.06f);
                InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.15f);
                check("shield_reference_blocks_weapon",shield.health==shieldHP&&p.blockFlash>0&&p.HitRegionLabel=="盾牌","Actual frontal shield block");
                p.SetLoadout(11,0);p.SetFeet(origin+Vector3.right*5);Aim(p,regionTarget.AimPoint);
                var wall=Shapes.Box("QA marker occluder",fixture.transform,origin+new Vector3(5,1,5),new Vector3(3,3,.3f),0,true,Layers.World);
                yield return new WaitForSeconds(.3f);float regionHP=regionTarget.health;
                InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return new WaitForSeconds(.06f);
                InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.15f);
                check("marker_cannot_cross_wall",!regionTarget.Marked&&regionTarget.health==regionHP,"Wall blocks damage and mark");
                wall.SetActive(false);UnityEngine.Object.Destroy(wall);

                p.ResetAt(origin);p.SetLoadout(2,0);yield return new WaitForSeconds(.3f);
                InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return null;yield return null;
                g.SetState(RunState.Paused);int pausedAmmo=p.ammo[2];yield return new WaitForSecondsRealtime(.35f);
                check("pause_cancels_queued_burst",p.ammo[2]==pausedAmmo&&p.reloadLeft==0,"No queued round while paused");
                g.SetState(RunState.Playing);yield return new WaitForSeconds(.35f);
                check("resume_requires_trigger_release",p.ammo[2]==pausedAmmo,"Held trigger does not restart burst");
                InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.08f);

                p.ResetAt(origin);yield return new WaitForSeconds(.15f);
                InputSystem.QueueStateEvent(keys,new KeyboardState(Key.LeftCtrl));yield return new WaitForSeconds(.15f);
                check("crouch_input_changes_capsule",p.IsCrouching&&p.controller.height<1.2f,"Real Ctrl changes stance");
                var ceiling=Shapes.Box("QA low ceiling",fixture.transform,origin+new Vector3(0,1.43f,0),new Vector3(2,.2f,2),0,true,Layers.World);
                Physics.SyncTransforms();
                InputSystem.QueueStateEvent(keys,new KeyboardState());yield return new WaitForSeconds(.15f);
                check("ceiling_prevents_standing",p.IsCrouching&&p.controller.height<1.2f,"crouch="+p.IsCrouching+"; capsule="+p.controller.height+"; feet="+p.transform.position.ToString("F3")+"; blocker="+p.LastStandBlocker+"; roof="+ceiling.GetComponent<Collider>().bounds);
                ceiling.SetActive(false);UnityEngine.Object.Destroy(ceiling);yield return new WaitForSeconds(.15f);
                check("stand_after_clearance",!p.IsCrouching&&Mathf.Abs(p.controller.height-1.8f)<.01f,"Stand only after clearance");
                p.ResetAt(origin);
                Shapes.Box("QA vault ledge",fixture.transform,origin+new Vector3(0,.42f,1.2f),new Vector3(4,.84f,1.6f),0,true,Layers.World);
                var roof=Shapes.Box("QA vault blocked landing",fixture.transform,origin+new Vector3(0,1.9f,1.1f),new Vector3(3,.2f,2),0,true,Layers.World);
                yield return new WaitForSeconds(.2f);check("vault_rejects_blocked_landing",!p.TryVault()&&!p.IsVaulting,"Blocked capsule prevents vault");
                roof.SetActive(false);UnityEngine.Object.Destroy(roof);yield return null;
                InputSystem.QueueStateEvent(keys,new KeyboardState(Key.Space));yield return null;yield return null;
                InputSystem.QueueStateEvent(keys,new KeyboardState());bool began=p.IsVaulting;yield return new WaitForSeconds(.7f);
                check("vault_real_jump_input",began&&!p.IsVaulting&&p.transform.position.y>origin.y+.65f&&p.transform.position.z>origin.z+.65f,"Result "+p.transform.position);
                check("vault_no_stuck_action",p.ActionLabel!="攀爬"&&p.controller.enabled,"Controller/action restored");
                foreach(var actor in actors)if(actor)actor.suppressAI=true;
                p.ResetAt(origin+Vector3.left*6);
                var walker=EnemyActor.Spawn(EnemyKind.Assault,origin+new Vector3(8,-.03f,17),0);actors.Add(walker);
                Vector3 gaitStart=walker.transform.position;float phaseStart=walker.GaitPhase;
                g.qaSuppressAI=false;yield return new WaitForSeconds(.9f);
                check("gait_tracks_actual_navigation",Vector3.Distance(gaitStart,walker.transform.position)>.7f&&walker.GaitWeight>.1f&&walker.GaitPhase>phaseStart+.1f,"distance="+Vector3.Distance(gaitStart,walker.transform.position)+", gait="+walker.GaitWeight);
                walker.suppressAI=true;yield return new WaitForSeconds(.55f);
                check("gait_settles_when_stopped",walker.GaitWeight<.06f,"Stopped gait weight="+walker.GaitWeight);
                g.qaSuppressAI=true;
            }
            finally
            {
                if(keys!=null)InputSystem.RemoveDevice(keys);if(mouse!=null)InputSystem.RemoveDevice(mouse);
                foreach(var actor in actors)if(actor){g.enemies.Remove(actor);actor.gameObject.SetActive(false);UnityEngine.Object.Destroy(actor.gameObject);}
                fixture.SetActive(false);UnityEngine.Object.Destroy(fixture);
                g.options=oldOptions;g.SyncLegacyBindings();g.ApplySettings(false);g.qaKeyboard=oldKeys;g.qaMouse=oldMouse;g.qaInputEnabled=oldInput;g.qaSuppressAI=oldSuppress;g.expeditionActive=oldExpedition;
                p.ResetAt(oldFeet);p.SetLoadout(oldLoad[0],oldLoad[1]);Array.Copy(oldAmmo,p.ammo,oldAmmo.Length);Array.Copy(oldReserve,p.reserve,oldReserve.Length);p.health=oldHealth;p.SetLook(oldYaw,oldPitch);g.SetState(oldState);
            }
        }
        static void Aim(PlayerMotor p,Vector3 point){Vector3 d=(point-p.Eye.position).normalized;p.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Asin(d.y)*Mathf.Rad2Deg);}
    }
}
