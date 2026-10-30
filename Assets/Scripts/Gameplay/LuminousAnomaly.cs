using System;
using System.Collections;
using UnityEngine;
using SpaceHorror.Player;
using SpaceHorror.Interaction;
using SpaceHorror.UI;

namespace SpaceHorror.Gameplay
{
    /// <summary>
    /// Event 2: Luminous Anomaly Orb (Day 3 Feature).
    /// Mysterious radiant spherical anomaly drifting in deep space void.
    /// Strictly adheres to Film Noir monochrome: stark white pulsating glow against pitch-black space.
    /// Player must scan anomalies (Hold [LMB] for 2.0s), which increases the player's Sanity level.
    /// Only manifests during Day 3.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MissionInteractable))]
    [RequireComponent(typeof(SignalBeacon))]
    public class LuminousAnomaly : MonoBehaviour
    {
        [Header("Anomaly Identity & Radio")]
        [Tooltip("Identifier code displayed on helmet HUD (e.g. 'ANOMALY', 'SPECTRAL-ORB')")]
        [SerializeField] private string anomalyId = "ANOMALY-01";

        [Tooltip("Optional radio broadcast beacon. Transmits on 166.6 MHz so astronaut can track it via VHF radio.")]
        [SerializeField] private bool broadcastBeacon = true;
        [SerializeField] private float broadcastFrequencyMHz = 166.6f;

        [Tooltip("If true, keeps this anomaly active in Play mode regardless of the active day (convenient for testing)")]
        [SerializeField] private bool forceActiveForTesting = false;

        [Tooltip("Current sleep/dormant state of this anomaly")]
        [SerializeField] private bool isSleeping = true;

        [Header("Sanity & Gameplay Impact")]
        [Tooltip("Sanity points added to the player when this anomaly is successfully scanned")]
        [SerializeField] private int sanityGain = 1;

        [Tooltip("Heart rate spike added to astronaut upon completing scan")]
        [SerializeField] private float heartSpikeOnScan = 30f;

        [Tooltip("Hold duration in seconds required to complete spectral scan")]
        [SerializeField] private float scanDuration = 2.0f;

        [Header("Lighting & Visual Pulsation (Film Noir Monochrome)")]
        [Tooltip("Point light casting stark chiaroscuro light onto surrounding debris")]
        [SerializeField] private Light orbLight;

        [Tooltip("Renderer of the glowing sphere")]
        [SerializeField] private Renderer orbRenderer;

        [Tooltip("Base light intensity when idle (tuned for URP: 1.2 to 2.0 avoids blown-out clipping)")]
        [SerializeField] private float baseLightIntensity = 1.5f;

        [Tooltip("Light range in meters (intimate radius around debris)")]
        [SerializeField] private float lightRange = 7.0f;

        [Tooltip("Speed of the breathing light pulsation")]
        [SerializeField] private float pulseSpeed = 1.6f;

        [Tooltip("Gentle zero-g floating levitation amplitude")]
        [SerializeField] private float floatAmplitude = 0.25f;
        [SerializeField] private float floatFrequency = 0.65f;

        [Header("Audio Feedback")]
        [Tooltip("Spatial 3D ambient hum loop audio")]
        [SerializeField] private AudioClip customHumAudioClip;

        [Tooltip("Audio played upon completing spectral scan")]
        [SerializeField] private AudioClip customScanCompleteAudioClip;

        [Header("Particle Effects")]
        [Tooltip("Particle effect prefab instantiated when scan/interaction completes (e.g. spectral burst, implosion flash, debris dust).")]
        [SerializeField] private GameObject interactionParticlePrefab;

        [Tooltip("Optional existing ParticleSystem component in children to play when scan completes.")]
        [SerializeField] private ParticleSystem interactionParticleSystem;

        [Tooltip("Custom spawn transform for the particle effect. If null, spawns at the anomaly position.")]
        [SerializeField] private Transform particleSpawnAnchor;

        public GameObject InteractionParticlePrefab
        {
            get => interactionParticlePrefab;
            set => interactionParticlePrefab = value;
        }

