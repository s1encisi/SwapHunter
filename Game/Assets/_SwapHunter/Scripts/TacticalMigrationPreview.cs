using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace SwapHunter
{
    [Serializable] public sealed class TacticalMigrationPreviewResult
    {
        public bool previewOnly=true,requiresManualReview=true,proposedCreditsKnown;
        public string sourceLabel,sourceSha256,exchangeRate="";
        public int sourceVersion,legacyCredits,highestUnlocked,proposedCredits;
        public int nominalSpecializationSpend,nominalRemovedRecallSpend;
        public bool verifiedSpecializationSpendKnown,verifiedRecallSpendKnown;
        public int verifiedSpecializationSpend,verifiedRecallSpend;
        public bool[] ownedWeapons,cleared,listedWeapons,weaponStarterSatisfied,skillStarterSatisfied;
        public int[] classSkillLevels,generalLevels,consumables,recommendedPrimary,recommendedSecondary,attachments;
        public float[] bestSeconds;
        public bool sharedCarbineSatisfied,sharedGeneralSatisfied;
        public ClassLoadout[] recommendedLoadouts;
        public List<ExpeditionReceipt> historicalReceipts;
        public List<TacticalPurchaseReceipt> purchases;
        public List<string> UnknownHistory=new List<string>();
        public List<string> Warnings=new List<string>();
    }

    // Text in, preview out. No file reads, Store construction, save, migration or import API.
    public static class TacticalMigrationPreview
    {
        public static TacticalMigrationPreviewResult Preview(string sourceJson,string sourceLabel,bool explicitlySelectedCopy,
            decimal? exchangeRate=null,int? verifiedSpecializationSpend=null,int? verifiedRecallSpend=null)
        {
            if(!explicitlySelectedCopy)throw new InvalidOperationException("只接受用户明确选择的旧档副本文本；不搜索或读取正式档");
            if(string.IsNullOrWhiteSpace(sourceJson)||string.IsNullOrWhiteSpace(sourceLabel))throw new ArgumentException("需要副本说明与完整JSON文本");
            if(exchangeRate.HasValue&&exchangeRate.Value<=0)throw new ArgumentOutOfRangeException(nameof(exchangeRate));
            if(verifiedSpecializationSpend.HasValue&&verifiedSpecializationSpend.Value<0)throw new ArgumentOutOfRangeException(nameof(verifiedSpecializationSpend));
            if(verifiedRecallSpend.HasValue&&verifiedRecallSpend.Value<0)throw new ArgumentOutOfRangeException(nameof(verifiedRecallSpend));
            foreach(string key in new[]{"version","credits","weapons","cleared","ranks","attachments","receipts"})
                if(!sourceJson.Contains("\""+key+"\""))throw new InvalidDataException("旧档缺少字段："+key+"；未补零或重置");
            var p=JsonUtility.FromJson<ExpeditionProfile>(sourceJson);
            if(p==null||(p.version!=4&&p.version!=5))throw new InvalidDataException("预览仅接受旧v4或T01 v5；未知版本保持原样");
            if(p.credits<0||p.highestUnlocked<0||p.highestUnlocked>11||p.weapons==null||p.weapons.Length!=12||p.cleared==null||p.cleared.Length!=12||p.attachments==null||p.attachments.Length!=12||p.ranks==null||p.ranks.Length!=3||p.bestSeconds==null||p.bestSeconds.Length!=12||p.knifeRank<0||p.knifeRank>3||p.receipts==null)throw new InvalidDataException("旧档结构无效；未生成可导入档案");
            foreach(int rank in p.ranks)if(rank<0||rank>3)throw new InvalidDataException("旧专精等级无效");
            foreach(int a in p.attachments)if(a<0||a>3)throw new InvalidDataException("旧配件无效");
            foreach(float t in p.bestSeconds)if(float.IsNaN(t)||float.IsInfinity(t)||t<0)throw new InvalidDataException("旧成绩字段无效");
            if(p.version==4)ExpeditionStore.Validate(p);
            if(p.version==5&&(p.medkitStock<0||p.mobilityLevel<0||p.mobilityLevel>1||p.shieldLevel<0||p.shieldLevel>1||p.classLoadouts==null||p.classLoadouts.Length!=4))throw new InvalidDataException("T01 v5字段不完整");
            if(p.version==5)
            {
                foreach(string key in new[]{"medkitStock","mobilityLevel","shieldLevel","classLoadouts","purchases"})
                    if(!sourceJson.Contains("\""+key+"\""))throw new InvalidDataException("T01副本缺少字段："+key);
                if(p.activeClass!=0||p.purchases==null)throw new InvalidDataException("T01副本职业或采购记录无效");
                foreach(var old in p.classLoadouts)
                    if(old==null||old.attachments==null||old.attachments.Length!=12||old.medkitCarry<0||old.medkitCarry>2)throw new InvalidDataException("T01职业预设不完整");
                if(!string.IsNullOrEmpty(p.activeRun)&&(p.deployment==null||p.deployment.runId!=p.activeRun||p.deployment.inventoryMedkits!=p.medkitStock))
                    throw new InvalidDataException("T01未结算快照不完整；无法可靠预览采购后库存");
            }
            var result=new TacticalMigrationPreviewResult {
                sourceLabel=sourceLabel,sourceVersion=p.version,legacyCredits=p.credits,highestUnlocked=p.highestUnlocked,
                ownedWeapons=(bool[])p.weapons.Clone(),cleared=(bool[])p.cleared.Clone(),bestSeconds=(float[])p.bestSeconds.Clone(),attachments=(int[])p.attachments.Clone(),
                listedWeapons=new bool[12],classSkillLevels=new int[8],generalLevels=new int[2],consumables=new int[3],
                recommendedPrimary=new int[4],recommendedSecondary=new int[4],weaponStarterSatisfied=new bool[4],skillStarterSatisfied=new bool[4]
            };
            using(var digest=SHA256.Create())result.sourceSha256=BitConverter.ToString(digest.ComputeHash(Encoding.UTF8.GetBytes(sourceJson))).Replace("-","").ToLowerInvariant();
            for(int i=0;i<12;i++)result.listedWeapons[i]=TacticalPrototypeRules.WeaponListed(i,p.cleared);
            if(p.version==4)
            {
                result.generalLevels[0]=1;result.generalLevels[1]=p.knifeRank==0?1:p.knifeRank==1?2:3;
                foreach(int rank in p.ranks)for(int tier=0;tier<rank;tier++)result.nominalSpecializationSpend=checked(result.nominalSpecializationSpend+ExpeditionCatalog.RankCost(tier));
                result.nominalRemovedRecallSpend=p.knifeRank==3?ExpeditionStore.KnifeUpgradeCost(2):0;
                result.Warnings.Add("旧飞刀0/1/2/3映射新1/2/3/3；旧A盾映射1级。保留沉默效果，不返还全部飞刀费用。");
                result.Warnings.Add("三项旧专精建议退出效果链；等级推算的名义价格不证明历史实付。旧专精不是新职业。");
            }
            else
            {
                result.classSkillLevels[1]=p.mobilityLevel;result.generalLevels[0]=p.shieldLevel;result.consumables[0]=p.medkitStock;
                result.Warnings.Add("T01医疗库存、快速机动/A盾等级与余额仅做映射预览；不补发更高的新首通奖励。");
            }
            for(int c=0;c<4;c++)
            {
                result.weaponStarterSatisfied[c]=p.weapons[TacticalPrototypeRules.StarterWeapon(c)];
                result.skillStarterSatisfied[c]=result.classSkillLevels[c*2]>0||result.classSkillLevels[c*2+1]>0;
                int oldPrimary=p.primary,oldSecondary=p.secondary;
                if(p.version==5&&p.classLoadouts[c]!=null){oldPrimary=p.classLoadouts[c].primary;oldSecondary=p.classLoadouts[c].secondary;}
                if(EligibleOwned(p,c,oldPrimary)&&EligibleOwned(p,c,oldSecondary)&&oldPrimary!=oldSecondary)
                {result.recommendedPrimary[c]=oldPrimary;result.recommendedSecondary[c]=oldSecondary;}
                else
                {
                    int first=-1,second=-1;
                    for(int w=0;w<12;w++)if(EligibleOwned(p,c,w)){if(first<0)first=w;else if(second<0)second=w;}
                    result.recommendedPrimary[c]=first;result.recommendedSecondary[c]=second;
                    result.Warnings.Add(TacticalPrototypeRules.ClassNames[c]+": 旧配装不满足该职业双枪资格，建议保留资产后重选；-1表示缺少合法已拥有武器，不偷偷授予。");
                }
            }
            result.sharedCarbineSatisfied=p.weapons[0];
            result.sharedGeneralSatisfied=result.generalLevels[0]>0||result.generalLevels[1]>0;
            result.recommendedLoadouts=new ClassLoadout[4];
            for(int c=0;c<4;c++)
            {
                var sourceLoadout=p.version==5?p.classLoadouts[c]:null;
                int selectedClass=-1;for(int i=c*2;i<c*2+2;i++)if(result.classSkillLevels[i]>0){selectedClass=i;break;}
                int selectedGeneral=result.generalLevels[0]>0?0:result.generalLevels[1]>0?1:-1;
                int carry=p.version==5?sourceLoadout.medkitCarry:0;
                result.recommendedLoadouts[c]=new ClassLoadout {
                    classId=c,primary=result.recommendedPrimary[c]>=0?result.recommendedPrimary[c]:0,
                    secondary=result.recommendedSecondary[c]>=0?result.recommendedSecondary[c]:TacticalPrototypeRules.StarterWeapon(c),
                    attachments=(int[])(sourceLoadout!=null?sourceLoadout.attachments:p.attachments).Clone(),
                    itemTypes=new[]{carry>0?0:-1,-1},itemCounts=new[]{carry,0},medkitCarry=carry,
                    classSkillId=selectedClass<0?"":TacticalPrototypeRules.SkillIds[selectedClass],
                    tacticalSkillId=selectedGeneral<0?"":TacticalPrototypeRules.GeneralSkillIds[selectedGeneral],
                    starterWeaponClaimed=result.weaponStarterSatisfied[c],starterSkillClaimed=result.skillStarterSatisfied[c],
                    starterSkillId=selectedClass<0?"":TacticalPrototypeRules.SkillIds[selectedClass]
                };
            }
            result.historicalReceipts=new List<ExpeditionReceipt>();
            foreach(var old in p.receipts)
            {
                if(old==null)throw new InvalidDataException("旧结算记录含空项");
                var historical=JsonUtility.FromJson<ExpeditionReceipt>(JsonUtility.ToJson(old));
                historical.historical=true;historical.sourceVersion=p.version;result.historicalReceipts.Add(historical);
            }
            result.purchases=new List<TacticalPurchaseReceipt>();
            if(p.version==5)foreach(var old in p.purchases)
            {
                if(old==null)throw new InvalidDataException("T01采购记录含空项");
                var receipt=JsonUtility.FromJson<TacticalPurchaseReceipt>(JsonUtility.ToJson(old));
                receipt.kind="consumable";receipt.productId=0;result.purchases.Add(receipt);
            }
            if(p.version==5)
            {
                // T01 already uses the same credit currency and has no removed paid specialization/recall gates.
                if(exchangeRate.HasValue||verifiedSpecializationSpend.HasValue||verifiedRecallSpend.HasValue)
                    result.Warnings.Add("T01已是信用点：忽略仅适用于v4研究点的r/S/D覆盖参数，保持原额与已购库存。");
                result.exchangeRate="1";result.proposedCredits=p.credits;result.proposedCreditsKnown=true;
                result.verifiedSpecializationSpendKnown=result.verifiedRecallSpendKnown=true;
            }
            else
            {
                result.verifiedSpecializationSpendKnown=verifiedSpecializationSpend.HasValue;
                result.verifiedRecallSpendKnown=verifiedRecallSpend.HasValue;
                if(verifiedSpecializationSpend.HasValue)result.verifiedSpecializationSpend=verifiedSpecializationSpend.Value;
                else result.UnknownHistory.Add("缺少可核实的实际旧专精投入S；名义价目算例不代替历史付款记录。");
                if(verifiedRecallSpend.HasValue)result.verifiedRecallSpend=verifiedRecallSpend.Value;
                else result.UnknownHistory.Add("缺少可核实的被取消召回门槛实际投入D；未知不按0填写。");
                if(!exchangeRate.HasValue)result.UnknownHistory.Add("研究点兑换比例r尚未确认。");
                else result.exchangeRate=exchangeRate.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if(exchangeRate.HasValue&&verifiedSpecializationSpend.HasValue&&verifiedRecallSpend.HasValue)
                {
                    decimal money=exchangeRate.Value*((decimal)p.credits+verifiedSpecializationSpend.Value+verifiedRecallSpend.Value);
                    if(money>int.MaxValue||money!=decimal.Truncate(money))result.UnknownHistory.Add("兑换结果超出整数信用点范围或需要另行确认取整规则。");
                    else {result.proposedCredits=(int)money;result.proposedCreditsKnown=true;}
                }
            }
            result.UnknownHistory.Add("单份Profile不能证明来源为正式玩家档而非演示/QA预置；导入前须核对副本来源。");
            if(!string.IsNullOrEmpty(p.activeRun))result.Warnings.Add("副本存在未结算行动。预览不执行旧Store恢复、不提交奖励，需人工核对采购后库存。");
            result.Warnings.Add("只输出差异预览；实际导入需另行确认、备份与幂等迁移标记。原始数据没有被读取之外的操作。");
            result.Warnings.Add("已拥有但尚未上架的武器建议继续保留；章节首通标记保留，不因新职业或更高奖励重发首通。");
            return result;
        }
        public static ExpeditionProfile BuildCandidate(TacticalMigrationPreviewResult preview,bool approved)
        {
            if(!approved)throw new InvalidOperationException("候选构造需要明确审阅同意；不会写入正式目录");
            if(preview==null||!preview.previewOnly||!preview.proposedCreditsKnown||!preview.verifiedSpecializationSpendKnown||!preview.verifiedRecallSpendKnown
                ||(preview.sourceVersion!=4&&preview.sourceVersion!=5)||string.IsNullOrEmpty(preview.sourceSha256))
                throw new InvalidOperationException("预览来源、映射或余额补偿尚未明确，不能构造候选");
            var copy=JsonUtility.FromJson<TacticalMigrationPreviewResult>(JsonUtility.ToJson(preview));
            var p=ExpeditionStore.NewTacticalProfile();
            p.credits=copy.proposedCredits;p.highestUnlocked=copy.highestUnlocked;p.weapons=copy.ownedWeapons;p.cleared=copy.cleared;p.bestSeconds=copy.bestSeconds;
            p.classSkillLevels=copy.classSkillLevels;p.generalLevels=copy.generalLevels;p.consumables=copy.consumables;p.classLoadouts=copy.recommendedLoadouts;
            p.sharedCarbineClaimed=copy.sharedCarbineSatisfied;p.sharedTacticalClaimed=copy.sharedGeneralSatisfied;
            p.starterGeneralId=p.generalLevels!=null&&p.generalLevels.Length==2?(p.generalLevels[0]>0?TacticalPrototypeRules.GeneralSkillIds[0]:p.generalLevels[1]>0?TacticalPrototypeRules.GeneralSkillIds[1]:""):"";
            p.receipts=copy.historicalReceipts;p.purchases=copy.purchases;
            if(p.classLoadouts!=null&&p.classLoadouts.Length==4&&p.classLoadouts[0]!=null)
            {p.primary=p.classLoadouts[0].primary;p.secondary=p.classLoadouts[0].secondary;p.attachments=p.classLoadouts[0].attachments;}
            if(p.consumables!=null&&p.consumables.Length==3)p.medkitStock=p.consumables[0];
            if(p.classSkillLevels!=null&&p.classSkillLevels.Length==8)p.mobilityLevel=p.classSkillLevels[1];
            if(p.generalLevels!=null&&p.generalLevels.Length==2)p.shieldLevel=p.generalLevels[0];
            ExpeditionStore.Validate(p);return p;
        }
        static bool EligibleOwned(ExpeditionProfile p,int role,int weapon)=>TacticalPrototypeRules.ClassCanUseWeapon(role,weapon)&&p.weapons[weapon];
    }
}