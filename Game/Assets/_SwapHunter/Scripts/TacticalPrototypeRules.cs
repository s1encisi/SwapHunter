using System;
namespace SwapHunter
{
    // D1.1 implementation balance, versioned separately from save schemas.
    public static class TacticalPrototypeRules
    {
        public const int Version=2, ClassCount=4, AssaultClass=0;
        public const int ShockSkillIndex=0,MobilitySkillIndex=1,FortifySkillIndex=2,ReloadSkillIndex=3,ScanSkillIndex=4,PrecisionSkillIndex=5,RemoteSkillIndex=6,DenialSkillIndex=7;
        public const int ShieldGeneralIndex=0,KnifeGeneralIndex=1;
        public const int MedkitPrice=20,AmmoPrice=25,ArmorPrice=25,MedkitCarryLimit=2,AmmoMagazineCount=2,OptionalReward=40;
        public const float MedkitHeal=40f,ArmorAmount=40f;
        public const string MobilitySkillId="rapid_mobility",ShieldSkillId="a_shield";
        public static readonly string[] ClassNames={"突击兵","重装兵","侦查兵","远程兵"};
        public static readonly string[] ClassSkillNames={"震荡突击","快速机动","阵地加固","快速装填","战术扫描","精确射击","遥控爆破","区域封锁"};
        public static readonly string[] GeneralSkillNames={"A盾","相位飞刀"};
        public static readonly string[] ConsumableNames={"医疗包","弹药包","应急护甲"};
        public static readonly string[] SkillIds={"concussive_assault",MobilitySkillId,"field_fortification","rapid_reload","tactical_scan","precision_shot","remote_explosive","area_denial"};
        public static readonly string[] GeneralSkillIds={ShieldSkillId,"phase_knife"};
        static readonly int[] WeaponPrices={120,120,140,140,120,180,180,240,220,160,220,200};
        public static bool ValidClass(int index)=>index>=0&&index<ClassCount;
        public static bool ValidSkill(int index,bool general=false)=>index>=0&&index<(general?GeneralSkillIds.Length:SkillIds.Length);
        public static bool ValidConsumable(int type)=>type>=0&&type<ConsumableNames.Length;
        public static int SkillIndex(string id,bool general=false)=>Array.IndexOf(general?GeneralSkillIds:SkillIds,id);
        public static bool ClassCanUseSkill(int classId,int index)=>ValidClass(classId)&&ValidSkill(index)&&index/2==classId;
        public static bool ClassCanUseWeapon(int classId,int weapon)
        {
            if(!ValidClass(classId)||!WeaponCatalog.Valid(weapon))return false;
            if(weapon==0)return true;
            switch(classId)
            {
                case 0:return weapon==1||weapon==4||weapon==5;
                case 1:return weapon==3||weapon==8;
                case 2:return weapon==2||weapon==6||weapon==7;
                case 3:return weapon==9||weapon==10||weapon==11;
                default:return false;
            }
        }
        public static int StarterWeapon(int classId)
        {
            switch(classId){case 0:return 1;case 1:return 3;case 2:return 2;case 3:return 9;default:throw new ArgumentOutOfRangeException(nameof(classId));}
        }
        public static bool WeaponListed(int weapon,bool[] cleared)
        {
            if(!WeaponCatalog.Valid(weapon))return false;
            switch(weapon)
            {
                case 0:case 1:case 2:case 3:case 9:return true;
                case 4:case 6:case 8:case 10:return cleared!=null&&cleared.Length>2&&cleared[2];
                default:return cleared!=null&&cleared.Length>5&&cleared[5];
            }
        }
        public static int WeaponPrice(int weapon)
        {if(!WeaponCatalog.Valid(weapon))throw new ArgumentOutOfRangeException(nameof(weapon));return WeaponPrices[weapon];}
        public static int SkillBasePrice(int index,bool general=false)
        {if(!ValidSkill(index,general))throw new ArgumentOutOfRangeException(nameof(index));return 100;}
        public static int SkillUpgradePrice(int currentLevel)
        {if(currentLevel==1)return 100;if(currentLevel==2)return 160;throw new InvalidOperationException(currentLevel==0?"先拥有该技能":"技能已满级");}
        public static int ConsumablePrice(int type)
        {if(!ValidConsumable(type))throw new ArgumentOutOfRangeException(nameof(type));return type==0?MedkitPrice:type==1?AmmoPrice:ArmorPrice;}
        public static int ConsumableLimit(int type)
        {if(!ValidConsumable(type))throw new ArgumentOutOfRangeException(nameof(type));return type==0?2:1;}
        public static int BaseReward(int level)
        {ValidateLevel(level);return 50;}
        public static int FirstClearExtra(int level)
        {ValidateLevel(level);return 150+15*level;}
        public static int FirstClearTotal(int level)=>BaseReward(level)+FirstClearExtra(level);
        static void ValidateLevel(int level){if(level<0||level>=12)throw new ArgumentOutOfRangeException(nameof(level));}
    }
}