        public ParticleSystem InteractionParticleSystem
        {
            get => interactionParticleSystem;
            set => interactionParticleSystem = value;
        }

        public Transform ParticleSpawnAnchor
        {
            get => particleSpawnAnchor;
            set => particleSpawnAnchor = value;
        }

        // References & Runtime States
        private MissionInteractable interactable;
        private SignalBeacon signalBeacon;
        private AudioSource audioSource;
        private AudioSource humAudioSource;
        private AudioClip proceduralHumClip;
        private AudioClip proceduralScanClip;

        private Vector3 initialPosition;
        private Vector3 initialScale;
        private bool isScanned = false;
        private float currentHoldProgress = 0f;
        private Coroutine implosionCoroutine;

        public bool IsScanned => isScanned;
        public bool IsSleeping => isSleeping;
        public string AnomalyId => anomalyId;
        public bool ForceActiveForTesting
        {
            get => forceActiveForTesting;
            set
            {
                forceActiveForTesting = value;
                EvaluateSleepState();
            }
        }

        private void Awake()
        {
            initialPosition = transform.position;
            initialScale = transform.localScale;

            // Auto-normalize legacy overblown values from inspector/scene to Film Noir standards
            if (baseLightIntensity > 4.0f) baseLightIntensity = 1.5f;
            if (lightRange > 10.0f) lightRange = 7.0f;

            EnsureComponents();
            SetupAudioSources();
        }

        private void OnEnable()
        {
            SubscribeToDayCycle();
            EvaluateSleepState();
        }

        private void OnDisable()
        {
            UnsubscribeFromDayCycle();
        }

        private void Start()
        {
            SubscribeToDayCycle();
            ConfigureInteractable();
            ConfigureBeacon();
            EvaluateSleepState();
        }

        private void SubscribeToDayCycle()
        {
            DayCycleManager dcm = DayCycleManager.Instance;
            if (dcm == null) dcm = FindFirstObjectByType<DayCycleManager>();

            if (dcm != null)
            {
                dcm.OnDayStarted -= HandleDayStarted;
                dcm.OnDayStarted += HandleDayStarted;
            }
        }

        private void UnsubscribeFromDayCycle()
        {
            DayCycleManager dcm = DayCycleManager.Instance;
            if (dcm == null) dcm = FindFirstObjectByType<DayCycleManager>();

            if (dcm != null)
            {
                dcm.OnDayStarted -= HandleDayStarted;
            }
        }

        private void Reset()
        {
            EnsureComponents();
            ConfigureInteractable();
            ConfigureBeacon();
            EvaluateSleepState();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                EnsureComponents();
                ConfigureInteractable();
                ConfigureBeacon();
                EvaluateSleepState();
            }
        }

        private void Update()
        {
            if (isScanned) return;

            // Self-healing state evaluation in Play Mode:
            // Dynamically checks DayCycle state each frame so changing Day or starting Day 3
            // immediately triggers WakeUp() even if event timing was desynchronized.
            if (Application.isPlaying)
            {
                DayCycleManager dcm = DayCycleManager.Instance;
                if (dcm == null) dcm = FindFirstObjectByType<DayCycleManager>();

                int currentDay = dcm != null ? dcm.CurrentDay : 1;
                bool shouldBeAwake = ((currentDay == 3) || forceActiveForTesting);

                if (shouldBeAwake && isSleeping)
                {
                    WakeUp();
                }
                else if (!shouldBeAwake && !isSleeping)
                {
                    Sleep();
                }
            }

            if (isSleeping) return;

            UpdateVisualPulsation();
            UpdateZeroGDrift();
        }

