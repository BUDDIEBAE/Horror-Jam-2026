using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceHorror.Player;

namespace SpaceHorror.UI
{
    /// <summary>
    /// Visor HUD Telemetry Widget for the Directional Signal Tracker.
    /// Strictly adheres to Film Noir aesthetics:
    /// - 100% Monochrome (White, Ash Silver, Charcoal, Pitch Black)
    /// - High-contrast CRT Oscilloscope waveform
    /// - Discrete 5-block RSSI signal bar
    /// - Negative / Invert contrast strobe upon target Lock-On
    /// - Zero external texture dependencies (procedural graphic fallbacks)
    /// </summary>
    [DisallowMultipleComponent]
    public class AstronautSignalHUD : MonoBehaviour
    {
        [Header("Player Tracker Reference")]
        [SerializeField] private AstronautSignalTracker signalTracker;

        [Header("UI Element Bindings (Optional - Procedural if empty)")]
        [SerializeField] private CanvasGroup signalCanvasGroup;
        [SerializeField] private Image backgroundPlate;
        [SerializeField] private Image topBorderBar;
        [SerializeField] private TMP_Text beaconTagText;
        [SerializeField] private TMP_Text frequencyDistanceText;
        [SerializeField] private Image[] signalMeterSegments; // 5-segment meter
        [SerializeField] private RawImage oscilloscopeDisplay;
        [SerializeField] private TMP_Text lockOnStatusText;

        [Header("Monochrome Noir Color Tokens")]
        private readonly Color colWhite = new Color(0.96f, 0.98f, 1.0f, 0.98f);
        private readonly Color colAshSilver = new Color(0.72f, 0.75f, 0.80f, 0.85f);
        private readonly Color colCharcoal = new Color(0.14f, 0.16f, 0.18f, 0.75f);
        private readonly Color colDeepBlack = new Color(0.015f, 0.02f, 0.025f, 0.92f);

        // Oscilloscope procedural texture
        private Texture2D oscTexture;
        private Color32[] oscPixels;
        private const int OSC_WIDTH = 128;
        private const int OSC_HEIGHT = 28;
        private float oscWavePhase = 0f;

        // Contrast Invert & Strobe
        private float strobeTimer = 0f;
        private bool isLockOnInverted = false;

        private void Awake()
        {
            if (signalTracker == null)
            {
                signalTracker = GetComponentInParent<AstronautSignalTracker>();
                if (signalTracker == null)
                {
                    signalTracker = UnityEngine.Object.FindFirstObjectByType<AstronautSignalTracker>();
                }
            }

            SetupOscilloscopeTexture();

            if (signalCanvasGroup == null)
            {
                BuildProceduralWidget();
            }
        }

        private float tuneFlashTimer = 0f;

        private void OnEnable()
        {
            if (signalTracker != null)
            {
                signalTracker.OnTelemetryUpdated += HandleTelemetryUpdated;
                signalTracker.OnLockOnChanged += HandleLockOnChanged;
                signalTracker.OnChannelTuned += HandleChannelTuned;
            }
        }

        private void OnDisable()
        {
            if (signalTracker != null)
            {
                signalTracker.OnTelemetryUpdated -= HandleTelemetryUpdated;
                signalTracker.OnLockOnChanged -= HandleLockOnChanged;
                signalTracker.OnChannelTuned -= HandleChannelTuned;
            }
        }

        private void Update()
        {
            if (signalTracker == null)
            {
                signalTracker = UnityEngine.Object.FindFirstObjectByType<AstronautSignalTracker>();
                if (signalTracker == null) return;
                signalTracker.OnTelemetryUpdated += HandleTelemetryUpdated;
                signalTracker.OnLockOnChanged += HandleLockOnChanged;
                signalTracker.OnChannelTuned += HandleChannelTuned;
            }

            if (tuneFlashTimer > 0f)
            {
                tuneFlashTimer -= Time.deltaTime;
            }

            UpdateOscilloscopeWaveform();
            UpdateLockOnStrobe();
        }

        private void OnDestroy()
        {
            if (oscTexture != null)
            {
                Destroy(oscTexture);
            }
        }

        #region Telemetry HUD Updates

