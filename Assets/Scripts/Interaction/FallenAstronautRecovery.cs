using System;
using UnityEngine;
using SpaceHorror.Gameplay;
using SpaceHorror.UI;
using SpaceHorror.Player;

namespace SpaceHorror.Interaction
{
    /// <summary>
    /// Attached to fallen player corpses adrift in space.
    /// Allows the astronaut in future loops to interact with their previous corpse,
    /// retrieve lost mission flight recorder telemetry, and salvage emergency backup life support.
    /// </summary>
    [RequireComponent(typeof(MissionInteractable))]
    public class FallenAstronautRecovery : MonoBehaviour
    {
        [Header("Recovery Configuration")]
        [Tooltip("Amount of backup oxygen restored upon salvaging the fallen suit")]
        [SerializeField] private float backupOxygenRestored = 25f;

        [Tooltip("Amount of suit integrity patched upon salvaging")]
        [SerializeField] private float suitRepairPatch = 15f;

        [Tooltip("Broadcast a distress signal on VHF 128.0 MHz so the corpse can be tracked using the radio scanner")]
        [SerializeField] private bool broadcastDistressSignal = true;

        [SerializeField] private float distressFrequencyMHz = 128.0f;

        private MissionInteractable interactable;
        private SignalBeacon beacon;
        private int corpseLoopDay = 1;
        private int corpseIteration = 1;
        private bool isDataRecovered = false;

        public bool IsDataRecovered => isDataRecovered;
        public int CorpseLoopDay => corpseLoopDay;
        public int CorpseIteration => corpseIteration;

        private void Awake()
        {
            interactable = GetComponent<MissionInteractable>();
            beacon = GetComponent<SignalBeacon>();
            SetupRecoveryInteractable();
        }

        public void Initialize(int loopDay, int iteration)
        {
            corpseLoopDay = loopDay;
            corpseIteration = iteration;
            SetupRecoveryInteractable();
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

                interactable.IsSingleUse = true;
                interactable.ConfigureInteractionMethod(InteractionMethod.HoldToComplete, 1.5f, ObjectiveType.Custom);
                interactable.ConfigureRuntimePrompts(
                    $"FALLEN ASTRONAUT (LOOP #{corpseIteration})",
                    "COLLECT MISSION DATA",
                    "MISSION DATA SECURED // TELEMETRY SYNCED"
                );
                interactable.onInteracted.RemoveListener(HandleDataCollected);
                interactable.onInteracted.AddListener(HandleDataCollected);
            }

            if (broadcastDistressSignal)
            {
                if (beacon == null) beacon = GetComponent<SignalBeacon>();
                if (beacon == null) beacon = gameObject.AddComponent<SignalBeacon>();

                if (beacon != null)
                {
                    beacon.Configure("SOS-CORPSE", $"FALLEN SUIT #{corpseIteration} (128.0 MHz)", distressFrequencyMHz, 160f, 2);
                }
            }
        }

        private void HandleDataCollected(GameObject player)
        {
            if (isDataRecovered) return;
            isDataRecovered = true;

            // 1. Recover pending mission data in DayCycleManager if in EVA
            bool missionProgressed = false;
            if (DayCycleManager.Instance != null)
            {
                missionProgressed = DayCycleManager.Instance.TryRecoverMissionData($"Fallen Suit #{corpseIteration}");
            }

            // 2. Replenish leftover backup oxygen / suit patch from corpse
            if (player != null)
            {
                var survival = player.GetComponent<AstronautSurvival>();
                if (survival != null)
                {
                    if (backupOxygenRestored > 0f) survival.ReplenishOxygen(backupOxygenRestored);
                    if (suitRepairPatch > 0f) survival.RepairSuit(suitRepairPatch);
                }
            }

            // 3. Show HUD debriefing / transmission log
            var hud = UnityEngine.Object.FindFirstObjectByType<AstronautHelmetHUD>();
            if (hud != null)
            {
                string title = $"TELEMETRY RECOVERED // LOOP #{corpseIteration}";
                string status = "FLIGHT RECORDER TELEMETRY SECURED.\nCONTINUE EXPEDITION DIRECTIVE.\nBACKUP O2 TANK SALVAGED (+25%).";
                hud.ShowMissionTransmission(title, status, 5.5f);
            }

            // 4. Update beacon state (silence distress broadcast now that data is secured)
            if (beacon != null)
            {
                beacon.SetTransmitting(false);
            }

            Debug.Log($"<color=#55FF55><b>[Corpse Recovery]</b></color> Collected mission data from corpse (Loop #{corpseIteration}, Day {corpseLoopDay}).");
        }
    }
}
