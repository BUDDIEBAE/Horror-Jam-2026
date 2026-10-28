using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceHorror.Interaction
{
    /// <summary>
    /// Predefined VHF Radio Frequency & Channel presets.
    /// Level designers can easily select a preset from the Inspector dropdown without memorizing MHz frequencies.
    /// </summary>
    public enum SignalChannelPreset
    {
        Custom = 0,             // Manual frequency, tag, and title entry
        MissionObjective = 1,   // 142.8 MHz - Main mission objective / Primary target
        AirlockHome = 2,        // 121.5 MHz - EVA dock, Home airlock, Escape pod
        OxygenStation = 3,      // 156.8 MHz - Life support / Emergency O2 depot
        DataTerminal = 4,       // 138.4 MHz - Archives, Audio logs, Terminals
        ReactorCore = 5,        // 149.2 MHz - Power generator, Coolant valves
        DistressBeacon = 6,     // 128.0 MHz - SOS transponder, Lost crew signal
        UnknownAnomaly = 7,     // 166.6 MHz - Horror anomaly, Ghost signal
        // [AUTO-PRESET-ENUM-ANCHOR]
    }

    public static class SignalChannelPresetExtensions
    {
        public static float GetDefaultFrequency(this SignalChannelPreset preset)
        {
            switch (preset)
            {
                case SignalChannelPreset.MissionObjective: return 142.8f;
                case SignalChannelPreset.AirlockHome: return 121.5f;
                case SignalChannelPreset.OxygenStation: return 156.8f;
                case SignalChannelPreset.DataTerminal: return 138.4f;
                case SignalChannelPreset.ReactorCore: return 149.2f;
                case SignalChannelPreset.DistressBeacon: return 128.0f;
                case SignalChannelPreset.UnknownAnomaly: return 166.6f;
                // [AUTO-FREQUENCY-ANCHOR]
                default: return 142.8f;
            }
        }

        public static string GetDefaultTag(this SignalChannelPreset preset)
        {
            switch (preset)
            {
                case SignalChannelPreset.MissionObjective: return "OBJECTIVE";
                case SignalChannelPreset.AirlockHome: return "AIRLOCK-HOME";
                case SignalChannelPreset.OxygenStation: return "O2-STATION";
                case SignalChannelPreset.DataTerminal: return "TERMINAL-LOG";
                case SignalChannelPreset.ReactorCore: return "REACTOR-CORE";
                case SignalChannelPreset.DistressBeacon: return "SOS-BEACON";
                case SignalChannelPreset.UnknownAnomaly: return "ANOMALY";
                // [AUTO-TAG-ANCHOR]
                default: return "BEACON";
            }
        }

        public static string GetDefaultTitle(this SignalChannelPreset preset)
        {
            switch (preset)
            {
                case SignalChannelPreset.MissionObjective: return "PRIMARY MISSION TARGET";
                case SignalChannelPreset.AirlockHome: return "EVA DOCK / AIRLOCK";
                case SignalChannelPreset.OxygenStation: return "EMERGENCY O2 DEPOT";
                case SignalChannelPreset.DataTerminal: return "TRANSMISSION ARCHIVE";
                case SignalChannelPreset.ReactorCore: return "MAIN REACTOR VALVE";
                case SignalChannelPreset.DistressBeacon: return "DISTRESS EMERGENCY BEACON";
                case SignalChannelPreset.UnknownAnomaly: return "UNIDENTIFIED EMISSION";
                // [AUTO-TITLE-ANCHOR]
                default: return "EMERGENCY BEACON";
            }
        }

        public static int GetDefaultPriority(this SignalChannelPreset preset)
        {
            switch (preset)
            {
                case SignalChannelPreset.MissionObjective: return 3;
                case SignalChannelPreset.AirlockHome: return 2;
                case SignalChannelPreset.OxygenStation: return 3;
                case SignalChannelPreset.ReactorCore: return 2;
                case SignalChannelPreset.DataTerminal: return 1;
                case SignalChannelPreset.DistressBeacon: return 2;
                case SignalChannelPreset.UnknownAnomaly: return 4;
                // [AUTO-PRIORITY-ANCHOR]
                default: return 1;
            }
        }
    }

    /// <summary>
    /// Radio Beacon transmitter for 6DoF Deep Space Horror.
    /// Emits a directional VHF/Radio signal that the player's helmet tracker can scan and align with.
    /// Adheres strictly to Film Noir aesthetics: monochromatic telemetry, frequency tuning, and zero color coding.
    /// </summary>
    [DisallowMultipleComponent]
    public class SignalBeacon : MonoBehaviour
    {
        // Centralized static registry for ultra-fast queries without GC or FindObjects overhead
        public static readonly List<SignalBeacon> ActiveBeacons = new List<SignalBeacon>();

        [Header("Channel Preset (Easy Level Design Dropdown)")]
        [Tooltip("Select preset to automatically configure Frequency, Tag, and Title. Set to Custom for manual override.")]
        [SerializeField] private SignalChannelPreset channelPreset = SignalChannelPreset.MissionObjective;

        [Tooltip("If true, manual values below will not be overwritten by the preset.")]
        [SerializeField] private bool customOverride = false;

        [Header("Beacon Identity (Film Noir Monochrome Telemetry)")]
        [Tooltip("Identifier code displayed on helmet HUD (e.g. 'OBJECTIVE', 'AIRLOCK-HOME', 'O2-STATION')")]
        [SerializeField] private string beaconTag = "OBJECTIVE";

        [Tooltip("Brief descriptive telemetry title")]
        [SerializeField] private string telemetryTitle = "PRIMARY MISSION TARGET";

        [Tooltip("VHF Radio frequency in MHz displayed on the digital tuner")]
        [SerializeField] private float broadcastFrequencyMHz = 142.8f;

        [Header("Signal Propagation")]
        [Tooltip("Maximum broadcast reach in world units / meters")]
        [SerializeField] private float maxRange = 75f;

        [Tooltip("Relative priority weight. Higher values take precedence when multiple beacons are in range.")]
        [SerializeField] private int priority = 2;

        [Tooltip("Is the beacon currently transmitting?")]
        [SerializeField] private bool isTransmitting = true;

        [Tooltip("If true, signal shuts down permanently once an associated objective is completed")]
        [SerializeField] private bool deactiveOnMissionComplete = true;

        // Properties
        public SignalChannelPreset ChannelPreset => channelPreset;
        public string BeaconTag => beaconTag;
        public string TelemetryTitle => telemetryTitle;
        public float BroadcastFrequencyMHz => broadcastFrequencyMHz;
        public float MaxRange => Mathf.Max(1f, maxRange);
        public int Priority => priority;
        public bool IsTransmitting => isTransmitting && enabled && gameObject.activeInHierarchy;
        public bool DeactivateOnMissionComplete => deactiveOnMissionComplete;
        public Vector3 Position => transform.position;

        private void OnValidate()
        {
            if (channelPreset != SignalChannelPreset.Custom && !customOverride)
            {
                broadcastFrequencyMHz = channelPreset.GetDefaultFrequency();
                beaconTag = channelPreset.GetDefaultTag();
                telemetryTitle = channelPreset.GetDefaultTitle();
                priority = channelPreset.GetDefaultPriority();
            }
        }

        private void OnEnable()
        {
            RegisterBeacon();
        }

        private void OnDisable()
        {
            UnregisterBeacon();
        }

        private void OnDestroy()
        {
            UnregisterBeacon();
        }

        public void SetTransmitting(bool active)
        {
            isTransmitting = active;
            enabled = active;
            if (active && !ActiveBeacons.Contains(this))
            {
                ActiveBeacons.Add(this);
            }
            else if (!active && ActiveBeacons.Contains(this))
            {
                ActiveBeacons.Remove(this);
            }
        }

        /// <summary>
        /// Configures the beacon using a preset. Ideal for 1-click builder and level design scripts.
        /// </summary>
        public void Configure(SignalChannelPreset preset, float range = 75f)
        {
            channelPreset = preset;
            customOverride = false;
            broadcastFrequencyMHz = preset.GetDefaultFrequency();
            beaconTag = preset.GetDefaultTag();
            telemetryTitle = preset.GetDefaultTitle();
            priority = preset.GetDefaultPriority();
            maxRange = range;
        }

        public void Configure(string tag, string title, float frequency, float range, int priorityLevel = 1)
        {
            channelPreset = SignalChannelPreset.Custom;
            customOverride = true;
            beaconTag = tag;
            telemetryTitle = title;
            broadcastFrequencyMHz = frequency;
            maxRange = range;
            priority = priorityLevel;
        }

        private void RegisterBeacon()
        {
            if (isTransmitting && !ActiveBeacons.Contains(this))
            {
                ActiveBeacons.Add(this);
            }
        }

        private void UnregisterBeacon()
        {
            if (ActiveBeacons.Contains(this))
            {
                ActiveBeacons.Remove(this);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Noir Ash-Silver wireframe sphere for range visualization in editor
            Gizmos.color = new Color(0.85f, 0.85f, 0.85f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, maxRange);
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, 1.0f);
        }
#endif
    }
}
