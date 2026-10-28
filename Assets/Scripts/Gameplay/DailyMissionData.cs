using System;
using System.Collections.Generic;
using UnityEngine;
using SpaceHorror.Interaction;

namespace SpaceHorror.Gameplay
{
    /// <summary>
    /// Represents an individual mission objective / target in outer space within a day's expedition.
    /// Can be bound to a target SignalBeacon and MissionInteractable.
    /// </summary>
    [Serializable]
    public class MissionObjectiveData
    {
        [Header("Objective Directive & Telemetry")]
        [Tooltip("Concise directive displayed on the astronaut visor HUD checklist")]
        public string directiveText = "SALVAGE DERELICT SATELLITE LOG";

        [Tooltip("Radio VHF frequency (MHz) broadcasting from the target beacon")]
        public float frequencyMHz = 142.8f;

        [Tooltip("Tag identifier on the beacon (e.g. 'OBJECTIVE', 'O2-DEPOT', 'CARGO-POD')")]
        public string beaconTag = "OBJECTIVE";

        [Header("Scene Object Bindings (Optional - Auto-detected if null)")]
        [Tooltip("The interactable object in space to complete. If null, automatically matched by beaconTag.")]
        public MissionInteractable targetInteractable;

        [Tooltip("The signal beacon attached to the target object. Activated upon contract acceptance.")]
        public SignalBeacon targetBeacon;

        /// <summary>
        /// Runtime completion state of this individual objective.
        /// </summary>
        [NonSerialized]
        public bool isCompleted = false;

        public void ResetRuntimeState()
        {
            isCompleted = false;
        }
    }

    /// <summary>
    /// Represents an entire expedition day.
    /// Contains 1 Day Title, 1 Briefing Transcript, 1 Completion Debriefing, and a list of Missions/Objectives for that day.
    /// </summary>
    [Serializable]
    public class DayExpeditionData
    {
        [Header("Day Identity")]
        [Tooltip("The day number this plan corresponds to (1, 2, 3...)")]
        public int dayNumber = 1;

        [Tooltip("Codename or title of the day's operation (e.g. 'OPERATION: RECOVERY PROTOCOL')")]
        public string dayTitle = "OPERATION: RECOVERY PROTOCOL";

        [Header("Day Transmissions (Single Briefing & Debriefing for the Day)")]
        [Tooltip("Incoming briefing transcript displayed on ship terminal upon contract acceptance")]
        [TextArea(3, 6)]
        public string briefingTranscript = "COMMAND DISPATCH // DAY 01:\n'Derelict cargo module verified adrift at Sector 4. Retrieve telemetry and return to airlock before life support depletes.'";

        [Tooltip("Debriefing summary displayed when returning to ship airlock and cycling")]
        [TextArea(2, 4)]
        public string completionDebriefing = "EXPEDITION COMPLETE // DAY 01\nDATA SECURED FOR SHIP RECHARGING.\nLIFE SUPPORT RESERVES REFILLED.";

        [Header("Missions / Objectives for this Day")]
        [Tooltip("List of missions to complete in outer space during this day")]
        public List<MissionObjectiveData> missions = new List<MissionObjectiveData>();

        [Header("Awakening & Spawn Anchor (Optional)")]
        [Tooltip("Custom awakening/spawn point for this specific day. If unassigned, defaults to DayCycleManager's spawnPoint or day3AwakeningPoint.")]
        public Transform customAwakeningPoint;
    }

    /// <summary>
    /// Backward-compatibility alias for DayExpeditionData.
    /// </summary>
    [Serializable]
    public class DailyMissionData : DayExpeditionData
    {
    }
}
