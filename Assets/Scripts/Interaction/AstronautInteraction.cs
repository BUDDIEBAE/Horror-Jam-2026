using System;
using UnityEngine;
using SpaceHorror.Player;
using SpaceHorror.UI;

namespace SpaceHorror.Interaction
{
    /// <summary>
    /// Player Interaction System for 6DoF Zero-G Astronaut.
    /// Raycasts forward from the player's helmet camera to detect MissionInteractable objects.
    /// Handles both instant [E] tap and smooth [E] hold progression with audio/visual feedback.
    /// Automatically suppresses Roll Right when aiming at an interactable to ensure smooth controls.
    /// </summary>
    [RequireComponent(typeof(AstronautInputHandler))]
    public class AstronautInteraction : MonoBehaviour
    {
        [Header("Raycast Settings")]
        [Tooltip("Maximum interaction reach distance in meters (ideal for EVA zero-g float reach)")]
        [SerializeField] private float reachDistance = 3.2f;

        [Tooltip("SphereCast radius for forgiving reticle targeting")]
        [SerializeField] private float sphereCastRadius = 0.25f;

        [Tooltip("Layer mask for interactable objects (default everything)")]
        [SerializeField] private LayerMask interactionLayers = ~0;

        [Header("Audio Feedback")]
        [Tooltip("Optional custom sound when interaction completes")]
        [SerializeField] private AudioClip customCompleteAudioClip;

        [Tooltip("Optional custom looping sound while holding [E]")]
        [SerializeField] private AudioClip customHoldLoopAudioClip;

        // References
        private Camera playerCamera;
        private AstronautInputHandler inputHandler;
        private AstronautHelmetHUD helmetHUD;
        private AudioSource interactionAudioSource;

        // Runtime Target Tracking
        private MissionInteractable currentTarget = null;
        private float holdTimer = 0f;
        private bool isHolding = false;

        // Procedural Audio Fallback
        private AudioClip proceduralCompleteClip;
        private AudioClip proceduralHoldClip;

        public MissionInteractable CurrentTarget => currentTarget;
        public float CurrentHoldProgress => currentTarget != null && currentTarget.HoldDuration > 0f ? Mathf.Clamp01(holdTimer / currentTarget.HoldDuration) : 0f;

        private void Awake()
        {
            inputHandler = GetComponent<AstronautInputHandler>();
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null) playerCamera = Camera.main;

            helmetHUD = GetComponentInChildren<AstronautHelmetHUD>();
            if (helmetHUD == null) helmetHUD = UnityEngine.Object.FindFirstObjectByType<AstronautHelmetHUD>();

            SetupAudioSource();
        }

        private void SetupAudioSource()
        {
            GameObject audioObj = new GameObject("Audio_Interaction");
            audioObj.transform.SetParent(transform, false);

            interactionAudioSource = audioObj.AddComponent<AudioSource>();
            interactionAudioSource.playOnAwake = false;
            interactionAudioSource.spatialBlend = 0f; // 2D in-helmet audio
            interactionAudioSource.volume = 0.75f;

            proceduralCompleteClip = GenerateProceduralChime();
        }

        private void Update()
        {
            DetectInteractable();
            HandleInteractionInput();
        }

