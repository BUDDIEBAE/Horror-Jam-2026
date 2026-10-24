using System;
using UnityEngine;
using SpaceHorror.Player;

namespace SpaceHorror.Audio
{
    /// <summary>
    /// Immersive In-Helmet Audio System for 6DoF Deep Space Horror.
    /// Simulates claustrophobic interior-suit acoustics (bone-conducted heartbeat, 
    /// respiratory airflow, thruster hiss, depressurization alarms, and collision thuds).
    /// 
    /// HYBRID ASSET ARCHITECTURE:
    /// - Serialized fields allow dragging custom .wav / .ogg sound clips into the Inspector.
    /// - If any clip is unassigned (null), a procedural math sound synthesizer automatically
    ///   generates authentic retro sci-fi waveforms at runtime with ZERO external dependencies!
    /// </summary>
    [RequireComponent(typeof(AstronautSurvival))]
    public class AstronautAudioSystem : MonoBehaviour
    {
        [Header("Custom Audio Clips (Optional - Procedural Fallback if empty)")]
        [Tooltip("Custom heartbeat thump clip. If null, procedural sub-bass dual thump is generated.")]
        [SerializeField] private AudioClip customHeartbeatClip;

        [Tooltip("Custom calm breathing loop clip. If null, procedural airflow is generated.")]
        [SerializeField] private AudioClip customBreathingNormalClip;

        [Tooltip("Custom heavy/panicked breathing clip. If null, procedural rapid breath is generated.")]
        [SerializeField] private AudioClip customBreathingHeavyClip;

        [Tooltip("Custom low oxygen computer alarm. If null, procedural retro 2-tone alarm is generated.")]
        [SerializeField] private AudioClip customLowOxygenAlarmClip;

        [Tooltip("Custom RCS cold-gas thruster hiss loop. If null, procedural jet hiss is generated.")]
        [SerializeField] private AudioClip customThrusterHissClip;

        [Tooltip("Custom hull collision impact thud. If null, procedural metallic thud is generated.")]
        [SerializeField] private AudioClip customSuitImpactClip;

        [Tooltip("Custom suit puncture depressurization hiss. If null, procedural high-pressure leak is generated.")]
        [SerializeField] private AudioClip customSuitPunctureClip;

        [Header("Master & Mix Volumes")]
        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 0.9f;

        [Range(0f, 1f)]
        [SerializeField] private float heartbeatVolume = 0.85f;

        [Range(0f, 1f)]
        [SerializeField] private float breathingVolume = 0.65f;

        [Range(0f, 1f)]
        [SerializeField] private float thrusterVolume = 0.55f;

        [Range(0f, 1f)]
        [SerializeField] private float alarmVolume = 0.75f;

        [Range(0f, 1f)]
        [SerializeField] private float impactVolume = 0.9f;

        // Dedicated AudioSource channels (All 2D helmet interior acoustics)
        private AudioSource heartbeatSource;
        private AudioSource breathingSource;
        private AudioSource thrusterSource;
        private AudioSource alarmSource;
        private AudioSource impactSource;
        private AudioSource punctureSource;

        // Active working clips (either user custom or procedural)
        private AudioClip activeHeartbeatClip;
        private AudioClip activeBreathingNormalClip;
        private AudioClip activeBreathingHeavyClip;
        private AudioClip activeAlarmClip;
        private AudioClip activeThrusterClip;
        private AudioClip activeImpactClip;
        private AudioClip activePunctureClip;

        // Component References
        private AstronautSurvival survival;
        private AstronautMovement6D movement;
        private AstronautInputHandler inputHandler;

        // Dynamic Audio State Tracking
        private float heartbeatTimer = 0f;
        private float currentBPM = 72f;
        private bool isCriticalO2 = false;
        private bool isAsphyxiated = false;
        private float targetThrusterVolume = 0f;
        private float currentThrusterVolume = 0f;
        private float breathingPhaseTimer = 0f;
        private bool isExhaling = false;

        private const int SAMPLE_RATE = 44100;

        private void Awake()
        {
            survival = GetComponent<AstronautSurvival>();
            movement = GetComponent<AstronautMovement6D>();
            inputHandler = GetComponent<AstronautInputHandler>();

            SetupAudioChannels();
            InitializeAudioClips();
        }

