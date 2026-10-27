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
        private bool hasCorpseToRecover = false;
        private bool isCorpseDataRecoveredThisLoop = false;
        public bool HasCorpseToRecover => hasCorpseToRecover;
        public bool IsCorpseDataRecoveredThisLoop => isCorpseDataRecoveredThisLoop;
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
            ResolveReferences();
            if (playerSurvival != null)
            {
                playerSurvival.OnPlayerAsphyxiated -= HandlePlayerAsphyxiated;
                playerSurvival.OnPlayerAsphyxiated += HandlePlayerAsphyxiated;
            }

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
                GameObject sp = GameObject.Find("Spawn Point");
                if (sp == null) sp = GameObject.Find("Spawnpoint");
                if (sp == null) sp = GameObject.Find("SpawnPoint");
                if (sp == null) sp = GameObject.Find("PlayerSpawnPoint");
                if (sp == null) sp = GameObject.Find("Player Spawn Point");
                if (sp == null) sp = GameObject.Find("ShipSpawnPoint");
                if (sp == null) sp = GameObject.Find("Ship Spawn Point");
                if (sp == null) sp = GameObject.Find("Player Spawn");
                if (sp != null)
                {
                    spawnPoint = sp.transform;
                    Debug.Log($"<color=#55FF55><b>[DayCycleManager]</b></color> Auto-detected Spawnpoint: '{sp.name}' at {spawnPoint.position}");
                }
                else if (returnAirlock != null)
                {
                    spawnPoint = returnAirlock.transform;
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
            if (dayNumber != currentDay)
            {
                hasCorpseToRecover = false;
                isCorpseDataRecoveredThisLoop = false;
            }

            currentDay = dayNumber;
            isDayConcluding = false;
            activeDay = GetDayData(currentDay);

            if (hasCorpseToRecover)
            {
                // In loop recovery: player is immediately authorized for EVA to recover the corpse
                currentState = ExpeditionState.EVA_InProgress;
                isTerminalContractAccepted = true;
            }
            else
            {
                currentState = ExpeditionState.InShip_AwaitingContract;
                isTerminalContractAccepted = false;
            }

            // 1. Refill player life support for the new expedition
            if (playerSurvival != null)
            {
                playerSurvival.ReplenishOxygen(100f);
                playerSurvival.RepairSuit(100f);
            }

            // 2. Position player at spawn point
            TeleportPlayerToSpawn();

            // 3. Setup mission target beacons and listeners (preserving completed missions if recovering corpse)
            if (activeDay != null && activeDay.missions != null)
            {
                for (int i = 0; i < activeDay.missions.Count; i++)
                {
                    MissionObjectiveData m = activeDay.missions[i];
                    if (!hasCorpseToRecover)
                    {
                        m.ResetRuntimeState();
                    }
                    AutoBindMissionTargets(m);
                    if (m.targetBeacon != null)
                    {
                        bool shouldTransmit = hasCorpseToRecover && !m.isCompleted;
                        m.targetBeacon.SetTransmitting(shouldTransmit);
                        m.targetBeacon.enabled = shouldTransmit;
                    }

                    if (m.targetInteractable != null)
                    {
                        m.targetInteractable.onInteracted.RemoveAllListeners();
                        m.targetInteractable.onInteracted.AddListener((player) => HandleObjectiveInteracted(m, player));
                        if (!hasCorpseToRecover)
                        {
                            m.targetInteractable.ResetObjective();
                        }
                    }
                }
            }

            // 4. Regenerate world-space survival pickups (O2 canisters, suit repair stations)
            ResetSurvivalInteractables(hasCorpseToRecover);

            // 5. Update Helmet Visor HUD
            if (helmetHUD != null)
            {
                helmetHUD.SetHUDAlpha(1f);
            }
            UpdateHUDObjectiveCard();
            if (helmetHUD != null && activeDay != null)
            {
                if (hasCorpseToRecover)
                {
                    helmetHUD.ShowDayBanner(currentDay, "RECOVER FALLEN TELEMETRY");
                }
                else
                {
                    helmetHUD.ShowDayBanner(currentDay, activeDay.dayTitle);
                }
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
        /// Updates the Paint-style HUD Objective Card dynamically according to the active expedition Phase:
        /// - Phase 1 (Pre-Acceptance): "Access Ship Mission Terminal" OR "Recover Mission Data from Fallen Corpse (128.0 MHz)"
        /// - Phase 2 (Active Execution): Day's space mission objectives configured in Inspector (preserves completed [DONE] status)
        /// - Phase 3 (Extraction/Turn-in): "Return to Ship Airlock (121.5 MHz)"
        /// </summary>
        private void UpdateHUDObjectiveCard()
        {
            if (helmetHUD == null || activeDay == null) return;

            int missionCount = activeDay.missions != null ? activeDay.missions.Count : 0;
            int incompleteCount = 0;
            int firstIncompleteMissionIdx = -1;
            for (int i = 0; i < missionCount; i++)
            {
                if (!activeDay.missions[i].isCompleted)
                {
                    incompleteCount++;
                    if (firstIncompleteMissionIdx == -1) firstIncompleteMissionIdx = i;
                }
            }

            // =========================================================================
            // PHASE 1: PRE-ACCEPTANCE / CORPSE RECOVERY (ก่อนรับภารกิจ / กู้ข้อมูลจากศพ)
            // =========================================================================
            if (hasCorpseToRecover && !isCorpseDataRecoveredThisLoop)
            {
                // Fallen astronaut loop recovery: Must first collect flight telemetry from corpse
                string[] tasks = new string[] { "Recover Mission Data from Fallen Corpse (128.0 MHz)" };
                bool[] completed = new bool[] { false };
                string header = $"DAY {currentDay} // CORPSE RECOVERY";
                helmetHUD.SetObjectiveChecklist(currentDay, tasks, completed, 0, header);
                return;
            }

            if (!isTerminalContractAccepted)
            {
                // Normal fresh day start: Must first access ship terminal to accept contract
                string[] tasks = new string[] { "Access Ship Mission Terminal" };
                bool[] completed = new bool[] { false };
                string header = $"DAY {currentDay} // PREPARATION";
                helmetHUD.SetObjectiveChecklist(currentDay, tasks, completed, 0, header);
                return;
            }

            // =========================================================================
            // PHASE 2: ACTIVE EXECUTION (รับภารกิจแล้ว / กำลังทำภารกิจในอวกาศตามที่ตั้งค่าไว้)
            // =========================================================================
            if (incompleteCount > 0)
            {
                string[] tasks = new string[missionCount];
                bool[] completed = new bool[missionCount];

                for (int i = 0; i < missionCount; i++)
                {
                    var m = activeDay.missions[i];
                    string freqInfo = (m != null && m.frequencyMHz > 0) ? $" ({m.frequencyMHz:F1} MHz)" : "";
                    string directive = m != null ? m.directiveText : "Complete Space Mission";
                    tasks[i] = $"{directive}{freqInfo}";
                    completed[i] = (m != null && m.isCompleted);
                }

                int activeIndex = firstIncompleteMissionIdx;
                string header = $"DAY {currentDay} // EXPEDITION";
                helmetHUD.SetObjectiveChecklist(currentDay, tasks, completed, activeIndex, header);
                return;
            }

            // =========================================================================
            // PHASE 3: EXTRACTION / TURN-IN (ภารกิจครบแล้ว / กลับมาส่งภารกิจที่ยาน)
            // =========================================================================
            {
                string[] tasks = new string[] { "Return to Ship Airlock (121.5 MHz)" };
                bool[] completed = new bool[] { isDayConcluding };
                int activeIndex = isDayConcluding ? -1 : 0;
                string header = $"DAY {currentDay} // EXTRACTION";
                helmetHUD.SetObjectiveChecklist(currentDay, tasks, completed, activeIndex, header);
            }
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
                        m.targetBeacon.SetTransmitting(true);
                        m.targetBeacon.enabled = true;
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
                mission.targetBeacon.SetTransmitting(false);
                mission.targetBeacon.enabled = false;
            }

            // Count completed objectives
            int completedCount = 0;
            int totalCount = activeDay.missions != null ? activeDay.missions.Count : 0;
            for (int i = 0; i < totalCount; i++)
            {
                if (activeDay.missions[i].isCompleted) completedCount++;
            }

            bool allMissionsDone = completedCount >= totalCount;
            bool corpseDone = !hasCorpseToRecover || isCorpseDataRecoveredThisLoop;

            // Notify HorrorEventManager of objective completion for scripted horror events (e.g. Rupture on final station)
            bool isHorrorEventTriggered = false;
            if (HorrorEventManager.Instance != null)
            {
                HorrorEventManager.Instance.HandleObjectiveCompleted(currentDay, mission, player, allMissionsDone);
                if (currentDay == 2 && HorrorEventManager.Instance.HasTriggeredO2Explosion)
                {
                    isHorrorEventTriggered = true;
                }
            }

            if (allMissionsDone && corpseDone)
            {
                // All objectives for the day (missions + corpse data) completed!
                currentState = ExpeditionState.EVA_ObjectiveCompleted;
                UpdateHUDObjectiveCard();

                if (helmetHUD != null && !isHorrorEventTriggered)
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
            else
            {
                // Still more objectives remaining in outer space
                UpdateHUDObjectiveCard();
                if (helmetHUD != null)
                {
                    if (!corpseDone)
                    {
                        helmetHUD.ShowInteractionStatusMessage($"OBJECTIVE SECURED ({completedCount}/{totalCount}) // RECOVER FALLEN TELEMETRY [128.0 MHz]", 4.0f);
                    }
                    else
                    {
                        helmetHUD.ShowInteractionStatusMessage($"OBJECTIVE SECURED ({completedCount}/{totalCount}) // PROCEED TO NEXT TARGET", 4.0f);
                    }
                }
                Debug.Log($"<color=#FFFFFF><b>[DayCycleManager]</b></color> Objective secured ({completedCount}/{totalCount}) for DAY {currentDay}");
            }
        }

        /// <summary>
        /// Recovers mission telemetry from an external source (such as the player's fallen corpse from a previous loop).
        /// Secures the flight recorder telemetry so the astronaut can continue with the remaining day's mission objectives.
        /// </summary>
        public bool TryRecoverMissionData(string sourceName = "Fallen Astronaut")
        {
            if (activeDay == null) return false;

            hasCorpseToRecover = false;
            isCorpseDataRecoveredThisLoop = true;

            int incompleteCount = 0;
            int totalCount = activeDay.missions != null ? activeDay.missions.Count : 0;
            if (activeDay.missions != null)
            {
                for (int i = 0; i < totalCount; i++)
                {
                    if (!activeDay.missions[i].isCompleted)
                    {
                        incompleteCount++;
                        if (activeDay.missions[i].targetBeacon != null)
                        {
                            activeDay.missions[i].targetBeacon.SetTransmitting(true);
                            activeDay.missions[i].targetBeacon.enabled = true;
                        }
                    }
                }
            }

            if (incompleteCount > 0)
            {
                // Telemetry secured: astronaut continues pending space missions
                currentState = ExpeditionState.EVA_InProgress;
                UpdateHUDObjectiveCard();
                if (helmetHUD != null)
                {
                    helmetHUD.ShowInteractionStatusMessage("CORPSE DATA SECURED // PROCEED WITH MISSION DIRECTIVE", 4.5f);
                }
                Debug.Log($"<color=#55FF55><b>[DayCycleManager]</b></color> Corpse telemetry recovered from {sourceName}. {incompleteCount} mission(s) pending.");
            }
            else
            {
                // All missions were already completed prior to death: astronaut can now return to airlock
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
                Debug.Log($"<color=#55FF55><b>[DayCycleManager]</b></color> Corpse telemetry recovered from {sourceName}. All missions completed! Return to Ship.");
            }

            return true;
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
        /// Public entry point called by AstronautSurvival upon death to trigger loop respawn.
        /// </summary>
        public void NotifyPlayerAsphyxiated()
        {
            HandlePlayerAsphyxiated();
        }

        /// <summary>
        /// Handles player asphyxiation death in outer space:
        /// Rescues player back to ship cryo bay and retries the day with corpse recovery objective.
        /// </summary>
        private void HandlePlayerAsphyxiated()
        {
            if (dayTransitionCoroutine != null) return;
            hasCorpseToRecover = true;
            isCorpseDataRecoveredThisLoop = false;
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
                helmetHUD.ShowMissionTransmission("OXYGEN DEPLETED", "LIFE SUPPORT OFFLINE // EMERGENCY RECOVERY TO SHIP CRYOPOD...", 3.0f);
            }

            yield return new WaitForSeconds(1.2f);

            // 3. Teleport immediately to spawn point
            TeleportPlayerToSpawn();

            yield return new WaitForSeconds(0.6f);

            // 4. Mark loop recovery: Player died and must recover mission data from corpse
            hasCorpseToRecover = true;
            isCorpseDataRecoveredThisLoop = false;

            // Restart current day inside ship (refills O2 & suit, sets top-left corpse recovery mission, triggers wake-up)
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
        /// Resets all world-space survival pickups (O2 canisters, suit repair stations, etc.)
        /// based on their resetOnNewDay / resetOnLoop configurations.
        /// </summary>
        private void ResetSurvivalInteractables(bool isLoopRevival)
        {
            MissionInteractable[] allInteractables = FindObjectsByType<MissionInteractable>(FindObjectsSortMode.None);
            for (int i = 0; i < allInteractables.Length; i++)
            {
                MissionInteractable item = allInteractables[i];
                if (item == null) continue;

                // Check if this interactable is an active objective of the day
                bool isMissionTarget = false;
                if (activeDay != null && activeDay.missions != null)
                {
                    for (int m = 0; m < activeDay.missions.Count; m++)
                    {
                        if (activeDay.missions[m].targetInteractable == item)
                        {
                            isMissionTarget = true;
                            break;
                        }
                    }
                }

                // Mission targets during loop recovery are handled separately to preserve completed objectives
                if (isMissionTarget && isLoopRevival)
                {
                    continue;
                }

                // Check regeneration flags on the interactable
                bool shouldRegen = isLoopRevival ? item.ResetOnLoop : item.ResetOnNewDay;
                if (shouldRegen)
                {
                    item.ResetObjective();
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
                Transform rootTransform = targetTransform.root;
                Rigidbody rb = rootTransform.GetComponent<Rigidbody>();
                if (rb == null) rb = targetTransform.GetComponent<Rigidbody>();

                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.position = spawnPoint.position;
                    rb.rotation = spawnPoint.rotation;
                }

                rootTransform.position = spawnPoint.position;
                rootTransform.rotation = spawnPoint.rotation;
                if (targetTransform != rootTransform)
                {
                    targetTransform.position = spawnPoint.position;
                    targetTransform.rotation = spawnPoint.rotation;
                }
                Physics.SyncTransforms();

                Debug.Log($"<color=#55FF55><b>[DayCycleManager]</b></color> Astronaut teleported to Spawnpoint '{spawnPoint.name}' at {spawnPoint.position}");
            }
        }
    }
}
