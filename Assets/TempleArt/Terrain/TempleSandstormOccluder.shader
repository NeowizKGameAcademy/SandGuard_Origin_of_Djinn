Shader "SandGuard/VFX/TempleSandstormOccluder"
{
    Properties
    {
        _NoiseTex ("Seamless cloud noise", 2D) = "gray" {}
        _DarkColor ("Shadow sand", Color) = (0.24,0.13,0.055,1)
        _LightColor ("Sunlit sand", Color) = (0.83,0.62,0.32,1)
        _Opacity ("Density", Range(0,1)) = 0.9
        _FlowSpeed ("Flow speed", Float) = 1
        _Phase ("Layer phase", Float) = 0
        _StormTime ("Animation time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend One Zero
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _DarkColor, _LightColor;
                float _Opacity, _FlowSpeed, _Phase, _StormTime;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1; };
            V vert(A i)
            {
                V o;
                float t = _StormTime * _FlowSpeed;
                float angle = i.uv.x * 6.2831853;
                float envelope = sin(i.uv.y * 3.14159265);
                i.positionOS.y += envelope * (2.8*sin(angle*7+t*.55+_Phase) + 1.5*sin(angle*13-t*.8));
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS; o.uv=i.uv; return o;
            }
            half4 frag(V i):SV_Target
            {
                float t = _StormTime * _FlowSpeed;
                float2 p=float2(i.uv.x*10-t*.045+_Phase, i.uv.y*2.8+t*.035);
                float warp=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,p*.6).r;
                float n=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,p+float2(warp*.35,warp*.6)).r;
                float fine=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,p*float2(2,4)+float2(t*.013,0)).r;
                float detail=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,p*float2(5,13)-float2(t*.018,0)).r;
                float density=saturate(n*.78+fine*.22);
                float edge=smoothstep(0,.075,i.uv.y)*(1-smoothstep(.88,1,i.uv.y));
                float alpha=smoothstep(.18,.7,density)*_Opacity*edge;
                // The large shells use a geometric edge fade; depth testing still clips
                // opaque structures. Avoid sampling capture-camera depth for this layer.
                float light=saturate(density*.75+fine*.20+detail*.10+.10);
                half3 color=lerp(_DarkColor.rgb,_LightColor.rgb,light);
                color*=.86+.14*sin(i.uv.x*6.2831853+1.0);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