        /// <summary>
        /// Ensures all visual, collider, light, and interaction components exist.
        /// Procedurally generates sphere and unlit stark white material if not present.
        /// </summary>
        private void EnsureComponents()
        {
            interactable = GetComponent<MissionInteractable>();
            if (interactable == null) interactable = gameObject.AddComponent<MissionInteractable>();

            // Ensure Sphere Collider for zero-g raycasting / interact detection
            SphereCollider col = GetComponent<SphereCollider>();
            if (col == null)
            {
                col = gameObject.AddComponent<SphereCollider>();
                col.radius = 0.8f;
                col.isTrigger = false;
            }

            // Ensure Orb Light
            if (orbLight == null)
            {
                orbLight = GetComponentInChildren<Light>();
                if (orbLight == null)
                {
                    GameObject lightObj = new GameObject("Orb_PointLight");
                    lightObj.transform.SetParent(transform, false);
                    orbLight = lightObj.AddComponent<Light>();
                }
            }

            if (orbLight != null)
            {
                orbLight.type = LightType.Point;
                orbLight.color = Color.white;
                orbLight.range = lightRange;
                orbLight.intensity = baseLightIntensity;
                orbLight.shadows = LightShadows.Hard; // Pure Noir hard shadows
            }

            // Ensure Orb Mesh Renderer
            if (orbRenderer == null)
            {
                orbRenderer = GetComponentInChildren<MeshRenderer>();
                if (orbRenderer == null)
                {
                    MeshFilter mf = GetComponent<MeshFilter>();
                    if (mf == null) mf = gameObject.AddComponent<MeshFilter>();

                    GameObject tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    mf.sharedMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
                    DestroyImmediate(tempSphere);

                    orbRenderer = gameObject.AddComponent<MeshRenderer>();
                    Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
                    if (unlit == null) unlit = Shader.Find("Unlit/Color");
                    if (unlit == null) unlit = Shader.Find("Standard");

                    Material mat = new Material(unlit);
                    mat.color = Color.white;
                    orbRenderer.sharedMaterial = mat;
                }
            }

            if (orbRenderer != null)
            {
                orbRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                orbRenderer.receiveShadows = false;
            }
        }

