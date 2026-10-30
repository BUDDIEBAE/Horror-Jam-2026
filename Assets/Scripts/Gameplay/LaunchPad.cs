using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using SpaceHorror.Player;
using SpaceHorror.Interaction;
using SpaceHorror.UI;
using SpaceHorror.Audio;

namespace SpaceHorror.Gameplay
{
    /// <summary>
    /// Interactive Launch Pad & Escape Vessel Controller - The Recursive Loop Ending (Horror Twist).
    /// Handles the final escape sequence of the expedition.
    /// 
    /// CINEMATIC HORROR SEQUENCE:
    /// 1. Player holds launch button to engage escape engines.
    /// 2. Thrusters roar to life, cabin shudders, and the ship leaps into hyperspace.
    /// 3. Suddenly, warp sound CUTS into violent radio glitch, static screech, and spatial collapse.
    /// 4. Complete blackout in the void.
    /// 5. Sharp, choking waking breath gasp (visceral helmet audio) as consciousness returns.
    /// 6. Station AI boot chime rings out: "GOOD MORNING, OPERATOR. DAY 01 CONTRACT INITIALIZED."
    /// 7. A CRT terminal screen reveals the horrifying truth: 
    ///    The void is populated by hundreds of derelict player ships and frozen corpses.
    ///    You have never escaped. The loop restarts now.
    /// </summary>
    [RequireComponent(typeof(MissionInteractable))]
    public class LaunchPad : MonoBehaviour
    {
        public static LaunchPad Instance { get; private set; }

        [Header("Launch Configuration")]
        [Tooltip("Action prompt displayed when looking at the launch pad")]
        [SerializeField] private string actionPrompt = "INITIATE LAUNCH SEQUENCE";

        [Tooltip("Objective title displayed on HUD")]
        [SerializeField] private string objectiveTitle = "ESCAPE VESSEL // LAUNCH PAD";

        [Tooltip("Time in seconds to hold the launch button before liftoff engages")]
        [Range(1f, 8f)]
        [SerializeField] private float holdDuration = 4.0f;

        [Tooltip("Duration of the thruster spool-up & camera rumble before the warp flash")]
        [Range(2f, 8f)]
        [SerializeField] private float launchCinematicDuration = 4.2f;

        [Header("Camera Shake & Rumble")]
        [SerializeField] private float cameraRumbleIntensity = 0.35f;

        [Header("Audio Feedback (Optional - Procedural if empty)")]
        [Tooltip("Custom rocket engine ignition clip. If null, procedural thruster rumble is generated.")]
        [SerializeField] private AudioClip customRocketIgnitionClip;

        [Tooltip("Custom warp acceleration clip. If null, procedural hyperspace sweep is generated.")]
        [SerializeField] private AudioClip customWarpClip;

        [Tooltip("Custom spatial collapse / glitch burst sound. If null, procedural glitch is generated.")]
        [SerializeField] private AudioClip customGlitchClip;

        [Tooltip("Custom computer reboot chime. If null, procedural synth chime is generated.")]
        [SerializeField] private AudioClip customRebootChimeClip;

        [Tooltip("Custom waking gasp breath clip. If null, procedural sharp suit intake is generated.")]
        [SerializeField] private AudioClip customWakingGaspClip;

        [Tooltip("Custom atmospheric horror drone. If null, procedural tritone dread chord is generated.")]
        [SerializeField] private AudioClip customEndingMusicClip;

        [Header("Loop Telemetry & Narrative")]
        [Tooltip("The recursive cycle counter displayed in terminal logs")]
        [SerializeField] private int loopIteration = 492;

        [Tooltip("Distress beacon broadcast frequency (MHz)")]
        [SerializeField] private float distressFrequencyMHz = 121.5f;

