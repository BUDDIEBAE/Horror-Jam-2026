using System;
using System.Collections;
using UnityEngine;
using SpaceHorror.Gameplay;
using SpaceHorror.UI;
using SpaceHorror.Player;
using SpaceHorror.Audio;

namespace SpaceHorror.Interaction
{
    /// <summary>
    /// Attached to fallen player corpses adrift in space.
    /// Manages loop flight telemetry recovery and Event 1 ("The Unsettled Corpse"):
    /// - Emits a faint 40 BPM heartbeat pulse on VHF 128.0 MHz
    /// - At 50% hold progress: Head smoothly turns to look directly at the astronaut
    /// - Spikes the player's heart rate (135+ BPM) and plays suit joint tension audio
    /// - At 100% completion: Blinding chiaroscuro flashlight strobe jolt into visor
    /// </summary>
    [RequireComponent(typeof(MissionInteractable))]
    public class FallenAstronautRecovery : MonoBehaviour
    {
        [Header("Recovery Life Support Values")]
        [Tooltip("Amount of backup oxygen restored upon salvaging the fallen suit")]
        [SerializeField] private float backupOxygenRestored = 25f;

        [Tooltip("Amount of suit integrity patched upon salvaging")]
        [SerializeField] private float suitRepairPatch = 15f;

        [Header("Radio Telemetry Beacon")]
        [Tooltip("Broadcast a distress signal on VHF 128.0 MHz so the corpse can be tracked using the radio scanner")]
        [SerializeField] private bool broadcastDistressSignal = true;
        [SerializeField] private float distressFrequencyMHz = 128.0f;

        [Header("Event 1 - The Unsettled Corpse Rigging")]
        [Tooltip("Optional direct reference to the corpse head bone. If null, automatically resolves 'spine.005' or 'spine.004' from the skeleton.")]
        [SerializeField] private Transform headBoneTransform;

        [Tooltip("Optional direct reference to corpse helmet flashlight. If null, automatically created procedurally.")]
        [SerializeField] private Light corpseHelmetLight;

        [Header("Audio Feedback")]
        [Tooltip("Optional audio clip for suit tension / mechanical creak. If null, generated procedurally.")]
        [SerializeField] private AudioClip customSuitCreakClip;

        [Tooltip("Optional audio clip for the electrical spark jolt at 100%. If null, generated procedurally.")]
        [SerializeField] private AudioClip customSparkJoltClip;

        [Tooltip("Optional audio clip for the faint 40 BPM telemetry pulse. If null, generated procedurally.")]
        [SerializeField] private AudioClip customBioPulseClip;

        // References
        private MissionInteractable interactable;
        private SignalBeacon beacon;
        private AudioSource corpseAudioSource;
        private AudioSource bioPulseAudioSource;

        // Procedural Audio Clips
        private static AudioClip proceduralSuitCreakClip;
        private static AudioClip proceduralSparkJoltClip;
        private static AudioClip proceduralBioPulseClip;

        // Runtime State
        private int corpseLoopDay = 1;
        private int corpseIteration = 1;
        private bool isDataRecovered = false;
        private bool hasSpikedHeartRate = false;
        private bool hasValidHeadBone = false;
        private Quaternion restHeadLocalRotation = Quaternion.identity;
        private float currentHoldProgress = 0f;
        private Coroutine joltCoroutine;

        [Header("Event 1 - Fixed Horror Event Trigger")]
        [Tooltip("If true, this corpse executes Event 1 (Unsettled Head Turn, Heart Spike & Flashlight Jolt). Specifically enabled for the corpse resulting from Event 0 (O2 Station Catastrophic Rupture).")]
        [SerializeField] private bool isUnsettledCorpseEvent = false;

        public bool IsUnsettledCorpseEvent
        {
            get => isUnsettledCorpseEvent;
            set
            {
                isUnsettledCorpseEvent = value;
                if (isUnsettledCorpseEvent && !isDataRecovered && broadcastDistressSignal)
                {
                    StartBioPulseTelemetry();
                }
                else if (!isUnsettledCorpseEvent && bioPulseAudioSource != null && bioPulseAudioSource.isPlaying)
                {
                    bioPulseAudioSource.Stop();
                }
            }
        }

