using System;
using UnityEngine;

namespace SpaceHorror.Player
{
    /// <summary>
    /// Core Survival & Biology Simulation for 6DoF EVA Astronaut in deep space.
    /// Simulates Oxygen tank depletion (accelerated by thruster boost & physical exertion),
    /// Suit Hull Integrity & puncture leaks from high-velocity debris collisions,
    /// and dynamic Heart Rate (BPM) influencing camera sway and respiratory audio.
    /// </summary>
    [RequireComponent(typeof(AstronautMovement6D))]
    [RequireComponent(typeof(AstronautInputHandler))]
    public class AstronautSurvival : MonoBehaviour
    {
        [Header("Oxygen Tank (Life Support)")]
        [Tooltip("Maximum oxygen capacity in percentage (100 = full tank)")]
        [SerializeField] private float maxOxygen = 100f;
        [SerializeField] private float currentOxygen = 100f;

        [Tooltip("Base consumption rate in % per second when floating stationary (~150s base supply)")]
        [SerializeField] private float baseOxygenDrainRate = 0.65f;

        [Tooltip("Oxygen drain multiplier when cruising above half speed")]
        [SerializeField] private float movingDrainMultiplier = 1.3f;

        [Tooltip("Oxygen drain multiplier when boosting with Left Shift (physical exertion & adrenaline)")]
        [SerializeField] private float boostDrainMultiplier = 2.5f;

        [Tooltip("Oxygen percentage threshold to trigger critical suffocation alarm")]
        [Range(10f, 40f)]
        [SerializeField] private float criticalOxygenThreshold = 25f;

        [Header("Death & Corpse Simulation")]
        [Tooltip("Corpse prefab instantiated when astronaut asphyxiates / runs out of oxygen")]
        [SerializeField] private GameObject corpsePrefab;

        [Tooltip("Inherit player linear and angular velocity onto the corpse in Zero-G")]
        [SerializeField] private bool inheritVelocityOnDeath = true;

        [Tooltip("Optional subtle tumble impulse (angular velocity) added to corpse upon death in zero-g")]
        [SerializeField] private float deathTumbleImpulse = 0.25f;

        [Tooltip("Ensure spawned corpses persist permanently in space across all loop cycles and scene reloads (Loop horror aesthetic)")]
        [SerializeField] private bool persistCorpsesForever = true;

        [Header("Suit Integrity & Debris Damage")]
        [Tooltip("Current suit pressure / hull integrity (100 = perfectly sealed)")]
        [SerializeField] private float currentSuitIntegrity = 100f;

        [Tooltip("Minimum collision velocity (m/s) with space debris to puncture the suit")]
        [SerializeField] private float punctureThresholdVelocity = 3.5f;

        [Tooltip("Suit damage amount per m/s velocity above the threshold")]
        [SerializeField] private float damagePerVelocityUnit = 6.0f;

        [Tooltip("Additional oxygen leak rate (%/sec) for each 10% of suit damage")]
        [SerializeField] private float punctureLeakPerDamageTier = 0.35f;

        [Header("Dynamic Heart Rate (BPM)")]
        [Tooltip("Resting heart rate in beats per minute")]
        [SerializeField] private float restingBPM = 72f;

        [Tooltip("Maximum panicked heart rate in beats per minute")]
        [SerializeField] private float maxBPM = 180f;

        [SerializeField] private float currentBPM = 72f;
        [SerializeField] private float bpmTransitionSpeed = 2.5f;

        // References
        private AstronautMovement6D movementController;
        private AstronautInputHandler inputHandler;
        private AstronautCameraRig cameraRig;
        private Rigidbody playerRigidbody;

        // Internal States
        private bool wasCriticalAlarmActive = false;
        private bool isAsphyxiated = false;
        private float acuteStressSpike = 0f;
        private GameObject lastSpawnedCorpse;

        // Public Events for HUD, Audio & Gameplay
        public event Action<float> OnOxygenChanged;             // 0.0 to 1.0 (normalized)
        public event Action<float> OnHeartRateChanged;          // Raw BPM
        public event Action<float> OnSuitIntegrityChanged;      // 0.0 to 1.0 (normalized)
        public event Action<bool> OnLowOxygenWarning;           // isCritical (true when < 25%)
        public event Action OnPlayerAsphyxiated;                // Death event (0% O2)
        public event Action<GameObject> OnPlayerCorpseSpawned;  // Corpse instantiated event
        public event Action<float> OnSuitPunctured;             // Damage amount taken

