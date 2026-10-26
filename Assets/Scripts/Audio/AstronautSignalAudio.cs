using System;
using UnityEngine;
using SpaceHorror.Player;

namespace SpaceHorror.Audio
{
    /// <summary>
    /// In-Helmet Radio Signal Audio Synthesizer for 6DoF Astronaut.
    /// Simulates retro VHF radio reception, geiger/sonar telemetry pings, and static interference.
    /// Strictly adheres to Film Noir atmosphere: solitary mono acoustics, procedural fallback, zero external dependencies.
    /// </summary>
    [RequireComponent(typeof(AstronautSignalTracker))]
    public class AstronautSignalAudio : MonoBehaviour
    {
        [Header("Custom Audio Clips (Optional - Procedural Fallback if empty)")]
        [Tooltip("Custom sonar ping sound. If empty, a procedural retro 8-bit sine blip is generated.")]
        [SerializeField] private AudioClip customSonarPingClip;

        [Tooltip("Custom radio static/hiss loop. If empty, procedural analog white noise is generated.")]
        [SerializeField] private AudioClip customRadioStaticClip;

        [Tooltip("Custom lock-on confirmation chime. If empty, procedural dual-frequency tone is generated.")]
        [SerializeField] private AudioClip customLockOnClip;

        [Header("Volume & Mix Controls")]
        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 0.85f;

        [Range(0f, 1f)]
        [SerializeField] private float sonarPingVolume = 0.75f;

        [Range(0f, 1f)]
        [SerializeField] private float staticHissVolume = 0.35f;

        [Range(0f, 1f)]
        [SerializeField] private float lockOnVolume = 0.85f;

        [Header("Ping Frequency Cadence")]
        [Tooltip("Ping interval in seconds when signal is at minimum threshold (faint distant beacon).")]
        [SerializeField] private float maxPingInterval = 2.0f;

        [Tooltip("Ping interval in seconds when signal is at maximum (dead-on close proximity).")]
        [SerializeField] private float minPingInterval = 0.14f;

        // Dedicated in-helmet 2D audio channels
        private AudioSource sonarSource;
        private AudioSource staticSource;
        private AudioSource lockOnSource;

        // Working clips
        private AudioClip activePingClip;
        private AudioClip activeStaticClip;
        private AudioClip activeLockOnClip;
        private AudioClip activeTuningClickClip;

        // References & State
        private AstronautSignalTracker tracker;
        private float pingTimer = 0f;
        private const int SAMPLE_RATE = 44100;

        private void Awake()
        {
            tracker = GetComponent<AstronautSignalTracker>();
            SetupAudioChannels();
            InitializeClips();
        }

        private void OnEnable()
        {
            if (tracker != null)
            {
                tracker.OnLockOnChanged += HandleLockOnChanged;
                tracker.OnChannelTuned += HandleChannelTuned;
            }
        }

        private void OnDisable()
        {
            if (tracker != null)
            {
                tracker.OnLockOnChanged -= HandleLockOnChanged;
                tracker.OnChannelTuned -= HandleChannelTuned;
            }

            if (staticSource != null && staticSource.isPlaying)
            {
                staticSource.Stop();
            }
        }

        private void Update()
        {
            if (tracker == null) return;

            UpdateSonarPing();
            UpdateRadioStatic();
        }

        private void SetupAudioChannels()
        {
            sonarSource = CreateHelmetAudioSource("Audio_Signal_Sonar");
            staticSource = CreateHelmetAudioSource("Audio_Signal_Static");
            lockOnSource = CreateHelmetAudioSource("Audio_Signal_LockOn");

            staticSource.loop = true;
        }

        private AudioSource CreateHelmetAudioSource(string name)
        {
            GameObject channelObj = new GameObject(name);
            channelObj.transform.SetParent(transform, false);

            AudioSource source = channelObj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 100% 2D in-helmet acoustic
            source.priority = 80;
            source.reverbZoneMix = 0.1f;
            return source;
        }

        private void InitializeClips()
        {
            activePingClip = customSonarPingClip != null ? customSonarPingClip : GenerateProceduralSonarPing();
            activeStaticClip = customRadioStaticClip != null ? customRadioStaticClip : GenerateProceduralRadioStatic();
            activeLockOnClip = customLockOnClip != null ? customLockOnClip : GenerateProceduralLockOnChime();
            activeTuningClickClip = GenerateProceduralTuningClick();

            if (staticSource != null && activeStaticClip != null)
            {
                staticSource.clip = activeStaticClip;
                staticSource.volume = 0f;
                staticSource.Play();
            }
        }

