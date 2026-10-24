using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace SpaceHorror.Rendering
{
    /// <summary>
    /// Unity 6 URP ScriptableRendererFeature providing authentic PlayStation 1 (PSX) post-processing:
    /// Bayer 4x4 & PS1 Hardware Dithering, 15-bit Color Banding, Pixelation Downsampling, and CRT Interlacing.
    /// Fully compatible with Unity 6 RenderGraph API.
    /// </summary>
    [DisallowMultipleRendererFeature("PSX Retro Post-Process")]
    public class PSXPostProcessRendererFeature : ScriptableRendererFeature
    {
        public enum RetroResolutionPreset
        {
            Authentic_320x240,
            N64_Style_480x360,
            HighRes_640x480,
            Custom
        }

        [Serializable]
        public class Settings
        {
            [Header("Render Timing")]
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

            [Header("Shader Reference")]
            public Shader psxShader;

            [Header("Pixelation Resolution")]
            public bool enablePixelation = true;
            public RetroResolutionPreset resolutionPreset = RetroResolutionPreset.Authentic_320x240;
            public Vector2 customResolution = new Vector2(320, 240);

            [Header("PS1 Bayer Dithering & Color Depth")]
            public bool enableDithering = true;
            [Range(1f, 4f)] public float ditheringScale = 1.0f;
            [Tooltip("32 = Authentic 15-bit RGB (32 steps per color channel)")]
            [Range(8f, 64f)] public float colorDepth = 32.0f;

            [Header("CRT Scanline Interlacing")]
            public bool enableInterlacing = true;
            [Range(0f, 0.5f)] public float interlacingIntensity = 0.18f;
            [Range(1f, 4f)] public float interlacingScale = 1.0f;
        }

        public static PSXPostProcessRendererFeature Instance { get; private set; }

        [SerializeField] private Settings settings = new Settings();
        private Material psxMaterial;
        private PSXRenderPass psxPass;

        public override void Create()
        {
            Instance = this;

            if (settings.psxShader == null)
            {
                settings.psxShader = Shader.Find("PSX/PSX_PostProcess_URP");
            }

            if (settings.psxShader != null)
            {
                psxMaterial = CoreUtils.CreateEngineMaterial(settings.psxShader);
            }

            psxPass = new PSXRenderPass(settings, psxMaterial);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (psxMaterial == null) return;
            if (renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            renderer.EnqueuePass(psxPass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(psxMaterial);
        }

        public Material GetMaterial() => psxMaterial;
        public Settings GetSettings() => settings;

        // --- RENDER PASS IMPLEMENTATION ---
        private class PSXRenderPass : ScriptableRenderPass
        {
            private readonly Settings settings;
            private readonly Material material;

            public PSXRenderPass(Settings settings, Material material)
            {
                this.settings = settings;
                this.material = material;
                this.renderPassEvent = settings.renderPassEvent;
            }

            private void UpdateMaterialProperties()
            {
                if (material == null) return;

                Vector2 targetRes;
                switch (settings.resolutionPreset)
                {
                    case RetroResolutionPreset.Authentic_320x240:
                        targetRes = new Vector2(320f, 240f);
                        break;
                    case RetroResolutionPreset.N64_Style_480x360:
                        targetRes = new Vector2(480f, 360f);
                        break;
                    case RetroResolutionPreset.HighRes_640x480:
                        targetRes = new Vector2(640f, 480f);
                        break;
                    case RetroResolutionPreset.Custom:
                    default:
                        targetRes = settings.customResolution;
                        break;
                }

                material.SetVector("_PixelResolution", new Vector4(targetRes.x, targetRes.y, 0f, 0f));
                material.SetFloat("_EnablePixelation", settings.enablePixelation ? 1f : 0f);
                material.SetFloat("_EnableDithering", settings.enableDithering ? 1f : 0f);
                material.SetFloat("_DitheringScale", settings.ditheringScale);
                material.SetFloat("_ColorDepth", settings.colorDepth);
                material.SetFloat("_EnableInterlacing", settings.enableInterlacing ? 1f : 0f);
                material.SetFloat("_InterlacingIntensity", settings.interlacingIntensity);
                material.SetFloat("_InterlacingScale", settings.interlacingScale);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null) return;

                UniversalResourceData resourcesData = frameData.Get<UniversalResourceData>();
                if (!resourcesData.cameraColor.IsValid()) return;

                UpdateMaterialProperties();

                // 1. Copy active camera color texture
                var targetDesc = renderGraph.GetTextureDesc(resourcesData.cameraColor);
                targetDesc.name = "_PSX_ColorBuffer_Temp";
                targetDesc.clearBuffer = false;

                TextureHandle source = resourcesData.activeColorTexture;
                TextureHandle copyTexture = renderGraph.CreateTexture(targetDesc);

                renderGraph.AddBlitPass(source, copyTexture, Vector2.one, Vector2.zero, passName: "PSX Copy Active Color");

                // 2. Blit from copy back into active color with PSX post-process shader
                TextureHandle destination = resourcesData.activeColorTexture;
                var blitParams = new RenderGraphUtils.BlitMaterialParameters(copyTexture, destination, material, 0);

                renderGraph.AddBlitPass(blitParams, passName: "PSX Retro PostProcess Blit");
            }
        }
    }
}
