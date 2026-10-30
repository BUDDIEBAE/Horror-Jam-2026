using UnityEngine;

namespace SpaceHorror.Player
{
    /// <summary>
    /// Immersive First-Person Camera Rig for EVA Astronaut Helmet.
    /// Provides rotational inertia lag, dynamic strafe/heave banking, acceleration inertia,
    /// breathing micro-bob, and collision impact camera tremors.
    /// </summary>
    public class AstronautCameraRig : MonoBehaviour
    {
        [Header("Mount & Target")]
        [Tooltip("The main Astronaut Rigidbody/Transform root")]
        [SerializeField] private AstronautMovement6D movementController;

        [Tooltip("The camera transform to manipulate (if null, uses this transform)")]
        [SerializeField] private Transform cameraTransform;

        [Header("Inertia & Lag")]
        [Tooltip("How smoothly the camera orientation tracks the suit body (lower = heavier suit feel)")]
        [SerializeField] private float rotationTrackingSpeed = 22f;

        [Tooltip("Positional tracking smoothness")]
        [SerializeField] private float positionTrackingSpeed = 35f;

        [Header("Dynamic Helmet Tilt (Horror Immersion)")]
        [Tooltip("Bank angle (Roll) when strafing with A / D")]
        [SerializeField] private float strafeRollAngle = 4.0f;

        [Tooltip("Nudge angle (Pitch) when heaving up/down with Space / Ctrl")]
        [SerializeField] private float heavePitchAngle = 3.0f;

        [Tooltip("Camera push back/forward on acceleration")]
        [SerializeField] private float accelerationInertiaAmount = 0.08f;

        [Header("Breathing Micro-Sway")]
        [Tooltip("Enable subtle floating sway from astronaut breathing")]
        [SerializeField] private bool enableBreathingSway = true;

        [SerializeField] private float breathingSpeed = 1.2f;
        [SerializeField] private float breathingSwayAmount = 0.015f;

        [Header("Impact Trauma & Shake")]
        [SerializeField] private float maxShakeAngle = 8f;
        [SerializeField] private float shakeDecay = 4.5f;

        // Runtime states
        private Vector3 initialLocalPos;
        private Quaternion initialLocalRot;
        private Vector3 currentVelocityPos;
        private float currentShakeTrauma = 0f;
        private Vector3 smoothedAcceleration;
        private Vector3 lastFixedVelocity;
        private float breathingTimer = 0f;

        public void Initialize(AstronautMovement6D movement, Transform cam)
        {
            movementController = movement;
            cameraTransform = cam;
        }

        private void Awake()
        {
            if (cameraTransform == null)
            {
                cameraTransform = transform;
            }

            initialLocalPos = cameraTransform.localPosition;
            initialLocalRot = cameraTransform.localRotation;

            if (movementController == null)
            {
                movementController = GetComponentInParent<AstronautMovement6D>();
            }
        }

        private void OnEnable()
        {
            if (movementController != null)
            {
                movementController.OnSuitImpact += HandleImpact;
            }
        }

        private void OnDisable()
        {
            if (movementController != null)
            {
                movementController.OnSuitImpact -= HandleImpact;
            }
        }

        private void FixedUpdate()
        {
            if (movementController == null) return;

            // Compute physics-rate acceleration smoothly to prevent render-frame oscillation
            Vector3 currentVel = movementController.CurrentVelocity;
            Vector3 rawAcceleration = (currentVel - lastFixedVelocity) / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            lastFixedVelocity = currentVel;

            smoothedAcceleration = Vector3.Lerp(smoothedAcceleration, rawAcceleration, Time.fixedDeltaTime * 12f);
        }

        private void Update()
        {
            if (movementController == null) return;

            UpdateDynamicTiltAndBreathing();
            UpdateShake();
        }

        [Header("Cinematic & External Overrides")]
        private Vector3 externalEulerOffset = Vector3.zero;

        /// <summary>
        /// Sets an external rotational offset (e.g. for waking-up chin drop or cutscenes).
        /// Blends smoothly with dynamic tilts and trauma shake.
        /// </summary>
        public void SetExternalEulerOffset(Vector3 offset)
        {
            externalEulerOffset = offset;
        }

        public Vector3 GetExternalEulerOffset()
        {
            return externalEulerOffset;
        }

        public Vector3 ExternalEulerOffset
        {
            get => externalEulerOffset;
            set => externalEulerOffset = value;
        }