        [Header("Ending Screen Narrative")]
        [TextArea(5, 10)]
        [SerializeField] private string endingNarrativeText = 
            "The hyperdrive engaged with blinding fury — and collapsed inward.\n" +
            "Space-time folded back onto itself, slingshotting the vessel to Sector 1.\n\n" +
            "You awaken shivering in the cockpit as suit life support reboots.\n" +
            "Outside your visor, the radio monitor blinks with hundreds of distress beacons:\n" +
            "491 identical transponders broadcasting your exact signal (121.5 MHz).\n\n" +
            "Every derelict station in this abyss was repaired by your hands.\n" +
            "Every frozen corpse drifting in the void is you.\n\n" +
            "You have never escaped. The cycle begins anew.";

        // Components & State
        private MissionInteractable interactable;
        private AudioSource audioSource;
        private AudioClip activeIgnitionClip;
        private AudioClip activeWarpClip;
        private AudioClip activeGlitchClip;
        private AudioClip activeRebootChimeClip;
        private AudioClip activeWakingGaspClip;
        private AudioClip activeEndingClip;

        private bool isLaunching = false;
        private Canvas endingCanvas;
        private CanvasGroup endingCanvasGroup;

        // Auto-detect and bind any existing Launch interactables in scene
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBindSceneLaunchPad()
        {
            var interactables = UnityEngine.Object.FindObjectsByType<MissionInteractable>(FindObjectsSortMode.None);
            for (int i = 0; i < interactables.Length; i++)
            {
                var mi = interactables[i];
                if (mi == null) continue;

                string title = mi.ObjectiveTitle != null ? mi.ObjectiveTitle.ToUpperInvariant() : "";
                string prompt = mi.ActionPrompt != null ? mi.ActionPrompt.ToUpperInvariant() : "";
                string objName = mi.gameObject.name.ToUpperInvariant();

                bool isLaunchTarget = title.Contains("LAUNCH") || prompt.Contains("LAUNCH") || objName.Contains("LAUNCH");
                if (isLaunchTarget)
                {
                    var existingLaunchPad = mi.GetComponent<LaunchPad>();
                    if (existingLaunchPad == null)
                    {
                        mi.gameObject.AddComponent<LaunchPad>();
                        Debug.Log($"<color=#55FF55><b>[LaunchPad]</b></color> Automatically attached LaunchPad to '{mi.gameObject.name}'");
                    }
                }
            }
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;

            interactable = GetComponent<MissionInteractable>();
            SetupAudio();
            ConfigureInteractable();
            BuildProceduralEndingUI();
        }

        private void SetupAudio()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D immersion audio
            audioSource.volume = 1.0f;

            activeIgnitionClip = customRocketIgnitionClip != null ? customRocketIgnitionClip : GenerateProceduralRocketRoar();
            activeWarpClip = customWarpClip != null ? customWarpClip : GenerateProceduralWarpFlash();
            activeGlitchClip = customGlitchClip != null ? customGlitchClip : GenerateProceduralGlitchBurst();
            activeRebootChimeClip = customRebootChimeClip != null ? customRebootChimeClip : GenerateProceduralBootChime();
            activeWakingGaspClip = customWakingGaspClip != null ? customWakingGaspClip : GenerateProceduralWakingGasp();
            activeEndingClip = customEndingMusicClip != null ? customEndingMusicClip : GenerateProceduralEndingChord();
        }

        private void ConfigureInteractable()
        {
            if (interactable == null) interactable = GetComponent<MissionInteractable>();
            if (interactable != null)
            {
                interactable.ConfigureInteractionMethod(InteractionMethod.HoldToComplete, holdDuration, ObjectiveType.Custom);
                interactable.ConfigureRuntimePrompts(objectiveTitle, actionPrompt, "LAUNCHING ESCAPE VESSEL...");
                interactable.IsSingleUse = true;

                interactable.onInteracted.RemoveListener(HandleLaunchInteracted);
                interactable.onInteracted.AddListener(HandleLaunchInteracted);
            }
        }

        private void HandleLaunchInteracted(GameObject player)
        {
            TriggerLaunchSequence(player);
        }

        /// <summary>
        /// Public entry point to execute the launch sequence and end the game.
        /// </summary>
        public void TriggerLaunchSequence(GameObject player = null)
        {
            if (isLaunching) return;
            isLaunching = true;

            StartCoroutine(LaunchSequenceRoutine(player));
        }

