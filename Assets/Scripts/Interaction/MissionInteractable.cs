using System;
using UnityEngine;
using UnityEngine.Events;
using SpaceHorror.Player;

namespace SpaceHorror.Interaction
{
    public enum ObjectiveType
    {
        Custom,                 // Custom behavior via UnityEvent
        RefillOxygen,           // Refills astronaut oxygen tank
        RepairSuit,             // Patches suit hull integrity and halts leaks
        CollectDataLog,         // Plays transmission/audio diary and displays text
        TogglePowerOrDoor       // Activates power generator or toggles airlock/door
    }

    public enum InteractionMethod
    {
        InstantPress,           // Single tap [E]
        HoldToComplete          // Hold [E] for specified duration with circular/bar progress
    }

    /// <summary>
    /// Modular Mission & Interaction Component.
    /// Can be attached to ANY GameObject in the scene (O2 stations, terminals, valves, debris, airlocks).
    /// Configure objective type, prompts, and hold duration directly via the Inspector.
    /// </summary>
    public class MissionInteractable : MonoBehaviour
    {
        [Header("Objective Identity & Prompts")]
        [Tooltip("Name of the mission target / station")]
        [SerializeField] private string objectiveTitle = "Emergency O2 Station";

        [Tooltip("Action verb prompt displayed on HUD (e.g. 'REFILL OXYGEN', 'DOWNLOAD LOG', 'OVERRIDE AIRLOCK')")]
        [SerializeField] private string actionPrompt = "REFILL OXYGEN";

        [Tooltip("Completion message shown briefly on visor HUD")]
        [SerializeField] private string completionMessage = "LIFE SUPPORT RECHARGED +40%";

        [Header("Interaction Configuration")]
        [Tooltip("Type of mission task")]
        [SerializeField] private ObjectiveType objectiveType = ObjectiveType.RefillOxygen;

        [Tooltip("Instant press vs. holding [E] down")]
        [SerializeField] private InteractionMethod interactionMethod = InteractionMethod.HoldToComplete;

        [Tooltip("Time in seconds to hold [E] (if HoldToComplete is chosen)")]
        [Range(0.2f, 8f)]
        [SerializeField] private float holdDuration = 1.5f;

        [Tooltip("Can this objective only be completed once, or repeatedly?")]
        [SerializeField] private bool isSingleUse = true;

        [Header("Objective Type Parameters")]
        [Tooltip("Amount of oxygen restored (if RefillOxygen)")]
        [Range(5f, 100f)]
        [SerializeField] private float oxygenAmount = 40f;

        [Tooltip("Amount of suit integrity restored (if RepairSuit)")]
        [Range(5f, 100f)]
        [SerializeField] private float suitRepairAmount = 35f;

        [Tooltip("Transmission / Audio log transcript (if CollectDataLog)")]
        [TextArea(3, 6)]
        [SerializeField] private string dataLogTranscript = "TRANSMISSION LOG #07:\n'Deep space anomaly detected near reactor core. Recommend immediate emergency lockdown...'";

        [Tooltip("Optional audio clip to play during transmission")]
        [SerializeField] private AudioClip customAudioLogClip;

        [Tooltip("Target GameObject to toggle active/inactive (if TogglePowerOrDoor)")]
        [SerializeField] private GameObject targetObjectToToggle;

        [Header("Visual Feedback & Highlighting")]
        [Tooltip("Optional MeshRenderer to highlight/glow when aimed at")]
        [SerializeField] private Renderer visualRenderer;

        [Tooltip("Highlight emission color when player looks at this interactable")]
        [SerializeField] private Color highlightColor = new Color(0.92f, 0.95f, 1.0f);

        [Header("Designer Callbacks (Unity Events)")]
        [Tooltip("Fires when the interaction successfully completes")]
        public UnityEvent<GameObject> onInteracted;

