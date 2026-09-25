Shader "SwapHunter/PortSky"
{
 Properties { _Zenith ("Zenith", Color) = (0.075,0.19,0.30,1) _Horizon ("Horizon", Color) = (0.52,0.64,0.69,1) _Ground ("Lower haze", Color) = (0.20,0.31,0.37,1) _SunDir ("Sun direction", Vector) = (-0.45,0.45,-0.65,0) }
 SubShader {
  Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
  Cull Off ZWrite Off
  Pass {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _Zenith, _Horizon, _Ground, _SunDir;
   CBUFFER_END
   struct Attributes { float4 positionOS : POSITION; };
   struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
   Varyings vert(Attributes input) { Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); o.direction = input.positionOS.xyz; return o; }
   half4 frag(Varyings input) : SV_Target {
    float3 d=normalize(input.direction); float elevation=saturate(d.y);
    half3 sky=lerp(_Horizon.rgb,_Zenith.rgb,pow(elevation,0.55));
    sky=lerp(sky,_Ground.rgb,saturate(-d.y*2.8));
    float alignment=saturate(dot(d,normalize(_SunDir.xyz)));
    sky+=half3(1.0,0.68,0.32)*(pow(alignment,48)*0.17+pow(alignment,950)*1.3);
    return half4(sky,1);
   }
   ENDHLSL
  }
 }
}
