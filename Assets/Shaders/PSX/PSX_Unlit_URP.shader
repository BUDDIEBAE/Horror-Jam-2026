Shader "PSX/PSX_Unlit_URP"
{
    Properties
    {
        [Header(Base Appearance)]
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(PSX Retro Emulation)]
        _GridResolution ("Vertex Grid Resolution (0 = Use Global)", Float) = 200.0
        _TextureWarping ("Texture Warping Factor", Range(0, 1)) = 1.0

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
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 affineUV   : TEXCOORD0; // xy = uv * w, z = w
                float fogFactor   : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _GridResolution;
                float _TextureWarping;
            CBUFFER_END

            float _PSX_GlobalOverride;
            float _PSX_GlobalGridResolution;
            float _PSX_GlobalTextureWarping;

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                float4 posCS = posInputs.positionCS;

                // Vertex Jitter / Snapping (Symmetric retro snapping)
                float grid = (_PSX_GlobalOverride > 0.5 && _PSX_GlobalGridResolution > 0.0) 
                             ? _PSX_GlobalGridResolution 
                             : (_GridResolution > 0.0 ? _GridResolution : _PSX_GlobalGridResolution);
                if (grid > 0.01)
                {
                    posCS.xy = (round((posCS.xy / posCS.w) * grid) / grid) * posCS.w;
                }
                output.positionCS = posCS;

                // Affine Texture Warping
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
                float2 uv = input.affineUV.xy / max(input.affineUV.z, 0.0001);
                float4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                col.rgb = MixFog(col.rgb, input.fogFactor);
                return col;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