        public bool IsDataRecovered => isDataRecovered;
        public int CorpseLoopDay => corpseLoopDay;
        public int CorpseIteration => corpseIteration;

        private void Awake()
        {
            interactable = GetComponent<MissionInteractable>();
            beacon = GetComponent<SignalBeacon>();

            ResolveHeadBone();
            ResolveHelmetLight();
            SetupCorpseAudioSources();
            SetupRecoveryInteractable();
        }

        private void Start()
        {
            if (isUnsettledCorpseEvent && !isDataRecovered && broadcastDistressSignal)
            {
                StartBioPulseTelemetry();
            }
        }

        public void Initialize(int loopDay, int iteration)
        {
            corpseLoopDay = loopDay;
            corpseIteration = iteration;
            SetupRecoveryInteractable();
        }

        private void Update()
        {
            if (!isUnsettledCorpseEvent || isDataRecovered || !hasValidHeadBone || headBoneTransform == null) return;

            UpdateHeadTracking();
        }

        /// <summary>
        /// Locates spine.005 (Head) or spine.004 (Neck) from the FBX hierarchy safely without throwing exceptions.
        /// </summary>
        private void ResolveHeadBone()
        {
            if (headBoneTransform == null)
            {
                headBoneTransform = FindChildRecursive(transform, "spine.005")
                                 ?? FindChildRecursive(transform, "spine.004")
                                 ?? FindChildFuzzy(transform, "head")
                                 ?? FindChildFuzzy(transform, "neck");
            }

            if (headBoneTransform != null)
            {
                hasValidHeadBone = true;
                restHeadLocalRotation = headBoneTransform.localRotation;
            }
            else
            {
                hasValidHeadBone = false;
            }
        }

        /// <summary>
        /// Ensures a stark white helmet spotlight exists for the Event 1 Chiaroscuro Jolt.
        /// </summary>
        private void ResolveHelmetLight()
        {
            if (corpseHelmetLight == null)
            {
                corpseHelmetLight = GetComponentInChildren<Light>(true);
            }

            if (corpseHelmetLight == null)
            {
                GameObject lightObj = new GameObject("Corpse_HelmetSpotlight");
                Transform parentTarget = headBoneTransform != null ? headBoneTransform : transform;
                lightObj.transform.SetParent(parentTarget, false);
                lightObj.transform.localPosition = new Vector3(0f, 0.12f, 0.22f);
                lightObj.transform.localRotation = Quaternion.identity;

                corpseHelmetLight = lightObj.AddComponent<Light>();
                corpseHelmetLight.type = LightType.Spot;
                corpseHelmetLight.color = Color.white;
                corpseHelmetLight.spotAngle = 55f;
                corpseHelmetLight.innerSpotAngle = 32f;
                corpseHelmetLight.range = 14f;
                corpseHelmetLight.intensity = 0f;
                corpseHelmetLight.enabled = false;
            }
            else
            {
                corpseHelmetLight.enabled = false;
            }
        }

        private void SetupCorpseAudioSources()
        {
            if (corpseAudioSource == null)
            {
                corpseAudioSource = gameObject.AddComponent<AudioSource>();
                corpseAudioSource.playOnAwake = false;
                corpseAudioSource.spatialBlend = 1.0f; // 3D positional zero-g audio
                corpseAudioSource.minDistance = 1.5f;
                corpseAudioSource.maxDistance = 12.0f;
                corpseAudioSource.rolloffMode = AudioRolloffMode.Linear;
            }

            if (bioPulseAudioSource == null)
            {
                bioPulseAudioSource = gameObject.AddComponent<AudioSource>();
                bioPulseAudioSource.playOnAwake = false;
                bioPulseAudioSource.loop = true;
                bioPulseAudioSource.spatialBlend = 1.0f;
                bioPulseAudioSource.minDistance = 1.0f;
                bioPulseAudioSource.maxDistance = 8.0f; // Audible within close proximity
                bioPulseAudioSource.volume = 0.65f;
                bioPulseAudioSource.rolloffMode = AudioRolloffMode.Linear;
            }
        }

