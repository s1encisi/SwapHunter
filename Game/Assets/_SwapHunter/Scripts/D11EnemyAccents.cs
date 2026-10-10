using UnityEngine;
namespace SwapHunter
{
    // Project-authored visual equipment only. Keep the original rig, forward axis, muzzle and hit volumes.
    public static class D11EnemyAccents
    {
        public static void Attach(Transform model,EnemyKind kind)
        {
            if(!model||kind==EnemyKind.Training||model.Find("D11 role accents"))return;
            var root=new GameObject("D11 role accents").transform;root.SetParent(model,false);
            Color color=kind==EnemyKind.Sniper?new Color(.86f,.31f,.28f):kind==EnemyKind.Shield?
                new Color(.95f,.68f,.20f):kind==EnemyKind.Elite?new Color(.72f,.39f,.92f):new Color(.20f,.72f,.82f);
            Plate(root,"Forward chest index",new Vector3(0,1.22f,.225f),new Vector3(.24f,.075f,.025f),color);
            if(kind==EnemyKind.Sniper)
            {
                Plate(root,"Rangefinder",new Vector3(.21f,1.63f,.10f),new Vector3(.07f,.07f,.13f),color);
                Plate(root,"Sniper shoulder profile",new Vector3(-.23f,1.34f,0),new Vector3(.16f,.15f,.24f),color*.65f);
            }
            else if(kind==EnemyKind.Elite)
            {
                for(int side=-1;side<=1;side+=2)
                {
                    Plate(root,"Commander rear energy bank",new Vector3(side*.24f,1.30f,-.20f),new Vector3(.13f,.40f,.13f),color*.7f);
                    Plate(root,"Commander bank indicator",new Vector3(side*.24f,1.32f,-.272f),new Vector3(.06f,.26f,.018f),color);
                }
            }
            else if(kind==EnemyKind.Shield)
            {
                for(int side=-1;side<=1;side+=2)Plate(root,"Shield guard shoulder",new Vector3(side*.26f,1.33f,0),new Vector3(.14f,.16f,.24f),color);
            }
            else
            {
                for(int side=-1;side<=1;side+=2)Plate(root,"Assault utility pouch",new Vector3(side*.19f,.84f,.18f),new Vector3(.11f,.15f,.07f),color*.7f);
            }
            ImportedModels.SetLayer(root,Layers.Actor);
        }
        static void Plate(Transform parent,string name,Vector3 position,Vector3 size,Color color)
        {
            var part=Shapes.Box(name,parent,position,size,1,false,Layers.Actor);
            var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);block.SetColor("_Color",color);
            part.GetComponent<Renderer>().SetPropertyBlock(block);
        }
    }
}