        private void OnEnable()
        {
            if (survival != null)
            {
                survival.OnHeartRateChanged += HandleHeartRateChanged;
                survival.OnLowOxygenWarning += HandleLowOxygenWarning;
                survival.OnPlayerAsphyxiated += HandlePlayerAsphyxiated;
                survival.OnSuitPunctured += HandleSuitPunctured;
            }

            if (movement != null)
            {
                movement.OnSuitImpact += HandleSuitImpact;
            }
        }

        private void OnDisable()
        {
            if (survival != null)
            {
                survival.OnHeartRateChanged -= HandleHeartRateChanged;
                survival.OnLowOxygenWarning -= HandleLowOxygenWarning;
                survival.OnPlayerAsphyxiated -= HandlePlayerAsphyxiated;
                survival.OnSuitPunctured -= HandleSuitPunctured;
            }

            if (movement != null)
            {
                movement.OnSuitImpact -= HandleSuitImpact;
            }
        }

        private void Start()
        {
            // Start looping channels
            if (activeThrusterClip != null)
            {
                thrusterSource.clip = activeThrusterClip;
                thrusterSource.loop = true;
                thrusterSource.volume = 0f;
                thrusterSource.Play();
            }

            if (activePunctureClip != null)
            {
                punctureSource.clip = activePunctureClip;
                punctureSource.loop = true;
                punctureSource.volume = 0f;
                punctureSource.Play();
            }
        }

        private void Update()
        {
            if (isAsphyxiated)
            {
                HandleAsphyxiationFade();
                return;
            }

            UpdateHeartbeat();
            UpdateBreathing();
            UpdateThrusters();
            UpdatePunctureHiss();
        }

        #region Channel Setup & Procedural Generation

        private void SetupAudioChannels()
        {
            // All helmet audio is 2D (SpatialBlend = 0) because sound travels directly through
            // suit atmosphere and bone conduction to the astronaut's ears, not through space vacuum!
            heartbeatSource = CreateAudioSource("Audio_Heartbeat");
            breathingSource = CreateAudioSource("Audio_Breathing");
            thrusterSource = CreateAudioSource("Audio_Thrusters");
            alarmSource = CreateAudioSource("Audio_SuitAlarm");
            impactSource = CreateAudioSource("Audio_SuitImpact");
            punctureSource = CreateAudioSource("Audio_PunctureLeak");
        }

        private AudioSource CreateAudioSource(string name)
        {
            GameObject channelObj = new GameObject(name);
            channelObj.transform.SetParent(transform, false);

            AudioSource source = channelObj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 100% 2D in-ear helmet acoustic
            source.priority = 64;
            source.reverbZoneMix = 0.1f; // Dry enclosed helmet interior
            return source;
        }

        private void InitializeAudioClips()
        {
            // Heartbeat
            activeHeartbeatClip = customHeartbeatClip != null ? customHeartbeatClip : GenerateProceduralHeartbeat();

            // Breathing
            activeBreathingNormalClip = customBreathingNormalClip != null ? customBreathingNormalClip : GenerateProceduralBreathCycle(2.8f, 0.4f);
            activeBreathingHeavyClip = customBreathingHeavyClip != null ? customBreathingHeavyClip : GenerateProceduralBreathCycle(1.3f, 0.7f);

            // Alarm
            activeAlarmClip = customLowOxygenAlarmClip != null ? customLowOxygenAlarmClip : GenerateProceduralAlarmBeep();

            // Thrusters
            activeThrusterClip = customThrusterHissClip != null ? customThrusterHissClip : GenerateProceduralThrusterHiss();

            // Impact & Puncture
            activeImpactClip = customSuitImpactClip != null ? customSuitImpactClip : GenerateProceduralImpactThud();
            activePunctureClip = customSuitPunctureClip != null ? customSuitPunctureClip : GenerateProceduralPunctureLeak();
        }

        #endregion

        #region Dynamic Audio Updates

        private void UpdateHeartbeat()
        {
            // Calculate beat interval in seconds based on current BPM
            float beatInterval = 60f / Mathf.Max(30f, currentBPM);
            heartbeatTimer += Time.deltaTime;

            if (heartbeatTimer >= beatInterval)
            {
                heartbeatTimer -= beatInterval;

                // Modulate volume and pitch with adrenaline/stress
                float bpmFactor = Mathf.InverseLerp(70f, 180f, currentBPM);
                float volume = heartbeatVolume * masterVolume * Mathf.Lerp(0.6f, 1.3f, bpmFactor);
                float pitch = Mathf.Lerp(0.95f, 1.25f, bpmFactor);

                heartbeatSource.pitch = pitch;
                heartbeatSource.PlayOneShot(activeHeartbeatClip, volume);
            }
        }