        // Public Getters
        public float CurrentOxygenNormalized => Mathf.Clamp01(currentOxygen / maxOxygen);
        public float CurrentOxygenPercent => currentOxygen;
        public float MaxOxygen => maxOxygen;
        public float CurrentHeartRate => currentBPM;
        public float CurrentSuitIntegrityNormalized => Mathf.Clamp01(currentSuitIntegrity / 100f);
        public float CurrentSuitIntegrityPercent => currentSuitIntegrity;
        public bool IsOxygenCritical => (currentOxygen / maxOxygen) <= (criticalOxygenThreshold / 100f);
        public bool IsAsphyxiated => isAsphyxiated;
        public bool HasSuitPuncture => currentSuitIntegrity < 99.9f;
        public GameObject CorpsePrefab { get => corpsePrefab; set => corpsePrefab = value; }
        public GameObject LastSpawnedCorpse => lastSpawnedCorpse;
        public bool PersistCorpsesForever { get => persistCorpsesForever; set => persistCorpsesForever = value; }

        private static readonly System.Collections.Generic.List<GameObject> allPersistentCorpses = new System.Collections.Generic.List<GameObject>();
        public static System.Collections.Generic.IReadOnlyList<GameObject> AllPersistentCorpses => allPersistentCorpses;
        public static int TotalCorpsesDriftingInSpace => allPersistentCorpses.Count;

#if UNITY_EDITOR
        private void Reset()
        {
            if (corpsePrefab == null)
            {
                corpsePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Astronaut Dead Body.prefab");
            }
        }
#endif

        private void Awake()
        {
            movementController = GetComponent<AstronautMovement6D>();
            inputHandler = GetComponent<AstronautInputHandler>();
            cameraRig = GetComponentInChildren<AstronautCameraRig>();
            playerRigidbody = GetComponent<Rigidbody>();

            currentOxygen = maxOxygen;
            currentSuitIntegrity = 100f;
            currentBPM = restingBPM;
        }

        private void OnEnable()
        {
            if (movementController != null)
            {
                movementController.OnSuitImpact += HandleSuitImpact;
            }
        }

        private void OnDisable()
        {
            if (movementController != null)
            {
                movementController.OnSuitImpact -= HandleSuitImpact;
            }
        }

        private void Start()
        {
            // Initial notification to listeners (HUD & Audio)
            OnOxygenChanged?.Invoke(CurrentOxygenNormalized);
            OnHeartRateChanged?.Invoke(currentBPM);
            OnSuitIntegrityChanged?.Invoke(CurrentSuitIntegrityNormalized);
        }

        private void Update()
        {
            if (isAsphyxiated) return;

            UpdateOxygen();
            UpdateHeartRate();
        }

        private void UpdateOxygen()
        {
            // 1. Calculate consumption multiplier based on player action
            float multiplier = 1.0f;

            if (inputHandler != null && inputHandler.IsBoosting)
            {
                // Heavy thruster burn & G-force exertion
                multiplier = boostDrainMultiplier;
            }
            else if (movementController != null && movementController.CurrentSpeed > 3.0f)
            {
                // Active maneuvering in Zero-G
                multiplier = movingDrainMultiplier;
            }

            // 2. Extra leakage if suit was punctured from debris collision
            float damageAmount = Mathf.Max(0f, 100f - currentSuitIntegrity);
            float punctureLeakRate = (damageAmount / 10f) * punctureLeakPerDamageTier;

            // 3. Drain oxygen
            float totalDrainThisFrame = (baseOxygenDrainRate * multiplier + punctureLeakRate) * Time.deltaTime;
            currentOxygen = Mathf.Max(0f, currentOxygen - totalDrainThisFrame);

            OnOxygenChanged?.Invoke(CurrentOxygenNormalized);

            // 4. Check Suffocation Warnings
            bool isNowCritical = IsOxygenCritical;
            if (isNowCritical != wasCriticalAlarmActive)
            {
                wasCriticalAlarmActive = isNowCritical;
                OnLowOxygenWarning?.Invoke(isNowCritical);
            }

            // 5. Total Asphyxiation Check
            if (currentOxygen <= 0f && !isAsphyxiated)
            {
                TriggerAsphyxiation();
            }
        }

