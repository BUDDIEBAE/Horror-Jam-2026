using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SpaceHorror.Audio;
using SpaceHorror.UI;

namespace SpaceHorror.Player
{
    /// <summary>
    /// Immersive Scene Entrance Awakening Transition for 6DoF Deep Space Horror.
    /// Emulates the visceral sensation of regaining consciousness and opening your eyes:
    /// - Dual biological eyelid aperture (Upper & Lower) with soft organic eyelash blur gradient.
    /// - Drowsy flutter curve: Eyes struggle open to a slit, flutter shut heavily, then snap wide open.
    /// - Physical chin lift & equilibrium recovery: Astronaut's head starts slumped chin-down (-11°),
    ///   smoothly lifting to horizon level as consciousness returns.
    /// - Visor HUD cold-boot CRT flicker sequence: Telemetry flickers to life as eyes open.
    /// - Visceral suit acoustics: Awakening sharp breath gasp + elevated adrenaline heart rate.
    /// - Temporary player input lock to ensure cinematic immersion before granting full 6DoF control.
    /// - 100% Film Noir aesthetic (Deep obsidian blacks, monochrome chiaroscuro, zero RGB hues).
    /// </summary>
    public class AstronautWakeUpTransition : MonoBehaviour
    {
        [Header("Transition Settings")]
        [Tooltip("Automatically play the waking transition as soon as the scene loads")]
        [SerializeField] private bool playOnSceneStart = true;

        [Tooltip("Total duration of the waking sequence in seconds")]
        [Range(2.0f, 6.0f)]
        [SerializeField] private float transitionDuration = 3.6f;

        [Tooltip("Allow player to press Space or Enter to skip the transition")]
        [SerializeField] private bool allowSkip = false;

        [Header("Eyelid Motion & Biological Flutter")]
        [Tooltip("Animation curve governing eyelid aperture (0 = shut, 1 = fully open)")]
        [SerializeField] private AnimationCurve eyeOpennessCurve = GetDefaultAwakeningCurve();

        [Tooltip("Height of the soft biological eyelash/eyelid blur fringe in pixels")]
        [SerializeField] private float fringeHeight = 90f;

        [Header("Head Equilibrium Recovery (Chin Lift)")]
        [Tooltip("Simulate astronaut's head lifting up from a slumped, unconscious posture")]
        [SerializeField] private bool enableHeadLift = true;

        [Tooltip("Initial downward pitch angle of the helmet (negative = looking down into chest)")]
        [SerializeField] private float initialChinDownPitch = -11.0f;

        [Tooltip("Initial slumped roll angle of the helmet")]
        [SerializeField] private float initialHeadRollTilt = 2.8f;

        [Header("Visor HUD Cold Boot")]
        [Tooltip("Synchronize visor HUD opacity to cold-boot CRT flicker during waking")]
        [SerializeField] private bool bootVisorHUD = true;

        [Tooltip("Normalized transition progress when HUD electronics flicker to life (0 to 1)")]
        [Range(0.2f, 0.7f)]
        [SerializeField] private float hudBootStartNormalized = 0.45f;

        [Header("Awakening Audio & Adrenaline")]
        [Tooltip("Trigger visceral suit intake gasp and heart rate surge")]
        [SerializeField] private bool playAwakeningAudio = true;

        [Tooltip("Delay in seconds from start before triggering the breath gasp")]
        [SerializeField] private float audioTriggerDelay = 0.25f;

        // Component References
        [Header("References (Auto-detected if null)")]
        [SerializeField] private AstronautInputHandler inputHandler;
        [SerializeField] private AstronautCameraRig cameraRig;
        [SerializeField] private AstronautAudioSystem audioSystem;
        [SerializeField] private AstronautSurvival survivalSystem;
        [SerializeField] private AstronautHelmetHUD helmetHUD;

        // UI Overlay Elements
        private Canvas transitionCanvas;
        private RectTransform topEyelidRect;
        private RectTransform bottomEyelidRect;
        private Image drowsyVignetteImage;
        private CanvasGroup canvasGroup;

        // Runtime States
        public bool IsWakingUp { get; private set; } = false;
        private Coroutine activeTransitionCoroutine;

        // Public Event
        public event Action OnWakeUpCompleted;

