using UnityEngine;
namespace SwapHunter
{
    public static class WeaponRig
    {
        public static Transform Create(int id, Transform parent, ModelLibrary models)
        {
            GameObject asset=id==0?models.carbine:id==1?models.shotgun:Resources.Load<GameObject>("Weapons/weapon_"+id.ToString("00"));
            bool fallback=!asset;
            if(fallback) asset=WeaponCatalog.Get(id).Shotgun?models.shotgun:models.carbine;
            Transform root=ImportedModels.Create(asset,parent,Vector3.zero).transform;
            root.name=WeaponCatalog.Get(id).name+" / weapon_"+id.ToString("00");
            if(fallback)
            {
                // Temporary imported-mesh fallback; authored Resources prefabs take precedence.
                Vector3[] silhouettes={Vector3.one,Vector3.one,new Vector3(1.04f,1,1.04f),new Vector3(1.14f,1.08f,1.1f),
                    new Vector3(.86f,.92f,.76f),new Vector3(1.15f,1,.74f),new Vector3(.95f,1,1.2f),new Vector3(.94f,1.02f,1.35f),
                    new Vector3(1.18f,1.15f,1.08f),new Vector3(1.24f,1.12f,.8f),new Vector3(1.1f,1.06f,1.22f),new Vector3(.82f,.88f,.79f)};
                root.localScale=silhouettes[id];
            }
            if(!ImportedModels.Find(root,"Muzzle"))
            {var tip=new GameObject("Muzzle");tip.transform.SetParent(root,false);tip.transform.localPosition=new Vector3(0,.03f,.58f);}
            ImportedModels.SetLayer(root,Layers.ViewModel);
            return root;
        }
    }
}
