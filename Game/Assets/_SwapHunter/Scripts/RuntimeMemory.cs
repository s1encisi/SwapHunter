using System;
using System.Runtime.InteropServices;
using UnityEngine;
namespace SwapHunter
{
 public static class RuntimeMemory
 {
  [StructLayout(LayoutKind.Sequential)] struct Counters
  {public uint cb,pageFaults;public UIntPtr peakWorkingSet,workingSet,peakPaged,paged,peakNonPaged,nonPaged,pagefile,peakPagefile;}
  [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
  [DllImport("psapi.dll",SetLastError=true)] static extern bool GetProcessMemoryInfo(IntPtr process,ref Counters counters,uint size);
  public static long WorkingSet()
  {
   try
   {
    if(Application.platform==RuntimePlatform.WindowsPlayer||Application.platform==RuntimePlatform.WindowsEditor)
    {var c=new Counters{cb=(uint)Marshal.SizeOf(typeof(Counters))};if(GetProcessMemoryInfo(GetCurrentProcess(),ref c,c.cb)){long bytes=(long)c.workingSet.ToUInt64();if(bytes>0)return bytes;}}
    using(var process=System.Diagnostics.Process.GetCurrentProcess()){process.Refresh();long bytes=process.WorkingSet64;return bytes>0?bytes:-1;}
   }
   catch{return -1;}
  }
 }
}
