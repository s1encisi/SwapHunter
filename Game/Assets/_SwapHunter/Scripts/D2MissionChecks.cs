using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SwapHunter
{
    // Runs after the host prepares a real chapter-5 deployment with suppressed combat AI.
    public static class D2MissionChecks
    {
        public static IEnumerator Run(Action<string,bool,string> add)=>new Suite(add).Run();
        sealed class Suite
        {
            readonly Action<string,bool,string> add;
            DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
            Keyboard keys;Mouse mouse;
            public Suite(Action<string,bool,string> add){this.add=add;}
            void C(string id,bool ok,string detail="")=>add(id,ok,detail);
            void Aim(Vector3 point)
            {Vector3 d=point-P.Eye.position;P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);}
            void Key(params Key[] held){InputSystem.QueueStateEvent(keys,new KeyboardState(held));InputSystem.QueueStateEvent(mouse,new MouseState());}
            public IEnumerator Run()
            {
                if(!RunStorage.IsValidation||!G.qaMode||!G.expeditionActive||G.expeditionLevel!=4||!G.IsPlaying)
                {C("D2_mission_fixture",false,"Requires isolated active chapter 5 with left R/S deployed");yield break;}
                var root=G.expeditionMap.root;var relay=root.GetComponent<FireControlRelay>();var facility=root.GetComponentInChildren<D2MaintenanceFacility>();
                bool ready=relay&&relay.Sender&&relay.Receiver&&relay.Device&&facility;
                C("D2_mission_fixture",ready);if(!ready)yield break;
                bool oldInput=G.qaInputEnabled;var oldKeys=G.qaKeyboard;var oldMouse=G.qaMouse;var oldBindings=G.options.actions;
                bool linkEnabled=FireControlRelay.FeatureEnabled,facilityEnabled=D2MaintenanceFacility.FeatureEnabled;
                keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();G.qaKeyboard=keys;G.qaMouse=mouse;G.qaInputEnabled=true;G.options.actions=GameOptions.DefaultBindings();
                D2MaintenanceFacility supplies=null;
                try
                {
                    FireControlRelay.FeatureEnabled=true;D2MaintenanceFacility.FeatureEnabled=true;
                    relay.Sender.MoveTo(new Vector3(-4,0,22));relay.Receiver.MoveTo(new Vector3(-7.6f,4.4f,28));relay.Sender.stunLeft=relay.Sender.silenceLeft=0;Physics.SyncTransforms();
                    FireControlRelay.FeatureEnabled=false;relay.Revalidate();FireControlRelay.FeatureEnabled=true;relay.Revalidate();
                    C("R01_no_instant_reconnect",relay.State==RelayLinkState.Connecting,relay.State+":"+relay.Reason);
                    yield return new WaitForSeconds(.86f);relay.Revalidate();C("R01_continuous_acquisition",relay.Connected,relay.State+":"+relay.Reason);
                    relay.Receiver.BeginSniperRearm();relay.Receiver.AdvanceSniperRearm(.9f);
                    float work=relay.Receiver.SniperRearmWork;relay.Receiver.warningLeft=1.2f;relay.Sender.ApplySilence(.4f);relay.Revalidate();
                    C("R02_sender_suppression_preserves_warning",relay.State==RelayLinkState.Suppressed&&Mathf.Abs(relay.Receiver.warningLeft-1.2f)<.001f);
                    relay.Receiver.AdvanceSniperRearm(.6f);
                    C("R02_rate_changes_remaining_work",Mathf.Abs(work-1.2f)<.001f&&Mathf.Abs(relay.Receiver.SniperRearmWork-.6f)<.001f,"linked .9 -> "+work+";unlinked .6 -> "+relay.Receiver.SniperRearmWork);
                    relay.Receiver.warningLeft=0;
                    yield return new WaitForSeconds(.45f);relay.Revalidate();C("R01_suppression_requires_new_charge",relay.State==RelayLinkState.Connecting);
                    C("Q01_old_bonus_replaced",G.expeditionMap.nodes.FindAll(n=>n&&n.kind==ExpeditionNodeKind.Bonus).Count==0,"Chapter-5 bonus now only device recovery");
                    int objectives=G.mission.CompletedObjectives;
                    facility.Anchor.MoveTo(facility.CoverSocket);P.CancelActions();P.SetFeet(facility.CoverSocket+Vector3.back*1.8f);Aim(facility.CoverSocket+Vector3.up*.8f);Physics.SyncTransforms();
                    var blocker=Shapes.Box("D2 occupied-output fixture",root,facility.CoverOutput.transform.position,Vector3.one*.4f,1,true,Layers.Actor);
                    Physics.SyncTransforms();C("F01_occupied_output_rejected",!facility.TryChoose(D2FacilityChoice.Cover)&&facility.Choice==D2FacilityChoice.Unselected);
                    blocker.SetActive(false);UnityEngine.Object.Destroy(blocker);Physics.SyncTransforms();
                    Key();yield return null;Key(UnityEngine.InputSystem.Key.E);yield return new WaitForSeconds(.45f);float progress=D2HoldInteraction.Progress;
                    G.SetState(RunState.Paused);yield return new WaitForSecondsRealtime(.15f);
                    C("F01_pause_freezes_hold",progress>0&&Mathf.Abs(D2HoldInteraction.Progress-progress)<.001f,"progress="+progress);
                    G.SetState(RunState.Playing);Key();yield return null;yield return null;
                    C("F01_release_resets_hold",D2HoldInteraction.Progress==0&&facility.Choice==D2FacilityChoice.Unselected);
                    Key(UnityEngine.InputSystem.Key.E);yield return new WaitForSeconds(1.65f);Key();yield return null;
                    C("F01_real_hold_selects_cover",facility.Choice==D2FacilityChoice.Cover&&facility.CoverOutput.activeSelf);
                    facility.Anchor.MoveTo(facility.SupplySocket);Physics.SyncTransforms();
                    C("F01_choice_is_latched_and_exclusive",!facility.TryChoose(D2FacilityChoice.Supply)&&facility.Choice==D2FacilityChoice.Cover&&G.mission.CompletedObjectives==objectives);
                    supplies=D2MaintenanceFacility.Create(root,facility.Anchor,facility.CoverSocket,facility.SupplySocket,new Vector3(5,0,39));
                    C("F02_supply_output_selects",supplies.TryChoose(D2FacilityChoice.Supply));
                    float oldHealth=P.health;var oldReserve=(int[])P.reserve.Clone();P.health=G.config.playerHealth;
                    foreach(int w in P.EquippedWeapons)P.reserve[w]=G.ExpeditionReserveLimit(w)+7;
                    C("F02_full_state_preserves_portions",!supplies.TakeMedical()&&!supplies.TakeAmmo()&&supplies.MedicalRemaining==1&&supplies.AmmoRemaining==1);
                    int high=P.EquippedWeapons[0],low=P.EquippedWeapons[1],over=P.reserve[high];P.reserve[low]=G.ExpeditionReserveLimit(low)-3;P.health-=6;
                    C("F02_partial_medical_once",supplies.TakeMedical()&&Mathf.Abs(P.health-G.config.playerHealth)<.001f&&!supplies.TakeMedical());
                    C("F02_caps_never_lower_old_reserve",supplies.TakeAmmo()&&P.reserve[high]==over&&P.reserve[low]==G.ExpeditionReserveLimit(low)&&!supplies.TakeAmmo(),"high="+P.reserve[high]+";low="+P.reserve[low]);
                    P.health=oldHealth;P.reserve=oldReserve;
                    supplies.gameObject.SetActive(false);UnityEngine.Object.Destroy(supplies.gameObject);supplies=null;
                    float body=relay.Sender.health;relay.Device.ApplyDamage(12,P.Eye.position);
                    C("Q01_damaged_device_still_usable",relay.Device.Health>0&&Mathf.Abs(relay.Sender.health-body)<.001f,"device="+relay.Device.Health);
                    var device=relay.Device;relay.Sender.Damage(10000);device.ResolveCarrierDeath();var pickup=device.Recovery;device.ResolveCarrierDeath();
                    C("Q01_unique_independent_pickup",pickup&&device.Recovery==pickup&&!pickup.transform.IsChildOf(relay.Sender.transform));
                    yield return new WaitForSeconds(2.65f);C("Q01_pickup_survives_body_cleanup",pickup&&pickup.gameObject.activeInHierarchy);
                    if(pickup)
                    {
                        int credits=G.expeditionStore.Profile.credits;P.CancelActions();P.SetFeet(pickup.transform.position+Vector3.back*1.6f);Aim(pickup.Point);Key();yield return null;
                        Key(UnityEngine.InputSystem.Key.E);yield return new WaitForSeconds(2.15f);Key();yield return null;
                        C("Q01_recovery_real_hold_no_instant_cash",pickup.Recovered&&G.expeditionBonus&&G.expeditionStore.Profile.credits==credits);
                        C("Q01_recovery_cannot_duplicate",!pickup.Commit()&&G.expeditionStore.Profile.credits==credits);
                    }
                }
                finally
                {
                    D2HoldInteraction.CancelForAction();if(G.state==RunState.Paused)G.SetState(RunState.Playing);
                    if(supplies){supplies.gameObject.SetActive(false);UnityEngine.Object.Destroy(supplies.gameObject);}
                    G.qaKeyboard=oldKeys;G.qaMouse=oldMouse;G.qaInputEnabled=oldInput;G.options.actions=oldBindings;
                    FireControlRelay.FeatureEnabled=linkEnabled;D2MaintenanceFacility.FeatureEnabled=facilityEnabled;
                    InputSystem.RemoveDevice(keys);InputSystem.RemoveDevice(mouse);
                }
            }
        }
    }
}
