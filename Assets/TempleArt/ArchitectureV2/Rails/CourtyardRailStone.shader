Shader "SandGuard/Architecture/Courtyard Rail Stone"
{
 Properties {
  _BaseMap("Original rail trim",2D)="white"{}
  _BaseColor("Courtyard sandstone tint",Color)=(.76,.645,.47,1)
  _DetailContrast("Preserved trim contrast",Range(0,2))=.7
  _TextureMidpoint("Linear texture midpoint",Range(0,1))=.35
  _Metallic("Metallic",Range(0,1))=0
  _Smoothness("Smoothness",Range(0,1))=.1
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass {
   Tags {"LightMode"="UniversalForwardOnly"}
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor,_BaseMap_ST;
    float _DetailContrast,_TextureMidpoint,_Metallic,_Smoothness;
   CBUFFER_END
   struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
   struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 n:TEXCOORD1;float fog:TEXCOORD2;float2 uv:TEXCOORD3;};
   V vert(A i){V o;VertexPositionInputs p=GetVertexPositionInputs(i.p.xyz);o.p=p.positionCS;o.fog=ComputeFogFactor(p.positionCS.z);o.world=p.positionWS;o.n=TransformObjectToWorldNormal(i.n);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);return o;}
   half4 frag(V i):SV_Target {
    float3 n=normalize(i.n);
    // Preserve the authored trim layout and luminance detail, but replace its
    // orange hue with the same sandstone palette used by courtyard walls.
    float3 trim=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
    float luminance=dot(trim,float3(.2126,.7152,.0722));
    float value=clamp(1+(luminance-_TextureMidpoint)*_DetailContrast,.72,1.18);
    float3 albedo=_BaseColor.rgb*value;
    InputData d=(InputData)0;d.positionWS=i.world;d.normalWS=n;d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);d.shadowCoord=TransformWorldToShadowCoord(i.world);d.bakedGI=max(SampleSH(n),half3(.16,.18,.22));d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);d.shadowMask=1;
    SurfaceData s=(SurfaceData)0;s.albedo=albedo;s.alpha=1;s.occlusion=1;s.smoothness=_Smoothness;s.metallic=_Metallic;s.emission=albedo*.018;
        half4 result=UniversalFragmentPBR(d,s);result.rgb=MixFog(result.rgb,i.fog);return result;
   }
   ENDHLSL
  }
  Pass {
   Name "ShadowCaster"
   Tags {"LightMode"="ShadowCaster"}
   ZWrite On ZTest LEqual ColorMask 0
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex ShadowVert
   #pragma fragment ShadowFrag
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   #include "Assets/TempleArt/ArchitectureV2/CourtyardStonePasses.hlsl"
   ENDHLSL
  }
  Pass {
   Name "DepthOnly"
   Tags {"LightMode"="DepthOnly"}
   ZWrite On ColorMask R
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex DepthVert
   #pragma fragment DepthFrag
   #include "Assets/TempleArt/ArchitectureV2/CourtyardStonePasses.hlsl"
   ENDHLSL
  }
  Pass {
   Name "DepthNormals"
   Tags {"LightMode"="DepthNormals"}
   ZWrite On
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex NormalsVert
   #pragma fragment NormalsFrag
   #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
   #include "Assets/TempleArt/ArchitectureV2/CourtyardStonePasses.hlsl"
   ENDHLSL
  }
 }
}
