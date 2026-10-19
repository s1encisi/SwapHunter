using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SwapHunter
{
    public sealed partial class PlayerMotor : MonoBehaviour
    {
        public CharacterController controller;
        public Transform Eye { get; private set; }
        public Camera cameraView, weaponCamera;
        public float health, verticalSpeed, yaw, pitch, cooldown, reloadLeft;
        public PlayerTactics tactics;
        public PlayerCombatTools combatTools;
        public PlayerClassSkill classSkill;
        public MeleeEquipmentV2 meleeEquipment;
        public float TemporaryArmor { get; private set; }
        public int weapon, shots, swapCount;
        public int[] ammo = new int[WeaponCatalog.Count], reserve = new int[WeaponCatalog.Count];
        SwapTarget pendingSwapTarget;
        DemoGame.SwapTelemetry pendingSwapTelemetry;
        int nextSwapTelemetryId;
        public SwapValidationResult swapValidation = new SwapValidationResult(SwapFailureReason.InvalidTarget);
        public string swapReason => swapValidation.ToDisplayText(G.config.swapRange);
        public SwapTarget target;
        public bool preparing;
        // The acceptance frame remains locked even if a zero-length/failed transaction finishes there.
        public bool SwapActionsLocked => preparing || swapAcceptedFrame == Time.frameCount;
        public bool ReloadSuspendedForSwap => preparing && reloadLeft > 0;
        public int ActionSequence => actionSequence;
        internal float NextShotTime => nextShot;
        internal float LastShotTime => lastShotAt;
        internal int PendingBurstRounds => burstRemaining;
        int swapAcceptedFrame = -1, actionSequence, fireSequence, fireSequenceWeapon = -1, burstRemaining;
        float nextBurstAt;
        bool swapHeldReload;
        public bool IsAiming { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsVaulting { get; private set; }
        public float ChargeProgress => charging ? Mathf.Clamp01((Time.time-chargeStarted)/CurrentWeaponDefinition.chargeTime) : 0;
        public int[] EquippedWeapons { get; private set; } = new[] { 0, 1 };
        public WeaponDefinition CurrentWeaponDefinition => WeaponCatalog.Get(weapon);
        public string CurrentWeaponName => CurrentWeaponDefinition.name;
        public Vector3 CurrentAdsLocalPosition=>adsPositions[weapon];
        public Vector3 CurrentAdsLocalEuler=>adsRotations[weapon].eulerAngles;
        public bool CurrentAdsCalibrated=>adsCalibrated[weapon];
        public string HitRegionLabel { get; private set; } = "";
        public string LastStandBlocker { get; private set; } = "";
        public bool AuthoredReloadGripValid=>authoredReloadRig&&magazines[weapon];
        public float ReloadGripWeight=>reloadGripWeight;
        public Vector3 ReloadGripTargetWorld=>reloadContactWorld;
        public Vector3 ReloadHandContactWorld=>reloadHand?reloadHand.TransformPoint(reloadPalmContact):Vector3.zero;
        public float ReloadGripError=>AuthoredReloadGripValid?Vector3.Distance(ReloadGripTargetWorld,ReloadHandContactWorld):-1;
        public float ReloadWristError=>authoredReloadRig?Vector3.Distance(reloadForearm.TransformPoint(authoredForearmWrist),reloadHand.TransformPoint(authoredHandWrist)):-1;
        public string ActionLabel => IsVaulting ? "攀爬" : charging ? "蓄能" : reloadLeft>0 ? "换弹" : IsSprinting ? "冲刺" : IsCrouching ? "蹲伏" : IsAiming ? "瞄准" : "就绪";
        public float hitFlash, hurtFlash, phaseFlash, killFlash, blockFlash;
        public bool AimingAtShield { get; private set; }
        public Vector3 lastThreat, lastSwapTarget;
        public float targetArrowTime;
        public bool Grounded => controller && controller.enabled && verticalSpeed <= .1f && Physics.Raycast(transform.position + Vector3.up * .14f, Vector3.down, .22f, Layers.WorldMask);
        public Vector3 HorizontalVelocity => horizontal;
        public float CurrentSpeed => horizontal.magnitude;
        public Vector2 MechanicalRecoil => recoilAngles;
        public int BurstCount => burstCount;
        public float ShotBloom => shotBloom;
        public float AimAlignmentError
        {
            get
            {
                var sight = ImportedModels.Find(weaponModels[weapon], "AimFront");
                if (!sight) return 1;
                Vector3 point = weaponCamera.WorldToViewportPoint(sight.position);
                return Vector2.Distance(new Vector2(point.x, point.y), Vector2.one * .5f);
            }
        }
        public float CurrentSpreadDegrees
        {
            get
            {
                float movement = Mathf.Clamp01(CurrentSpeed / Mathf.Max(.1f, G.config.walkSpeed * G.options.moveSpeedScale));
                float spread = EffectiveWeaponStats.Read(weapon,G.config).Spread + movement * (weapon != 0 && CurrentWeaponDefinition.Shotgun ? 1 : G.config.movingSpread) + shotBloom;
                if (!Grounded) spread += G.config.airborneSpread;
                if (IsAiming) spread *= .76f;
                return spread * G.ModuleSpread * G.ExpeditionSpreadMultiplier * (IsCrouching ? .80f : 1);
            }
        }
        Transform weaponRoot, rifleModel, shotgunModel, muzzle, viewArms, playerBody, bodyLeftLeg, bodyRightLeg;
        Transform reloadHand, reloadForearm;
        Vector3 handRest, forearmRest;
        Quaternion handRotation, forearmRotation;
        bool authoredReloadRig;float reloadGripWeight;
        Vector3 authoredForearmWrist,authoredForearmElbow,authoredHandWrist,authoredElbowParent;
        Transform[] weaponModels = new Transform[WeaponCatalog.Count], magazines = new Transform[WeaponCatalog.Count], bolts = new Transform[WeaponCatalog.Count];
        Vector3[] magazineOrigins = new Vector3[WeaponCatalog.Count], boltOrigins = new Vector3[WeaponCatalog.Count];
        GameObject[] flashes = new GameObject[WeaponCatalog.Count];
        readonly Vector3[] adsPositions=new Vector3[WeaponCatalog.Count], sightRearLocal=new Vector3[WeaponCatalog.Count], sightFrontLocal=new Vector3[WeaponCatalog.Count];
        readonly Quaternion[] adsRotations=new Quaternion[WeaponCatalog.Count];
        readonly bool[] adsCalibrated=new bool[WeaponCatalog.Count];
        Vector3 horizontal, weaponPosition;
        Vector2 recoilAngles, sway;
        float nextShot, visualKick, travel, stepDistance, lastShotAt = -100, shotBloom, reloadDuration, muzzleFlashUntil;
        int burstCount, ignoreLook;
        bool fireArmed = true, sprintLatched, reloadSoundA, reloadSoundB;
        Coroutine swapRoutine, vaultRoutine;
        bool charging, chargeRequiresHold, inputFire;
        float chargeStarted, cycleSoundAt;
        DemoGame G => DemoGame.I;
        static readonly float[] SidePattern = { 0, .08f, -.05f, .12f, .06f, -.14f, -.17f, .05f, .16f, .09f, -.11f, -.07f };

        public void Initialize()
        {
            gameObject.layer = Layers.Actor;
            controller = gameObject.AddComponent<CharacterController>(); controller.height = 1.8f; controller.radius = .32f;
            controller.center = new Vector3(0, .9f, 0); controller.stepOffset = .35f; controller.skinWidth = .035f; controller.slopeLimit = 48;
            GameObject eye = new GameObject("First person camera"); eye.transform.SetParent(transform, false); eye.transform.localPosition = Vector3.up * 1.62f;
            Eye = eye.transform; cameraView = eye.AddComponent<Camera>(); cameraView.tag = "MainCamera"; cameraView.nearClipPlane = .045f; cameraView.farClipPlane = 400;
            cameraView.backgroundColor = new Color(.16f, .24f, .31f); cameraView.clearFlags = CameraClearFlags.Skybox; cameraView.cullingMask &= ~(1 << Layers.ViewModel);
            eye.AddComponent<AudioListener>();
            var viewCameraObject = new GameObject("Equipment camera"); viewCameraObject.transform.SetParent(Eye, false);
            weaponCamera = viewCameraObject.AddComponent<Camera>(); weaponCamera.nearClipPlane = .01f; weaponCamera.farClipPlane = 3; weaponCamera.cullingMask = 1 << Layers.ViewModel;
            var overlay = weaponCamera.GetUniversalAdditionalCameraData(); overlay.renderType = CameraRenderType.Overlay;
            cameraView.GetUniversalAdditionalCameraData().cameraStack.Add(weaponCamera);
            weaponRoot = new GameObject("First person equipment").transform; weaponRoot.SetParent(Eye, false); weaponRoot.localPosition=new Vector3(.23f,-.28f,.53f);
            for(int i=0;i<WeaponCatalog.Count;i++) weaponModels[i]=WeaponVisualFactory.Create(i,weaponRoot,Layers.ViewModel);
            rifleModel=weaponModels[0]; shotgunModel=weaponModels[1];
            viewArms = WeaponVisualFactory.InstantiateVisual(Resources.Load<GameObject>(WeaponVisualCatalog.ArmsResourcePath),Eye,"New CC0 first person arms",Layers.ViewModel);
            // Shoulder frame stays with the camera/body; only wrists follow the moving gun.
            viewArms.localPosition=new Vector3(.23f,-.28f,.53f);
            foreach(var renderer in viewArms.GetComponentsInChildren<Renderer>(true))renderer.shadowCastingMode=ShadowCastingMode.Off;
            reloadHand = ImportedModels.Find(viewArms, "hand_L"); reloadForearm = ImportedModels.Find(viewArms, "forearm_L");
            if (reloadHand) { handRest = reloadHand.localPosition; handRotation = reloadHand.localRotation; }
            if (reloadForearm) { forearmRest = reloadForearm.localPosition; forearmRotation = reloadForearm.localRotation; }
            var authoredWrist=ImportedModels.Find(viewArms,"wrist_L");
            var authoredElbow=ImportedModels.Find(viewArms,"elbow_L");
            if(reloadHand&&reloadForearm&&authoredWrist&&authoredElbow)
            {
                authoredForearmWrist=reloadForearm.InverseTransformPoint(authoredWrist.position);
                authoredForearmElbow=reloadForearm.InverseTransformPoint(authoredElbow.position);
                authoredHandWrist=reloadHand.InverseTransformPoint(authoredWrist.position);
                authoredElbowParent=reloadForearm.parent.InverseTransformPoint(authoredElbow.position);
                authoredReloadRig=(authoredForearmWrist-authoredForearmElbow).sqrMagnitude>.001f;
            }
            InitializeCc0Hands();
            for (int i = 0; i < WeaponCatalog.Count; i++)
            {
                Transform root = weaponModels[i];
                magazines[i] = ImportedModels.Find(root, "magazine"); bolts[i] = ImportedModels.Find(root, "charging_handle");
                if (magazines[i]) magazineOrigins[i] = magazines[i].localPosition;
                if (bolts[i]) boltOrigins[i] = bolts[i].localPosition;
                var marker = ImportedModels.Find(root, "Muzzle");
                flashes[i] = CreateMuzzleFlash(marker);
                flashes[i].SetActive(false);
                CacheAdsPose(i);
            }
            InitializeWeaponPresentation();
            ImportedModels.SetLayer(weaponRoot, Layers.ViewModel);
            foreach (Renderer renderer in weaponRoot.GetComponentsInChildren<Renderer>(true)) renderer.shadowCastingMode = ShadowCastingMode.Off;
            playerBody = ClassVisualFactory.CreatePlayerShadowBody(0,transform).transform;
            bodyLeftLeg=bodyRightLeg=null; // New skinned world body is animated by its visual rig.
            classSkill = gameObject.AddComponent<PlayerClassSkill>(); classSkill.Initialize(this);
            ApplyViewSettings(); ResetAt(new Vector3(0, .06f, 2));
            gameObject.AddComponent<SwapEndpointPreview>().Initialize(this);
        }
        void CacheAdsPose(int id)
        {
            adsPositions[id]=new Vector3(0,-.1723f,.53f);adsRotations[id]=Quaternion.Euler(3.8f,0,0);
            Transform rear=ImportedModels.Find(weaponModels[id],"AimRear"),front=ImportedModels.Find(weaponModels[id],"AimFront");
            if(!rear||!front)return;
            // Read the authored sight geometry once, in the shared weapon-root coordinate frame.
            // No sight marker or mesh is moved: ADS transforms the complete weapon/hand rig.
            sightRearLocal[id]=weaponRoot.InverseTransformPoint(rear.position);
            sightFrontLocal[id]=weaponRoot.InverseTransformPoint(front.position);
            Vector3 axis=sightFrontLocal[id]-sightRearLocal[id];
            if(axis.sqrMagnitude<.0001f)return;
            Quaternion cameraBasis=Quaternion.Inverse(weaponRoot.parent.rotation)*weaponCamera.transform.rotation;
            Vector3 cameraOrigin=weaponRoot.parent.InverseTransformPoint(weaponCamera.transform.position);
            adsRotations[id]=cameraBasis*Quaternion.Inverse(Quaternion.LookRotation(axis.normalized,Vector3.up));
            Vector3 rotatedRear=adsRotations[id]*sightRearLocal[id];
            Vector3 forward=cameraBasis*Vector3.forward;
            // Preserve the established .53m root standoff instead of pulling the optic into the near plane.
            float rearDepth=Mathf.Max(.32f,.53f+Vector3.Dot(rotatedRear,forward));
            adsPositions[id]=cameraOrigin+forward*rearDepth-rotatedRear;
            adsCalibrated[id]=true;
        }
        public void ApplyViewSettings() { if (cameraView) cameraView.fieldOfView = G.fov; if (weaponCamera) weaponCamera.fieldOfView = G.options.viewModelFov; }
        public void SetViewActive(bool active) { cameraView.enabled = active; weaponCamera.enabled = active; if (playerBody) playerBody.gameObject.SetActive(active); }
        public void ResetAt(Vector3 feet)
        {
            StopV2Travel();ResetAllReloadV2();CancelConsumableV2();if(meleeEquipment)meleeEquipment.ResetForAttempt();if(tactics)tactics.ResetCombat();if(combatTools)combatTools.ResetCombat();if(classSkill)classSkill.ResetCombat();
            ClearArmor(); CancelActions(); IsCrouching=false; controller.height=1.8f; controller.center=Vector3.up*.9f;
            Eye.localPosition=Vector3.up*1.62f; SetFeet(feet); health = G.config.playerHealth;
            EquippedWeapons=new[]{0,1};
            for(int i=0;i<WeaponCatalog.Count;i++){ammo[i]=Capacity(i);reserve[i]=EffectiveWeaponStats.Read(i,G.config).Reserve;}
            cooldown = 0; verticalSpeed = 0; horizontal = Vector3.zero; weapon = 0; nextShot = 0; visualKick = 0; travel = stepDistance = 0;
            recoilAngles = Vector2.zero; shotBloom = 0; burstCount = 0; lastShotAt = -100; targetArrowTime = 0; target = null;
            // Classic checkpoint reserves retain their original config override; expedition SetLoadout uses effective base reserves.
            reserve[0] = G.config.rifleReserve; reserve[1] = G.config.shotgunReserve;
            hitFlash = hurtFlash = phaseFlash = killFlash = blockFlash = 0; AimingAtShield = false; HitRegionLabel=""; for(int i=0;i<WeaponCatalog.Count;i++)weaponModels[i].gameObject.SetActive(i==0);
            muzzle = ImportedModels.Find(rifleModel, "Muzzle"); weaponPosition = new Vector3(.23f, -.28f, .53f); SetLook(0, 0);
        }
        public void CancelActions()
        {
            if (swapRoutine != null) { RecordSwap("swap_cancel", pendingSwapTarget, new SwapValidationResult(SwapFailureReason.Canceled)); StopCoroutine(swapRoutine); }
            pendingSwapTarget = null; pendingSwapTelemetry = null;
            if(vaultRoutine!=null)StopCoroutine(vaultRoutine);vaultRoutine=null;IsVaulting=false;IsSprinting=false;
            CancelFireSequence();
            swapRoutine = null; pendingSwapTarget = null; preparing = false; swapAcceptedFrame = -1; swapHeldReload = false; SuspendReloadV2(); ResetReloadPresentation(); fireArmed = false; ignoreLook = 2; IsAiming = false; sprintLatched = false; horizontal = Vector3.zero;
        }
        public void SetFeet(Vector3 feet) { controller.enabled = false; transform.position = feet; controller.enabled = true; Physics.SyncTransforms(); }
        public bool Launch(float speed=18)
        {
            if(!G.IsPlaying||!Grounded||IsVaulting||health<=0)return false;
            verticalSpeed=Mathf.Clamp(speed,10,22);G.sound.Play("vault");G.Record("launch",verticalSpeed.ToString("F1"));return true;
        }
        public void SetLook(float newYaw, float newPitch) { yaw = newYaw; pitch = Mathf.Clamp(newPitch, -85, 85); SetEyeRotation(); }
        void SetEyeRotation()
        {
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            Eye.localRotation = Quaternion.Euler(Mathf.Clamp(pitch - recoilAngles.x - visualKick * G.shake * .15f, -88, 88), recoilAngles.y, 0);
        }
        void Update()
        {
            if(classSkill)classSkill.Tick(Time.deltaTime);
            if (!G.IsPlaying || health<=0) return;
            float dt = Time.deltaTime;TickV2Travel(dt); float oldCooldown = cooldown; cooldown = Mathf.Max(0, cooldown - dt);
            if (oldCooldown > 0 && cooldown == 0) G.sound.Play("confirm");
            blockFlash = Mathf.Max(0, blockFlash - dt); hitFlash = Mathf.Max(0, hitFlash - dt); killFlash = Mathf.Max(0, killFlash - dt); hurtFlash = Mathf.Max(0, hurtFlash - dt); phaseFlash = Mathf.Max(0, phaseFlash - dt); targetArrowTime = Mathf.Max(0, targetArrowTime - dt);
            if (Time.time - lastShotAt > .12f)
            {
                float recoveryStep = Mathf.Min(dt, Time.time - lastShotAt - .12f);
                shotBloom = Mathf.MoveTowards(shotBloom, 0, recoveryStep * (burstCount <= 3 ? G.config.shortRecovery : G.config.longRecovery));
                recoilAngles = Vector2.MoveTowards(recoilAngles, Vector2.zero, recoveryStep * (burstCount <= 3 ? 5 : 3.4f));
            }
            visualKick = Mathf.Lerp(visualKick, 0, 1 - Mathf.Exp(-20 * dt));
            if(cycleSoundAt>0 && Time.time>=cycleSoundAt){cycleSoundAt=0;G.sound.Play("reload_bolt");}
            if(charging && chargeRequiresHold && !G.Held(12))CancelFireSequence();
            Eye.localPosition=Vector3.Lerp(Eye.localPosition,Vector3.up*(IsCrouching?.98f:1.62f),1-Mathf.Exp(-18*dt));
            AdvanceFireSequence();
            if(IsVaulting){if(classSkill)classSkill.ProcessInput();UpdateEquipment(Vector2.zero,false,dt);return;}
            TickReloadV2(dt);
            Mouse mouse = G.qaMode && !G.qaInputEnabled ? null : G.MouseDevice;
            Vector2 delta = mouse == null ? Vector2.zero : mouse.delta.ReadValue();
            if (ignoreLook > 0) ignoreLook--;
            else
            {
                Vector2 turn = delta * (G.sensitivity * .04f * (IsAiming ? G.options.aimSensitivity : 1));
                yaw += turn.x; pitch = Mathf.Clamp(pitch + turn.y * G.options.verticalSensitivity * (G.invertY ? 1 : -1), -85, 85);
            }
            // Already-due events above commit before Q; new input below observes the shared lock.
            SetEyeRotation(); target = FindTarget(); swapValidation = ValidateSwap(target);
            if (G.Pressed(6)) RequestSwap(target);
            if(classSkill)classSkill.ProcessInput();
            if (SwapActionsLocked && G.Held(12)) fireArmed = false;
            else if (!G.Held(12)) fireArmed = true;
            if (G.options.toggleAim) { if (G.Pressed(13)) IsAiming = !IsAiming; } else IsAiming = G.Held(13);
            if (reloadLeft > 0 || preparing) IsAiming = false;
            if(G.options.toggleCrouch){if(G.Pressed(17))TrySetCrouch(!IsCrouching);}else TrySetCrouch(G.Held(17));
            if (G.options.toggleSprint) { if (G.Pressed(5)) sprintLatched = !sprintLatched; } else sprintLatched = G.Held(5);
            Vector2 movement = Vector2.ClampMagnitude(new Vector2(G.Held(3) ? 1 : 0, G.Held(0) ? 1 : 0) - new Vector2(G.Held(2) ? 1 : 0, G.Held(1) ? 1 : 0), 1);
            bool shooting = G.Held(12) && fireArmed;
            bool sprint = sprintLatched && movement.y > .1f && !shooting && !IsAiming && !IsCrouching && reloadLeft<=0 && !charging && !preparing; IsSprinting=sprint;
            if (shooting || movement.y <= 0) sprintLatched = false;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            bool grounded = Grounded; float fallSpeed = verticalSpeed;
            float speed = (sprint ? G.config.sprintSpeed : G.Held(9) ? G.config.slowSpeed : G.config.walkSpeed) * G.options.moveSpeedScale;
            speed *= EnemyV2Tether.PlayerMoveMultiplier;
            speed *= G.ModuleMovement * G.ModuleAimMovement * G.ExpeditionMoveMultiplier * EffectiveWeaponStats.Read(weapon,G.config).Movement * (IsCrouching?.52f:1);
            if(grounded && classSkill && classSkill.MobilityActive) speed *= PlayerClassSkill.GroundSpeedMultiplier;
            Vector3 desired = (transform.forward * movement.y + transform.right * movement.x) * speed;
            float rate = grounded ? (movement.sqrMagnitude < .01f ? G.config.braking : Vector3.Dot(horizontal, desired) < -.1f ? G.config.counterBraking : G.config.acceleration) : G.config.airAcceleration;
            horizontal = TacticalAdvanceActive||JumpTransportActive?V2DesiredVelocity(desired):Vector3.MoveTowards(horizontal, desired, rate * dt);
            if (grounded && verticalSpeed < 0) verticalSpeed = -2;
            if(G.Pressed(4)&&grounded)
            {
                if(V2JumpPad.TryLaunch(this)){}
                else if(TryVault())return;
                else if(!IsCrouching||TrySetCrouch(false))verticalSpeed=Mathf.Sqrt(G.config.jumpHeight*2*22);
            }
            verticalSpeed -= 22 * dt; Vector3 beforeMove = transform.position;
            CollisionFlags collision = controller.Move((horizontal + Vector3.up * verticalSpeed) * dt);
            if ((collision & CollisionFlags.Above) != 0 && verticalSpeed > 0) verticalSpeed = 0;
            if ((collision & CollisionFlags.Sides) != 0 && dt > 0) horizontal = Vector3.ProjectOnPlane(transform.position - beforeMove, Vector3.up) / dt;
            if (!grounded && Grounded && fallSpeed < -4) { visualKick += .2f; G.sound.Play("land"); }
            float distance = horizontal.magnitude * dt; travel += distance; stepDistance += grounded ? distance : 0;
            if (travel > 3) G.tutorialMoved = true;
            if (stepDistance > (sprint ? 1.75f : 1.4f)) { stepDistance = 0; G.sound.Play("step"); }
            if (transform.position.y < -12) Hurt(1000, transform.position, shieldBlockable:false, kind:PlayerDamageKind.Environment);
            if (G.Pressed(10)) SwitchWeapon(EquippedWeapons[0]); if (G.Pressed(11)) SwitchWeapon(EquippedWeapons[1]);
            if (!V2ItemsSelected && (G.Pressed(14) || G.Pressed(15))) SwitchWeapon(weapon==EquippedWeapons[0]?EquippedWeapons[1]:EquippedWeapons[0]);
            if (G.Pressed(7)) Reload();
            SetEyeRotation(); if (shooting && (CurrentWeaponDefinition.Automatic || G.Pressed(12))) { inputFire=true; Fire(); inputFire=false; } SetEyeRotation();
            target = FindTarget(); swapValidation = ValidateSwap(target);
            if (G.Pressed(8) && !SwapActionsLocked) G.Interact();
            cameraView.fieldOfView = Mathf.Lerp(cameraView.fieldOfView, IsAiming ? G.fov * CurrentWeaponDefinition.aimZoom : G.fov, 1 - Mathf.Exp(-14 * dt));
            UpdateEquipment(delta, grounded, dt);
        }
        void UpdateEquipment(Vector2 mouseDelta, bool grounded, float dt)
        {
            float motion = G.options.weaponMotion;
            float moving = Mathf.Clamp01(CurrentSpeed / G.config.walkSpeed) * (grounded ? 1 : .2f);
            sway = Vector2.Lerp(sway, Vector2.ClampMagnitude(mouseDelta * .002f, .025f), 1 - Mathf.Exp(-14 * dt));
            Vector3 targetPosition = IsAiming ? adsPositions[weapon] : new Vector3(.23f,-.28f,.41f);
            targetPosition += new Vector3(-sway.x, -sway.y + Mathf.Sin(travel * 3) * .007f * moving, 0) * motion;
            targetPosition.z -= visualKick * .026f;
            if(combatTools&&combatTools.PoseActive)targetPosition+=new Vector3(.08f,-.16f,-.08f)*Mathf.Sin(combatTools.PoseProgress*Mathf.PI);
            if (Physics.Raycast(Eye.position, Eye.forward, out var close, .85f, Layers.WorldMask)) { float t = 1 - close.distance / .85f; targetPosition += new Vector3(0,-.10f,-.17f) * t; }
            targetPosition += ReloadPoseOffset;
            weaponPosition = Vector3.Lerp(weaponPosition, targetPosition, 1 - Mathf.Exp(-18 * dt)); weaponRoot.localPosition = weaponPosition;
            Quaternion rotation = (IsAiming ? adsRotations[weapon] : Quaternion.identity) *
                Quaternion.Euler(visualKick * 3,0,sway.x * 40 * motion) * ReloadPoseRotation;
            weaponRoot.localRotation = Quaternion.Slerp(weaponRoot.localRotation, rotation, 1 - Mathf.Exp(-20 * dt));
            for(int i=0;i<WeaponCatalog.Count;i++)flashes[i].SetActive(i==weapon&&Time.time<muzzleFlashUntil);
            UpdateWeaponPresentation();
            UpdateToolPose();
            if (bodyLeftLeg) bodyLeftLeg.localRotation = Quaternion.Euler(Mathf.Sin(travel * 2.5f) * moving * 25, 0, 0);
            if (bodyRightLeg) bodyRightLeg.localRotation = Quaternion.Euler(-Mathf.Sin(travel * 2.5f) * moving * 25, 0, 0);
        }
        public Vector3 ToolGripWorld=>reloadHand?reloadHand.TransformPoint(reloadPalmContact):Eye.position+Eye.forward*.5f;
        public Quaternion ToolGripRotation {get;private set;}
        void UpdateToolPose()
        {
            if(!combatTools||!combatTools.PoseActive||!reloadHand||!authoredReloadRig)return;
            float p=combatTools.PoseProgress;
            float sweep=Mathf.Sin(Mathf.Clamp01(p/.75f)*Mathf.PI);
            Vector3 grip=Eye.TransformPoint(new Vector3(Mathf.Lerp(-.28f,.04f,sweep),-.19f+Mathf.Sin(p*Mathf.PI)*.08f,.48f+sweep*.23f));
            ToolGripRotation=Eye.rotation*Quaternion.Euler(-12,Mathf.Lerp(-38,45,sweep),25);
            PoseCc0ToolHand(grip,ToolGripRotation);
        }
        public int Capacity(int slot) => EffectiveWeaponStats.Read(slot,G.config).Magazine;
        float Rate => EffectiveWeaponStats.Read(weapon,G.config).Rate;
        public void SetLoadout(int primary,int secondary)
        {
            if(!WeaponCatalog.Valid(primary)||!WeaponCatalog.Valid(secondary)||primary==secondary)
                throw new ArgumentException("Loadout requires two different valid weapon IDs.");
            CancelActions();ResetAllReloadV2(); EquippedWeapons=new[]{primary,secondary};
            foreach(int id in EquippedWeapons){ammo[id]=Capacity(id);reserve[id]=EffectiveWeaponStats.Read(id,G.config).Reserve;}
            weapon=primary;ShowWeapon();nextShot=Time.time+.2f;
        }
        void ShowWeapon()
        {
            for(int i=0;i<WeaponCatalog.Count;i++) if(weaponModels[i])weaponModels[i].gameObject.SetActive(i==weapon);
            muzzle=ImportedModels.Find(weaponModels[weapon],"Muzzle");
        }
        void CancelFireSequence()
        {
            actionSequence++;fireSequence=-1;fireSequenceWeapon=-1;burstRemaining=0;
            charging=false;chargeRequiresHold=false;cycleSoundAt=0;
        }
        public void SwitchWeapon(int slot)
        {
            if(!WeaponCatalog.Valid(slot)||Array.IndexOf(EquippedWeapons,slot)<0||IsVaulting||SwapActionsLocked)return;
            bool changed=slot!=weapon||V2ItemsSelected||meleeEquipment&&meleeEquipment.Equipped;
            if(!changed)return;if(meleeEquipment)meleeEquipment.Unequip();CancelConsumableV2();
            CancelFireSequence();SuspendReloadV2();ResetReloadPresentation();IsAiming=false;sprintLatched=IsSprinting=false;
            weapon=slot;ShowWeapon();ResumeReloadV2();nextShot=Mathf.Max(nextShot,Time.time+WeaponMotionV2.Get(weapon).readySeconds);
            G.sound.Play("reload");
        }
        public bool Reload(){if(meleeEquipment&&meleeEquipment.Equipped)return false;CancelConsumableV2();return BeginReloadV2();}
        public bool AccelerateCurrentReload(float remainingMultiplier)=>AccelerateReloadV2(remainingMultiplier);
        Vector3 TracerOrigin()
        {
            Vector3 uv=weaponCamera.WorldToViewportPoint(muzzle.position);
            return cameraView.ViewportToWorldPoint(new Vector3(uv.x,uv.y,.8f));
        }
        public bool Fire()
        {
            if(!G.IsPlaying||health<=0||meleeEquipment&&meleeEquipment.Equipped||TacticalAdvanceActive)return false;
            if(V2UsingConsumable)CancelConsumableV2();
            if(!EnsureReloadV2Ready())return false;
            if(!G.IsPlaying||health<=0||Time.time<nextShot||reloadLeft>0||IsVaulting||SwapActionsLocked||charging||burstRemaining>0||combatTools&&combatTools.PoseActive)return false;
            if(ammo[weapon]<=0){if(!Reload()){nextShot=Time.time+.3f;G.sound.Play("empty");}return false;}
            sprintLatched=IsSprinting=false;
            if(CurrentWeaponDefinition.mechanism==FireMechanism.Burst)
            {
                fireSequence=++actionSequence;fireSequenceWeapon=weapon;burstRemaining=2;
                ShootRound();nextBurstAt=Time.time+1/EffectiveWeaponStats.Read(weapon,G.config).Rate;return true;
            }
            if(CurrentWeaponDefinition.mechanism==FireMechanism.Charge)
            {
                fireSequence=++actionSequence;fireSequenceWeapon=weapon;
                chargeRequiresHold=inputFire;charging=true;chargeStarted=Time.time;G.sound.Play("phase");return true;
            }
            return ShootRound();
        }
        // One due round per frame, as before; no catch-up burst or altered rate under a slow frame.
        // Owning the schedule here ensures due events precede this frame's Q arbitration.
        void AdvanceFireSequence()
        {
            if(!charging && burstRemaining<=0)return;
            if(fireSequence!=actionSequence || weapon!=fireSequenceWeapon || reloadLeft>0 || preparing || IsVaulting || combatTools&&combatTools.PoseActive)
            {
                bool wasBurst=burstRemaining>0;CancelFireSequence();
                if(wasBurst)nextShot=Mathf.Max(nextShot,Time.time+.28f);
                return;
            }
            if(charging)
            {
                if(Time.time-chargeStarted<CurrentWeaponDefinition.chargeTime)return;
                charging=false;chargeRequiresHold=false;fireSequence=-1;fireSequenceWeapon=-1;ShootRound();return;
            }
            if(Time.time<nextBurstAt)return;
            if(ammo[weapon]>0){ShootRound();burstRemaining--;}
            else burstRemaining=0;
            if(burstRemaining>0)nextBurstAt=Time.time+1/EffectiveWeaponStats.Read(weapon,G.config).Rate;
            else {nextShot=Mathf.Max(nextShot,Time.time+.28f);fireSequence=-1;fireSequenceWeapon=-1;}
        }
        bool ShootRound()
        {
            if(ammo[weapon]<=0||combatTools&&combatTools.PoseActive)return false;
            var def=CurrentWeaponDefinition;
            if(Time.time-lastShotAt>.5f)burstCount=0;
            ammo[weapon]--;shots++;G.totalShots++;
            if(classSkill)classSkill.EndForAttack("fire");
            float abilityMultiplier=classSkill?classSkill.ConsumePrecisionShot():1;
            float interval=1/Mathf.Max(.1f,Rate);nextShot=Time.time+interval;
            G.sound.Play(def.sound);if(def.accent.Length>0)G.sound.Play(def.accent);
            if(def.mechanism==FireMechanism.Pump||def.mechanism==FireMechanism.BoltAction)cycleSoundAt=Time.time+interval*.48f;
            float spreadAngle=CurrentSpreadDegrees;
            int count=EffectiveWeaponStats.Read(weapon,G.config).Pellets;
            bool anyDamage=false,anyKill=false,anyShield=false,anySurface=false;int impactCount=0;Vector3 surfacePoint=Eye.position;
            string region="";
            if(def.mechanism==FireMechanism.Grenade)
                PlayerOrdnance.Launch(this,Eye.position,Eye.forward,EffectiveWeaponStats.Read(weapon,G.config).Damage*G.ExpeditionDamageMultiplier*G.ModuleShotDamage(0)*abilityMultiplier);
            else
            {
                var batch=new DamageBatch(DamageSource.PlayerBullet,transform,Eye.position);
                for(int i=0;i<count;i++)
                {
                    Vector2 spread=UnityEngine.Random.insideUnitCircle*Mathf.Tan(spreadAngle*Mathf.Deg2Rad);
                    Vector3 direction=(Eye.forward+Eye.right*spread.x+Eye.up*spread.y).normalized;
                    Vector3 endpoint;
                    bool found=V2WeaponRay(direction,out var hit,out endpoint,out var actualRay,out bool shieldBlocked);
                    if(shieldBlocked){anyShield=true;Shapes.Trace(TracerOrigin(),endpoint,def.tracer,.04f,.01f);continue;}
                    if(found)
                    {
                        endpoint=hit.point;Collider collider=hit.collider;
                        var device=collider.GetComponentInParent<RelayDevice>();
                        var enemy=device||collider.GetComponentInParent<EnemyV2Device>()?null:collider.GetComponentInParent<EnemyActor>();
                        if(enemy)collider=enemy.ResolveHitCollider(actualRay,collider,ref endpoint);
                        var sample=batch.Direct(collider,ShotDamage(def,hit.distance,abilityMultiplier),endpoint,batch.NewEvent());
                        if(sample!=null)
                        {
                            var hitEnemy=enemy;
                            sample.after=result=>{
                                anyDamage|=result.applied;anyKill|=result.killed;anyShield|=result.blocked;
                                if(result.applied)
                                {
                                    G.totalHits++;
                                    if(hitEnemy)
                                    {
                                        if(region!="头部")region=hitEnemy.LastHitRegionLabel;
                                        if(def.mechanism==FireMechanism.PhaseMark&&hitEnemy.Alive){hitEnemy.MarkPhase(5);region="相位标记";}
                                    }
                                    else region=result.receiverKind=="device"?"中继设备":"任务装置";
                                }
                            };
                            if(enemy)G.tutorialShot=true;
                        }
                        else {anySurface=true;surfacePoint=hit.point;}
                        if(impactCount++<3)Shapes.Impact(endpoint,hit.normal,enemy&&enemy.IsShieldCollider(collider));
                    }
                    if(def.Shotgun||weapon>=6||shots%2==1)Shapes.Trace(TracerOrigin(),endpoint,def.tracer,weapon==10?.07f:.023f,weapon==10?.012f:.0035f);
                }
                // All pellet rays have captured their original receivers before a carrier can die.
                batch.Apply();
            }
            if(anyDamage)ReportWeaponHit(anyKill,region);
            else if(anyShield){HitRegionLabel="盾牌";hitFlash=killFlash=0;blockFlash=.6f;G.sound.Play("shield");G.Record("shot_blocked","shield");}
            else if(anySurface){HitRegionLabel="环境";G.sound.Play("surface",surfacePoint);}
            G.ConsumeModuleShot();burstCount++;lastShotAt=Time.time;muzzleFlashUntil=Time.time+.022f;
            if(weapon==0)
            {
                recoilAngles.x=Mathf.Min(6,recoilAngles.x+.42f+Mathf.Min(burstCount,8)*.045f);
                recoilAngles.y=Mathf.Clamp(recoilAngles.y+SidePattern[(burstCount-1)%SidePattern.Length],-2,2);
                shotBloom=Mathf.Min(1.3f,shotBloom+G.config.shotSpreadGrowth);
            }
            else
            {
                recoilAngles.x=Mathf.Min(9,recoilAngles.x+def.recoil*(IsCrouching?.8f:1));
                recoilAngles.y=Mathf.Clamp(recoilAngles.y+SidePattern[(burstCount-1)%SidePattern.Length]*def.recoil,-3,3);
                shotBloom=Mathf.Min(weapon==8?2.8f:1.5f,shotBloom+(weapon==8?.19f:def.Shotgun?.2f:.075f));
            }
            visualKick=def.recoil;return true;
        }
        float ShotDamage(WeaponDefinition def,float distance,float abilityMultiplier=1)
        {
            float damage=EffectiveWeaponStats.Read(weapon,G.config).Damage;
            float falloff=def.Shotgun?Mathf.Lerp(1,.3f,Mathf.InverseLerp(6,16,distance)):
                weapon==4?Mathf.Lerp(1,.35f,Mathf.InverseLerp(12,32,distance)):
                weapon==6||weapon==7||weapon==10?1:Mathf.Lerp(1,.6f,Mathf.InverseLerp(20,45,distance));
            return damage*falloff*G.ModuleShotDamage(distance)*G.ExpeditionDamageMultiplier*abilityMultiplier;
        }
        public void ReportWeaponHit(bool killed,string region)
        {
            HitRegionLabel=region;blockFlash=0;
            if(killed){killFlash=hitFlash=.25f;G.sound.Play("kill");}
            else{hitFlash=.14f;G.sound.Play(region=="头部"?"head_hit":region=="四肢"?"limb_hit":"impact");}
        }
        public bool TrySetCrouch(bool crouch)
        {
            if(IsVaulting||preparing)return false;
            if(crouch==IsCrouching)return true;
            if(!crouch)
            {
                // Moving/created ceilings must be visible to this same-frame stand-up query.
                Physics.SyncTransforms();
                if(!StandingRoom(transform.position))return false;
            }
            IsCrouching=crouch;G.sound.Play("crouch");controller.height=crouch?1.12f:1.8f;controller.center=Vector3.up*(controller.height*.5f);
            if(crouch)sprintLatched=IsSprinting=false;
            Physics.SyncTransforms();return true;
        }
        bool StandingRoom(Vector3 feet)
        {
            LastStandBlocker="";
            foreach(var c in Physics.OverlapCapsule(feet+Vector3.up*.37f,feet+Vector3.up*1.46f,.285f,Layers.CombatMask,QueryTriggerInteraction.Ignore))
                if(c.transform!=transform&&!c.transform.IsChildOf(transform)){LastStandBlocker=c.name;return false;}
            return true;
        }
        public bool TryVault()
        {
            if(!G.IsPlaying||!Grounded||IsVaulting||preparing||charging)return false;
            Vector3 forward=transform.forward;
            if(!Physics.Raycast(transform.position+Vector3.up*.65f,forward,out var wall,.9f,Layers.WorldMask)||Mathf.Abs(wall.normal.y)>.35f)return false;
            Vector3 probe=wall.point+forward*.60f;probe.y=transform.position.y+1.65f;
            if(!Physics.Raycast(probe,Vector3.down,out var top,1.2f,Layers.WorldMask)||top.normal.y<.7f)return false;
            float rise=top.point.y-transform.position.y;
            Vector3 landing=top.point+Vector3.up*.045f;
            if(rise<.38f||rise>1.35f||!StandingRoom(landing))return false;
            Vector3 raised=transform.position+Vector3.up*(rise+.12f);
            for(int i=1;i<=6;i++)if(!StandingRoom(Vector3.Lerp(raised,landing+Vector3.up*.1f,i/6f)))return false;
            if(IsCrouching&&!TrySetCrouch(false))return false;
            CancelFireSequence();SuspendReloadV2();IsAiming=false;sprintLatched=IsSprinting=false;IsVaulting=true;
            vaultRoutine=StartCoroutine(VaultMotion(raised,landing));return true;
        }
        IEnumerator VaultMotion(Vector3 raised,Vector3 landing)
        {
            Vector3 start=transform.position;float elapsed=0;horizontal=Vector3.zero;verticalSpeed=0;
            while(elapsed<.55f&&G.IsPlaying)
            {
                elapsed+=Time.deltaTime;float t=Mathf.Clamp01(elapsed/.55f);
                Vector3 desired=t<.42f?Vector3.Lerp(start,raised,Mathf.SmoothStep(0,1,t/.42f)):Vector3.Lerp(raised,landing,Mathf.SmoothStep(0,1,(t-.42f)/.58f));
                controller.Move(desired-transform.position);
                yield return null;
            }
            IsVaulting=false;vaultRoutine=null;verticalSpeed=-2;G.sound.Play("vault");G.Record("vault",transform.position.ToString());
        }
        public bool TryApplyArmor(float amount)
        {
            if((!G.IsPlaying && !(G.expeditionInventory && G.state==RunState.Paused)) || health<=0 || !PlayerClassSkill.IsTacticalFlow || amount<=0 || float.IsNaN(amount) || float.IsInfinity(amount)) return false;
            if(TemporaryArmor>0){G.Toast("现有护甲尚未耗尽，未消耗道具",2);return false;}
            TemporaryArmor=amount;G.Record("temporary_armor_applied",amount.ToString("F1"));return true;
        }
        public bool ApplyTemporaryArmor(int amount)=>TryApplyArmor(amount);
        public void ClearArmor(){TemporaryArmor=0;}
        public void Hurt(float damage, Vector3 source, bool shieldBlockable = true, PlayerDamageKind kind = PlayerDamageKind.Normal)
        {
            if (!G.IsPlaying || health <= 0 || damage<=0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
            if(shieldBlockable&&tactics&&tactics.BlockDirect(source,Eye.position))return;
            float effective=damage*G.DamageMultiplier*G.ModuleDamageTaken*G.ExpeditionDamageTaken;
            float absorbed=0;
            if(kind==PlayerDamageKind.Normal)
            {
                if(classSkill)effective*=classSkill.FortifyDamageMultiplier();
                absorbed=Mathf.Min(TemporaryArmor,effective);TemporaryArmor-=absorbed;effective-=absorbed;
                if(absorbed>0)G.RecordAttemptArmorAbsorbed(absorbed);
            }
            float healthBefore=health;health = Mathf.Max(0, health - effective);
            G.Record("player_effective_damage",(healthBefore-health).ToString("R",System.Globalization.CultureInfo.InvariantCulture)); hurtFlash = .40f; lastThreat = source; G.sound.Play("player_hit");
            G.Record("player_damage", "raw="+damage.ToString("F1")+";health="+effective.ToString("F1")+";armor="+absorbed.ToString("F1")+";kind="+kind);
            if (health <= 0) { ClearArmor(); if(classSkill)classSkill.ResetCombat(); G.Die(); }
        }
        public void Supply(float hp, int bullets, int shells)
        {
            health = Mathf.Min(G.config.playerHealth, health + hp); reserve[0] = Mathf.Min(240,reserve[0]+Mathf.RoundToInt(bullets * G.ModuleAmmoSupply)); reserve[1] = Mathf.Min(60,reserve[1]+Mathf.RoundToInt(shells * G.ModuleAmmoSupply));
            foreach(int id in EquippedWeapons) if(id>=2)
            {
                int amount=WeaponCatalog.Get(id).Shotgun?shells:bullets<=0?0:Mathf.Max(1,Mathf.RoundToInt(bullets*(Capacity(id)/(float)Mathf.Max(1,Capacity(0)))));
                reserve[id]=Mathf.Min(EffectiveWeaponStats.Read(id,G.config).Reserve*2,reserve[id]+Mathf.RoundToInt(amount*G.ModuleAmmoSupply));
            }
            G.sound.Play("confirm"); G.Toast("补给已获取",1.5f);
        }
        // The proven swap transaction and placement checks are appended unchanged.
        public SwapTarget FindTarget()
        {
            AimingAtShield = false;
            if (!EnemyActor.CastWeaponRay(Eye.position, Eye.forward, 70, transform, out var hit)) return null;
            var candidate = hit.collider.GetComponentInParent<SwapTarget>();
            AimingAtShield = candidate is EnemyActor enemy && enemy.Alive && enemy.IsShieldCollider(hit.collider);
            return candidate;
        }
        public SwapValidationResult ValidateSwap(SwapTarget candidate)
        {
            if(TacticalAdvanceActive)return new SwapValidationResult(SwapFailureReason.ActionInProgress);
            if(EnemyV2DynamicField.BlocksQ(transform.position)||(candidate&&EnemyV2DynamicField.BlocksQ(candidate.transform.position)))return new SwapValidationResult(SwapFailureReason.Interference);
            if (!G.IsPlaying) return new SwapValidationResult(SwapFailureReason.NotPlaying);
            if (IsVaulting || charging) return new SwapValidationResult(SwapFailureReason.ActionInProgress);
            if (cooldown > .001f) return new SwapValidationResult(SwapFailureReason.Cooldown);
            if (preparing) return new SwapValidationResult(SwapFailureReason.Preparing);
            if (!candidate || !candidate.Alive || !(candidate is EnemyActor || candidate is PhaseAnchor)) return new SwapValidationResult(SwapFailureReason.InvalidTarget);
            if (PhaseInterferenceField.TryGetBlockingField(transform.position, out var field) || PhaseInterferenceField.TryGetBlockingField(candidate.transform.position, out field))
                return new SwapValidationResult(SwapFailureReason.Interference, field.Id);
            if (Vector3.Distance(Eye.position, candidate.AimPoint) > G.config.swapRange) return new SwapValidationResult(SwapFailureReason.OutOfRange);
            // Airborne endpoints are valid if their occupied volumes and eventual landing are safe.
            Vector3 direction = candidate.AimPoint - Eye.position;
            if (CombatRay.Cast(Eye.position, direction.normalized, direction.magnitude + .05f, transform, out var sight) && sight.collider.GetComponentInParent<SwapTarget>() != candidate) return new SwapValidationResult(SwapFailureReason.LineOfSightBlocked);
            Vector3 destination = candidate.transform.position;
            if (!Fits(destination, .32f, 1.8f, candidate) || !Fits(transform.position, candidate.Radius, candidate.Height, candidate)) return new SwapValidationResult(SwapFailureReason.DestinationBlocked);
            if (!AttachedShapesFit(candidate, transform.position)) return new SwapValidationResult(SwapFailureReason.DestinationBlocked);
            // Midair/over-gap exchanges are intentional: the displaced enemy falls physically.
            return new SwapValidationResult(SwapFailureReason.None);
        }
        bool Fits(Vector3 feet, float radius, float height, SwapTarget other)
        {
            // Collision-free air is a valid position; no implicit ground snap or teleport.
            Vector3 a = feet + Vector3.up * (radius + .06f), b = feet + Vector3.up * (height - radius - .03f);
            foreach (Collider collider in Physics.OverlapCapsule(a, b, Mathf.Max(.1f, radius - .035f), Layers.CombatMask, QueryTriggerInteraction.Ignore))
            {
                if (collider.transform.IsChildOf(transform) || collider.transform == transform || collider.transform.IsChildOf(other.transform) || collider.transform == other.transform) continue;
                return false;
            }
            return true;
        }
        bool AttachedShapesFit(SwapTarget other, Vector3 destination)
        {
            Vector3 delta = destination - other.transform.position;
            foreach (Collider shape in other.GetComponentsInChildren<Collider>())
            {
                if (!shape.enabled || shape.isTrigger) continue;
                Collider[] overlaps = null;
                if (shape is BoxCollider box)
                {
                    Vector3 half = Vector3.Scale(box.size, box.transform.lossyScale) * .5f - Vector3.one * .02f;
                    half = new Vector3(Mathf.Max(.01f, Mathf.Abs(half.x)), Mathf.Max(.01f, Mathf.Abs(half.y)), Mathf.Max(.01f, Mathf.Abs(half.z)));
                    overlaps = Physics.OverlapBox(box.transform.TransformPoint(box.center) + delta, half, box.transform.rotation, Layers.CombatMask, QueryTriggerInteraction.Ignore);
                }
                else if (shape is SphereCollider sphere)
                {
                    Vector3 scale = sphere.transform.lossyScale;
                    overlaps = Physics.OverlapSphere(sphere.transform.TransformPoint(sphere.center) + delta, sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) - .02f, Layers.CombatMask, QueryTriggerInteraction.Ignore);
                }
                if (overlaps == null) continue;
                foreach (Collider collider in overlaps)
                {
                    if (collider.transform == transform || collider.transform.IsChildOf(transform) || collider.transform == other.transform || collider.transform.IsChildOf(other.transform)) continue;
                    return false;
                }
            }
            return true;
        }
        public bool RequestSwap(SwapTarget candidate)
        {
            if(BufferV2Swap(candidate))return false;
            var result = ValidateSwap(candidate);
            var context = CaptureSwapTelemetry(candidate, ++nextSwapTelemetryId);
            RecordSwap("swap_attempt", candidate, result, context);
            if (!result.IsValid) { G.Toast("换位未开始："+result.ToDisplayText(G.config.swapRange), 1.2f); RecordSwap("swap_fail", candidate, result, context); return false; }
            CancelConsumableV2();if(meleeEquipment)meleeEquipment.CancelForAction();
            pendingSwapTelemetry = context;
            int canceledRounds=burstRemaining;
            if(canceledRounds>0)
            {
                nextShot=Mathf.Max(nextShot,lastShotAt+.28f);CancelFireSequence();
                G.Record("swap_burst_cancel","remaining="+canceledRounds+";nextShot="+nextShot+";sequence="+actionSequence);
            }
            else actionSequence++;
            swapAcceptedFrame=Time.frameCount;swapHeldReload=reloadLeft>0;fireArmed=false;
            preparing = true; pendingSwapTarget = candidate;
            G.Record("swap_action_accept","sequence="+actionSequence+";reloadHeld="+swapHeldReload+";remaining="+reloadLeft);
            swapRoutine = StartCoroutine(CompleteSwap(candidate)); return true;
        }
        DemoGame.SwapTelemetry CaptureSwapTelemetry(SwapTarget candidate, int attemptId)
        {
            return new DemoGame.SwapTelemetry
            {
                attemptId = attemptId, targetId = candidate ? candidate.GetInstanceID() : 0,
                targetType = candidate ? candidate.GetType().Name : "none", hasTargetBefore = candidate,
                sourceBefore = transform.position, targetBefore = candidate ? candidate.transform.position : Vector3.zero
            };
        }
        void RecordSwap(string eventName, SwapTarget candidate, SwapValidationResult result,
            DemoGame.SwapTelemetry context = null, string legacyDetail = null)
        {
            var before = context ?? pendingSwapTelemetry ?? CaptureSwapTelemetry(candidate, 0);
            var payload = new DemoGame.SwapTelemetry
            {
                attemptId = before.attemptId, reasonCode = (int)result.Code, reason = result.Code.ToString(),
                fieldId = result.FieldId ?? "", targetId = before.targetId, targetType = before.targetType,
                success = eventName == "swap_success" && result.IsValid,
                hasTargetBefore = before.hasTargetBefore, hasTargetAfter = candidate,
                sourceBefore = before.sourceBefore, targetBefore = before.targetBefore,
                sourceAfter = transform.position, targetAfter = candidate ? candidate.transform.position : Vector3.zero
            };
            string detail = legacyDetail ?? ("reason=" + result + ";source=" + transform.position.ToString("F3") + ";target=" + (candidate ? candidate.transform.position.ToString("F3") : "none") + ";object=" + (candidate ? candidate.name + ":" + candidate.GetInstanceID() : "none"));
            G.RecordSwapTelemetry(eventName, detail, payload);
        }
        IEnumerator CompleteSwap(SwapTarget candidate)
        {
            yield return new WaitForSeconds(G.config.swapWindup);
            preparing = false;
            var finalResult = ValidateSwap(candidate);
            if (!finalResult.IsValid) { swapRoutine = null; pendingSwapTarget = null; G.Toast("换位取消：" + finalResult.ToDisplayText(G.config.swapRange)+(swapHeldReload?" · 装填继续":""), 1.3f);swapHeldReload=false; RecordSwap("swap_cancel", candidate, finalResult); pendingSwapTelemetry = null; yield break; }
            bool wasAirborne=!Grounded;
            StopV2Travel();Vector3 oldPlayer = transform.position, oldTarget = candidate.transform.position;
            // Success/rollback coordinates describe the real execution endpoints,
            // while the earlier attempt retains the request-time coordinates.
            pendingSwapTelemetry = CaptureSwapTelemetry(candidate, pendingSwapTelemetry != null ? pendingSwapTelemetry.attemptId : 0);
            Vector3 oldVelocity = horizontal; float oldVertical = verticalSpeed;
            // Move BOTH endpoints with the controller disabled, then publish physics atomically.
            // A Transform-only PhaseAnchor move must not leave an old collider at the player's destination.
            controller.enabled=false;
            transform.position=oldTarget+Vector3.up*.015f;
            bool moved=candidate.MoveTo(oldPlayer);
            if(!moved)
            {
                transform.position=oldPlayer;candidate.MoveTo(oldTarget);
                Physics.SyncTransforms();controller.enabled=true;Physics.SyncTransforms();
                horizontal=oldVelocity;verticalSpeed=oldVertical;
                swapRoutine=null;pendingSwapTarget=null;G.Toast("换位取消：导航恢复失败"+(swapHeldReload?" · 装填继续":""),1.5f);swapHeldReload=false;RecordSwap("swap_rollback", candidate, new SwapValidationResult(SwapFailureReason.RelocationFailed));pendingSwapTelemetry=null;yield break;
            }
            Physics.SyncTransforms();controller.enabled=true;Physics.SyncTransforms();
            G.Record("swap_mode",wasAirborne?"air":"ground");
            G.Record("swap_endpoints","beforePlayer="+oldPlayer.ToString("F3")+"; beforeTarget="+oldTarget.ToString("F3")+"; actualPlayer="+transform.position.ToString("F3")+"; actualTarget="+candidate.transform.position.ToString("F3"));
            if(swapHeldReload){SuspendReloadV2();ResetReloadPresentation();G.Toast("换位成功 · 保留装填机械进度",1.3f);}swapHeldReload=false;
            verticalSpeed = 0; blockFlash = 0; candidate.AfterSwap(); cooldown = G.CampaignCooldown * G.ExpeditionCooldownMultiplier; G.OnCampaignSwap(candidate);EnemyV2Tether.RecheckAll();
            swapCount++; G.totalSwaps++;EnemyTacticalAI.NotifySwap(oldPlayer,transform.position,candidate as EnemyActor);if(G.tutorialCourse)G.tutorialCourse.NotifySwap(wasAirborne); G.tutorialSwapped = true; phaseFlash = .18f; lastSwapTarget = oldPlayer; targetArrowTime = 2;
            Shapes.Echo(oldPlayer); Shapes.Echo(oldTarget); Shapes.Trace(oldPlayer + Vector3.up, oldTarget + Vector3.up, new Color(.2f, 1, .9f), .13f, .09f);
            G.sound.Play("phase"); RecordSwap("swap_success", candidate, new SwapValidationResult(SwapFailureReason.None), legacyDetail: oldPlayer + " -> " + oldTarget); swapRoutine = null; pendingSwapTarget = null; pendingSwapTelemetry = null;
        }
    }
}




