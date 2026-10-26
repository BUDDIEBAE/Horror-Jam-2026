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

        [Header("Atmospheric Deep Space Fog")]
        [Tooltip("Enable atmospheric depth fog to emulate authentic PS1 draw-distance horror")]
        [SerializeField] private bool enableFog = true;

        [Tooltip("Color of the space fog. Deep black/cold blue recommended to match space void")]
        [SerializeField] private Color fogColor = new Color(0.003f, 0.005f, 0.009f, 1f);

        [SerializeField] private FogMode fogMode = FogMode.Linear;

        [Tooltip("Distance in meters where objects begin to fade into fog")]
        [Range(1f, 30f)]
        [SerializeField] private float fogStartDistance = 7f;

        [Tooltip("Distance in meters where objects become completely shrouded in darkness")]
        [Range(15f, 100f)]
        [SerializeField] private float fogEndDistance = 38f;

        [Tooltip("Exponential fog density (only used if FogMode is Exponential/Exp2)")]
        [Range(0.001f, 0.15f)]
        [SerializeField] private float fogDensity = 0.035f;

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
                    enableFog = true;
                    fogColor = new Color(0.005f, 0.008f, 0.015f, 1f);
                    fogMode = FogMode.Linear;
                    fogStartDistance = 5f;
                    fogEndDistance = 30f;
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
                    enableFog = true;
                    fogColor = new Color(0.002f, 0.004f, 0.007f, 1f);
                    fogMode = FogMode.Linear;
                    fogStartDistance = 7f;
                    fogEndDistance = 38f;
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
                    enableFog = true;
                    fogColor = new Color(0.008f, 0.012f, 0.02f, 1f);
                    fogMode = FogMode.Linear;
                    fogStartDistance = 10f;
                    fogEndDistance = 50f;
                    break;

                case PSXPreset.CleanModern:
                    overrideMaterials = true;
                    vertexGridResolution = 0f;
                    textureWarping = 0f;
                    enablePixelation = false;
                    enableDithering = false;
                    enableInterlacing = false;
                    enableFog = false;
                    break;

                case PSXPreset.Custom:
                default:
                    break;
            }

            ApplyActiveSettings();
        }

        /// <summary>
        /// Pushes settings to global shader uniforms, the URP Renderer Feature, and RenderSettings Fog.
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

            // 3. Apply Deep Space Atmospheric Fog to Scene
            RenderSettings.fog = enableFog;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = fogMode;
            if (fogMode == FogMode.Linear)
            {
                RenderSettings.fogStartDistance = fogStartDistance;
                RenderSettings.fogEndDistance = fogEndDistance;
            }
            else
            {
                RenderSettings.fogDensity = fogDensity;
            }
        }
    }
}