        private IEnumerator LaunchSequenceRoutine(GameObject player)
        {
            // 1. Locate player systems if not passed in
            if (player == null)
            {
                var input = UnityEngine.Object.FindFirstObjectByType<AstronautInputHandler>();
                if (input != null) player = input.gameObject;
            }

            AstronautMovement6D movement = null;
            AstronautInputHandler inputHandler = null;
            AstronautSurvival survival = null;
            AstronautCameraRig cameraRig = null;
            AstronautHelmetHUD helmetHUD = null;
            AstronautAudioSystem audioSystem = null;
            Rigidbody rb = null;

            if (player != null)
            {
                movement = player.GetComponent<AstronautMovement6D>();
                inputHandler = player.GetComponent<AstronautInputHandler>();
                survival = player.GetComponent<AstronautSurvival>();
                cameraRig = player.GetComponentInChildren<AstronautCameraRig>();
                helmetHUD = player.GetComponentInChildren<AstronautHelmetHUD>();
                audioSystem = player.GetComponent<AstronautAudioSystem>();
                rb = player.GetComponent<Rigidbody>();
            }

            if (helmetHUD == null) helmetHUD = UnityEngine.Object.FindFirstObjectByType<AstronautHelmetHUD>();
            if (cameraRig == null) cameraRig = UnityEngine.Object.FindFirstObjectByType<AstronautCameraRig>();
            if (audioSystem == null) audioSystem = UnityEngine.Object.FindFirstObjectByType<AstronautAudioSystem>();

            // 2. Lock all player physics, movement, and disable oxygen drain during ending
            if (movement != null) movement.enabled = false;
            if (inputHandler != null) inputHandler.InputEnabled = false;
            if (survival != null) survival.enabled = false; // Freeze oxygen/suit degradation

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            // 3. Complete active Day 4 mission in DayCycleManager if active
            if (DayCycleManager.Instance != null && DayCycleManager.Instance.ActiveMissions != null)
            {
                for (int i = 0; i < DayCycleManager.Instance.ActiveMissions.Count; i++)
                {
                    var m = DayCycleManager.Instance.ActiveMissions[i];
                    if (m != null && !m.isCompleted)
                    {
                        DayCycleManager.Instance.CompleteMissionObjective(m, player);
                        break;
                    }
                }
            }

            // 4. Audio: Play rocket engine spool-up & ignition
            if (audioSource != null && activeIgnitionClip != null)
            {
                audioSource.clip = activeIgnitionClip;
                audioSource.loop = false;
                audioSource.volume = 1.0f;
                audioSource.Play();
            }

            // 5. Visor HUD status alerts
            if (helmetHUD != null)
            {
                helmetHUD.ShowInteractionStatusMessage("MAIN THRUSTERS ENGAGED // ESCAPE TRAJECTORY LOCKED", 3.5f);
            }

            // 6. Camera Rumble & Thruster Shake
            float elapsed = 0f;
            Vector3 originalCamOffset = cameraRig != null ? cameraRig.GetExternalEulerOffset() : Vector3.zero;

            while (elapsed < launchCinematicDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / launchCinematicDuration);

                if (progress > 0.55f && helmetHUD != null)
                {
                    helmetHUD.ShowInteractionStatusMessage("WARP DRIVE CHARGING // ATTEMPTING SECTOR BREAKOUT...", 2.0f);
                }

                // Intensity escalates as engines build to maximum thrust
                float currentIntensity = Mathf.Lerp(0.06f, cameraRumbleIntensity, progress);
                float shakePitch = (Mathf.PerlinNoise(Time.time * 26f, 0f) - 0.5f) * currentIntensity * 9f;
                float shakeYaw = (Mathf.PerlinNoise(0f, Time.time * 26f) - 0.5f) * currentIntensity * 9f;
                float shakeRoll = (Mathf.PerlinNoise(Time.time * 18f, Time.time * 18f) - 0.5f) * currentIntensity * 6f;

                if (cameraRig != null)
                {
                    cameraRig.SetExternalEulerOffset(originalCamOffset + new Vector3(shakePitch, shakeYaw, shakeRoll));
                }

                yield return null;
            }