        private void DetectInteractable()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
                if (playerCamera == null) return;
            }

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit hit;

            MissionInteractable detected = null;

            // SphereCast gives smooth, forgiving zero-g raycasting
            if (Physics.SphereCast(ray, sphereCastRadius, out hit, reachDistance, interactionLayers, QueryTriggerInteraction.Collide))
            {
                detected = hit.collider.GetComponentInParent<MissionInteractable>();
            }

            // Target changed
            if (detected != currentTarget)
            {
                if (currentTarget != null)
                {
                    currentTarget.SetHighlight(false);
                    currentTarget.ReportProgress(0f);
                }

                currentTarget = detected;
                holdTimer = 0f;
                isHolding = false;

                if (currentTarget != null && currentTarget.CanInteract)
                {
                    currentTarget.SetHighlight(true);
                }
            }

            // Tell InputHandler whether to suppress Roll Right while looking at an interactable
            bool hasValidTarget = currentTarget != null && currentTarget.CanInteract;
            if (inputHandler != null)
            {
                inputHandler.SuppressRollRight = hasValidTarget;
            }

            // Update Visor HUD prompt
            UpdateHUDDisplay(hasValidTarget);
        }

        private void UpdateHUDDisplay(bool hasValidTarget)
        {
            if (helmetHUD == null)
            {
                helmetHUD = UnityEngine.Object.FindFirstObjectByType<AstronautHelmetHUD>();
                if (helmetHUD == null) return;
            }

            if (hasValidTarget)
            {
                float progress = currentTarget.Method == InteractionMethod.HoldToComplete ? CurrentHoldProgress : 0f;
                string prompt = currentTarget.ActionPrompt;
                if (currentTarget.Method == InteractionMethod.HoldToComplete)
                {
                    prompt = $"{prompt} (HOLD)";
                }

                helmetHUD.ShowInteractionPrompt("LMB", prompt, progress, currentTarget.ObjectiveTitle);
            }
            else
            {
                helmetHUD.HideInteractionPrompt();
            }
        }

        private void HandleInteractionInput()
        {
            if (currentTarget == null || !currentTarget.CanInteract)
            {
                holdTimer = 0f;
                isHolding = false;
                return;
            }

            if (currentTarget.Method == InteractionMethod.InstantPress)
            {
                if (inputHandler != null && inputHandler.InteractPressed)
                {
                    ExecuteInteraction();
                }
            }
            else if (currentTarget.Method == InteractionMethod.HoldToComplete)
            {
                if (inputHandler != null && inputHandler.IsInteracting)
                {
                    isHolding = true;
                    holdTimer += Time.deltaTime;
                    float progress = Mathf.Clamp01(holdTimer / currentTarget.HoldDuration);
                    currentTarget.ReportProgress(progress);

                    if (holdTimer >= currentTarget.HoldDuration)
                    {
                        ExecuteInteraction();
                        holdTimer = 0f;
                        isHolding = false;
                    }
                }
                else
                {
                    if (isHolding)
                    {
                        isHolding = false;
                        holdTimer = 0f;
                        currentTarget.ReportProgress(0f);
                    }
                }
            }
        }

        private void ExecuteInteraction()
        {
            if (currentTarget == null) return;

            MissionInteractable target = currentTarget;
            target.TriggerInteraction(gameObject);

            // Play completion sound
            PlayCompletionAudio();

            // Display completion message on HUD
            if (helmetHUD != null && !string.IsNullOrEmpty(target.CompletionMessage))
            {
                helmetHUD.ShowInteractionStatusMessage(target.CompletionMessage);

                // If it's a data log, show the transmission transcript
                if (target.Type == ObjectiveType.CollectDataLog && !string.IsNullOrEmpty(target.DataLogTranscript))
                {
                    helmetHUD.ShowMissionTransmission(target.ObjectiveTitle, target.DataLogTranscript);
                }
            }
        }

        private void PlayCompletionAudio()
        {
            AudioClip clipToPlay = customCompleteAudioClip != null ? customCompleteAudioClip : proceduralCompleteClip;
            if (clipToPlay != null && interactionAudioSource != null)
            {
                interactionAudioSource.pitch = UnityEngine.Random.Range(0.98f, 1.05f);
                interactionAudioSource.PlayOneShot(clipToPlay);
            }
        }

        private AudioClip GenerateProceduralChime()
        {
            int sampleRate = 44100;
            float duration = 0.35f;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-12f * t);

                // Two bright pleasant sci-fi harmonics (1046 Hz [C6] & 1568 Hz [G6])
                float chime = Mathf.Sin(2f * Mathf.PI * 1046f * t) * 0.7f
                            + Mathf.Sin(2f * Mathf.PI * 1568f * t) * 0.5f;

                data[i] = Mathf.Clamp(chime * env * 0.7f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural_InteractionChime", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnDisable()
        {
            if (currentTarget != null)
            {
                currentTarget.SetHighlight(false);
                currentTarget = null;
            }

            if (inputHandler != null)
            {
                inputHandler.SuppressRollRight = false;
            }

            if (helmetHUD != null)
            {
                helmetHUD.HideInteractionPrompt();
            }
        }
    }
}
