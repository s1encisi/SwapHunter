using UnityEngine;
namespace SwapHunter
{
    public enum FireMechanism { Automatic, Pump, Burst, SemiAutomatic, DoubleBarrel, BoltAction, Grenade, Charge, PhaseMark }
    [System.Serializable] public sealed class WeaponDefinition
    {
        public int id, magazine, reserve, pellets=1;
        public string name, description, sound="rifle", accent="";
        public FireMechanism mechanism;
        public float damage, rate, reload, spread, recoil=.7f, movement=1, aimZoom=.84f, chargeTime=.65f;
        public Color tracer=new Color(1,.84f,.55f);
        // Magazine motion in its authored parent frame. Most magazines leave downward.
        public Vector3 magazineExitDirection=Vector3.down;
        public bool useAuthoredReloadGrip;
        public Vector3 reloadMagazineGripPoint,reloadHandContactPoint,reloadHandEuler;
        public bool Automatic => mechanism==FireMechanism.Automatic;
        public bool Shotgun => mechanism==FireMechanism.Pump || mechanism==FireMechanism.DoubleBarrel;
    }
    public static class WeaponCatalog
    {
        public const int Count=12;
        public static readonly WeaponDefinition[] All={
            W(0,"脉冲卡宾枪",FireMechanism.Automatic,24,7.5f,24,144,1.6f,.12f,.75f,"均衡全自动；适合短点射。"),
            W(1,"泵动霰弹枪",FireMechanism.Pump,12,1.2f,6,36,1.8f,3,1.4f,"八枚弹丸；泵动间隔内不能再次开火。",8,"shotgun"),
            W(2,"三连发步枪",FireMechanism.Burst,19,10,27,135,1.9f,.18f,.55f,"扣动一次发射三弹；松开再触发下一组。"),
            W(3,"重型突击步枪",FireMechanism.Automatic,38,4.5f,18,90,2.1f,.25f,1.1f,"低射速高伤害；后坐较强。",1,"rifle","reload_bolt"),
            W(4,"紧凑冲锋枪",FireMechanism.Automatic,13,13,36,180,1.35f,.5f,.38f,"高射速；远距离伤害与精度衰减明显。",1,"rifle","empty"),
            W(5,"双管破门枪",FireMechanism.DoubleBarrel,14,2.8f,2,24,2.3f,4.2f,1.8f,"两次快速击发；十枚弹丸，双发后需装填。",10,"shotgun","reload_bolt"),
            W(6,"精确射手步枪",FireMechanism.SemiAutomatic,62,2.7f,10,60,2,.08f,1.15f,"半自动单点；瞄准倍率更高。",1,"rifle","surface"),
            W(7,"栓动狙击枪",FireMechanism.BoltAction,118,.8f,5,25,2.8f,.035f,2,"单发高伤害；拉栓后才能再射。",1,"shotgun","surface"),
            W(8,"支援轻机枪",FireMechanism.Automatic,21,9,60,180,3.4f,.4f,.82f,"大弹箱压制；持续射击散布更大。",1,"rifle","reload_in"),
            W(9,"弧线榴弹发射器",FireMechanism.Grenade,95,.85f,4,16,2.6f,.3f,1.5f,"抛物线榴弹；爆炸受墙体遮挡，也会伤害自己。",1,"shotgun","land"),
            W(10,"蓄能线圈枪",FireMechanism.Charge,145,1,5,25,2.7f,.055f,1.7f,"按住蓄能0.65秒；提前松开取消，不扣弹药。",1,"phase","rifle"),
            W(11,"相位标记器",FireMechanism.PhaseMark,16,2.4f,12,60,1.7f,.1f,.35f,"命中标记5秒；后续命中伤害+20%，不能穿墙或穿盾。",1,"phase","impact")
        };
        static WeaponCatalog()
        {
            for(int i=2;i<Count;i++){All[i].sound="weapon_"+i.ToString("00");All[i].accent="";}
            All[1].movement=.96f; All[3].movement=.94f; All[4].movement=1.04f; All[4].magazineExitDirection=Vector3.up;
            // Top of the authored .043m-radius cylinder and underside of the .055m-thick glove palm.
            All[4].useAuthoredReloadGrip=true;
            All[4].reloadMagazineGripPoint=new Vector3(0,.043f,0);
            All[4].reloadHandContactPoint=new Vector3(0,-.0275f,.04f);
            All[4].reloadHandEuler=new Vector3(0,90,0); All[5].movement=.96f;
            All[6].aimZoom=.63f; All[7].aimZoom=.40f; All[7].movement=.9f; All[8].movement=.86f;
            All[9].movement=.92f; All[10].aimZoom=.65f; All[10].tracer=new Color(.25f,.9f,1); All[11].tracer=new Color(.65f,.35f,1);
        }
        static WeaponDefinition W(int id,string name,FireMechanism mode,float damage,float rate,int magazine,int reserve,float reload,float spread,float recoil,string description,int pellets=1,string sound="rifle",string accent="")
            =>new WeaponDefinition{id=id,name=name,mechanism=mode,damage=damage,rate=rate,magazine=magazine,reserve=reserve,reload=reload,spread=spread,recoil=recoil,description=description,pellets=pellets,sound=sound,accent=accent};
        public static bool Valid(int id)=>id>=0&&id<Count;
        public static WeaponDefinition Get(int id)=>All[Mathf.Clamp(id,0,Count-1)];
    }
}
