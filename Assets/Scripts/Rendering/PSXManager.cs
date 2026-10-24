using System;
using UnityEngine;

namespace SpaceHorror.Rendering
{
    /// <summary>
    /// Central manager & preset controller for the PSX retro emulation pipeline in Unity 6 URP.
    /// Provides live Inspector controls and presets for vertex wobble, affine texture warping,
    /// resolution downsampling, Bayer dithering, and CRT scanlines.
    /// </summary>
    [ExecuteAlways]
    public class PSXManager : MonoBehaviour
    {
        public enum PSXPreset
        {
            Custom,
            AuthenticPS1,
            FilmNoirSpaceHorror,
            SubtleRetro,
            CleanModern
        }

        [Header("Preset Switcher")]
        [SerializeField] private PSXPreset activePreset = PSXPreset.FilmNoirSpaceHorror;

        [Header("Global Vertex & Geometry Emulation")]
        [Tooltip("Force all PSX materials in the scene to use these global settings")]
        [SerializeField] private bool overrideMaterials = true;

        [Tooltip("Density of vertex grid snapping. Lower = more wobbling, Higher = subtle authentic wobble.")]
        [Range(50f, 500f)]
        [SerializeField] private float vertexGridResolution = 200f;

        [Tooltip("Perspective-incorrect texture warping factor (0 = straight/modern, 1 = authentic PS1 affine warp)")]
        [Range(0f, 1f)]
        [SerializeField] private float textureWarping = 1.0f;

        [Header("Retro Screen Resolution")]
        [SerializeField] private bool enablePixelation = true;
        [SerializeField] private PSXPostProcessRendererFeature.RetroResolutionPreset resolutionPreset = PSXPostProcessRendererFeature.RetroResolutionPreset.Authentic_320x240;
        [SerializeField] private Vector2 customResolution = new Vector2(320, 240);

        [Header("PS1 Bayer Dithering & Color Quantization")]
        [SerializeField] private bool enableDithering = true;
        [Range(1f, 4f)]
        [SerializeField] private float ditheringScale = 1.0f;
        [Tooltip("32 = Authentic 15-bit RGB PlayStation 1 color depth")]
        [Range(8f, 64f)]
        [SerializeField] private float colorDepth = 32f;

        [Header("CRT Scanlines")]
        [SerializeField] private bool enableInterlacing = true;
        [Range(0f, 0.5f)]
        [SerializeField] private float interlacingIntensity = 0.15f;
        [Range(1f, 4f)]
        [SerializeField] private float interlacingScale = 1.0f;

        private PSXPreset previousPreset;

        private void OnEnable()
        {
            previousPreset = activePreset;
            ApplyActiveSettings();
        }

        private void Update()
        {
            // If preset changed in inspector, apply preset defaults
            if (activePreset != previousPreset)
            {
                ApplyPreset(activePreset);
                previousPreset = activePreset;
            }

            ApplyActiveSettings();
        }

        private void OnValidate()
        {
            if (activePreset != previousPreset)
            {
                ApplyPreset(activePreset);
                previousPreset = activePreset;
            }

            ApplyActiveSettings();
        }

        /// <summary>
        /// Applies preset configuration to all inspector fields.
        /// </summary>
        public void ApplyPreset(PSXPreset preset)
        {
            activePreset = preset;

            switch (preset)
            {
                case PSXPreset.AuthenticPS1:
                    overrideMaterials = true;
                    vertexGridResolution = 180f;
                    textureWarping = 1.0f;
                    enablePixelation = true;
                    resolutionPreset = PSXPostProcessRendererFeature.RetroResolutionPreset.Authentic_320x240;
                    enableDithering = true;
                    ditheringScale = 1.0f;
                    colorDepth = 32f;
                    enableInterlacing = true;
                    interlacingIntensity = 0.16f;
                    interlacingScale = 1.0f;
                    break;

                case PSXPreset.FilmNoirSpaceHorror:
                    overrideMaterials = true;
                    vertexGridResolution = 220f; // Stable retro wobble, eliminating motion flickering
                    textureWarping = 0.85f;
                    enablePixelation = true;
                    resolutionPreset = PSXPostProcessRendererFeature.RetroResolutionPreset.Authentic_320x240;
                    enableDithering = true;
                    ditheringScale = 1.0f;
                    colorDepth = 32f;
                    enableInterlacing = true;
                    interlacingIntensity = 0.12f;
                    interlacingScale = 1.0f;
                    break;

                case PSXPreset.SubtleRetro:
                    overrideMaterials = true;
                    vertexGridResolution = 320f;
                    textureWarping = 0.4f;
                    enablePixelation = true;
                    resolutionPreset = PSXPostProcessRendererFeature.RetroResolutionPreset.N64_Style_480x360;
                    enableDithering = true;
                    ditheringScale = 1.0f;
                    colorDepth = 48f;
                    enableInterlacing = false;
                    interlacingIntensity = 0.08f;
                    interlacingScale = 1.0f;
                    break;

                case PSXPreset.CleanModern:
                    overrideMaterials = true;
                    vertexGridResolution = 0f;
                    textureWarping = 0f;
                    enablePixelation = false;
                    enableDithering = false;
                    enableInterlacing = false;
                    break;

                case PSXPreset.Custom:
                default:
                    break;
            }

            ApplyActiveSettings();
        }

        /// <summary>
        /// Pushes settings to global shader uniforms and the URP Renderer Feature.
        /// </summary>
        public void ApplyActiveSettings()
        {
            // 1. Push Global Shader Uniforms (Affects PSX_Lit_URP and PSX_Unlit_URP)
            Shader.SetGlobalFloat("_PSX_GlobalOverride", overrideMaterials ? 1.0f : 0.0f);
            Shader.SetGlobalFloat("_PSX_GlobalGridResolution", vertexGridResolution);
            Shader.SetGlobalFloat("_PSX_GlobalTextureWarping", textureWarping);

            // 2. Push Post-Processing Settings to Renderer Feature
            if (PSXPostProcessRendererFeature.Instance != null)
            {
                var settings = PSXPostProcessRendererFeature.Instance.GetSettings();
                if (settings != null)
                {
                    settings.enablePixelation = enablePixelation;
                    settings.resolutionPreset = resolutionPreset;
                    settings.customResolution = customResolution;
                    settings.enableDithering = enableDithering;
                    settings.ditheringScale = ditheringScale;
                    settings.colorDepth = colorDepth;
                    settings.enableInterlacing = enableInterlacing;
                    settings.interlacingIntensity = interlacingIntensity;
                    settings.interlacingScale = interlacingScale;
                }
            }
        }
    }
}