        private void Awake()
        {
            ResolveReferences();
            BuildProceduralTransitionUI();

            if (playOnSceneStart)
            {
                // Instantly black out screen and lock inputs before the first frame renders!
                ApplyInstantBlackoutState();
            }
        }

        private void Start()
        {
            if (playOnSceneStart)
            {
                StartWakeUpSequence();
            }
        }

        private void Update()
        {
            if (IsWakingUp && allowSkip)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                {
                    CompleteWakeUpInstantly();
                }
            }
        }

        private void ResolveReferences()
        {
            if (inputHandler == null) inputHandler = GetComponentInParent<AstronautInputHandler>();
            if (cameraRig == null) cameraRig = GetComponentInChildren<AstronautCameraRig>();
            if (cameraRig == null) cameraRig = FindFirstObjectByType<AstronautCameraRig>();
            if (audioSystem == null) audioSystem = GetComponentInParent<AstronautAudioSystem>();
            if (audioSystem == null) audioSystem = FindFirstObjectByType<AstronautAudioSystem>();
            if (survivalSystem == null) survivalSystem = GetComponentInParent<AstronautSurvival>();
            if (survivalSystem == null) survivalSystem = FindFirstObjectByType<AstronautSurvival>();
            if (helmetHUD == null) helmetHUD = FindFirstObjectByType<AstronautHelmetHUD>();
        }

