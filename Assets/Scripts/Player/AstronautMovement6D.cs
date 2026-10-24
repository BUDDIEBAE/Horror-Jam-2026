using System;
using UnityEngine;

namespace SpaceHorror.Player
{
    /// <summary>
    /// Core 6 Degrees of Freedom (6DoF) Rigidbody Physics Controller for Zero-G Astronaut movement.
    /// Handles 3-axis translational thrust (Surge, Sway, Heave) and 3-axis rotational thrust (Pitch, Yaw, Roll).
    /// Features active RCS inertia stabilization, emergency braking, and physical collision impacts.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AstronautInputHandler))]
    public class AstronautMovement6D : MonoBehaviour
    {
        [Header("Thrust Forces (Translation)")]
        [Tooltip("Main forward/backward thruster force")]
        [SerializeField] private float forwardThrust = 16f;

        [Tooltip("Lateral strafe thruster force (A / D)")]
        [SerializeField] private float strafeThrust = 13f;

        [Tooltip("Vertical heave thruster force (Space / Ctrl)")]
        [SerializeField] private float verticalThrust = 13f;

        [Tooltip("Thrust multiplier when holding Left Shift")]
        [SerializeField] private float boostMultiplier = 2.0f;

        [Tooltip("Maximum cruising velocity in m/s")]
        [SerializeField] private float maxSpeed = 10f;

        [Tooltip("Maximum velocity when boosting in m/s")]
        [SerializeField] private float maxBoostSpeed = 20f;

        [Header("Rotational Thrusters (Orientation)")]
        [Tooltip("Sensitivity of Pitch & Yaw rotation")]
        [SerializeField] private float turnSpeed = 45f;

        [Tooltip("Speed of Roll rotation (Q / E) in degrees/sec")]
        [SerializeField] private float rollSpeed = 60f;

        [Tooltip("How quickly angular velocity reacts to mouse/roll inputs")]
        [SerializeField] private float rotationResponsiveness = 14f;

        [Header("RCS & Inertia Stabilization")]
        [Tooltip("Enable automatic counter-thrust to damp drifting when no move keys are pressed")]
        [SerializeField] private bool rcsStabilizationActive = true;

        [Tooltip("How fast linear velocity slows down automatically when drifting")]
        [SerializeField] private float linearDampingRate = 2.0f;

        [Tooltip("How fast residual spin slows down when no rotation input is given")]
        [SerializeField] private float angularDampingRate = 4.0f;

        [Tooltip("Emergency brake deceleration multiplier when holding X")]
        [SerializeField] private float emergencyBrakePower = 8.0f;

        [Header("Collision & Impact")]
        [Tooltip("Minimum impact velocity magnitude to trigger audio/visual horror reaction")]
        [SerializeField] private float impactThreshold = 1.5f;

        // References
        private Rigidbody rb;
        private AstronautInputHandler inputHandler;

        // Public Events for Audio / FX / HUD
        public event Action<Vector3, bool> OnThrustFired; // direction, isBoosting
        public event Action OnEmergencyBrake;
        public event Action<bool> OnStabilizerToggled;
        public event Action<float, Vector3> OnSuitImpact; // impactMagnitude, contactPoint

        // Public State Getters
        public bool IsRcsActive => rcsStabilizationActive;
        public Vector3 CurrentVelocity => rb.linearVelocity;
        public float CurrentSpeed => rb.linearVelocity.magnitude;
        public float MaxSpeed => inputHandler != null && inputHandler.IsBoosting ? maxBoostSpeed : maxSpeed;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            inputHandler = GetComponent<AstronautInputHandler>();

            ConfigureRigidbody();
        }

        private void ConfigureRigidbody()
        {
            // Set up realistic zero-gravity physics body
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Zero-G vacuum has no atmospheric drag; all damping is handled by RCS thrusters
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.1f;
        }

        private void Update()
        {
            // Toggle RCS Stabilization mode
            if (inputHandler.ToggleStabilizerPressed)
            {
                rcsStabilizationActive = !rcsStabilizationActive;
                OnStabilizerToggled?.Invoke(rcsStabilizationActive);
            }
        }

        private void FixedUpdate()
        {
            HandleRotation();
            HandleTranslation();
        }

        private void HandleTranslation()
        {
            Vector3 input = inputHandler.MovementInput;
            bool isBoosting = inputHandler.IsBoosting;
            bool isBraking = inputHandler.IsBraking;

            float currentMaxSpeed = isBoosting ? maxBoostSpeed : maxSpeed;

            if (isBraking)
            {
                // Emergency Brake: Active counter-thrust against linear momentum
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * emergencyBrakePower);
                OnEmergencyBrake?.Invoke();
                return;
            }

            if (input.sqrMagnitude > 0.01f)
            {
                // Calculate directional thrust along astronaut's local axes
                float multiplier = isBoosting ? boostMultiplier : 1.0f;
                Vector3 localThrust = new Vector3(
                    input.x * strafeThrust * multiplier,
                    input.y * verticalThrust * multiplier,
                    input.z * forwardThrust * multiplier
                );

                Vector3 worldThrust = transform.TransformDirection(localThrust);
                rb.AddForce(worldThrust, ForceMode.Force);

                // Notify thruster audio / particle systems
                OnThrustFired?.Invoke(input, isBoosting);
            }
            else if (rcsStabilizationActive)
            {
                // Active RCS: automatically apply gentle counter-thrust when no movement input is pressed
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * linearDampingRate);
            }

            // Clamp max velocity
            if (rb.linearVelocity.magnitude > currentMaxSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * currentMaxSpeed;
            }
        }

        private void HandleRotation()
        {
            Vector3 rotInput = inputHandler.RotationInput;
            bool isBraking = inputHandler.IsBraking;

            if (isBraking)
            {
                // Dampen any residual spin
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, Time.fixedDeltaTime * emergencyBrakePower);
                return;
            }

            // Convert pitch, yaw, roll into angular velocity vector in world space
            float pitchRad = rotInput.x * turnSpeed * Mathf.Deg2Rad;
            float yawRad = rotInput.y * turnSpeed * Mathf.Deg2Rad;
            float rollRad = rotInput.z * rollSpeed * Mathf.Deg2Rad;

            Vector3 desiredLocalAngularVelocity = new Vector3(pitchRad, yawRad, rollRad);
            Vector3 desiredWorldAngularVelocity = transform.TransformDirection(desiredLocalAngularVelocity);

            if (rotInput.sqrMagnitude > 0.001f)
            {
                // Smoothly steer angular velocity towards input direction
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, desiredWorldAngularVelocity, Time.fixedDeltaTime * rotationResponsiveness);
            }
            else if (rcsStabilizationActive)
            {
                // Dampen spin when player is not actively looking/rolling
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, Time.fixedDeltaTime * angularDampingRate);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            float impactMagnitude = collision.relativeVelocity.magnitude;
            if (impactMagnitude >= impactThreshold)
            {
                Vector3 contactPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
                OnSuitImpact?.Invoke(impactMagnitude, contactPoint);
            }
        }

        /// <summary>
        /// External impulse (e.g. explosion, entity strike, tether snap).
        /// </summary>
        public void ApplyExternalImpulse(Vector3 impulse, Vector3 angularImpulse)
        {
            rb.AddForce(impulse, ForceMode.Impulse);
            rb.AddTorque(angularImpulse, ForceMode.Impulse);
        }
    }
}
