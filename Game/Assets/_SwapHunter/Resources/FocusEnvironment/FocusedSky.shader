Shader "SwapHunter/FocusedSky"
{
 Properties { _Sky("CC0 HDR panorama",2D)="grey"{} _Exposure("Exposure",Float)=.7 _Rotation("Yaw degrees",Float)=120 }
 SubShader
 {
  Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" } Cull Off ZWrite Off
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_Sky);SAMPLER(sampler_Sky);CBUFFER_START(UnityPerMaterial) float _Exposure,_Rotation;CBUFFER_END
   struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 d:TEXCOORD0;};
   V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.d=i.p.xyz;return o;}
   half4 frag(V i):SV_Target {float3 d=normalize(i.d);float2 uv=float2(atan2(d.z,d.x)/(2*PI)+.5+_Rotation/360,asin(clamp(d.y,-1,1))/PI+.5);return half4(SAMPLE_TEXTURE2D(_Sky,sampler_Sky,uv).rgb*_Exposure,1);}
   ENDHLSL
  }
 }
}
