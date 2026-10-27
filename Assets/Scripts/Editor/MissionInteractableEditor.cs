using UnityEditor;
using UnityEngine;
using SpaceHorror.Interaction;

namespace SpaceHorror.Editor
{
    /// <summary>
    /// Custom Inspector for MissionInteractable.
    /// Dynamically reveals only relevant fields based on the selected ObjectiveType and InteractionMethod.
    /// Keeps the Unity Inspector clean, contextual, and designer-friendly.
    /// </summary>
    [CustomEditor(typeof(MissionInteractable))]
    [CanEditMultipleObjects]
    public class MissionInteractableEditor : UnityEditor.Editor
    {
        // 1. Identity & Prompts
        private SerializedProperty propTitle;
        private SerializedProperty propPrompt;
        private SerializedProperty propCompletionMsg;

        // 2. Interaction Configuration
        private SerializedProperty propType;
        private SerializedProperty propMethod;
        private SerializedProperty propHoldDuration;
        private SerializedProperty propSingleUse;
        private SerializedProperty propResetOnNewDay;
        private SerializedProperty propResetOnLoop;

        // 3. Objective-Specific Parameters
        private SerializedProperty propOxygen;
        private SerializedProperty propSuitRepair;
        private SerializedProperty propTranscript;
        private SerializedProperty propAudioClip;
        private SerializedProperty propToggleTarget;

        // 4. World Space & Visual Feedback
        private SerializedProperty propPromptAnchor;
        private SerializedProperty propPromptOffset;
        private SerializedProperty propOutlineWidth;
        private SerializedProperty propHighlightColor;
        private SerializedProperty propPulseOutline;
        private SerializedProperty propVisualRenderer;
        private SerializedProperty propCustomOutlineMaterial;

        // 5. Designer Callbacks
        private SerializedProperty propOnInteracted;
        private SerializedProperty propOnHoldProgress;

