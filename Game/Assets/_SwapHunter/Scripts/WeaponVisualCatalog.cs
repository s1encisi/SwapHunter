using System;
using UnityEngine;
namespace SwapHunter
{
    // New CC0 meshes; WeaponCatalog remains the sole authority for mechanics and balance.
    public static class WeaponVisualCatalog
    {
        public const string ArmsResourcePath="VisualV2/Characters/fps_arms";
        public const string BladeResourcePath="VisualV2/Weapons/phase_blade";
        public static readonly string[] SourceAssets={
            "QuaterniusUltimateGuns/AssaultRifle_1.blend",
            "QuaterniusUltimateGuns/Shotgun_2.blend",
            "QuaterniusUltimateGuns/Bullpup_2.blend",
            "QuaterniusUltimateGuns/AssaultRifle2_3.blend",
            "QuaterniusUltimateGuns/SubmachineGun_1.blend",
            "QuaterniusUltimateGuns/Shotgun_SawedOff.blend",
            "QuaterniusUltimateGuns/SniperRifle_2.blend",
            "QuaterniusUltimateGuns/SniperRifle_5.blend",
            "QuaterniusUltimateGuns/AssaultRifle2_4.blend + authored feed/bipod parts",
            "KenneyBlasterKit/blaster-m.glb + authored four-round hinged launcher",
            "KenneyBlasterKit/blaster-g.glb + authored charging coils",
            "KenneyBlasterKit/blaster-r.glb + authored phase emitter"
        };
        public static string ResourcePath(int id)
        {if(!WeaponCatalog.Valid(id))throw new ArgumentOutOfRangeException(nameof(id));return "VisualV2/Weapons/weapon_"+id.ToString("00");}
    }
    public static class WeaponVisualFactory
    {
        static readonly GameObject[] assets=new GameObject[WeaponCatalog.Count];
        public static string SourceKey(int id)=>"Resources/"+WeaponVisualCatalog.ResourcePath(id);
        public static GameObject ResolveAsset(int id)
        {
            if(!WeaponCatalog.Valid(id))throw new ArgumentOutOfRangeException(nameof(id));
            if(!assets[id])assets[id]=Resources.Load<GameObject>(WeaponVisualCatalog.ResourcePath(id));
            if(!assets[id])throw new InvalidOperationException("缺少新的CC0武器资源："+SourceKey(id));
            return assets[id];
        }
        public static Transform Create(int id,Transform parent,int layer=0)
        {
            var root=InstantiateVisual(ResolveAsset(id),parent,WeaponCatalog.Get(id).name+" / CC0 weapon_"+id.ToString("00"),layer);
            foreach(string marker in new[]{"Muzzle","Grip","SupportGrip","AimRear","AimFront"})
                if(!ImportedModels.Find(root,marker)){UnityEngine.Object.Destroy(root.gameObject);throw new InvalidOperationException(SourceKey(id)+" 缺少实际模型标记 "+marker);}
            // glTF's handedness reflection can reverse an empty node's rotation even when its
            // position and visible barrel are correct. Put gameplay-facing markers in the positive
            // wrapper frame, keeping their real authored positions and using the actual sight axis.
            var rear=ImportedModels.Find(root,"AimRear");var front=ImportedModels.Find(root,"AimFront");
            Vector3 forward=front.position-rear.position;
            if(forward.sqrMagnitude<.0001f){UnityEngine.Object.Destroy(root.gameObject);throw new InvalidOperationException("新枪实际瞄准轴无效："+id);}
            Quaternion orientation=Quaternion.LookRotation(forward.normalized,root.up);
            foreach(string markerName in new[]{"Muzzle","Grip","SupportGrip","AimRear","AimFront"})
            {
                var marker=ImportedModels.Find(root,markerName);
                Vector3 authoredPoint=marker.position;marker.SetParent(root,true);
                marker.localScale=Vector3.one;marker.SetPositionAndRotation(authoredPoint,orientation);
            }
            var magazineGrip=ImportedModels.Find(root,"MagazineGrip");
            if(magazineGrip)magazineGrip.rotation=orientation;
            return root;
        }
        public static Transform InstantiateVisual(GameObject asset,Transform parent,string name,int layer)
        {
            if(!asset)throw new InvalidOperationException("缺少新模型资源："+name);
            var root=new GameObject(name).transform;root.SetParent(parent,false);
            var model=UnityEngine.Object.Instantiate(asset,root,false);
            // Preserve the authored/imported asset transform. The wrapper itself is Unity metres and scale 1.
            model.transform.localPosition=asset.transform.localPosition;
            model.transform.localRotation=asset.transform.localRotation;
            model.transform.localScale=asset.transform.localScale;
            foreach(var collider in root.GetComponentsInChildren<Collider>(true)){collider.enabled=false;UnityEngine.Object.Destroy(collider);}
            ImportedModels.SetLayer(root,layer);return root;
        }
    }
}