        private void UpdateDynamicTiltAndBreathing()
        {
            // Calculate local velocity to determine strafe/heave/forward forces
            Vector3 localVelocity = movementController.transform.InverseTransformDirection(movementController.CurrentVelocity);
            Vector3 localAccel = movementController.transform.InverseTransformDirection(smoothedAcceleration);

            // 1. Dynamic Tilts
            float targetRoll = -Mathf.Clamp(localVelocity.x / 10f, -1f, 1f) * strafeRollAngle;
            float targetPitch = -Mathf.Clamp(localVelocity.y / 10f, -1f, 1f) * heavePitchAngle;

            // 2. Breathing Micro-Bob
            Vector3 breathingOffset = Vector3.zero;
            if (enableBreathingSway)
            {
                breathingTimer += Time.deltaTime * breathingSpeed;
                float breathY = Mathf.Sin(breathingTimer) * breathingSwayAmount;
                float breathX = Mathf.Cos(breathingTimer * 0.5f) * (breathingSwayAmount * 0.5f);
                breathingOffset = new Vector3(breathX, breathY, 0f);
            }

            // 3. Positional Inertia (suit moves slightly opposite to acceleration)
            Vector3 inertiaPosOffset = -Vector3.ClampMagnitude(localAccel * 0.005f, accelerationInertiaAmount);
            Vector3 targetLocalPos = initialLocalPos + breathingOffset + inertiaPosOffset;

            cameraTransform.localPosition = Vector3.SmoothDamp(
                cameraTransform.localPosition,
                targetLocalPos,
                ref currentVelocityPos,
                1f / positionTrackingSpeed
            );

            // 4. Combine Rotations (Initial + Dynamic Tilts + Shake + External Offset)
            Quaternion dynamicTilt = Quaternion.Euler(targetPitch, 0f, targetRoll);
            Quaternion shakeRot = GetShakeRotation();
            Quaternion externalRot = Quaternion.Euler(externalEulerOffset);

            Quaternion targetLocalRot = initialLocalRot * dynamicTilt * shakeRot * externalRot;
            cameraTransform.localRotation = Quaternion.Slerp(
                cameraTransform.localRotation,
                targetLocalRot,
                Time.deltaTime * rotationTrackingSpeed
            );
        }

        private void UpdateShake()
        {
            if (currentShakeTrauma > 0f)
            {
                currentShakeTrauma = Mathf.MoveTowards(currentShakeTrauma, 0f, Time.deltaTime * shakeDecay);
            }
        }

        private Quaternion GetShakeRotation()
        {
            if (currentShakeTrauma <= 0.001f) return Quaternion.identity;

            float traumaSq = currentShakeTrauma * currentShakeTrauma;
            float pitchShake = (Mathf.PerlinNoise(Time.time * 25f, 0f) * 2f - 1f) * maxShakeAngle * traumaSq;
            float yawShake = (Mathf.PerlinNoise(0f, Time.time * 25f) * 2f - 1f) * maxShakeAngle * traumaSq;
            float rollShake = (Mathf.PerlinNoise(Time.time * 25f, Time.time * 25f) * 2f - 1f) * maxShakeAngle * traumaSq;

            return Quaternion.Euler(pitchShake, yawShake, rollShake);
        }

        private void HandleImpact(float impactMagnitude, Vector3 contactPoint)
        {
            // Add shake trauma proportional to impact
            float traumaToAdd = Mathf.Clamp01(impactMagnitude / 12f);
            currentShakeTrauma = Mathf.Clamp01(currentShakeTrauma + traumaToAdd);
        }

        /// <summary>
        /// Manually trigger shake trauma (e.g. for explosions, horror jumpscare vibrations).
        /// </summary>
        public void AddTrauma(float amount)
        {
            currentShakeTrauma = Mathf.Clamp01(currentShakeTrauma + amount);
        }

        private float baseBreathingSpeed = 1.2f;

        /// <summary>
        /// Scales breathing sway speed based on heart rate / physical exertion from survival system.
        /// </summary>
        public void SetBreathingRateMultiplier(float multiplier)
        {
            if (baseBreathingSpeed <= 0f) baseBreathingSpeed = breathingSpeed;
            breathingSpeed = baseBreathingSpeed * Mathf.Max(0.5f, multiplier);
        }
    }
}