        private void UpdateBreathing()
        {
            // Breathing rate accelerates proportionally with heart rate
            float bpmFactor = Mathf.InverseLerp(65f, 180f, currentBPM);
            float cycleDuration = Mathf.Lerp(3.2f, 1.2f, bpmFactor);

            breathingPhaseTimer += Time.deltaTime;
            if (breathingPhaseTimer >= cycleDuration * 0.5f)
            {
                breathingPhaseTimer = 0f;
                isExhaling = !isExhaling;

                // Play appropriate breath sample
                AudioClip breathClip = bpmFactor > 0.55f || isCriticalO2 ? activeBreathingHeavyClip : activeBreathingNormalClip;
                float breathVol = breathingVolume * masterVolume * Mathf.Lerp(0.5f, 1.2f, bpmFactor);
                float breathPitch = isExhaling ? 0.92f : 1.05f;

                breathingSource.pitch = breathPitch;
                breathingSource.PlayOneShot(breathClip, breathVol);
            }
        }

        private void UpdateThrusters()
        {
            // Check translational and rotational input from player
            bool isThrusting = false;
            bool isBoosting = false;

            if (inputHandler != null)
            {
                Vector3 move = inputHandler.MovementInput;
                float roll = inputHandler.RotationInput.z;
                isBoosting = inputHandler.IsBoosting;

                if (move.sqrMagnitude > 0.05f || Mathf.Abs(roll) > 0.05f)
                {
                    isThrusting = true;
                }
            }

            // Adjust target thruster volume
            targetThrusterVolume = isThrusting ? (isBoosting ? 1.0f : 0.65f) : 0f;
            currentThrusterVolume = Mathf.MoveTowards(currentThrusterVolume, targetThrusterVolume, Time.deltaTime * 6f);

            thrusterSource.volume = currentThrusterVolume * thrusterVolume * masterVolume;
            thrusterSource.pitch = isBoosting ? 1.2f : 1.0f;
        }

        private void UpdatePunctureHiss()
        {
            if (survival == null) return;

            // Puncture hiss volume scales with suit damage
            float damageNormalized = 1f - survival.CurrentSuitIntegrityNormalized;
            float targetLeakVolume = damageNormalized > 0.01f ? Mathf.Lerp(0.2f, 1.0f, damageNormalized) : 0f;

            punctureSource.volume = Mathf.MoveTowards(punctureSource.volume, targetLeakVolume * masterVolume * 0.85f, Time.deltaTime * 2f);
        }

        private void HandleAsphyxiationFade()
        {
            // Asphyxiation death: fade all respiratory audio out into chilling cosmic silence
            float fadeSpeed = Time.deltaTime * 0.8f;
            heartbeatSource.volume = Mathf.MoveTowards(heartbeatSource.volume, 0f, fadeSpeed);
            breathingSource.volume = Mathf.MoveTowards(breathingSource.volume, 0f, fadeSpeed * 1.5f);
            thrusterSource.volume = Mathf.MoveTowards(thrusterSource.volume, 0f, fadeSpeed);
            alarmSource.volume = Mathf.MoveTowards(alarmSource.volume, 0f, fadeSpeed * 0.5f);
            punctureSource.volume = Mathf.MoveTowards(punctureSource.volume, 0f, fadeSpeed);
        }

        #endregion

        #region Event Callbacks

        private void HandleHeartRateChanged(float bpm)
        {
            currentBPM = bpm;
        }

        private void HandleLowOxygenWarning(bool isCritical)
        {
            isCriticalO2 = isCritical;

            if (isCritical)
            {
                // Play looping or repeating alarm pulse
                if (!alarmSource.isPlaying && activeAlarmClip != null)
                {
                    alarmSource.clip = activeAlarmClip;
                    alarmSource.loop = true;
                    alarmSource.volume = alarmVolume * masterVolume;
                    alarmSource.Play();
                }
            }
            else
            {
                if (alarmSource.isPlaying)
                {
                    alarmSource.Stop();
                }
            }
        }

        private void HandlePlayerAsphyxiated()
        {
            isAsphyxiated = true;
            // Play one final weak dying heartbeat thump
            heartbeatSource.pitch = 0.7f;
            heartbeatSource.PlayOneShot(activeHeartbeatClip, heartbeatVolume * masterVolume * 0.5f);
        }

