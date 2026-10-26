Shader "PSX/PSX_SpaceDust"
{
    Properties
    {
        _TintColor ("Tint Color", Color) = (0.85, 0.92, 1.0, 0.6)
        _TwinkleSpeed ("Twinkle Speed", Float) = 2.0
        _TwinkleAmount ("Twinkle Amount", Range(0, 1)) = 0.35
        _Softness ("Edge Softness", Range(0.1, 2.0)) = 1.0
    }

    SubShader
    {
        Tags 
        { 
            "Queue" = "Transparent" 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True"
        }

        LOD 100
        Blend SrcAlpha One // Additive blend: soft glowing cosmic dust in darkness
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SpaceDustPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float  fogFactor    : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float  _TwinkleSpeed;
                float  _TwinkleAmount;
                float  _Softness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.color = input.color * _TintColor;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Procedural soft circle sprite to avoid missing texture dependencies
                float2 centeredUV = (input.uv - 0.5) * 2.0;
                float distSq = dot(centeredUV, centeredUV);
                if (distSq > 1.0) discard;

                // Smooth exponential falloff from center
                float falloff = saturate(1.0 - distSq);
                falloff = pow(falloff, _Softness);

                // Subtle twinkling glint for ice crystal reflection
                float twinkle = 1.0 - _TwinkleAmount * 0.5 * (sin(_Time.y * _TwinkleSpeed + input.positionWS.x * 6.0 + input.positionWS.z * 6.0) + 1.0);

                float4 finalColor = input.color;
                finalColor.a *= falloff * twinkle;

                // Fade particles into background fog at distance
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);

                return finalColor;
            }
            ENDHLSL
        }
    }
}
