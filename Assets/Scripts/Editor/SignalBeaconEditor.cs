using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using SpaceHorror.Interaction;

namespace SpaceHorror.Editor
{
    /// <summary>
    /// Custom Inspector for SignalBeacon with 1-Click "Save Custom Channel as New Preset Enum".
    /// Allows level designers to enter custom Tag, Title, and Frequency, and automatically append
    /// them directly into the SignalChannelPreset enum and extension methods!
    /// </summary>
    [CustomEditor(typeof(SignalBeacon))]
    [CanEditMultipleObjects]
    public class SignalBeaconEditor : UnityEditor.Editor
    {
        private SerializedProperty propPreset;
        private SerializedProperty propCustomOverride;
        private SerializedProperty propBeaconTag;
        private SerializedProperty propTelemetryTitle;
        private SerializedProperty propFrequency;
        private SerializedProperty propMaxRange;
        private SerializedProperty propPriority;
        private SerializedProperty propIsTransmitting;
        private SerializedProperty propDeactivateOnComplete;

        // Custom Preset Generator State
        private string proposedEnumName = "";
        private string lastGeneratedTag = "";

        private void OnEnable()
        {
            propPreset = serializedObject.FindProperty("channelPreset");
            propCustomOverride = serializedObject.FindProperty("customOverride");
            propBeaconTag = serializedObject.FindProperty("beaconTag");
            propTelemetryTitle = serializedObject.FindProperty("telemetryTitle");
            propFrequency = serializedObject.FindProperty("broadcastFrequencyMHz");
            propMaxRange = serializedObject.FindProperty("maxRange");
            propPriority = serializedObject.FindProperty("priority");
            propIsTransmitting = serializedObject.FindProperty("isTransmitting");
            propDeactivateOnComplete = serializedObject.FindProperty("deactiveOnMissionComplete");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SignalBeacon beacon = (SignalBeacon)target;

            // 1. Channel Preset Selection
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("VHF CHANNEL SELECTION", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(propPreset, new GUIContent("Channel Preset"));

            SignalChannelPreset currentPreset = (SignalChannelPreset)propPreset.enumValueIndex;

            if (currentPreset != SignalChannelPreset.Custom)
            {
                EditorGUILayout.PropertyField(propCustomOverride, new GUIContent("Custom Override", "Allow editing values without changing preset."));
            }

            bool isReadOnly = (currentPreset != SignalChannelPreset.Custom && !propCustomOverride.boolValue);

            // 2. Identity Section
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("TELEMETRY IDENTITY (FILM NOIR MONOCHROME)", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(isReadOnly);
            EditorGUILayout.PropertyField(propBeaconTag, new GUIContent("Beacon Tag", "Short code shown on Visor HUD"));
            EditorGUILayout.PropertyField(propTelemetryTitle, new GUIContent("Telemetry Title", "Full descriptive telemetry readout"));
            EditorGUILayout.PropertyField(propFrequency, new GUIContent("Frequency (MHz)", "VHF radio broadcast frequency"));
            EditorGUI.EndDisabledGroup();

            if (isReadOnly)
            {
                EditorGUILayout.HelpBox($"Values are managed by preset [{currentPreset}]. Enable 'Custom Override' or switch to 'Custom' to modify.", MessageType.Info);
            }

            // 3. Propagation Settings
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("SIGNAL PROPAGATION & MISSION", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(propMaxRange, new GUIContent("Max Range (Meters)"));
            EditorGUILayout.PropertyField(propPriority, new GUIContent("Priority Weight"));
            EditorGUILayout.PropertyField(propIsTransmitting, new GUIContent("Is Transmitting"));
            EditorGUILayout.PropertyField(propDeactivateOnComplete, new GUIContent("Deactivate On Complete"));

            serializedObject.ApplyModifiedProperties();

            // 4. Save Custom Channel to Enum Section
            if (currentPreset == SignalChannelPreset.Custom || propCustomOverride.boolValue)
            {
                DrawSavePresetSection(beacon);
            }
        }

        private void DrawSavePresetSection(SignalBeacon beacon)
        {
            EditorGUILayout.Space(12);

            // Suggest enum name from beaconTag if user hasn't typed one
            string currentTag = propBeaconTag.stringValue;
            if (string.IsNullOrEmpty(proposedEnumName) || lastGeneratedTag != currentTag)
            {
                proposedEnumName = SanitizeToIdentifier(currentTag);
                lastGeneratedTag = currentTag;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("💾 SAVE AS NEW CHANNEL PRESET ENUM", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Automatically adds these settings to the SignalChannelPreset enum dropdown.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            proposedEnumName = EditorGUILayout.TextField("New Enum Identifier", proposedEnumName);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Summary: {proposedEnumName} = {propFrequency.floatValue:F1} MHz [{propBeaconTag.stringValue}]", EditorStyles.miniLabel);

            GUI.backgroundColor = new Color(0.85f, 0.95f, 1.0f);
            if (GUILayout.Button("Save Preset to Enum & Recompile", GUILayout.Height(28)))
            {
                ExecuteSavePreset(
                    proposedEnumName,
                    propFrequency.floatValue,
                    propBeaconTag.stringValue,
                    propTelemetryTitle.stringValue,
                    propPriority.intValue
                );
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }

        private void ExecuteSavePreset(string enumName, float frequency, string tag, string title, int priority)
        {
            enumName = SanitizeToIdentifier(enumName);

            if (string.IsNullOrWhiteSpace(enumName))
            {
                EditorUtility.DisplayDialog("Invalid Identifier", "Please provide a valid alphanumeric name for the enum.", "OK");
                return;
            }

            if (Enum.GetNames(typeof(SignalChannelPreset)).Contains(enumName))
            {
                EditorUtility.DisplayDialog("Preset Exists", $"A preset named '{enumName}' already exists in SignalChannelPreset!", "OK");
                return;
            }

            string scriptPath = FindSignalBeaconScriptPath();
            if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            {
                EditorUtility.DisplayDialog("Error", "Could not locate SignalBeacon.cs file in project.", "OK");
                return;
            }

            try
            {
                string code = File.ReadAllText(scriptPath);

                // Compute next enum integer value
                int nextVal = Enum.GetValues(typeof(SignalChannelPreset)).Cast<int>().Max() + 1;

                string enumAnchor = "// [AUTO-PRESET-ENUM-ANCHOR]";
                string freqAnchor = "// [AUTO-FREQUENCY-ANCHOR]";
                string tagAnchor = "// [AUTO-TAG-ANCHOR]";
                string titleAnchor = "// [AUTO-TITLE-ANCHOR]";
                string priorityAnchor = "// [AUTO-PRIORITY-ANCHOR]";

                if (!code.Contains(enumAnchor))
                {
                    EditorUtility.DisplayDialog("Error", "Anchor comment not found in SignalBeacon.cs. Please ensure [AUTO-PRESET-ENUM-ANCHOR] is present.", "OK");
                    return;
                }

                // 1. Enum insertion
                string newEnumEntry = $"        {enumName} = {nextVal},     // {frequency:F1} MHz - {title}\n        {enumAnchor}";
                code = code.Replace(enumAnchor, newEnumEntry);

                // 2. Frequency insertion
                string newFreqEntry = $"case SignalChannelPreset.{enumName}: return {frequency:F1}f;\n                {freqAnchor}";
                code = code.Replace(freqAnchor, newFreqEntry);

                // 3. Tag insertion
                string newTagEntry = $"case SignalChannelPreset.{enumName}: return \"{tag}\";\n                {tagAnchor}";
                code = code.Replace(tagAnchor, newTagEntry);

                // 4. Title insertion
                string newTitleEntry = $"case SignalChannelPreset.{enumName}: return \"{title}\";\n                {titleAnchor}";
                code = code.Replace(titleAnchor, newTitleEntry);

                // 5. Priority insertion
                string newPriorityEntry = $"case SignalChannelPreset.{enumName}: return {priority};\n                {priorityAnchor}";
                code = code.Replace(priorityAnchor, newPriorityEntry);

                File.WriteAllText(scriptPath, code);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog(
                    "Preset Successfully Added!",
                    $"New preset '{enumName}' ({frequency:F1} MHz) has been added to SignalChannelPreset enum!\n\nUnity is now recompiling the scripts. It will be immediately selectable in the dropdown.",
                    "OK"
                );

                Debug.Log($"<color=#E2E8F0><b>[Space Horror]</b></color> Successfully added <b>{enumName}</b> ({frequency:F1} MHz) to SignalChannelPreset Enum!");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Error Saving Preset", $"An exception occurred: {ex.Message}", "OK");
                Debug.LogError($"[SignalBeaconEditor] Failed to append preset: {ex}");
            }
        }

        private static string FindSignalBeaconScriptPath()
        {
            string[] guids = AssetDatabase.FindAssets("SignalBeacon t:MonoScript");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == "SignalBeacon.cs")
                {
                    return path;
                }
            }

            string fallback = "Assets/Scripts/Interaction/SignalBeacon.cs";
            return File.Exists(fallback) ? fallback : null;
        }

        private static string SanitizeToIdentifier(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "CustomChannel";

            // Remove special characters, split words by spaces/dashes/underscores, and capitalize (PascalCase)
            string clean = Regex.Replace(input, @"[^a-zA-Z0-9_\s-]", "");
            string[] words = clean.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0) return "CustomChannel";

            string result = "";
            foreach (string w in words)
            {
                if (w.Length > 0)
                {
                    result += char.ToUpper(w[0]) + (w.Length > 1 ? w.Substring(1).ToLower() : "");
                }
            }

            // Ensure starts with a valid C# letter
            if (char.IsDigit(result[0]))
            {
                result = "Channel_" + result;
            }

            return result;
        }
    }
}