        private void StartBioPulseTelemetry()
        {
            if (bioPulseAudioSource != null && !bioPulseAudioSource.isPlaying)
            {
                AudioClip clip = customBioPulseClip != null ? customBioPulseClip : GetProceduralBioPulseClip();
                if (clip != null)
                {
                    bioPulseAudioSource.clip = clip;
                    bioPulseAudioSource.Play();
                }
            }
        }

        public void SetupRecoveryInteractable()
        {
            if (interactable == null) interactable = GetComponent<MissionInteractable>();
            if (interactable == null) interactable = gameObject.AddComponent<MissionInteractable>();

            if (interactable != null)
            {
                if (interactable.onInteracted == null)
                {
                    interactable.onInteracted = new UnityEngine.Events.UnityEvent<GameObject>();
                }
                if (interactable.onHoldProgress == null)
                {
                    interactable.onHoldProgress = new UnityEngine.Events.UnityEvent<float>();
                }

                interactable.IsSingleUse = true;
                interactable.ConfigureInteractionMethod(InteractionMethod.HoldToComplete, 2.0f, ObjectiveType.Custom);
                interactable.ConfigureRuntimePrompts(
                    $"FALLEN ASTRONAUT (LOOP #{corpseIteration})",
                    "RECOVER TELEMETRY",
                    "TELEMETRY SECURED // SYSTEM SYNCED"
                );

                interactable.onInteracted.RemoveListener(HandleDataCollected);
                interactable.onInteracted.AddListener(HandleDataCollected);

                interactable.onHoldProgress.RemoveListener(HandleHoldProgress);
                interactable.onHoldProgress.AddListener(HandleHoldProgress);
            }

            if (broadcastDistressSignal && !isDataRecovered)
            {
                if (beacon == null) beacon = GetComponent<SignalBeacon>();
                if (beacon == null) beacon = gameObject.AddComponent<SignalBeacon>();

                if (beacon != null)
                {
                    beacon.Configure("SOS-CORPSE", $"FALLEN SUIT #{corpseIteration} (128.0 MHz)", distressFrequencyMHz, 160f, 2);
                    beacon.SetTransmitting(true);
                }
            }
        }

        /// <summary>
        /// Evaluates hold progress (0.0 to 1.0) reported by MissionInteractable every frame.
        /// Triggers Event 1 horror staging:
        /// - 50%+: Head turns to face player camera & spikes heart rate to 135+ BPM
        /// </summary>
        private void HandleHoldProgress(float progress)
        {
            if (!isUnsettledCorpseEvent || isDataRecovered) return;

            currentHoldProgress = progress;

            // Trigger Shock Spike when player crosses 50% threshold for the first time
            if (progress >= 0.5f && !hasSpikedHeartRate)
            {
                hasSpikedHeartRate = true;
                TriggerShockSpike();
            }
        }

        private void TriggerShockSpike()
        {
            // 1. Spike Astronaut Heart Rate (Visor HUD & Audio will thump rapidly)
            var survival = FindFirstObjectByType<AstronautSurvival>();
            if (survival != null)
            {
                survival.TriggerHeartSpike(55f);
            }

            // 2. Trigger sharp claustrophobic gasp in helmet microphone
            var audioSys = FindFirstObjectByType<AstronautAudioSystem>();
            if (audioSys != null)
            {
                audioSys.TriggerWakeUpGasp();
            }

            // 3. Play subtle spacesuit joint creak / vacuum tension sound
            PlayOneShotAudio(customSuitCreakClip != null ? customSuitCreakClip : GetProceduralSuitCreakClip(), 0.95f);
        }

