Shader "SandGuard/VFX/GoldenShield"
{
    Properties
    {
        _Tint("Gold", Color) = (1,0.62,0.12,1)
        _Opacity("Opacity", Range(0,1)) = 0.14
        _Emission("Brightness", Float) = 1.4
        _EffectFade("Summon Fade", Range(0,1)) = 1
        _Hit("Hit Flash", Range(0,1)) = 0
        _Rim("Rim", Range(0,1)) = 0.18
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            float _Opacity, _Emission, _EffectFade, _Hit, _Rim;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 local:TEXCOORD2; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.local=v.positionOS.xyz;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float rim=pow(1-abs(dot(normalize(i.normalWS),normalize(GetWorldSpaceViewDir(i.positionWS)))),3);
                float sweep=pow(saturate(.5+.5*sin(i.local.y*18-_Time.y*2.4)),14);
                // Grow upwards on summon; the shell stays transparent during a hit.
                float reveal=smoothstep(i.local.y-.07,i.local.y+.07,lerp(-.72,.72,_EffectFade));
                float alpha=saturate(_Opacity+rim*_Rim+sweep*.035+_Hit*.12)*_EffectFade*reveal;
                float3 color=lerp(_Tint.rgb,float3(1,.96,.70),_Hit*.7)*(_Emission+sweep*.25+_Hit*.5);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
