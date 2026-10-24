using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceHorror.Player;

namespace SpaceHorror.UI
{
    /// <summary>
    /// Diagetic Helmet Visor HUD for 6DoF Astronaut in Unity 6 URP.
    /// Emulates 90s PlayStation 1 digital flight telemetry:
    /// - Oxygen tank level gauge (Cyan -> Amber -> Red warning)
    /// - Real-time ECG Heart Rate pulse monitor (synced to BPM tempo)
    /// - Suit pressure & hull puncture alert
    /// - RCS gyro stabilization & helmet flashlight indicators
    /// - Suffocation Tunnel Vision (constricting blackout vignette during oxygen starvation)
    /// - Hybrid architecture: Dedicated slots for custom external sprites/fonts with procedural fallback.
    /// </summary>
    public class AstronautHelmetHUD : MonoBehaviour
    {
        [Header("External Asset Overrides (Optional Slots)")]
        [Tooltip("Optional custom digital retro font. If empty, uses default TextMeshPro font.")]
        [SerializeField] private TMP_FontAsset customFont;

        [Tooltip("Optional custom helmet visor frame sprite. If empty, uses procedural vignette.")]
        [SerializeField] private Sprite customVisorFrameSprite;

        [Tooltip("Optional custom heart/ECG icon sprite. If empty, uses procedural pixel heart.")]
        [SerializeField] private Sprite customHeartIconSprite;

        [Tooltip("Optional custom oxygen tank icon sprite.")]
        [SerializeField] private Sprite customOxygenIconSprite;

        [Header("Player References")]
        [SerializeField] private AstronautSurvival survivalController;
        [SerializeField] private AstronautMovement6D movementController;
        [SerializeField] private HelmetFlashlight flashlightController;

        [Header("UI Component Bindings")]
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private Image visorFrameImage;
        [SerializeField] private Image oxygenFillBar;
        [SerializeField] private Image oxygenIconImage;
        [SerializeField] private TMP_Text oxygenValueText;

        [SerializeField] private Image heartIconImage;
        [SerializeField] private TMP_Text heartRateText;

        [SerializeField] private TMP_Text suitStatusText;
        [SerializeField] private TMP_Text rcsStatusText;
        [SerializeField] private TMP_Text lampStatusText;

        [Header("Interaction & Mission Telemetry")]
        [SerializeField] private CanvasGroup interactionPromptGroup;
        [SerializeField] private TMP_Text interactionPromptText;
        [SerializeField] private Image interactionProgressBar;
        [SerializeField] private TMP_Text interactionSubtext;
        [SerializeField] private CanvasGroup interactionStatusGroup;
        [SerializeField] private TMP_Text interactionStatusText;

        [Header("Mission Transmission Log")]
        [SerializeField] private CanvasGroup transmissionLogGroup;
        [SerializeField] private TMP_Text transmissionLogTitle;
        [SerializeField] private TMP_Text transmissionLogBody;

        [Header("Critical Suffocation & Horror FX")]
        [SerializeField] private CanvasGroup warningAlertGroup;
        [SerializeField] private TMP_Text warningAlertText;
        [SerializeField] private Image suffocationVignetteImage;

        [Header("Display Mode")]
        [Tooltip("Renders as crisp overlay on top of screen resolution (recommended for high readability)")]
        [SerializeField] private bool useCrispOverlay = true;

        // Film Noir Visual Palette (Chiaroscuro Silver & Stark Contrast)
        private readonly Color colNoirWhite = new Color(0.96f, 0.98f, 1.0f, 0.95f);       // Crisp stark white
        private readonly Color colNoirAshSilver = new Color(0.75f, 0.78f, 0.82f, 0.9f);    // Caution ash-silver
        private readonly Color colNoirDangerRed = new Color(1.0f, 0.22f, 0.25f, 0.95f);    // Emergency stark crimson
        private readonly Color colNoirMutedDim = new Color(0.5f, 0.54f, 0.58f, 0.7f);      // Inactive / Muted grey