        private void SetupAudioSources()
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1.0f; // 3D positional
                audioSource.minDistance = 2.0f;
                audioSource.maxDistance = 18.0f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
            }

            if (humAudioSource == null)
            {
                humAudioSource = gameObject.AddComponent<AudioSource>();
                humAudioSource.playOnAwake = false;
                humAudioSource.loop = true;
                humAudioSource.spatialBlend = 1.0f;
                humAudioSource.minDistance = 1.5f;
                humAudioSource.maxDistance = 16.0f;
                humAudioSource.volume = 0.45f;
                humAudioSource.rolloffMode = AudioRolloffMode.Linear;
            }
        }

        private void StartAmbientHum()
        {
            if (isSleeping || isScanned) return;

            if (humAudioSource != null && !humAudioSource.isPlaying && gameObject.activeInHierarchy)
            {
                AudioClip clip = customHumAudioClip != null ? customHumAudioClip : GetProceduralHumClip();
                if (clip != null)
                {
                    humAudioSource.clip = clip;
                    humAudioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
                    humAudioSource.Play();
                }
            }
        }

        private void ConfigureInteractable()
        {
            if (interactable == null) return;

            interactable.ConfigureRuntimePrompts(
                $"LUMINOUS ANOMALY // {anomalyId}",
                "SCAN ANOMALY",
                "SPECTRAL EMISSION SECURED // SANITY ACCUMULATED"
            );
            interactable.ConfigureInteractionMethod(InteractionMethod.HoldToComplete, scanDuration, ObjectiveType.Custom);
            interactable.IsSingleUse = true;
            interactable.RequiresActiveMission = true; // Gated strictly to active mission contract

            interactable.onHoldProgress.RemoveListener(HandleHoldProgress);
            interactable.onHoldProgress.AddListener(HandleHoldProgress);

            interactable.onInteracted.RemoveListener(HandleScanCompleted);
            interactable.onInteracted.AddListener(HandleScanCompleted);
        }

        public void ConfigureBeacon()
        {
            if (signalBeacon == null) signalBeacon = GetComponent<SignalBeacon>();
            if (signalBeacon == null) signalBeacon = gameObject.AddComponent<SignalBeacon>();

            if (signalBeacon != null)
            {
                if (broadcastBeacon)
                {
                    signalBeacon.Configure("ANOMALY", $"LUMINOUS EMISSION [{anomalyId}]", broadcastFrequencyMHz, 160f, 3);
                    signalBeacon.SetTransmitting(!isSleeping && !isScanned);
                }
                else
                {
                    signalBeacon.SetTransmitting(false);
                }
            }
        }

        private void HandleDayStarted(int dayNumber, DayExpeditionData data)
        {
            EvaluateSleepState();
        }

        public void UpdateDay3Visibility()
        {
            EvaluateSleepState();
        }

        /// <summary>
        /// Evaluates whether this anomaly should be in Sleep mode or Awake mode based on DayCycle.
        /// </summary>
        public void EvaluateSleepState()
        {
            if (!Application.isPlaying)
            {
                // In Editor mode, keep awake so level designers can see it in Scene View
                WakeUp();
                return;
            }

            DayCycleManager dcm = DayCycleManager.Instance;
            if (dcm == null) dcm = FindFirstObjectByType<DayCycleManager>();

            int currentDay = dcm != null ? dcm.CurrentDay : 1;
            bool shouldBeAwake = ((currentDay == 3) || forceActiveForTesting) && !isScanned;

            if (shouldBeAwake)
            {
                WakeUp();
            }
            else
            {
                Sleep();
            }
        }

        /// <summary>
        /// Puts the anomaly into dormant sleep mode: light off, sound stopped, mesh hidden, radio silent.
        /// </summary>
        [ContextMenu("Sleep Anomaly")]
        public void Sleep()
        {
            isSleeping = true;

            // 1. Completely turn off light (disable component AND deactivate GameObject)
            if (orbLight != null)
            {
                orbLight.enabled = false;
                orbLight.gameObject.SetActive(false);
            }

            // 2. Hide visual mesh renderer
            if (orbRenderer != null)
            {
                orbRenderer.enabled = false;
            }

            // 3. Disable collider
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // 4. Shut down audio completely
            if (humAudioSource != null)
            {
                if (humAudioSource.isPlaying) humAudioSource.Stop();
                humAudioSource.enabled = false;
            }
            if (audioSource != null)
            {
                if (audioSource.isPlaying) audioSource.Stop();
                audioSource.enabled = false;
            }

            // 5. Silence radio beacon
            if (signalBeacon != null)
            {
                signalBeacon.SetTransmitting(false);
            }

            // 6. Disable interactable
            if (interactable != null)
            {
                interactable.enabled = false;
            }
        }

        /// <summary>
        /// Wakes up the anomaly for Day 3: light glowing, ambient hum playing, radio broadcasting.
        /// </summary>
        [ContextMenu("Wake Up Anomaly")]
        public void WakeUp()
        {
            if (isScanned) return;
            isSleeping = false;

            // 1. Activate light GameObject and enable light component
            if (orbLight != null)
            {
                orbLight.gameObject.SetActive(true);
                orbLight.enabled = true;
                orbLight.intensity = baseLightIntensity;
            }

            // 2. Enable visual mesh renderer
            if (orbRenderer != null)
            {
                orbRenderer.enabled = true;
            }

            // 3. Enable collider
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = true;

            // 4. Enable and start audio hum
            if (humAudioSource != null)
            {
                humAudioSource.enabled = true;
                if (Application.isPlaying && !humAudioSource.isPlaying)
                {
                    StartAmbientHum();
                }
            }
            if (audioSource != null)
            {
                audioSource.enabled = true;
            }

            // 5. Broadcast radio beacon
            if (signalBeacon != null && broadcastBeacon)
            {
                signalBeacon.SetTransmitting(true);
            }

            // 6. Enable interactable
            if (interactable != null)
            {
                interactable.enabled = true;
            }
        }

        /// <summary>
        /// Updates the visual breathing glow and intensifies light while player holds scan.
        /// </summary>
        private void UpdateVisualPulsation()
        {
            float sinePulse = (Mathf.Sin(Time.time * pulseSpeed) + 1.0f) * 0.5f; // 0.0 to 1.0
            float scanMultiplier = 1.0f + (currentHoldProgress * 0.65f); // Subtle, tense brightness surge during hold

            if (orbLight != null)
            {
                // Pulsate gently between base (1.5) and 2.1, with gentle scan boost
                float targetIntensity = (baseLightIntensity + (sinePulse * 0.6f)) * scanMultiplier;
                orbLight.intensity = targetIntensity;
                orbLight.range = lightRange + (sinePulse * 0.75f);
            }

            if (humAudioSource != null && humAudioSource.isPlaying)
            {
                // Pitch bends up during hold to create rising tension
                humAudioSource.pitch = Mathf.Lerp(1.0f, 1.45f, currentHoldProgress);
            }
        }

        private void UpdateZeroGDrift()
        {
            // Gentle hovering vertical & lateral oscillation
            float yOffset = Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
            float xOffset = Mathf.Cos(Time.time * floatFrequency * 0.7f) * (floatAmplitude * 0.5f);
            transform.position = initialPosition + new Vector3(xOffset, yOffset, 0f);
        }

        /// <summary>
        /// Invoked every frame while player holds [LMB] on this anomaly.
        /// </summary>
        private void HandleHoldProgress(float progress)
        {
            if (isScanned) return;
            currentHoldProgress = progress;
        }

        /// <summary>
        /// Triggered when the scan reaches 100% completion.
        /// Increases Sanity, spikes heart rate, flashes visor, spawns particle, and implodes the anomaly.
        /// </summary>
        private void HandleScanCompleted(GameObject player)
        {
            if (isScanned) return;
            isScanned = true;

            // Spawn particle effects with strict null safety guards
            SpawnInteractionParticles();

            // 1. Increase Player Sanity
            AstronautSurvival survival = player != null ? player.GetComponent<AstronautSurvival>() : FindFirstObjectByType<AstronautSurvival>();
            if (survival != null)
            {
                survival.IncreaseSanity(sanityGain);
                if (heartSpikeOnScan > 0f)
                {
                    survival.TriggerHeartSpike(heartSpikeOnScan);
                }
            }

            // 2. Visor HUD CRT Glitch & Stark White Noir Flash
            var hud = FindFirstObjectByType<AstronautHelmetHUD>();
            if (hud != null)
            {
                hud.TriggerVisorGlitch(0.35f);
                hud.TriggerNoirFlash(0.25f);
                hud.ShowMissionTransmission(
                    ">> ANOMALY SPECTRAL DATA SECURED",
                    $"ANOMALOUS WAVELENGTH CATALOGUED.\nSANITY EXPOSURE LEVEL: {survival?.Sanity ?? 1}\nPSYCHOMETRIC TENSION ACCELERATED.",
                    4.5f
                );
            }

            // 3. Play Scan Implosion Audio Pop
            AudioClip popClip = customScanCompleteAudioClip != null ? customScanCompleteAudioClip : GetProceduralScanClip();
            if (audioSource != null && popClip != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
                audioSource.PlayOneShot(popClip, 1.0f);
            }

            // 4. Silence Beacon & Ambient Hum
            if (humAudioSource != null && humAudioSource.isPlaying)
            {
                humAudioSource.Stop();
            }

            if (signalBeacon != null)
            {
                signalBeacon.SetTransmitting(false);
                signalBeacon.enabled = false;
            }

            // 5. Implosion Visual Effect
            if (implosionCoroutine != null) StopCoroutine(implosionCoroutine);
            implosionCoroutine = StartCoroutine(RoutineImplosion());

            // 6. Notify HorrorEventManager of Event 2 progress
            if (HorrorEventManager.Instance != null)
            {
                HorrorEventManager.Instance.NotifyAnomalyScanned(survival != null ? survival.Sanity : 1);
            }

            Debug.Log($"<color=#FFFFFF><b>[LuminousAnomaly]</b></color> Anomaly '{anomalyId}' scanned successfully! Sanity increased by {sanityGain}.");
        }

        /// <summary>
        /// Spawns or plays the assigned interaction particle effect with strict null safety guards.
        /// </summary>
        public void SpawnInteractionParticles()
        {
            // Null safety-guard: Check if particle prefab is assigned
            if (interactionParticlePrefab != null)
            {
                Vector3 spawnPos = particleSpawnAnchor != null ? particleSpawnAnchor.position : transform.position;
                Quaternion spawnRot = particleSpawnAnchor != null ? particleSpawnAnchor.rotation : Quaternion.identity;

                GameObject spawned = Instantiate(interactionParticlePrefab, spawnPos, spawnRot);
                if (spawned != null)
                {
                    ParticleSystem ps = spawned.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        ps.Play();
                        float maxDuration = ps.main.duration + ps.main.startLifetime.constantMax;
                        Destroy(spawned, Mathf.Max(maxDuration, 3.5f));
                    }
                }
            }

            // Null safety-guard: Check if particle system component is assigned
            if (interactionParticleSystem != null)
            {
                interactionParticleSystem.Play();
            }
        }

        private IEnumerator RoutineImplosion()
        {
            // A. Sharp Stark Flash
            if (orbLight != null)
            {
                orbLight.intensity = baseLightIntensity * 2.5f;
                orbLight.range = lightRange * 1.3f;
            }

            yield return new WaitForSeconds(0.08f);

            // B. Rapid collapse / implosion scaling to zero
            float collapseDuration = 0.35f;
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;
            float startIntensity = orbLight != null ? orbLight.intensity : 0f;

            while (elapsed < collapseDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / collapseDuration;
                float easeIn = t * t;

                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, easeIn);
                if (orbLight != null)
                {
                    orbLight.intensity = Mathf.Lerp(startIntensity, 0f, easeIn);
                }

                yield return null;
            }

            transform.localScale = Vector3.zero;
            Sleep();
            implosionCoroutine = null;
        }

        #region Procedural Audio Generators
        private AudioClip GetProceduralHumClip()
        {
            if (proceduralHumClip != null) return proceduralHumClip;

            int sampleRate = 44100;
            float duration = 2.0f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                // Ethereal dual sine resonance (528 Hz pure tone with 1056 Hz harmonic overtone)
                float sine1 = Mathf.Sin(2f * Mathf.PI * 528f * t);
                float sine2 = Mathf.Sin(2f * Mathf.PI * 1056f * t) * 0.35f;
                float vibrato = Mathf.Sin(2f * Mathf.PI * 4.0f * t) * 0.15f;

                samples[i] = (sine1 * 0.7f + sine2) * (1.0f + vibrato) * 0.25f;
            }

            proceduralHumClip = AudioClip.Create("Procedural_Anomaly_Hum", totalSamples, 1, sampleRate, false);
            proceduralHumClip.SetData(samples, 0);
            return proceduralHumClip;
        }

        private AudioClip GetProceduralScanClip()
        {
            if (proceduralScanClip != null) return proceduralScanClip;

            int sampleRate = 44100;
            float duration = 0.8f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 6.0f); // Fast decay
                // High frequency glass resonant pop (1800 Hz down to 320 Hz)
                float freq = Mathf.Lerp(1800f, 320f, t / duration);
                float sine = Mathf.Sin(2f * Mathf.PI * freq * t);
                float hiss = (UnityEngine.Random.value * 2f - 1f) * 0.2f;

                samples[i] = (sine * 0.8f + hiss) * env * 0.75f;
            }

            proceduralScanClip = AudioClip.Create("Procedural_Anomaly_ScanPop", totalSamples, 1, sampleRate, false);
            proceduralScanClip.SetData(samples, 0);
            return proceduralScanClip;
        }
        #endregion

        #region Editor Context Menu & Gizmos
        [ContextMenu("Debug: Simulate Scan 100%")]
        public void DebugSimulateScan()
        {
            var survival = FindFirstObjectByType<AstronautSurvival>();
            HandleScanCompleted(survival != null ? survival.gameObject : gameObject);
        }

        private void OnDrawGizmos()
        {
            // Stark white Noir wireframe sphere gizmo in Scene view
            Gizmos.color = new Color(1f, 1f, 1f, 0.65f);
            Gizmos.DrawWireSphere(transform.position, 0.7f);

            // Subtle outer light reach
            Gizmos.color = new Color(1f, 1f, 1f, 0.12f);
            Gizmos.DrawWireSphere(transform.position, lightRange);
        }
        #endregion
    }
}
