Shader "SandGuard/VFX/SpawnWarningCylinder"
{
    Properties
    {
        _Tint("Warning Color", Color) = (1,0.48,0.08,1)
        _Fade("Fade", Range(0,1)) = 1
        _Scroll("Downward Motion", Float) = 0
        _Pulse("Pulse", Range(0,1)) = 0
        _Rows("Chevron Rows", Float) = 3
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
                float _Fade, _Scroll, _Pulse, _Rows;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input, FRONT_FACE_TYPE facing:FRONT_FACE_SEMANTIC):SV_Target
            {
                float front = IS_FRONT_VFACE(facing, 1.0, 0.0);
                float u = frac(input.uv.x * 12.0);
                float v = frac(input.uv.y * _Rows + _Scroll);
                // A low centre and raised wings form a downward V, repeated around the wall.
                float line = 0.25 + abs(u - 0.5) * 1.25;
                float aa = max(fwidth(v - line), 0.008);
                float chevron = 1.0 - smoothstep(0.038, 0.038 + aa, abs(v - line));
                chevron *= smoothstep(0.07, 0.12, u) * (1.0 - smoothstep(0.88, 0.93, u));
                chevron *= front;
                float ends = smoothstep(0.01, 0.12, input.uv.y) * (1.0 - smoothstep(0.85, 0.99, input.uv.y));
                float rim = 1.0 - smoothstep(0.005, 0.012, min(input.uv.y, 1.0 - input.uv.y));
                float alpha = (0.07 * ends + chevron * ends * 0.75 + rim * 0.5) * _Fade;
                float3 color = _Tint.rgb * (1.1 + chevron * 0.8 + _Pulse * 0.25);
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
