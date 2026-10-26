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
    /// </summary>
    [RequireComponent(typeof(MissionInteractable))]
    [RequireComponent(typeof(SignalBeacon))]
    public class ShipReturnAirlock : MonoBehaviour
    {
        [Header("Airlock Identity")]
        [SerializeField] private string airlockName = "EVA AIRLOCK & EXTRACTION DOCK";

        [Header("Homing Beacon Settings")]
        [SerializeField] private float homingFrequencyMHz = 121.5f;
        [SerializeField] private float beaconRange = 250f;

        [Header("Audio Feedback")]
        [Tooltip("Optional custom airlock pressurization / door cycle sound. If null, procedural pneumatic hiss is generated.")]
        [SerializeField] private AudioClip customAirlockCycleClip;

        private MissionInteractable interactable;
        private SignalBeacon homingBeacon;
        private AudioSource airlockAudioSource;
        private AudioClip activeCycleClip;

        private void Awake()
        {
            interactable = GetComponent<MissionInteractable>();
            homingBeacon = GetComponent<SignalBeacon>();

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
        }

        private void ConfigureInteractableDefaults()
        {
            if (interactable != null)
            {
                interactable.IsSingleUse = false; // Airlock is used repeatedly across all days
                interactable.onInteracted.AddListener(HandleAirlockInteracted);
            }
        }

        private void OnEnable()
        {
            if (DayCycleManager.Instance != null)
            {
                DayCycleManager.Instance.OnMissionCompleted += HandleMissionCompleted;
                DayCycleManager.Instance.OnDayStarted += HandleDayStarted;
            }
        }

        private void OnDisable()
        {
            if (DayCycleManager.Instance != null)
            {
                DayCycleManager.Instance.OnMissionCompleted -= HandleMissionCompleted;
                DayCycleManager.Instance.OnDayStarted -= HandleDayStarted;
            }
        }

        private void Start()
        {
            ResetAirlock();
        }

        private void HandleDayStarted(int day, DayExpeditionData dayData)
        {
            ResetAirlock();
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

            bool isDone = DayCycleManager.Instance != null && DayCycleManager.Instance.IsDailyObjectiveCompleted;

            if (isDone)
            {
                interactable.ConfigureRuntimePrompts(
                    airlockName,
                    "CYCLE AIRLOCK & SLEEP",
                    "AIRLOCK PRESSURIZING // DOCKING COMPLETE"
                );
            }
            else
            {
                interactable.ConfigureRuntimePrompts(
                    airlockName,
                    "ABORT EXPEDITION & SLEEP",
                    "EXPEDITION ABORTED"
                );
            }
        }

        private void HandleAirlockInteracted(GameObject player)
        {
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
