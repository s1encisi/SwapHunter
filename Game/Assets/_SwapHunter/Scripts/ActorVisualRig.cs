using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SwapHunter
{
    // Presentation adapter for the new CC0 skins. The gameplay transform is never an animation target.
    [DefaultExecutionOrder(200)]
    public sealed class ActorVisualRig : MonoBehaviour
    {
        public string ResourcePath {get;private set;}
        public string SourceKey=>ResourcePath;
        public string CurrentAnimation=>currentClip?currentClip.name:"";
        public bool AnimationAvailable=>clips.Length>0&&graph.IsValid()&&player.IsValid();
        public float AnimationSeconds=>player.IsValid()?(float)player.GetTime():0;
        public Transform VisualRoot=>transform;
        public Bounds ModelBounds
        {
            get{Bounds bounds=new Bounds(transform.position,Vector3.zero);bool found=false;if(body)foreach(var renderer in body.GetComponentsInChildren<Renderer>(true)){if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);}return bounds;}
        }
        public float ModelHeight=>ModelBounds.size.y;
        public string CurrentMotion {get;private set;}="";
        public int AnimationClipCount=>clips.Length;
        public bool IsMech {get;private set;}
        public Transform WeaponMount {get;private set;}
        public Transform Muzzle {get;private set;}
        public Transform RightHand=>rightHand;
        public Transform Head=>head;
        public Transform Chest=>chest;
        public Transform Pelvis=>hips;
        public Vector3 HeadAimMarker=>equipmentV2&&equipmentV2.MechanicalBody?equipmentV2.Weakpoint("equipment").position:authoredHeadHit?authoredHeadHit.position:hitMarkersReady?head.TransformPoint(headHitOffset):head.position;
        public Vector3 TorsoAimMarker=>equipmentV2&&equipmentV2.MechanicalBody?transform.TransformPoint(new Vector3(0,1.12f*(owner?owner.Height/1.8f:1),0)):authoredChestHit?authoredChestHit.position:hitMarkersReady?chest.TransformPoint(torsoHitOffset):chest.position;
        public bool IsPlayerShadow=>shadowOnly;
        public Transform BodyRoot=>body?body.transform:null;
        public bool BladeVisible=>blade&&blade.gameObject.activeInHierarchy;
        public bool RifleVisible=>weapon&&weapon.gameObject.activeInHierarchy;
        public Vector3 BladePosition=>blade?blade.position:transform.position;
        public float BladeGripError=>bladeGrip&&rightHand?Vector3.Distance(bladeGrip.position,rightHand.position):float.PositiveInfinity;
        public float MeleePoseOffset=>shieldPoseOffset;
        public bool RootMotionDisabled=>animator&&!animator.applyRootMotion&&!animator.fireEvents;
        EnemyActor owner;PlayerMotor playerOwner;GameObject body;Animator animator;AnimationClip[] clips=Array.Empty<AnimationClip>();
        PlayableGraph graph;AnimationPlayableOutput output;AnimationClipPlayable player;
        AnimationClip currentClip,idleClip,walkClip,hitClip,shootClip,pickupClip,punchClip,kickClip,jumpClip,slashClip,deathClip;float clipClock,transitionClock,deathVisualTime;string motionKey="",bossMotionKey="";
        Quaternion handWeaponOffset=Quaternion.identity,handBladeOffset=Quaternion.identity;
        struct BonePose { public Transform bone; public Vector3 restPosition,fromPosition;public Quaternion restRotation,fromRotation; }
        BonePose[] bones=Array.Empty<BonePose>();
        Transform hips,head,chest,rightUpper,rightLower,rightHand,leftUpper,leftLower,leftHand;
        Transform rightPalmMarker,leftPalmMarker;
        Vector3 rightPalmLocal,leftPalmLocal;
        Quaternion rightPalmFrameInHand,leftPalmFrameInHand;
        public float FirearmPalmError=>weapon&&rightPalmMarker?Vector3.Distance(rightPalmMarker.position,WeaponMount.position):-1;
        public float SupportPalmError=>weapon&&leftPalmMarker&&supportGrip?Vector3.Distance(leftPalmMarker.position,supportGrip.position):-1;
        Transform weapon,supportGrip,blade,bladeGrip,shield,shieldGrip;BoxCollider shieldProxy;
        Vector3 hipsRestInBody,aim,headHitOffset,torsoHitOffset;float bodyScale=1;bool hitMarkersReady;int observedPlayerShots;float speed,gait,hitLeft,shotLeft,shieldSeconds,bossWindup,bossSwing,shieldPoseOffset;
        bool displayOnly,shadowOnly,dead,stunned,grenadePreparing,shieldMelee,bossMelee,shotRestart;
        TacticalState shieldState;BossSkill lastBossSkill;BossSkillStep lastBossStep;float telegraphSeconds;
        int displayClass,weaponId;
        EnemyV2VisualEquipment equipmentV2; int variantV2=-1,bossChapterV2=-1; float pickupBodyWeight;Vector3 pickupHandWorld;bool pickupHandValid; Transform authoredHeadHit,authoredChestHit;

        public static ActorVisualRig CreateEnemy(EnemyActor actor,BoxCollider physicalShield)
        {
            var go=new GameObject("New actor visual rig");go.layer=Layers.Actor;go.transform.SetParent(actor.transform,false);
            var rig=go.AddComponent<ActorVisualRig>();rig.owner=actor;rig.shieldProxy=physicalShield;
            string key=actor.kind==EnemyKind.Sniper?"class_02":actor.kind==EnemyKind.Shield||actor.kind==EnemyKind.Elite?"class_01":"class_00";
            rig.weaponId=actor.kind==EnemyKind.Sniper?7:actor.kind==EnemyKind.Elite?8:0;
            rig.LoadBody(key,false,actor.Height);rig.ConfigureVariant((int)actor.kind);
            rig.shieldMelee=actor.kind==EnemyKind.Shield;
            if(physicalShield){rig.shield=ActorEquipmentVisuals.CreateShield(physicalShield.transform);rig.shield.localScale=physicalShield.size.y/1.42f*Vector3.one;rig.shieldGrip=ImportedModels.Find(rig.shield,"Grip");}
            rig.aim=actor.AimPoint+actor.transform.forward*10;rig.Evaluate(0);rig.CalibrateHitMarkers();return rig;
        }
        public static GameObject CreateClassDisplay(int classId,Transform parent)
        {
            if(classId<0||classId>3)throw new ArgumentOutOfRangeException(nameof(classId));
            var go=new GameObject("New class body "+classId);go.transform.SetParent(parent,false);
            var rig=go.AddComponent<ActorVisualRig>();rig.displayOnly=true;rig.displayClass=classId;rig.weaponId=TacticalPrototypeRules.StarterWeapon(classId);
            rig.LoadBody("class_"+classId.ToString("00"),false,1.8f);
            rig.aim=go.transform.position+go.transform.forward*10+go.transform.up*1.3f;rig.Evaluate(0);rig.CalibrateHitMarkers();
            ImportedModels.SetLayer(go.transform,parent?parent.gameObject.layer:0);return go;
        }
        public static GameObject CreatePlayerShadowBody(int classId,Transform parent)
        {
            var player=parent?parent.GetComponent<PlayerMotor>():null;if(!player)throw new ArgumentException("Player shadow body requires PlayerMotor parent");
            var go=new GameObject("New player shadow body");go.layer=Layers.Actor;go.transform.SetParent(parent,false);
            var rig=go.AddComponent<ActorVisualRig>();rig.shadowOnly=true;rig.playerOwner=player;rig.displayClass=classId;
            rig.LoadBody("class_"+Mathf.Clamp(classId,0,3).ToString("00"),false,1.8f);rig.aim=player.transform.position+player.transform.forward*10+Vector3.up*1.3f;rig.Evaluate(0);rig.CalibrateHitMarkers();return go;
        }
        void LoadBody(string key,bool mech,float targetHeight)
        {
            ResourcePath=(mech?"VisualV2/Actors/":"VisualMapV2/Actors/")+key;IsMech=mech;bodyScale=targetHeight/(mech?2.1f:1.8f);hitMarkersReady=false;
            var asset=Resources.Load<GameObject>(ResourcePath);
            if(!asset)throw new InvalidOperationException("Required new actor model is missing: "+ResourcePath);
            ReleaseAnimation();if(WeaponMount)Destroy(WeaponMount.gameObject);if(blade)Destroy(blade.gameObject);if(body){body.SetActive(false);Destroy(body);}
            body=Instantiate(asset,transform,false);body.name=(mech?"CC0 final boss / ":"Original tactical human / ")+key;body.transform.localPosition=Vector3.zero;body.transform.localRotation=Quaternion.identity;body.transform.localScale=Vector3.one*bodyScale;
            foreach(var c in body.GetComponentsInChildren<Collider>(true)){c.enabled=false;Destroy(c);}
            foreach(var r in body.GetComponentsInChildren<Rigidbody>(true))Destroy(r);
            animator=body.GetComponent<Animator>();if(!animator)animator=body.GetComponentInChildren<Animator>(true);if(!animator)animator=body.AddComponent<Animator>();
            foreach(var b in body.GetComponentsInChildren<Behaviour>(true))if(b!=animator){b.enabled=false;Destroy(b);}
            animator.enabled=true;animator.applyRootMotion=false;animator.fireEvents=false;animator.runtimeAnimatorController=null;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            clips=Resources.LoadAll<AnimationClip>(ResourcePath);
            if(clips.Length==0)throw new InvalidOperationException("New actor has no imported animation clips: "+ResourcePath);
            hips=Bone(new[]{"Body","Hips","mixamorig:Hips","Pelvis"});
            chest=Bone(new[]{"Chest","Spine2","mixamorig:Spine2","Torso","Spine"});
            head=Bone(new[]{"Head","mixamorig:Head"});
            rightUpper=Bone(new[]{"UpperArm.R","RightArm","mixamorig:RightArm","upper_arm.R"});rightLower=Bone(new[]{"LowerArm.R","RightForeArm","mixamorig:RightForeArm","forearm.R"});
            leftUpper=Bone(new[]{"UpperArm.L","LeftArm","mixamorig:LeftArm","upper_arm.L"});leftLower=Bone(new[]{"LowerArm.L","LeftForeArm","mixamorig:LeftForeArm","forearm.L"});
            rightHand=mech?MechPalm(rightLower,".R"):Bone(new[]{"Fist.R","Hand.R","RightHand","mixamorig:RightHand","hand.R"});leftHand=mech?MechPalm(leftLower,".L"):Bone(new[]{"Fist.L","Hand.L","LeftHand","mixamorig:LeftHand","hand.L"});
            rightPalmMarker=ImportedModels.Find(body.transform,"PalmContact_R");leftPalmMarker=ImportedModels.Find(body.transform,"PalmContact_L");
            // Calibrate the authored closed hand in bind pose. Forward is the
            // palm-facing direction; the thumb side is opposite on the two hands.
            // A wrist bone origin is not a weapon contact point.
            if(rightPalmMarker){rightPalmLocal=rightHand.InverseTransformPoint(rightPalmMarker.position);rightPalmFrameInHand=Quaternion.Inverse(rightHand.rotation)*Quaternion.LookRotation(transform.forward,-transform.right);}
            if(leftPalmMarker){leftPalmLocal=leftHand.InverseTransformPoint(leftPalmMarker.position);leftPalmFrameInHand=Quaternion.Inverse(leftHand.rotation)*Quaternion.LookRotation(transform.forward,transform.right);}
            hipsRestInBody=body.transform.InverseTransformPoint(hips.position);
            authoredHeadHit=ImportedModels.Find(body.transform,"HeadHit");authoredChestHit=ImportedModels.Find(body.transform,"ChestHit");
            var unique=new HashSet<Transform>();foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>(true)){skin.updateWhenOffscreen=true;foreach(var bone in skin.bones)if(bone)unique.Add(bone);}
            bones=new BonePose[unique.Count];int at=0;foreach(var bone in unique)bones[at++]=new BonePose{bone=bone,restPosition=bone.localPosition,restRotation=bone.localRotation,fromPosition=bone.localPosition,fromRotation=bone.localRotation};
            graph=PlayableGraph.Create("Actor visuals / "+key);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);output=AnimationPlayableOutput.Create(graph,"Body",animator);graph.Play();
            idleClip=Clip("Idle","Idle_Holding","StandingIdle");if(!idleClip)throw new InvalidOperationException(ResourcePath+" requires an Idle clip");
            walkClip=Clip("Walk_Holding","Walk_Carry","Walk","Walking")??idleClip;
            hitClip=Clip("HitRecieve_1","RecieveHit","Hit","HitReact")??idleClip;shootClip=Clip("Shoot","Shoot_OneHanded","Shooting")??idleClip;
            pickupClip=Clip("Pickup","Throw","Punch")??idleClip;punchClip=Clip("Punch","Attack")??idleClip;kickClip=Clip("Kick")??idleClip;
            jumpClip=Clip("Jump")??idleClip;slashClip=Clip("SwordSlash","Punch","Attack")??idleClip;deathClip=Clip("Death","Die")??idleClip;
            CreateWeapons();ImportedModels.SetLayer(body.transform,displayOnly?gameObject.layer:Layers.Actor);
            if(shadowOnly)foreach(var renderer in body.GetComponentsInChildren<Renderer>(true))renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }
        Transform Bone(string[] names,bool required=true)
        {
            // Mesh object "Body" and skeleton bone "Body" are different nodes in the new packs.
            foreach(var name in names)foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))foreach(var node in skin.bones)if(node&&string.Equals(node.name,name,StringComparison.OrdinalIgnoreCase))return node;
            foreach(var name in names)foreach(var node in body.GetComponentsInChildren<Transform>(true))if(string.Equals(node.name,name,StringComparison.OrdinalIgnoreCase))return node;
            if(required)throw new InvalidOperationException(ResourcePath+" is missing required new-rig bone "+string.Join(" / ",names));return null;
        }
        Transform MechPalm(Transform forearm,string side)
        {
            Vector3 center=Vector3.zero;int count=0;
            foreach(Transform node in forearm)if(node.name.StartsWith("Palm",StringComparison.Ordinal)&&node.name.EndsWith(side,StringComparison.Ordinal)){center+=node.localPosition;count++;}
            if(count==0)throw new InvalidOperationException("New mech forearm has no palm roots: "+side);
            var socket=new GameObject("Mech palm socket "+side).transform;socket.SetParent(forearm,false);socket.localPosition=center/count;return socket;
        }
        void CreateWeapons()
        {
            var mount=new GameObject("New weapon mount");mount.transform.SetParent(transform,false);WeaponMount=mount.transform;
            weapon=null;Muzzle=supportGrip=blade=bladeGrip=null;if(shadowOnly)return;
            weapon=WeaponVisualFactory.Create(weaponId,WeaponMount,displayOnly?gameObject.layer:Layers.Actor);
            var grip=ImportedModels.Find(weapon,"Grip");if(!grip)throw new InvalidOperationException("New firearm has no Grip marker");
            Vector3 gripPoint=weapon.InverseTransformPoint(grip.position);weapon.localPosition=-gripPoint;
            Muzzle=ImportedModels.Find(weapon,"Muzzle");supportGrip=ImportedModels.Find(weapon,"SupportGrip");
            if(!Muzzle)throw new InvalidOperationException("New firearm has no Muzzle marker");
            if(!displayOnly&&owner&&(IsMech||owner.kind==EnemyKind.Shield))
            {
                blade=ActorEquipmentVisuals.CreateBlade(transform,"New actor phase blade");blade.localScale=Vector3.one*(IsMech?1.45f:1);
                bladeGrip=ImportedModels.Find(blade,"Grip");Vector3 p=blade.InverseTransformPoint(bladeGrip.position);blade.localPosition=-Vector3.Scale(p,blade.localScale);
            }
        }
        public void ConfigureVariant(int archetype,int chapterBoss=-1)
        {
            if(equipmentV2){equipmentV2.ReleaseAttachments();equipmentV2.gameObject.SetActive(false);Destroy(equipmentV2.gameObject);}
            variantV2=archetype;bossChapterV2=chapterBoss;
            var node=new GameObject("MapV2 enemy equipment / "+archetype+" / "+chapterBoss);node.layer=Layers.Actor;node.transform.SetParent(transform,false);node.transform.localScale=Vector3.one*(owner?owner.Height/1.8f:1);
            equipmentV2=node.AddComponent<EnemyV2VisualEquipment>();equipmentV2.Build(archetype,chapterBoss);equipmentV2.BindToSkeleton(this);
            if(body&&!equipmentV2.MechanicalBody)
            {
                Color role=EnemyV2VisualEquipment.RoleColor(archetype);var block=new MaterialPropertyBlock();
                foreach(var renderer in body.GetComponentsInChildren<Renderer>(true))
                {
                    var materials=renderer.sharedMaterials;
                    for(int i=0;i<materials.Length;i++)
                    {
                        if(!materials[i])continue;string label=materials[i].name.ToLowerInvariant();
                        if(!label.Contains("fabric")&&!label.Contains("weave")&&!label.Contains("ripstop")&&!label.Contains("armor")&&!label.Contains("ceramic")&&!label.Contains("composite")&&!label.Contains("identification"))continue;
                        Color color=label.Contains("identification")?role:label.Contains("fabric")||label.Contains("weave")||label.Contains("ripstop")?Color.Lerp(new Color(.035f,.046f,.058f),role,.10f):Color.Lerp(new Color(.12f,.16f,.20f),role,.28f);
                        renderer.GetPropertyBlock(block,i);block.SetColor("_BaseColor",color);renderer.SetPropertyBlock(block,i);block.Clear();
                    }
                }
            }
            if(body)foreach(var renderer in body.GetComponentsInChildren<Renderer>(true))renderer.enabled=!equipmentV2.MechanicalBody;
            if(weapon)weapon.gameObject.SetActive(archetype!=0&&archetype!=10);
        }
        public void SetAbilityPresentation(string key,float normalized,bool active)
        {if(equipmentV2)equipmentV2.Pose(key,normalized,active);grenadePreparing=active;}
        public Transform Weakpoint(string name)=>equipmentV2?equipmentV2.Weakpoint(name):chest;
        public void SetPickupBody(float crouchWeight){pickupBodyWeight=Mathf.Clamp01(crouchWeight);if(pickupBodyWeight<=0)pickupHandValid=false;}
        public void EvaluatePickupPose(){Evaluate(0);}
        public void SetPickupHand(Vector3 point){pickupHandWorld=point;pickupHandValid=true;}
        void ApplyPickupBody()
        {
            if(pickupBodyWeight<=0||IsMech)return;float w=pickupBodyWeight;
            hips.position-=transform.up*(.62f*w)+transform.forward*(.22f*w);chest.rotation=Quaternion.AngleAxis(70*w,transform.right)*chest.rotation;
            foreach(string side in new[]{"L","R"})
            {
                var thigh=Bone(new[]{"UpperLeg."+side},false);var calf=Bone(new[]{"LowerLeg."+side},false);var foot=Bone(new[]{"Foot."+side},false);
                if(thigh&&calf&&foot){Vector3 target=transform.TransformPoint(new Vector3(side=="R"?.11f:-.11f,.12f,0));SolveArm(thigh,calf,foot,target,transform.position+transform.forward*.7f+transform.up*.5f);}
            }
        }
        public void ConvertToBoss()
        {
            if(equipmentV2){equipmentV2.gameObject.SetActive(false);Destroy(equipmentV2.gameObject);equipmentV2=null;}variantV2=-1;
            if(!IsMech){weaponId=8;LoadBody("boss_george",true,owner.Height);Evaluate(0);CalibrateHitMarkers();}
            shieldMelee=false;if(shield)shield.gameObject.SetActive(false);
        }
        public void SetLocomotion(float actualSpeed,float weight,bool isStunned){speed=actualSpeed;gait=weight;stunned=isStunned;}
        public void SetAim(Vector3 point){aim=point;}
        public void SetGrenadePreparing(bool value){grenadePreparing=value;}
        public void NotifyShot(){shotLeft=.18f;shotRestart=true;}
        float headHitLeft;
        public void NotifyHit(bool headshot=false){hitLeft=headshot?.28f:.18f;if(headshot)headHitLeft=.28f;}
        public void BeginDeath(){if(dead)return;dead=true;deathVisualTime=0;}
        public void SetShieldMelee(TacticalState state,float seconds){shieldMelee=true;shieldState=state;shieldSeconds=seconds;if(weapon)weapon.gameObject.SetActive(false);if(blade)blade.gameObject.SetActive(true);}
        public void DisableShieldMelee(){shieldMelee=false;if(blade)blade.gameObject.SetActive(false);if(weapon)weapon.gameObject.SetActive(true);}
        public void SetBossPresentation(bool melee,float windup,float swing,Vector3 point){bossMelee=melee;bossWindup=windup;bossSwing=swing;aim=point;if(weapon)weapon.gameObject.SetActive(!melee&&variantV2!=0&&variantV2!=10);if(blade)blade.gameObject.SetActive(melee);}
        void LateUpdate()
        {
            if(displayOnly||(!owner&&!playerOwner)||!DemoGame.I||!DemoGame.I.IsPlaying)return;
            if(playerOwner)
            {
                int id=PlayerClassSkill.IsTacticalFlow?DemoGame.I.expeditionStore.ActiveClass:0;
                if(id!=displayClass){displayClass=id;LoadBody("class_"+id.ToString("00"),false,1.8f);Evaluate(0);CalibrateHitMarkers();}
                speed=playerOwner.CurrentSpeed;aim=playerOwner.Eye.position+playerOwner.Eye.forward*10;dead=playerOwner.health<=0;
                if(observedPlayerShots!=playerOwner.shots){observedPlayerShots=playerOwner.shots;NotifyShot();}
            }
            Evaluate(Time.deltaTime);
        }
        AnimationClip Clip(params string[] names)
        {
            foreach(string name in names)foreach(var clip in clips)
            {string n=clip.name;int split=n.LastIndexOf('|');if(split>=0)n=n.Substring(split+1);if(string.Equals(n,name,StringComparison.OrdinalIgnoreCase))return clip;}
            return null;
        }
        void Evaluate(float dt)
        {
            if(!body)return;hitLeft=Mathf.Max(0,hitLeft-dt);shotLeft=Mathf.Max(0,shotLeft-dt);
            var idle=idleClip;
            AnimationClip selected=idle;bool loop=true;string key="idle";float sample=-1;
            if(speed>.1f&&!stunned){selected=walkClip;key="walk";}
            if(grenadePreparing){selected=pickupClip;key="grenade prepare";loop=false;}
            if(hitLeft>0&&speed<.1f){selected=hitClip;key="hit";loop=false;}
            if(shotLeft>0&&speed<.1f){selected=shootClip;key="shoot";loop=false;}
            if(shieldMelee&&(shieldState==TacticalState.MeleeWarning||shieldState==TacticalState.Recover))
            {selected=slashClip;key="shield "+shieldState;loop=false;sample=shieldState==TacticalState.MeleeWarning?.05f+.68f*Mathf.Clamp01(1-shieldSeconds/.65f):.74f+.25f*Mathf.Clamp01(1-shieldSeconds/1.1f);}
            if(owner&&owner.boss)
            {
                var boss=owner.boss;
                if(boss.Skill!=lastBossSkill||boss.SkillStep!=lastBossStep){lastBossSkill=boss.Skill;lastBossStep=boss.SkillStep;bossMotionKey="boss "+lastBossSkill+" "+lastBossStep;if(lastBossStep==BossSkillStep.Telegraph)telegraphSeconds=Mathf.Max(.01f,boss.SkillSeconds);}
                bool tele=boss.SkillStep==BossSkillStep.Telegraph,execute=boss.SkillStep==BossSkillStep.Execute;
                if(tele||execute)
                {
                    key=bossMotionKey;loop=false;
                    switch(boss.Skill)
                    {
                        case BossSkill.SwapElbow:selected=punchClip;sample=tele?.05f+.3f*(1-boss.SkillSeconds/telegraphSeconds):.35f+.64f*bossSwing;break;
                        case BossSkill.GroundWave:selected=kickClip;sample=tele?.03f+.35f*(1-boss.SkillSeconds/telegraphSeconds):.38f+.61f*Mathf.Clamp01((2.7f-boss.SkillSeconds)/.4f);break;
                        case BossSkill.Attraction:selected=tele?pickupClip:shootClip;loop=execute;break;
                        case BossSkill.AdvantageSwap:selected=tele?jumpClip:idleClip;sample=tele?.08f+.3f*(1-boss.SkillSeconds/telegraphSeconds):-1;break;
                        case BossSkill.SweepLaser:case BossSkill.OmniFire:selected=shootClip;loop=true;break;
                    }
                }
                else if(bossMelee&&(bossWindup>0||bossSwing>0)){selected=punchClip;key="basic elbow";sample=bossWindup>0?.05f+.35f*bossWindup:.4f+.59f*(1-bossSwing);loop=false;}
            }
            if(dead){selected=deathClip;key="death";loop=false;sample=-1;}
            Play(selected,key,dt,loop,sample);
            LockVisualRoot();ApplyPickupBody();
            if(!dead)PoseHands();
            else
            {
                WeaponMount.position=rightHand.position;WeaponMount.rotation=rightHand.rotation*handWeaponOffset;
                if(blade&&blade.gameObject.activeSelf)FollowBlade(rightHand.rotation*handBladeOffset);
                deathVisualTime+=dt;
                if(shield&&shieldProxy)
                {
                    float drop=Mathf.SmoothStep(0,1,Mathf.Clamp01(deathVisualTime/.5f));
                    shield.localRotation=Quaternion.Slerp(Quaternion.identity,Quaternion.Euler(-90,0,12),drop);
                    shield.localPosition=Vector3.up*Mathf.Lerp(0,-shieldProxy.transform.localPosition.y+.12f,drop);
                }
            }
            if(head&&!dead){Vector3 local=transform.InverseTransformDirection(aim-head.position);float pitch=-Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg;head.rotation=Quaternion.AngleAxis(Mathf.Clamp(pitch,-15,15),transform.right)*head.rotation;}
            if(owner&&hitMarkersReady)owner.AlignVisualHitRegions(this);
            // Visual-only neck recoil is applied after the hit proxies were aligned.
            // The next animation evaluation restores bones before gameplay alignment.
            if(headHitLeft>0&&head&&!dead)
            {
                float age=.28f-headHitLeft;
                float weight=Mathf.Sin(Mathf.Clamp01(age/.28f)*Mathf.PI);
                head.localRotation*=Quaternion.Euler(-17*weight,0,7*weight);
                headHitLeft=Mathf.Max(0,headHitLeft-dt);
            }
            shotRestart=false;
        }
        void CalibrateHitMarkers()
        {
            if(authoredHeadHit&&authoredChestHit){headHitOffset=head.InverseTransformPoint(authoredHeadHit.position);torsoHitOffset=chest.InverseTransformPoint(authoredChestHit.position);hitMarkersReady=true;return;}
            // Centers measured from >.35 named-bone weights in normalized Idle export metadata.
            // Trigger sizes and damage multipliers stay in EnemyActor; only their centers follow bones.
            Vector3 headCenter,torsoCenter;
            if(IsMech){headCenter=new Vector3(-.01862f,1.65883f,-.29546f);torsoCenter=new Vector3(-.01874f,1.17082f,-.25644f);}
            else if(ResourcePath.EndsWith("class_02",StringComparison.Ordinal)||ResourcePath.EndsWith("enemy_sniper",StringComparison.Ordinal)){headCenter=new Vector3(-.00046f,1.42477f,.03188f);torsoCenter=new Vector3(-.00048f,.84403f,.04051f);}
            else if(ResourcePath.EndsWith("class_03",StringComparison.Ordinal)){headCenter=new Vector3(-.00036f,1.45961f,.08659f);torsoCenter=new Vector3(-.00046f,.79817f,.03373f);}
            else{headCenter=new Vector3(-.00061f,1.46859f,.04244f);torsoCenter=new Vector3(-.00046f,.81099f,.03998f);}
            headHitOffset=head.InverseTransformPoint(transform.TransformPoint(headCenter*bodyScale));
            torsoHitOffset=chest.InverseTransformPoint(transform.TransformPoint(torsoCenter*bodyScale));hitMarkersReady=true;
        }
        void Play(AnimationClip clip,string key,float dt,bool loop,float normalized)
        {
            bool initial=currentClip==null;bool changed=initial||currentClip!=clip||motionKey!=key||(shotRestart&&key=="shoot");
            if(changed)
            {
                for(int i=0;i<bones.Length;i++){var pose=bones[i];pose.fromPosition=pose.bone.localPosition;pose.fromRotation=pose.bone.localRotation;bones[i]=pose;}
                if(player.IsValid())graph.DestroyPlayable(player);player=AnimationClipPlayable.Create(graph,clip);player.SetApplyFootIK(false);player.SetApplyPlayableIK(false);output.SetSourcePlayable(player);
                currentClip=clip;motionKey=key;CurrentMotion=key+" / "+clip.name;clipClock=0;transitionClock=initial?.12f:0;
            }
            clipClock+=dt*(key=="walk"?Mathf.Clamp(speed/2.2f,.45f,1.65f):1);transitionClock+=dt;
            foreach(var pose in bones){pose.bone.localPosition=pose.restPosition;pose.bone.localRotation=pose.restRotation;}
            float length=Mathf.Max(.001f,clip.length);float time=normalized>=0?Mathf.Clamp01(normalized)*(length-.001f):loop?Mathf.Repeat(clipClock,length):Mathf.Min(clipClock,length-.001f);
            player.SetTime(time);player.SetSpeed(0);graph.Evaluate(0);
            float blend=displayOnly?1:Mathf.Clamp01(transitionClock/.12f);
            if(blend<1)foreach(var pose in bones){pose.bone.localPosition=Vector3.Lerp(pose.fromPosition,pose.bone.localPosition,blend);pose.bone.localRotation=Quaternion.Slerp(pose.fromRotation,pose.bone.localRotation,blend);}
        }
        void LockVisualRoot()
        {
            body.transform.localPosition=Vector3.zero;body.transform.localRotation=Quaternion.identity;
            // Remove horizontal animation travel below the Animator too; Q owns the actor root.
            Vector3 delta=body.transform.InverseTransformPoint(hips.position)-hipsRestInBody;delta.y=0;hips.position-=body.transform.TransformVector(delta);
        }
        void PoseHands()
        {
            float scale=owner?owner.Height/1.8f:1;bool melee=shieldMelee||owner&&owner.boss&&bossMelee;
            if(weapon)weapon.gameObject.SetActive(!melee&&variantV2!=0&&variantV2!=10);if(blade)blade.gameObject.SetActive(melee);
            Vector3 wrist=transform.TransformPoint((IsMech?new Vector3(.24f,1.18f,.42f):new Vector3(.08f,1.24f,.13f))*scale);
            if(shadowOnly&&pickupBodyWeight>0&&pickupHandValid)wrist=pickupHandWorld;
            if(melee)
            {
                float prep=owner&&owner.boss?bossWindup:shieldState==TacticalState.MeleeWarning?Mathf.Clamp01((1-shieldSeconds/.65f)/.7f):0;
                float strike=owner&&owner.boss?bossSwing:shieldState==TacticalState.MeleeWarning?Mathf.Clamp01((1-shieldSeconds/.65f-.7f)/.3f):shieldState==TacticalState.Recover?Mathf.Clamp01(shieldSeconds/1.1f):0;
                wrist=transform.TransformPoint(Vector3.Lerp(Vector3.Lerp(new Vector3(.28f,1.18f,.31f),new Vector3(.38f,1.48f,-.08f),prep),new Vector3(.13f,1.13f,.83f),strike)*scale);
                shieldPoseOffset=Mathf.Max(prep,strike)*55;
            }
            else{shieldPoseOffset=0;wrist-=transform.forward*(shotLeft/.18f*.045f);}
            bool firearmPalm=weapon&&!melee&&rightPalmMarker;
            if(firearmPalm)
            {
                // Keep the established firearm mount and authored muzzle/sight
                // geometry. Solve the wrist behind the real palm contact instead
                // of displacing the firearm to the wrist bone.
                Vector3 direction=aim-wrist;if(direction.sqrMagnitude<.01f)direction=transform.forward;
                WeaponMount.position=wrist;WeaponMount.rotation=Quaternion.LookRotation(direction,transform.up);
                Quaternion handRotation=WeaponMount.rotation*Quaternion.Inverse(rightPalmFrameInHand);
                SolvePalm(rightUpper,rightLower,rightHand,rightPalmLocal,WeaponMount.position,handRotation,transform.position+transform.right*.65f+transform.up*.95f);
            }
            else
            {
                SolveArm(rightUpper,rightLower,rightHand,wrist,transform.position+transform.right*.65f+transform.up*.95f);
                Vector3 direction=aim-rightHand.position;if(direction.sqrMagnitude<.01f)direction=transform.forward;
                WeaponMount.position=rightHand.position;WeaponMount.rotation=Quaternion.LookRotation(direction,transform.up);
            }
            handWeaponOffset=Quaternion.Inverse(rightHand.rotation)*WeaponMount.rotation;
            if(melee&&blade){FollowBlade(WeaponMount.rotation);handBladeOffset=Quaternion.Inverse(rightHand.rotation)*blade.rotation;}
            if(shield&&shield.gameObject.activeInHierarchy&&shieldGrip)SolveArm(leftUpper,leftLower,leftHand,shieldGrip.position,transform.position-transform.right*.7f+transform.up*.85f);
            else if(!melee&&supportGrip)
            {
                if(leftPalmMarker)
                {
                    // Offhand cups the fore-end: palm normal faces upward and
                    // slightly rearward; the thumb follows the barrel direction.
                    Quaternion supportFrame=supportGrip.rotation*Quaternion.FromToRotation(Vector3.up,new Vector3(0,.45f,1).normalized)*Quaternion.Euler(0,180,0);
                    SolvePalm(leftUpper,leftLower,leftHand,leftPalmLocal,supportGrip.position,supportFrame*Quaternion.Inverse(leftPalmFrameInHand),transform.position-transform.right*.65f+transform.up*.9f);
                }
                else SolveArm(leftUpper,leftLower,leftHand,supportGrip.position,transform.position-transform.right*.65f+transform.up*.9f);
            }
            else if(shadowOnly)SolveArm(leftUpper,leftLower,leftHand,transform.TransformPoint(new Vector3(-.12f,1.14f,.60f)),transform.position-transform.right*.65f+transform.up*.9f);
        }
        void FollowBlade(Quaternion rotation)
        {
            blade.rotation=rotation;Vector3 gripLocal=blade.InverseTransformPoint(bladeGrip.position);blade.position=rightHand.position-blade.TransformVector(gripLocal);
        }
        static void SolvePalm(Transform upper,Transform lower,Transform hand,Vector3 palmLocal,Vector3 contact,Quaternion rotation,Vector3 hint)
        {
            Vector3 wrist=contact-rotation*Vector3.Scale(palmLocal,hand.lossyScale);
            float length=Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,hand.position);
            Vector3 reach=wrist-upper.position;
            // A bounded shoulder reach avoids stretching bones or leaving a palm
            // floating on long rifle fore-ends. Animation restores this next frame.
            if(reach.magnitude>length-.002f)upper.position+=reach.normalized*Mathf.Min(.08f,reach.magnitude-length+.002f);
            SolveArm(upper,lower,hand,wrist,hint);hand.rotation=rotation;
        }
        static void SolveArm(Transform upper,Transform lower,Transform hand,Vector3 wanted,Vector3 hint)
        {
            Vector3 shoulder=upper.position;float a=Vector3.Distance(shoulder,lower.position),b=Vector3.Distance(lower.position,hand.position);
            if(a<.001f||b<.001f)return;Vector3 direction=wanted-shoulder;float distance=Mathf.Clamp(direction.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);direction=direction.normalized;
            Vector3 bend=Vector3.ProjectOnPlane(hint-shoulder,direction).normalized;if(bend.sqrMagnitude<.01f)bend=Vector3.Cross(direction,Vector3.up).normalized;
            float along=(a*a+distance*distance-b*b)/(2*distance);Vector3 elbow=shoulder+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;
            Vector3 handTarget=shoulder+direction*distance;lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,handTarget-lower.position)*lower.rotation;
        }
        void ReleaseAnimation(){if(graph.IsValid())graph.Destroy();currentClip=null;motionKey="";}
        void OnDestroy(){ReleaseAnimation();}
    }
}