        private void UpdateSonarPing()
        {
            float strength = tracker.SmoothedStrength;

            // Only ping if there is an active signal
            if (!tracker.HasActiveSignal || strength < 0.02f)
            {
                pingTimer = 0f;
                return;
            }

            // Exponential cadence: tempo accelerates dramatically as alignment tightens
            float interval = Mathf.Lerp(maxPingInterval, minPingInterval, Mathf.Pow(strength, 1.4f));
            pingTimer += Time.deltaTime;

            if (pingTimer >= interval)
            {
                pingTimer = 0f;

                // Pitch shifts up subtly as alignment tightens (880 Hz to 1250 Hz range)
                float pitch = Mathf.Lerp(0.9f, 1.35f, strength);
                float volume = sonarPingVolume * masterVolume * Mathf.Lerp(0.4f, 1.1f, strength);

                sonarSource.pitch = pitch;
                sonarSource.PlayOneShot(activePingClip, volume);
            }
        }

        private void UpdateRadioStatic()
        {
            if (staticSource == null) return;

            float strength = tracker.SmoothedStrength;
            bool hasSignal = tracker.HasActiveSignal;

            // Target volume modulates based on presence of a radio broadcast in the void
            float targetVol = 0f;
            if (hasSignal)
            {
                // Subtle background static floor that peaks at mid-range and clarifies when locked on
                float clarityFactor = tracker.IsLockedOn ? 0.35f : 1.0f;
                targetVol = staticHissVolume * masterVolume * Mathf.Clamp01(strength * 0.7f + 0.1f) * clarityFactor;
            }

            staticSource.volume = Mathf.MoveTowards(staticSource.volume, targetVol, Time.deltaTime * 3.5f);
        }

        private void HandleLockOnChanged(bool isLockedOn)
        {
            if (isLockedOn && lockOnSource != null && activeLockOnClip != null)
            {
                lockOnSource.PlayOneShot(activeLockOnClip, lockOnVolume * masterVolume);
            }
        }

        private void HandleChannelTuned(int channelIndex, string channelBadge, float frequencyMHz)
        {
            if (sonarSource != null && activeTuningClickClip != null)
            {
                sonarSource.PlayOneShot(activeTuningClickClip, masterVolume * 0.75f);
            }
        }

        #region Procedural Audio Generators

        /// <summary>
        /// Generates an authentic rotary switch dial click and subtle radio flutter when changing channels.
        /// </summary>
        private AudioClip GenerateProceduralTuningClick()
        {
            float duration = 0.08f;
            int numSamples = (int)(SAMPLE_RATE * duration);
            float[] samples = new float[numSamples];
            System.Random rand = new System.Random(1337);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = (float)i / numSamples;
                float envelope = Mathf.Exp(-progress * 28f);

                // Sharp transient click (1800 Hz) + white noise burst
                float click = Mathf.Sin(2f * Mathf.PI * 1800f * t);
                float noise = (float)(rand.NextDouble() * 2.0 - 1.0) * 0.35f;
                samples[i] = (click + noise) * envelope * 0.85f;
            }

            AudioClip clip = AudioClip.Create("Procedural_TuningClick", numSamples, 1, SAMPLE_RATE, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Generates an authentic retro 90s monochrome sonar ping (sine wave with exponential envelope).
        /// </summary>
        private AudioClip GenerateProceduralSonarPing()
        {
            float duration = 0.12f;
            int numSamples = (int)(SAMPLE_RATE * duration);
            float[] samples = new float[numSamples];
            float freq = 980f; // High-pitched crisp telemetry beep

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = (float)i / numSamples;
                float envelope = Mathf.Exp(-progress * 22f); // Sharp percussive decay
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                samples[i] = wave * envelope * 0.8f;
            }

            AudioClip clip = AudioClip.Create("Procedural_SonarPing", numSamples, 1, SAMPLE_RATE, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Generates authentic analog radio static noise with bandpass filtering.
        /// </summary>
        private AudioClip GenerateProceduralRadioStatic()
        {
            float duration = 1.8f; // Seamless loop
            int numSamples = (int)(SAMPLE_RATE * duration);
            float[] samples = new float[numSamples];
            System.Random rand = new System.Random(42);

            float lastSample = 0f;
            for (int i = 0; i < numSamples; i++)
            {
                float whiteNoise = (float)(rand.NextDouble() * 2.0 - 1.0);
                // Pink/Brown noise low-pass filter to sound like an in-helmet VHF radio receiver
                lastSample = (lastSample + (0.04f * whiteNoise)) / 1.04f;
                samples[i] = lastSample * 3.2f;
            }

            AudioClip clip = AudioClip.Create("Procedural_RadioStatic", numSamples, 1, SAMPLE_RATE, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Generates a crisp dual-tone lock-on confirmation chime (1320 Hz + 1760 Hz).
        /// </summary>
        private AudioClip GenerateProceduralLockOnChime()
        {
            float duration = 0.18f;
            int numSamples = (int)(SAMPLE_RATE * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = (float)i / numSamples;
                float envelope = Mathf.Exp(-progress * 14f);

                float tone1 = Mathf.Sin(2f * Mathf.PI * 1320f * t);
                float tone2 = Mathf.Sin(2f * Mathf.PI * 1760f * t);
                samples[i] = (tone1 + tone2) * 0.45f * envelope;
            }

            AudioClip clip = AudioClip.Create("Procedural_LockOnChime", numSamples, 1, SAMPLE_RATE, false);
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion
    }
}