        /// <summary>
        /// Smoothly rotates the head towards the player when hold progress is between 50% and 100%.
        /// Adds subtle micro-tremors and grinding stutter to simulate vacuum-frozen joints and seals.
        /// If player releases LMB early, smoothly returns back to the rest orientation.
        /// </summary>
        private void UpdateHeadTracking()
        {
            if (headBoneTransform == null) return;

            if (currentHoldProgress >= 0.5f)
            {
                Camera cam = Camera.main;
                Vector3 targetPos = cam != null ? cam.transform.position : transform.position + transform.forward * 2f;
                Vector3 lookDir = (targetPos - headBoneTransform.position).normalized;

                if (lookDir.sqrMagnitude > 0.001f)
                {
                    Quaternion worldTargetRot = Quaternion.LookRotation(lookDir, transform.up);
                    Quaternion localTargetRot = headBoneTransform.parent != null
                        ? Quaternion.Inverse(headBoneTransform.parent.rotation) * worldTargetRot
                        : worldTargetRot;

                    // Map 0.5f -> 1.0f to 0.0f -> 1.0f
                    float turnFactor = Mathf.InverseLerp(0.5f, 1.0f, currentHoldProgress);
                    Quaternion targetRot = Quaternion.Slerp(restHeadLocalRotation, localTargetRot, turnFactor);

                    // Micro-tremor / organic frozen joint grinding stutter as the neck turns
                    float microJitter = (Mathf.PerlinNoise(Time.time * 32f, 0f) * 2f - 1f) * 2.4f * (1f - turnFactor);
                    Quaternion jitterRot = Quaternion.Euler(microJitter, microJitter * 0.7f, 0f);
                    Quaternion finalRot = targetRot * jitterRot;

                    headBoneTransform.localRotation = Quaternion.Slerp(headBoneTransform.localRotation, finalRot, Time.deltaTime * 6.5f);
                }
            }
            else
            {
                // Smoothly settle back to resting angle if player lets go
                headBoneTransform.localRotation = Quaternion.Slerp(headBoneTransform.localRotation, restHeadLocalRotation, Time.deltaTime * 3.5f);
            }
        }

        /// <summary>
        /// Invoked when hold interaction reaches 100% completion.
        /// Executes Event 1 Finale:
        /// - Stark white Chiaroscuro Visor Flash & Glitch
        /// - Concussive Camera Trauma Shake
        /// - Corpse helmet flashlight strobe with hard shadows
        /// - Electrical snap sound
        /// - Restores O2/suit and advances DayCycleManager
        /// </summary>
        private void HandleDataCollected(GameObject player)
        {
            if (isDataRecovered) return;
            isDataRecovered = true;

            // 1. Stop bio pulse telemetry
            if (bioPulseAudioSource != null && bioPulseAudioSource.isPlaying)
            {
                bioPulseAudioSource.Stop();
            }

            // 2. Trigger Event 1 Chiaroscuro Jolt Strobe Coroutine (Specifically on Event 0 corpse)
            if (isUnsettledCorpseEvent)
            {
                if (joltCoroutine != null) StopCoroutine(joltCoroutine);
                joltCoroutine = StartCoroutine(RoutineCorpseJolt(player));
            }

            // 3. Recover pending mission data in DayCycleManager
            if (DayCycleManager.Instance != null)
            {
                DayCycleManager.Instance.TryRecoverMissionData($"Fallen Suit #{corpseIteration}");
            }

            // 4. Replenish leftover backup oxygen / suit patch from corpse
            if (player != null)
            {
                var survival = player.GetComponent<AstronautSurvival>();
                if (survival != null)
                {
                    if (backupOxygenRestored > 0f) survival.ReplenishOxygen(backupOxygenRestored);
                    if (suitRepairPatch > 0f) survival.RepairSuit(suitRepairPatch);
                }
            }

            // 5. Show HUD debriefing / transmission log
            var hud = UnityEngine.Object.FindFirstObjectByType<AstronautHelmetHUD>();
            if (hud != null)
            {
                string title = $"TELEMETRY RECOVERED // LOOP #{corpseIteration}";
                string status = "FLIGHT RECORDER TELEMETRY SECURED.\nRETURN TO AIRLOCK [121.5 MHz] TO CONCLUDE EXPEDITION.\nBACKUP O2 TANK SALVAGED (+25%).";
                hud.ShowMissionTransmission(title, status, 5.5f);
            }

            // 6. Silence and permanently destroy the distress beacon on the corpse so radio tracker loses it
            SilenceAndDestroyBeacon();

            Debug.Log($"<color=#55FF55><b>[Corpse Recovery]</b></color> Collected mission data from corpse (Loop #{corpseIteration}, Day {corpseLoopDay}).");
        }

