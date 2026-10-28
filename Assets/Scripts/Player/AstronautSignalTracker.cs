using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SpaceHorror.Interaction;

namespace SpaceHorror.Player
{
    /// <summary>
    /// Telemetry data container broadcasted to the Helmet Visor HUD and Audio Synthesizer.
    /// Follows strict Film Noir monochromatic standards: no RGB color flags, relying purely on
    /// signal amplitude, angle alignment, distance, frequency, and lock-on state.
    /// </summary>
    [System.Serializable]
    public struct SignalTelemetry
    {
        public bool hasSignal;
        public string channelBadge;    // e.g. "CH 00 [AUTO-SCAN]" or "CH 01 [142.8 MHz]"
        public int channelIndex;       // 0 = Auto-Scan, 1..N = Specific Channel
        public int totalChannels;      // Total selectable channels
        public string beaconTag;
        public string telemetryTitle;
        public float frequencyMHz;
        public float distance;
        public float facingScore;      // 0.0 to 1.0 (Angular alignment)
        public float distanceScore;    // 0.0 to 1.0 (Proximity score)
        public float totalStrength;    // 0.0 to 1.0 (Combined signal intensity)
        public bool isLockedOn;        // True when helmet forward vector points directly at beacon (< threshold)
        public float angleDegrees;     // Angle between helmet look vector and target vector
    }

    /// <summary>
    /// Directional Radio Signal Tracker for 6DoF Astronaut.
    /// Features Multi-Channel VHF Radio Tuning:
    /// - CH 00: [AUTO-SCAN] Dynamically tracks the closest & highest-priority beacon.
    /// - CH 01..N: Dedicated channels locked onto specific beacons (Objective, Airlock, O2 Stations).
    /// - Controls: Press [R] or Scroll Mouse Wheel to tune between channels!
    /// </summary>
    [DisallowMultipleComponent]
    public class AstronautSignalTracker : MonoBehaviour
    {
        [Header("Camera & Alignment Settings")]
        [Tooltip("Astronaut first-person camera transform. If null, automatically located.")]
        [SerializeField] private Transform helmetCameraTransform;

        [Tooltip("Curvature exponent applied to facing alignment. Higher value tightens the beam (requires more precise aim).")]
        [Range(1f, 4f)]
        [SerializeField] private float facingSharpness = 1.8f;

        [Tooltip("Angle in degrees within which the tracker enters high-contrast Lock-On mode.")]
        [Range(2f, 15f)]
        [SerializeField] private float lockOnThresholdAngle = 6.0f;

        [Header("Distance & Proximity Falloff")]
        [Tooltip("Range in meters within which proximity static sounds even if facing away (Noir blind-spot tension).")]
        [Range(3f, 15f)]
        [SerializeField] private float ambientProximityRange = 8.0f;

        [Tooltip("Maximum baseline ambient score granted when immediately adjacent to a beacon.")]
        [Range(0f, 0.4f)]
        [SerializeField] private float maxAmbientFloor = 0.2f;

        [Header("Meter Responsiveness (Analog Damping)")]
        [Tooltip("Smoothing speed for needle / meter telemetry movement")]
        [Range(2f, 30f)]
        [SerializeField] private float meterSmoothingSpeed = 12f;

        [Header("Radio Channel Tuning")]
        [Tooltip("Current tuned channel index: 0 = AUTO-SCAN, 1..N = Dedicated Beacon Channels")]
        [SerializeField] private int currentChannelIndex = 0;

        // Runtime Tracking State
        private SignalBeacon currentTargetBeacon = null;
        private SignalTelemetry currentTelemetry;
        private float smoothedSignalStrength = 0f;
        private bool wasLockedOn = false;
        private float scrollCooldown = 0f;

        // Public Telemetry Accessors
        public SignalTelemetry CurrentTelemetry => currentTelemetry;
        public float SmoothedStrength => smoothedSignalStrength;
        public SignalBeacon CurrentTarget => currentTargetBeacon;
        public bool IsLockedOn => currentTelemetry.isLockedOn;
        public bool HasActiveSignal => currentTelemetry.hasSignal && smoothedSignalStrength > 0.01f;
        public int CurrentChannelIndex => currentChannelIndex;

