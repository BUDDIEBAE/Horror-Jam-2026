using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using SpaceHorror.Player;
using SpaceHorror.Gameplay;

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

        [Header("Mission Contract Binding")]
        [Tooltip("If true, this object can ONLY be interacted with when targeted by an active mission in DayCycleManager for the current day.\nIf false, acts as an ambient survival pickup (e.g. loose O2 canister) usable anytime.")]
        [SerializeField] private bool requiresActiveMission = false;

        [Tooltip("Feedback prompt shown on visor when aimed at while locked/unauthorized (e.g. 'STANDBY // NO CONTRACT AUTHORIZATION')")]
        [SerializeField] private string unauthorizedPrompt = "STANDBY // NO CONTRACT AUTHORIZATION";

        [Header("Loop & Day Regeneration")]
        [Tooltip("Automatically resets this interactable when starting a fresh expedition day (normal completion / sleep)")]
        [SerializeField] private bool resetOnNewDay = true;

        [Tooltip("Automatically resets this interactable when reviving from death in a loop cycle (corpse recovery)")]
        [SerializeField] private bool resetOnLoop = true;

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

        [Header("World-Space Prompt Anchor")]
        [Tooltip("Optional custom transform where the floating text should anchor. If null, calculates top center of mesh/collider.")]
        [SerializeField] private Transform promptAnchor;

        [Tooltip("Offset in world units added to the prompt position")]
        [SerializeField] private Vector3 promptOffset = new Vector3(0f, 0.40f, 0f);

        [Header("Outer Silhouette Outline")]
        [Tooltip("Thickness of the outer border outline in world units (e.g. 0.025 = 2.5cm)")]
        [Range(0.005f, 0.08f)]
        [SerializeField] private float outlineWidth = 0.025f;

        [Tooltip("Color of the outer border outline")]
        [SerializeField] private Color highlightColor = new Color(0.35f, 0.85f, 1.0f, 1.0f);

        [Tooltip("Pulse the outline thickness gently as a breathing cue")]
        [SerializeField] private bool pulseOutline = true;

        [Tooltip("Optional MeshRenderer to outline. If null, gathers all renderers on this object/children.")]
        [SerializeField] private Renderer visualRenderer;

        [Tooltip("Optional custom outline material. If null, automatically uses Resources/Materials/M_InteractableOutline.")]
        [SerializeField] private Material customOutlineMaterial;

        [Header("Designer Callbacks (Unity Events)")]
        [Tooltip("Fires when the interaction successfully completes")]
        public UnityEvent<GameObject> onInteracted = new UnityEvent<GameObject>();

        [Tooltip("Fires each frame during hold interaction with progress (0.0 to 1.0)")]
        public UnityEvent<float> onHoldProgress = new UnityEvent<float>();

        // Runtime states
        private bool isCompleted = false;
        private bool isHighlighted = false;
        private Renderer[] targetRenderers;
        private MaterialPropertyBlock propBlock;

        private struct OutlineEntry
        {
            public GameObject outlineObj;
            public Renderer outlineRend;
        }

        private readonly List<OutlineEntry> outlineEntries = new List<OutlineEntry>();
        private static Material sharedOutlineMaterial;
        private static readonly int PropOutlineColor = Shader.PropertyToID("_OutlineColor");
        private static readonly int PropOutlineWidth = Shader.PropertyToID("_OutlineWidth");

        // Getters & Setters
        public string ObjectiveTitle => objectiveTitle;
        public string ActionPrompt => actionPrompt;
        public string CompletionMessage => completionMessage;
        public ObjectiveType Type { get => objectiveType; set => objectiveType = value; }
        public InteractionMethod Method { get => interactionMethod; set => interactionMethod = value; }
        public float HoldDuration { get => holdDuration; set => holdDuration = value; }
        public bool IsSingleUse
        {
            get => isSingleUse;
            set => isSingleUse = value;
        }
        public bool IsCompleted => isCompleted;
        public bool RequiresActiveMission
        {
            get => requiresActiveMission;
            set => requiresActiveMission = value;
        }
        public string UnauthorizedPrompt => unauthorizedPrompt;

        public bool CanInteract
        {
            get
            {
                // 1. Single-use completion check
                if (isCompleted && isSingleUse) return false;

                // 2. Ambient survival pickups (loose O2, repair patches, ship doors) are always accessible
                if (!requiresActiveMission) return true;

                // 3. Mission-bound objectives require verification against DayCycleManager
                if (DayCycleManager.Instance != null)
                {
                    return DayCycleManager.Instance.IsMissionActiveToday(this);
                }

                return false;
            }
        }

        public bool ResetOnNewDay { get => resetOnNewDay; set => resetOnNewDay = value; }
        public bool ResetOnLoop { get => resetOnLoop; set => resetOnLoop = value; }
        public string DataLogTranscript => dataLogTranscript;
        public AudioClip AudioLogClip => customAudioLogClip;

        /// <summary>
        /// Updates the prompts dynamically at runtime (e.g. when state changes from 'Accept Contract' to 'In Progress').
        /// </summary>
        public void ConfigureRuntimePrompts(string title, string prompt, string completionMsg = "")
        {
            objectiveTitle = title;
            actionPrompt = prompt;
            if (!string.IsNullOrEmpty(completionMsg))
            {
                completionMessage = completionMsg;
            }
        }

        /// <summary>
        /// Configures interaction behavior and duration dynamically at runtime.
        /// </summary>
        public void ConfigureInteractionMethod(InteractionMethod method, float duration = 1.5f, ObjectiveType type = ObjectiveType.Custom)
        {
            interactionMethod = method;
            holdDuration = duration;
            objectiveType = type;
        }

        private static Material GetOrCreateOutlineMaterial()
        {
            if (sharedOutlineMaterial != null) return sharedOutlineMaterial;

            // 1. Load pre-packaged Material asset from Resources (guaranteed to be included in Standalone Builds)
            sharedOutlineMaterial = Resources.Load<Material>("Materials/M_InteractableOutline");
            if (sharedOutlineMaterial != null) return sharedOutlineMaterial;

            sharedOutlineMaterial = Resources.Load<Material>("M_InteractableOutline");
            if (sharedOutlineMaterial != null) return sharedOutlineMaterial;

            // 2. Try Shader.Find if in Editor or included in Always Included Shaders
            Shader shader = Shader.Find("SpaceHorror/PSX_InteractableOutline");
            if (shader != null)
            {
                sharedOutlineMaterial = new Material(shader);
                sharedOutlineMaterial.name = "M_PSX_InteractableOutline";
                return sharedOutlineMaterial;
            }

            // 3. Fallback to standard URP unlit shader if custom shader missing
            Shader fallbackShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (fallbackShader != null)
            {
                sharedOutlineMaterial = new Material(fallbackShader);
                sharedOutlineMaterial.name = "M_PSX_Outline_Fallback";
                return sharedOutlineMaterial;
            }

            Debug.LogWarning("[MissionInteractable] Outline shader/material could not be located in build!");
            return null;
        }

        private void Awake()
        {
            if (onInteracted == null) onInteracted = new UnityEvent<GameObject>();
            if (onHoldProgress == null) onHoldProgress = new UnityEvent<float>();
            propBlock = new MaterialPropertyBlock();

            if (visualRenderer != null)
            {
                targetRenderers = new Renderer[] { visualRenderer };
            }
            else
            {
                targetRenderers = GetComponentsInChildren<Renderer>();
            }

            SetupOutlineShells();
        }

        private void SetupOutlineShells()
        {
            Material outlineMat = customOutlineMaterial != null ? customOutlineMaterial : GetOrCreateOutlineMaterial();
            if (outlineMat == null || targetRenderers == null) return;

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer rend = targetRenderers[i];
                if (rend == null) continue;

                Mesh mesh = null;
                MeshFilter mf = rend.GetComponent<MeshFilter>();
                if (mf != null)
                {
                    mesh = mf.sharedMesh;
                }

                if (mesh == null) continue;

                GameObject outlineObj = new GameObject("_Outline_Shell");
                outlineObj.transform.SetParent(rend.transform, false);
                outlineObj.transform.localPosition = Vector3.zero;
                outlineObj.transform.localRotation = Quaternion.identity;
                outlineObj.transform.localScale = Vector3.one;

                MeshFilter outlineMf = outlineObj.AddComponent<MeshFilter>();
                outlineMf.sharedMesh = mesh;

                MeshRenderer outlineRend = outlineObj.AddComponent<MeshRenderer>();

                // Ensure all submeshes receive the outline material
                int subMeshCount = mesh.subMeshCount;
                if (subMeshCount > 1)
                {
                    Material[] mats = new Material[subMeshCount];
                    for (int m = 0; m < subMeshCount; m++) mats[m] = outlineMat;
                    outlineRend.sharedMaterials = mats;
                }
                else
                {
                    outlineRend.sharedMaterial = outlineMat;
                }

                outlineRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outlineRend.receiveShadows = false;

                outlineObj.SetActive(false);

                outlineEntries.Add(new OutlineEntry
                {
                    outlineObj = outlineObj,
                    outlineRend = outlineRend
                });
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < outlineEntries.Count; i++)
            {
                if (outlineEntries[i].outlineObj != null)
                {
                    Destroy(outlineEntries[i].outlineObj);
                }
            }
            outlineEntries.Clear();
        }

        private void Update()
        {
            if (!CanInteract && isHighlighted)
            {
                SetHighlight(false);
                return;
            }

            if (isHighlighted && CanInteract && pulseOutline)
            {
                // Soft breathing / pulsing edge width
                float pulse = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f; // 0.0 to 1.0
                float currentWidth = Mathf.Lerp(outlineWidth * 0.85f, outlineWidth * 1.25f, pulse);
                UpdateOutlineProperties(currentWidth);
            }
        }

        /// <summary>
        /// Calculates the optimal floating position in 3D world space for the prompt,
        /// dynamically adapting in real time to the player's camera position and zero-g orientation.
        /// Finds the outer surface point facing the astronaut, keeping the prompt cleanly on the visible side.
        /// </summary>
        public Vector3 GetPromptWorldPosition(Vector3 cameraPosition, Vector3 cameraUp)
        {
            if (promptAnchor != null)
            {
                Vector3 toCamFromAnchor = (cameraPosition - promptAnchor.position).normalized;
                return promptAnchor.position + (cameraUp * promptOffset.y) + (toCamFromAnchor * 0.08f);
            }

            Collider col = GetComponentInChildren<Collider>();
            if (col != null)
            {
                // Real-time outer surface point closest to player camera
                Vector3 closestPoint = col.ClosestPoint(cameraPosition);
                Vector3 toCam = (cameraPosition - closestPoint).normalized;

                float verticalDist = promptOffset.y > 0.01f ? promptOffset.y : 0.35f;
                float forwardDist = 0.10f; // slight hover towards player to prevent z-fighting

                return closestPoint + (cameraUp * verticalDist) + (toCam * forwardDist);
            }

            Renderer rend = visualRenderer != null ? visualRenderer : GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                Vector3 center = rend.bounds.center;
                Vector3 dirToCam = (cameraPosition - center).normalized;
                float radius = rend.bounds.extents.magnitude * 0.45f;
                Vector3 surfaceApprox = center + dirToCam * radius;

                float verticalDist = promptOffset.y > 0.01f ? promptOffset.y : 0.35f;
                return surfaceApprox + (cameraUp * verticalDist) + (dirToCam * 0.10f);
            }

            return transform.position + promptOffset;
        }

        public Vector3 GetPromptWorldPosition()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                return GetPromptWorldPosition(cam.transform.position, cam.transform.up);
            }
            return transform.position + promptOffset;
        }

        /// <summary>
        /// Highlights or unhighlights the outer border outline when reticle aims at it.
        /// Never highlights if the interactable is single-use and already completed.
        /// </summary>
        public void SetHighlight(bool active)
        {
            if (active && !CanInteract)
            {
                active = false;
            }

            if (isHighlighted == active) return;
            isHighlighted = active;

            for (int i = 0; i < outlineEntries.Count; i++)
            {
                if (outlineEntries[i].outlineObj != null)
                {
                    outlineEntries[i].outlineObj.SetActive(active);
                }
            }

            if (active)
            {
                UpdateOutlineProperties(outlineWidth);
            }
        }

        private void UpdateOutlineProperties(float width)
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();

            for (int i = 0; i < outlineEntries.Count; i++)
            {
                Renderer rend = outlineEntries[i].outlineRend;
                if (rend == null) continue;

                rend.GetPropertyBlock(propBlock);
                propBlock.SetColor(PropOutlineColor, highlightColor);
                propBlock.SetFloat(PropOutlineWidth, width);
                rend.SetPropertyBlock(propBlock);
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

                // Shut down radio beacon transmission if attached
                SignalBeacon beacon = GetComponent<SignalBeacon>();
                if (beacon != null && beacon.DeactivateOnMissionComplete)
                {
                    beacon.SetTransmitting(false);
                }
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
            SetHighlight(false);

            SignalBeacon beacon = GetComponent<SignalBeacon>();
            if (beacon != null)
            {
                beacon.SetTransmitting(true);
            }
        }
    }
}
