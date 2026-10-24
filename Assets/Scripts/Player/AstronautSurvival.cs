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

        // Internal States
        private bool wasCriticalAlarmActive = false;
        private bool isAsphyxiated = false;
        private float acuteStressSpike = 0f;

        // Public Events for HUD, Audio & Gameplay
        public event Action<float> OnOxygenChanged;             // 0.0 to 1.0 (normalized)
        public event Action<float> OnHeartRateChanged;          // Raw BPM
        public event Action<float> OnSuitIntegrityChanged;      // 0.0 to 1.0 (normalized)
        public event Action<bool> OnLowOxygenWarning;           // isCritical (true when < 25%)
        public event Action OnPlayerAsphyxiated;                // Death event (0% O2)
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

        private void Awake()
        {
            movementController = GetComponent<AstronautMovement6D>();
            inputHandler = GetComponent<AstronautInputHandler>();
            cameraRig = GetComponentInChildren<AstronautCameraRig>();

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
                isAsphyxiated = true;
                currentOxygen = 0f;
                OnPlayerAsphyxiated?.Invoke();
                Debug.LogWarning("<color=#FF3344><b>[Life Support Alert]</b></color> Astronaut oxygen fully depleted! Life support offline.");
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
        /// Spikes astronaut heart rate instantly (e.g. cosmic horror jump scare, anomaly detection).
        /// </summary>
        public void TriggerHeartSpike(float bpmSpike)
        {
            acuteStressSpike = Mathf.Clamp(acuteStressSpike + bpmSpike, 0f, 80f);
        }
    }
}
