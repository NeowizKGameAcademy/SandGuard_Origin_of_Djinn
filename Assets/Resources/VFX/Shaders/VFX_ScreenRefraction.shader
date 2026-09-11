// 화면 굴절(볼록 렌즈) 셰이더. URP의 불투명 텍스처(Opaque Texture, PC 파이프라인 에셋에서 켜져 있음)를 법선 방향으로 밀어 샘플링한다.
// 구체 메시에 씌우면 가장자리로 갈수록 안쪽 화면을 끌어와 물방울처럼 부풀어 보인다. _Strength는 VfxRefractionBubble이 MaterialPropertyBlock으로 넣는다.
Shader "SandGuard/VFX/ScreenRefraction"
{
    Properties
    {
        _Strength ("Distortion", Range(0, 0.3)) = 0.05
        _EdgePower ("Edge Power", Range(0.5, 6)) = 2.0
        _Tint ("Tint (alpha = amount)", Color) = (1, 1, 1, 0)
        _RimColor ("Rim Color", Color) = (1, 1, 1, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "ScreenRefraction"
            Tags { "LightMode" = "UniversalForward" }
            Blend One Zero
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
                float _EdgePower;
                half4 _Tint;
                half4 _RimColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positions.positionCS;
                output.normalWS = normals.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(positions.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewDirWS);
                float rim = pow(1.0 - saturate(dot(n, v)), _EdgePower);
                float3 nVS = TransformWorldToViewDir(n);
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float2 offset = nVS.xy * (_Strength * rim);
                offset.x *= _ScreenParams.y / _ScreenParams.x; // 화면 비율 보정: 원형으로 부푼다
                uv = saturate(uv - offset);                    // 안쪽 화면을 끌어와 확대(볼록 렌즈)
                half3 color = SampleSceneColor(uv);
                color = lerp(color, color * _Tint.rgb, _Tint.a);
                color += _RimColor.rgb * (_RimColor.a * rim);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
