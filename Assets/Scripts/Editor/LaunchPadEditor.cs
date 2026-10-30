using UnityEditor;
using UnityEngine;
using SpaceHorror.Gameplay;
using SpaceHorror.Interaction;

namespace SpaceHorror.Editor
{
    [CustomEditor(typeof(LaunchPad))]
    public class LaunchPadEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            LaunchPad launchPad = (LaunchPad)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Debug & Testing", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            if (GUILayout.Button("Trigger Launch Sequence (Play Mode)", GUILayout.Height(32)))
            {
                launchPad.TriggerLaunchSequence();
            }
            EditorGUI.EndDisabledGroup();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test the launch cinematic sequence and ending screen directly.", MessageType.Info);
            }
        }

        [MenuItem("Tools/Space Horror/Setup Launch Pad in Active Scene")]
        public static void SetupLaunchPadInScene()
        {
            var interactables = Object.FindObjectsByType<MissionInteractable>(FindObjectsSortMode.None);
            int setupCount = 0;

            for (int i = 0; i < interactables.Length; i++)
            {
                var mi = interactables[i];
                if (mi == null) continue;

                string title = mi.ObjectiveTitle != null ? mi.ObjectiveTitle.ToUpperInvariant() : "";
                string prompt = mi.ActionPrompt != null ? mi.ActionPrompt.ToUpperInvariant() : "";
                string name = mi.gameObject.name.ToUpperInvariant();

                if (title.Contains("LAUNCH") || prompt.Contains("LAUNCH") || name.Contains("LAUNCH"))
                {
                    var lp = mi.GetComponent<LaunchPad>();
                    if (lp == null)
                    {
                        lp = Undo.AddComponent<LaunchPad>(mi.gameObject);
                        setupCount++;
                        Debug.Log($"<color=#55FF55><b>[LaunchPadEditor]</b></color> Added LaunchPad component to '{mi.gameObject.name}'");
                    }
                }
            }

            if (setupCount > 0)
            {
                EditorUtility.DisplayDialog("Launch Pad Setup", $"Successfully attached LaunchPad to {setupCount} object(s) in the scene!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Launch Pad Setup", "No unconfigured Launch objects found in scene (or already configured).", "OK");
            }
        }
    }
}