        [Tooltip("Fires each frame during hold interaction with progress (0.0 to 1.0)")]
        public UnityEvent<float> onHoldProgress;

        // Runtime states
        private bool isCompleted = false;
        private Material originalMaterial;
        private Color originalColor;
        private bool isHighlighted = false;

        // Getters
        public string ObjectiveTitle => objectiveTitle;
        public string ActionPrompt => actionPrompt;
        public string CompletionMessage => completionMessage;
        public ObjectiveType Type => objectiveType;
        public InteractionMethod Method => interactionMethod;
        public float HoldDuration => holdDuration;
        public bool IsSingleUse => isSingleUse;
        public bool IsCompleted => isCompleted;
        public bool CanInteract => !isCompleted || !isSingleUse;
        public string DataLogTranscript => dataLogTranscript;
        public AudioClip AudioLogClip => customAudioLogClip;

        private void Awake()
        {
            if (visualRenderer != null && visualRenderer.material != null)
            {
                originalMaterial = visualRenderer.material;
                if (originalMaterial.HasProperty("_BaseColor"))
                {
                    originalColor = originalMaterial.GetColor("_BaseColor");
                }
                else if (originalMaterial.HasProperty("_Color"))
                {
                    originalColor = originalMaterial.GetColor("_Color");
                }
            }
        }

        /// <summary>
        /// Highlights or unhighlights the target object when player reticle is looking at it.
        /// </summary>
        public void SetHighlight(bool active)
        {
            if (isHighlighted == active || isCompleted && isSingleUse) return;
            isHighlighted = active;

            if (visualRenderer == null || visualRenderer.material == null) return;

            if (active)
            {
                if (visualRenderer.material.HasProperty("_EmissionColor"))
                {
                    visualRenderer.material.EnableKeyword("_EMISSION");
                    visualRenderer.material.SetColor("_EmissionColor", highlightColor * 1.5f);
                }
            }
            else
            {
                if (visualRenderer.material.HasProperty("_EmissionColor"))
                {
                    visualRenderer.material.SetColor("_EmissionColor", Color.black);
                }
            }
        }

        /// <summary>
        /// Invoked when the interaction hold finishes or instant press happens.
        /// </summary>
        public void TriggerInteraction(GameObject player)
        {
            if (!CanInteract) return;

            // Execute based on objective type
            switch (objectiveType)
            {
                case ObjectiveType.RefillOxygen:
                    var survival = player.GetComponent<AstronautSurvival>();
                    if (survival != null)
                    {
                        survival.ReplenishOxygen(oxygenAmount);
                    }
                    break;

                case ObjectiveType.RepairSuit:
                    var suit = player.GetComponent<AstronautSurvival>();
                    if (suit != null)
                    {
                        suit.RepairSuit(suitRepairAmount);
                    }
                    break;

                case ObjectiveType.TogglePowerOrDoor:
                    if (targetObjectToToggle != null)
                    {
                        targetObjectToToggle.SetActive(!targetObjectToToggle.activeSelf);
                    }
                    break;

                case ObjectiveType.CollectDataLog:
                    // Audio log can be played via audio source
                    if (customAudioLogClip != null)
                    {
                        AudioSource.PlayClipAtPoint(customAudioLogClip, transform.position, 1.0f);
                    }
                    break;

                case ObjectiveType.Custom:
                    break;
            }

            if (isSingleUse)
            {
                isCompleted = true;
                SetHighlight(false);
            }

            // Invoke designer UnityEvent
            onInteracted?.Invoke(player);
        }

        /// <summary>
        /// Reports hold progress (0.0 to 1.0) to custom listeners or visual effects.
        /// </summary>
        public void ReportProgress(float progress)
        {
            onHoldProgress?.Invoke(progress);
        }

        /// <summary>
        /// Resets the objective state to be interactable again.
        /// </summary>
        public void ResetObjective()
        {
            isCompleted = false;
        }
    }
}