        private void HandleSuitPunctured(float damageAmount)
        {
            // Play puncture thud & rupture sound
            if (activeImpactClip != null)
            {
                float vol = Mathf.Clamp01(damageAmount / 20f) * impactVolume * masterVolume;
                impactSource.pitch = UnityEngine.Random.Range(0.85f, 1.1f);
                impactSource.PlayOneShot(activeImpactClip, Mathf.Max(0.5f, vol));
            }
        }

        private void HandleSuitImpact(float impactSpeed, Vector3 contactPoint)
        {
            if (activeImpactClip != null)
            {
                float vol = Mathf.InverseLerp(1.5f, 10f, impactSpeed) * impactVolume * masterVolume;
                impactSource.pitch = UnityEngine.Random.Range(0.9f, 1.15f);
                impactSource.PlayOneShot(activeImpactClip, Mathf.Max(0.3f, vol));
            }
        }

        #endregion

        #region Procedural Audio Synthesizers

        /// <summary>
        /// Generates an anatomical dual-thump "Lub-Dub" heartbeat waveform.
        /// Deep sub-bass resonance (50-60 Hz) simulating acoustic skull/bone conduction.
        /// </summary>
        private AudioClip GenerateProceduralHeartbeat()
        {
            float duration = 0.42f;
            int samples = Mathf.CeilToInt(duration * SAMPLE_RATE);
            float[] data = new float[samples];

            float s1Duration = 0.14f; // "Lub" (S1 - AV valves closure)
            float s2Start = 0.22f;    // Interval
            float s2Duration = 0.12f; // "Dub" (S2 - Semilunar valves closure)

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float sample = 0f;

                // S1 (Lub)
                if (t < s1Duration)
                {
                    float env = Mathf.Exp(-16f * t) * Mathf.Sin(Mathf.PI * (t / s1Duration));
                    sample += Mathf.Sin(2f * Mathf.PI * 52f * t) * env;
                    sample += 0.35f * Mathf.Sin(2f * Mathf.PI * 105f * t) * env;
                }
                // S2 (Dub)
                else if (t >= s2Start && t < s2Start + s2Duration)
                {
                    float tRel = t - s2Start;
                    float env = Mathf.Exp(-20f * tRel) * Mathf.Sin(Mathf.PI * (tRel / s2Duration));
                    sample += 0.8f * Mathf.Sin(2f * Mathf.PI * 64f * tRel) * env;
                    sample += 0.25f * Mathf.Sin(2f * Mathf.PI * 128f * tRel) * env;
                }

                data[i] = Mathf.Clamp(sample * 0.95f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural_Heartbeat", samples, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Generates a realistic respiratory airflow burst inside a sealed helmet visor.
        /// Uses low-pass filtered noise with a soft exponential amplitude envelope.
        /// </summary>
        private AudioClip GenerateProceduralBreathCycle(float duration, float intensity)
        {
            int samples = Mathf.CeilToInt(duration * SAMPLE_RATE);
            float[] data = new float[samples];

            System.Random rand = new System.Random(42);
            float lastVal = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = t / duration;

                // Smooth bell envelope for breath inhalation/exhalation
                float envelope = Mathf.Sin(progress * Mathf.PI);
                envelope = Mathf.Pow(envelope, 1.8f) * intensity;

                // Filtered pink-ish noise
                float white = (float)(rand.NextDouble() * 2.0 - 1.0);
                lastVal = Mathf.Lerp(lastVal, white, 0.15f); // Low-pass filter for soft breath warmth

                // Add slight acoustic cavity resonance (~350Hz)
                float resonance = Mathf.Sin(2f * Mathf.PI * 340f * t) * 0.12f;

                data[i] = Mathf.Clamp((lastVal + resonance) * envelope, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural_Breath", samples, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Generates a cold-gas RCS thruster hiss loop (pressurized nitrogen venting into vacuum).
        /// </summary>
        private AudioClip GenerateProceduralThrusterHiss()
        {
            float duration = 1.0f;
            int samples = Mathf.CeilToInt(duration * SAMPLE_RATE);
            float[] data = new float[samples];

            System.Random rand = new System.Random(1337);
            float lpState = 0f;
            float hpState = 0f;

            for (int i = 0; i < samples; i++)
            {
                float white = (float)(rand.NextDouble() * 2.0 - 1.0);

                // High-pass + Low-pass band filter for cold gas hiss
                lpState = Mathf.Lerp(lpState, white, 0.45f);
                hpState = Mathf.Lerp(hpState, lpState, 0.05f);
                float band = lpState - hpState;

                // Subtle jet flutter
                float flutter = 1.0f + 0.1f * Mathf.Sin(2f * Mathf.PI * 45f * ((float)i / SAMPLE_RATE));

                data[i] = Mathf.Clamp(band * flutter * 0.6f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural_ThrusterHiss", samples, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Generates a retro sci-fi digital suit alarm beep (pulsing dual high-pitched tones).
        /// </summary>
        private AudioClip GenerateProceduralAlarmBeep()
        {
            float duration = 0.65f;
            int samples = Mathf.CeilToInt(duration * SAMPLE_RATE);
            float[] data = new float[samples];

            float toneDuration = 0.12f;
            float tone1Start = 0.02f;
            float tone2Start = 0.18f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float sample = 0f;

                // Tone 1: 960 Hz
                if (t >= tone1Start && t < tone1Start + toneDuration)
                {
                    float tRel = t - tone1Start;
                    float env = Mathf.Sin(Mathf.PI * (tRel / toneDuration));
                    sample += Mathf.Sin(2f * Mathf.PI * 960f * tRel) * env * 0.7f;
                }
                // Tone 2: 1280 Hz
                else if (t >= tone2Start && t < tone2Start + toneDuration)
                {
                    float tRel = t - tone2Start;
                    float env = Mathf.Sin(Mathf.PI * (tRel / toneDuration));
                    sample += Mathf.Sin(2f * Mathf.PI * 1280f * tRel) * env * 0.8f;
                }

                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural_AlarmBeep", samples, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Generates a heavy physical suit collision impact thud with metallic resonance.
        /// </summary>
        private AudioClip GenerateProceduralImpactThud()
        {
            float duration = 0.45f;
            int samples = Mathf.CeilToInt(duration * SAMPLE_RATE);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float env = Mathf.Exp(-14f * t);

                // Deep low-frequency hull shudder (~65Hz)
                float sub = Mathf.Sin(2f * Mathf.PI * 65f * t) * env;

                // Metallic ring overtones (~280Hz and ~560Hz)
                float metallic = Mathf.Sin(2f * Mathf.PI * 280f * t) * Mathf.Exp(-25f * t) * 0.4f
                               + Mathf.Sin(2f * Mathf.PI * 560f * t) * Mathf.Exp(-35f * t) * 0.2f;

                data[i] = Mathf.Clamp((sub + metallic) * 0.9f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural_ImpactThud", samples, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Generates a high-pressure suit puncture leak hiss.
        /// </summary>
        private AudioClip GenerateProceduralPunctureLeak()
        {
            float duration = 1.0f;
            int samples = Mathf.CeilToInt(duration * SAMPLE_RATE);
            float[] data = new float[samples];

            System.Random rand = new System.Random(999);
            float lpState = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float white = (float)(rand.NextDouble() * 2.0 - 1.0);

                // High-frequency hiss
                lpState = Mathf.Lerp(lpState, white, 0.8f);
                float hiss = white - lpState * 0.3f;

                // Pressure whistle resonance (~2400Hz)
                float whistle = Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.08f;

                data[i] = Mathf.Clamp((hiss + whistle) * 0.5f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural_PunctureLeak", samples, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        #endregion

        #region Public Configuration Helper

        /// <summary>
        /// Allows editor scripts or runtime managers to assign custom audio clips.
        /// </summary>
        public void SetCustomClips(
            AudioClip heartbeat = null,
            AudioClip breathNormal = null,
            AudioClip breathHeavy = null,
            AudioClip alarm = null,
            AudioClip thruster = null,
            AudioClip impact = null,
            AudioClip puncture = null)
        {
            if (heartbeat != null) customHeartbeatClip = heartbeat;
            if (breathNormal != null) customBreathingNormalClip = breathNormal;
            if (breathHeavy != null) customBreathingHeavyClip = breathHeavy;
            if (alarm != null) customLowOxygenAlarmClip = alarm;
            if (thruster != null) customThrusterHissClip = thruster;
            if (impact != null) customSuitImpactClip = impact;
            if (puncture != null) customSuitPunctureClip = puncture;

            InitializeAudioClips();
        }

        #endregion
    }
}
