using System;
using UnityEngine;
using SpaceHorror.Interaction;
using SpaceHorror.UI;

namespace SpaceHorror.Gameplay
{
    /// <summary>
    /// Interactive Ship Airlock & Cryo Sleep Hatch.
    /// Acts as the home extraction point for the astronaut.
    /// Emits a continuous homing beacon on 121.5 MHz ('AIRLOCK-HOME') so the player can always
    /// navigate back through zero-gravity space.
    /// Interacting cycles the airlock, concludes the daily expedition, and triggers the day transition.
    /// Sealed & locked if daily contract objectives are incomplete.
    /// </summary>
    [RequireComponent(typeof(MissionInteractable))]
    [RequireComponent(typeof(SignalBeacon))]
    public class ShipReturnAirlock : MonoBehaviour
    {
        [Header("Airlock Identity")]
        [SerializeField] private string airlockName = "EVA AIRLOCK & EXTRACTION DOCK";

        [Header("Contract Enforcement")]
        [Tooltip("If true, the return airlock is sealed and locked until all daily contract objectives are completed. If false, player can abort anytime.")]
        [SerializeField] private bool requireContractCompletion = true;

        [Tooltip("Feedback prompt shown on visor when aiming at airlock while contract is still incomplete.")]
        [SerializeField] private string lockedAirlockPrompt = "AIRLOCK LOCKED // CONTRACT INCOMPLETE";

        [Header("Homing Beacon Settings")]
        [SerializeField] private float homingFrequencyMHz = 121.5f;
        [SerializeField] private float beaconRange = 250f;

        [Header("Visual Feedback")]
        [Tooltip("Optional status indicator light (Amber/Red when sealed, Green/Cyan when unlocked & ready).")]
        [SerializeField] private Light statusLight;

        [Header("Audio Feedback")]
        [Tooltip("Optional custom airlock pressurization / door cycle sound. If null, procedural pneumatic hiss is generated.")]
        [SerializeField] private AudioClip customAirlockCycleClip;

        [Tooltip("Optional custom access denied buzzer sound. If null, procedural denied buzz is generated.")]
        [SerializeField] private AudioClip customAirlockLockedClip;

        private MissionInteractable interactable;
        private SignalBeacon homingBeacon;
        private AudioSource airlockAudioSource;
        private AudioClip activeCycleClip;
        private AudioClip activeLockedClip;

        public bool RequireContractCompletion
        {
            get => requireContractCompletion;
            set
            {
                requireContractCompletion = value;
                RefreshAirlockPrompts();
            }
        }

        public bool CanCycleAirlock => !requireContractCompletion || (DayCycleManager.Instance != null && DayCycleManager.Instance.IsDailyObjectiveCompleted);

        private void Awake()
        {
            interactable = GetComponent<MissionInteractable>();
            homingBeacon = GetComponent<SignalBeacon>();

            if (statusLight == null)
            {
                statusLight = GetComponentInChildren<Light>();
            }

            ConfigureHomingBeacon();
            SetupAirlockAudio();
            ConfigureInteractableDefaults();
        }

        private void ConfigureHomingBeacon()
        {
            if (homingBeacon != null)
            {
                if (homingFrequencyMHz > 0f)
                {
                    homingBeacon.Configure("AIRLOCK-HOME", "EVA DOCK / AIRLOCK", homingFrequencyMHz, beaconRange, 2);
                }
                else
                {
                    homingBeacon.Configure(SignalChannelPreset.AirlockHome, beaconRange);
                }
            }
        }

        private void SetupAirlockAudio()
        {
            airlockAudioSource = gameObject.AddComponent<AudioSource>();
            airlockAudioSource.playOnAwake = false;
            airlockAudioSource.spatialBlend = 1f; // 3D positional acoustic at airlock door
            airlockAudioSource.minDistance = 2f;
            airlockAudioSource.maxDistance = 18f;
            airlockAudioSource.rolloffMode = AudioRolloffMode.Linear;

            activeCycleClip = customAirlockCycleClip != null ? customAirlockCycleClip : GenerateProceduralAirlockHiss();
            activeLockedClip = customAirlockLockedClip != null ? customAirlockLockedClip : GenerateProceduralDeniedBuzz();
        }

        private void ConfigureInteractableDefaults()
        {
            if (interactable != null)
            {
                interactable.IsSingleUse = false; // Airlock is used repeatedly across all days
                interactable.onInteracted.RemoveListener(HandleAirlockInteracted);
                interactable.onInteracted.AddListener(HandleAirlockInteracted);
            }
        }

        private void OnEnable()
        {
            SubscribeToDayCycleManager();
        }

        private void OnDisable()
        {
            UnsubscribeFromDayCycleManager();
        }

        private void Start()
        {
            SubscribeToDayCycleManager();
            ResetAirlock();
        }

        private void SubscribeToDayCycleManager()
        {
            if (DayCycleManager.Instance != null)
            {
                DayCycleManager.Instance.OnDayStarted -= HandleDayStarted;
                DayCycleManager.Instance.OnDayStarted += HandleDayStarted;

                DayCycleManager.Instance.OnMissionAccepted -= HandleMissionAccepted;
                DayCycleManager.Instance.OnMissionAccepted += HandleMissionAccepted;

                DayCycleManager.Instance.OnMissionCompleted -= HandleMissionCompleted;
                DayCycleManager.Instance.OnMissionCompleted += HandleMissionCompleted;
            }
        }