        private void UpdateHeartRate()
        {
            // 1. Calculate Target Heart Rate based on exertion, oxygen starvation & shock
            float targetBPM = restingBPM;

            // Exertion from boosting or fast translation
            if (inputHandler != null && inputHandler.IsBoosting)
            {
                targetBPM += 50f;
            }
            else if (movementController != null && movementController.CurrentSpeed > 2.5f)
            {
                targetBPM += 18f;
            }

            // Suffocation panic as oxygen depletes below critical threshold
            if (IsOxygenCritical)
            {
                float criticalFactor = 1f - (currentOxygen / criticalOxygenThreshold);
                targetBPM += Mathf.Lerp(30f, 75f, criticalFactor);
            }

            // Suit puncture anxiety
            if (HasSuitPuncture)
            {
                targetBPM += 15f;
            }

            // Acute shock trauma spike (from collisions or horror jumpscares)
            targetBPM += acuteStressSpike;
            acuteStressSpike = Mathf.MoveTowards(acuteStressSpike, 0f, Time.deltaTime * 18f);

            // Clamp target BPM
            targetBPM = Mathf.Clamp(targetBPM, restingBPM, maxBPM);

            // 2. Smoothly transition current BPM
            currentBPM = Mathf.Lerp(currentBPM, targetBPM, Time.deltaTime * bpmTransitionSpeed);
            OnHeartRateChanged?.Invoke(currentBPM);

            // 3. Link heart rate to Camera Breathing Sway frequency
            if (cameraRig != null)
            {
                float breathingRateMultiplier = currentBPM / restingBPM;
                cameraRig.SetBreathingRateMultiplier(breathingRateMultiplier);
            }
        }

        private void HandleSuitImpact(float impactMagnitude, Vector3 contactPoint)
        {
            if (impactMagnitude >= punctureThresholdVelocity)
            {
                float excessSpeed = impactMagnitude - punctureThresholdVelocity;
                float damage = Mathf.Clamp(excessSpeed * damagePerVelocityUnit, 5f, 40f);

                currentSuitIntegrity = Mathf.Max(0f, currentSuitIntegrity - damage);
                OnSuitIntegrityChanged?.Invoke(CurrentSuitIntegrityNormalized);
                OnSuitPunctured?.Invoke(damage);

                // Add stress spike from physical trauma
                TriggerHeartSpike(Mathf.Min(35f, damage * 1.5f));

                Debug.LogWarning($"<color=#FF9900><b>[Suit Breach Warning]</b></color> Impact velocity {impactMagnitude:F1} m/s caused {damage:F1}% suit damage! Oxygen leaking.");
            }
            else
            {
                // Minor bump: small heart flutter
                TriggerHeartSpike(10f);
            }
        }

        /// <summary>
        /// Refills astronaut oxygen supply (e.g. from space station airlock or portable O2 canister).
        /// </summary>
        public void ReplenishOxygen(float amount)
        {
            currentOxygen = Mathf.Clamp(currentOxygen + amount, 0f, maxOxygen);
            isAsphyxiated = false;
            OnOxygenChanged?.Invoke(CurrentOxygenNormalized);

            if (!IsOxygenCritical && wasCriticalAlarmActive)
            {
                wasCriticalAlarmActive = false;
                OnLowOxygenWarning?.Invoke(false);
            }
        }

        /// <summary>
        /// Seals punctures and restores suit integrity (e.g. using emergency EVA patch tape).
        /// </summary>
        public void RepairSuit(float amount)
        {
            currentSuitIntegrity = Mathf.Clamp(currentSuitIntegrity + amount, 0f, 100f);
            OnSuitIntegrityChanged?.Invoke(CurrentSuitIntegrityNormalized);
        }

        /// <summary>
        /// Scripted event method: Directly sets suit integrity to a specific percentage (e.g. catastrophic rupture).
        /// </summary>
        public void ForceSuitIntegrity(float targetIntegrityPercent, bool triggerSpike = true)
        {
            currentSuitIntegrity = Mathf.Clamp(targetIntegrityPercent, 0f, 100f);
            OnSuitIntegrityChanged?.Invoke(CurrentSuitIntegrityNormalized);
            OnSuitPunctured?.Invoke(Mathf.Max(0f, 100f - currentSuitIntegrity));
            if (triggerSpike)
            {
                TriggerHeartSpike(45f);
            }
        }

        /// <summary>
        /// Spikes astronaut heart rate instantly (e.g. cosmic horror jump scare, anomaly detection).
        /// </summary>
        public void TriggerHeartSpike(float bpmSpike)
        {
            acuteStressSpike = Mathf.Clamp(acuteStressSpike + bpmSpike, 0f, 80f);
        }