        /// <summary>
        /// Instantly blacks out view, slumps the head, and zeroes inputs so no world visuals bleed through.
        /// </summary>
        private void ApplyInstantBlackoutState()
        {
            if (inputHandler != null)
            {
                inputHandler.InputEnabled = false;
            }

            if (enableHeadLift && cameraRig != null)
            {
                cameraRig.SetExternalEulerOffset(new Vector3(initialChinDownPitch, 0f, initialHeadRollTilt));
            }

            if (bootVisorHUD && helmetHUD != null)
            {
                helmetHUD.SetHUDAlpha(0f);
            }

            UpdateEyelidPositions(0f);

            if (transitionCanvas != null)
            {
                transitionCanvas.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Public entry point to trigger the waking transition at any time (e.g. after respawn or cutscene).
        /// </summary>
        public void TriggerWakeUp(Action onComplete = null)
        {
            ResolveReferences();
            if (activeTransitionCoroutine != null)
            {
                StopCoroutine(activeTransitionCoroutine);
            }

            activeTransitionCoroutine = StartCoroutine(WakeUpRoutine(onComplete));
        }

        private void StartWakeUpSequence()
        {
            if (activeTransitionCoroutine != null)
            {
                StopCoroutine(activeTransitionCoroutine);
            }

            activeTransitionCoroutine = StartCoroutine(WakeUpRoutine(null));
        }

        private IEnumerator WakeUpRoutine(Action onComplete)
        {
            IsWakingUp = true;
            ApplyInstantBlackoutState();

            float elapsed = 0f;
            bool audioTriggered = false;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedProgress = Mathf.Clamp01(elapsed / transitionDuration);

                // 1. Biological Eyelid Aperture
                float openness = eyeOpennessCurve.Evaluate(normalizedProgress);
                UpdateEyelidPositions(openness);

                // 2. Drowsy Peripheral Blur & Vignette (Fades out as eyes adapt to light)
                if (drowsyVignetteImage != null)
                {
                    float vignetteAlpha = Mathf.Lerp(0.95f, 0f, Mathf.Pow(openness, 1.5f));
                    drowsyVignetteImage.color = new Color(0.01f, 0.01f, 0.02f, vignetteAlpha);
                }

                // 3. Audio Awakening Gasp & Heart Rate Surge
                if (playAwakeningAudio && !audioTriggered && elapsed >= audioTriggerDelay)
                {
                    audioTriggered = true;
                    if (audioSystem != null)
                    {
                        audioSystem.TriggerWakeUpGasp();
                    }
                    else if (survivalSystem != null)
                    {
                        survivalSystem.TriggerHeartSpike(45f);
                    }
                }

                // 4. Physical Head Lift & Equilibrium Recovery
                if (enableHeadLift && cameraRig != null)
                {
                    // Head begins lifting once eyes start opening, smoothly settling to level horizon
                    float liftProgress = Mathf.Clamp01((normalizedProgress - 0.15f) / 0.80f);
                    float smoothLift = Mathf.SmoothStep(0f, 1f, liftProgress);

                    float currentPitch = Mathf.Lerp(initialChinDownPitch, 0f, smoothLift);
                    float currentRoll = Mathf.Lerp(initialHeadRollTilt, 0f, smoothLift);
                    cameraRig.SetExternalEulerOffset(new Vector3(currentPitch, 0f, currentRoll));
                }

                // 5. Visor HUD Cold-Boot CRT Sequence
                if (bootVisorHUD && helmetHUD != null)
                {
                    if (normalizedProgress < hudBootStartNormalized)
                    {
                        helmetHUD.SetHUDAlpha(0f);
                    }
                    else
                    {
                        float hudProgress = (normalizedProgress - hudBootStartNormalized) / (1f - hudBootStartNormalized);
                        float hudAlpha = CalculateCrtBootAlpha(hudProgress);
                        helmetHUD.SetHUDAlpha(hudAlpha);
                    }
                }

                // 6. Early Input Unlock (Regain subtle control near the tail end of opening)
                if (inputHandler != null && !inputHandler.InputEnabled && normalizedProgress >= 0.88f)
                {
                    inputHandler.InputEnabled = true;
                }

                yield return null;
            }

            FinishWakeUp(onComplete);
        }

        /// <summary>
        /// Simulates authentic retro CRT phosphor warming up with sharp scanline flicker.
        /// </summary>
        private float CalculateCrtBootAlpha(float progress)
        {
            if (progress <= 0f) return 0f;
            if (progress >= 1f) return 1f;

            if (progress < 0.15f)
            {
                // Quick initial phosphor spark flash
                return Mathf.PingPong(progress * 25f, 0.35f);
            }
            else if (progress < 0.35f)
            {
                // Power dip flicker
                return 0.08f + Mathf.PingPong(progress * 18f, 0.25f);
            }
            else if (progress < 0.60f)
            {
                // High-voltage power-on surge
                return Mathf.Lerp(0.35f, 0.90f, (progress - 0.35f) / 0.25f)
                       + (Mathf.Sin(progress * 60f) > 0.4f ? 0.15f : -0.1f);
            }
            else
            {
                // Settle smoothly into clean 100% stark telemetry
                return Mathf.Lerp(0.85f, 1.0f, (progress - 0.60f) / 0.40f);
            }
        }

        /// <summary>
        /// Computes normalized eyelid boundary coordinates with overlap to guarantee zero light bleeding.
        /// </summary>
        private void UpdateEyelidPositions(float openness)
        {
            openness = Mathf.Clamp01(openness);

            if (topEyelidRect != null && bottomEyelidRect != null)
            {
                // Closed state: Upper lid reaches down to 0.48, Lower lid reaches up to 0.52 (4% overlap = pitch black)
                // Open state: Upper lid retracts to 1.05, Lower lid retracts to -0.05 (completely off-screen)
                float topLidBottom = Mathf.Lerp(0.48f, 1.05f, openness);
                float bottomLidTop = Mathf.Lerp(0.52f, -0.05f, openness);

                topEyelidRect.anchorMin = new Vector2(0f, topLidBottom);
                topEyelidRect.anchorMax = new Vector2(1f, 1.0f);
                topEyelidRect.offsetMin = Vector2.zero;
                topEyelidRect.offsetMax = Vector2.zero;

                bottomEyelidRect.anchorMin = new Vector2(0f, 0.0f);
                bottomEyelidRect.anchorMax = new Vector2(1f, bottomLidTop);
                bottomEyelidRect.offsetMin = Vector2.zero;
                bottomEyelidRect.offsetMax = Vector2.zero;
            }
        }

        private void CompleteWakeUpInstantly()
        {
            if (activeTransitionCoroutine != null)
            {
                StopCoroutine(activeTransitionCoroutine);
            }

            FinishWakeUp(null);
        }

        private void FinishWakeUp(Action onComplete)
        {
            IsWakingUp = false;
            activeTransitionCoroutine = null;

            UpdateEyelidPositions(1.0f);

            if (inputHandler != null)
            {
                inputHandler.InputEnabled = true;
            }

            if (enableHeadLift && cameraRig != null)
            {
                cameraRig.SetExternalEulerOffset(Vector3.zero);
            }

            if (bootVisorHUD && helmetHUD != null)
            {
                helmetHUD.SetHUDAlpha(1.0f);
            }

            if (transitionCanvas != null)
            {
                transitionCanvas.gameObject.SetActive(false);
            }

            OnWakeUpCompleted?.Invoke();
            onComplete?.Invoke();
        }

        #region Procedural UI Canvas Generation

        private void BuildProceduralTransitionUI()
        {
            // Remove existing overlay if present
            Transform existing = transform.Find("Awakening_Transition_Canvas");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            GameObject canvasObj = new GameObject("Awakening_Transition_Canvas");
            canvasObj.transform.SetParent(transform, false);

            transitionCanvas = canvasObj.AddComponent<Canvas>();
            transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transitionCanvas.sortingOrder = 999; // Render atop all UI, HUD, and post-processing

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = canvasObj.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            Sprite flatWhiteSprite = GetOrCreateFlatSprite();
            Sprite softFringeSprite = GetOrCreateSoftFringeSprite();
            Sprite centralVignetteSprite = GetOrCreateVignetteSprite();

            // 1. Drowsy Peripheral Vignette (Subtle pupil dilation / tunnel vision)
            GameObject vigObj = new GameObject("Drowsy_Vignette");
            vigObj.transform.SetParent(canvasObj.transform, false);
            RectTransform vigRect = vigObj.AddComponent<RectTransform>();
            vigRect.anchorMin = Vector2.zero;
            vigRect.anchorMax = Vector2.one;
            vigRect.offsetMin = Vector2.zero;
            vigRect.offsetMax = Vector2.zero;

            drowsyVignetteImage = vigObj.AddComponent<Image>();
            drowsyVignetteImage.sprite = centralVignetteSprite;
            drowsyVignetteImage.color = new Color(0.01f, 0.01f, 0.02f, 0.95f);
            drowsyVignetteImage.raycastTarget = false;

            // 2. Top Eyelid (Upper lid + bottom soft eyelash fringe)
            GameObject topObj = new GameObject("Top_Eyelid");
            topObj.transform.SetParent(canvasObj.transform, false);
            topEyelidRect = topObj.AddComponent<RectTransform>();
            topEyelidRect.anchorMin = new Vector2(0f, 0.48f);
            topEyelidRect.anchorMax = new Vector2(1f, 1f);
            topEyelidRect.pivot = new Vector2(0.5f, 1f);
            topEyelidRect.offsetMin = Vector2.zero;
            topEyelidRect.offsetMax = Vector2.zero;

            Image topSolid = topObj.AddComponent<Image>();
            topSolid.sprite = flatWhiteSprite;
            topSolid.color = new Color(0.005f, 0.005f, 0.008f, 1.0f); // Deep pitch obsidian
            topSolid.raycastTarget = false;

            // Soft blur fringe on bottom rim of top eyelid
            GameObject topFringeObj = new GameObject("Top_Eyelid_Fringe");
            topFringeObj.transform.SetParent(topObj.transform, false);
            RectTransform topFringeRect = topFringeObj.AddComponent<RectTransform>();
            topFringeRect.anchorMin = new Vector2(0f, 0f);
            topFringeRect.anchorMax = new Vector2(1f, 0f);
            topFringeRect.pivot = new Vector2(0.5f, 1f); // Hangs downward from bottom of eyelid
            topFringeRect.anchoredPosition = Vector2.zero;
            topFringeRect.sizeDelta = new Vector2(0f, fringeHeight);

            Image topFringeImg = topFringeObj.AddComponent<Image>();
            topFringeImg.sprite = softFringeSprite;
            topFringeImg.color = new Color(0.005f, 0.005f, 0.008f, 1.0f);
            topFringeImg.raycastTarget = false;

            // 3. Bottom Eyelid (Lower lid + top soft eyelash fringe)
            GameObject bottomObj = new GameObject("Bottom_Eyelid");
            bottomObj.transform.SetParent(canvasObj.transform, false);
            bottomEyelidRect = bottomObj.AddComponent<RectTransform>();
            bottomEyelidRect.anchorMin = new Vector2(0f, 0f);
            bottomEyelidRect.anchorMax = new Vector2(1f, 0.52f);
            bottomEyelidRect.pivot = new Vector2(0.5f, 0f);
            bottomEyelidRect.offsetMin = Vector2.zero;
            bottomEyelidRect.offsetMax = Vector2.zero;

            Image bottomSolid = bottomObj.AddComponent<Image>();
            bottomSolid.sprite = flatWhiteSprite;
            bottomSolid.color = new Color(0.005f, 0.005f, 0.008f, 1.0f);
            bottomSolid.raycastTarget = false;

            // Soft blur fringe on top rim of bottom eyelid (flipped vertically)
            GameObject bottomFringeObj = new GameObject("Bottom_Eyelid_Fringe");
            bottomFringeObj.transform.SetParent(bottomObj.transform, false);
            RectTransform bottomFringeRect = bottomFringeObj.AddComponent<RectTransform>();
            bottomFringeRect.anchorMin = new Vector2(0f, 1f);
            bottomFringeRect.anchorMax = new Vector2(1f, 1f);
            bottomFringeRect.pivot = new Vector2(0.5f, 0f); // Extends upward from top of eyelid
            bottomFringeRect.anchoredPosition = Vector2.zero;
            bottomFringeRect.sizeDelta = new Vector2(0f, fringeHeight);
            bottomFringeRect.localScale = new Vector3(1f, -1f, 1f); // Invert gradient

            Image bottomFringeImg = bottomFringeObj.AddComponent<Image>();
            bottomFringeImg.sprite = softFringeSprite;
            bottomFringeImg.color = new Color(0.005f, 0.005f, 0.008f, 1.0f);
            bottomFringeImg.raycastTarget = false;
        }

        #endregion

        #region Procedural Visual Sprites

        private static Sprite GetOrCreateFlatSprite()
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color[] colors = new Color[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(colors);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Generates a smooth vertical organic gradient (opaque black -> transparent)
        /// to simulate the soft blurry edge of biological eyelids and eyelashes.
        /// </summary>
        private static Sprite GetOrCreateSoftFringeSprite()
        {
            int width = 8;
            int height = 64;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                // y = 0 is transparent tip, y = height-1 is solid opaque lid
                float t = (float)y / (height - 1);
                // Smooth exponential cosine curve for biological soft falloff
                float alpha = Mathf.SmoothStep(0f, 1f, t);
                alpha = Mathf.Pow(alpha, 1.4f);

                Color c = new Color(1f, 1f, 1f, alpha);
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }

        private static Sprite GetOrCreateVignetteSprite()
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxRadius = size * 0.5f;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float normDist = Mathf.Clamp01(dist / maxRadius);
                    float alpha = Mathf.SmoothStep(0f, 1f, normDist);
                    alpha = Mathf.Pow(alpha, 2.2f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        #endregion

        #region Default Animation Curve

        /// <summary>
        /// Realistic awakening flutter curve:
        /// - 0.0 to 0.14: Pitch black (unconscious)
        /// - 0.14 to 0.32: Groggy peek (opens ~35%)
        /// - 0.32 to 0.44: Drowsy collapse (eyelids flutter shut to 4%)
        /// - 0.44 to 0.68: Second opening with adrenaline gasp (opens to 82%)
        /// - 0.68 to 0.76: Rapid micro-blink (adjusts to ambient light)
        /// - 0.76 to 1.00: Full wakefulness (100% open, retracts completely)
        /// </summary>
        private static AnimationCurve GetDefaultAwakeningCurve()
        {
            Keyframe[] keys = new Keyframe[]
            {
                new Keyframe(0.00f, 0.00f, 0.0f, 0.0f),
                new Keyframe(0.14f, 0.00f, 0.0f, 1.8f),
                new Keyframe(0.32f, 0.36f, 0.0f, -0.8f),
                new Keyframe(0.44f, 0.04f, -1.2f, 0.0f),
                new Keyframe(0.68f, 0.82f, 1.8f, 0.4f),
                new Keyframe(0.76f, 0.56f, -1.5f, 2.2f),
                new Keyframe(0.90f, 0.98f, 0.6f, 0.1f),
                new Keyframe(1.00f, 1.00f, 0.0f, 0.0f)
            };

            AnimationCurve curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.keys.Length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }
            return curve;
        }

        #endregion
    }
}
