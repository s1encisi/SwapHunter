#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace SwapHunter
{
 public sealed class FocusEnvironmentImporter:AssetPostprocessor
 {
  void OnPreprocessTexture()
  {
   if(!assetPath.StartsWith("Assets/_SwapHunter/Resources/FocusEnvironment/"))return;
   var importer=(TextureImporter)assetImporter;importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;importer.sRGBTexture=assetPath.Contains("_color.");importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;importer.maxTextureSize=assetPath.EndsWith(".hdr")?2048:1024;importer.textureCompression=TextureImporterCompression.CompressedHQ;
  }
 }
}
#endif
