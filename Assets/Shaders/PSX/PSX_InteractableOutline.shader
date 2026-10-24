Shader "SpaceHorror/PSX_InteractableOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.35, 0.85, 1.0, 1.0)
        _OutlineWidth ("Outline Width", Float) = 0.025
        _GridResolution ("Vertex Grid Resolution (0 = Use Global)", Float) = 200.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry+15" 
        }

        // Render backfaces only; frontfaces will be occluded by the original mesh
        Cull Front
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _GridResolution;
            CBUFFER_END

            float _PSX_GlobalOverride;
            float _PSX_GlobalGridResolution;

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Extrude vertices along normal in Object Space
                float len = length(input.normalOS);
                float3 normal = len > 0.0001 ? (input.normalOS / len) : float3(0, 1, 0);
                float3 extrudedPosOS = input.positionOS.xyz + normal * _OutlineWidth;

                // Transform to clip space
                VertexPositionInputs posInputs = GetVertexPositionInputs(extrudedPosOS);
                float4 posCS = posInputs.positionCS;

                // Snap coordinates in PSX style for visual consistency
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

            float4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
