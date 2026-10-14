using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SwapHunter
{
    // SSU-01 A1-A4: real runtime actions on an isolated platform, with explicit controlled timers.
    // Input events exercise Update arbitration; direct calls exercise the same public action guards.
    public static class SynergyActionChecks
    {
        public static IEnumerator Run(Action<string,bool,string> add) => new Suite(add).Run();
        sealed class Suite
        {
            readonly Action<string,bool,string> add;
            readonly List<GameObject> owned = new List<GameObject>();
            readonly Vector3 origin = new Vector3(180,8.03f,180);
            Keyboard keys; Mouse mouse;
            DemoGame G => DemoGame.I;
            PlayerMotor P => G.player;
            PlayerClassSkill S => P.classSkill;
            ExpeditionStore Store => G.expeditionStore;
            public Suite(Action<string,bool,string> add) { this.add=add; }
            void C(string id,bool result,string detail="") => add(id,result,detail);
            static bool Near(float a,float b) => Mathf.Abs(a-b)<.001f;
            void Aim(Vector3 point)
            {
                var d=point-P.Eye.position;
                P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);
            }
            void ReleaseInput()
            {
                InputSystem.QueueStateEvent(keys,new KeyboardState());
                InputSystem.QueueStateEvent(mouse,new MouseState());
            }
            void Clear()
            {
                foreach(var go in owned)if(go){go.SetActive(false);UnityEngine.Object.Destroy(go);}
                owned.Clear();
            }
            void Place()
            {
                P.CancelActions();P.SetFeet(origin);P.verticalSpeed=0;P.cooldown=0;
                P.ammo[P.weapon]=P.Capacity(P.weapon);P.reserve[P.weapon]=WeaponCatalog.Get(P.weapon).reserve;
                P.SetLook(0,0);Physics.SyncTransforms();
            }
            PhaseAnchor Anchor()
            {
                var go=new GameObject("SSU action fixture anchor");owned.Add(go);
                go.layer=Layers.Actor;go.transform.position=origin+Vector3.right*4;
                var capsule=go.AddComponent<CapsuleCollider>();capsule.center=Vector3.up*.675f;capsule.height=1.35f;capsule.radius=.32f;
                var anchor=go.AddComponent<PhaseAnchor>();Physics.SyncTransforms();return anchor;
            }
            IEnumerator Prepare(int skill,int gun=0,int general=0)
            {
                ReleaseInput();Clear();
                if(G.expeditionActive&&G.expeditionReceipt==null&&!G.AbandonExpedition())throw new InvalidOperationException("A fixture abandon failed");
                G.OpenExpeditionBoard();yield return null;
                Store.SelectClass(skill/2);Store.EquipClassSkill(skill);Store.EquipGeneralSkill(general);
                Store.Equip(0,gun);Store.Equip(1,gun==0?TacticalPrototypeRules.StarterWeapon(skill/2):0);
                Store.SetAttachment(gun,0);
                if(!G.BeginExpedition(0))throw new InvalidOperationException("A fixture deploy failed: "+G.expeditionNotice);
                var floor=Shapes.Box("SSU isolated action platform",G.effects,new Vector3(180,7.75f,180),new Vector3(40,.5f,40),1,true,Layers.World);
                owned.Add(floor);Place();yield return new WaitForSeconds(.04f);
                if(P.weapon!=gun)P.SwitchWeapon(gun);
                yield return new WaitForSeconds(.22f);
            }
            public IEnumerator Run()
            {
                if(!RunStorage.IsValidation||!G.qaMode){C("A_isolated_precondition",false);yield break;}
                bool suppress=G.qaSuppressAI, enabled=G.qaInputEnabled;
                var previousKeys=G.qaKeyboard;var previousMouse=G.qaMouse;var bindings=G.options.actions;
                if(G.expeditionActive&&G.expeditionReceipt==null)G.AbandonExpedition();
                var folder=Path.Combine(RunStorage.Root,"SynergyActions");Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder,"expedition-v6.json"),JsonUtility.ToJson(TacticalDemoProfile.Create(0,true)));
                G.expeditionStore=new ExpeditionStore(folder,true);
                keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
                G.qaKeyboard=keys;G.qaMouse=mouse;G.qaInputEnabled=true;G.qaSuppressAI=true;G.options.actions=GameOptions.DefaultBindings();
                try
                {
                    yield return SameFrame();
                    yield return Burst();
                    yield return Reload();
                    yield return CompatibleAbilities();
                }
                finally
                {
                    if(G.state==RunState.Paused)G.SetState(RunState.Playing);
                    P.CancelActions();Clear();
                    if(G.expeditionActive&&G.expeditionReceipt==null)G.AbandonExpedition();
                    G.OpenExpeditionBoard();
                    G.qaKeyboard=previousKeys;G.qaMouse=previousMouse;G.qaInputEnabled=enabled;G.qaSuppressAI=suppress;G.options.actions=bindings;
                    InputSystem.RemoveDevice(keys);InputSystem.RemoveDevice(mouse);
                }
            }
            IEnumerator SameFrame()
            {
                yield return Prepare(1,0,1);var anchor=Anchor();Aim(anchor.AimPoint);
                int shots=P.shots,ammo=P.ammo[0],swaps=P.swapCount;int selected=P.weapon;
                // Q, fire, melee, throw, reload, switch and E all arrive in one native input update.
                InputSystem.QueueStateEvent(keys,new KeyboardState(Key.Q,Key.V,Key.G,Key.R,Key.Digit2,Key.E));
                InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));
                yield return null;InputSystem.QueueStateEvent(keys,new KeyboardState());
                C("A1_accepted_same_frame_blocks_new_actions",P.SwapActionsLocked&&P.shots==shots&&P.ammo[0]==ammo&&P.weapon==selected&&P.reloadLeft==0
                    &&P.combatTools.ThrowCooldownLeft==0&&P.combatTools.MeleeCooldownLeft==0&&P.combatTools.Knife==null,
                    "locked="+P.SwapActionsLocked+";shots="+(P.shots-shots)+";weapon="+P.weapon+";validation="+P.swapValidation);
                C("A4_active_interaction_rejected",P.SwapActionsLocked&&!G.Interact(),"Public active E entry while preparing");
                yield return new WaitForSeconds(.17f);
                C("A1_valid_q_completed_no_held_fire_replay",P.swapCount==swaps+1&&P.shots==shots,"swapDelta="+(P.swapCount-swaps)+";heldFireShotDelta="+(P.shots-shots));
                ReleaseInput();yield return null;
                Place();anchor.transform.position=origin+Vector3.right*4;Physics.SyncTransforms();Aim(anchor.AimPoint);yield return null;
                P.cooldown=1;shots=P.shots;ammo=P.ammo[0];
                InputSystem.QueueStateEvent(keys,new KeyboardState(Key.Q));InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));
                yield return null;ReleaseInput();
                C("A1_invalid_q_still_fires",!P.preparing&&P.shots==shots+1&&P.ammo[0]==ammo-1,"shots="+(P.shots-shots)+";reason="+P.swapValidation);
                yield return null;
            }
            IEnumerator Burst()
            {
                yield return Prepare(4,2);var anchor=Anchor();
                foreach(float delay in new[]{0f,.03f,.07f,.1f})foreach(bool cancel in new[]{false,true})
                {
                    Place();anchor.gameObject.SetActive(true);anchor.transform.position=origin+Vector3.right*4;Physics.SyncTransforms();
                    yield return new WaitForSeconds(.32f);
                    int initialShots=P.shots,initialAmmo=P.ammo[2],swaps=P.swapCount;float start=Time.time;
                    bool fired=P.Fire();if(delay>0)yield return new WaitForSeconds(delay);
                    int beforeQ=P.shots;float last=P.LastShotTime;bool accepted=P.RequestSwap(anchor);
                    int sequence=P.ActionSequence;bool gate=P.PendingBurstRounds==0&&P.NextShotTime>=last+.28f-.001f;
                    if(cancel)anchor.gameObject.SetActive(false);
                    yield return new WaitForSeconds(.22f);
                    bool result=cancel?P.swapCount==swaps&&P.cooldown==0:P.swapCount==swaps+1;
                    C("A2_burst_"+delay.ToString("F2")+"_"+(cancel?"cancel":"success"),fired&&accepted&&gate&&result&&P.shots==beforeQ
                        &&initialAmmo-P.ammo[2]==beforeQ-initialShots&&P.ActionSequence==sequence,
                        "requestedDelay="+delay+";elapsed="+(Time.time-start)+";actualRounds="+(P.shots-initialShots)+";ammoDelta="+(initialAmmo-P.ammo[2])+";nextShot="+P.NextShotTime+";last="+last);
                }
            }
            IEnumerator Reload()
            {
                yield return Prepare(3,8);var anchor=Anchor();
                foreach(float remaining in new[]{.04f,.12f})foreach(bool cancel in new[]{false,true})
                {
                    Place();S.ResetCombat();anchor.gameObject.SetActive(true);anchor.transform.position=origin+Vector3.right*4;Physics.SyncTransforms();yield return null;
                    P.ammo[8]=0;int stock=P.reserve[8],swaps=P.swapCount;
                    bool begun=P.Reload(),quick=S.ActivateEquipped();
                    // Controlled remaining-time fixture; actual completion and transfer stay production-owned.
                    P.reloadLeft=remaining;float cd=S.CooldownLeft;bool accepted=P.RequestSwap(anchor);
                    bool quickRejected=!S.ActivateEquipped()&&Near(cd,S.CooldownLeft);
                    if(cancel)anchor.gameObject.SetActive(false);
                    yield return null;
                    bool held=P.preparing&&P.ReloadSuspendedForSwap&&Near(P.reloadLeft,remaining)&&P.ammo[8]==0&&P.reserve[8]==stock;
                    yield return new WaitForSeconds(.3f);
                    bool resource=cancel?P.swapCount==swaps&&P.cooldown==0&&P.ammo[8]==P.Capacity(8)&&P.reserve[8]==stock-P.Capacity(8)
                        :P.swapCount==swaps+1&&P.reloadLeft==0&&P.ammo[8]==0&&P.reserve[8]==stock;
                    C("A3_reload_"+remaining.ToString("F2")+"_"+(cancel?"resume":"cancel_on_success"),begun&&quick&&accepted&&quickRejected&&held&&resource&&S.CooldownLeft>cd-1,
                        "controlledRemaining="+remaining+";held="+held+";ammo="+P.ammo[8]+";reserve="+P.reserve[8]+";quickCD="+S.CooldownLeft);
                }
                Place();anchor.gameObject.SetActive(true);anchor.transform.position=origin+Vector3.right*4;Physics.SyncTransforms();yield return null;
                P.ammo[8]=0;P.Reload();P.reloadLeft=.12f;bool requested=P.RequestSwap(anchor);float left=P.reloadLeft;int before=P.swapCount;
                G.SetState(RunState.Paused);yield return new WaitForSecondsRealtime(.15f);
                C("A3_pause_preserves_q_and_reload",requested&&P.preparing&&P.ReloadSuspendedForSwap&&Near(P.reloadLeft,left)&&P.ammo[8]==0&&P.swapCount==before);
                G.SetState(RunState.Playing);yield return new WaitForSeconds(.2f);
                C("A3_resume_finishes_original_q",P.swapCount==before+1&&P.reloadLeft==0&&P.ammo[8]==0);
                Place();anchor.transform.position=origin+Vector3.right*4;Physics.SyncTransforms();yield return null;
                P.ammo[8]=0;P.Reload();P.reloadLeft=.0001f;int reserve=P.reserve[8];Aim(anchor.AimPoint);
                InputSystem.QueueStateEvent(keys,new KeyboardState(Key.Q));yield return null;ReleaseInput();
                C("A3_due_transfer_precedes_new_q",P.preparing&&!P.ReloadSuspendedForSwap&&P.ammo[8]==P.Capacity(8)&&P.reserve[8]==reserve-P.Capacity(8));
                yield return new WaitForSeconds(.16f);
            }
            IEnumerator CompatibleAbilities()
            {
                foreach(int skill in new[]{0,2,6,7})
                {
                    yield return Prepare(skill);var anchor=Anchor();bool requested=P.RequestSwap(anchor);
                    bool blocked=!S.ActivateEquipped()&&S.CooldownLeft==0&&S.FortifyLeft==0&&!S.RemoteCharge&&!S.DenialField;
                    bool shieldBlocked=!P.tactics.ActivateShield()&&P.tactics.ShieldCooldownLeft==0&&!P.tactics.ShieldActive;
                    C("A4_world_creation_blocked_"+skill,requested&&blocked&&shieldBlocked,"failure="+S.LastFailure);
                    yield return new WaitForSeconds(.16f);
                }
                foreach(int skill in new[]{1,4,5})
                {
                    yield return Prepare(skill);var anchor=Anchor();bool requested=P.RequestSwap(anchor),cast=S.ActivateEquipped();float left=S.ActiveSeconds;int swaps=P.swapCount;
                    yield return new WaitForSeconds(.16f);
                    C("A4_self_preparation_continues_"+skill,requested&&cast&&P.swapCount==swaps+1&&S.ActiveSeconds>0&&S.ActiveSeconds<left,
                        "before="+left+";after="+S.ActiveSeconds);
                }
                yield return Prepare(2);bool fortified=S.ActivateEquipped(),shielded=P.tactics.ActivateShield();
                Vector3 fortify=S.FortifyCenter,shield=P.tactics.ShieldPosition;float shieldLife=P.tactics.ShieldLeft;var fixedAnchor=Anchor();bool q=P.RequestSwap(fixedAnchor);
                yield return new WaitForSeconds(.16f);
                C("A4_existing_world_effects_stay_fixed",fortified&&shielded&&q&&Vector3.Distance(S.FortifyCenter,fortify)<.001f&&Vector3.Distance(P.tactics.ShieldPosition,shield)<.001f&&P.tactics.ShieldLeft<shieldLife);

                yield return Prepare(1,0,1);bool thrown=P.combatTools.Throw();yield return new WaitForSeconds(.1f);
                var knife=P.combatTools.Knife;var knifeAnchor=Anchor();bool knifeQ=P.RequestSwap(knifeAnchor);Vector3 launch=knife?knife.LaunchPoint:Vector3.zero;
                bool recalled=P.combatTools.Recall();
                C("A4_existing_knife_recall_allowed",thrown&&knifeQ&&recalled&&knife&&knife.Returning&&Vector3.Distance(knife.LaunchPoint,launch)<.001f);
                yield return new WaitForSeconds(.18f);

                yield return Prepare(6,9);P.SetLook(0,45);bool placed=S.ActivateEquipped();yield return new WaitForSeconds(.65f);
                var charge=S.RemoteCharge;bool armed=charge&&charge.Armed;Vector3 center=charge?charge.transform.position:origin;
                var target=Shapes.Box("SSU detonation witness",G.effects,center+Vector3.right*.6f,new Vector3(.3f,.3f,.3f),1,true,Layers.Actor);owned.Add(target);
                var objective=target.AddComponent<DamageableObjective>();objective.Configure("ssu-detonation",200,null);
                P.SetFeet(origin+Vector3.left*6);Physics.SyncTransforms();var remoteAnchor=Anchor();bool remoteQ=P.RequestSwap(remoteAnchor),detonated=S.ActivateEquipped();
                C("A4_existing_remote_detonates_at_device",placed&&armed&&remoteQ&&detonated&&S.RemoteCharge==null&&S.CooldownLeft>0&&objective.Health<200,
                    "device="+center+";actualObjectiveHealth="+objective.Health+";cooldown="+S.CooldownLeft);
                yield return new WaitForSeconds(.16f);

                yield return Prepare(6,10);var coilAnchor=Anchor();int ammo=P.ammo[10];
                InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return new WaitForSeconds(.1f);
                bool charging=P.ChargeProgress>0,denied=!P.RequestSwap(coilAnchor);ReleaseInput();yield return null;
                bool canceled=P.ChargeProgress==0&&P.ammo[10]==ammo,afterRelease=P.RequestSwap(coilAnchor);
                C("A4_coil_blocks_q_until_real_release",charging&&denied&&canceled&&afterRelease,"charging="+charging+";unchangedAmmo="+(P.ammo[10]==ammo));
                yield return new WaitForSeconds(.16f);

                P.cooldown=0;P.ammo[P.weapon]=0;P.Reload();P.reloadLeft=.12f;coilAnchor.transform.position=P.transform.position+Vector3.right*4;Physics.SyncTransforms();
                bool cleanupRequest=P.RequestSwap(coilAnchor);P.CancelActions();
                yield return new WaitForSeconds(.15f);
                C("A4_cleanup_drops_suspended_actions",cleanupRequest&&!P.preparing&&!P.SwapActionsLocked&&P.reloadLeft==0&&P.PendingBurstRounds==0,"No deferred shot/reload is restored after CancelActions");
            }
        }
    }
}