        private void HandleChannelTuned(int channelIndex, string channelBadge, float frequencyMHz)
        {
            tuneFlashTimer = 0.4f;
            if (lockOnStatusText != null)
            {
                lockOnStatusText.text = "[ TUNING ]";
            }
        }

        private void HandleTelemetryUpdated(SignalTelemetry telemetry)
        {
            if (signalCanvasGroup == null) return;

            float strength = signalTracker.SmoothedStrength;
            bool hasSignal = signalTracker.HasActiveSignal;

            // Fade in widget when signal is detected, faint scan mode when idling
            float targetAlpha = hasSignal ? 1.0f : 0.25f;
            signalCanvasGroup.alpha = Mathf.MoveTowards(signalCanvasGroup.alpha, targetAlpha, Time.deltaTime * 3f);

            // 1. Text Readouts with Multi-Channel Telemetry
            if (beaconTagText != null)
            {
                string badge = !string.IsNullOrEmpty(telemetry.channelBadge) ? telemetry.channelBadge : "CH 00 [AUTO-SCAN]";
                beaconTagText.text = $"SIG // {badge}";
            }

            if (frequencyDistanceText != null)
            {
                if (hasSignal)
                {
                    int distMeters = Mathf.RoundToInt(telemetry.distance);
                    frequencyDistanceText.text = $"{telemetry.frequencyMHz:F1} MHz | ~{distMeters}M | [R] TUNE";
                }
                else
                {
                    frequencyDistanceText.text = "OUT OF RANGE | [R] TUNE";
                }
            }

            // 2. 5-Segment Discrete Signal Meter [■ ■ ■ ■ □]
            if (signalMeterSegments != null && signalMeterSegments.Length > 0)
            {
                int activeBlocks = Mathf.RoundToInt(strength * signalMeterSegments.Length);

                for (int i = 0; i < signalMeterSegments.Length; i++)
                {
                    if (signalMeterSegments[i] == null) continue;

                    bool isLit = i < activeBlocks && hasSignal;
                    // Strict Film Noir: Lit is crisp White, unlit is dim Charcoal
                    signalMeterSegments[i].color = isLit ? colWhite : colCharcoal;
                }
            }
        }

        private void HandleLockOnChanged(bool isLockedOn)
        {
            if (lockOnStatusText != null)
            {
                lockOnStatusText.text = isLockedOn ? "[ LOCK-ON ]" : "[ BEARING ]";
            }
        }

        private void UpdateLockOnStrobe()
        {
            if (signalTracker == null) return;

            bool isLocked = signalTracker.IsLockedOn;

            if (isLocked)
            {
                // High-contrast negative inversion strobe (10 Hz)
                strobeTimer += Time.deltaTime * 10f;
                bool invert = (int)strobeTimer % 2 == 0;

                if (invert != isLockOnInverted)
                {
                    isLockOnInverted = invert;
                    ApplyColorInversion(isLockOnInverted);
                }
            }
            else if (isLockOnInverted)
            {
                isLockOnInverted = false;
                ApplyColorInversion(false);
            }
        }

        private void ApplyColorInversion(bool invert)
        {
            if (backgroundPlate != null)
            {
                // Normal: Deep Black with white border. Inverted: Stark White with black border.
                backgroundPlate.color = invert ? colWhite : colDeepBlack;
            }

            if (topBorderBar != null)
            {
                topBorderBar.color = invert ? colDeepBlack : colAshSilver;
            }

            Color textColor = invert ? colDeepBlack : colWhite;

            if (beaconTagText != null) beaconTagText.color = textColor;
            if (frequencyDistanceText != null) frequencyDistanceText.color = invert ? colDeepBlack : colAshSilver;
            if (lockOnStatusText != null) lockOnStatusText.color = textColor;
        }

        #endregion

        #region CRT Oscilloscope Procedural Renderer

        private void SetupOscilloscopeTexture()
        {
            oscTexture = new Texture2D(OSC_WIDTH, OSC_HEIGHT, TextureFormat.RGBA32, false);
            oscTexture.filterMode = FilterMode.Point;
            oscTexture.wrapMode = TextureWrapMode.Clamp;

            oscPixels = new Color32[OSC_WIDTH * OSC_HEIGHT];
            ClearOscilloscope();
            oscTexture.Apply();

            if (oscilloscopeDisplay != null)
            {
                oscilloscopeDisplay.texture = oscTexture;
            }
        }

