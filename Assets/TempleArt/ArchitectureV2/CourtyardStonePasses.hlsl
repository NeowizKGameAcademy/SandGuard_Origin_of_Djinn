// Depth / shadow / normals programs shared by the courtyard stone shaders.
// Written here instead of UsePass "Universal Render Pipeline/Lit/..." because borrowing Lit's
// passes drags Lit's keyword space into the material and Unity asserts
// "State comes from an incompatible keyword space" on first render. Opaque, no alpha clip.
#ifndef COURTYARD_STONE_PASSES
#define COURTYARD_STONE_PASSES
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

struct StoneAttributes{float4 p:POSITION;float3 n:NORMAL;};
struct StoneNormals{float4 p:SV_POSITION;float3 n:TEXCOORD0;};

// ShadowCaster — same bias rules as URP's ShadowCasterPass.hlsl.
float3 _LightDirection;
float3 _LightPosition;
float4 ShadowVert(StoneAttributes i):SV_POSITION{
 float3 ws=TransformObjectToWorld(i.p.xyz);float3 n=TransformObjectToWorldNormal(i.n);
 #if _CASTING_PUNCTUAL_LIGHT_SHADOW
 float3 dir=normalize(_LightPosition-ws);
 #else
 float3 dir=_LightDirection;
 #endif
 float4 cs=TransformWorldToHClip(ApplyShadowBias(ws,n,dir));
 #if UNITY_REVERSED_Z
 cs.z=min(cs.z,UNITY_NEAR_CLIP_VALUE);
 #else
 cs.z=max(cs.z,UNITY_NEAR_CLIP_VALUE);
 #endif
 return cs;
}
half4 ShadowFrag():SV_Target{return 0;}

// DepthOnly
float4 DepthVert(StoneAttributes i):SV_POSITION{return TransformObjectToHClip(i.p.xyz);}
half DepthFrag():SV_Target{return 0;}

// DepthNormals — matches LitDepthNormalsPass.hlsl output encoding.
StoneNormals NormalsVert(StoneAttributes i){StoneNormals o;o.p=TransformObjectToHClip(i.p.xyz);o.n=TransformObjectToWorldNormal(i.n);return o;}
half4 NormalsFrag(StoneNormals i):SV_Target{
 float3 n=NormalizeNormalPerPixel(i.n);
 #if defined(_GBUFFER_NORMALS_OCT)
 float2 oct=PackNormalOctQuadEncode(n);
 return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
 #else
 return half4(n,0);
 #endif
}
#endif
