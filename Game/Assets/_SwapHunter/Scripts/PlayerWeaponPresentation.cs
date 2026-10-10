using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
        public float ReloadProgress=>reloadLeft>0?Mathf.Clamp01(1-reloadLeft/Mathf.Max(.1f,reloadDuration)):0;
        public string ReloadStageTag {get;private set;}="ready";
        public int DiscardedMagazineCount=>discardedMagazineCount;
        public float MagazineSeparation=>magazines[weapon]?Vector3.Distance(magazines[weapon].localPosition,magazineOrigins[weapon]):0;
        public bool ReloadStartedEmpty=>reloadWasEmpty;


        sealed class Cc0ArmChain
        {
            public readonly Transform upper,fore,hand;
            public readonly Vector3 palm;
            readonly Vector3 upperPosition,forePosition,handPosition;
            readonly Quaternion upperRotation,foreRotation,handRotation,bindRigRotation;
            readonly Vector3 bindPalmAxis;
            public Cc0ArmChain(Transform upperBone,Transform foreBone,Transform handBone,Vector3 palmPoint,Transform reference)
            {
                upper=upperBone;fore=foreBone;hand=handBone;palm=palmPoint;
                upperPosition=upper.localPosition;forePosition=fore.localPosition;handPosition=hand.localPosition;
                upperRotation=upper.localRotation;foreRotation=fore.localRotation;handRotation=hand.localRotation;
                bindRigRotation=Quaternion.Inverse(reference.rotation)*hand.rotation;
                bindPalmAxis=reference.InverseTransformDirection(hand.TransformVector(palm)).normalized;
                if(bindPalmAxis.sqrMagnitude<.5f)throw new InvalidOperationException("新手臂PalmContact不构成实际掌心偏移");
            }
            public void Reset()
            {
                upper.localPosition=upperPosition;fore.localPosition=forePosition;hand.localPosition=handPosition;
                upper.localRotation=upperRotation;fore.localRotation=foreRotation;hand.localRotation=handRotation;
            }
            public Quaternion GripRotation(Quaternion frame,Vector3 palmAxis)
                =>frame*Quaternion.FromToRotation(bindPalmAxis,palmAxis.normalized)*bindRigRotation;
            public void Solve(Vector3 contact,Quaternion rotation,Vector3 pole,float weight)
            {
                weight=Mathf.Clamp01(weight);if(weight<=0)return;
                Vector3 priorPosition=upper.localPosition;
                Quaternion priorUpper=upper.localRotation,priorFore=fore.localRotation,priorHand=hand.localRotation;
                Reset();
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
            leftCc0Arm=new Cc0ArmChain(reloadForearm.parent,reloadForearm,reloadHand,reloadPalmContact,weaponRoot);
            rightCc0Arm=new Cc0ArmChain(rightForearm.parent,rightForearm,rightHand,rightPalmContact,weaponRoot);
            foreach(var animator in viewArms.GetComponentsInChildren<Animator>(true))animator.enabled=false;
            foreach(var skin in viewArms.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
        }
        void ResetCc0Hands(){if(leftCc0Arm!=null)leftCc0Arm.Reset();if(rightCc0Arm!=null)rightCc0Arm.Reset();}
        void PoseRightCc0Hand()
        {
            rightGrip=primaryGrips[weapon];
            if(rightCc0Arm==null||!rightGrip)return;
            rightCc0Arm.Solve(rightGrip.position,rightCc0Arm.GripRotation(rightGrip.rotation,Vector3.up),
                Eye.TransformDirection(new Vector3(1,-1,-.2f)),1);
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
                if(magazines[i])
                {
                    magazineRestRotations[i]=magazines[i].localRotation;
                    var marker=ImportedModels.Find(magazines[i],"MagazineGrip");
                    magazineGripPoints[i]=marker?magazines[i].InverseTransformPoint(marker.position):MagazineSideGrip(magazines[i],weaponRoot);
                    if(!marker)throw new InvalidOperationException("新武器弹匣缺少MagazineGrip："+i);
                }
                supportGrips[i]=ImportedModels.Find(weaponModels[i],"SupportGrip");
                primaryGrips[i]=ImportedModels.Find(weaponModels[i],"Grip");
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
            ResetCc0Hands();PoseRightCc0Hand();
            if(supportGrips[weapon])PlaceSupportHand(supportGrips[weapon].position,1);
        }
        void RestoreWeaponParts(int i)
        {
            if(magazines[i]){magazines[i].gameObject.SetActive(true);magazines[i].localPosition=magazineOrigins[i];magazines[i].localRotation=magazineRestRotations[i];}
            if(actionHinges[i])actionHinges[i].localRotation=hingeOrigins[i];
            if(roundGroups[i]){roundGroups[i].gameObject.SetActive(true);roundGroups[i].localPosition=roundOrigins[i];}
            if(belts[i])belts[i].gameObject.SetActive(true);
            if(pumps[i])pumps[i].localPosition=pumpOrigins[i];
            if(bolts[i])bolts[i].localPosition=boltOrigins[i];
        }
        float ReloadPoseWeight=>reloadLeft>0?PoseWindow(ReloadProgress,0,.12f,.88f,1):0;
        Vector3 ReloadPoseOffset=>(weapon==5?new Vector3(.035f,.025f,.075f):new Vector3(.035f,.065f,.08f))*ReloadPoseWeight;
        Quaternion ReloadPoseRotation=>Quaternion.Euler((weapon==5?new Vector3(18,-16,-12):new Vector3(-8,-18,-24))*ReloadPoseWeight);

        void UpdateWeaponPresentation()
        {
            if(!presentationReady)return;
            for(int i=0;i<WeaponCatalog.Count;i++)RestoreWeaponParts(i);
            ResetCc0Hands();PoseRightCc0Hand();
            if(supportGrips[weapon])PlaceSupportHand(supportGrips[weapon].position,1);
            if(reloadLeft<=0)
            {
                ReloadStageTag="ready";reloadGripWeight=0;
                UpdateCyclingParts();
                if(supportGrips[weapon]&&authoredReloadRig)PlaceSupportHand(supportGrips[weapon].position,1);
                return;
            }
            float phase=ReloadProgress;
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
        void UpdateCyclingParts()
        {
            float elapsed=Time.time-lastShotAt;
            float duration=Mathf.Min(.72f,1/Mathf.Max(.1f,Rate)*.85f);
            if(elapsed<0||elapsed>duration)return;
            float phase=elapsed/duration,cycle=PoseWindow(phase,.16f,.43f,.58f,.91f);
            if(CurrentWeaponDefinition.mechanism==FireMechanism.Pump&&pumps[weapon])
                pumps[weapon].localPosition=pumpOrigins[weapon]-Vector3.forward*(.11f*cycle);
            if(CurrentWeaponDefinition.mechanism==FireMechanism.BoltAction&&bolts[weapon])
                bolts[weapon].localPosition=boltOrigins[weapon]-Vector3.forward*(.09f*cycle);
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