        private void UnsubscribeFromDayCycleManager()
        {
            if (DayCycleManager.Instance != null)
            {
                DayCycleManager.Instance.OnDayStarted -= HandleDayStarted;
                DayCycleManager.Instance.OnMissionAccepted -= HandleMissionAccepted;
                DayCycleManager.Instance.OnMissionCompleted -= HandleMissionCompleted;
            }
        }

        private void HandleDayStarted(int day, DayExpeditionData dayData)
        {
            ResetAirlock();
        }

        private void HandleMissionAccepted(DayExpeditionData dayData)
        {
            RefreshAirlockPrompts();
        }

        private void HandleMissionCompleted(DayExpeditionData dayData)
        {
            RefreshAirlockPrompts();
        }

        /// <summary>
        /// Explicitly resets the airlock extraction dock for the new expedition day.
        /// </summary>
        public void ResetAirlock()
        {
            if (interactable == null) interactable = GetComponent<MissionInteractable>();
            if (interactable != null)
            {
                interactable.IsSingleUse = false;
                interactable.ResetObjective();
            }

            if (homingBeacon != null)
            {
                homingBeacon.SetTransmitting(true);
            }

            RefreshAirlockPrompts();
        }

        public void RefreshAirlockPrompts()
        {
            if (interactable == null) interactable = GetComponent<MissionInteractable>();
            if (interactable == null) return;

            interactable.IsSingleUse = false;
            interactable.ResetObjective();

            bool isContractDone = DayCycleManager.Instance != null && DayCycleManager.Instance.IsDailyObjectiveCompleted;

            if (!requireContractCompletion || isContractDone)
            {
                interactable.IsLocked = false;
                interactable.ConfigureRuntimePrompts(
                    airlockName,
                    "CYCLE AIRLOCK & SLEEP",
                    "AIRLOCK PRESSURIZING // DOCKING COMPLETE"
                );

                if (statusLight != null)
                {
                    statusLight.color = Color.white; // Pure Film Noir Stark White
                }
            }
            else
            {
                interactable.IsLocked = true;
                interactable.UnauthorizedPrompt = lockedAirlockPrompt;
                interactable.ConfigureRuntimePrompts(
                    airlockName,
                    "AIRLOCK SEALED // CONTRACT PENDING",
                    "AIRLOCK LOCKED"
                );

                if (statusLight != null)
                {
                    statusLight.color = new Color(0.25f, 0.25f, 0.25f); // Dim Ash-Silver (Locked)
                }
            }
        }

        private void HandleAirlockInteracted(GameObject player)
        {
            bool isContractDone = DayCycleManager.Instance != null && DayCycleManager.Instance.IsDailyObjectiveCompleted;

            if (requireContractCompletion && !isContractDone)
            {
                PlayLockedSound();

                var hud = FindFirstObjectByType<AstronautHelmetHUD>();
                if (hud != null)
                {
                    hud.ShowInteractionStatusMessage("AIRLOCK SEALED // COMPLETE CONTRACT OBJECTIVES BEFORE RETURN", 3.5f);
                }
                return;
            }

            PlayAirlockSound();

            if (DayCycleManager.Instance != null)
            {
                DayCycleManager.Instance.CompleteDayAndSleep();
            }
        }

        public void PlayAirlockSound()
        {
            if (airlockAudioSource != null && activeCycleClip != null)
            {
                airlockAudioSource.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
                airlockAudioSource.PlayOneShot(activeCycleClip, 1.0f);
            }
        }

        public void PlayLockedSound()
        {
            if (airlockAudioSource != null && activeLockedClip != null)
            {
                airlockAudioSource.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
                airlockAudioSource.PlayOneShot(activeLockedClip, 0.85f);
            }
        }

        private AudioClip GenerateProceduralDeniedBuzz()
        {
            int sampleRate = 44100;
            float duration = 0.28f;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                // Double pulse low buzz (130 Hz square wave)
                float pulse = (t < 0.11f || (t > 0.15f && t < 0.26f)) ? 1f : 0f;
                float wave = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 130f * t));
                float env = pulse * (1f - (t % 0.13f) / 0.13f);

                data[i] = wave * env * 0.45f;
            }

            AudioClip clip = AudioClip.Create("Procedural_AirlockDenied", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateProceduralAirlockHiss()
        {
            int sampleRate = 44100;
            float duration = 1.8f;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];

            System.Random rand = new System.Random(1994);
            float lpState = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;

                // Pneumatic pressure hiss envelope
                float env = Mathf.Sin((t / duration) * Mathf.PI);
                env = Mathf.Pow(env, 1.6f);

                float white = (float)(rand.NextDouble() * 2.0 - 1.0);
                lpState = Mathf.Lerp(lpState, white, 0.4f);

                // Heavy mechanical clamp thud at start (t = 0 to 0.1s)
                float thud = 0f;
                if (t < 0.12f)
                {
                    float thudEnv = Mathf.Exp(-35f * t);
                    thud = Mathf.Sin(2f * Mathf.PI * 65f * t) * thudEnv * 0.9f;
                }

                data[i] = Mathf.Clamp(lpState * env * 0.7f + thud, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural_AirlockHiss", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