            // 7. Warp Flash & Hyperspace Leap
            if (audioSource != null && activeWarpClip != null)
            {
                audioSource.PlayOneShot(activeWarpClip, 1.0f);
            }

            if (helmetHUD != null)
            {
                helmetHUD.TriggerNoirFlash(1.2f);
            }

            yield return new WaitForSeconds(1.1f);

            // 8. THE HORROR TWIST: Sudden Audio Cut, Digital Glitch, & Reality Collapse
            if (audioSource != null)
            {
                audioSource.Stop();
            }

            // Sharp digital glitch burst & radio screech
            if (audioSource != null && activeGlitchClip != null)
            {
                audioSource.PlayOneShot(activeGlitchClip, 1.0f);
            }

            if (helmetHUD != null)
            {
                helmetHUD.TriggerVisorGlitch(0.85f);
                helmetHUD.ShowInteractionStatusMessage("<b>[ ! ] CRITICAL SPATIAL CURVATURE LOOP // DISPLACEMENT: 0.000m</b>", 4.0f);
            }

            // Violent slump forward (head drops chin down into chest)
            if (cameraRig != null)
            {
                cameraRig.SetExternalEulerOffset(new Vector3(-14f, 0f, 3.5f));
            }

            // 9. Hold in complete cosmic silence & darkness
            yield return new WaitForSeconds(1.8f);

            // 10. The False Awakening: Visceral gasp for air + heartbeat shock
            if (audioSystem != null)
            {
                audioSystem.TriggerWakeUpGasp();
            }
            else if (audioSource != null && activeWakingGaspClip != null)
            {
                audioSource.PlayOneShot(activeWakingGaspClip, 1.0f);
            }

            // Head slowly recovers equilibrium from slump
            float liftElapsed = 0f;
            float liftDuration = 1.4f;
            while (liftElapsed < liftDuration)
            {
                liftElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(liftElapsed / liftDuration);
                if (cameraRig != null)
                {
                    cameraRig.SetExternalEulerOffset(Vector3.Lerp(new Vector3(-14f, 0f, 3.5f), Vector3.zero, Mathf.SmoothStep(0f, 1f, t)));
                }
                yield return null;
            }

            yield return new WaitForSeconds(0.4f);

            // 11. Computer Reboot Chime & Cold Station AI Notification
            if (audioSource != null && activeRebootChimeClip != null)
            {
                audioSource.PlayOneShot(activeRebootChimeClip, 0.95f);
            }

            if (helmetHUD != null)
            {
                helmetHUD.ShowInteractionStatusMessage($">> STATION OS REBOOT // GOOD MORNING, OPERATOR #{loopIteration}", 3.5f);
            }

            yield return new WaitForSeconds(1.8f);

            // 12. Fade in Fullscreen Ending Screen & Revelation
            yield return StartCoroutine(FadeInEndingScreen());

            // 13. Play atmospheric looping dread chord
            if (audioSource != null && activeEndingClip != null)
            {
                audioSource.clip = activeEndingClip;
                audioSource.loop = true;
                audioSource.volume = 0.85f;
                audioSource.Play();
            }

            // 14. Unlock and reveal mouse cursor for UI buttons
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private IEnumerator FadeInEndingScreen()
        {
            if (endingCanvasGroup == null) yield break;

            endingCanvas.gameObject.SetActive(true);
            endingCanvasGroup.alpha = 0f;

            float fadeDuration = 2.4f;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                endingCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }

