using System;
using System.Globalization;
using System.IO;
using UnityEngine;
namespace SwapHunter
{
 public static class MigrationPreviewCommand
 {
  static string Value(string[] args,string key,bool required=false)
  {
   int at=Array.IndexOf(args,key);
   if(at<0){if(required)throw new ArgumentException("缺少参数 "+key);return null;}
   if(at+1>=args.Length||args[at+1].StartsWith("-"))throw new ArgumentException("参数需要值："+key);
   return args[at+1];
  }
  public static bool TryRun(out int exitCode)
  {
   exitCode=0;var args=Environment.GetCommandLineArgs();
   if(Array.IndexOf(args,"-migrationCopy")<0)return false;
   try
   {
    string copy=Path.GetFullPath(Value(args,"-migrationCopy",true));
    string output=Path.GetFullPath(Value(args,"-migrationOutput",true));
    string formal=Path.GetFullPath(Application.persistentDataPath).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
    if(copy.StartsWith(formal,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("请先复制旧档到单独目录；预览入口不直接读取正式存档路径");
    if(Directory.Exists(output)||File.Exists(output))throw new IOException("输出目录必须是新目录");
    string r=Value(args,"-exchangeRate"),s=Value(args,"-verifiedSpecializationSpend"),d=Value(args,"-verifiedRecallSpend");
    decimal? rate=r==null?(decimal?)null:decimal.Parse(r,CultureInfo.InvariantCulture);
    int? spec=s==null?(int?)null:int.Parse(s,CultureInfo.InvariantCulture);
    int? recall=d==null?(int?)null:int.Parse(d,CultureInfo.InvariantCulture);
    var preview=TacticalMigrationPreview.Preview(File.ReadAllText(copy),Path.GetFileName(copy),true,rate,spec,recall);
    Directory.CreateDirectory(output);
    File.WriteAllText(Path.Combine(output,"migration-preview.json"),JsonUtility.ToJson(preview,true));
    string summary="# 旧档迁移预览\n\n来源副本："+Path.GetFileName(copy)+"\n\n源SHA256："+preview.sourceSha256+"\n\n原版本："+preview.sourceVersion+"；目标版本：6。原文件未修改，未导入正式进度。\n\n";
    summary+=preview.proposedCreditsKnown?"候选信用点："+preview.proposedCredits+"\n\n":"信用点补偿尚不能确定，未补零。\n\n";
    foreach(string unknown in preview.UnknownHistory)summary+="- 待确认："+unknown+"\n";
    foreach(string warning in preview.Warnings)summary+="- 说明："+warning+"\n";
    if(Array.IndexOf(args,"-approvedCandidate")>=0)
    {
     var candidate=TacticalMigrationPreview.BuildCandidate(preview,true);
     File.WriteAllText(Path.Combine(output,"candidate-expedition-v6.json"),JsonUtility.ToJson(candidate,true));
     summary+="\n已按显式选项生成候选副本，仍未写入游戏档案目录。\n";
    }
    File.WriteAllText(Path.Combine(output,"开始阅读.md"),summary);
    Debug.Log("SWAPHUNTER_MIGRATION_PREVIEW="+output);
   }
   catch(Exception error){Debug.LogError("迁移预览未完成："+error.Message);exitCode=2;}
   return true;
  }
 }
}