        /// <summary>
        /// Forces immediate player asphyxiation death, instantiating the corpse and invoking death events.
        /// </summary>
        public void TriggerAsphyxiation()
        {
            if (isAsphyxiated) return;

            currentOxygen = 0f;
            isAsphyxiated = true;
            OnOxygenChanged?.Invoke(0f);
            SpawnCorpse();
            OnPlayerAsphyxiated?.Invoke();

            if (SpaceHorror.Gameplay.DayCycleManager.Instance != null)
            {
                SpaceHorror.Gameplay.DayCycleManager.Instance.NotifyPlayerAsphyxiated();
            }
            else
            {
                StartCoroutine(EmergencyRespawnFallback());
            }

            Debug.LogWarning("<color=#FF3344><b>[Life Support Alert]</b></color> Astronaut oxygen fully depleted! Life support offline.");
        }

        private System.Collections.IEnumerator EmergencyRespawnFallback()
        {
            yield return new WaitForSeconds(1.5f);
            ReplenishOxygen(maxOxygen);
            RepairSuit(100f);

            GameObject sp = GameObject.Find("Spawn Point");
            if (sp == null) sp = GameObject.Find("Spawnpoint");
            if (sp == null) sp = GameObject.Find("SpawnPoint");

            if (sp != null)
            {
                transform.position = sp.transform.position;
                transform.rotation = sp.transform.rotation;
                if (playerRigidbody != null)
                {
                    playerRigidbody.linearVelocity = Vector3.zero;
                    playerRigidbody.angularVelocity = Vector3.zero;
                    playerRigidbody.position = sp.transform.position;
                    playerRigidbody.rotation = sp.transform.rotation;
                }
                Physics.SyncTransforms();
            }

            var wakeUp = GetComponent<AstronautWakeUpTransition>();
            if (wakeUp != null)
            {
                wakeUp.TriggerWakeUp();
            }
            else
            {
                var input = GetComponent<AstronautInputHandler>();
                if (input != null) input.InputEnabled = true;
            }
        }

        /// <summary>
        /// Instantiates the player's corpse in the world at the current death position and rotation.
        /// The corpse persists permanently in space across all future loops without ever being cleared.
        /// Transfers linear and angular momentum to the corpse if configured.
        /// </summary>
        public GameObject SpawnCorpse()
        {
            if (corpsePrefab == null)
            {
                Debug.LogWarning("<color=#FF3344><b>[AstronautSurvival]</b></color> Corpse Prefab is not assigned! Unable to instantiate player corpse.");
                return null;
            }

            GameObject corpse = Instantiate(corpsePrefab, transform.position, transform.rotation);
            int loopNumber = SpaceHorror.Gameplay.DayCycleManager.Instance != null ? SpaceHorror.Gameplay.DayCycleManager.Instance.CurrentDay : 1;
            int deathCount = allPersistentCorpses.Count + 1;
            corpse.name = $"Astronaut Corpse (Loop Day {loopNumber:D2} - Body #{deathCount})";
            lastSpawnedCorpse = corpse;
            allPersistentCorpses.Add(corpse);

            if (persistCorpsesForever)
            {
                // Ensure corpse is never cleared by Unity scene transitions or loop resets
                DontDestroyOnLoad(corpse);
            }

            Rigidbody corpseRb = corpse.GetComponent<Rigidbody>();
            if (corpseRb != null)
            {
                if (inheritVelocityOnDeath && playerRigidbody != null)
                {
                    corpseRb.linearVelocity = playerRigidbody.linearVelocity;
                    corpseRb.angularVelocity = playerRigidbody.angularVelocity;
                }

                // Apply a subtle zero-g tumble drift so the lifeless body floats eerily in space
                if (deathTumbleImpulse > 0f)
                {
                    Vector3 randomTorque = UnityEngine.Random.insideUnitSphere * deathTumbleImpulse;
                    corpseRb.AddTorque(randomTorque, ForceMode.Impulse);
                }
            }

            // Setup Interactive Corpse Recovery & Telemetry Collection
            try
            {
                var recovery = corpse.GetComponent<SpaceHorror.Interaction.FallenAstronautRecovery>();
                if (recovery == null)
                {
                    recovery = corpse.AddComponent<SpaceHorror.Interaction.FallenAstronautRecovery>();
                }
                if (recovery != null)
                {
                    recovery.Initialize(loopNumber, deathCount);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"<color=#FF5555>[AstronautSurvival]</color> Error initializing corpse recovery: {ex.Message}");
            }

            OnPlayerCorpseSpawned?.Invoke(corpse);
            Debug.Log($"<color=#FF4444><b>[Loop Cycle]</b></color> Persistent corpse spawned in space: '{corpse.name}' at {transform.position}. Total bodies adrift: {allPersistentCorpses.Count}");
            return corpse;
        }
    }
}