        private void ClearOscilloscope()
        {
            Color32 bg = new Color32(4, 5, 6, 230); // Deep CRT phosphor dark
            for (int i = 0; i < oscPixels.Length; i++)
            {
                oscPixels[i] = bg;
            }
        }

        private void UpdateOscilloscopeWaveform()
        {
            if (oscTexture == null) return;

            ClearOscilloscope();

            float strength = signalTracker != null ? signalTracker.SmoothedStrength : 0f;
            bool hasSignal = signalTracker != null && signalTracker.HasActiveSignal;

            // Advance phase based on signal tempo
            oscWavePhase += Time.deltaTime * (hasSignal ? Mathf.Lerp(4f, 24f, strength) : 2f);

            int centerY = OSC_HEIGHT / 2;
            float amplitude = hasSignal ? Mathf.Lerp(1.5f, 10.5f, strength) : 0.8f;
            float frequency = hasSignal ? Mathf.Lerp(0.08f, 0.28f, strength) : 0.05f;

            Color32 waveColor = isLockOnInverted ? new Color32(10, 10, 10, 255) : new Color32(245, 250, 255, 255);
            Color32 dimGlow = isLockOnInverted ? new Color32(80, 80, 80, 180) : new Color32(160, 175, 190, 150);

            for (int x = 0; x < OSC_WIDTH; x++)
            {
                float sine = Mathf.Sin((x * frequency) + oscWavePhase);

                // Add subtle telemetry harmonics when signal is strong
                if (hasSignal && strength > 0.5f)
                {
                    sine += Mathf.Sin((x * frequency * 2.2f) - oscWavePhase * 1.5f) * 0.35f;
                }

                int y = centerY + Mathf.RoundToInt(sine * amplitude);
                y = Mathf.Clamp(y, 1, OSC_HEIGHT - 2);

                // Draw 1-pixel high-contrast white beam with phosphor bleed
                oscPixels[y * OSC_WIDTH + x] = waveColor;
                oscPixels[(y - 1) * OSC_WIDTH + x] = dimGlow;
                oscPixels[(y + 1) * OSC_WIDTH + x] = dimGlow;
            }

            oscTexture.SetPixels32(oscPixels);
            oscTexture.Apply();
        }

        #endregion

        #region Procedural UI Hierarchy Builder (Fallback)

        /// <summary>
        /// Automatically constructs the Film Noir Signal Widget inside the Helmet Visor Canvas if not wired manually.
        /// </summary>
        public void BuildProceduralWidget()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
                if (canvas != null)
                {
                    transform.SetParent(canvas.transform, false);
                }
            }

            RectTransform rootRect = GetComponent<RectTransform>();
            if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();

