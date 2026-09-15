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
  _OrnamentWeathering("Ornament weathering",Range(0,1))=0
  _PatinaColor("Faded glaze or patina",Color)=(.2,.4,.34,1)
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
    float4 _BaseColor,_JointColor,_BlockSize,_PatinaColor;float _OrnamentWeathering;float _JointWidth,_Variation,_Masonry,_WeatherScale,_BumpStrength,_Metallic,_Smoothness,_IsSand;
   CBUFFER_END
   struct A{float4 p:POSITION;float3 n:NORMAL;};
   struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 n:TEXCOORD1;float fog:TEXCOORD2;};
   V vert(A i){V o;VertexPositionInputs p=GetVertexPositionInputs(i.p.xyz);o.p=p.positionCS;o.fog=ComputeFogFactor(p.positionCS.z);o.world=p.positionWS;o.n=TransformObjectToWorldNormal(i.n);return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float stoneNoise(float2 p){
    float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(hash(c),hash(c+float2(1,0)),f.x),lerp(hash(c+float2(0,1)),hash(c+1),f.x),f.y);
   }
   half4 frag(V i):SV_Target {
    float3 n=normalize(i.n),an=abs(n);
    float2 uv=an.y>.7?i.world.xz:float2(an.z>an.x?i.world.x:i.world.z,i.world.y);
    float3 weather=SAMPLE_TEXTURE2D(_WeatherMap,sampler_WeatherMap,uv*_WeatherScale).rgb;
    float2 size=an.y>.7?_BlockSize.xy*float2(1,1.7):_BlockSize.xy;
    float row=floor(uv.y/size.y);size.x*=lerp(.72,1.35,hash(float2(row,17)));
    float2 grid=uv/size;grid.x+=hash(float2(row,29))*.8;
    // Jitter shared boundaries, rather than assigning disconnected tile sizes.
    float column=floor(grid.x);
    float start=column+(hash(float2(column,row))-.5)*.34;
    if(grid.x<start)column-=1;
    else if(grid.x>column+1+(hash(float2(column+1,row))-.5)*.34)column+=1;
    start=column+(hash(float2(column,row))-.5)*.34;
    float finish=column+1+(hash(float2(column+1,row))-.5)*.34;
    float2 cell=float2(column,row),p=float2((grid.x-start)*size.x,frac(grid.y)*size.y);
    float2 tileSize=float2((finish-start)*size.x,size.y);
    float2 edge=min(p,tileSize-p);float aa=max(fwidth(uv.x),fwidth(uv.y));
    float edgeDistance=min(edge.x,edge.y);
    float erosion=stoneNoise(uv*7.3+31)*stoneNoise(uv*2.1);
    float chippedEdge=edgeDistance-(smoothstep(.38,.72,erosion)*.065);
    float joint=1-smoothstep(_JointWidth,_JointWidth+max(.014,aa),chippedEdge);
    // Sparse, differently angled hairline cracks; avoid the same vertical mark on every wall.
    float seed=hash(cell+9);
    float cx=tileSize.x*(.22+seed*.56)+(p.y-tileSize.y*.5)*(seed-.5)*.55;
    cx+=(stoneNoise(float2(p.y*6,seed*70))-.5)*.09;
    float crack=(1-smoothstep(.004,.012+aa,abs(p.x-cx)))*step(.91,hash(cell+3))*smoothstep(.05,.3,p.y/tileSize.y)*smoothstep(.05,.35,1-p.y/tileSize.y);
    float wear=(1-smoothstep(.025,.13,chippedEdge))*(.35+.65*stoneNoise(uv*3));
    float mottling=(weather.r-.5)*.18+(weather.g-.5)*.04;
    float variation=(hash(cell)-.5)*_Variation*1.9;
    float3 stoneTint=lerp(float3(1.04,1.015,.95),float3(.945,.975,1.025),hash(cell+43));
    float3 albedo=_BaseColor.rgb*(1+mottling+variation*_Masonry);
    albedo*=lerp(float3(1,1,1),stoneTint,_Masonry);
    albedo=lerp(albedo,_JointColor.rgb,saturate(joint*.62+crack*.48)*_Masonry);
    albedo=lerp(albedo,_BaseColor.rgb*1.11,wear*.32*_Masonry);
    // Dust settles mostly in paving seams; it is shading only, with no displaced geometry.
    float dust=(1-smoothstep(.035,.24,edgeDistance))*smoothstep(.4,.75,stoneNoise(uv*.65+19))*step(.7,an.y)*_Masonry;
    albedo=lerp(albedo,_BaseColor.rgb*float3(1.025,1.01,.965),dust*.42);
    albedo=lerp(albedo,SAMPLE_TEXTURE2D(_SurfaceMap,sampler_SurfaceMap,uv*.1).rgb*_BaseColor.rgb,_IsSand);
    float height=(weather.g-.5)*.016+(weather.b-.5)*.002-(joint*.018+crack*.008+wear*.009)*_Masonry;
    // Broad faded patches, fine wear and small differences in metal polish.
    // Only explicitly opted-in ornament materials use this treatment.
    float patina=smoothstep(.35,.7,stoneNoise(uv*1.15+53))*(.55+.45*stoneNoise(uv*4.2+7))*_OrnamentWeathering;
    float polish=smoothstep(.65,.87,stoneNoise(uv*6.5+11))*_OrnamentWeathering;
    albedo=lerp(albedo,_PatinaColor.rgb,patina*.65);
    albedo=lerp(albedo,_BaseColor.rgb*1.16,polish*.35);
    float3 ds=ddx(i.world),dt=ddy(i.world),r1=cross(dt,n),r2=cross(n,ds);float determinant=dot(ds,r1);
    float3 gradient=sign(determinant)*(ddx(height)*r1+ddy(height)*r2);
    n=normalize(abs(determinant)*n-_BumpStrength*gradient+1e-8*n);
    InputData d=(InputData)0;d.positionWS=i.world;d.normalWS=n;d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);d.shadowCoord=TransformWorldToShadowCoord(i.world);d.bakedGI=max(SampleSH(n),half3(.16,.18,.22));d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);d.shadowMask=1;
    SurfaceData s=(SurfaceData)0;s.albedo=albedo;s.alpha=1;s.occlusion=1-joint*_Masonry*.12;s.smoothness=saturate(_Smoothness*(1-wear*.45*_Masonry)*(1-patina*.55)+polish*.08);s.metallic=_Metallic*(1-patina*.8);s.emission=albedo*.018;
        half4 result=UniversalFragmentPBR(d,s);result.rgb=MixFog(result.rgb,i.fog);return result;
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}