            endingCanvasGroup.alpha = 1f;
        }

        private void Update()
        {
            // Keyboard shortcuts when the ending screen is shown (New Input System compliant)
            if (isLaunching && endingCanvasGroup != null && endingCanvasGroup.alpha > 0.8f)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.escapeKey.wasPressedThisFrame)
                    {
                        QuitGame();
                    }
                    else if (keyboard.rKey.wasPressedThisFrame)
                    {
                        RestartGame();
                    }
                }
            }
        }

        /// <summary>
        /// Quits the standalone build or stops Play mode in the Unity Editor.
        /// </summary>
        public void QuitGame()
        {
            Debug.Log("<b>[LaunchPad]</b> Simulation Aborted. Quitting Game.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Reloads the scene to allow the player to enter the recursive nightmare loop again.
        /// </summary>
        public void RestartGame()
        {
            Debug.Log($"<b>[LaunchPad]</b> Re-entering loop cycle #{loopIteration + 1}.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        #region Procedural Ending UI Construction

        private void BuildProceduralEndingUI()
        {
            GameObject canvasObj = new GameObject("Ending_Screen_Canvas");
            canvasObj.transform.SetParent(transform, false);

            endingCanvas = canvasObj.AddComponent<Canvas>();
            endingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            endingCanvas.sortingOrder = 999; // Render on top of everything

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            endingCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
            endingCanvasGroup.alpha = 0f;

            // 1. Fullscreen Obsidian Film Noir Backdrop
            GameObject bgObj = new GameObject("Obsidian_Backdrop");
            bgObj.transform.SetParent(canvasObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.0f, 0.0f, 0.0f, 1.0f); // Pure Pitch Black Noir

            // 2. Center Card Frame (CRT Terminal Style - Pure Film Noir Monochrome)
            GameObject cardObj = new GameObject("Terminal_Card");
            cardObj.transform.SetParent(canvasObj.transform, false);
            RectTransform cardRect = cardObj.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(860f, 550f);

            // Outer Border (Stark High-Contrast White Rim)
            Image cardBorder = cardObj.AddComponent<Image>();
            cardBorder.color = new Color(0.95f, 0.95f, 0.95f, 0.95f);

            // Inner Plate (2px inset)
            GameObject innerObj = new GameObject("Inner_Plate");
            innerObj.transform.SetParent(cardObj.transform, false);
            RectTransform innerRect = innerObj.AddComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(2f, 2f);
            innerRect.offsetMax = new Vector2(-2f, -2f);

            Image innerPlate = innerObj.AddComponent<Image>();
            innerPlate.color = new Color(0.04f, 0.04f, 0.04f, 1.0f);

            // 3. Top Alert Header (RECURSIVE HORIZON COLLAPSE)
            GameObject titleObj = new GameObject("Title_Text");
            titleObj.transform.SetParent(innerObj.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -28f);
            titleRect.sizeDelta = new Vector2(0f, 44f);

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "<b>[ ! ] RECURSIVE HORIZON COLLAPSE</b>";
            titleText.fontSize = 26f;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);

            // 4. Subtitle / Cycle Alert
            GameObject subObj = new GameObject("Subtitle_Text");
            subObj.transform.SetParent(innerObj.transform, false);
            RectTransform subRect = subObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0f, 1f);
            subRect.anchorMax = new Vector2(1f, 1f);
            subRect.pivot = new Vector2(0.5f, 1f);
            subRect.anchoredPosition = new Vector2(0f, -72f);
            subRect.sizeDelta = new Vector2(0f, 26f);

            TextMeshProUGUI subText = subObj.AddComponent<TextMeshProUGUI>();
            subText.text = $"<b>ITERATION #{loopIteration} // ATTEMPT TO ESCAPE: DENIED</b>";
            subText.fontSize = 14f;
            subText.alignment = TextAlignmentOptions.Center;
            subText.color = new Color(0.80f, 0.80f, 0.80f, 1.0f);

            // 5. Telemetry Stats Strip Box
            GameObject statsBoxObj = new GameObject("Stats_Strip");
            statsBoxObj.transform.SetParent(innerObj.transform, false);
            RectTransform statsRect = statsBoxObj.AddComponent<RectTransform>();
            statsRect.anchorMin = new Vector2(0.08f, 1f);
            statsRect.anchorMax = new Vector2(0.92f, 1f);
            statsRect.pivot = new Vector2(0.5f, 1f);
            statsRect.anchoredPosition = new Vector2(0f, -106f);
            statsRect.sizeDelta = new Vector2(0f, 32f);

            Image statsBg = statsBoxObj.AddComponent<Image>();
            statsBg.color = new Color(0.12f, 0.12f, 0.12f, 0.90f);

            GameObject statsTextObj = new GameObject("Stats_Text");
            statsTextObj.transform.SetParent(statsBoxObj.transform, false);
            RectTransform statsTextRect = statsTextObj.AddComponent<RectTransform>();
            statsTextRect.anchorMin = Vector2.zero;
            statsTextRect.anchorMax = Vector2.one;
            statsTextRect.offsetMin = new Vector2(8f, 0f);
            statsTextRect.offsetMax = new Vector2(-8f, 0f);

            TextMeshProUGUI statsText = statsTextObj.AddComponent<TextMeshProUGUI>();
            statsText.text = $"BEACONS DETECTED: {loopIteration - 1} ({distressFrequencyMHz:F1} MHz)   |   DISPLACEMENT: 0.000 km   |   STATUS: TRAPPED";
            statsText.fontSize = 12f;
            statsText.alignment = TextAlignmentOptions.Center;
            statsText.color = new Color(0.95f, 0.95f, 0.95f, 1.0f);

            // 6. Narrative Lore Text
            GameObject descObj = new GameObject("Narrative_Text");
            descObj.transform.SetParent(innerObj.transform, false);
            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.08f, 0.32f);
            descRect.anchorMax = new Vector2(0.92f, 0.74f);
            descRect.offsetMin = Vector2.zero;
            descRect.offsetMax = Vector2.zero;

            TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = endingNarrativeText;
            descText.fontSize = 13.5f;
            descText.alignment = TextAlignmentOptions.Center;
            descText.lineSpacing = 16f;
            descText.color = new Color(0.85f, 0.85f, 0.85f, 1.0f);

            // 7. System Boot Footer
            GameObject promptObj = new GameObject("Boot_Prompt_Text");
            promptObj.transform.SetParent(innerObj.transform, false);
            RectTransform promptRect = promptObj.AddComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0f, 0.20f);
            promptRect.anchorMax = new Vector2(1f, 0.29f);
            promptRect.offsetMin = Vector2.zero;
            promptRect.offsetMax = Vector2.zero;

            TextMeshProUGUI promptText = promptObj.AddComponent<TextMeshProUGUI>();
            promptText.text = $">> SYSTEM REBOOT CONFIRMED. OPERATOR #{loopIteration} AWAKENED.\n" +
                              ">> PLEASE SIGN YOUR DAILY CONTRACT AT THE TERMINAL.";
            promptText.fontSize = 12f;
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.color = new Color(0.70f, 0.70f, 0.70f, 1.0f);

            // 8. Horror Jam Footer
            GameObject jamObj = new GameObject("Jam_Text");
            jamObj.transform.SetParent(innerObj.transform, false);
            RectTransform jamRect = jamObj.AddComponent<RectTransform>();
            jamRect.anchorMin = new Vector2(0f, 0.12f);
            jamRect.anchorMax = new Vector2(1f, 0.18f);
            jamRect.offsetMin = Vector2.zero;
            jamRect.offsetMax = Vector2.zero;

            TextMeshProUGUI jamText = jamObj.AddComponent<TextMeshProUGUI>();
            jamText.text = "HORROR JAM 2026 // THE RECURSIVE NIGHTMARE";
            jamText.fontSize = 11f;
            jamText.alignment = TextAlignmentOptions.Center;
            jamText.color = new Color(0.40f, 0.40f, 0.40f, 1.0f);

            // 9. Interactive Action Buttons
            CreateButton(innerObj.transform, new Vector2(-135f, -485f), $"BEGIN CYCLE #{loopIteration + 1} [R]", RestartGame, true);
            CreateButton(innerObj.transform, new Vector2(135f, -485f), "ABORT PROTOCOL [ESC]", QuitGame, false);

            canvasObj.SetActive(false);
        }

        private void CreateButton(Transform parent, Vector2 pos, string label, UnityEngine.Events.UnityAction action, bool isPrimary)
        {
            GameObject btnObj = new GameObject($"Btn_{label}");
            btnObj.transform.SetParent(parent, false);

            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 1f);
            btnRect.anchorMax = new Vector2(0.5f, 1f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = pos;
            btnRect.sizeDelta = new Vector2(230f, 44f);

            // Button Outer Border (Stark White for primary, Ash Silver for secondary)
            Image btnBorder = btnObj.AddComponent<Image>();
            btnBorder.color = isPrimary 
                ? new Color(1.0f, 1.0f, 1.0f, 1.0f) 
                : new Color(0.50f, 0.50f, 0.50f, 0.85f);

            // Button Inner Plate
            GameObject innerBtn = new GameObject("Inner");
            innerBtn.transform.SetParent(btnObj.transform, false);
            RectTransform innerRect = innerBtn.AddComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(2f, 2f);
            innerRect.offsetMax = new Vector2(-2f, -2f);

            Image innerImg = innerBtn.AddComponent<Image>();
            innerImg.color = isPrimary 
                ? new Color(0.12f, 0.12f, 0.12f, 1.0f) 
                : new Color(0.04f, 0.04f, 0.04f, 1.0f);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = innerImg;

            var colors = btn.colors;
            colors.normalColor = innerImg.color;
            colors.highlightedColor = isPrimary ? new Color(0.35f, 0.35f, 0.35f, 1.0f) : new Color(0.20f, 0.20f, 0.20f, 1.0f);
            colors.pressedColor = isPrimary ? new Color(0.80f, 0.80f, 0.80f, 1.0f) : new Color(0.50f, 0.50f, 0.50f, 1.0f); // Contrast Inversion
            btn.colors = colors;
            btn.onClick.AddListener(action);

            // Button Label
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(innerBtn.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI btnText = textObj.AddComponent<TextMeshProUGUI>();
            btnText.text = $"<b>{label}</b>";
            btnText.fontSize = 12.5f;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = isPrimary 
                ? new Color(1.0f, 1.0f, 1.0f, 1.0f) 
                : new Color(0.80f, 0.80f, 0.80f, 1.0f);
        }

        #endregion

        #region Procedural Audio Synthesizers

        private AudioClip GenerateProceduralRocketRoar()
        {
            int sampleRate = 44100;
            float duration = launchCinematicDuration + 0.5f;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];

            System.Random rand = new System.Random(2026);
            float lowPass = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = Mathf.Clamp01(t / launchCinematicDuration);

                // Deep sub-bass frequency rising as thrusters build up (38 Hz to 75 Hz)
                float subFreq = Mathf.Lerp(38f, 75f, progress);
                float subWave = Mathf.Sin(2f * Mathf.PI * subFreq * t);

                // Jet rumble noise
                float white = (float)(rand.NextDouble() * 2.0 - 1.0);
                lowPass = Mathf.Lerp(lowPass, white, 0.12f);

                // Spool up envelope (soft start, heavy climax)
                float env = Mathf.Pow(progress, 1.4f) * 0.85f;

                data[i] = Mathf.Clamp((subWave * 0.6f + lowPass * 0.7f) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural_RocketIgnition", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateProceduralWarpFlash()
        {
            int sampleRate = 44100;
            float duration = 2.4f;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                // Exponential frequency sweep upward (120 Hz to 2800 Hz)
                float freq = 120f * Mathf.Exp(1.4f * t);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                float env = Mathf.Exp(-1.5f * t); // Decays smoothly

                data[i] = wave * env * 0.75f;
            }

            AudioClip clip = AudioClip.Create("Procedural_WarpFlash", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateProceduralGlitchBurst()
        {
            int sampleRate = 44100;
            float duration = 1.0f;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];

            System.Random rand = new System.Random(492);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                // Sub-bass frequency drop (240 Hz diving down to 28 Hz)
                float freq = Mathf.Lerp(240f, 28f, t * t);
                float subWave = Mathf.Sin(2f * Mathf.PI * freq * t);

                // Harsh radio screech & digital bitcrush clicks
                float noise = (float)(rand.NextDouble() * 2.0 - 1.0);
                float click = (i % 64 == 0) ? (float)(rand.NextDouble() * 1.5 - 0.75) : 0f;

                float env = Mathf.Exp(-3.2f * t);
                data[i] = Mathf.Clamp((subWave * 0.5f + noise * 0.35f + click) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural_GlitchBurst", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateProceduralBootChime()
        {
            int sampleRate = 44100;
            float duration = 1.1f;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];

            // Retro computer dual-tone boot chime (Tone 1: 784 Hz G5, Tone 2: 1046.5 Hz C6)
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float tone1 = Mathf.Sin(2f * Mathf.PI * 784f * t) * Mathf.Clamp01(1f - (t / 0.3f));
                float tone2 = 0f;
                if (t > 0.22f)
                {
                    float t2 = t - 0.22f;
                    tone2 = Mathf.Sin(2f * Mathf.PI * 1046.5f * t) * Mathf.Exp(-3.5f * t2);
                }

                data[i] = (tone1 * 0.45f + tone2 * 0.55f);
            }

            AudioClip clip = AudioClip.Create("Procedural_BootChime", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateProceduralWakingGasp()
        {
            int sampleRate = 44100;
            float duration = 1.2f;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];

            System.Random rand = new System.Random(101);
            float lowPass = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                // Inward rushing breath envelope (swells quickly then drops)
                float breathEnv = Mathf.Sin(Mathf.Clamp01(t / 0.85f) * Mathf.PI);

                float white = (float)(rand.NextDouble() * 2.0 - 1.0);
                lowPass = Mathf.Lerp(lowPass, white, 0.25f);

                // Heart shock thump at start (t = 0.15s)
                float heartThump = 0f;
                if (t >= 0.15f && t <= 0.35f)
                {
                    float ht = (t - 0.15f) / 0.20f;
                    heartThump = Mathf.Sin(ht * Mathf.PI) * Mathf.Sin(2f * Mathf.PI * 52f * t);
                }

                data[i] = Mathf.Clamp((lowPass * breathEnv * 0.65f) + (heartThump * 0.75f), -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural_WakingGasp", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateProceduralEndingChord()
        {
            int sampleRate = 44100;
            float duration = 8.0f; // 8-second seamless looping dread drone
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];

            // Haunting horror chord with tritone dissonance & heartbeat pulse
            // F#1 (46.2 Hz), C2 (65.4 Hz - Tritone!), A2 (110.0 Hz - Minor 3rd), C#3 (138.6 Hz)
            float f1 = 46.25f;
            float f2 = 65.41f;
            float f3 = 110.00f;
            float f4 = 138.59f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;

                // Slow subtle vibrato / pitch drift
                float lfo = Mathf.Sin(2f * Mathf.PI * 0.25f * t) * 0.8f;

                float chord = (Mathf.Sin(2f * Mathf.PI * (f1 + lfo) * t) * 0.38f) +
                              (Mathf.Sin(2f * Mathf.PI * (f2 - lfo) * t) * 0.26f) +
                              (Mathf.Sin(2f * Mathf.PI * f3 * t) * 0.18f) +
                              (Mathf.Sin(2f * Mathf.PI * f4 * t) * 0.12f);

                // Subtle rhythmic 40 BPM sub-bass heartbeat pulse (interval = 1.5s)
                float pulseTime = t % 1.5f;
                float heartPulse = 0f;
                if (pulseTime < 0.22f)
                {
                    heartPulse = Mathf.Sin((pulseTime / 0.22f) * Mathf.PI) * Mathf.Sin(2f * Mathf.PI * 48f * t) * 0.28f;
                }

                // Smooth looping window so audio loops seamlessly without click
                float loopWindow = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);

                data[i] = Mathf.Clamp((chord + heartPulse) * (0.7f + 0.3f * loopWindow), -1f, 1f) * 0.75f;
            }

            AudioClip clip = AudioClip.Create("Procedural_LoopDreadChord", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        #endregion
    }
}
