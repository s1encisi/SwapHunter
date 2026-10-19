using System;
using UnityEngine;
namespace SwapHunter
{
    public static class TacticalDemoProfile
    {
        public static ExpeditionProfile CreateFullPreview(int selectedClass=0)
        {
            var p=Create(selectedClass,true);p.credits=100000;p.consumables=new[]{99,99,99};p.medkitStock=99;
            p.meleeKnives=new[]{true,true,true,true,true,true};
            for(int i=0;i<p.cleared.Length;i++)p.cleared[i]=true;
            foreach(var loadout in p.classLoadouts)loadout.knifeId=1;
            // Explicit preview progress only; no fabricated records, best times or purchases.
            ExpeditionStore.Validate(p);return p;
        }
        public static ExpeditionProfile Create(int selectedClass=0,bool testFunds=false)
        {
            if(!RunStorage.IsIsolated)throw new InvalidOperationException("展示种子仅用于隔离演示或验证目录");
            if(!TacticalPrototypeRules.ValidClass(selectedClass))throw new ArgumentOutOfRangeException(nameof(selectedClass));
            var p=ExpeditionStore.NewTacticalProfile();p.activeClass=selectedClass;p.highestUnlocked=11;
            p.credits=testFunds?10000:0;p.sharedCarbineClaimed=p.sharedTacticalClaimed=true;p.starterGeneralId=TacticalPrototypeRules.GeneralSkillIds[0];
            for(int i=0;i<12;i++)p.weapons[i]=true;
            for(int i=0;i<8;i++)p.classSkillLevels[i]=3;
            p.generalLevels[0]=p.generalLevels[1]=3;p.consumables=new[]{4,2,2};
            for(int i=0;i<4;i++)
            {
                var loadout=p.classLoadouts[i];int skill=i==0?1:i*2;
                loadout.starterWeaponClaimed=loadout.starterSkillClaimed=true;
                loadout.starterSkillId=loadout.classSkillId=TacticalPrototypeRules.SkillIds[skill];
                loadout.tacticalSkillId=TacticalPrototypeRules.GeneralSkillIds[0];
                loadout.itemTypes=new[]{0,1};loadout.itemCounts=new[]{2,1};loadout.medkitCarry=2;
            }
            p.primary=p.classLoadouts[selectedClass].primary;p.secondary=p.classLoadouts[selectedClass].secondary;
            p.medkitStock=4;p.mobilityLevel=p.shieldLevel=3;
            // No invented clear history, best times or transaction receipts.
            ExpeditionStore.Validate(p);return p;
        }
    }
}