        // Runtime states
        private float heartbeatTimer = 0f;
        private float heartIconScale = 1.0f;
        private bool isCriticalO2 = false;
        private float warningFlashTimer = 0f;
        private float statusMessageTimer = 0f;
        private float transmissionTimer = 0f;

        public void Initialize(AstronautSurvival survival, AstronautMovement6D movement, HelmetFlashlight flashlight)
        {
            survivalController = survival;
            movementController = movement;
            flashlightController = flashlight;

            if (hudCanvas != null && useCrispOverlay)
            {
                hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            SetupEventListeners();
            ApplyCustomAssets();
        }

        private void Awake()
        {
            if (hudCanvas == null)
                hudCanvas = GetComponent<Canvas>();

            if (hudCanvas != null && useCrispOverlay)
            {
                hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            if (survivalController == null)
                survivalController = GetComponentInParent<AstronautSurvival>();

            if (movementController == null)
                movementController = GetComponentInParent<AstronautMovement6D>();

            if (flashlightController == null)
                flashlightController = GetComponentInParent<HelmetFlashlight>();

            ApplyCustomAssets();
        }

        private void OnEnable()
        {
            SetupEventListeners();
        }

        private void OnDisable()
        {
            RemoveEventListeners();
        }

        private void SetupEventListeners()
        {
            if (survivalController != null)
            {
                survivalController.OnOxygenChanged += HandleOxygenChanged;
                survivalController.OnHeartRateChanged += HandleHeartRateChanged;
                survivalController.OnSuitIntegrityChanged += HandleSuitIntegrityChanged;
                survivalController.OnLowOxygenWarning += HandleLowOxygenWarning;
                survivalController.OnPlayerAsphyxiated += HandleAsphyxiated;
            }

            if (movementController != null)
            {
                movementController.OnStabilizerToggled += HandleRcsToggled;
            }

            if (flashlightController != null)
            {
                flashlightController.OnFlashlightToggled += HandleFlashlightToggled;
            }
        }

        private void RemoveEventListeners()
        {
            if (survivalController != null)
            {
                survivalController.OnOxygenChanged -= HandleOxygenChanged;
                survivalController.OnHeartRateChanged -= HandleHeartRateChanged;
                survivalController.OnSuitIntegrityChanged -= HandleSuitIntegrityChanged;
                survivalController.OnLowOxygenWarning -= HandleLowOxygenWarning;
                survivalController.OnPlayerAsphyxiated -= HandleAsphyxiated;
            }

            if (movementController != null)
            {
                movementController.OnStabilizerToggled -= HandleRcsToggled;
            }

            if (flashlightController != null)
            {
                flashlightController.OnFlashlightToggled -= HandleFlashlightToggled;
            }
        }

        private void Start()
        {
            // Initial UI sync
            if (survivalController != null)
            {
                HandleOxygenChanged(survivalController.CurrentOxygenNormalized);
                HandleHeartRateChanged(survivalController.CurrentHeartRate);
                HandleSuitIntegrityChanged(survivalController.CurrentSuitIntegrityNormalized);
            }

            if (movementController != null)
            {
                HandleRcsToggled(movementController.IsRcsActive);
            }

            if (flashlightController != null)
            {
                HandleFlashlightToggled(flashlightController.IsLightOn);
            }
        }

        private void Update()
        {
            UpdateHeartPulseAnimation();
            UpdateSuffocationVisuals();
            UpdateInteractionTimers();
        }

        /// <summary>
        /// Animates the heart icon scale to beat exactly with the astronaut's real-time BPM.
        /// </summary>
        private void UpdateHeartPulseAnimation()
        {
            if (survivalController == null || heartIconImage == null) return;

            float bpm = survivalController.CurrentHeartRate;
            float beatInterval = 60f / Mathf.Max(bpm, 40f);

            heartbeatTimer += Time.deltaTime;
            if (heartbeatTimer >= beatInterval)
            {
                heartbeatTimer -= beatInterval;
                heartIconScale = 1.38f; // Pulse expansion on systolic pump
            }

            heartIconScale = Mathf.MoveTowards(heartIconScale, 1.0f, Time.deltaTime * 3.5f);
            heartIconImage.transform.localScale = Vector3.one * heartIconScale;
        }

        /// <summary>
        /// Controls the suffocation tunnel-vision vignette and critical warning flashing.
        /// </summary>
        private void UpdateSuffocationVisuals()
        {
            if (survivalController == null) return;

            float o2 = survivalController.CurrentOxygenNormalized;

            // 1. Flashing Warning Banner
            if (isCriticalO2 && warningAlertGroup != null)
            {
                warningFlashTimer += Time.deltaTime * 5f;
                float flashAlpha = (Mathf.Sin(warningFlashTimer) + 1f) * 0.5f;
                warningAlertGroup.alpha = flashAlpha;
            }
            else if (warningAlertGroup != null)
            {
                warningAlertGroup.alpha = 0f;
            }

            // 2. Suffocation Tunnel-Vision Vignette
            if (suffocationVignetteImage != null)
            {
                if (survivalController.IsAsphyxiated)
                {
                    // Full blackout on death
                    suffocationVignetteImage.color = Color.Lerp(suffocationVignetteImage.color, new Color(0f, 0f, 0f, 1.0f), Time.deltaTime * 3f);
                }
                else if (o2 < 0.28f)
                {
                    // Hypoxia tunnel vision constricts inward
                    float hypoxiaFactor = 1f - (o2 / 0.28f);
                    // Pulsates with labored breathing
                    float breathPulse = Mathf.Sin(Time.time * (survivalController.CurrentHeartRate / 25f)) * 0.12f;
                    float targetAlpha = Mathf.Clamp01(hypoxiaFactor * 0.85f + breathPulse);

                    Color c = Color.black;
                    c.a = targetAlpha;
                    suffocationVignetteImage.color = c;
                }
                else
                {
                    // Clear visor when oxygen is safe
                    Color c = Color.black;
                    c.a = Mathf.MoveTowards(suffocationVignetteImage.color.a, 0f, Time.deltaTime * 2f);
                    suffocationVignetteImage.color = c;
                }
            }
        }

        private void HandleOxygenChanged(float normalizedO2)
        {
            if (oxygenFillBar != null)
            {
                oxygenFillBar.fillAmount = normalizedO2;

                // Color coding based on remaining supply (Film Noir Chiaroscuro)
                if (normalizedO2 > 0.45f)
                    oxygenFillBar.color = colNoirWhite;
                else if (normalizedO2 > 0.25f)
                    oxygenFillBar.color = colNoirAshSilver;
                else
                    oxygenFillBar.color = colNoirDangerRed;
            }

            if (oxygenValueText != null)
            {
                int percent = Mathf.RoundToInt(normalizedO2 * 100f);
                string statusTag = normalizedO2 > 0.25f ? "O2" : "<color=#FF3344>O2 CRIT</color>";
                oxygenValueText.text = $"{statusTag}: {percent}%";
            }
        }

        private void HandleHeartRateChanged(float bpm)
        {
            if (heartRateText != null)
            {
                int bpmInt = Mathf.RoundToInt(bpm);
                string colorHex = bpm > 130f ? "#FF3344" : (bpm > 100f ? "#CBD5E1" : "#FFFFFF");
                heartRateText.text = $"<color={colorHex}>{bpmInt} BPM</color>";
            }

            if (heartIconImage != null)
            {
                heartIconImage.color = bpm > 130f ? colNoirDangerRed : (bpm > 100f ? colNoirAshSilver : colNoirWhite);
            }
        }

        private void HandleSuitIntegrityChanged(float normalizedIntegrity)
        {
            if (suitStatusText != null)
            {
                int integrityPercent = Mathf.RoundToInt(normalizedIntegrity * 100f);
                if (normalizedIntegrity >= 0.99f)
                {
                    suitStatusText.text = "<color=#E2E8F0>SUIT: 100% OK</color>";
                }
                else
                {
                    suitStatusText.text = $"<color=#FF3344>LEAK: {integrityPercent}%</color>";
                }
            }
        }

        private void HandleLowOxygenWarning(bool isCritical)
        {
            isCriticalO2 = isCritical;
            if (warningAlertText != null)
            {
                warningAlertText.text = "[!] WARNING: OXYGEN LEVEL CRITICAL";
            }
        }

        private void HandleAsphyxiated()
        {
            isCriticalO2 = false;
            if (warningAlertGroup != null) warningAlertGroup.alpha = 1f;
            if (warningAlertText != null)
            {
                warningAlertText.text = "<color=#FF2233>LIFE SUPPORT TERMINATED\nSYSTEM OFFLINE</color>";
            }
        }

        private void HandleRcsToggled(bool isRcsActive)
        {
            if (rcsStatusText != null)
            {
                if (isRcsActive)
                    rcsStatusText.text = "<color=#E2E8F0>RCS: STABILIZED</color>";
                else
                    rcsStatusText.text = "<color=#94A3B8>RCS: DRIFT</color>";
            }
        }

        private void HandleFlashlightToggled(bool isFlashlightOn)
        {
            if (lampStatusText != null)
            {
                if (isFlashlightOn)
                    lampStatusText.text = "<color=#FFFFFF>LAMP: ON</color>";
                else
                    lampStatusText.text = "<color=#64748B>LAMP: OFF</color>";
            }
        }

        /// <summary>
        /// Applies custom external assets if assigned in Inspector slots; otherwise falls back gracefully.
        /// </summary>
        public void ApplyCustomAssets()
        {
            // Apply custom font if provided
            if (customFont != null)
            {
                if (oxygenValueText != null) oxygenValueText.font = customFont;
                if (heartRateText != null) heartRateText.font = customFont;
                if (suitStatusText != null) suitStatusText.font = customFont;
                if (rcsStatusText != null) rcsStatusText.font = customFont;
                if (lampStatusText != null) lampStatusText.font = customFont;
                if (warningAlertText != null) warningAlertText.font = customFont;
            }

            // Apply custom visor frame if provided
            if (customVisorFrameSprite != null && visorFrameImage != null)
            {
                visorFrameImage.sprite = customVisorFrameSprite;
                visorFrameImage.color = Color.white;
            }

            // Apply custom heart icon if provided
            if (customHeartIconSprite != null && heartIconImage != null)
            {
                heartIconImage.sprite = customHeartIconSprite;
            }

            // Apply custom oxygen icon if provided
            if (customOxygenIconSprite != null && oxygenIconImage != null)
            {
                oxygenIconImage.sprite = customOxygenIconSprite;
            }
        }

        private void UpdateInteractionTimers()
        {
            if (interactionStatusGroup != null)
            {
                if (statusMessageTimer > 0f)
                {
                    statusMessageTimer -= Time.deltaTime;
                }
                else if (interactionStatusGroup.alpha > 0f)
                {
                    interactionStatusGroup.alpha = Mathf.MoveTowards(interactionStatusGroup.alpha, 0f, Time.deltaTime * 3f);
                }
            }

            if (transmissionLogGroup != null)
            {
                if (transmissionTimer > 0f)
                {
                    transmissionTimer -= Time.deltaTime;
                }
                else if (transmissionLogGroup.alpha > 0f)
                {
                    transmissionLogGroup.alpha = Mathf.MoveTowards(transmissionLogGroup.alpha, 0f, Time.deltaTime * 2f);
                }
            }
        }

        /// <summary>
        /// Displays the interactive HUD prompt pod in the center of the helmet visor.
        /// </summary>
        public void ShowInteractionPrompt(string key, string prompt, float progress, string subtext = "")
        {
            if (interactionPromptGroup != null) interactionPromptGroup.alpha = 1f;
            if (interactionPromptText != null) interactionPromptText.text = $"[{key}] {prompt}";
            if (interactionProgressBar != null)
            {
                interactionProgressBar.gameObject.SetActive(progress > 0.005f);
                interactionProgressBar.fillAmount = progress;
            }
            if (interactionSubtext != null)
            {
                interactionSubtext.text = subtext;
                interactionSubtext.gameObject.SetActive(!string.IsNullOrEmpty(subtext));
            }
        }

        /// <summary>
        /// Hides the interactive prompt when player looks away or target is out of reach.
        /// </summary>
        public void HideInteractionPrompt()
        {
            if (interactionPromptGroup != null) interactionPromptGroup.alpha = 0f;
            if (interactionProgressBar != null) interactionProgressBar.gameObject.SetActive(false);
        }

        /// <summary>
        /// Shows a brief status confirmation toast upon interaction completion (e.g. 'OXYGEN REFILLED +40%').
        /// </summary>
        public void ShowInteractionStatusMessage(string message, float duration = 2.5f)
        {
            if (interactionStatusText != null) interactionStatusText.text = message;
            if (interactionStatusGroup != null) interactionStatusGroup.alpha = 1f;
            statusMessageTimer = duration;
        }

        /// <summary>
        /// Shows a full mission transmission log popup (e.g. Black Box transcript / distress call).
        /// </summary>
        public void ShowMissionTransmission(string title, string body, float duration = 7.0f)
        {
            if (transmissionLogTitle != null) transmissionLogTitle.text = title;
            if (transmissionLogBody != null) transmissionLogBody.text = body;
            if (transmissionLogGroup != null) transmissionLogGroup.alpha = 1f;
            transmissionTimer = duration;
        }

        /// <summary>
        /// Hides the transmission log immediately.
        /// </summary>
        public void HideMissionTransmission()
        {
            if (transmissionLogGroup != null) transmissionLogGroup.alpha = 0f;
            transmissionTimer = 0f;
        }

        /// <summary>
        /// Public setter to configure UI element bindings (used by AstronautPlayerBuilder).
        /// </summary>
        public void ConfigureBindings(
            Canvas canvas,
            Image visor,
            Image o2Bar,
            Image o2Icon,
            TMP_Text o2Text,
            Image heartIcon,
            TMP_Text heartText,
            TMP_Text suitText,
            TMP_Text rcsText,
            TMP_Text lampText,
            CanvasGroup warnGroup,
            TMP_Text warnText,
            Image vignette,
            CanvasGroup interactGroup = null,
            TMP_Text interactText = null,
            Image interactBar = null,
            TMP_Text interactSub = null,
            CanvasGroup statusGroup = null,
            TMP_Text statusText = null,
            CanvasGroup transGroup = null,
            TMP_Text transTitle = null,
            TMP_Text transBody = null)
        {
            hudCanvas = canvas;
            visorFrameImage = visor;
            oxygenFillBar = o2Bar;
            oxygenIconImage = o2Icon;
            oxygenValueText = o2Text;
            heartIconImage = heartIcon;
            heartRateText = heartText;
            suitStatusText = suitText;
            rcsStatusText = rcsText;
            lampStatusText = lampText;
            warningAlertGroup = warnGroup;
            warningAlertText = warnText;
            suffocationVignetteImage = vignette;

            interactionPromptGroup = interactGroup;
            interactionPromptText = interactText;
            interactionProgressBar = interactBar;
            interactionSubtext = interactSub;
            interactionStatusGroup = statusGroup;
            interactionStatusText = statusText;
            transmissionLogGroup = transGroup;
            transmissionLogTitle = transTitle;
            transmissionLogBody = transBody;
        }
    }
}
