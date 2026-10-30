using UnityEditor;
using UnityEngine;
using SpaceHorror.Gameplay;
using SpaceHorror.Interaction;

namespace SpaceHorror.Editor
{
    [CustomEditor(typeof(AuxiliaryO2Substation))]
    public class AuxiliaryO2SubstationEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            AuxiliaryO2Substation sub = (AuxiliaryO2Substation)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Debug & Testing", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            if (GUILayout.Button("Test Spawn Particle (Play Mode)", GUILayout.Height(30)))
            {
                sub.SpawnInteractionParticles();
            }
            EditorGUI.EndDisabledGroup();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test the interaction particle effect directly.", MessageType.Info);
            }
        }

        [MenuItem("Tools/Space Horror/Setup Auxiliary O2 Substations in Active Scene")]
        public static void SetupSubstationsInScene()
        {
            var interactables = Object.FindObjectsByType<MissionInteractable>(FindObjectsSortMode.None);
            int setupCount = 0;

            for (int i = 0; i < interactables.Length; i++)
            {
                var mi = interactables[i];
                if (mi == null) continue;

                string objName = mi.gameObject.name.ToUpperInvariant();
                string title = mi.ObjectiveTitle != null ? mi.ObjectiveTitle.ToUpperInvariant() : "";

                bool isSubstation = objName.Contains("SUBSTATION") || title.Contains("AUXILIARY O2") || title.Contains("REGULATOR");
                if (isSubstation)
                {
                    var existingSub = mi.GetComponent<AuxiliaryO2Substation>();
                    if (existingSub == null)
                    {
                        Undo.AddComponent<AuxiliaryO2Substation>(mi.gameObject);
                        setupCount++;
                        Debug.Log($"<color=#55FF55><b>[AuxiliaryO2SubstationEditor]</b></color> Added AuxiliaryO2Substation component to '{mi.gameObject.name}'");
                    }
                }
            }

            if (setupCount > 0)
            {
                EditorUtility.DisplayDialog("Auxiliary O2 Setup", $"Successfully attached AuxiliaryO2Substation to {setupCount} object(s) in the scene!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Auxiliary O2 Setup", "No unconfigured Substation objects found in scene (or already configured).", "OK");
            }
        }
    }
}
