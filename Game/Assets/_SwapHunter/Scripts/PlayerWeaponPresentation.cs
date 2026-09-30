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
        readonly Transform[] supportGrips=new Transform[WeaponCatalog.Count],pumps=new Transform[WeaponCatalog.Count];
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
                    if(i==4)magazineGripPoints[i]=WeaponCatalog.Get(i).reloadMagazineGripPoint;
                }
                supportGrips[i]=ImportedModels.Find(weaponModels[i],"SupportGrip");
                pumps[i]=ImportedModels.Find(weaponModels[i],"foregrip");if(pumps[i])pumpOrigins[i]=pumps[i].localPosition;
                string hingeName=i==5?"break_action":i==8?"feed_cover":i==9?"cylinder_hinge":"";
                actionHinges[i]=hingeName.Length>0?ImportedModels.Find(weaponModels[i],hingeName):null;
                if(actionHinges[i])hingeOrigins[i]=actionHinges[i].localRotation;
                roundGroups[i]=ImportedModels.Find(weaponModels[i],"reload_rounds");if(roundGroups[i])roundOrigins[i]=roundGroups[i].localPosition;
                belts[i]=ImportedModels.Find(weaponModels[i],"ammo_belt");
            }
            ToneViewArms();
            presentationReady=true;
        }
        void ToneViewArms()
        {
            foreach(var renderer in viewArms.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    string name=materials[i].name;Color color;
                    if(name.IndexOf("Ceramic",StringComparison.OrdinalIgnoreCase)>=0)color=new Color(.15f,.175f,.18f,1);
                    else if(name.IndexOf("Titanium",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("Aluminium",StringComparison.OrdinalIgnoreCase)>=0)color=new Color(.19f,.21f,.22f,1);
                    else if(name.IndexOf("Phase glass",StringComparison.OrdinalIgnoreCase)>=0)color=new Color(.035f,.12f,.105f,1);
                    else if(name.IndexOf("Elastomer",StringComparison.OrdinalIgnoreCase)>=0)color=new Color(.024f,.03f,.032f,1);
                    else color=new Color(.055f,.073f,.08f,1);
                    var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,i);
                    block.SetColor("baseColorFactor",color);
                    block.SetFloat("metallicFactor",.03f);block.SetFloat("roughnessFactor",.88f);
                    if(name.IndexOf("Phase glass",StringComparison.OrdinalIgnoreCase)>=0)block.SetColor("emissiveFactor",new Color(.03f,.13f,.10f));
                    renderer.SetPropertyBlock(block,i);
                }
            }
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
            if(reloadHand){reloadHand.localPosition=handRest;reloadHand.localRotation=handRotation;}
            if(reloadForearm){reloadForearm.localPosition=forearmRest;reloadForearm.localRotation=forearmRotation;}
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
        Vector3 ReloadPoseOffset=>(weapon==5?new Vector3(-.08f,.10f,.02f):new Vector3(-.14f,.24f,.16f))*ReloadPoseWeight;
        Quaternion ReloadPoseRotation=>Quaternion.Euler((weapon==5?new Vector3(10,-32,-16):new Vector3(-7,-35,-40))*ReloadPoseWeight);

        void UpdateWeaponPresentation()
        {
            if(!presentationReady)return;
            for(int i=0;i<WeaponCatalog.Count;i++)RestoreWeaponParts(i);
            if(reloadLeft<=0)
            {
                ReloadStageTag="ready";reloadGripWeight=0;
                if(reloadHand){reloadHand.localPosition=handRest;reloadHand.localRotation=handRotation;}
                if(reloadForearm){reloadForearm.localPosition=forearmRest;reloadForearm.localRotation=forearmRotation;}
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
            Quaternion gripRotation=magazine.rotation*(weapon==4?Quaternion.Euler(0,90,0):Quaternion.Euler(0,0,90));
            if(rotary&&roundGroups[weapon])
            {
                contact=roundGroups[weapon].position+roundGroups[weapon].rotation*new Vector3(-.04f,-.02f,-.04f);
                gripRotation=roundGroups[weapon].rotation*Quaternion.Euler(0,0,90);
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
            Quaternion restWorld=reloadHand.parent.rotation*handRotation;
            PlaceReloadHand(target,restWorld,weight);
        }
        void PlaceReloadHand(Vector3 grip,Quaternion worldRotation,float weight)
        {
            if(!reloadHand||!reloadForearm||!authoredReloadRig)return;
            Vector3 palmContact=new Vector3(0,-.0275f,.04f);
            // Imported coordinate correction can include a mirrored X scale. Use
            // transformed basis vectors so the palm still faces the real surface.
            Quaternion localRotation=Quaternion.LookRotation(reloadHand.parent.InverseTransformVector(worldRotation*Vector3.forward),reloadHand.parent.InverseTransformVector(worldRotation*Vector3.up));
            Vector3 local=reloadHand.parent.InverseTransformPoint(grip)-localRotation*Vector3.Scale(palmContact,reloadHand.localScale);
            reloadHand.localPosition=Vector3.Lerp(handRest,local,weight);
            reloadHand.localRotation=Quaternion.Slerp(handRotation,localRotation,weight);
            Vector3 wrist=reloadForearm.parent.InverseTransformPoint(reloadHand.TransformPoint(authoredHandWrist));
            Vector3 restAxis=forearmRotation*Vector3.Scale(authoredForearmWrist-authoredForearmElbow,reloadForearm.localScale);
            // The shoulder stays with the player while the gun rolls. A gun-local
            // elbow reference exposes a floating sleeve cap during a canted reload.
            Vector3 elbowReference=reloadForearm.parent.InverseTransformPoint(Eye.TransformPoint(new Vector3(-.28f,-.54f,-.08f)));
            Vector3 direction=wrist-elbowReference;
            if(direction.sqrMagnitude>.0001f)
            {
                var rotation=Quaternion.FromToRotation(restAxis,direction)*forearmRotation;
                reloadForearm.localRotation=rotation;
                reloadForearm.localPosition=wrist-rotation*Vector3.Scale(authoredForearmWrist,reloadForearm.localScale);
            }
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
