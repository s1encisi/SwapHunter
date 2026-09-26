using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SwapHunter
{
    public sealed class PlayerMotor : MonoBehaviour
    {
        public CharacterController controller;
        public Transform Eye { get; private set; }
        public Camera cameraView, weaponCamera;
        public float health, verticalSpeed, yaw, pitch, cooldown, reloadLeft;
        public int weapon, shots, swapCount;
        public int[] ammo = new int[2], reserve = new int[2];
        public string swapReason = "寻找目标";
        public SwapTarget target;
        public bool preparing;
        public bool IsAiming { get; private set; }
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
                var sight = ImportedModels.Find(weapon == 0 ? rifleModel : shotgunModel, "AimFront");
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
                float spread = weapon == 0 ? G.config.stationarySpread + movement * G.config.movingSpread + shotBloom : 3.0f + movement * 1.0f;
                if (!Grounded) spread += G.config.airborneSpread;
                if (IsAiming) spread *= .76f;
                return spread * G.ModuleSpread;
            }
        }
        Transform weaponRoot, rifleModel, shotgunModel, muzzle, viewArms, playerBody, bodyLeftLeg, bodyRightLeg;
        Transform reloadHand, reloadForearm;
        Vector3 handRest, forearmRest;
        Quaternion handRotation, forearmRotation;
        Transform[] magazines = new Transform[2], bolts = new Transform[2];
        Vector3[] magazineOrigins = new Vector3[2], boltOrigins = new Vector3[2];
        GameObject[] flashes = new GameObject[2];
        Vector3 horizontal, weaponPosition;
        Vector2 recoilAngles, sway;
        float nextShot, visualKick, travel, stepDistance, lastShotAt = -100, shotBloom, reloadDuration, muzzleFlashUntil;
        int burstCount, ignoreLook;
        bool fireArmed = true, sprintLatched, reloadSoundA, reloadSoundB;
        Coroutine swapRoutine;
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
            weaponRoot = new GameObject("First person equipment").transform; weaponRoot.SetParent(Eye, false);
            rifleModel = ImportedModels.Create(G.config.models.carbine, weaponRoot, Vector3.zero).transform;
            shotgunModel = ImportedModels.Create(G.config.models.shotgun, weaponRoot, Vector3.zero).transform;
            viewArms = ImportedModels.Create(G.config.models.arms, weaponRoot, Vector3.zero).transform;
            reloadHand = ImportedModels.Find(viewArms, "hand_L"); reloadForearm = ImportedModels.Find(viewArms, "forearm_L");
            if (reloadHand) { handRest = reloadHand.localPosition; handRotation = reloadHand.localRotation; }
            if (reloadForearm) { forearmRest = reloadForearm.localPosition; forearmRotation = reloadForearm.localRotation; }
            for (int i = 0; i < 2; i++)
            {
                Transform root = i == 0 ? rifleModel : shotgunModel;
                magazines[i] = ImportedModels.Find(root, "magazine"); bolts[i] = ImportedModels.Find(root, "charging_handle");
                if (magazines[i]) magazineOrigins[i] = magazines[i].localPosition;
                if (bolts[i]) boltOrigins[i] = bolts[i].localPosition;
                var marker = ImportedModels.Find(root, "Muzzle");
                flashes[i] = Shapes.Make("Muzzle flash", PrimitiveType.Sphere, marker, new Vector3(0,0,.025f), new Vector3(.065f,.05f,.15f), 5);
                flashes[i].SetActive(false);
            }
            ImportedModels.SetLayer(weaponRoot, Layers.ViewModel);
            foreach (Renderer renderer in weaponRoot.GetComponentsInChildren<Renderer>(true)) renderer.shadowCastingMode = ShadowCastingMode.Off;
            playerBody = ImportedModels.Create(G.config.models.playerBody, transform, Vector3.zero).transform;
            foreach (string part in new[] { "head", "torso", "arm_L", "arm_R" })
            {
                Transform hidden = ImportedModels.Find(playerBody, part);
                if (hidden) foreach (Renderer renderer in hidden.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            bodyLeftLeg = ImportedModels.Find(playerBody, "leg_L"); bodyRightLeg = ImportedModels.Find(playerBody, "leg_R");
            ApplyViewSettings(); ResetAt(new Vector3(0, .06f, 2));
        }
        public void ApplyViewSettings() { if (cameraView) cameraView.fieldOfView = G.fov; if (weaponCamera) weaponCamera.fieldOfView = G.options.viewModelFov; }
        public void SetViewActive(bool active) { cameraView.enabled = active; weaponCamera.enabled = active; if (playerBody) playerBody.gameObject.SetActive(active); }
        public void ResetAt(Vector3 feet)
        {
            CancelActions(); SetFeet(feet); health = G.config.playerHealth;
            cooldown = 0; verticalSpeed = 0; horizontal = Vector3.zero; weapon = 0; nextShot = 0; visualKick = 0; travel = stepDistance = 0;
            recoilAngles = Vector2.zero; shotBloom = 0; burstCount = 0; lastShotAt = -100; targetArrowTime = 0; target = null;
            ammo[0] = G.config.rifleMagazine; ammo[1] = G.config.shotgunMagazine; reserve[0] = G.config.rifleReserve; reserve[1] = G.config.shotgunReserve;
            hitFlash = hurtFlash = phaseFlash = killFlash = blockFlash = 0; AimingAtShield = false; rifleModel.gameObject.SetActive(true); shotgunModel.gameObject.SetActive(false);
            muzzle = ImportedModels.Find(rifleModel, "Muzzle"); weaponPosition = new Vector3(.23f, -.28f, .53f); SetLook(0, 0);
        }
        public void CancelActions()
        {
            if (swapRoutine != null) StopCoroutine(swapRoutine);
            swapRoutine = null; preparing = false; reloadLeft = 0; fireArmed = false; ignoreLook = 2; IsAiming = false; sprintLatched = false; horizontal = Vector3.zero;
        }
        public void SetFeet(Vector3 feet) { controller.enabled = false; transform.position = feet; controller.enabled = true; Physics.SyncTransforms(); }
        public void SetLook(float newYaw, float newPitch) { yaw = newYaw; pitch = Mathf.Clamp(newPitch, -85, 85); SetEyeRotation(); }
        void SetEyeRotation()
        {
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            Eye.localRotation = Quaternion.Euler(Mathf.Clamp(pitch - recoilAngles.x - visualKick * G.shake * .15f, -88, 88), recoilAngles.y, 0);
        }
        void Update()
        {
            if (!G.IsPlaying) return;
            float dt = Time.deltaTime; float oldCooldown = cooldown; cooldown = Mathf.Max(0, cooldown - dt);
            if (oldCooldown > 0 && cooldown == 0) G.sound.Play("confirm");
            blockFlash = Mathf.Max(0, blockFlash - dt); hitFlash = Mathf.Max(0, hitFlash - dt); killFlash = Mathf.Max(0, killFlash - dt); hurtFlash = Mathf.Max(0, hurtFlash - dt); phaseFlash = Mathf.Max(0, phaseFlash - dt); targetArrowTime = Mathf.Max(0, targetArrowTime - dt);
            if (Time.time - lastShotAt > .12f)
            {
                float recoveryStep = Mathf.Min(dt, Time.time - lastShotAt - .12f);
                shotBloom = Mathf.MoveTowards(shotBloom, 0, recoveryStep * (burstCount <= 3 ? G.config.shortRecovery : G.config.longRecovery));
                recoilAngles = Vector2.MoveTowards(recoilAngles, Vector2.zero, recoveryStep * (burstCount <= 3 ? 5 : 3.4f));
            }
            visualKick = Mathf.Lerp(visualKick, 0, 1 - Mathf.Exp(-20 * dt));
            if (reloadLeft > 0)
            {
                reloadLeft -= dt; float phase = 1 - Mathf.Max(0,reloadLeft) / reloadDuration;
                if (phase > .22f && !reloadSoundA) { reloadSoundA = true; G.sound.Play("reload_out"); }
                if (phase > .76f && !reloadSoundB) { reloadSoundB = true; G.sound.Play("reload_in"); }
                if (reloadLeft <= 0) { int take = Mathf.Min(Capacity(weapon) - ammo[weapon], reserve[weapon]); ammo[weapon] += take; reserve[weapon] -= take; G.sound.Play("reload_bolt"); }
            }
            Mouse mouse = G.qaMode && !G.qaInputEnabled ? null : G.MouseDevice;
            Vector2 delta = mouse == null ? Vector2.zero : mouse.delta.ReadValue();
            if (ignoreLook > 0) ignoreLook--;
            else
            {
                Vector2 turn = delta * (G.sensitivity * .04f * (IsAiming ? G.options.aimSensitivity : 1));
                yaw += turn.x; pitch = Mathf.Clamp(pitch + turn.y * G.options.verticalSensitivity * (G.invertY ? 1 : -1), -85, 85);
            }
            if (!G.Held(12)) fireArmed = true;
            if (G.options.toggleAim) { if (G.Pressed(13)) IsAiming = !IsAiming; } else IsAiming = G.Held(13);
            if (reloadLeft > 0) IsAiming = false;
            if (G.options.toggleSprint) { if (G.Pressed(5)) sprintLatched = !sprintLatched; } else sprintLatched = G.Held(5);
            Vector2 movement = Vector2.ClampMagnitude(new Vector2(G.Held(3) ? 1 : 0, G.Held(0) ? 1 : 0) - new Vector2(G.Held(2) ? 1 : 0, G.Held(1) ? 1 : 0), 1);
            bool shooting = G.Held(12) && fireArmed;
            bool sprint = sprintLatched && movement.y > .1f && !shooting && !IsAiming;
            if (shooting || movement.y <= 0) sprintLatched = false;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            float speed = (sprint ? G.config.sprintSpeed : G.Held(9) ? G.config.slowSpeed : G.config.walkSpeed) * G.options.moveSpeedScale;
            speed *= G.ModuleMovement * G.ModuleAimMovement;
            if (weapon == 1) speed *= .96f;
            Vector3 desired = (transform.forward * movement.y + transform.right * movement.x) * speed;
            bool grounded = Grounded; float fallSpeed = verticalSpeed;
            float rate = grounded ? (movement.sqrMagnitude < .01f ? G.config.braking : Vector3.Dot(horizontal, desired) < -.1f ? G.config.counterBraking : G.config.acceleration) : G.config.airAcceleration;
            horizontal = Vector3.MoveTowards(horizontal, desired, rate * dt);
            if (grounded && verticalSpeed < 0) verticalSpeed = -2;
            if (G.Pressed(4) && grounded) verticalSpeed = Mathf.Sqrt(G.config.jumpHeight * 2 * 22);
            verticalSpeed -= 22 * dt; Vector3 beforeMove = transform.position;
            CollisionFlags collision = controller.Move((horizontal + Vector3.up * verticalSpeed) * dt);
            if ((collision & CollisionFlags.Above) != 0 && verticalSpeed > 0) verticalSpeed = 0;
            if ((collision & CollisionFlags.Sides) != 0 && dt > 0) horizontal = Vector3.ProjectOnPlane(transform.position - beforeMove, Vector3.up) / dt;
            if (!grounded && Grounded && fallSpeed < -4) { visualKick += .2f; G.sound.Play("land"); }
            float distance = horizontal.magnitude * dt; travel += distance; stepDistance += grounded ? distance : 0;
            if (travel > 3) G.tutorialMoved = true;
            if (stepDistance > (sprint ? 1.75f : 1.4f)) { stepDistance = 0; G.sound.Play("step"); }
            if (transform.position.y < -12) Hurt(1000, transform.position);
            if (G.Pressed(10)) SwitchWeapon(0); if (G.Pressed(11)) SwitchWeapon(1);
            if (G.Pressed(14) || G.Pressed(15)) SwitchWeapon(1 - weapon);
            if (G.Pressed(7)) Reload();
            SetEyeRotation(); if (shooting) Fire(); SetEyeRotation();
            target = FindTarget(); swapReason = ValidateSwap(target);
            if (G.Pressed(6)) RequestSwap(target); if (G.Pressed(8)) G.Interact();
            cameraView.fieldOfView = Mathf.Lerp(cameraView.fieldOfView, IsAiming ? G.fov * .84f : G.fov, 1 - Mathf.Exp(-14 * dt));
            UpdateEquipment(delta, grounded, dt);
        }
        void UpdateEquipment(Vector2 mouseDelta, bool grounded, float dt)
        {
            float motion = G.options.weaponMotion;
            float moving = Mathf.Clamp01(CurrentSpeed / G.config.walkSpeed) * (grounded ? 1 : .2f);
            sway = Vector2.Lerp(sway, Vector2.ClampMagnitude(mouseDelta * .002f, .025f), 1 - Mathf.Exp(-14 * dt));
            Vector3 targetPosition = new Vector3(IsAiming ? 0 : .23f, IsAiming ? -.1723f : -.28f, .53f);
            targetPosition += new Vector3(-sway.x, -sway.y + Mathf.Sin(travel * 3) * .007f * moving, 0) * motion;
            targetPosition.z -= visualKick * .026f;
            if (Physics.Raycast(Eye.position, Eye.forward, out var close, .85f, Layers.WorldMask)) { float t = 1 - close.distance / .85f; targetPosition += new Vector3(0,-.10f,-.17f) * t; }
            float reload = reloadLeft > 0 ? 1 - reloadLeft / Mathf.Max(.1f,reloadDuration) : 0;
            float envelope = reloadLeft > 0 ? Mathf.Sin(reload * Mathf.PI) : 0;
            targetPosition += new Vector3(-.045f, .20f, .50f) * envelope * motion;
            weaponPosition = Vector3.Lerp(weaponPosition, targetPosition, 1 - Mathf.Exp(-18 * dt)); weaponRoot.localPosition = weaponPosition;
            Quaternion rotation = Quaternion.Euler((IsAiming ? 3.8f : 0) + visualKick * 3 - envelope * 6 * motion, -envelope * 22 * motion, envelope * 22 * motion + sway.x * 40 * motion);
            weaponRoot.localRotation = Quaternion.Slerp(weaponRoot.localRotation, rotation, 1 - Mathf.Exp(-20 * dt));
            for (int i = 0; i < 2; i++)
            {
                if (magazines[i]) magazines[i].localPosition = magazineOrigins[i] + Vector3.down * (i == weapon ? Mathf.Sin(Mathf.Clamp01((reload - .1f) / .75f) * Mathf.PI) * .16f * envelope * motion : 0);
                if (bolts[i]) bolts[i].localPosition = boltOrigins[i] - Vector3.forward * (i == weapon ? visualKick * .023f : 0);
                flashes[i].SetActive(i == weapon && Time.time < muzzleFlashUntil);
            }
            if (reloadHand)
            {
                float reach = reloadLeft > 0 ? Mathf.SmoothStep(0, 1, reload / .18f) * (1 - Mathf.SmoothStep(.80f, 1, reload)) : 0;
                Transform contact = reload > .82f && bolts[weapon] ? bolts[weapon] : magazines[weapon];
                Vector3 targetHand = contact ? reloadHand.parent.InverseTransformPoint(contact.position - contact.up * .11f) : handRest;
                reloadHand.localPosition = Vector3.Lerp(handRest, targetHand, reach);
                reloadHand.localRotation = Quaternion.Slerp(handRotation, Quaternion.Euler(-90, 0, 0), reach);
                if (reloadForearm)
                {
                    Vector3 restAxis = forearmRotation * Vector3.up;
                    Vector3 elbow = forearmRest - restAxis * .20f;
                    Vector3 axis = (reloadHand.localPosition - elbow).normalized;
                    reloadForearm.localPosition = Vector3.Lerp(forearmRest, elbow + axis * .20f, reach);
                    reloadForearm.localRotation = Quaternion.Slerp(forearmRotation, Quaternion.FromToRotation(restAxis, axis) * forearmRotation, reach);
                }
            }
            if (bodyLeftLeg) bodyLeftLeg.localRotation = Quaternion.Euler(Mathf.Sin(travel * 2.5f) * moving * 25, 0, 0);
            if (bodyRightLeg) bodyRightLeg.localRotation = Quaternion.Euler(-Mathf.Sin(travel * 2.5f) * moving * 25, 0, 0);
        }
        public int Capacity(int slot) => slot == 0 ? G.config.rifleMagazine : G.config.shotgunMagazine;
        public void SwitchWeapon(int slot)
        {
            if (slot == weapon || slot < 0 || slot > 1) return;
            reloadLeft = 0; weapon = slot; rifleModel.gameObject.SetActive(slot == 0); shotgunModel.gameObject.SetActive(slot == 1);
            muzzle = ImportedModels.Find(slot == 0 ? rifleModel : shotgunModel, "Muzzle"); nextShot = Mathf.Max(nextShot, Time.time + .20f);
            G.sound.Play("reload");
        }
        public bool Reload()
        {
            if (reloadLeft > 0 || ammo[weapon] == Capacity(weapon) || reserve[weapon] <= 0) return false;
            reloadDuration = (weapon == 0 ? G.config.rifleReload : G.config.shotgunReload) * G.ModuleReload;
            reloadLeft = reloadDuration; reloadSoundA = reloadSoundB = false; return true;
        }
        Vector3 TracerOrigin()
        {
            Vector3 uv = weaponCamera.WorldToViewportPoint(muzzle.position);
            return cameraView.ViewportToWorldPoint(new Vector3(uv.x, uv.y, .8f));
        }
        public bool Fire()
        {
            if (!G.IsPlaying || Time.time < nextShot || reloadLeft > 0) return false;
            if (ammo[weapon] <= 0) { if (!Reload()) { nextShot = Time.time + .3f; G.sound.Play("empty"); } return false; }
            if (Time.time - lastShotAt > .5f) burstCount = 0;
            ammo[weapon]--; shots++; G.totalShots++;
            float interval = 1 / (weapon == 0 ? G.config.rifleRate : G.config.shotgunRate);
            nextShot = nextShot < Time.time - interval * .5f ? Time.time + interval : nextShot + interval;
            G.sound.Play(weapon == 0 ? "rifle" : "shotgun");
            float spreadAngle = CurrentSpreadDegrees;
            int count = weapon == 0 ? 1 : G.config.shotgunPellets;
            bool anyDamage = false, anyKill = false, anyShield = false, anySurface = false; int impactCount = 0; Vector3 surfacePoint = Eye.position;
            for (int i = 0; i < count; i++)
            {
                Vector2 spread = UnityEngine.Random.insideUnitCircle * Mathf.Tan(spreadAngle * Mathf.Deg2Rad);
                Vector3 direction = (Eye.forward + Eye.right * spread.x + Eye.up * spread.y).normalized;
                Vector3 endpoint = Eye.position + direction * 65;
                if (CombatRay.Cast(Eye.position, direction, 65, transform, out var hit))
                {
                    endpoint = hit.point; EnemyActor enemy = hit.collider.GetComponentInParent<EnemyActor>();
                    if (enemy)
                    {
                        float damage = weapon == 0 ? G.config.rifleDamage * Mathf.Lerp(1,.6f,Mathf.InverseLerp(20,45,hit.distance)) : G.config.shotgunDamage * Mathf.Lerp(1,.3f,Mathf.InverseLerp(6,16,hit.distance));
                        bool head = weapon == 0 && hit.point.y > enemy.transform.position.y + 1.4f; if (head) damage *= 1.5f;
                        damage *= G.ModuleShotDamage(hit.distance);
                        bool wasAlive = enemy.Alive; bool damaged = enemy.Hit(damage, hit.point, hit.collider, Eye.position);
                        anyDamage |= damaged; anyKill |= wasAlive && !enemy.Alive; anyShield |= !damaged && wasAlive;
                        G.totalHits += damaged ? 1 : 0; G.tutorialShot = true;
                    }
                    if (!enemy) { anySurface = true; surfacePoint = hit.point; }
                    if (impactCount++ < 3) Shapes.Impact(hit.point, hit.normal, enemy && enemy.IsShieldCollider(hit.collider));
                }
                if (weapon == 1 || shots % 2 == 1) Shapes.Trace(TracerOrigin(), endpoint, new Color(1,.84f,.55f), .035f, .010f);
            }
            if (anyKill) { blockFlash = 0; killFlash = hitFlash = .25f; G.sound.Play("kill"); }
            else if (anyDamage) { blockFlash = 0; hitFlash = .12f; G.sound.Play("impact"); }
            else if (anyShield) { hitFlash = killFlash = 0; blockFlash = .6f; G.sound.Play("shield"); G.Record("shot_blocked", "shield"); }
            else if (anySurface) G.sound.Play("surface", surfacePoint);
            G.ConsumeModuleShot();
            burstCount++; lastShotAt = Time.time; muzzleFlashUntil = Time.time + .035f;
            if (weapon == 0)
            {
                recoilAngles.x = Mathf.Min(6, recoilAngles.x + .42f + Mathf.Min(burstCount, 8) * .045f);
                recoilAngles.y = Mathf.Clamp(recoilAngles.y + SidePattern[(burstCount - 1) % SidePattern.Length], -2, 2);
                shotBloom = Mathf.Min(1.3f, shotBloom + G.config.shotSpreadGrowth);
            }
            else { recoilAngles.x = Mathf.Min(7, recoilAngles.x + 1.8f); shotBloom = Mathf.Min(1.3f, shotBloom + .2f); }
            visualKick = weapon == 0 ? .75f : 1.4f;
            return true;
        }
        public void Hurt(float damage, Vector3 source)
        {
            if (!G.IsPlaying || health <= 0) return;
            health = Mathf.Max(0, health - damage * G.DamageMultiplier * G.ModuleDamageTaken); hurtFlash = .40f; lastThreat = source; G.sound.Play("player_hit");
            G.Record("player_damage", damage.ToString("F1")); if (health <= 0) G.Die();
        }
        public void Supply(float hp, int bullets, int shells)
        {
            health = Mathf.Min(G.config.playerHealth, health + hp); reserve[0] = Mathf.Min(240,reserve[0]+Mathf.RoundToInt(bullets * G.ModuleAmmoSupply)); reserve[1] = Mathf.Min(60,reserve[1]+Mathf.RoundToInt(shells * G.ModuleAmmoSupply));
            G.sound.Play("confirm"); G.Toast("补给已获取",1.5f);
        }
        // The proven swap transaction and placement checks are appended unchanged.
        public SwapTarget FindTarget()
        {
            AimingAtShield = false;
            if (!CombatRay.Cast(Eye.position, Eye.forward, 70, transform, out var hit)) return null;
            var candidate = hit.collider.GetComponentInParent<SwapTarget>();
            AimingAtShield = candidate is EnemyActor enemy && enemy.Alive && enemy.IsShieldCollider(hit.collider);
            return candidate;
        }
        public string ValidateSwap(SwapTarget candidate)
        {
            if (cooldown > .001f) return "冷却中";
            if (preparing) return "相位准备中";
            if (!candidate || !candidate.Alive) return "寻找目标";
            if (Vector3.Distance(Eye.position, candidate.AimPoint) > G.config.swapRange) return "超出 22 米";
            if (!Grounded || !candidate.Grounded) return "落地后可换位";
            Vector3 direction = candidate.AimPoint - Eye.position;
            if (CombatRay.Cast(Eye.position, direction.normalized, direction.magnitude + .05f, transform, out var sight) && sight.collider.GetComponentInParent<SwapTarget>() != candidate) return "视线被挡";
            Vector3 destination = candidate.transform.position;
            if (!Fits(destination, .32f, 1.8f, candidate) || !Fits(transform.position, candidate.Radius, candidate.Height, candidate)) return "落点受阻";
            if (!AttachedShapesFit(candidate, transform.position)) return "落点受阻";
            if (candidate is EnemyActor && (!NavMesh.SamplePosition(transform.position, out var nav, .4f, NavMesh.AllAreas) || Vector3.Distance(nav.position, transform.position) > .3f)) return "落点无法导航";
            return "可换位";
        }
        bool Fits(Vector3 feet, float radius, float height, SwapTarget other)
        {
            if (!Physics.Raycast(feet + Vector3.up * .15f, Vector3.down, out var floor, .35f, Layers.WorldMask) || floor.normal.y < .65f) return false;
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
            string reason = ValidateSwap(candidate);
            if (reason != "可换位") { G.Toast(reason, 1.2f); G.Record("swap_fail", reason); return false; }
            preparing = true; swapRoutine = StartCoroutine(CompleteSwap(candidate)); return true;
        }
        IEnumerator CompleteSwap(SwapTarget candidate)
        {
            yield return new WaitForSeconds(G.config.swapWindup);
            preparing = false;
            if (!G.IsPlaying || ValidateSwap(candidate) != "可换位") { swapRoutine = null; G.Toast("换位取消：目标或落点已改变", 1.3f); G.Record("swap_cancel", "final validation"); yield break; }
            Vector3 oldPlayer = transform.position, oldTarget = candidate.transform.position;
            Vector3 oldVelocity = horizontal; float oldVertical = verticalSpeed;
            SetFeet(oldTarget + Vector3.up * .015f);
            if (!candidate.MoveTo(oldPlayer))
            {
                SetFeet(oldPlayer); candidate.MoveTo(oldTarget); horizontal = oldVelocity; verticalSpeed = oldVertical;
                swapRoutine = null; G.Toast("换位取消：导航恢复失败", 1.5f); G.Record("swap_rollback", "navigation"); yield break;
            }
            verticalSpeed = 0; blockFlash = 0; candidate.AfterSwap(); cooldown = G.CampaignCooldown; G.OnCampaignSwap(candidate);
            swapCount++; G.totalSwaps++; G.tutorialSwapped = true; phaseFlash = .18f; lastSwapTarget = oldPlayer; targetArrowTime = 2;
            Shapes.Echo(oldPlayer); Shapes.Echo(oldTarget); Shapes.Trace(oldPlayer + Vector3.up, oldTarget + Vector3.up, new Color(.2f, 1, .9f), .13f, .09f);
            G.sound.Play("phase"); G.Record("swap_success", oldPlayer + " -> " + oldTarget); swapRoutine = null;
        }
    }
}



