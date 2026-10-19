using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SwapHunter
{
    public sealed partial class PlayerMotor
    {
        readonly Quaternion[] magazineRestRotations=new Quaternion[WeaponCatalog.Count];
        readonly Vector3[] magazineGripPoints=new Vector3[WeaponCatalog.Count];
        readonly Transform[] supportGrips=new Transform[WeaponCatalog.Count],pumps=new Transform[WeaponCatalog.Count],primaryGrips=new Transform[WeaponCatalog.Count];
        readonly Vector3[] pumpOrigins=new Vector3[WeaponCatalog.Count];
        readonly Transform[] actionHinges=new Transform[WeaponCatalog.Count],roundGroups=new Transform[WeaponCatalog.Count],belts=new Transform[WeaponCatalog.Count];
        readonly Quaternion[] hingeOrigins=new Quaternion[WeaponCatalog.Count];
        readonly Vector3[] roundOrigins=new Vector3[WeaponCatalog.Count];
        bool presentationReady,reloadWasEmpty,presentationDiscarded;
        int discardedMagazineCount;
        Vector3 reloadContactWorld;
        GameObject discardedMagazine;
        readonly List<Mesh> ownedWeaponMeshes=new List<Mesh>();
        public float ReloadProgress=>ReloadV2Pending?Mathf.Clamp01(ReloadV2Elapsed/Mathf.Max(.1f,ReloadV2Total)):reloadLeft>0?Mathf.Clamp01(1-reloadLeft/Mathf.Max(.1f,reloadDuration)):0;
        public string ReloadStageTag {get;private set;}="ready";
        public int DiscardedMagazineCount=>discardedMagazineCount;
        public float MagazineSeparation=>magazines[weapon]?Vector3.Distance(magazines[weapon].localPosition,magazineOrigins[weapon]):0;
        public bool ReloadStartedEmpty=>reloadWasEmpty;


        sealed class Cc0ArmChain
        {
            public readonly Transform upper,fore,hand;
            public readonly Vector3 palm;
            public Vector3 stanceOffset;
            readonly Vector3 upperPosition,forePosition,handPosition;
            readonly Quaternion upperRotation,foreRotation,handRotation,gripFrameInHand;readonly bool left;
            public Cc0ArmChain(Transform upperBone,Transform foreBone,Transform handBone,Vector3 palmPoint,Transform reference,Transform authoredFrame,Transform authoredUp,Transform authoredForward,bool isLeft)
            {
                upper=upperBone;fore=foreBone;hand=handBone;palm=palmPoint;
                upperPosition=upper.localPosition;forePosition=fore.localPosition;handPosition=hand.localPosition;
                upperRotation=upper.localRotation;foreRotation=fore.localRotation;handRotation=hand.localRotation;
                if(!authoredFrame||!authoredUp||!authoredForward)throw new InvalidOperationException("连续手套缺少掌面握持方向");
                var palmFrame=Quaternion.LookRotation(authoredForward.position-authoredFrame.position,authoredUp.position-authoredFrame.position);
                gripFrameInHand=Quaternion.Inverse(hand.rotation)*palmFrame;left=isLeft;
            }
            public void Reset()
            {
                upper.localPosition=upperPosition;fore.localPosition=forePosition;hand.localPosition=handPosition;
                upper.localRotation=upperRotation;fore.localRotation=foreRotation;hand.localRotation=handRotation;
            }
            public Quaternion GripRotation(Quaternion frame,Vector3 palmAxis)
            {
                Quaternion axis=Quaternion.FromToRotation(Vector3.up,palmAxis.normalized);
                if(left&&palmAxis.z>.5f)axis*=Quaternion.Euler(0,180,0);
                return frame*axis*Quaternion.Inverse(gripFrameInHand);
            }
            public void Solve(Vector3 contact,Quaternion rotation,Vector3 pole,float weight)
            {
                weight=Mathf.Clamp01(weight);if(weight<=0)return;
                Vector3 priorPosition=upper.localPosition;
                Quaternion priorUpper=upper.localRotation,priorFore=fore.localRotation,priorHand=hand.localRotation;
                Reset();upper.position+=stanceOffset;
                Vector3 wrist=contact-rotation*Vector3.Scale(palm,hand.lossyScale);
                float first=Vector3.Distance(upper.position,fore.position),second=Vector3.Distance(fore.position,hand.position);
                if(first<.01f||second<.01f)throw new InvalidOperationException("新手臂骨链长度无效");
                Vector3 delta=wrist-upper.position;float distance=delta.magnitude;
                if(distance<.0001f)return;
                Vector3 axis=delta/distance;float maximum=(first+second)*.998f;
                // A small real shoulder reach is part of the reload pose; bone lengths and contact markers stay intact.
                if(distance>maximum){upper.position+=axis*Mathf.Min(.12f,distance-maximum);delta=wrist-upper.position;distance=delta.magnitude;axis=delta.normalized;}
                float reach=Mathf.Clamp(distance,Mathf.Abs(first-second)+.002f,maximum);
                Vector3 poleDirection=Vector3.ProjectOnPlane(pole,axis).normalized;
                if(poleDirection.sqrMagnitude<.5f)poleDirection=Vector3.Cross(axis,Vector3.up).normalized;
                float cosine=Mathf.Clamp((first*first+reach*reach-second*second)/(2*first*reach),-1,1);
                Vector3 elbow=upper.position+axis*(first*cosine)+poleDirection*(first*Mathf.Sqrt(Mathf.Max(0,1-cosine*cosine)));
                upper.rotation=Quaternion.FromToRotation(fore.position-upper.position,elbow-upper.position)*upper.rotation;
                Vector3 goal=upper.position+axis*reach;
                fore.rotation=Quaternion.FromToRotation(hand.position-fore.position,goal-fore.position)*fore.rotation;
                hand.rotation=rotation;
                Vector3 solvedPosition=upper.localPosition;
                Quaternion solvedUpper=upper.localRotation,solvedFore=fore.localRotation,solvedHand=hand.localRotation;
                upper.localPosition=Vector3.Lerp(priorPosition,solvedPosition,weight);
                upper.localRotation=Quaternion.Slerp(priorUpper,solvedUpper,weight);
                fore.localRotation=Quaternion.Slerp(priorFore,solvedFore,weight);
                hand.localRotation=Quaternion.Slerp(priorHand,solvedHand,weight);
            }
        }
        Cc0ArmChain leftCc0Arm,rightCc0Arm;
        Transform rightHand,rightGrip;
        Vector3 reloadPalmContact,rightPalmContact;
        public string CurrentWeaponVisualSource=>WeaponVisualFactory.SourceKey(weapon);
        public string CurrentArmsVisualSource=>"Resources/"+WeaponVisualCatalog.ArmsResourcePath;
        public Vector3 CurrentMuzzleWorld=>muzzle?muzzle.position:Eye.position;
        public Vector3 CurrentMuzzleForward=>muzzle?muzzle.forward:Eye.forward;
        public Vector3 RightGripTargetWorld=>rightGrip?rightGrip.position:Vector3.zero;
        public Vector3 RightHandContactWorld=>rightHand?rightHand.TransformPoint(rightPalmContact):Vector3.zero;
        public float RightGripError=>rightGrip&&rightHand?Vector3.Distance(RightGripTargetWorld,RightHandContactWorld):-1;
        public float SupportHandGripError=>reloadHand&&supportGrips[weapon]?Vector3.Distance(ReloadHandContactWorld,supportGrips[weapon].position):-1;
        public bool Cc0HandRigReady=>leftCc0Arm!=null&&rightCc0Arm!=null;
        void InitializeCc0Hands()
        {
            var leftPalm=ImportedModels.Find(viewArms,"PalmContact_L");
            rightHand=ImportedModels.Find(viewArms,"hand_R");
            var rightForearm=ImportedModels.Find(viewArms,"forearm_R");
            var rightPalm=ImportedModels.Find(viewArms,"PalmContact_R");
            if(!reloadHand||!reloadForearm||!leftPalm||!rightHand||!rightForearm||!rightPalm||
                reloadHand.parent!=reloadForearm||rightHand.parent!=rightForearm)
                throw new InvalidOperationException("新CC0 FPS双臂缺少真实上臂/前臂/手与掌心链");
            reloadPalmContact=reloadHand.InverseTransformPoint(leftPalm.position);
            rightPalmContact=rightHand.InverseTransformPoint(rightPalm.position);
            reloadForearm.parent.position+=Eye.forward*.16f+Eye.right*.04f;
            leftCc0Arm=new Cc0ArmChain(reloadForearm.parent,reloadForearm,reloadHand,reloadPalmContact,weaponRoot,ImportedModels.Find(viewArms,"PalmFrame_L"),ImportedModels.Find(viewArms,"PalmToolUp_L"),ImportedModels.Find(viewArms,"PalmToolForward_L"),true);
            rightCc0Arm=new Cc0ArmChain(rightForearm.parent,rightForearm,rightHand,rightPalmContact,weaponRoot,ImportedModels.Find(viewArms,"PalmFrame_R"),ImportedModels.Find(viewArms,"PalmToolUp_R"),ImportedModels.Find(viewArms,"PalmToolForward_R"),false);
            foreach(var animator in viewArms.GetComponentsInChildren<Animator>(true))animator.enabled=false;
            foreach(var skin in viewArms.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
        }
        public Vector3 V2RightPalmWorld=>rightHand?rightHand.TransformPoint(rightPalmContact):Eye.position;
        public float V2KnifeGripError(Transform grip)=>grip?Vector3.Distance(V2RightPalmWorld,grip.position):float.PositiveInfinity;
        public string LastV2PickupDiagnostic{get;private set;}="";
        public Vector3 V2PickupShoulder(bool table)
        {
            float bend=table?.1f:1;float angle=70*bend*Mathf.Deg2Rad;
            return transform.TransformPoint(new Vector3(table?.235f:.295f,1.04f-.62f*bend+.4f*Mathf.Cos(angle)-(table?0:.04f),.4f*Mathf.Sin(angle)-.22f*bend));
        }
        public bool V2PickupReachable(Transform grip,bool table)
        {
            if(!grip||rightCc0Arm==null)return false;
            // Preview the same physical two-bone solve used at the grab event, then restore it.
            // A camera-relative distance estimate is wrong while looking down at a floor/table.
            Vector3 rootLocal=viewArms.localPosition;
            Transform[] chain={rightCc0Arm.upper,rightCc0Arm.fore,rightCc0Arm.hand};
            var positions=new Vector3[3];var rotations=new Quaternion[3];
            for(int i=0;i<3;i++){positions[i]=chain[i].localPosition;rotations[i]=chain[i].localRotation;}
            viewArms.localPosition=new Vector3(.23f,-.28f,.53f);rightCc0Arm.Reset();
            float first=Vector3.Distance(chain[0].position,chain[1].position),second=Vector3.Distance(chain[1].position,chain[2].position);
            Vector3 desired=V2PickupShoulder(table);viewArms.position+=desired-chain[0].position;
            rightCc0Arm.Solve(grip.position,rightCc0Arm.GripRotation(KnifeHandFrameV3(grip),Vector3.up),transform.TransformDirection(new Vector3(1,-.7f,-.25f)),1);
            float error=V2KnifeGripError(grip);
            LastV2PickupDiagnostic="table="+table+";feet="+transform.position.ToString("F3")+";shoulder="+chain[0].position.ToString("F3")+";wrist="+chain[2].position.ToString("F3")+";grip="+grip.position.ToString("F3")+";lengths="+first.ToString("F4")+"/"+second.ToString("F4")+";error="+error.ToString("F5");
            viewArms.localPosition=rootLocal;for(int i=0;i<3;i++){chain[i].localPosition=positions[i];chain[i].localRotation=rotations[i];}
            return error<=.035f;
        }
        public int V2HandsRenderLayer=>viewArms?viewArms.gameObject.layer:-1;
        public void SetPickupProjectionV2(bool active)
        {
            // Pickup is a world-space contact, so hands, world knife and torso use the same
            // projection and normal depth. The narrow firearm FOV resumes after the action.
            if(viewArms)ImportedModels.SetLayer(viewArms,active?Layers.Actor:Layers.ViewModel);
            if(weaponRoot)ImportedModels.SetLayer(weaponRoot,active?Layers.Actor:Layers.ViewModel);
            if(weaponCamera&&cameraView)
            {
                if(active)weaponCamera.fieldOfView=cameraView.fieldOfView;
                else weaponCamera.fieldOfView=Mathf.Lerp(weaponCamera.fieldOfView,G.options.viewModelFov,1-Mathf.Exp(-18*Time.unscaledDeltaTime));
                weaponCamera.enabled=cameraView.enabled&&!active;
            }
        }
        public void SetV2EquipmentVisibility(bool firearm)
        {for(int i=0;i<weaponModels.Length;i++)if(weaponModels[i])weaponModels[i].gameObject.SetActive(firearm&&i==weapon);if(viewArms)viewArms.gameObject.SetActive(true);}
        public void RestoreFirearmHands(){if(leftCc0Arm!=null)leftCc0Arm.stanceOffset=Vector3.zero;ResetCc0Hands();PoseRightCc0Hand();if(supportGrips[weapon])PlaceSupportHand(supportGrips[weapon].position,1);SetKnifeFingers(1);SetPickupBody(0);}
        public void PoseKnifeHands(Transform grip,float fingerClose,Vector3? reach=null,float leftSupport=0)
        {
            if(!grip||rightCc0Arm==null)return;
            viewArms.localPosition=new Vector3(.23f,-.28f,.53f);viewArms.localRotation=Quaternion.identity;ResetCc0Hands();
            if(v2BodyBend>0)
            {
                float bodyBlend=v2PickupTable?Mathf.Clamp01(v2BodyBend*10):v2BodyBend;
                viewArms.rotation=Quaternion.Slerp(Eye.rotation,transform.rotation,bodyBlend);
                if(leftCc0Arm!=null)leftCc0Arm.stanceOffset=(-viewArms.forward*.16f-viewArms.right*(v2PickupTable?.04f:.16f))*bodyBlend;
                Vector3 wanted=Vector3.Lerp(rightCc0Arm.upper.position,V2PickupShoulder(v2PickupTable),bodyBlend);
                viewArms.position+=wanted-rightCc0Arm.upper.position;
            }
            Vector3 target=reach??grip.position;
            rightCc0Arm.Solve(target,rightCc0Arm.GripRotation(KnifeHandFrameV3(grip),Vector3.up),transform.TransformDirection(new Vector3(1,-.7f,-.25f)),1);
            if(leftCc0Arm!=null)leftCc0Arm.Solve(Vector3.Lerp(Eye.TransformPoint(new Vector3(-.23f,-.26f,.31f)),grip.position-grip.right*.045f-grip.up*.025f,Mathf.Clamp01(leftSupport)),leftCc0Arm.GripRotation(Eye.rotation,Vector3.forward),Eye.TransformDirection(new Vector3(-1,-1,-.2f)),1);
            SetKnifeFingers(fingerClose);
            if(v2BodyBend>0&&playerBody){var worldRig=playerBody.GetComponent<ActorVisualRig>();if(worldRig)worldRig.SetPickupHand(rightHand.position);}
            var knife=grip.GetComponentInParent<KnifeV2Visual>();if(knife&&knife.KnifeId==2&&knife.Ring&&fingerClose>.8f)PoseIndexContactV2(knife.Ring.position);
        }
        static Quaternion KnifeHandFrameV3(Transform grip)
        {var knife=grip?grip.GetComponentInParent<KnifeV2Visual>():null;return grip.rotation*(knife&&knife.KnifeId==2?Quaternion.Euler(0,0,180):Quaternion.identity);}
        readonly Dictionary<Transform,Quaternion> knifeFingerRest=new Dictionary<Transform,Quaternion>(),fingerClosedV3=new Dictionary<Transform,Quaternion>(),fingerTriggerV3=new Dictionary<Transform,Quaternion>();
        void SetKnifeFingers(float closed)
        {
            if(!viewArms)return;
            if(knifeFingerRest.Count==0)foreach(var t in viewArms.GetComponentsInChildren<Transform>(true))
            {
                if(!t.name.StartsWith("Finger",StringComparison.Ordinal)||!(t.name.EndsWith(".R",StringComparison.Ordinal)||t.name.EndsWith(".L",StringComparison.Ordinal)))continue;
                var shut=ImportedModels.Find(viewArms,"ClosedPose_"+t.name);var trigger=ImportedModels.Find(viewArms,"TriggerPose_"+t.name);
                if(!shut||!trigger)throw new InvalidOperationException("连续手套缺少指节姿态："+t.name);
                knifeFingerRest[t]=t.localRotation;
                Quaternion calibration=Quaternion.Inverse(FingerDirectionFrameV3("Open",t.name))*t.rotation;
                fingerClosedV3[t]=Quaternion.Inverse(t.parent.rotation)*FingerDirectionFrameV3("Closed",t.name)*calibration;
                fingerTriggerV3[t]=Quaternion.Inverse(t.parent.rotation)*FingerDirectionFrameV3("Trigger",t.name)*calibration;
            }
            foreach(var entry in knifeFingerRest)if(entry.Key)entry.Key.localRotation=Quaternion.Slerp(entry.Value,fingerClosedV3[entry.Key],Mathf.Clamp01(closed));
        }
        Quaternion FingerDirectionFrameV3(string pose,string bone)
        {
            var head=ImportedModels.Find(viewArms,pose+"Head_"+bone);var tail=ImportedModels.Find(viewArms,pose+"Tail_"+bone);var side=ImportedModels.Find(viewArms,pose+"Side_"+bone);
            if(!head||!tail||!side)throw new InvalidOperationException("缺少指节几何方向："+pose+"/"+bone);
            Vector3 up=(tail.position-head.position).normalized,right=(side.position-head.position).normalized;
            return Quaternion.LookRotation(Vector3.Cross(right,up),up);
        }
        float v2BodyBend;bool v2PickupTable;
        public void SetPickupBody(float crouchWeight,bool table=false)
        {
            v2BodyBend=Mathf.Clamp01(crouchWeight);v2PickupTable=table;
            if(playerBody)
            {
                var bodyRig=playerBody.GetComponent<ActorVisualRig>();if(bodyRig)
                {
                    bodyRig.SetPickupBody(crouchWeight);bodyRig.EvaluatePickupPose();
                    // The world body keeps its full crouch. First-person framing uses arms only;
                    // central torso/knees would occlude a ground-level contact point.
                }
            }
            if(viewArms&&v2BodyBend<=0){viewArms.localPosition=new Vector3(.23f,-.28f,.53f);viewArms.localRotation=Quaternion.identity;if(leftCc0Arm!=null)leftCc0Arm.stanceOffset=Vector3.zero;}
        }
        void ResetCc0Hands(){if(leftCc0Arm!=null)leftCc0Arm.Reset();if(rightCc0Arm!=null)rightCc0Arm.Reset();}
        void PoseRightCc0Hand()
        {
            rightGrip=primaryGrips[weapon];
            if(rightCc0Arm==null||!rightGrip)return;
            rightCc0Arm.Solve(rightGrip.position,rightCc0Arm.GripRotation(rightGrip.rotation,Vector3.up),
                Eye.TransformDirection(new Vector3(1,-1,-.2f)),1);SetKnifeFingers(1);
            PoseIndexContactV2(weaponModels[weapon].TransformPoint(new Vector3(.008f,-.059f,-.052f)));
        }
        public float V2TriggerFingerError{get;private set;}
        void PoseIndexContactV2(Vector3 target)
        {
            var tip=ImportedModels.Find(viewArms,"FingerIndexTip_R");if(!tip)return;
            foreach(var entry in fingerTriggerV3)if(entry.Key&&entry.Key.name.StartsWith("Finger1",StringComparison.Ordinal)&&entry.Key.name.EndsWith(".R",StringComparison.Ordinal))entry.Key.localRotation=entry.Value;
            // Bounded correction to the authored trigger pose. It rotates real finger joints,
            // keeps their lengths fixed and never moves the palm off its handle surface.
            var end=ImportedModels.Find(viewArms,"Finger1_tip.R");if(!end)return;var middle=end.parent;var first=middle.parent;
            for(int pass=0;pass<3;pass++)foreach(var joint in new[]{end,middle,first})
            {
                Vector3 from=tip.position-joint.position,toward=target-joint.position;if(from.sqrMagnitude<.000001f||toward.sqrMagnitude<.000001f)continue;
                var turn=Quaternion.RotateTowards(Quaternion.identity,Quaternion.FromToRotation(from,toward),10);joint.rotation=turn*joint.rotation;
                if(knifeFingerRest.TryGetValue(joint,out var open))joint.localRotation=Quaternion.RotateTowards(open,joint.localRotation,joint==first?95:110);
            }
            V2TriggerFingerError=Vector3.Distance(tip.position,target);
        }
        void PoseCc0ToolHand(Vector3 contact,Quaternion frame)
        {
            if(leftCc0Arm==null)return;
            leftCc0Arm.Solve(contact,leftCc0Arm.GripRotation(frame,Vector3.forward),
                Eye.TransformDirection(new Vector3(-1,-.6f,-.2f)),1);
        }

        static float PoseEase(float a,float b,float value)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,value));
        static float PoseWindow(float phase,float a,float b,float c,float d)=>PoseEase(a,b,phase)*(1-PoseEase(c,d,phase));
        void InitializeWeaponPresentation()
        {
            for(int i=0;i<WeaponCatalog.Count;i++)
            {
                firearmRestPositionV2[i]=weaponModels[i].localPosition;if(bolts[i])boltRestRotationV2[i]=bolts[i].localRotation;
                if(magazines[i])
                {
                    magazineRestRotations[i]=magazines[i].localRotation;
                    var marker=ImportedModels.Find(magazines[i],"MagazineGrip");
                    magazineGripPoints[i]=marker?magazines[i].InverseTransformPoint(marker.position):MagazineSideGrip(magazines[i],weaponRoot);
                    if(!marker)throw new InvalidOperationException("新武器弹匣缺少MagazineGrip："+i);
                }
                supportGrips[i]=ImportedModels.Find(weaponModels[i],"SupportGrip");
                primaryGrips[i]=ImportedModels.Find(weaponModels[i],"Grip");
                if(primaryGrips[i])primaryGrips[i].position-=weaponModels[i].forward*.020f;
                pumps[i]=ImportedModels.Find(weaponModels[i],"foregrip");if(pumps[i])pumpOrigins[i]=pumps[i].localPosition;
                if(i==1&&pumps[i]&&supportGrips[i])supportGrips[i].SetParent(pumps[i],true);
                string hingeName=i==5?"break_action":i==8?"feed_cover":i==9?"cylinder_hinge":"";
                actionHinges[i]=hingeName.Length>0?ImportedModels.Find(weaponModels[i],hingeName):null;
                if(actionHinges[i])hingeOrigins[i]=actionHinges[i].localRotation;
                roundGroups[i]=ImportedModels.Find(weaponModels[i],"reload_rounds");if(roundGroups[i])roundOrigins[i]=roundGroups[i].localPosition;
                belts[i]=ImportedModels.Find(weaponModels[i],"ammo_belt");
            }
            // Keep the new CC0 skin materials; no legacy ceramic/metal recolour pass.
            presentationReady=true;
        }
        static Vector3 MagazineSideGrip(Transform magazine,Transform rig)
        {
            bool found=false;Bounds bounds=new Bounds();
            foreach(var mesh in magazine.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!mesh.sharedMesh)continue;var b=mesh.sharedMesh.bounds;
                for(int mask=0;mask<8;mask++)
                {
                    var corner=b.center+Vector3.Scale(b.extents,new Vector3((mask&1)==0?-1:1,(mask&2)==0?-1:1,(mask&4)==0?-1:1));
                    var point=magazine.InverseTransformPoint(mesh.transform.TransformPoint(corner));
                    if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);
                }
            }
            bool localRightIsRight=Vector3.Dot(magazine.TransformVector(Vector3.right),rig.right)>0;
            return found?new Vector3(localRightIsRight?bounds.min.x-.001f:bounds.max.x+.001f,bounds.center.y,bounds.center.z):new Vector3(-.035f,-.07f,0);
        }
        void BeginReloadPresentation()
        {
            ResetReloadPresentation();reloadWasEmpty=ammo[weapon]==0;presentationDiscarded=false;discardedMagazineCount=0;
        }
        void ResetReloadPresentation()
        {
            if(!presentationReady)return;
            for(int i=0;i<WeaponCatalog.Count;i++)RestoreWeaponParts(i);
            if(discardedMagazine)Destroy(discardedMagazine);
            reloadGripWeight=0;ReloadStageTag="ready";
            ApplyFirearmMotionV2();ResetCc0Hands();PoseRightCc0Hand();
            if(supportGrips[weapon])PlaceSupportHand(supportGrips[weapon].position,1);
        }
        void RestoreWeaponParts(int i)
        {
            if(magazines[i]){magazines[i].gameObject.SetActive(true);magazines[i].localPosition=magazineOrigins[i];magazines[i].localRotation=magazineRestRotations[i];}
            if(actionHinges[i])actionHinges[i].localRotation=hingeOrigins[i];
            if(roundGroups[i]){roundGroups[i].gameObject.SetActive(true);roundGroups[i].localPosition=roundOrigins[i];}
            if(belts[i])belts[i].gameObject.SetActive(true);
            if(pumps[i])pumps[i].localPosition=pumpOrigins[i];
            if(bolts[i]){bolts[i].localPosition=boltOrigins[i];bolts[i].localRotation=boltRestRotationV2[i];}
        }
        float ReloadPoseWeight=>reloadLeft>0?PoseWindow(ReloadProgress,0,.12f,.88f,1):0;
        Vector3 ReloadPoseOffset=>(weapon==5?new Vector3(.035f,.025f,.075f):new Vector3(.035f,.065f,.08f))*ReloadPoseWeight;
        Quaternion ReloadPoseRotation=>Quaternion.Euler((weapon==5?new Vector3(18,-16,-12):new Vector3(-8,-18,-24))*ReloadPoseWeight);

        void UpdateWeaponPresentation()
        {
            if(!presentationReady)return;
            for(int i=0;i<WeaponCatalog.Count;i++)RestoreWeaponParts(i);
            ApplyFirearmMotionV2();ResetCc0Hands();PoseRightCc0Hand();
            if(supportGrips[weapon])PlaceSupportHand(supportGrips[weapon].position,1);
            if(!ReloadV2Pending&&reloadLeft<=0)
            {
                ReloadStageTag="ready";reloadGripWeight=0;
                UpdateCyclingParts();
                if(supportGrips[weapon]&&authoredReloadRig)PlaceSupportHand(supportGrips[weapon].position,1);
                return;
            }
            float realPhase=ReloadProgress;
            if(weapon==1&&ReloadV2Pending){PoseTubeReloadV2();return;}
            float phase=RemapReloadV2(realPhase);
            ReloadStageTag=phase<.20f?"reach":phase<.40f?"extract":phase<.50f?"stow":phase<.56f?"fetch":phase<.79f?"insert":phase<.91f?(reloadWasEmpty||weapon==5||weapon==9?"chamber":"return"):"ready";
            var magazine=magazines[weapon];if(!magazine)return;
            bool breakAction=weapon==5,rotary=weapon==9;
            if(actionHinges[weapon])
            {
                float opened=PoseWindow(phase,.08f,.22f,.79f,.92f);
                var euler=breakAction?new Vector3(35,0,0):rotary?new Vector3(0,0,-65):new Vector3(-55,0,0);
                actionHinges[weapon].localRotation=hingeOrigins[weapon]*Quaternion.Euler(euler*opened);
            }
            float withdrawal=phase<.40f?PoseEase(.20f,.38f,phase):phase<.56f?1:1-PoseEase(.56f,.79f,phase);
            bool magazineVisible=phase<.42f||phase>=.54f;
            Transform moving=rotary&&roundGroups[weapon]?roundGroups[weapon]:magazine;
            if(!rotary||roundGroups[weapon])
            {
                Vector3 direction=breakAction||rotary?Vector3.back:WeaponCatalog.Get(weapon).magazineExitDirection;
                float travel=breakAction?.17f:rotary?.16f:weapon==8?.33f:weapon==4?.26f:.29f;
                Vector3 rest=rotary?roundOrigins[weapon]:magazineOrigins[weapon];
                float handedness=Vector3.Dot(moving.parent.TransformVector(Vector3.right),weaponRoot.right)>0?1:-1;
                // Leave the well straight before moving sideways; reverse that
                // order for insertion so the magazine lip cannot cut the well wall.
                Vector3 side=breakAction||rotary?Vector3.zero:Vector3.left*(.065f*PoseEase(.35f,.8f,withdrawal)*handedness);
                float stow=PoseWindow(phase,.38f,.47f,.51f,.58f);
                moving.localPosition=rest+direction*travel*withdrawal+side+
                    (breakAction||rotary?Vector3.back*.14f:Vector3.down*.30f+Vector3.back*.12f)*stow;
                moving.gameObject.SetActive(magazineVisible);
            }
            if(belts[weapon])belts[weapon].gameObject.SetActive(phase<.26f||phase>.74f);
            if(phase>=.40f&&!presentationDiscarded)
            {
                presentationDiscarded=true;
                // Remaining rounds are retained by stowing a partial magazine. Only
                // an empty-box reload drops an empty decorative magazine.
                if(reloadWasEmpty&&!breakAction&&!rotary)DropEmptyMagazine(magazine);
            }
            Vector3 contact=magazine.TransformPoint(magazineGripPoints[weapon]);
            Quaternion gripRotation=magazine.rotation;
            if(rotary&&roundGroups[weapon])
            {
                contact=roundGroups[weapon].position+roundGroups[weapon].rotation*new Vector3(-.04f,-.02f,-.04f);
                gripRotation=roundGroups[weapon].rotation;
            }
            float weight=PoseWindow(phase,.07f,.20f,.86f,1);
            // Reach/stow/release are transitions, not closed-hand contact. The
            // public contact weight describes only the actually held supply item.
            reloadGripWeight=phase>=.20f&&phase<=.79f&&magazineVisible?1:0;reloadContactWorld=contact;
            PlaceReloadHand(contact,gripRotation,weight);
            if(phase>.79f&&phase<.93f&&reloadWasEmpty&&!breakAction&&!rotary)
            {
                float action=PoseWindow(phase,.79f,.84f,.89f,.94f);
                if(bolts[weapon])bolts[weapon].localPosition=boltOrigins[weapon]-Vector3.forward*(.065f*action);
                if(weapon==1&&pumps[weapon])pumps[weapon].localPosition=pumpOrigins[weapon]-Vector3.forward*(.11f*action);
                // The off hand releases the seated magazine before reaching the action.
                reloadGripWeight=0;
                Vector3 latch=weaponModels[weapon].TransformPoint(new Vector3(-.07f,.045f,.035f));
                PlaceReloadHand(Vector3.Lerp(contact,latch,action),weaponModels[weapon].rotation*Quaternion.Euler(0,0,90),weight);
            }
        }
        void PlaceSupportHand(Vector3 target,float weight)
        {
            if(leftCc0Arm==null)return;
            leftCc0Arm.Solve(target,leftCc0Arm.GripRotation(weaponRoot.rotation,new Vector3(0,.45f,1)),
                Eye.TransformDirection(new Vector3(-1,-1,-.2f)),weight);
        }
        void PlaceReloadHand(Vector3 grip,Quaternion worldRotation,float weight)
        {
            if(leftCc0Arm==null)return;
            leftCc0Arm.Solve(grip,leftCc0Arm.GripRotation(worldRotation,Vector3.up),
                Eye.TransformDirection(new Vector3(-1,-1,-.2f)),weight);
        }
        void DropEmptyMagazine(Transform source)
        {
            discardedMagazine=Instantiate(source.gameObject,G.effects,true);
            discardedMagazine.name="Discarded magazine / weapon_"+weapon.ToString("00");
            discardedMagazine.SetActive(true);ImportedModels.SetLayer(discardedMagazine.transform,Layers.ViewModel);
            foreach(var collider in discardedMagazine.GetComponentsInChildren<Collider>()){collider.enabled=false;Destroy(collider);}
            var effect=discardedMagazine.AddComponent<DiscardedMagazineVisual>();effect.weaponId=weapon;effect.sourceMagazineId=source.GetInstanceID();
            effect.velocity=Eye.right*-.72f+Eye.up*.52f+Eye.forward*.10f;
            discardedMagazineCount++;
        }
        readonly Vector3[] firearmRestPositionV2=new Vector3[WeaponCatalog.Count];
        readonly Quaternion[] boltRestRotationV2=new Quaternion[WeaponCatalog.Count];
        int visualWeaponV2=-1,lastCaseShotV2=-1;float visualSwitchAtV2;
        Transform tubeShellV2;
        float RemapReloadV2(float p)
        {
            float commit=ReloadV2CommitFraction(weapon);
            if(p<=commit)return p/Mathf.Max(.01f,commit)*.79f;
            return .79f+(p-commit)/Mathf.Max(.01f,1-commit)*.21f;
        }
        void ApplyFirearmMotionV2()
        {
            if(visualWeaponV2!=weapon){visualWeaponV2=weapon;visualSwitchAtV2=Time.time;}
            var profile=WeaponMotionV2.Get(weapon);float since=Time.time-lastShotAt;
            float impulse=since<.035f?Mathf.Clamp01(since/.035f):1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.035f,profile.recoilReturn,since));
            float entry=1-Mathf.SmoothStep(0,1,(Time.time-visualSwitchAtV2)/profile.readySeconds);
            if(weaponModels[weapon])weaponModels[weapon].localPosition=firearmRestPositionV2[weapon]+new Vector3(.025f*entry,-.12f*entry,-profile.recoilTravel*impulse);
            if(tubeShellV2)tubeShellV2.gameObject.SetActive(weapon==1&&ReloadV2Pending);
            float ejectAt=weapon==1?.35f:weapon==7?.56f:.035f;
            if(shots!=lastCaseShotV2&&since>=ejectAt&&since<ejectAt+.12f&&weapon<9&&weapon!=5){lastCaseShotV2=shots;EjectCaseV2();}
        }
        void PoseTubeReloadV2()
        {
            ReloadStageTag=ReloadV2Stage;float elapsed=ReloadV2Elapsed;
            float roundPhase=Mathf.Repeat(Mathf.Max(0,elapsed-.25f),.4f)/.4f;
            if(!tubeShellV2)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="Single physical loading shell";go.GetComponent<Collider>().enabled=false;Destroy(go.GetComponent<Collider>());go.transform.SetParent(weaponModels[1],false);go.transform.localScale=new Vector3(.017f,.031f,.017f);go.GetComponent<Renderer>().sharedMaterial=Shapes.Mat(4);ImportedModels.SetLayer(go.transform,Layers.ViewModel);tubeShellV2=go.transform;
            }
            Vector3 port=weaponModels[1].TransformPoint(new Vector3(-.03f,-.065f,-.08f));
            Vector3 supply=Eye.TransformPoint(new Vector3(-.21f,-.52f,.28f));
            float insert=PoseEase(.10f,.84f,roundPhase);tubeShellV2.position=Vector3.Lerp(supply,port,insert);tubeShellV2.rotation=weaponModels[1].rotation*Quaternion.Euler(90,0,0);
            tubeShellV2.gameObject.SetActive(elapsed>=.25f&&ReloadV2Stage!="return"&&roundPhase<.88f);
            PlaceReloadHand(tubeShellV2.position,tubeShellV2.rotation,1);reloadContactWorld=tubeShellV2.position;reloadGripWeight=1;
            if(ReloadV2Stage=="return")PlaceSupportHand(supportGrips[1].position,Mathf.Clamp01((elapsed-(ReloadV2Total-.25f))/.25f));
        }
        void EjectCaseV2()
        {
            if(!weaponModels[weapon])return;var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="Case from receiver / "+weapon;go.GetComponent<Collider>().enabled=false;Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(G.effects,false);go.transform.localScale=new Vector3(.008f,.019f,.008f);
            go.transform.position=bolts[weapon]?bolts[weapon].position:weaponModels[weapon].TransformPoint(new Vector3(.045f,.035f,.015f));go.transform.rotation=weaponModels[weapon].rotation*Quaternion.Euler(0,0,90);go.GetComponent<Renderer>().sharedMaterial=Shapes.Mat(4);go.layer=Layers.ViewModel;
            var effect=go.AddComponent<DiscardedMagazineVisual>();effect.velocity=Eye.right*1.1f+Eye.up*.6f;effect.weaponId=weapon;
        }
        void UpdateCyclingParts()
        {
            float t=Time.time-lastShotAt;
            if(CurrentWeaponDefinition.mechanism==FireMechanism.Pump&&pumps[weapon])
            {
                float pull=PoseEase(.12f,.35f,t)*(1-PoseEase(.35f,.56f,t));pumps[weapon].localPosition=pumpOrigins[weapon]-Vector3.forward*(.11f*pull);
            }
            if(CurrentWeaponDefinition.mechanism==FireMechanism.BoltAction&&bolts[weapon]&&t>=.18f&&t<=1.25f)
            {
                float lift=PoseEase(.18f,.32f,t)*(1-PoseEase(.88f,1.05f,t)),pull=PoseEase(.32f,.56f,t)*(1-PoseEase(.66f,.88f,t));
                bolts[weapon].localPosition=boltOrigins[weapon]-Vector3.forward*(.09f*pull);bolts[weapon].localRotation=boltRestRotationV2[weapon]*Quaternion.Euler(0,0,-55*lift);
                float hold=PoseWindow(t,.18f,.29f,1.05f,1.25f);
                if(rightCc0Arm!=null)rightCc0Arm.Solve(bolts[weapon].position,rightCc0Arm.GripRotation(bolts[weapon].rotation,Vector3.up),Eye.TransformDirection(new Vector3(1,-.7f,-.1f)),hold);
            }
        }
        GameObject CreateMuzzleFlash(Transform marker)
        {
            var root=new GameObject("Brief muzzle gas flash");root.transform.SetParent(marker,false);
            var mesh=new Mesh{name="Crossed muzzle flash tongues"};ownedWeaponMeshes.Add(mesh);
            var vertices=new Vector3[12];var triangles=new int[12];
            for(int i=0;i<4;i++)
            {
                float angle=i*Mathf.PI*.5f;Vector3 side=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*.011f;
                vertices[i*3]=side;vertices[i*3+1]=-side;vertices[i*3+2]=Vector3.forward*(i%2==0?.083f:.055f);
                triangles[i*3]=i*3;triangles[i*3+1]=i*3+1;triangles[i*3+2]=i*3+2;
            }
            mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=Shapes.Mat(7);renderer.shadowCastingMode=ShadowCastingMode.Off;
            var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",new Color(1,.79f,.43f));renderer.SetPropertyBlock(block);
            root.SetActive(false);return root;
        }
        void OnDestroy()
        {
            // These meshes are generated by this player; imported weapon meshes
            // and shared authored materials are owned by their asset resources.
            foreach(var mesh in ownedWeaponMeshes)if(mesh)Destroy(mesh);
            ownedWeaponMeshes.Clear();
        }
    }
    public sealed class DiscardedMagazineVisual:MonoBehaviour
    {
        public int weaponId,sourceMagazineId;
        public Vector3 velocity;
        float age;
        void Update()
        {
            float dt=Time.deltaTime;age+=dt;velocity+=Vector3.down*5.5f*dt;
            transform.position+=velocity*dt;transform.Rotate(new Vector3(80,-50,35)*dt,Space.Self);
            if(age>.85f)Destroy(gameObject);
        }
    }
}
