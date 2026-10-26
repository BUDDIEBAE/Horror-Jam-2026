using System;
using UnityEngine;
using SpaceHorror.Interaction;
using SpaceHorror.UI;

namespace SpaceHorror.Gameplay
{
    /// <summary>
    /// Interactive Ship Mission Terminal.
    /// Located inside the player's ship. Allows the astronaut to access mission briefings,
    /// review operation telemetry, and accept daily contracts to begin the EVA expedition.
    /// </summary>
    [RequireComponent(typeof(MissionInteractable))]
    public class ShipMissionTerminal : MonoBehaviour
    {
        [Header("Terminal Identity")]
        [SerializeField] private string terminalName = "SHIP EXPEDITION TERMINAL";

        [Header("Audio Feedback")]
        [Tooltip("Optional custom terminal keystroke / data chirp sound. If null, procedural retro chirp is generated.")]
        [SerializeField] private AudioClip customTerminalChirp;

        private MissionInteractable interactable;
        private AudioSource terminalAudioSource;
        private AudioClip activeChirpClip;

        private void Awake()
        {
            interactable = GetComponent<MissionInteractable>();
            SetupTerminalAudio();
            ConfigureInteractableDefaults();
        }

        private void SetupTerminalAudio()
        {
            terminalAudioSource = gameObject.AddComponent<AudioSource>();
            terminalAudioSource.playOnAwake = false;
            terminalAudioSource.spatialBlend = 1f; // 3D positional sound in ship interior
            terminalAudioSource.minDistance = 1f;
            terminalAudioSource.maxDistance = 8f;
            terminalAudioSource.rolloffMode = AudioRolloffMode.Linear;

            activeChirpClip = customTerminalChirp != null ? customTerminalChirp : GenerateProceduralTerminalChirp();
        }

        private void ConfigureInteractableDefaults()
        {
            if (interactable != null)
            {
                interactable.IsSingleUse = false; // Terminal can be interacted with repeatedly across all days
                interactable.onInteracted.AddListener(HandleTerminalInteracted);
            }
        }

        private void OnEnable()
        {
            if (DayCycleManager.Instance != null)
            {
                DayCycleManager.Instance.OnDayStarted += HandleDayStarted;
                DayCycleManager.Instance.OnMissionAccepted += HandleMissionAccepted;
            }
        }

        private void OnDisable()
        {
            if (DayCycleManager.Instance != null)
            {
                DayCycleManager.Instance.OnDayStarted -= HandleDayStarted;
                DayCycleManager.Instance.OnMissionAccepted -= HandleMissionAccepted;
            }
        }

        private void Start()
        {
            ResetTerminal();
        }

        private void HandleDayStarted(int dayNumber, DayExpeditionData dayData)
        {
            ResetTerminal();
        }

        private void HandleMissionAccepted(DayExpeditionData dayData)
        {
            PlayTerminalSound();
        }

        /// <summary>
        /// Explicitly resets the terminal state and re-enables interaction for a new day.
        /// </summary>
        public void ResetTerminal()
        {
            if (interactable == null) interactable = GetComponent<MissionInteractable>();
            if (interactable != null)
            {
                interactable.IsSingleUse = false;
                interactable.ResetObjective();
            }
            RefreshTerminalState();
        }

        public void RefreshTerminalState()
        {
            if (interactable == null) interactable = GetComponent<MissionInteractable>();
            if (interactable == null) return;

            interactable.IsSingleUse = false;

            bool isContractActive = DayCycleManager.Instance != null && DayCycleManager.Instance.IsMissionActive;

            if (isContractActive)
            {
                interactable.ConfigureRuntimePrompts(
                    terminalName,
                    "REVIEW ACTIVE CONTRACT",
                    "MISSION ACTIVE // PROCEED TO AIRLOCK"
                );
            }
            else
            {
                interactable.ResetObjective();
                interactable.ConfigureRuntimePrompts(
                    terminalName,
                    "ACCEPT DAILY CONTRACT",
                    "CONTRACT SIGNED // BEACON BROADCASTING"
                );
            }
        }

        private void HandleTerminalInteracted(GameObject player)
        {
            PlayTerminalSound();

            if (DayCycleManager.Instance != null)
            {
                if (DayCycleManager.Instance.IsMissionActive)
                {
                    // Already accepted today: replay telemetry transcript
                    var activeDay = DayCycleManager.Instance.ActiveDay;
                    var hud = FindFirstObjectByType<AstronautHelmetHUD>();
                    if (hud != null && activeDay != null && !string.IsNullOrEmpty(activeDay.briefingTranscript))
                    {
                        hud.ShowMissionTransmission(activeDay.dayTitle, activeDay.briefingTranscript, 5.0f);
                    }
                    return;
                }

                DayCycleManager.Instance.AcceptCurrentDailyMission();
            }

            RefreshTerminalState();
        }

        private void PlayTerminalSound()
        {
            if (terminalAudioSource != null && activeChirpClip != null)
            {
                terminalAudioSource.pitch = UnityEngine.Random.Range(0.98f, 1.05f);
                terminalAudioSource.PlayOneShot(activeChirpClip, 0.8f);
            }
        }

        private AudioClip GenerateProceduralTerminalChirp()
        {
            int sampleRate = 44100;
            float duration = 0.22f;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];

            // Retro sci-fi 3-tone data burst
            float f1 = 1200f;
            float f2 = 1800f;
            float f3 = 2400f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = t < 0.07f ? f1 : (t < 0.14f ? f2 : f3);
                float env = Mathf.Sin((t / duration) * Mathf.PI);

                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.65f;
            }

            AudioClip clip = AudioClip.Create("Procedural_TerminalChirp", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
