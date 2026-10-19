Shader "SwapHunter/FocusedSurface"
{
 Properties { _BaseMap("CC0 base color",2D)="white"{} _NormalMap("CC0 OpenGL normal",2D)="bump"{} _RoughnessMap("CC0 roughness",2D)="white"{} _BaseColor("Tint",Color)=(1,1,1,1) _TileSize("Real metres per repeat",Float)=2 _NormalStrength("Normal detail",Range(0,1))=.5 _Metallic("Metal",Range(0,1))=0 _SmoothBias("Smoothness offset",Range(-.5,.5))=0 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
  Pass
  {
   Name "FocusedForward" Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   TEXTURE2D(_NormalMap);SAMPLER(sampler_NormalMap);
   TEXTURE2D(_RoughnessMap);SAMPLER(sampler_RoughnessMap);
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor;float _TileSize;half _NormalStrength,_Metallic,_SmoothBias;
   CBUFFER_END
   struct Attributes { float4 positionOS:POSITION;float3 normalOS:NORMAL; };
   struct Varyings { float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float4 shadowCoord:TEXCOORD2;half fog:TEXCOORD3; };
   Varyings vert(Attributes v){Varyings o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.shadowCoord=GetShadowCoord(p);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
   half4 frag(Varyings i):SV_Target
   {
    half3 n=normalize(i.normalWS),weights=pow(abs(n),4);weights/=max(.0001,weights.x+weights.y+weights.z);float3 p=i.positionWS/max(.1,_TileSize);
    half3 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.zy).rgb*weights.x+SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz).rgb*weights.y+SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy).rgb*weights.z;
    half rough=SAMPLE_TEXTURE2D(_RoughnessMap,sampler_RoughnessMap,p.zy).r*weights.x+SAMPLE_TEXTURE2D(_RoughnessMap,sampler_RoughnessMap,p.xz).r*weights.y+SAMPLE_TEXTURE2D(_RoughnessMap,sampler_RoughnessMap,p.xy).r*weights.z;
    half3 nx=SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,p.zy).rgb*2-1,ny=SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,p.xz).rgb*2-1,nz=SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,p.xy).rgb*2-1;
    nx.xy*=_NormalStrength;ny.xy*=_NormalStrength;nz.xy*=_NormalStrength;
    half3 normal=normalize(half3(nx.z*sign(n.x),nx.y,nx.x)*weights.x+half3(ny.x,ny.z*sign(n.y),ny.y)*weights.y+half3(nz.x,nz.y,nz.z*sign(n.z))*weights.z);
    InputData input=(InputData)0;input.positionWS=i.positionWS;input.normalWS=normal;input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);input.shadowCoord=i.shadowCoord;input.fogCoord=i.fog;input.vertexLighting=VertexLighting(i.positionWS,normal);input.bakedGI=SampleSH(normal);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);input.shadowMask=half4(1,1,1,1);
    SurfaceData surface=(SurfaceData)0;surface.albedo=color*_BaseColor.rgb;surface.metallic=_Metallic;surface.specular=half3(.04,.04,.04);surface.smoothness=saturate(1-rough+_SmoothBias);surface.normalTS=half3(0,0,1);surface.occlusion=1;surface.alpha=1;
    half4 result=UniversalFragmentPBR(input,surface);result.rgb=MixFog(result.rgb,i.fog);return result;
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