        // Events
        public event Action<SignalTelemetry> OnTelemetryUpdated;
        public event Action<bool> OnLockOnChanged;
        public event Action<SignalBeacon> OnTargetChanged;
        public event Action<int, string, float> OnChannelTuned; // (channelIndex, channelBadge, frequencyMHz)

        private void Awake()
        {
            if (helmetCameraTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam == null) cam = Camera.main;
                if (cam != null) helmetCameraTransform = cam.transform;
            }
        }

        private void Update()
        {
            if (helmetCameraTransform == null)
            {
                Camera cam = Camera.main;
                if (cam != null) helmetCameraTransform = cam.transform;
                if (helmetCameraTransform == null) return;
            }

            HandleChannelTuningInput();
            EvaluateActiveBeacons();
            UpdateSmoothing();
        }

        /// <summary>
        /// Handles player keyboard [R] and mouse scroll inputs to tune radio channels.
        /// </summary>
        private void HandleChannelTuningInput()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (scrollCooldown > 0f)
            {
                scrollCooldown -= Time.deltaTime;
            }

            // [R] Key: Cycle channel forward
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                CycleChannel(1);
            }

            // Mouse Scroll Wheel: Tune frequencies up or down
            if (mouse != null && scrollCooldown <= 0f)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.1f)
                {
                    int dir = scroll > 0f ? 1 : -1;
                    CycleChannel(dir);
                    scrollCooldown = 0.18f; // Debounce fast scroll
                }
            }
        }

        /// <summary>
        /// Gets a list of all distinct frequency channels currently transmitting in the scene.
        /// Channel 0 is always AUTO-SCAN.
        /// </summary>
        private List<float> GetDistinctFrequencies()
        {
            List<float> freqs = new List<float>();
            List<SignalBeacon> beacons = SignalBeacon.ActiveBeacons;
            if (beacons != null)
            {
                for (int i = 0; i < beacons.Count; i++)
                {
                    if (beacons[i] != null && beacons[i].IsTransmitting)
                    {
                        float f = beacons[i].BroadcastFrequencyMHz;
                        // Avoid duplicates within 0.05 MHz
                        bool exists = false;
                        for (int j = 0; j < freqs.Count; j++)
                        {
                            if (Mathf.Abs(freqs[j] - f) < 0.05f) { exists = true; break; }
                        }
                        if (!exists) freqs.Add(f);
                    }
                }
            }
            return freqs;
        }

        /// <summary>
        /// Cycles to the next or previous available radio frequency channel.
        /// Channels are grouped by Frequency (e.g. all O2 stations share one channel).
        /// </summary>
        public void CycleChannel(int direction)
        {
            List<float> freqs = GetDistinctFrequencies();
            int totalChannels = 1 + freqs.Count; // 0 is AUTO-SCAN, 1..N are unique frequencies

            if (totalChannels <= 1)
            {
                currentChannelIndex = 0;
            }
            else
            {
                currentChannelIndex = (currentChannelIndex + direction) % totalChannels;
                if (currentChannelIndex < 0) currentChannelIndex += totalChannels;
            }

            string badge;
            float freq = 0f;

            if (currentChannelIndex == 0)
            {
                badge = "CH 00 [AUTO-SCAN]";
            }
            else
            {
                int fIdx = currentChannelIndex - 1;
                freq = freqs[fIdx];

                // Find a representative beacon on this frequency to display its tag
                SignalBeacon rep = null;
                var beacons = SignalBeacon.ActiveBeacons;
                for (int i = 0; i < beacons.Count; i++)
                {
                    if (beacons[i] != null && Mathf.Abs(beacons[i].BroadcastFrequencyMHz - freq) < 0.05f)
                    {
                        rep = beacons[i];
                        break;
                    }
                }

                string tag = rep != null ? rep.BeaconTag : $"{freq:F1} MHz";
                badge = $"CH {currentChannelIndex:D2} [{tag}]";
            }

            OnChannelTuned?.Invoke(currentChannelIndex, badge, freq);
        }

        /// <summary>
        /// Automatically tunes to a specific frequency (e.g. 121.5 MHz for AIRLOCK-HOME) or reverts to AUTO-SCAN if not found.
        /// Useful when an objective or corpse beacon is secured and silenced.
        /// </summary>
        public void TuneToFrequency(float targetFrequencyMHz)
        {
            List<float> freqs = GetDistinctFrequencies();
            for (int i = 0; i < freqs.Count; i++)
            {
                if (Mathf.Abs(freqs[i] - targetFrequencyMHz) < 0.15f)
                {
                    currentChannelIndex = i + 1;
                    CycleChannel(0); // Refresh badge and fire events
                    return;
                }
            }
            // If target frequency is not currently broadcasting, revert cleanly to AUTO-SCAN
            currentChannelIndex = 0;
            CycleChannel(0);
        }

        /// <summary>
        /// Evaluates registered beacons in space and calculates alignment and distance metrics.
        /// </summary>
        private void EvaluateActiveBeacons()
        {
            List<SignalBeacon> beacons = SignalBeacon.ActiveBeacons;
            List<float> distinctFreqs = GetDistinctFrequencies();
            int totalChannels = 1 + distinctFreqs.Count;

            // If a beacon went offline and current tuned channel is now out of range, revert to AUTO-SCAN
            if (currentChannelIndex >= totalChannels)
            {
                currentChannelIndex = 0;
                CycleChannel(0);
            }

            if (beacons == null || beacons.Count == 0)
            {
                ClearTelemetry(totalChannels);
                return;
            }

            Vector3 camPos = helmetCameraTransform.position;
            Vector3 camFwd = helmetCameraTransform.forward;

            SignalBeacon targetBeacon = null;

            // Target frequency if on a specific channel (channelIndex > 0)
            float targetFreq = -1f;
            if (currentChannelIndex > 0 && currentChannelIndex - 1 < distinctFreqs.Count)
            {
                targetFreq = distinctFreqs[currentChannelIndex - 1];
            }

            float bestWeightedSignal = -1f;

            for (int i = 0; i < beacons.Count; i++)
            {
                SignalBeacon beacon = beacons[i];
                if (beacon == null || !beacon.IsTransmitting) continue;

                // If locked onto a specific channel, only consider beacons on this frequency
                if (targetFreq > 0f && Mathf.Abs(beacon.BroadcastFrequencyMHz - targetFreq) >= 0.05f)
                {
                    continue;
                }

                Vector3 toTarget = beacon.Position - camPos;
                float dist = toTarget.magnitude;
                if (dist > beacon.MaxRange && dist > ambientProximityRange) continue;

                Vector3 dir = dist > 0.001f ? toTarget / dist : camFwd;
                float dot = Mathf.Pow(Mathf.Clamp01(Vector3.Dot(camFwd, dir)), facingSharpness);
                float distScore = 1.0f - Mathf.Clamp01(dist / beacon.MaxRange);
                float raw = (dot * distScore);
                float weighted = raw * (1.0f + (beacon.Priority * 0.15f));

                if (weighted > bestWeightedSignal)
                {
                    bestWeightedSignal = weighted;
                    targetBeacon = beacon;
                }
            }

            // Calculate metrics for the selected beacon
            if (targetBeacon != null && targetBeacon.IsTransmitting)
            {
                Vector3 toTarget = targetBeacon.Position - camPos;
                float distance = toTarget.magnitude;
                Vector3 dirToTarget = distance > 0.001f ? toTarget / distance : camFwd;

                float dot = Vector3.Dot(camFwd, dirToTarget);
                float facingScore = Mathf.Clamp01(dot);
                facingScore = Mathf.Pow(facingScore, facingSharpness);

                float distRatio = Mathf.Clamp01(distance / targetBeacon.MaxRange);
                float distanceScore = 1.0f - distRatio;

                float ambientScore = 0f;
                if (distance < ambientProximityRange)
                {
                    float ambRatio = 1.0f - Mathf.Clamp01(distance / ambientProximityRange);
                    ambientScore = ambRatio * maxAmbientFloor;
                }

                float rawSignal = Mathf.Clamp01((facingScore * distanceScore) + ambientScore);
                float angle = Vector3.Angle(camFwd, dirToTarget);
                bool lockOn = (angle <= lockOnThresholdAngle) && (distance <= targetBeacon.MaxRange);

                // Update Telemetry
                if (targetBeacon != currentTargetBeacon)
                {
                    currentTargetBeacon = targetBeacon;
                    OnTargetChanged?.Invoke(currentTargetBeacon);
                }

                currentTelemetry.hasSignal = rawSignal > 0.005f;
                currentTelemetry.channelIndex = currentChannelIndex;
                currentTelemetry.totalChannels = totalChannels;
                currentTelemetry.channelBadge = currentChannelIndex == 0 ? "CH 00 [AUTO-SCAN]" : $"CH {currentChannelIndex:D2} [{targetBeacon.BeaconTag}]";
                currentTelemetry.beaconTag = targetBeacon.BeaconTag;
                currentTelemetry.telemetryTitle = targetBeacon.TelemetryTitle;
                currentTelemetry.frequencyMHz = targetBeacon.BroadcastFrequencyMHz;
                currentTelemetry.distance = distance;
                currentTelemetry.facingScore = facingScore;
                currentTelemetry.distanceScore = distanceScore;
                currentTelemetry.totalStrength = rawSignal;
                currentTelemetry.isLockedOn = lockOn;
                currentTelemetry.angleDegrees = angle;

                if (lockOn != wasLockedOn)
                {
                    wasLockedOn = lockOn;
                    OnLockOnChanged?.Invoke(wasLockedOn);
                }
            }
            else
            {
                // Beacon offline or manual channel empty
                ClearTelemetry(totalChannels);
            }
        }

        private void UpdateSmoothing()
        {
            float targetStrength = currentTelemetry.hasSignal ? currentTelemetry.totalStrength : 0f;
            smoothedSignalStrength = Mathf.MoveTowards(
                smoothedSignalStrength, 
                targetStrength, 
                Time.deltaTime * meterSmoothingSpeed
            );

            OnTelemetryUpdated?.Invoke(currentTelemetry);
        }

        private void ClearTelemetry(int totalChannels)
        {
            if (currentTargetBeacon != null)
            {
                currentTargetBeacon = null;
                OnTargetChanged?.Invoke(null);
            }

            if (wasLockedOn)
            {
                wasLockedOn = false;
                OnLockOnChanged?.Invoke(false);
            }

            currentTelemetry.hasSignal = false;
            currentTelemetry.channelIndex = currentChannelIndex;
            currentTelemetry.totalChannels = totalChannels;
            currentTelemetry.channelBadge = currentChannelIndex == 0 ? "CH 00 [AUTO-SCAN]" : $"CH {currentChannelIndex:D2} [OUT OF RANGE]";
            currentTelemetry.beaconTag = "NO SIGNAL";
            currentTelemetry.telemetryTitle = "SCANNING...";
            currentTelemetry.frequencyMHz = 0f;
            currentTelemetry.distance = 0f;
            currentTelemetry.facingScore = 0f;
            currentTelemetry.distanceScore = 0f;
            currentTelemetry.totalStrength = 0f;
            currentTelemetry.isLockedOn = false;
            currentTelemetry.angleDegrees = 180f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (helmetCameraTransform == null) return;

            Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
            Gizmos.DrawRay(helmetCameraTransform.position, helmetCameraTransform.forward * 4f);

            if (currentTargetBeacon != null && currentTelemetry.hasSignal)
            {
                Gizmos.color = currentTelemetry.isLockedOn ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.4f);
                Gizmos.DrawLine(helmetCameraTransform.position, currentTargetBeacon.Position);
            }
        }
#endif
    }
}
