using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using SpaceHorror.Player;
using SpaceHorror.UI;
using SpaceHorror.Interaction;

namespace SpaceHorror.Gameplay
{
    public enum ExpeditionState
    {
        InShip_AwaitingContract,    // Woke up inside ship, directive is to access terminal
        EVA_InProgress,             // Contract signed at terminal, EVA in outer space
        EVA_ObjectiveCompleted,     // Daily objectives finished in space, directive is return to airlock
        TransitioningToNextDay      // Cycled airlock, playing debriefing report & advancing day
    }

    /// <summary>
    /// Master Expedition Loop Director.
    /// Manages daily expedition cycles:
    /// 1. Wakes up in ship -> Access Ship Terminal
    /// 2. Signs Daily Contract -> EVA into outer space to complete 1 or more missions
    /// 3. Returns to Ship Airlock (121.5 MHz) -> Cycles airlock & sleeps
    /// 4. Cycles to next day with full oxygen/suit refill.
    /// Each Day has its own single Briefing Transcript and Completion Debriefing, containing a list of Missions.
    /// </summary>
    public class DayCycleManager : MonoBehaviour
    {
        public static DayCycleManager Instance { get; private set; }

        [Header("Day Cycle Tracking")]
        [Tooltip("Current active day number")]
        [SerializeField] private int currentDay = 1;

        [Tooltip("Current operational state of the expedition")]
        [SerializeField] private ExpeditionState currentState = ExpeditionState.InShip_AwaitingContract;

        [Header("Spawn & Ship Anchors")]
        [Tooltip("Transform position where astronaut spawns at start of day or upon O2 depletion")]
        [FormerlySerializedAs("shipSpawnPoint")]
        [SerializeField] private Transform spawnPoint;

        public Transform SpawnPoint
        {
            get => spawnPoint;
            set => spawnPoint = value;
        }

        [Header("Daily Expeditions (List of Days -> Missions per Day)")]
        [Tooltip("Curated daily expeditions. Each day contains a single Briefing & Debriefing with 1 or more Missions.")]
        [SerializeField] private List<DayExpeditionData> days = new List<DayExpeditionData>();

        [Header("Components (Auto-detected if unassigned)")]
        [SerializeField] private AstronautSurvival playerSurvival;
        [SerializeField] private AstronautMovement6D playerMovement;
        [SerializeField] private AstronautWakeUpTransition playerWakeUp;
        [SerializeField] private AstronautHelmetHUD helmetHUD;
        [SerializeField] private ShipReturnAirlock returnAirlock;
        [SerializeField] private ShipMissionTerminal missionTerminal;

        // Public Getters & State Flags
        public int CurrentDay => currentDay;
        public ExpeditionState State => currentState;
        public bool IsMissionActive => currentState == ExpeditionState.EVA_InProgress;
        public bool IsDailyObjectiveCompleted => currentState == ExpeditionState.EVA_ObjectiveCompleted;

        // Events
        public event Action<int, DayExpeditionData> OnDayStarted;
        public event Action<DayExpeditionData> OnMissionAccepted;
        public event Action<DayExpeditionData> OnMissionCompleted;
        public event Action<int> OnDayEnded;

        private DayExpeditionData activeDay;
        public DayExpeditionData ActiveDay => activeDay;
        public List<MissionObjectiveData> ActiveMissions => activeDay != null ? activeDay.missions : null;

        private bool isTerminalContractAccepted = false;
        private bool isDayConcluding = false;
        private Coroutine dayTransitionCoroutine;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            ResolveReferences();
            PopulateDefaultDaysIfEmpty();
        }

        private void OnEnable()
        {
            if (playerSurvival != null)
            {
                playerSurvival.OnPlayerAsphyxiated += HandlePlayerAsphyxiated;
            }
        }

        private void OnDisable()
        {
            if (playerSurvival != null)
            {
                playerSurvival.OnPlayerAsphyxiated -= HandlePlayerAsphyxiated;
            }
        }