        private IEnumerator RoutineCorpseJolt(GameObject player)
        {
            // A. Electrical Spark / Pop Sound
            AudioClip sparkClip = customSparkJoltClip != null ? customSparkJoltClip : GetProceduralSparkJoltClip();
            PlayOneShotAudio(sparkClip, 1.0f);

            // B. Helmet Visor Stark White Flash & CRT Glitch (Chiaroscuro Noir Flash)
            var hud = UnityEngine.Object.FindFirstObjectByType<AstronautHelmetHUD>();
            if (hud != null)
            {
                hud.TriggerNoirFlash(0.65f);
                hud.TriggerVisorGlitch(0.40f);
            }

            // C. Concussive Visor Shock / Camera Trauma Recoil
            var cameraRig = UnityEngine.Object.FindFirstObjectByType<AstronautCameraRig>();
            if (cameraRig != null)
            {
                cameraRig.AddTrauma(0.55f);
            }

            // D. Strobe Flashlight on Corpse Helmet with Stark Hard Shadows
            if (corpseHelmetLight != null)
            {
                corpseHelmetLight.shadows = LightShadows.Hard;
                corpseHelmetLight.enabled = true;
                corpseHelmetLight.intensity = 22f;
                yield return new WaitForSeconds(0.12f);

                corpseHelmetLight.intensity = 2.0f;
                yield return new WaitForSeconds(0.06f);

                corpseHelmetLight.intensity = 16f;
                yield return new WaitForSeconds(0.18f);

                corpseHelmetLight.intensity = 0f;
                corpseHelmetLight.enabled = false;
            }

            // E. Notify HorrorEventManager of Event 1 completion
            if (HorrorEventManager.Instance != null)
            {
                HorrorEventManager.Instance.NotifyCorpseEventCompleted();
            }

            joltCoroutine = null;
        }

        /// <summary>
        /// Permanently shuts down, disables, and destroys the corpse's radio beacon.
        /// Unregisters from ActiveBeacons so it completely disappears from the radio scanner,
        /// and retunes the astronaut's signal tracker to the Home Airlock (121.5 MHz).
        /// </summary>
        private void SilenceAndDestroyBeacon()
        {
            broadcastDistressSignal = false;

            // Gather all SignalBeacons on this corpse (including children or dynamic components)
            SignalBeacon[] corpseBeacons = GetComponentsInChildren<SignalBeacon>(true);
            for (int i = 0; i < corpseBeacons.Length; i++)
            {
                if (corpseBeacons[i] != null)
                {
                    corpseBeacons[i].SetTransmitting(false);
                    corpseBeacons[i].enabled = false;
                    if (SignalBeacon.ActiveBeacons.Contains(corpseBeacons[i]))
                    {
                        SignalBeacon.ActiveBeacons.Remove(corpseBeacons[i]);
                    }
#if UNITY_EDITOR
                    if (Application.isPlaying)
                    {
                        Destroy(corpseBeacons[i]);
                    }
                    else
                    {
                        DestroyImmediate(corpseBeacons[i]);
                    }
#else
                    Destroy(corpseBeacons[i]);
#endif
                }
            }
            beacon = null;

            // Silence bio-pulse heartbeat audio
            if (bioPulseAudioSource != null && bioPulseAudioSource.isPlaying)
            {
                bioPulseAudioSource.Stop();
            }

            // Retune astronaut signal tracker away from the dead corpse frequency to the Airlock (121.5 MHz)
            var tracker = FindFirstObjectByType<AstronautSignalTracker>();
            if (tracker != null)
            {
                tracker.TuneToFrequency(121.5f);
            }

            Debug.Log($"<color=#55FF55><b>[Corpse Recovery]</b></color> Corpse Beacon (128.0 MHz) permanently silenced and removed from radio.");
        }

        private void PlayOneShotAudio(AudioClip clip, float volume = 1.0f)
        {
            if (corpseAudioSource != null && clip != null)
            {
                corpseAudioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
                corpseAudioSource.PlayOneShot(clip, volume);
            }
        }