            // Anchor to Top-Center of Helmet Visor
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -22f);
            rootRect.sizeDelta = new Vector2(340f, 62f);

            signalCanvasGroup = GetComponent<CanvasGroup>();
            if (signalCanvasGroup == null) signalCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            signalCanvasGroup.alpha = 0.85f;

            // Background Plate
            GameObject bgObj = new GameObject("Plate_BG");
            bgObj.transform.SetParent(transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            backgroundPlate = bgObj.AddComponent<Image>();
            backgroundPlate.color = colDeepBlack;

            // Top Border Accent
            GameObject borderObj = new GameObject("Top_Border");
            borderObj.transform.SetParent(transform, false);
            RectTransform borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = new Vector2(0f, 1f);
            borderRect.anchorMax = new Vector2(1f, 1f);
            borderRect.pivot = new Vector2(0.5f, 1f);
            borderRect.sizeDelta = new Vector2(0f, 2f);
            topBorderBar = borderObj.AddComponent<Image>();
            topBorderBar.color = colAshSilver;

            // Beacon Tag Text (Top Left)
            GameObject tagObj = new GameObject("Tag_Text");
            tagObj.transform.SetParent(transform, false);
            RectTransform tagRect = tagObj.AddComponent<RectTransform>();
            tagRect.anchorMin = new Vector2(0f, 1f);
            tagRect.anchorMax = new Vector2(0f, 1f);
            tagRect.pivot = new Vector2(0f, 1f);
            tagRect.anchoredPosition = new Vector2(10f, -6f);
            tagRect.sizeDelta = new Vector2(190f, 18f);
            beaconTagText = tagObj.AddComponent<TextMeshProUGUI>();
            beaconTagText.fontSize = 11f;
            beaconTagText.text = "SIG // INITIALIZING...";
            beaconTagText.color = colWhite;
            beaconTagText.fontStyle = FontStyles.Bold;

            // Lock-On Status (Top Right)
            GameObject lockObj = new GameObject("Lock_Text");
            lockObj.transform.SetParent(transform, false);
            RectTransform lockRect = lockObj.AddComponent<RectTransform>();
            lockRect.anchorMin = new Vector2(1f, 1f);
            lockRect.anchorMax = new Vector2(1f, 1f);
            lockRect.pivot = new Vector2(1f, 1f);
            lockRect.anchoredPosition = new Vector2(-10f, -6f);
            lockRect.sizeDelta = new Vector2(100f, 18f);
            lockOnStatusText = lockObj.AddComponent<TextMeshProUGUI>();
            lockOnStatusText.fontSize = 10f;
            lockOnStatusText.text = "[ BEARING ]";
            lockOnStatusText.alignment = TextAlignmentOptions.Right;
            lockOnStatusText.color = colWhite;

            // Frequency & Distance Subtext
            GameObject freqObj = new GameObject("Freq_Text");
            freqObj.transform.SetParent(transform, false);
            RectTransform freqRect = freqObj.AddComponent<RectTransform>();
            freqRect.anchorMin = new Vector2(0f, 0f);
            freqRect.anchorMax = new Vector2(0f, 0f);
            freqRect.pivot = new Vector2(0f, 0f);
            freqRect.anchoredPosition = new Vector2(10f, 6f);
            freqRect.sizeDelta = new Vector2(210f, 16f);
            frequencyDistanceText = freqObj.AddComponent<TextMeshProUGUI>();
            frequencyDistanceText.fontSize = 10f;
            frequencyDistanceText.text = "142.8 MHz  |  RANGE: ~0M";
            frequencyDistanceText.color = colAshSilver;

            // Oscilloscope RawImage
            GameObject oscObj = new GameObject("CRT_Oscilloscope");
            oscObj.transform.SetParent(transform, false);
            RectTransform oscRect = oscObj.AddComponent<RectTransform>();
            oscRect.anchorMin = new Vector2(0f, 0.5f);
            oscRect.anchorMax = new Vector2(0f, 0.5f);
            oscRect.pivot = new Vector2(0f, 0.5f);
            oscRect.anchoredPosition = new Vector2(10f, 0f);
            oscRect.sizeDelta = new Vector2(120f, 22f);
            oscilloscopeDisplay = oscObj.AddComponent<RawImage>();
            oscilloscopeDisplay.texture = oscTexture;

            // 5-Segment Signal Meter (Right side)
            signalMeterSegments = new Image[5];
            float segWidth = 14f;
            float segSpacing = 4f;
            float totalMeterWidth = (5 * segWidth) + (4 * segSpacing);
            float startX = 340f - 10f - totalMeterWidth;

            for (int i = 0; i < 5; i++)
            {
                GameObject segObj = new GameObject($"Meter_Segment_{i}");
                segObj.transform.SetParent(transform, false);
                RectTransform segRect = segObj.AddComponent<RectTransform>();
                segRect.anchorMin = new Vector2(0f, 0f);
                segRect.anchorMax = new Vector2(0f, 0f);
                segRect.pivot = new Vector2(0f, 0f);
                segRect.anchoredPosition = new Vector2(startX + (i * (segWidth + segSpacing)), 8f);
                segRect.sizeDelta = new Vector2(segWidth, 14f);

                Image segImg = segObj.AddComponent<Image>();
                segImg.color = colCharcoal;
                signalMeterSegments[i] = segImg;
            }
        }

        #endregion
    }
}
