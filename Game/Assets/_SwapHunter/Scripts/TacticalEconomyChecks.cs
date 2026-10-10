using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace SwapHunter
{
    [Serializable] public sealed class TacticalEconomyRow
    {
        public int classId,chapter,opening,supplyCost,purchaseCost,beforeDeployment,income,closing;
        public int primary,secondary,classSkillLevel,generalSkillLevel;
        public string purchases;
    }
    // Synthetic isolated transactions only. These checks do not establish combat completion or human balance.
    public static class TacticalEconomyChecks
    {
        public static List<TacticalEconomyRow> Run(string fixtureRoot,Action<string,bool,string> add)
        {
            if(string.IsNullOrWhiteSpace(fixtureRoot)||add==null)throw new ArgumentException("需要隔离测试根目录和报告回调");
            string root=Path.Combine(fixtureRoot,"economy-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            var rows=new List<TacticalEconomyRow>();
            for(int role=0;role<4;role++)
            {
                int captured=role;
                Check(add,"economy_path_class_"+role,()=>PathForClass(Path.Combine(root,"class-"+captured),captured,rows,add));
            }
            Check(add,"transactions_snapshots_all_consumables",()=>Transactions(Path.Combine(root,"transactions"),add));
            Check(add,"migration_synthetic_preview_and_candidate",()=>Migration(add));
            add("economy_all_four_paths_have_twelve_rows",rows.Count==48,"rows="+rows.Count+"; synthetic transactions, no combat-play conclusion");
            return rows;
        }
        static void PathForClass(string folder,int role,List<TacticalEconomyRow> rows,Action<string,bool,string> add)
        {
            int[] firstGuns={4,8,6,10},secondGuns={5,-1,7,11},skills={1,3,5,6};
            int skill=skills[role],general=role%2;
            var store=new ExpeditionStore(folder,true);store.SelectClass(role);store.ClaimWeaponStarter();store.ClaimSkillStarter(skill);store.ClaimGeneralStarter(general);
            int initial=store.Profile.credits;
            Require(initial==0&&store.ConsumableStock(0)==0,"Initial assets must not include cash or consumables");
            for(int level=0;level<12;level++)
            {
                var row=new TacticalEconomyRow{classId=role,chapter=level+1,opening=store.Profile.credits,purchases=""};
                if(level>0)
                {
                    store.BuyConsumable("supply-med-"+level,0,2);store.BuyConsumable("supply-ammo-"+level,1);
                    store.SetConsumableSlot(0,0,2);store.SetConsumableSlot(1,1,1);
                    row.supplyCost=2*TacticalPrototypeRules.ConsumablePrice(0)+TacticalPrototypeRules.ConsumablePrice(1);
                }
                int before=store.Profile.credits;
                if(level==1){store.UpgradeSkill("class-two",skill);row.purchases="职业技能 1→2";}
                if(level==2){store.UpgradeSkill("general-two",general,true);row.purchases="通用技能 1→2";}
                if(level==3){store.BuyWeapon("main-gun",firstGuns[role]);store.Equip(0,firstGuns[role]);row.purchases="购买并装备 "+WeaponCatalog.Get(firstGuns[role]).name;}
                if(level==4){store.UpgradeSkill("class-three",skill);row.purchases="职业技能 2→3";}
                if(level==5){store.UpgradeSkill("general-three",general,true);row.purchases="通用技能 2→3";}
                if(level==6&&secondGuns[role]>=0){store.BuyWeapon("second-gun",secondGuns[role]);store.Equip(1,secondGuns[role]);row.purchases="购买并装备 "+WeaponCatalog.Get(secondGuns[role]).name;}
                row.purchaseCost=before-store.Profile.credits;row.beforeDeployment=store.Profile.credits;
                row.primary=store.Primary;row.secondary=store.Secondary;row.classSkillLevel=store.EquippedClassSkillLevel;row.generalSkillLevel=store.EquippedGeneralSkillLevel;
                Require(row.beforeDeployment>=0,"Negative cash before deployment");
                Require(store.CanEquip(row.primary)&&store.CanEquip(row.secondary)&&row.primary!=row.secondary,"Illegal weapon pair");
                if(level==6)Require(row.primary!=0&&row.secondary!=0&&row.classSkillLevel==3&&row.generalSkillLevel==3,"Main pair and two skills not formed before chapter 7");
                string run=store.Begin(level);
                Require(store.Profile.deployment.medkits==(level==0?0:2)&&store.Profile.deployment.ammoPacks==(level==0?0:1),"Carry snapshot disagrees with planned supply");
                var receipt=store.Settle(run,true,false,60,level==0?0:2,level==0?0:1,0);
                row.income=receipt.reward;row.closing=store.Profile.credits;rows.Add(row);
                Require(receipt.first&&!receipt.bonus&&receipt.reward==TacticalPrototypeRules.FirstClearTotal(level),"Path used replay or optional income");
                Require(row.closing==row.opening-row.supplyCost-row.purchaseCost+row.income,"Cash flow does not reconcile");
                Require(store.ConsumableStock(0)==0&&store.ConsumableStock(1)==0,"Normal supplies were not charged once");
            }
            add("economy_class_"+role+"_formed_before_7",true,"48-row report includes legal purchases, no replay/optional; final="+store.Profile.credits);
        }
        static void Transactions(string folder,Action<string,bool,string> add)
        {
            var s=new ExpeditionStore(folder,true);
            Require(s.Profile.version==6&&Path.GetFileName(s.SavePath)=="expedition-v6.json","Wrong isolated save version");
            s.ClaimAssaultStarter();s.ClaimAssaultStarter();
            Require(s.Profile.credits==0&&s.HasCurrentClassStarter,"Starter repeats must not award cash");
            Require(Reject(()=>s.BuyMedkit("no-money")),"Unfunded purchase accepted");
            Require(Reject(()=>s.ClaimSkillStarter(0))&&Reject(()=>s.ClaimGeneralStarter(1)),"Alternative guarantee repeated");
            string run=s.Begin(0);var first=s.Settle(run,true,false,30);
            Require(first.reward==200&&!s.Profile.weapons[4],"First clear must list later goods, not automatically own a weapon");
            s.BuySkill("independent-other-assault",0);s.BuySkill("general-knife",1,true);
            s.EquipClassSkill(0);s.EquipGeneralSkill(1);
            Require(s.SkillLevel(0)==1&&s.SkillLevel(1)==1&&s.HasKnife&&!s.HasShield,"Independent class purchase or generic exclusivity failed");
            s.EquipGeneralSkill(0);Require(s.HasShield&&!s.HasKnife,"Generic slot did not switch exclusively");
            s.SetAttachment(0,1);s.SelectClass(1);s.ClaimWeaponStarter();s.ClaimSkillStarter(2);s.SetAttachment(0,2);
            Require(s.Profile.credits==0&&!s.CanEquip(1)&&s.Profile.weapons[1]&&s.CanEquip(3),"Shared ownership or profession use boundary failed");
            s.SelectClass(0);Require(s.AttachmentFor(0)==1,"Per-class shared-weapon attachment leaked");
            for(int level=1;level<=3;level++){run=s.Begin(level);s.Settle(run,true,false,30);}
            for(int skill=0;skill<8;skill++)if(s.SkillLevel(skill)==0)s.BuySkill("all-bases-"+skill,skill);
            Require(Reject(()=>s.EquipClassSkill(2)),"Other-class skill equipped");
            for(int skill=0;skill<8;skill++)Require(s.SkillLevel(skill)==1,"Base skill required its sibling or chapter gate");
            s.BuyConsumable("med-stock",0,3);s.BuyConsumable("ammo-stock",1,2);s.BuyConsumable("armor-stock",2,2);
            int money=s.Profile.credits;s.BuyConsumable("med-stock",0,3);
            Require(s.Profile.credits==money&&s.ConsumableStock(0)==3&&Reject(()=>s.BuyConsumable("med-stock",1,3)),"Purchase request identity failed");
            s.SetConsumableSlot(0,0,2);s.SetConsumableSlot(1,2,1);
            Require(Reject(()=>s.SetConsumableSlot(1,0,1))&&Reject(()=>s.SetConsumableSlot(1,1,2)),"Duplicate type or capacity limit accepted");
            string bytes=File.ReadAllText(s.SavePath),temp=s.SavePath+".tmp";int stock=s.MedkitStock;
            Directory.CreateDirectory(temp);
            try{Require(Reject(()=>s.BuyMedkit("failed-write")),"Purchase write fault did not reject");}
            finally{Directory.Delete(temp);}
            Require(s.Profile.credits==money&&s.MedkitStock==stock&&File.ReadAllText(s.SavePath)==bytes,"Failed purchase changed memory or disk");
            s.BuyMedkit("failed-write");
            run=s.Begin(4);s.Settle(run,true,false,30);s.UpgradeSkill("shock-two",0);s.UpgradeSkill("shock-three",0);
            Require(Reject(()=>s.UpgradeSkill("shock-four",0)),"Fourth skill level accepted");
            run=s.Begin(5);money=s.Profile.credits;int[] inventory=(int[])s.Profile.consumables.Clone();
            Require(Reject(()=>s.SelectClass(1))&&Reject(()=>s.SetConsumableSlot(1,1,1))&&Reject(()=>s.EquipGeneralSkill(1))&&Reject(()=>s.BuyMedkit("in-run")),"Active-run changes were not locked");
            s.RecordDeath(run);var snapshot=s.Retry(run);
            Require(snapshot.runId==run&&snapshot.attempts==2&&snapshot.deaths==1&&snapshot.medkits==2&&snapshot.armorPacks==1&&snapshot.ammoPacks==0,"Retry did not retain deployment");
            Require(Reject(()=>s.Settle(run,true,false,30,0,1,0)),"Consumed an uncarried item");
            bytes=File.ReadAllText(s.SavePath);Directory.CreateDirectory(temp);
            try{Require(Reject(()=>s.Settle(run,true,false,30,1,0,1)),"Settlement write fault did not reject");}
            finally{Directory.Delete(temp);}
            Require(s.Profile.activeRun==run&&s.Profile.credits==money&&s.MedkitStock==inventory[0]&&File.ReadAllText(s.SavePath)==bytes,"Failed settlement partially committed");
            var receipt=s.Settle(run,true,false,30,1,0,1);int finalMoney=s.Profile.credits;
            s.Settle(run,false,true,0,2,1,1);
            Require(s.Profile.credits==finalMoney&&s.ConsumableStock(0)==inventory[0]-1&&s.ConsumableStock(1)==inventory[1]&&s.ConsumableStock(2)==inventory[2]-1&&receipt.attempts==2,"Settlement duplicated or charged wrong items");
            run=s.Begin(6);int[] beforeExit=(int[])s.Profile.consumables.Clone();s.Settle(run,false,false,1,1,0,1);
            Require(s.Profile.credits==finalMoney&&Equal(beforeExit,s.Profile.consumables)&&!s.Profile.cleared[6],"Abandon changed inventory, credit or first-clear progress");
            run=s.Begin(6);var reloaded=new ExpeditionStore(folder,true);
            Require(reloaded.Profile.activeRun==""&&reloaded.Profile.deployment==null&&reloaded.Profile.credits==finalMoney&&Equal(beforeExit,reloaded.Profile.consumables),"Interrupted load did not preserve post-purchase assets");
            // An explicit synthetic legacy file proves v6 does not select the old path.
            string isolatedOld=Path.Combine(folder,"expedition-v4.json");File.WriteAllText(isolatedOld,"synthetic old profile must remain unread");
            string old=File.ReadAllText(isolatedOld);new ExpeditionStore(folder,true);
            Require(File.ReadAllText(isolatedOld)==old,"v6 touched the old profile path");
            add("full_store_transaction_detail",true,"shared ownership, 8 independent bases, class/generic eligibility, two distinct slots, 3 inventories, failure rollback, retries, exit, recovery, duplicate settlement");
        }
        static void Migration(Action<string,bool,string> add)
        {
            var legacy=new ExpeditionProfile{credits=100,highestUnlocked=1,knifeRank=3,ranks=new[]{1,2,3}};
            legacy.cleared[0]=true;legacy.receipts.Add(new ExpeditionReceipt{runId="historical-v4",level=0,reward=80,success=true,first=true,seconds=30});
            string text=JsonUtility.ToJson(legacy);
            Require(Reject(()=>TacticalMigrationPreview.Preview(text,"synthetic",false)),"Unselected copy accepted");
            var pending=TacticalMigrationPreview.Preview(text,"synthetic-v4",true);
            Require(!pending.proposedCreditsKnown&&pending.UnknownHistory.Count>0&&Reject(()=>TacticalMigrationPreview.BuildCandidate(pending,true)),"Unknown payment history silently became zero");
            var known=TacticalMigrationPreview.Preview(text,"synthetic-v4",true,1m,1120,220);
            Require(Reject(()=>TacticalMigrationPreview.BuildCandidate(known,false)),"Candidate built without review approval");
            var candidate=TacticalMigrationPreview.BuildCandidate(known,true);
            var again=TacticalMigrationPreview.Preview(text,"synthetic-v4",true,1m,1120,220);
            var againCandidate=TacticalMigrationPreview.BuildCandidate(again,true);
            add("migration_repeated_preview_deterministic",known.sourceSha256==again.sourceSha256&&JsonUtility.ToJson(candidate)==JsonUtility.ToJson(againCandidate),"Pure preview/candidate generates no new run or duplicate reward");
            Require(candidate.version==6&&candidate.credits==1440&&candidate.generalLevels[0]==1&&candidate.generalLevels[1]==3&&candidate.cleared[0]&&candidate.ranks[0]==0&&candidate.receipts[0].historical,"Legacy effect mapping or refund failed");
            var t01=ExpeditionStore.NewTacticalProfile();t01.version=5;t01.credits=73;t01.medkitStock=3;t01.mobilityLevel=1;t01.shieldLevel=1;t01.weapons[0]=t01.weapons[1]=true;
            t01.classLoadouts[0].medkitCarry=2;t01.classLoadouts[0].classSkillId=TacticalPrototypeRules.MobilitySkillId;t01.classLoadouts[0].tacticalSkillId=TacticalPrototypeRules.ShieldSkillId;
            t01.purchases.Add(new TacticalPurchaseReceipt{requestId="old-med",quantity=2,unitPrice=20,totalCost=40,balanceAfter=73,stockAfter=3,rulesVersion=1});
            var t01Preview=TacticalMigrationPreview.Preview(JsonUtility.ToJson(t01),"synthetic-v5",true);
            var t01Candidate=TacticalMigrationPreview.BuildCandidate(t01Preview,true);
            Require(t01Candidate.credits==73&&t01Candidate.consumables[0]==3&&t01Candidate.classSkillLevels[1]==1&&t01Candidate.generalLevels[0]==1&&t01Candidate.purchases[0].kind=="consumable","T01 new currency or known purchase records were not preserved");
            add("migration_preview_detail",true,"synthetic v4/v5 only; no formal profile read or import");
        }
        static bool Equal(int[] a,int[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
        static bool Reject(Action action){try{action();return false;}catch(InvalidOperationException){return true;}catch(ArgumentException){return true;}catch(IOException){return true;}catch(UnauthorizedAccessException){return true;}}
        static void Require(bool passed,string reason){if(!passed)throw new InvalidOperationException(reason);}
        static void Check(Action<string,bool,string> add,string name,Action test)
        {try{test();add(name,true,"");}catch(Exception e){add(name,false,e.GetType().Name+": "+e.Message);}}
    }
}