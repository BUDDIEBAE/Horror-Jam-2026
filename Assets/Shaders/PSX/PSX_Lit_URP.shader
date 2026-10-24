Shader "PSX/PSX_Lit_URP"
{
    Properties
    {
        [Header(Base Appearance)]
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(PSX Retro Emulation)]
        _GridResolution ("Vertex Grid Resolution (0 = Use Global)", Float) = 200.0
        _TextureWarping ("Texture Warping Factor", Range(0, 1)) = 1.0

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionMap ("Emission Map", 2D) = "black" {}

        [Header(Surface Options)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry"
        }

        LOD 100
        Cull [_Cull]

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP keywords for lighting and shadows
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 affineUV   : TEXCOORD0; // xy = uv * w, z = w
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : NORMAL;
                float fogFactor   : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _EmissionColor;
                float _GridResolution;
                float _TextureWarping;
            CBUFFER_END

            // Global settings set by PSXManager (Phase 3)
            float _PSX_GlobalOverride;
            float _PSX_GlobalGridResolution;
            float _PSX_GlobalTextureWarping;

            Varyings vert(Attributes input)
            {
                Varyings output;

                // 1. World & View Positions
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;

                // 2. Vertex Jitter / Snapping (PS1 Fixed-Point Math emulation)
                float4 posCS = posInputs.positionCS;
                float grid = (_PSX_GlobalOverride > 0.5 && _PSX_GlobalGridResolution > 0.0) 
                             ? _PSX_GlobalGridResolution 
                             : (_GridResolution > 0.0 ? _GridResolution : _PSX_GlobalGridResolution);
                if (grid > 0.01)
                {
                    // Snap clip-space coordinates symmetrically to simulate low-precision hardware
                    posCS.xy = (round((posCS.xy / posCS.w) * grid) / grid) * posCS.w;
                }
                output.positionCS = posCS;

                // 3. Affine Texture Warping (Perspective-Incorrect UV sampling)
                float2 rawUV = TRANSFORM_TEX(input.uv, _BaseMap);
                float warpFactor = (_PSX_GlobalOverride > 0.5) 
                                   ? _PSX_GlobalTextureWarping 
                                   : (_TextureWarping > 0.0 ? _TextureWarping : _PSX_GlobalTextureWarping);
                float wComponent = max(posCS.w, 0.001);
                float affineW = lerp(1.0, wComponent, saturate(warpFactor));
                output.affineUV = float4(rawUV * affineW, affineW, 0.0);

                output.fogFactor = ComputeFogFactor(posCS.z);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Invert the affine perspective division to produce the authentic PS1 texture warp
                float2 uv = input.affineUV.xy / max(input.affineUV.z, 0.0001);

                float4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                float3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor.rgb;

                // Setup URP InputData
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);

                // Ambient starlight: Ensure minimum floor so objects don't crush to pitch black
                float3 ambientSH = SampleSH(inputData.normalWS);
                float3 spaceStarlightFloor = float3(0.08, 0.09, 0.12);
                inputData.bakedGI = max(ambientSH, spaceStarlightFloor);

                // Setup URP SurfaceData
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = texColor.rgb;
                surfaceData.alpha = texColor.a;
                surfaceData.emission = emission;
                surfaceData.specular = 0.04;
                surfaceData.smoothness = 0.15;
                surfaceData.occlusion = 1.0;

                // Native URP Blinn-Phong lighting: handles Directional sun, Spotlights, Forward, Forward+, shadows
                float4 finalColor = UniversalFragmentBlinnPhong(inputData, surfaceData);
                return finalColor;
            }
            ENDHLSL
        }

        // Shadow Caster Pass (allows debris and structures to cast real-time shadows in URP)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;
            float _GridResolution;
            float _PSX_GlobalOverride;
            float _PSX_GlobalGridResolution;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                float3 biasedWS = ApplyShadowBias(posInputs.positionWS, normInputs.normalWS, _LightDirection);
                float4 posCS = TransformWorldToHClip(biasedWS);
                #if UNITY_REVERSED_Z
                posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                // Apply vertex snapping to shadows as well for consistent jitter
                float grid = (_PSX_GlobalOverride > 0.5 && _PSX_GlobalGridResolution > 0.0) 
                             ? _PSX_GlobalGridResolution 
                             : (_GridResolution > 0.0 ? _GridResolution : _PSX_GlobalGridResolution);
                if (grid > 0.01)
                {
                    posCS.xy = (round((posCS.xy / posCS.w) * grid) / grid) * posCS.w;
                }

                output.positionCS = posCS;
                return output;
            }

            float4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // DepthOnly Pass (for URP depth pre-pass and post-processing)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthPassVertex
            #pragma fragment DepthPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float _GridResolution;
            float _PSX_GlobalOverride;
            float _PSX_GlobalGridResolution;

            Varyings DepthPassVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                float4 posCS = posInputs.positionCS;

                float grid = (_PSX_GlobalOverride > 0.5 && _PSX_GlobalGridResolution > 0.0) 
                             ? _PSX_GlobalGridResolution 
                             : (_GridResolution > 0.0 ? _GridResolution : _PSX_GlobalGridResolution);
                if (grid > 0.01)
                {
                    posCS.xy = (round((posCS.xy / posCS.w) * grid) / grid) * posCS.w;
                }

                output.positionCS = posCS;
                return output;
            }

            float4 DepthPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // DepthNormals Pass (for SSAO and depth-normal prepasses)
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
            };

            float _GridResolution;
            float _PSX_GlobalOverride;
            float _PSX_GlobalGridResolution;

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                float4 posCS = posInputs.positionCS;
                float grid = (_PSX_GlobalOverride > 0.5 && _PSX_GlobalGridResolution > 0.0) 
                             ? _PSX_GlobalGridResolution 
                             : (_GridResolution > 0.0 ? _GridResolution : _PSX_GlobalGridResolution);
                if (grid > 0.01)
                {
                    posCS.xy = (round((posCS.xy / posCS.w) * grid) / grid) * posCS.w;
                }

                output.positionCS = posCS;
                output.normalWS = normInputs.normalWS;
                return output;
            }

            float4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                float3 normal = NormalizeNormalPerPixel(input.normalWS);
                return float4(normal, 0.0);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