        private void OnEnable()
        {
            // Identity
            propTitle = serializedObject.FindProperty("objectiveTitle");
            propPrompt = serializedObject.FindProperty("actionPrompt");
            propCompletionMsg = serializedObject.FindProperty("completionMessage");

            // Config
            propType = serializedObject.FindProperty("objectiveType");
            propMethod = serializedObject.FindProperty("interactionMethod");
            propHoldDuration = serializedObject.FindProperty("holdDuration");
            propSingleUse = serializedObject.FindProperty("isSingleUse");
            propResetOnNewDay = serializedObject.FindProperty("resetOnNewDay");
            propResetOnLoop = serializedObject.FindProperty("resetOnLoop");

            // Type parameters
            propOxygen = serializedObject.FindProperty("oxygenAmount");
            propSuitRepair = serializedObject.FindProperty("suitRepairAmount");
            propTranscript = serializedObject.FindProperty("dataLogTranscript");
            propAudioClip = serializedObject.FindProperty("customAudioLogClip");
            propToggleTarget = serializedObject.FindProperty("targetObjectToToggle");

            // World space & outline
            propPromptAnchor = serializedObject.FindProperty("promptAnchor");
            propPromptOffset = serializedObject.FindProperty("promptOffset");
            propOutlineWidth = serializedObject.FindProperty("outlineWidth");
            propHighlightColor = serializedObject.FindProperty("highlightColor");
            propPulseOutline = serializedObject.FindProperty("pulseOutline");
            propVisualRenderer = serializedObject.FindProperty("visualRenderer");
            propCustomOutlineMaterial = serializedObject.FindProperty("customOutlineMaterial");

            // Events
            propOnInteracted = serializedObject.FindProperty("onInteracted");
            propOnHoldProgress = serializedObject.FindProperty("onHoldProgress");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // --- 1. Objective Identity & Prompts ---
            EditorGUILayout.LabelField("Objective Identity & Prompts", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(propTitle, new GUIContent("Objective Title", "Name of the mission station / target displayed under the action verb"));
            EditorGUILayout.PropertyField(propPrompt, new GUIContent("Action Verb", "Action prompt verb (e.g. 'REFILL OXYGEN', 'DOWNLOAD LOG', 'OVERRIDE AIRLOCK')"));
            EditorGUILayout.PropertyField(propCompletionMsg, new GUIContent("Completion Message", "Brief confirmation message toast shown upon completion"));
            EditorGUILayout.Space(6);

            // --- 2. Interaction Configuration ---
            EditorGUILayout.LabelField("Interaction Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(propType, new GUIContent("Objective Type", "The gameplay behavior executed when interacted with"));

            EditorGUILayout.PropertyField(propMethod, new GUIContent("Interaction Method", "Single tap [LMB] vs holding down [LMB] with progress ring"));

            // Only show holdDuration if HoldToComplete is chosen
            if (propMethod.enumValueIndex == (int)InteractionMethod.HoldToComplete)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propHoldDuration, new GUIContent("Hold Duration (sec)", "Time in seconds the player must hold down the interact button"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(propSingleUse, new GUIContent("Is Single Use", "If true, interactable completes once, immediately extinguishes its outline, and ignores future targeting"));

            if (propSingleUse.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propResetOnNewDay, new GUIContent("Reset On New Day", "Automatically resets this interactable when starting a fresh expedition day (normal completion / sleep)"));
                EditorGUILayout.PropertyField(propResetOnLoop, new GUIContent("Reset On Loop", "Automatically resets this interactable when reviving from death in a loop cycle (corpse recovery)"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space(6);

            // --- 3. Dynamic Objective Parameters ---
            ObjectiveType currentType = (ObjectiveType)propType.enumValueIndex;
            EditorGUILayout.LabelField($"Objective Parameters: {currentType}", EditorStyles.boldLabel);

            switch (currentType)
            {
                case ObjectiveType.RefillOxygen:
                    EditorGUILayout.PropertyField(propOxygen, new GUIContent("Oxygen Restored (%)", "Amount of oxygen replenishment given to the player"));
                    break;

                case ObjectiveType.RepairSuit:
                    EditorGUILayout.PropertyField(propSuitRepair, new GUIContent("Suit Restored (%)", "Hull integrity restored to the astronaut suit to halt leaks"));
                    break;

                case ObjectiveType.CollectDataLog:
                    EditorGUILayout.PropertyField(propTranscript, new GUIContent("Log Transcript", "Transmission log text displayed in the black box window"));
                    EditorGUILayout.PropertyField(propAudioClip, new GUIContent("Audio Clip", "Optional transmission audio file played upon collection"));
                    break;

                case ObjectiveType.TogglePowerOrDoor:
                    EditorGUILayout.PropertyField(propToggleTarget, new GUIContent("Target Object To Toggle", "GameObject to switch between Active and Inactive upon interaction"));
                    break;

                case ObjectiveType.Custom:
                    EditorGUILayout.HelpBox("Custom Objective: No preset logic is executed. Use the 'On Interacted' UnityEvent below to trigger your own game logic.", MessageType.Info);
                    break;
            }
            EditorGUILayout.Space(6);

            // --- 4. World-Space Positioning & Outline ---
            EditorGUILayout.LabelField("World-Space Positioning & Outline", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(propPromptAnchor, new GUIContent("Prompt Anchor", "Optional transform for prompt anchor. If null, automatically uses closest surface point facing player."));
            EditorGUILayout.PropertyField(propPromptOffset, new GUIContent("Prompt Offset", "Offset added along the astronaut's visor UP axis and forward distance"));
            EditorGUILayout.PropertyField(propOutlineWidth, new GUIContent("Outline Width", "Thickness of the inverted hull outer silhouette border"));
            EditorGUILayout.PropertyField(propHighlightColor, new GUIContent("Highlight Color", "Color of the outer border outline"));
            EditorGUILayout.PropertyField(propPulseOutline, new GUIContent("Pulse Outline", "Gently breathes outline thickness as an atmospheric cue"));
            EditorGUILayout.PropertyField(propVisualRenderer, new GUIContent("Visual Renderer", "Optional specific renderer. If null, automatically outlines all meshes on this object & children."));
            EditorGUILayout.PropertyField(propCustomOutlineMaterial, new GUIContent("Custom Outline Material", "Optional override material. If null, automatically loads Resources/Materials/M_InteractableOutline."));
            EditorGUILayout.Space(6);

            // --- 5. Designer Callbacks ---
            EditorGUILayout.LabelField("Designer Callbacks", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(propOnInteracted, new GUIContent("On Interacted (GameObject)"));

            // Only show OnHoldProgress if HoldToComplete is chosen
            if (propMethod.enumValueIndex == (int)InteractionMethod.HoldToComplete)
            {
                EditorGUILayout.PropertyField(propOnHoldProgress, new GUIContent("On Hold Progress (float 0..1)"));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