        #region Procedural Audio Generators
        private static AudioClip GetProceduralBioPulseClip()
        {
            if (proceduralBioPulseClip != null) return proceduralBioPulseClip;

            int sampleRate = 44100;
            float duration = 1.5f; // 40 BPM = 1 beat every 1.5s
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float beatT = t < 0.22f ? t : (t >= 0.32f && t < 0.50f ? t - 0.32f : -1f);

                float sample = 0f;
                if (beatT >= 0f)
                {
                    float env = Mathf.Exp(-beatT * 18f);
                    float freq = Mathf.Lerp(62f, 42f, beatT / 0.22f);
                    sample = Mathf.Sin(2f * Mathf.PI * freq * beatT) * env * 0.75f;
                }

                // Faint radio static hiss in the background
                float hiss = (UnityEngine.Random.value * 2f - 1f) * 0.025f;
                samples[i] = sample + hiss;
            }

            proceduralBioPulseClip = AudioClip.Create("Procedural_Corpse_BioPulse_40BPM", totalSamples, 1, sampleRate, false);
            proceduralBioPulseClip.SetData(samples, 0);
            return proceduralBioPulseClip;
        }

        private static AudioClip GetProceduralSuitCreakClip()
        {
            if (proceduralSuitCreakClip != null) return proceduralSuitCreakClip;

            int sampleRate = 44100;
            float duration = 0.55f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);

                // Modulated friction creak
                float mod = Mathf.Sin(2f * Mathf.PI * 28f * t);
                float carrier = Mathf.Sin(2f * Mathf.PI * (210f + mod * 45f) * t);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.15f;

                samples[i] = (carrier * 0.65f + noise * 0.35f) * env * 0.6f;
            }

            proceduralSuitCreakClip = AudioClip.Create("Procedural_Suit_JointCreak", totalSamples, 1, sampleRate, false);
            proceduralSuitCreakClip.SetData(samples, 0);
            return proceduralSuitCreakClip;
        }

        private static AudioClip GetProceduralSparkJoltClip()
        {
            if (proceduralSparkJoltClip != null) return proceduralSparkJoltClip;

            int sampleRate = 44100;
            float duration = 0.22f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 22f);

                // Sharp electrical snap
                float freq = Mathf.Lerp(1800f, 140f, t / duration);
                float sine = Mathf.Sin(2f * Mathf.PI * freq * t);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.65f;

                samples[i] = (sine * 0.5f + noise * 0.5f) * env;
            }

            proceduralSparkJoltClip = AudioClip.Create("Procedural_Electrical_SparkSnap", totalSamples, 1, sampleRate, false);
            proceduralSparkJoltClip.SetData(samples, 0);
            return proceduralSparkJoltClip;
        }
        #endregion

        #region Transform Hierarchy Helpers
        private static Transform FindChildRecursive(Transform parent, string targetName)
        {
            if (parent == null) return null;
            if (parent.name.Equals(targetName, StringComparison.OrdinalIgnoreCase)) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildRecursive(parent.GetChild(i), targetName);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindChildFuzzy(Transform parent, string keyword)
        {
            if (parent == null) return null;
            if (parent.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildFuzzy(parent.GetChild(i), keyword);
                if (found != null) return found;
            }
            return null;
        }
        #endregion

        #region Editor Context Menu Debugging
        [ContextMenu("Debug: Simulate 60% Progress (Head Turn)")]
        public void DebugSimulateHeadTurn()
        {
            IsUnsettledCorpseEvent = true;
            HandleHoldProgress(0.60f);
            Debug.Log("[Debug] Simulated 60% hold progress on Event 1 corpse.");
        }

        [ContextMenu("Debug: Simulate 100% Complete (The Jolt)")]
        public void DebugSimulateJolt()
        {
            IsUnsettledCorpseEvent = true;
            var survival = FindFirstObjectByType<AstronautSurvival>();
            HandleDataCollected(survival != null ? survival.gameObject : gameObject);
        }
        #endregion
    }
}
