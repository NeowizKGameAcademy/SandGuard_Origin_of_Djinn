Shader "SandGuard/Architecture/Weathered Stone"
{
 Properties {
  _BaseColor("Stone",Color)=(.68,.53,.34,1)
  _JointColor("Recesses",Color)=(.36,.27,.16,1)
  _BlockSize("Block width / height",Vector)=(4.8,2.2,0,0)
  _JointWidth("Mortar width",Float)=.025
  _Variation("Stone variation",Range(0,1))=.12
  _Masonry("Masonry courses",Range(0,1))=1
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass {
   Tags {"LightMode"="UniversalForwardOnly"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor,_JointColor,_BlockSize;float _JointWidth,_Variation,_Masonry;
   CBUFFER_END
   struct A {float4 p:POSITION;float3 n:NORMAL;};
   struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 n:TEXCOORD1;};
   V vert(A i){V o;VertexPositionInputs p=GetVertexPositionInputs(i.p.xyz);o.p=p.positionCS;o.world=p.positionWS;o.n=TransformObjectToWorldNormal(i.n);return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
   half4 frag(V i):SV_Target {
    float3 n=normalize(i.n);float3 an=abs(n);
    float2 uv=an.y>.7?i.world.xz:float2(an.z>an.x?i.world.x:i.world.z,i.world.y);
    float2 grid=uv/_BlockSize.xy;grid.x+=fmod(floor(grid.y),2)*.5;
    float2 cell=floor(grid);float2 edge=min(frac(grid),1-frac(grid))*_BlockSize.xy;
    float aa=max(fwidth(uv.x),fwidth(uv.y));
    float mortar=1-smoothstep(_JointWidth,_JointWidth+aa,min(edge.x,edge.y));
    float grain=(noise(uv*24)-.5)*.05;
    float stain=(noise(uv*.32)-.5)*.14+(noise(uv*2)-.5)*.065;
    float3 albedo=_BaseColor.rgb*(1+(hash(cell)-.5)*_Variation+grain+stain);
    albedo=lerp(albedo,_JointColor.rgb,mortar*_Masonry*.42);
    InputData d=(InputData)0;d.positionWS=i.world;d.normalWS=n;d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);
    d.shadowCoord=TransformWorldToShadowCoord(i.world);d.bakedGI=SampleSH(n);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);d.shadowMask=half4(1,1,1,1);
    SurfaceData s=(SurfaceData)0;s.albedo=albedo;s.alpha=1;s.occlusion=1;s.smoothness=.14;
    s.emission=albedo*half3(.025,.023,.02);
    return UniversalFragmentPBR(d,s);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}