        private void Start()
        {
            // Begin Day 1
            StartDay(currentDay, false);
        }

        private void ResolveReferences()
        {
            if (playerSurvival == null) playerSurvival = FindFirstObjectByType<AstronautSurvival>();
            if (playerMovement == null) playerMovement = FindFirstObjectByType<AstronautMovement6D>();
            if (playerWakeUp == null) playerWakeUp = FindFirstObjectByType<AstronautWakeUpTransition>();
            if (helmetHUD == null) helmetHUD = FindFirstObjectByType<AstronautHelmetHUD>();
            if (returnAirlock == null) returnAirlock = FindFirstObjectByType<ShipReturnAirlock>();
            if (missionTerminal == null) missionTerminal = FindFirstObjectByType<ShipMissionTerminal>();

            if (spawnPoint == null)
            {
                GameObject sp = GameObject.Find("Spawnpoint");
                if (sp == null) sp = GameObject.Find("SpawnPoint");
                if (sp == null) sp = GameObject.Find("PlayerSpawnPoint");
                if (sp == null) sp = GameObject.Find("ShipSpawnPoint");
                if (sp != null)
                {
                    spawnPoint = sp.transform;
                    Debug.Log($"<color=#55FF55><b>[DayCycleManager]</b></color> Auto-detected Spawnpoint: '{sp.name}' at {spawnPoint.position}");
                }
            }
        }

