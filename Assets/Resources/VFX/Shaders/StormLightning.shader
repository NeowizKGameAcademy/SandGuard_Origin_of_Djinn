Shader "SandGuard/VFX/StormLightning"
{
    // 폭풍 벽 안쪽 고리에 치는 청록 마법 번개. 줄기의 둘레 위치·씨앗·시작 시각·지속 시간은
    // StormLightning 컴포넌트가 MaterialPropertyBlock 으로 넣는다(_Bolts, _LightningTime).
    Properties
    {
        _NoiseTex ("Seamless cloud noise", 2D) = "gray" {}
        _CoreColor ("Bolt core", Color) = (0.78,1,1,1)
        _GlowColor ("Bolt glow", Color) = (0.16,0.95,0.9,1)
        _Perimeter ("Ring perimeter (m)", Float) = 740
        _Height ("Ring height (m)", Float) = 100
        _CoreWidth ("Core width (m)", Float) = 0.45
        _GlowWidth ("Glow width (m)", Float) = 5
        _Intensity ("Intensity", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor, _GlowColor;
                float _Perimeter, _Height, _CoreWidth, _GlowWidth, _Intensity;
            CBUFFER_END
            // x: 둘레 위치 0..1, y: 씨앗, z: 시작 시각, w: 지속 시간 (StormLightning.Bolts 개)
            float4 _Bolts[4];
            float _LightningTime;

            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float fog:TEXCOORD1; };
            V vert(A i)
            {
                V o; VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS; o.uv = i.uv; o.fog = ComputeFogFactor(p.positionCS.z); return o;
            }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            // 빠르게 켜지고 깜빡이며 꺼진다. StormLightning.Envelope 와 같은 식이어야 조명과 맞는다.
            float envelope(float t)
            {
                if (t < 0 || t > 1) return 0;
                float rise = saturate(t / .08);
                float decay = t < .08 ? 1 : exp(-(t - .08) * 5.5);
                float flicker = .7 + .3 * sin(t * 95) * sin(t * 37 + 1.3);
                return rise * decay * flicker;
            }
            float3 bolt(float4 b, float2 uv, float t)
            {
                float env = envelope(t); if (env <= 0) return 0;
                float du = frac(uv.x - b.x + .5) - .5;          // 고리를 한 바퀴 감아 이어지는 둘레 거리
                float xm = du * _Perimeter, ym = uv.y * _Height;  // 미터 단위
                float top = _Height * (.62 + .33 * hash(float2(b.y, 1.7)));
                float bottom = _Height * (.04 + .18 * hash(float2(b.y, 4.1)));
                float wander = (SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(b.y * 13.7, uv.y * 3.2 + b.y)).r - .5) * 18
                             + (SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(b.y * 5.3, uv.y * 14 + b.y * 2)).r - .5) * 4;
                float span = smoothstep(bottom, bottom + 6, ym) * smoothstep(top, top - 6, ym);
                float d = abs(xm - wander);
                float core = exp(-d * d / (_CoreWidth * _CoreWidth)) * span;
                float glow = exp(-d / _GlowWidth) * span;
                // 가지: 위쪽 한 점에서 비스듬히 갈라져 내려간다.
                float len = top - bottom;
                float forkY = top - len * (.25 + .35 * hash(float2(b.y, 9.3)));
                float dir = hash(float2(b.y, 2.2)) < .5 ? -1 : 1;
                float bx = wander + (forkY - ym) * dir * (1.2 + hash(float2(b.y, 5.5)));
                float bspan = smoothstep(forkY - len * .35, forkY - len * .3, ym) * smoothstep(forkY, forkY - 2, ym);
                float d2 = abs(xm - bx);
                float core2 = exp(-d2 * d2 / (_CoreWidth * _CoreWidth * .5)) * bspan * .7;
                float glow2 = exp(-d2 / (_GlowWidth * .6)) * bspan * .5;
                // 넓은 후광: 줄기 주변 모래가 청록으로 물든다.
                float halo = exp(-d / (_GlowWidth * 8)) * span * .16;
                return (_CoreColor.rgb * (core + core2) * 2.8 + _GlowColor.rgb * (glow * 1.4 + glow2 + halo)) * env;
            }
            half4 frag(V i):SV_Target
            {
                float3 c = 0;
                [unroll] for (int k = 0; k < 4; k++)
                {
                    float4 b = _Bolts[k];
                    c += bolt(b, i.uv, (_LightningTime - b.z) / max(b.w, .01));
                }
                c *= _Intensity;
                c = MixFogColor(c, half3(0, 0, 0), i.fog);   // 가산 합성이므로 안개는 검정으로 섞는다
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
