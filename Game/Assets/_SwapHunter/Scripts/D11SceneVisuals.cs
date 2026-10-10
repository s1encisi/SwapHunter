using UnityEngine;
namespace SwapHunter
{
 // Kept as the stable binding used by authored chapter generation.
 public static class D11SceneVisuals
 {
  public static bool EnabledFor(int chapter)=>chapter>=0&&chapter<12;
  public static GameObject CreateModule(string key,Transform parent,Vector3 center,Vector3 size)=>FinalEnvironmentModels.Create(key,parent,center,size);
  public static void ReplaceProxy(GameObject proxy,string key,bool turnWall=false)=>FinalEnvironmentModels.Replace(proxy,key,turnWall?Quaternion.Euler(0,90,0):Quaternion.identity,key=="Wall_1");
 }
}
