Shader "PSX/PSX_VolumetricBeam"
{
    Properties
    {
        _BeamColor ("Beam Color Tint", Color) = (0.82, 0.90, 1.0, 0.16)
        _FalloffPower ("Edge Softness", Float) = 1.8
        _LengthFalloff ("Length Fade", Float) = 1.2
        _DustIntensity ("Dust Motes Intensity", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags 
        { 
            "Queue" = "Transparent" 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        LOD 100
        Blend SrcAlpha One // Additive volumetric glow
        ZWrite Off
        Cull Off // Visible from both inside and outside the cone beam

        Pass
        {
            Name "VolumetricBeam"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirWS  : TEXCOORD0;
                float3 normalWS   : NORMAL;
                float lengthFactor: TEXCOORD1;
                float fogFactor   : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BeamColor;
                float _FalloffPower;
                float _LengthFalloff;
                float _DustIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.normalWS = normInputs.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);

                // Cone apex is at local Z=0, base is at local Z=1 (or Y depending on mesh)
                // Normalize length factor
                output.lengthFactor = saturate(input.positionOS.z / 25.0);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDir = normalize(input.viewDirWS);

                // Soft rim falloff (view angle grazing)
                float NdotV = abs(dot(normalWS, viewDir));
                float edgeFade = pow(saturate(1.0 - NdotV), _FalloffPower);

                // Length fade: bright near the astronaut's helmet lens, fading into the void
                float lengthFade = pow(saturate(1.0 - input.lengthFactor), _LengthFalloff);

                // Subtle dust speckle/shimmer floating in the beam
                float dust = sin(input.lengthFactor * 60.0 + _Time.y * 3.0) * cos(input.lengthFactor * 45.0 - _Time.y * 2.0);
                float dustMotes = 1.0 + (dust * _DustIntensity);

                // Dissolve beam into deep space fog at distance
                float alpha = edgeFade * lengthFade * _BeamColor.a * dustMotes * saturate(input.fogFactor);

                return float4(_BeamColor.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
