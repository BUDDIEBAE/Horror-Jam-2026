#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using SpaceHorror.Interaction;
using SpaceHorror.Gameplay;

namespace SpaceHorror.Editor
{
    /// <summary>
    /// Editor utility to construct and configure the authentic PSX Ship Prefab.
    /// Hooks up the exported FBX, PSX Material, Colliders, ShipReturnAirlock, and ShipMissionTerminal.
    /// </summary>
    public static class ShipPrefabBuilder
    {
        private const string FbxPath = "Assets/Assets/Ship.fbx";
        private const string MaterialPath = "Assets/Settings/Materials/M_Ship_PSX.mat";
        private const string PrefabDir = "Assets/Prefabs";
        private const string PrefabPath = "Assets/Prefabs/Ship.prefab";

        [InitializeOnLoadMethod]
        private static void AutoBuildIfMissing()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(PrefabPath) && File.Exists(FbxPath))
                {
                    Debug.Log("[ShipPrefabBuilder] Auto-generating Ship.prefab on import...");
                    BuildShipPrefab();
                }
            };
        }

        [MenuItem("Tools/Space Horror/Ship/Build Ship Prefab", false, 20)]
        public static GameObject BuildShipPrefab()
        {
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError($"[ShipPrefabBuilder] Failed to find Ship model at {FbxPath}!");
                return null;
            }

            Material shipMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (shipMat == null)
            {
                Debug.LogWarning($"[ShipPrefabBuilder] Material not found at {MaterialPath}. Checking Assets/Assets...");
                shipMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Assets/M_Ship_PSX.mat");
            }

            // Create instance in scene for configuration
            GameObject shipRoot = UnityEngine.Object.Instantiate(fbxAsset);
            shipRoot.name = "Ship";
            shipRoot.transform.position = Vector3.zero;
            shipRoot.transform.rotation = Quaternion.identity;

            // Configure all child meshes
            MeshRenderer[] renderers = shipRoot.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in renderers)
            {
                if (shipMat != null)
                {
                    mr.sharedMaterial = shipMat;
                }

                GameObject go = mr.gameObject;
                string goName = go.name;

                // Colliders based on part type
                if (goName.Contains("Body"))
                {
                    MeshCollider mc = go.GetComponent<MeshCollider>();
                    if (mc == null) mc = go.AddComponent<MeshCollider>();
                    mc.convex = false; // Static hull collider for player walking & exterior collision
                }
                else if (goName.Contains("Sledge"))
                {
                    BoxCollider bc = go.GetComponent<BoxCollider>();
                    if (bc == null) bc = go.AddComponent<BoxCollider>();
                }
                else if (goName.Contains("Booster"))
                {
                    MeshCollider mc = go.GetComponent<MeshCollider>();
                    if (mc == null) mc = go.AddComponent<MeshCollider>();
                    mc.convex = true;
                }
                else if (goName.Contains("Air Lock"))
                {
                    SetupAirlock(go);
                }
                else if (goName.Contains("Terminal"))
                {
                    SetupTerminal(go);
                }
            }

            // Ensure directory exists
            if (!Directory.Exists(PrefabDir))
            {
                Directory.CreateDirectory(PrefabDir);
                AssetDatabase.Refresh();
            }

            // Save as Prefab Asset
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(shipRoot, PrefabPath);
            UnityEngine.Object.DestroyImmediate(shipRoot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#66FF66><b>[ShipPrefabBuilder] Successfully created and saved Ship Prefab at:</b></color> {PrefabPath}");
            return prefab;
        }

        private static void SetupAirlock(GameObject go)
        {
            // Box Collider
            BoxCollider bc = go.GetComponent<BoxCollider>();
            if (bc == null) bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(1.2f, 2.2f, 1.2f);
            bc.center = new Vector3(0f, 1.1f, 0f);

            // Mission Interactable
            MissionInteractable mi = go.GetComponent<MissionInteractable>();
            if (mi == null) mi = go.AddComponent<MissionInteractable>();

            SerializedObject soMi = new SerializedObject(mi);
            soMi.FindProperty("objectiveTitle").stringValue = "EVA AIRLOCK & EXTRACTION DOCK";
            soMi.FindProperty("actionPrompt").stringValue = "CYCLE AIRLOCK // DOCK";
            soMi.FindProperty("completionMessage").stringValue = "AIRLOCK SEALED // EXPEDITION CONCLUDED";
            soMi.FindProperty("objectiveType").enumValueIndex = (int)ObjectiveType.TogglePowerOrDoor;
            soMi.FindProperty("interactionMethod").enumValueIndex = (int)InteractionMethod.HoldToComplete;
            soMi.FindProperty("holdDuration").floatValue = 1.5f;
            soMi.FindProperty("isSingleUse").boolValue = false;
            soMi.FindProperty("requiresActiveMission").boolValue = false;
            soMi.ApplyModifiedProperties();

            // Signal Beacon (Airlock Home 121.5 MHz)
            SignalBeacon beacon = go.GetComponent<SignalBeacon>();
            if (beacon == null) beacon = go.AddComponent<SignalBeacon>();

            SerializedObject soBeacon = new SerializedObject(beacon);
            soBeacon.FindProperty("channelPreset").enumValueIndex = (int)SignalChannelPreset.AirlockHome;
            soBeacon.FindProperty("customOverride").boolValue = true;
            soBeacon.FindProperty("beaconTag").stringValue = "AIRLOCK-HOME";
            soBeacon.FindProperty("telemetryTitle").stringValue = "EVA EXTRACTION DOCK";
            soBeacon.FindProperty("broadcastFrequencyMHz").floatValue = 121.5f;
            soBeacon.FindProperty("maxRange").floatValue = 300f;
            soBeacon.FindProperty("priority").intValue = 2;
            soBeacon.FindProperty("isTransmitting").boolValue = true;
            soBeacon.ApplyModifiedProperties();

            // Ship Return Airlock script
            ShipReturnAirlock returnAirlock = go.GetComponent<ShipReturnAirlock>();
            if (returnAirlock == null) returnAirlock = go.AddComponent<ShipReturnAirlock>();

            // Small Amber Indicator Light
            GameObject lightObj = new GameObject("Airlock_Status_Light");
            lightObj.transform.SetParent(go.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 1.8f, 0.4f);

            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1.0f, 0.75f, 0.2f);
            light.intensity = 1.2f;
            light.range = 3.5f;
            light.shadows = LightShadows.None;
        }

        private static void SetupTerminal(GameObject go)
        {
            // Box Collider
            BoxCollider bc = go.GetComponent<BoxCollider>();
            if (bc == null) bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(1.0f, 1.8f, 1.0f);
            bc.center = new Vector3(0f, 0.9f, 0f);

            // Mission Interactable
            MissionInteractable mi = go.GetComponent<MissionInteractable>();
            if (mi == null) mi = go.AddComponent<MissionInteractable>();

            SerializedObject soMi = new SerializedObject(mi);
            soMi.FindProperty("objectiveTitle").stringValue = "SHIP EXPEDITION TERMINAL";
            soMi.FindProperty("actionPrompt").stringValue = "ACCESS MISSION BRIEFING";
            soMi.FindProperty("completionMessage").stringValue = "TELEMETRY SYNCED";
            soMi.FindProperty("objectiveType").enumValueIndex = (int)ObjectiveType.CollectDataLog;
            soMi.FindProperty("interactionMethod").enumValueIndex = (int)InteractionMethod.InstantPress;
            soMi.FindProperty("isSingleUse").boolValue = false;
            soMi.FindProperty("requiresActiveMission").boolValue = false;
            soMi.ApplyModifiedProperties();

            // Signal Beacon (Terminal Log 138.4 MHz)
            SignalBeacon beacon = go.GetComponent<SignalBeacon>();
            if (beacon == null) beacon = go.AddComponent<SignalBeacon>();

            SerializedObject soBeacon = new SerializedObject(beacon);
            soBeacon.FindProperty("channelPreset").enumValueIndex = (int)SignalChannelPreset.DataTerminal;
            soBeacon.FindProperty("customOverride").boolValue = true;
            soBeacon.FindProperty("beaconTag").stringValue = "TERMINAL-LOG";
            soBeacon.FindProperty("telemetryTitle").stringValue = "MISSION ARCHIVE & DISPATCH";
            soBeacon.FindProperty("broadcastFrequencyMHz").floatValue = 138.4f;
            soBeacon.FindProperty("maxRange").floatValue = 180f;
            soBeacon.FindProperty("priority").intValue = 3;
            soBeacon.FindProperty("isTransmitting").boolValue = true;
            soBeacon.ApplyModifiedProperties();

            // Ship Mission Terminal script
            ShipMissionTerminal terminal = go.GetComponent<ShipMissionTerminal>();
            if (terminal == null) terminal = go.AddComponent<ShipMissionTerminal>();

            // Small Cyan Console Display Glow Light
            GameObject lightObj = new GameObject("Console_Screen_Glow");
            lightObj.transform.SetParent(go.transform, false);
            lightObj.transform.localPosition = new Vector3(0.3f, 1.2f, 0.3f);

            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.4f, 0.85f, 1.0f);
            light.intensity = 1.0f;
            light.range = 2.5f;
            light.shadows = LightShadows.None;
        }

        [MenuItem("Tools/Space Horror/Ship/Place Ship in Scene", false, 21)]
        public static void PlaceShipInScene()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                prefab = BuildShipPrefab();
            }

            if (prefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "Ship_PlayerHome";
                Undo.RegisterCreatedObjectUndo(instance, "Place Ship");
                Selection.activeGameObject = instance;
                Debug.Log($"[ShipPrefabBuilder] Placed Ship in active scene at {instance.transform.position}");
            }
        }

        private const string InteriorFbxPath = "Assets/Assets/Ship Interior.fbx";
        private const string InteriorMaterialPath = "Assets/Settings/Materials/M_ShipInterior_PSX.mat";
        private const string InteriorPrefabPath = "Assets/Prefabs/Ship Interior.prefab";

        [MenuItem("Tools/Space Horror/Ship/Build Ship Interior Prefab", false, 22)]
        public static GameObject BuildShipInteriorPrefab()
        {
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(InteriorFbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError($"[ShipPrefabBuilder] Failed to find Ship Interior model at {InteriorFbxPath}!");
                return null;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(InteriorMaterialPath);

            GameObject root = UnityEngine.Object.Instantiate(fbxAsset);
            root.name = "Ship Interior";
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in renderers)
            {
                if (mat != null)
                {
                    mr.sharedMaterial = mat;
                }

                GameObject go = mr.gameObject;
                string goName = go.name;

                if (goName.Contains("Floor"))
                {
                    MeshCollider mc = go.GetComponent<MeshCollider>();
                    if (mc == null) mc = go.AddComponent<MeshCollider>();
                    mc.convex = false; // Walking mesh for player inside corridor
                }
                else if (goName.Contains("Stand"))
                {
                    BoxCollider bc = go.GetComponent<BoxCollider>();
                    if (bc == null) bc = go.AddComponent<BoxCollider>();
                }
                else if (goName.Contains("Button"))
                {
                    BoxCollider bc = go.GetComponent<BoxCollider>();
                    if (bc == null) bc = go.AddComponent<BoxCollider>();

                    MissionInteractable mi = go.GetComponent<MissionInteractable>();
                    if (mi == null) mi = go.AddComponent<MissionInteractable>();

                    SerializedObject soMi = new SerializedObject(mi);
                    soMi.FindProperty("objectiveTitle").stringValue = "EMERGENCY LAUNCH SYSTEM";
                    soMi.FindProperty("actionPrompt").stringValue = "INITIATE LAUNCH SEQUENCE";
                    soMi.FindProperty("completionMessage").stringValue = "THRUSTERS ENGAGED // LAUNCH INITIATED";
                    soMi.FindProperty("objectiveType").enumValueIndex = (int)ObjectiveType.Custom;
                    soMi.FindProperty("interactionMethod").enumValueIndex = (int)InteractionMethod.HoldToComplete;
                    soMi.FindProperty("holdDuration").floatValue = 2.0f;
                    soMi.FindProperty("isSingleUse").boolValue = true;
                    soMi.ApplyModifiedProperties();

                    // Button Red Light
                    GameObject lightObj = new GameObject("Launch_Button_Light");
                    lightObj.transform.SetParent(go.transform, false);
                    lightObj.transform.localPosition = new Vector3(0f, 0f, 0.5f);

                    Light l = lightObj.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(1.0f, 0.2f, 0.1f);
                    l.intensity = 1.0f;
                    l.range = 2.0f;
                }
            }

            // Ceiling Fluorescent Light
            GameObject ceilLight = new GameObject("Ceiling_Fluorescent_Light");
            ceilLight.transform.SetParent(root.transform, false);
            ceilLight.transform.localPosition = new Vector3(0f, 1.2f, -1.5f);

            Light cl = ceilLight.AddComponent<Light>();
            cl.type = LightType.Point;
            cl.color = new Color(0.92f, 0.96f, 1.0f);
            cl.intensity = 1.5f;
            cl.range = 8.0f;

            if (!Directory.Exists(PrefabDir))
            {
                Directory.CreateDirectory(PrefabDir);
                AssetDatabase.Refresh();
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, InteriorPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#66FF66><b>[ShipPrefabBuilder] Successfully created Ship Interior Prefab at:</b></color> {InteriorPrefabPath}");
            return prefab;
        }

        [MenuItem("Tools/Space Horror/Ship/Place Ship Interior in Scene", false, 23)]
        public static void PlaceShipInteriorInScene()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InteriorPrefabPath);
            if (prefab == null)
            {
                prefab = BuildShipInteriorPrefab();
            }

            if (prefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "Ship Interior_Configured";
                Undo.RegisterCreatedObjectUndo(instance, "Place Ship Interior");
                Selection.activeGameObject = instance;
                Debug.Log($"[ShipPrefabBuilder] Placed Ship Interior in scene at {instance.transform.position}");
            }
        }
    }
}
#endif