        /// <summary>
        /// Populates default curated expeditions for Day 1, 2, 3 if none were manually configured in Inspector.
        /// </summary>
        private void PopulateDefaultDaysIfEmpty()
        {
            if (days != null && days.Count > 0) return;

            days = new List<DayExpeditionData>
            {
                new DayExpeditionData
                {
                    dayNumber = 1,
                    dayTitle = "OPERATION: RECOVERY PROTOCOL",
                    briefingTranscript = "STATION LOG // DAY 01:\n'Derelict cargo module adrift at coordinates 142.8 MHz. Retrieve flight recorder telemetry and return to airlock before life support depletes.'",
                    completionDebriefing = "EXPEDITION COMPLETE // DAY 01\nDATA LOG TRANSFERRED TO SHIP COMPUTER.\nLIFE SUPPORT RESERVES REFILLED.",
                    missions = new List<MissionObjectiveData>
                    {
                        new MissionObjectiveData
                        {
                            directiveText = "SALVAGE DERELICT SATELLITE LOG",
                            frequencyMHz = 142.8f,
                            beaconTag = "OBJECTIVE"
                        }
                    }
                },
                new DayExpeditionData
                {
                    dayNumber = 2,
                    dayTitle = "OPERATION: AUXILIARY LIFE SUPPORT",
                    briefingTranscript = "COMMAND DISPATCH // DAY 02:\n'Sub-station O2 pressure drop detected across Sector 2. Locate backup depot at 156.8 MHz, initiate manual recharge, and secure the supply.'",
                    completionDebriefing = "EXPEDITION COMPLETE // DAY 02\nO2 DEPOT INTEGRITY RESTORED.\nSTATION ATMOSPHERE STABILIZED.",
                    missions = new List<MissionObjectiveData>
                    {
                        new MissionObjectiveData
                        {
                            directiveText = "LOCATE & REPAIR EMERGENCY O2 DEPOT",
                            frequencyMHz = 156.8f,
                            beaconTag = "O2-STATION"
                        }
                    }
                },
                new DayExpeditionData
                {
                    dayNumber = 3,
                    dayTitle = "OPERATION: BLACKOUT SIGNAL",
                    briefingTranscript = "ALERT // DAY 03:\n'Unidentified transponder pinging on emergency frequency 127.1 MHz deep within the debris field. Investigate origin and extract surviving logs.'",
                    completionDebriefing = "EXPEDITION COMPLETE // DAY 03\nANOMALY LOG SECURED.\nCOMMENCING CRYO SLEEP PROTOCOL.",
                    missions = new List<MissionObjectiveData>
                    {
                        new MissionObjectiveData
                        {
                            directiveText = "INVESTIGATE DISTRESS TRANSMITTER",
                            frequencyMHz = 127.1f,
                            beaconTag = "DISTRESS"
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Starts the specified day expedition.
        /// Refills life support, teleports to ship, announces day banner, and triggers wake-up.
        /// </summary>
        public void StartDay(int dayNumber, bool triggerWakeUp = true)
        {
            currentDay = dayNumber;
            currentState = ExpeditionState.InShip_AwaitingContract;
            isTerminalContractAccepted = false;
            isDayConcluding = false;
            activeDay = GetDayData(currentDay);

            // 1. Refill player life support for the new expedition
            if (playerSurvival != null)
            {
                playerSurvival.ReplenishOxygen(100f);
                playerSurvival.RepairSuit(100f);
            }

            // 2. Position player at spawn point
            TeleportPlayerToSpawn();

            // 3. Deactivate all mission target beacons until contract is accepted at terminal
            if (activeDay != null && activeDay.missions != null)
            {
                for (int i = 0; i < activeDay.missions.Count; i++)
                {
                    MissionObjectiveData m = activeDay.missions[i];
                    m.ResetRuntimeState();
                    AutoBindMissionTargets(m);
                    if (m.targetBeacon != null)
                    {
                        m.targetBeacon.gameObject.SetActive(false);
                    }
                }
            }

            // 4. Update Helmet Visor HUD
            if (helmetHUD != null)
            {
                helmetHUD.SetHUDAlpha(1f);
            }
            UpdateHUDObjectiveCard();
            if (helmetHUD != null && activeDay != null)
            {
                helmetHUD.ShowDayBanner(currentDay, activeDay.dayTitle);
            }

            // 5. Trigger Waking Transition or ensure input enabled
            if (triggerWakeUp && playerWakeUp != null)
            {
                playerWakeUp.TriggerWakeUp();
            }
            else
            {
                AstronautInputHandler input = FindFirstObjectByType<AstronautInputHandler>();
                if (input != null) input.InputEnabled = true;
            }

            // 6. Fully reset terminal and airlock states for the new day
            if (missionTerminal != null)
            {
                missionTerminal.ResetTerminal();
            }
            else
            {
                missionTerminal = FindFirstObjectByType<ShipMissionTerminal>();
                if (missionTerminal != null) missionTerminal.ResetTerminal();
            }

            if (returnAirlock != null)
            {
                returnAirlock.ResetAirlock();
            }
            else
            {
                returnAirlock = FindFirstObjectByType<ShipReturnAirlock>();
                if (returnAirlock != null) returnAirlock.ResetAirlock();
            }

            OnDayStarted?.Invoke(currentDay, activeDay);
            int missionCount = activeDay != null && activeDay.missions != null ? activeDay.missions.Count : 0;
            Debug.Log($"<color=#FFFFFF><b>[DayCycleManager]</b></color> Starting DAY {currentDay}: {activeDay?.dayTitle} ({missionCount} mission(s))");
        }

        /// <summary>
        /// Generates the structured daily checklist items matching the Paint HUD wireframe.
        /// If 1 mission is configured in the day, produces 1 objective. If multiple, produces all objectives.
        /// </summary>
        private string[] GetDailyTaskList()
        {
            List<string> tasks = new List<string>();
            tasks.Add("Access Ship Mission Terminal");

            if (activeDay != null && activeDay.missions != null)
            {
                for (int i = 0; i < activeDay.missions.Count; i++)
                {
                    var m = activeDay.missions[i];
                    string freqInfo = (m != null && m.frequencyMHz > 0) ? $" ({m.frequencyMHz:F1} MHz)" : "";
                    string directive = m != null ? m.directiveText : "Complete Space Mission";
                    tasks.Add($"{directive}{freqInfo}");
                }
            }

            tasks.Add("Return to Ship Airlock (121.5 MHz)");
            return tasks.ToArray();
        }

        private void UpdateHUDObjectiveCard()
        {
            if (helmetHUD == null || activeDay == null) return;

            string[] tasks = GetDailyTaskList();
            int totalTasks = tasks.Length;
            bool[] completedStates = new bool[totalTasks];

            // 1. Terminal task (index 0)
            completedStates[0] = isTerminalContractAccepted;

            // 2. Mission objectives (indices 1 to activeDay.missions.Count)
            int firstIncompleteMissionIdx = -1;
            int missionCount = activeDay.missions != null ? activeDay.missions.Count : 0;
            for (int i = 0; i < missionCount; i++)
            {
                bool done = activeDay.missions[i].isCompleted;
                completedStates[i + 1] = done;
                if (!done && firstIncompleteMissionIdx == -1)
                {
                    firstIncompleteMissionIdx = i;
                }
            }

            // 3. Airlock extraction task (last index)
            completedStates[totalTasks - 1] = isDayConcluding;

            // Determine which task is currently active (highlighted bright white)
            int activeIndex = -1;
            if (!isTerminalContractAccepted)
            {
                activeIndex = 0; // Access terminal
            }
            else if (firstIncompleteMissionIdx != -1)
            {
                activeIndex = firstIncompleteMissionIdx + 1; // Current pending space mission
            }
            else if (!isDayConcluding)
            {
                activeIndex = totalTasks - 1; // All missions done -> Return to airlock
            }

            helmetHUD.SetObjectiveChecklist(currentDay, tasks, completedStates, activeIndex);
        }

        /// <summary>
        /// Accepts the daily mission contract from the Ship Terminal.
        /// Activates all target beacons in outer space and displays the day's single briefing transcript.
        /// </summary>
        public void AcceptCurrentDailyMission()
        {
            if (currentState != ExpeditionState.InShip_AwaitingContract || activeDay == null) return;

            currentState = ExpeditionState.EVA_InProgress;
            isTerminalContractAccepted = true;

            // 1. Activate external target beacons and hook interaction listeners for each mission
            if (activeDay.missions != null)
            {
                for (int i = 0; i < activeDay.missions.Count; i++)
                {
                    MissionObjectiveData m = activeDay.missions[i];
                    if (m.targetBeacon != null)
                    {
                        m.targetBeacon.gameObject.SetActive(true);
                    }

                    if (m.targetInteractable != null)
                    {
                        m.targetInteractable.onInteracted.RemoveAllListeners();
                        m.targetInteractable.onInteracted.AddListener((player) => HandleObjectiveInteracted(m, player));
                    }
                }
            }

            // 2. Update HUD Directive checklist
            UpdateHUDObjectiveCard();

            // 3. Show the day's single briefing log popup
            if (helmetHUD != null && !string.IsNullOrEmpty(activeDay.briefingTranscript))
            {
                helmetHUD.ShowMissionTransmission(activeDay.dayTitle, activeDay.briefingTranscript, 6.5f);
            }

            OnMissionAccepted?.Invoke(activeDay);
            Debug.Log($"<color=#FFFFFF><b>[DayCycleManager]</b></color> Contract signed for DAY {currentDay} ({activeDay.dayTitle})");
        }

        private void HandleObjectiveInteracted(MissionObjectiveData mission, GameObject player)
        {
            if (currentState != ExpeditionState.EVA_InProgress || activeDay == null) return;
            if (mission == null || mission.isCompleted) return;

            mission.isCompleted = true;

            if (mission.targetBeacon != null)
            {
                mission.targetBeacon.gameObject.SetActive(false);
            }

            // Count completed objectives
            int completedCount = 0;
            int totalCount = activeDay.missions != null ? activeDay.missions.Count : 0;
            for (int i = 0; i < totalCount; i++)
            {
                if (activeDay.missions[i].isCompleted) completedCount++;
            }

            if (completedCount < totalCount)
            {
                // Still more objectives remaining in outer space
                UpdateHUDObjectiveCard();
                if (helmetHUD != null)
                {
                    helmetHUD.ShowInteractionStatusMessage($"OBJECTIVE SECURED ({completedCount}/{totalCount}) // PROCEED TO NEXT TARGET", 4.0f);
                }
                Debug.Log($"<color=#FFFFFF><b>[DayCycleManager]</b></color> Objective secured ({completedCount}/{totalCount}) for DAY {currentDay}");
            }
            else
            {
                // All objectives for the day completed!
                currentState = ExpeditionState.EVA_ObjectiveCompleted;
                UpdateHUDObjectiveCard();

                if (helmetHUD != null)
                {
                    helmetHUD.ShowInteractionStatusMessage("ALL OBJECTIVES SECURED // RETURN TO AIRLOCK [121.5 MHz]", 4.5f);
                }

                if (returnAirlock != null)
                {
                    returnAirlock.RefreshAirlockPrompts();
                }

                OnMissionCompleted?.Invoke(activeDay);
                Debug.Log($"<color=#FFFFFF><b>[DayCycleManager]</b></color> All {totalCount} objectives completed for DAY {currentDay}! Return to Ship.");
            }
        }

        /// <summary>
        /// Concludes the day, displays debriefing, cycles airlock, and advances to the next day.
        /// </summary>
        public void CompleteDayAndSleep()
        {
            if (currentState == ExpeditionState.TransitioningToNextDay) return;

            if (dayTransitionCoroutine != null)
            {
                StopCoroutine(dayTransitionCoroutine);
            }

            dayTransitionCoroutine = StartCoroutine(DayTransitionRoutine());
        }

        private IEnumerator DayTransitionRoutine()
        {
            currentState = ExpeditionState.TransitioningToNextDay;
            isDayConcluding = true;

            // 1. Update HUD card & show single day debriefing report
            UpdateHUDObjectiveCard();
            if (helmetHUD != null && activeDay != null && !string.IsNullOrEmpty(activeDay.completionDebriefing))
            {
                helmetHUD.ShowMissionTransmission("EXPEDITION DEBRIEFING", activeDay.completionDebriefing, 5.5f);
            }

            // 2. Lock player input during sleep sequence
            AstronautInputHandler input = FindFirstObjectByType<AstronautInputHandler>();
            if (input != null) input.InputEnabled = false;

            yield return new WaitForSeconds(2.0f);

            // 3. Fade out HUD
            float fadeTime = 1.5f;
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                if (helmetHUD != null)
                {
                    helmetHUD.SetHUDAlpha(Mathf.Lerp(1f, 0f, t / fadeTime));
                }
                yield return null;
            }

            yield return new WaitForSeconds(1.0f);

            OnDayEnded?.Invoke(currentDay);

            // 4. Increment Day and start next cycle
            int nextDay = currentDay + 1;
            StartDay(nextDay, true);

            dayTransitionCoroutine = null;
        }

        /// <summary>
        /// Handles player asphyxiation death in outer space:
        /// Rescues player back to ship cryo bay and retries the day.
        /// </summary>
        private void HandlePlayerAsphyxiated()
        {
            if (dayTransitionCoroutine != null) return;
            dayTransitionCoroutine = StartCoroutine(EmergencyRescueRoutine());
        }

        private IEnumerator EmergencyRescueRoutine()
        {
            // 1. Immediately disable player input & arrest remaining momentum
            AstronautInputHandler input = FindFirstObjectByType<AstronautInputHandler>();
            if (input != null) input.InputEnabled = false;

            if (playerMovement != null)
            {
                Rigidbody rb = playerMovement.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            // 2. Visor emergency notification
            if (helmetHUD != null)
            {
                helmetHUD.ShowMissionTransmission("OXYGEN DEPLETED", "LIFE SUPPORT OFFLINE // EMERGENCY RECOVERY TO SHIP CRYOPOD...", 3.5f);
            }

            yield return new WaitForSeconds(1.2f);

            // 3. Teleport immediately to spawn point
            TeleportPlayerToSpawn();

            yield return new WaitForSeconds(1.5f);

            // 4. Restart current day inside ship (refills O2 & suit, resets terminal, triggers wake-up)
            StartDay(currentDay, true);
            dayTransitionCoroutine = null;
        }

        private DayExpeditionData GetDayData(int day)
        {
            if (days != null)
            {
                for (int i = 0; i < days.Count; i++)
                {
                    if (days[i].dayNumber == day)
                    {
                        return days[i];
                    }
                }
            }

            // Fallback for procedural/infinite days
            return new DayExpeditionData
            {
                dayNumber = day,
                dayTitle = $"OPERATION: SECTOR EXPEDITION {day:D2}",
                briefingTranscript = $"COMMAND LOG // DAY {day:D2}:\n'Continue sector reconnaissance. Radio beacon transmitting on 142.8 MHz. Secure data and return.'",
                completionDebriefing = $"EXPEDITION COMPLETE // DAY {day:D2}\nDATA SECURED FOR SHIP RECHARGING.\nLIFE SUPPORT RESERVES REFILLED.",
                missions = new List<MissionObjectiveData>
                {
                    new MissionObjectiveData
                    {
                        directiveText = "LOCATE & SALVAGE DEEP SPACE TELEMETRY",
                        frequencyMHz = 142.8f,
                        beaconTag = "OBJECTIVE"
                    }
                }
            };
        }

        private void AutoBindMissionTargets(MissionObjectiveData mission)
        {
            if (mission == null) return;

            // 1. Auto-bind beacon if null
            if (mission.targetBeacon == null && !string.IsNullOrEmpty(mission.beaconTag))
            {
                SignalBeacon[] allBeacons = FindObjectsByType<SignalBeacon>(FindObjectsSortMode.None);
                for (int i = 0; i < allBeacons.Length; i++)
                {
                    if (allBeacons[i].BeaconTag.Equals(mission.beaconTag, StringComparison.OrdinalIgnoreCase))
                    {
                        mission.targetBeacon = allBeacons[i];
                        break;
                    }
                }
            }

            // 2. Auto-bind interactable if null
            if (mission.targetInteractable == null)
            {
                if (mission.targetBeacon != null)
                {
                    mission.targetInteractable = mission.targetBeacon.GetComponent<MissionInteractable>();
                }

                if (mission.targetInteractable == null && !string.IsNullOrEmpty(mission.beaconTag))
                {
                    MissionInteractable[] allInteractables = FindObjectsByType<MissionInteractable>(FindObjectsSortMode.None);
                    for (int i = 0; i < allInteractables.Length; i++)
                    {
                        if (allInteractables[i].gameObject.name.Contains(mission.beaconTag, StringComparison.OrdinalIgnoreCase))
                        {
                            mission.targetInteractable = allInteractables[i];
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Instantly teleports the player to the configured SpawnPoint, cancelling all inertia and angular drift.
        /// </summary>
        [ContextMenu("Teleport Player To Spawn")]
        public void TeleportPlayerToSpawn()
        {
            if (spawnPoint == null)
            {
                ResolveReferences();
            }

            if (spawnPoint == null)
            {
                Debug.LogWarning("<color=#FF9900><b>[DayCycleManager]</b></color> Cannot teleport: SpawnPoint Transform is not assigned!");
                return;
            }

            Transform targetTransform = null;
            if (playerMovement != null)
            {
                targetTransform = playerMovement.transform;
            }
            else if (playerSurvival != null)
            {
                targetTransform = playerSurvival.transform;
            }
            else
            {
                var mv = FindFirstObjectByType<AstronautMovement6D>();
                if (mv != null) targetTransform = mv.transform;
            }

            if (targetTransform != null)
            {
                Rigidbody rb = targetTransform.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.position = spawnPoint.position;
                    rb.rotation = spawnPoint.rotation;
                }

                targetTransform.position = spawnPoint.position;
                targetTransform.rotation = spawnPoint.rotation;
                Physics.SyncTransforms();

                Debug.Log($"<color=#55FF55><b>[DayCycleManager]</b></color> Astronaut teleported to Spawnpoint '{spawnPoint.name}' at {spawnPoint.position}");
            }
        }
    }
}
