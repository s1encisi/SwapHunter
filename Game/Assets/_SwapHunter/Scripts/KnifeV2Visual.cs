using System;
using UnityEngine;
namespace SwapHunter
{
    public readonly struct WeaponMotionV2Profile
    {
        public readonly float readySeconds,totalSeconds,adsIn,adsOut,sprintReady,recoilTravel,recoilReturn;
        public WeaponMotionV2Profile(float ready,float ads,float travel,float recovery){readySeconds=ready;totalSeconds=ready+.07f;adsIn=ads;adsOut=ads*.8f;sprintReady=ready*.64f;recoilTravel=travel;recoilReturn=recovery;}
    }
    public static class WeaponMotionV2
    {
        static readonly WeaponMotionV2Profile[] data={
            new WeaponMotionV2Profile(.28f,.20f,.020f,.24f),new WeaponMotionV2Profile(.34f,.24f,.038f,.30f),
            new WeaponMotionV2Profile(.30f,.21f,.018f,.23f),new WeaponMotionV2Profile(.36f,.28f,.031f,.28f),
            new WeaponMotionV2Profile(.22f,.15f,.012f,.18f),new WeaponMotionV2Profile(.31f,.22f,.040f,.30f),
            new WeaponMotionV2Profile(.34f,.25f,.026f,.29f),new WeaponMotionV2Profile(.43f,.32f,.044f,.35f),
            new WeaponMotionV2Profile(.45f,.32f,.024f,.36f),new WeaponMotionV2Profile(.40f,.28f,.034f,.32f),
            new WeaponMotionV2Profile(.39f,.28f,.030f,.30f),new WeaponMotionV2Profile(.27f,.20f,.014f,.20f)};
        public static WeaponMotionV2Profile Get(int id){if(id<0||id>=data.Length)throw new ArgumentOutOfRangeException(nameof(id));return data[id];}
    }
    // Model-only contract. Inventory, melee hits and pickup transactions are owned by MeleeEquipmentV2.
    public sealed class KnifeV2Visual:MonoBehaviour
    {
        public int KnifeId{get;private set;}
        public Transform Grip{get;private set;}
        public Transform Tip{get;private set;}
        public Transform Ring{get;private set;}
        public Transform Blade{get;private set;}
        public float OpenAmount{get;private set;}=1;
        public float PoseSeconds{get;private set;}
        Transform hingeA,hingeB;Quaternion aRest,bRest;Transform[] rope=Array.Empty<Transform>();Quaternion[] ropeRest=Array.Empty<Quaternion>();
        public static readonly string[] Names={"普通匕首","M9宝石刀","爪子刀","蝴蝶刀","系绳匕首","锯齿刀"};
        public static KnifeV2Visual Create(int id,Transform parent,int layer=0)
        {
            if(id<0||id>=6)throw new ArgumentOutOfRangeException(nameof(id));string path="VisualMapV2/Knives/knife_"+id.ToString("00");
            var asset=Resources.Load<GameObject>(path);if(!asset)throw new InvalidOperationException("缺少刀具模型 "+path);
            var root=WeaponVisualFactory.InstantiateVisual(asset,parent,Names[id]+" / 原创实体刀具",layer);var result=root.gameObject.AddComponent<KnifeV2Visual>();result.KnifeId=id;
            result.Grip=ImportedModels.Find(root,"Grip");result.Tip=ImportedModels.Find(root,"Tip");result.Ring=ImportedModels.Find(root,"Ring");result.Blade=ImportedModels.Find(root,"Blade");
            if(!result.Grip||!result.Tip||!result.Blade)throw new InvalidOperationException("刀具缺少握点/刃尖/刀刃: "+id);
            // Positive Unity marker orientation, preserving the real authored point.
            Vector3 gp=result.Grip.position;result.Grip.SetParent(root,true);result.Grip.SetPositionAndRotation(gp,root.rotation);
            result.hingeA=ImportedModels.Find(root,"HandleHingeA");result.hingeB=ImportedModels.Find(root,"HandleHingeB");
            if(result.hingeA)result.aRest=result.hingeA.localRotation;if(result.hingeB){result.bRest=result.hingeB.localRotation;if(id==3)result.Grip.SetParent(result.hingeB,true);}
            return result;
        }
        public void SetFold(float open)
        {
            OpenAmount=Mathf.Clamp01(open);if(KnifeId!=3)return;
            if(hingeA)hingeA.localRotation=aRest*Quaternion.AngleAxis(180*(1-OpenAmount),Vector3.forward);
            if(hingeB)hingeB.localRotation=bRest*Quaternion.AngleAxis(-180*(1-OpenAmount),Vector3.forward);
        }
        public void AlignGrip(Vector3 worldPosition,Quaternion worldRotation)
        {Quaternion delta=worldRotation*Quaternion.Inverse(Grip.rotation);transform.rotation=delta*transform.rotation;transform.position+=worldPosition-Grip.position;}
        public void SetInspect(float normalized)
        {
            PoseSeconds=Mathf.Clamp01(normalized);float turn=Mathf.Sin(PoseSeconds*Mathf.PI);
            if(KnifeId==3)SetFold(1-.72f*Mathf.Sin(PoseSeconds*2*Mathf.PI)*Mathf.Sin(PoseSeconds*2*Mathf.PI));
            // The holder positions this root; no autonomous rotation or ownership transfer occurs here.
            if(KnifeId==4)UpdateLanyard(turn);
        }
        void UpdateLanyard(float motion)
        {
            if(rope.Length==0){var all=GetComponentsInChildren<Transform>();var list=new System.Collections.Generic.List<Transform>();foreach(var t in all)if(t.name.StartsWith("Lanyard segment",StringComparison.Ordinal))list.Add(t);rope=list.ToArray();ropeRest=new Quaternion[rope.Length];for(int i=0;i<rope.Length;i++)ropeRest[i]=rope[i].localRotation;}
            // Bounded visual compliance; each segment retains its authored length.
            for(int i=0;i<rope.Length;i++)rope[i].localRotation=ropeRest[i]*Quaternion.Euler(0,0,Mathf.Sin(Time.time*5+i*.12f)*motion*3);
        }
    }
}
