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

        [Header("Mission Directive Pod (Top-Left)")]
        [SerializeField] private CanvasGroup directiveGroup;
        [SerializeField] private TMP_Text directiveDayText;
        [SerializeField] private TMP_Text directiveMessageText;

        [Header("Day Announcement Banner (Center Screen)")]
        [SerializeField] private CanvasGroup dayBannerGroup;
        [SerializeField] private TMP_Text dayBannerTitleText;
        [SerializeField] private TMP_Text dayBannerSubtitleText;
        private float dayBannerTimer = 0f;

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
        private readonly Color colNoirStarkInvert = new Color(1.0f, 1.0f, 1.0f, 1.0f);     // High-contrast stark white strobe
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
            EnsureToolsPanelLayout();
            EnsureDirectivePanelLayout();
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
            EnsureToolsPanelLayout();
            EnsureDirectivePanelLayout();
        }

        private CanvasGroup hudCanvasGroup;

        /// <summary>
        /// Modulates overall HUD alpha (e.g. for waking-up cold-boot CRT flicker or blackout sequences).
        /// </summary>
        public void SetHUDAlpha(float alpha)
        {
            if (hudCanvasGroup == null)
            {
                hudCanvasGroup = GetComponent<CanvasGroup>();
                if (hudCanvasGroup == null)
                {
                    hudCanvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
            hudCanvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        /// <summary>
        /// Repositions the Tools panel (RCS & Lamp) to the Top-Right corner so it never collides with the Signal Tracker.
        /// </summary>
        private void EnsureToolsPanelLayout()
        {
            if (rcsStatusText != null)
            {
                RectTransform panelRect = rcsStatusText.transform.parent as RectTransform;
                if (panelRect != null)
                {
                    panelRect.anchorMin = new Vector2(1f, 1f);
                    panelRect.anchorMax = new Vector2(1f, 1f);
                    panelRect.pivot = new Vector2(1f, 1f);
                    panelRect.anchoredPosition = new Vector2(-45f, -22f);
                    panelRect.sizeDelta = new Vector2(320f, 38f);
                }
            }
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

                // Contrast coding based on remaining supply (Film Noir Chiaroscuro)
                if (normalizedO2 > 0.45f)
                    oxygenFillBar.color = colNoirWhite;
                else if (normalizedO2 > 0.25f)
                    oxygenFillBar.color = colNoirAshSilver;
                else
                    oxygenFillBar.color = colNoirStarkInvert;
            }

            if (oxygenValueText != null)
            {
                int percent = Mathf.RoundToInt(normalizedO2 * 100f);
                string statusTag = normalizedO2 > 0.25f ? "O2" : "[!] O2 CRIT";
                oxygenValueText.text = $"{statusTag}: {percent}%";
            }
        }

        private void HandleHeartRateChanged(float bpm)
        {
            if (heartRateText != null)
            {
                int bpmInt = Mathf.RoundToInt(bpm);
                heartRateText.text = $"{bpmInt} BPM";
            }

            if (heartIconImage != null)
            {
                heartIconImage.color = bpm > 130f ? colNoirStarkInvert : (bpm > 100f ? colNoirAshSilver : colNoirWhite);
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
                    suitStatusText.text = $"[!] SUIT LEAK: {integrityPercent}%";
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
                warningAlertText.text = "LIFE SUPPORT TERMINATED\nSYSTEM OFFLINE";
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
                if (directiveDayText != null) directiveDayText.font = customFont;
                if (directiveMessageText != null) directiveMessageText.font = customFont;
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

            if (dayBannerGroup != null)
            {
                if (dayBannerTimer > 0f)
                {
                    dayBannerTimer -= Time.deltaTime;
                    dayBannerGroup.alpha = Mathf.MoveTowards(dayBannerGroup.alpha, 1f, Time.deltaTime * 3f);
                }
                else if (dayBannerGroup.alpha > 0f)
                {
                    dayBannerGroup.alpha = Mathf.MoveTowards(dayBannerGroup.alpha, 0f, Time.deltaTime * 2.5f);
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
        /// Updates the Paint-style Objective Card on the visor HUD (Day title on top, box with bullets below).
        /// </summary>
        public void SetMissionDirective(int dayNumber, string directiveText)
        {
            EnsureDirectivePanelLayout();
            if (directiveDayText != null) directiveDayText.text = $"DAY {dayNumber}";

            if (directiveMessageText != null)
            {
                if (!directiveText.StartsWith("-"))
                {
                    directiveMessageText.text = $"- {directiveText}";
                }
                else
                {
                    directiveMessageText.text = directiveText;
                }
            }

            if (directiveGroup != null) directiveGroup.alpha = 1f;
        }

        /// <summary>
        /// Updates the Objective Card with structured bullet points and visual progress indicators (matching Paint layout).
        /// Supports individual completedStates for multi-objective contracts.
        /// </summary>
        public void SetObjectiveChecklist(int dayNumber, string[] tasks, bool[] completedStates, int activeTaskIndex = -1)
        {
            EnsureDirectivePanelLayout();
            if (directiveDayText != null) directiveDayText.text = $"DAY {dayNumber}";

            if (directiveMessageText != null && tasks != null)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < tasks.Length; i++)
                {
                    bool isDone = completedStates != null && i < completedStates.Length ? completedStates[i] : (activeTaskIndex >= 0 && i < activeTaskIndex);
                    bool isActive = (i == activeTaskIndex);

                    if (isDone)
                    {
                        // Completed task: Dimmed struck-through silver with [DONE] tag
                        sb.AppendLine($"<color=#64748B>- <s>{tasks[i]}</s>  <size=10>[DONE]</size></color>");
                    }
                    else if (isActive)
                    {
                        // Current active task: Stark bright white bold
                        sb.AppendLine($"<color=#FFFFFF>- <b>{tasks[i]}</b></color>");
                    }
                    else
                    {
                        // Upcoming pending task: Subtle muted silver
                        sb.AppendLine($"<color=#94A3B8>- {tasks[i]}</color>");
                    }
                }
                directiveMessageText.text = sb.ToString().TrimEnd();
            }

            if (directiveGroup != null) directiveGroup.alpha = 1f;
        }

        /// <summary>
        /// Convenience overload for linear progression tasks.
        /// </summary>
        public void SetObjectiveChecklist(int dayNumber, string[] tasks, int activeTaskIndex = 0)
        {
            SetObjectiveChecklist(dayNumber, tasks, null, activeTaskIndex);
        }

        /// <summary>
        /// Displays a dramatic centered day title card (e.g. 'DAY 01' // 'ORBITAL EXPEDITION')
        /// </summary>
        public void ShowDayBanner(int dayNumber, string title, float duration = 3.5f)
        {
            EnsureDayBannerLayout();
            if (dayBannerTitleText != null) dayBannerTitleText.text = $"DAY {dayNumber:D2}";
            if (dayBannerSubtitleText != null) dayBannerSubtitleText.text = title;
            if (dayBannerGroup != null)
            {
                dayBannerGroup.alpha = 0f;
                dayBannerGroup.blocksRaycasts = false;
                dayBannerGroup.interactable = false;
            }
            dayBannerTimer = duration;
        }

        /// <summary>
        /// Immediately hides the day announcement banner.
        /// </summary>
        public void HideDayBanner()
        {
            dayBannerTimer = 0f;
            if (dayBannerGroup != null)
            {
                dayBannerGroup.alpha = 0f;
            }
        }

        [ContextMenu("Rebuild Objective Card Layout")]
        public void RebuildObjectiveCardLayout()
        {
            if (hudCanvas == null) hudCanvas = GetComponent<Canvas>();
            if (hudCanvas == null) return;

            Transform existing = hudCanvas.transform.Find("Objective_Card_Panel");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
                directiveGroup = null;
                directiveDayText = null;
                directiveMessageText = null;
            }
            EnsureDirectivePanelLayout();
        }

        private void EnsureDirectivePanelLayout()
        {
            if (directiveGroup != null && directiveMessageText != null) return;
            if (hudCanvas == null) hudCanvas = GetComponent<Canvas>();
            if (hudCanvas == null) return;

            // Remove legacy cramped horizontal panel if present
            Transform oldPanel = hudCanvas.transform.Find("Top_Directive_Panel");
            if (oldPanel != null)
            {
                DestroyImmediate(oldPanel.gameObject);
            }

            Transform existing = hudCanvas.transform.Find("Objective_Card_Panel");
            if (existing != null)
            {
                directiveGroup = existing.GetComponent<CanvasGroup>();
                directiveDayText = existing.Find("Day_Title_Text")?.GetComponent<TMP_Text>();
                directiveMessageText = existing.Find("Box_Frame/Box_Inner/Objective_List_Text")?.GetComponent<TMP_Text>();
                if (directiveMessageText != null) return;
                DestroyImmediate(existing.gameObject);
            }

            // Root Card Container (Top-Left corner)
            GameObject cardRoot = new GameObject("Objective_Card_Panel");
            cardRoot.transform.SetParent(hudCanvas.transform, false);

            RectTransform rootRect = cardRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(40f, -25f);
            rootRect.sizeDelta = new Vector2(340f, 215f);

            directiveGroup = cardRoot.AddComponent<CanvasGroup>();
            directiveGroup.alpha = 1f;

            // 1. "DAY 1" Header on Top (Outside / Above the Box)
            GameObject dayObj = new GameObject("Day_Title_Text");
            dayObj.transform.SetParent(cardRoot.transform, false);
            RectTransform dayRect = dayObj.AddComponent<RectTransform>();
            dayRect.anchorMin = new Vector2(0f, 1f);
            dayRect.anchorMax = new Vector2(1f, 1f);
            dayRect.pivot = new Vector2(0f, 1f);
            dayRect.anchoredPosition = new Vector2(2f, 0f);
            dayRect.sizeDelta = new Vector2(0f, 30f);

            directiveDayText = dayObj.AddComponent<TextMeshProUGUI>();
            directiveDayText.text = "DAY 1";
            directiveDayText.fontSize = 24f;
            directiveDayText.fontStyle = FontStyles.Bold;
            directiveDayText.color = new Color(0.96f, 0.98f, 1f, 1f);
            directiveDayText.alignment = TextAlignmentOptions.Left;

            // 2. The Box Frame (White Outline Border, 2px)
            GameObject boxFrameObj = new GameObject("Box_Frame");
            boxFrameObj.transform.SetParent(cardRoot.transform, false);
            RectTransform frameRect = boxFrameObj.AddComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0f, 0f);
            frameRect.anchorMax = new Vector2(1f, 1f);
            frameRect.pivot = new Vector2(0f, 1f);
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = new Vector2(0f, -34f); // Sits right below DAY 1

            Image frameBorder = boxFrameObj.AddComponent<Image>();
            frameBorder.color = new Color(0.96f, 0.98f, 1f, 1f); // Stark Crisp White Border

            // 3. Inner Box (Obsidian Dark Background inset by 2px to leave crisp border)
            GameObject boxInnerObj = new GameObject("Box_Inner");
            boxInnerObj.transform.SetParent(boxFrameObj.transform, false);
            RectTransform innerRect = boxInnerObj.AddComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(2f, 2f);
            innerRect.offsetMax = new Vector2(-2f, -2f);

            Image innerBg = boxInnerObj.AddComponent<Image>();
            innerBg.color = new Color(0.02f, 0.025f, 0.035f, 0.92f); // Deep Obsidian Backing

            // 4. "OBJECTIVE" Header inside Box
            GameObject objHeader = new GameObject("Objective_Header_Text");
            objHeader.transform.SetParent(boxInnerObj.transform, false);
            RectTransform objHeaderRect = objHeader.AddComponent<RectTransform>();
            objHeaderRect.anchorMin = new Vector2(0f, 1f);
            objHeaderRect.anchorMax = new Vector2(1f, 1f);
            objHeaderRect.pivot = new Vector2(0f, 1f);
            objHeaderRect.anchoredPosition = new Vector2(16f, -10f);
            objHeaderRect.sizeDelta = new Vector2(-32f, 26f);

            TMP_Text headerText = objHeader.AddComponent<TextMeshProUGUI>();
            headerText.text = "OBJECTIVE";
            headerText.fontSize = 16f;
            headerText.fontStyle = FontStyles.Bold;
            headerText.characterSpacing = 2.0f;
            headerText.color = new Color(0.96f, 0.98f, 1f, 1f);
            headerText.alignment = TextAlignmentOptions.Left;

            // 5. Divider Line under OBJECTIVE
            GameObject divObj = new GameObject("Header_Divider");
            divObj.transform.SetParent(boxInnerObj.transform, false);
            RectTransform divRect = divObj.AddComponent<RectTransform>();
            divRect.anchorMin = new Vector2(0f, 1f);
            divRect.anchorMax = new Vector2(1f, 1f);
            divRect.pivot = new Vector2(0f, 0.5f);
            divRect.anchoredPosition = new Vector2(14f, -38f);
            divRect.sizeDelta = new Vector2(-28f, 1.5f);

            Image divImg = divObj.AddComponent<Image>();
            divImg.color = new Color(0.65f, 0.70f, 0.78f, 0.40f);

            // 6. Objective List Text (Bullet points with auto-wrapping)
            GameObject listObj = new GameObject("Objective_List_Text");
            listObj.transform.SetParent(boxInnerObj.transform, false);
            RectTransform listRect = listObj.AddComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0f, 0f);
            listRect.anchorMax = new Vector2(1f, 1f);
            listRect.pivot = new Vector2(0f, 1f);
            listRect.offsetMin = new Vector2(16f, 12f);
            listRect.offsetMax = new Vector2(-16f, -44f);

            directiveMessageText = listObj.AddComponent<TextMeshProUGUI>();
            directiveMessageText.text = "- Access Ship Mission Terminal\n- Salvage Satellite Log (142.8 MHz)\n- Return to Ship Airlock (121.5 MHz)";
            directiveMessageText.fontSize = 13.5f;
            directiveMessageText.lineSpacing = 8f;
            directiveMessageText.color = new Color(0.88f, 0.90f, 0.94f, 0.95f);
            directiveMessageText.alignment = TextAlignmentOptions.TopLeft;
            directiveMessageText.textWrappingMode = TextWrappingModes.Normal;
            directiveMessageText.overflowMode = TextOverflowModes.Overflow;
        }

        private void EnsureDayBannerLayout()
        {
            if (dayBannerGroup != null) return;
            if (hudCanvas == null) hudCanvas = GetComponent<Canvas>();
            if (hudCanvas == null) return;

            Transform existing = hudCanvas.transform.Find("Day_Banner_Panel");
            if (existing != null)
            {
                dayBannerGroup = existing.GetComponent<CanvasGroup>();
                dayBannerTitleText = existing.Find("Day_Title")?.GetComponent<TMP_Text>();
                dayBannerSubtitleText = existing.Find("Day_Subtitle")?.GetComponent<TMP_Text>();
                return;
            }

            GameObject bannerObj = new GameObject("Day_Banner_Panel");
            bannerObj.transform.SetParent(hudCanvas.transform, false);

            RectTransform rect = bannerObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 60f);
            rect.sizeDelta = new Vector2(600f, 90f);

            dayBannerGroup = bannerObj.AddComponent<CanvasGroup>();
            dayBannerGroup.alpha = 0f;
            dayBannerGroup.blocksRaycasts = false;
            dayBannerGroup.interactable = false;

            GameObject titleObj = new GameObject("Day_Title");
            titleObj.transform.SetParent(bannerObj.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, 0f);
            titleRect.sizeDelta = new Vector2(580f, 44f);

            dayBannerTitleText = titleObj.AddComponent<TextMeshProUGUI>();
            dayBannerTitleText.text = "DAY 01";
            dayBannerTitleText.fontSize = 32f;
            dayBannerTitleText.fontStyle = FontStyles.Bold;
            dayBannerTitleText.color = new Color(0.96f, 0.98f, 1f, 1f);
            dayBannerTitleText.alignment = TextAlignmentOptions.Center;

            GameObject subObj = new GameObject("Day_Subtitle");
            subObj.transform.SetParent(bannerObj.transform, false);
            RectTransform subRect = subObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0f);
            subRect.anchorMax = new Vector2(0.5f, 0f);
            subRect.pivot = new Vector2(0.5f, 0f);
            subRect.anchoredPosition = new Vector2(0f, 10f);
            subRect.sizeDelta = new Vector2(580f, 26f);

            dayBannerSubtitleText = subObj.AddComponent<TextMeshProUGUI>();
            dayBannerSubtitleText.text = "OPERATION: RECOVERY PROTOCOL";
            dayBannerSubtitleText.fontSize = 15f;
            dayBannerSubtitleText.color = new Color(0.75f, 0.78f, 0.85f, 0.9f);
            dayBannerSubtitleText.alignment = TextAlignmentOptions.Center;
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
