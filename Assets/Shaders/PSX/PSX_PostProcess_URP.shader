Shader "PSX/PSX_PostProcess_URP"
{
    Properties
    {
        [Header(Pixelation)]
        _PixelResolution ("Pixel Resolution (X, Y)", Vector) = (320, 240, 0, 0)
        [Toggle] _EnablePixelation ("Enable Pixelation", Float) = 1.0

        [Header(Dithering and Color Depth)]
        [Toggle] _EnableDithering ("Enable Dithering", Float) = 1.0
        _DitheringScale ("Dithering Scale", Float) = 1.0
        _ColorDepth ("Color Depth per Channel (32 = 15-bit PS1)", Float) = 32.0

        [Header(CRT Interlacing Scanlines)]
        [Toggle] _EnableInterlacing ("Enable Interlacing", Float) = 1.0
        _InterlacingIntensity ("Interlacing Darkening", Range(0, 1)) = 0.18
        _InterlacingScale ("Interlacing Line Scale", Float) = 1.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        LOD 100
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "PSX_PostProcess"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _PixelResolution;
                float _EnablePixelation;
                float _EnableDithering;
                float _DitheringScale;
                float _ColorDepth;
                float _EnableInterlacing;
                float _InterlacingIntensity;
                float _InterlacingScale;
            CBUFFER_END

            // Authentic PlayStation 1 Hardware 4x4 Bayer Dithering Matrix
            static const int ps1DitherMatrix[16] =
            {
                -4,  0, -3,  1,
                 2, -2,  3, -1,
                -3,  1, -4,  0,
                 3, -1,  2, -2
            };

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                // 1. Pixelation (Low-Resolution Quantization sampled at texel centers)
                if (_EnablePixelation > 0.5)
                {
                    float2 res = _PixelResolution.xy;
                    if (res.x > 0.0 && res.y > 0.0)
                    {
                        uv = (floor(uv * res) + 0.5) / res;
                    }
                }

                // Sample scene color buffer with point clamp for crisp retro pixels
                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);

                // Compute discrete pixel grid for dithering and scanlines
                float2 screenPixel = input.positionCS.xy;
                if (_EnablePixelation > 0.5 && _PixelResolution.x > 0.0 && _PixelResolution.y > 0.0)
                {
                    screenPixel = floor(input.texcoord * _PixelResolution.xy);
                }

                float scale = max(_DitheringScale, 0.001);
                int2 ditherPixel = (int2)floor(screenPixel / scale);

                // 2. Bayer 4x4 Dithering & Color Banding (15-bit RGB quantization)
                if (_EnableDithering > 0.5)
                {
                    // Safe modulo 4
                    int ditherX = abs(ditherPixel.x) % 4;
                    int ditherY = abs(ditherPixel.y) % 4;
                    int ditherOffset = ps1DitherMatrix[ditherX + ditherY * 4];

                    float depth = max(_ColorDepth, 2.0);
                    float stepSize = 1.0 / (depth - 1.0);

                    // Add signed dither offset scaled to bit depth
                    float offsetNorm = (float)ditherOffset / (depth * 8.0);

                    color.rgb = floor((color.rgb + offsetNorm) * (depth - 1.0) + 0.5) * stepSize;
                    color.rgb = saturate(color.rgb);
                }

                // 3. CRT Scanlines (Stable scanlines without 30Hz camera-motion strobing)
                if (_EnableInterlacing > 0.5)
                {
                    int lineIndex = (int)floor(screenPixel.y / max(_InterlacingScale, 1.0));
                    if ((abs(lineIndex) % 2) == 0)
                    {
                        color.rgb *= (1.0 - _InterlacingIntensity);
                    }
                }

                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
