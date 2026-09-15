Shader "SandGuard/Architecture/Courtyard Stone"
{
 Properties {
  _BaseColor("Stone tint",Color)=(.7,.56,.39,1)
  _JointColor("Stone joints",Color)=(.35,.27,.18,1)
  _BlockSize("Block size",Vector)=(2.8,1.25,0,0)
  _JointWidth("Joint width",Float)=.023
  _Variation("Stone variation",Range(0,1))=.17
  _Masonry("Masonry",Range(0,1))=1
  _WeatherMap("Blender baked erosion",2D)="gray"{}
  _WeatherScale("Weather scale",Float)=.18
  _BumpStrength("Relief",Float)=.8
  _Metallic("Metallic",Range(0,1))=0
  _Smoothness("Smoothness",Range(0,1))=.1
  _SurfaceMap("Sand base color",2D)="white"{}
  _IsSand("Sand surface",Float)=0
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
   TEXTURE2D(_WeatherMap);SAMPLER(sampler_WeatherMap);
   TEXTURE2D(_SurfaceMap);SAMPLER(sampler_SurfaceMap);
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor,_JointColor,_BlockSize;float _JointWidth,_Variation,_Masonry,_WeatherScale,_BumpStrength,_Metallic,_Smoothness,_IsSand;
   CBUFFER_END
   struct A{float4 p:POSITION;float3 n:NORMAL;};
   struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 n:TEXCOORD1;float fog:TEXCOORD2;};
   V vert(A i){V o;VertexPositionInputs p=GetVertexPositionInputs(i.p.xyz);o.p=p.positionCS;o.fog=ComputeFogFactor(p.positionCS.z);o.world=p.positionWS;o.n=TransformObjectToWorldNormal(i.n);return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   half4 frag(V i):SV_Target {
    float3 n=normalize(i.n),an=abs(n);
    float2 uv=an.y>.7?i.world.xz:float2(an.z>an.x?i.world.x:i.world.z,i.world.y);
    float3 weather=SAMPLE_TEXTURE2D(_WeatherMap,sampler_WeatherMap,uv*_WeatherScale).rgb;
    float2 size=an.y>.7?_BlockSize.xy*float2(1,1.7):_BlockSize.xy;
    float row=floor(uv.y/size.y);size.x*=lerp(.72,1.35,hash(float2(row,17)));
    float2 grid=uv/size;grid.x+=hash(float2(row,29))*.8;
    float2 cell=floor(grid),p=frac(grid)*size;
    float2 edge=min(p,size-p);float aa=max(fwidth(uv.x),fwidth(uv.y));
    float joint=1-smoothstep(_JointWidth,_JointWidth+max(.012,aa),min(edge.x,edge.y)+(weather.g-.5)*.025);
    float cx=size.x*(.25+hash(cell+9)*.5)+sin(p.y*13+hash(cell)*12)*.055+sin(p.y*29)*.014;
    float crack=(1-smoothstep(.006,.014+aa,abs(p.x-cx)))*step(.87,hash(cell+3))*smoothstep(.05,.25,p.y/size.y)*smoothstep(0,.18,1-p.y/size.y);
    float mottling=(weather.r-.5)*.24+(weather.g-.5)*.055;
    float3 albedo=_BaseColor.rgb*(1+mottling+(hash(cell)-.5)*_Variation*_Masonry);
    albedo*=lerp(float3(1.025,.985,.94),float3(.96,1.0,1.035),hash(cell+43))*lerp(1,1.07,(1-smoothstep(.035,.12,min(edge.x,edge.y)))*_Masonry);
    albedo=lerp(albedo,_JointColor.rgb,saturate(joint*.38+crack*.38)*_Masonry);
    albedo=lerp(albedo,SAMPLE_TEXTURE2D(_SurfaceMap,sampler_SurfaceMap,uv*.1).rgb*_BaseColor.rgb,_IsSand);
    float height=(weather.g-.5)*.016+(weather.b-.5)*.002-(joint*.012+crack*.008)*_Masonry;
    float3 ds=ddx(i.world),dt=ddy(i.world),r1=cross(dt,n),r2=cross(n,ds);float determinant=dot(ds,r1);
    float3 gradient=sign(determinant)*(ddx(height)*r1+ddy(height)*r2);
    n=normalize(abs(determinant)*n-_BumpStrength*gradient+1e-8*n);
    InputData d=(InputData)0;d.positionWS=i.world;d.normalWS=n;d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);d.shadowCoord=TransformWorldToShadowCoord(i.world);d.bakedGI=max(SampleSH(n),half3(.16,.18,.22));d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);d.shadowMask=1;
    SurfaceData s=(SurfaceData)0;s.albedo=albedo;s.alpha=1;s.occlusion=1-joint*_Masonry*.12;s.smoothness=_Smoothness;s.metallic=_Metallic;s.emission=albedo*.018;
        half4 result=UniversalFragmentPBR(d,s);result.rgb=MixFog(result.rgb,i.fog);return result;
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}
