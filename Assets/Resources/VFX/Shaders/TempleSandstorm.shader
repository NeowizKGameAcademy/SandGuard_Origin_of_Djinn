Shader "SandGuard/VFX/TempleSandstorm"
{
    Properties
    {
        _NoiseTex ("Seamless cloud noise", 2D) = "gray" {}
        _DarkColor ("Shadow sand", Color) = (0.24,0.13,0.055,1)
        _LightColor ("Sunlit sand", Color) = (0.83,0.62,0.32,1)
        _Opacity ("Density", Range(0,1)) = 0.9
        _DensityFloor ("Minimum body density", Range(0,1)) = 0
        _FlowSpeed ("Flow speed", Float) = 1
        _Phase ("Layer phase", Float) = 0
        _StormTime ("Animation time", Float) = 0
        _Swirl ("Vortex swirl (0 = drifting bands)", Range(0,1)) = 0
        _Streak ("Streak stretch along the ring", Range(0,1)) = 0
        _RingTiles ("Pattern repeats around the ring", Float) = 10
        _SwirlScale ("Swirl bulge amplitude (object space; 1/radius for scaled rings)", Float) = 1
        _WaveAmplitude ("Vertical wave amplitude", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _DarkColor, _LightColor;
                float _Opacity, _DensityFloor, _FlowSpeed, _Phase, _StormTime, _Swirl, _Streak, _WaveAmplitude, _RingTiles, _SwirlScale;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1; };
            V vert(A i)
            {
                V o;
                float t = _StormTime * _FlowSpeed;
                float angle = i.uv.x * 6.2831853;
                float envelope = sin(i.uv.y * 3.14159265);
                i.positionOS.y += envelope * _WaveAmplitude * (2.8*sin(angle*7+t*.55+_Phase) + 1.5*sin(angle*13-t*.8));
                // 소용돌이: 위아래 물결 대신 둘레를 따라 도는 반경 방향 불룩함이 비스듬히 감겨 올라간다.
                float2 radial = normalize(i.positionOS.xz + 1e-5);
                // 진폭은 오브젝트 공간이라 XZ 스케일로 커지는 링에서는 반경에 비례해 부풀어 형체를 잃는다.
                // 커지는 링은 _SwirlScale에 1/반경을 넣어 월드 진폭을 고정한다(스케일 1인 봉인 폭풍은 1 그대로).
                i.positionOS.xz += radial * envelope * _Swirl * 2.0 * _SwirlScale * sin(angle*8 - t*.7 + i.uv.y*9 + _Phase);
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS; o.uv=i.uv; return o;
            }
            half4 frag(V i):SV_Target
            {
                float t = _StormTime * _FlowSpeed;
                // 소용돌이: 둘레 방향으로 빠르게 돌고 높이에 따라 비스듬히 감기며, 무늬는 둘레 방향으로 길게 늘어난다.
                // 둘레 반복 수는 _RingTiles. 커지는 링(사막 폭풍)이 반경에 비례해 올려 주므로 무늬의 실제 크기가 유지되고,
                // uv.x 계수가 함께 커져 "무늬 하나가 지나가는 시간"은 반경과 무관하게 일정해진다(= 접선 속도 유지).
                float tiles=max(1,_RingTiles);
                float2 p=float2(i.uv.x*tiles-t*.045*(1+_Swirl*3)+_Phase+i.uv.y*_Swirl*2.5, (i.uv.y*2.8+t*.035*(1+_Swirl))*(1+_Streak*3));
                float warp=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,p*.6).r;
                float n=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,p+float2(warp*.35,warp*.6)).r;
                float fine=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,p*float2(2,4)+float2(t*.013,0)).r;
                float detail=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,p*float2(5,13)-float2(t*.018,0)).r;
                // 나선 띠: 둘레와 높이를 함께 타고 도는 밝기 띠가 소용돌이 구조를 읽히게 한다.
                float density=saturate(n*.78+fine*.22+_Swirl*.16*sin((i.uv.x*tiles*1.4+i.uv.y*5)*6.2831853-t*.9));
                float edge=smoothstep(0,.075,i.uv.y)*(1-smoothstep(.88,1,i.uv.y));
                float alpha=lerp(_DensityFloor,1,smoothstep(.18,.7,density))*_Opacity*edge;
                // The large shells use a geometric edge fade; depth testing still clips
                // opaque structures. Avoid sampling capture-camera depth for this layer.
                float light=saturate(density*.75+fine*.20+detail*.10+.10);
                half3 color=lerp(_DarkColor.rgb,_LightColor.rgb,light);
                color*=.86+.14*sin(i.uv.x*6.2831853+1.0);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
