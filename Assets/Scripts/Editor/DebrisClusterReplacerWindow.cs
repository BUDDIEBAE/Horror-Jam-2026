#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpaceHorror.Editor
{
    /// <summary>
    /// Custom Editor Window for replacing placeholder debris (e.g. cubes in Space_Debris_Cluster)
    /// with actual 3D models/prefabs (e.g., Asteroids / Derelict hulls), randomly re-scaling them,
    /// and assigning random materials from a curated list (2-3+ materials).
    /// </summary>
    public class DebrisClusterReplacerWindow : EditorWindow
    {
        public enum TargetScope
        {
            SingleClusterChildren,
            AllClustersInScene,
            CurrentHierarchySelection
        }

        public enum ScaleMode
        {
            UniformRandom,
            NonUniformRandom,
            MultiplyExisting
        }

        public enum RotationMode
        {
            FullRandom360,
            KeepOriginal,
            RandomYawOnly
        }

        public enum ColliderType
        {
            MeshColliderConvex,
            BoxCollider,
            SphereCollider,
            None
        }

        // --- Target Settings ---
        [SerializeField] private TargetScope targetScope = TargetScope.SingleClusterChildren;
        [SerializeField] private GameObject targetClusterRoot;
        [SerializeField] private string clusterSearchName = "Space_Debris_Cluster";

        // --- Replacement Assets ---
        [SerializeField] private List<GameObject> replacementModels = new List<GameObject>();
        [SerializeField] private List<Material> randomMaterials = new List<Material>();

        // --- Transform Settings ---
        [SerializeField] private ScaleMode scaleMode = ScaleMode.UniformRandom;
        [SerializeField] private float minUniformScale = 1.0f;
        [SerializeField] private float maxUniformScale = 4.0f;
        [SerializeField] private Vector3 minNonUniformScale = new Vector3(1.0f, 1.0f, 1.0f);
        [SerializeField] private Vector3 maxNonUniformScale = new Vector3(4.0f, 4.0f, 6.0f);
        [SerializeField] private float minScaleMultiplier = 0.8f;
        [SerializeField] private float maxScaleMultiplier = 2.0f;

        [SerializeField] private RotationMode rotationMode = RotationMode.FullRandom360;
        [SerializeField] private bool applyPositionJitter = false;
        [SerializeField] private float positionJitterRadius = 1.5f;

        // --- Physics & Rigidbody Settings ---
        [SerializeField] private ColliderType colliderType = ColliderType.MeshColliderConvex;
        [SerializeField] private bool configureRigidbody = true;
        [SerializeField] private float debrisMass = 500f;
        [SerializeField] private float linearDamping = 0.5f;
        [SerializeField] private float angularDamping = 0.5f;
        [SerializeField] private bool useGravity = false;
        [SerializeField] private bool isKinematic = false;

        // UI Foldouts & Scroll
        private Vector2 scrollPos;
        private bool showTargetSection = true;
        private bool showModelsSection = true;
        private bool showMaterialsSection = true;
        private bool showTransformSection = true;
        private bool showPhysicsSection = true;

        [MenuItem("Tools/Space Horror/3. Environment & Space/Debris Cluster Replacer & Rescaler", false, 23)]
        [MenuItem("Window/Space Horror/Debris Cluster Replacer")]
        public static void OpenWindow()
        {
            var window = GetWindow<DebrisClusterReplacerWindow>("Debris Replacer");
            window.minSize = new Vector2(400, 600);
            window.AutoDetectTargetCluster();
            window.AutoDetectProjectAssets();
            window.Show();
        }

        private void OnEnable()
        {
            if (targetClusterRoot == null)
            {
                AutoDetectTargetCluster();
            }

            if (replacementModels.Count == 0 || randomMaterials.Count == 0)
            {
                AutoDetectProjectAssets();
            }
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawHeader();

            EditorGUILayout.Space(6);
            DrawTargetSection();

            EditorGUILayout.Space(6);
            DrawModelsSection();

            EditorGUILayout.Space(6);
            DrawMaterialsSection();

            EditorGUILayout.Space(6);
            DrawTransformSection();

            EditorGUILayout.Space(6);
            DrawPhysicsSection();

            EditorGUILayout.Space(12);
            DrawActionButtons();

            EditorGUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        // =========================================================================
        // UI DRAWING
        // =========================================================================

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("DEBRIS CLUSTER REPLACER & RESCALER", titleStyle);
            EditorGUILayout.LabelField("Replace placeholder cubes with 3D models, re-scale, and randomize materials.", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawTargetSection()
        {
            showTargetSection = EditorGUILayout.BeginFoldoutHeaderGroup(showTargetSection, "1. TARGET SELECTION");
            if (showTargetSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                targetScope = (TargetScope)EditorGUILayout.EnumPopup("Target Scope", targetScope);

                if (targetScope == TargetScope.SingleClusterChildren)
                {
                    EditorGUILayout.BeginHorizontal();
                    targetClusterRoot = (GameObject)EditorGUILayout.ObjectField("Cluster Root", targetClusterRoot, typeof(GameObject), true);
                    if (GUILayout.Button("Find in Scene", GUILayout.Width(100)))
                    {
                        AutoDetectTargetCluster();
                    }
                    EditorGUILayout.EndHorizontal();

                    if (targetClusterRoot != null)
                    {
                        int childCount = targetClusterRoot.transform.childCount;
                        EditorGUILayout.HelpBox($"Target '{targetClusterRoot.name}' has {childCount} children to replace.", MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Select or drag the 'Space_Debris_Cluster' GameObject from your Hierarchy.", MessageType.Warning);
                    }
                }
                else if (targetScope == TargetScope.AllClustersInScene)
                {
                    clusterSearchName = EditorGUILayout.TextField("Cluster Search Name", clusterSearchName);
                    int matchCount = FindClustersInScene(clusterSearchName).Count;
                    EditorGUILayout.HelpBox($"Found {matchCount} GameObject(s) matching '{clusterSearchName}' in the active scene.", MessageType.Info);
                }
                else
                {
                    int selectedCount = Selection.gameObjects != null ? Selection.gameObjects.Length : 0;
                    EditorGUILayout.HelpBox($"Currently selected: {selectedCount} GameObject(s) in Hierarchy.", MessageType.Info);
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawModelsSection()
        {
            showModelsSection = EditorGUILayout.BeginFoldoutHeaderGroup(showModelsSection, $"2. REPLACEMENT MODELS ({replacementModels.Count})");
            if (showModelsSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Auto-Load Asteroids from Project", EditorStyles.miniButtonLeft))
                {
                    LoadAsteroidModelsFromProject();
                }
                if (GUILayout.Button("+ Add Slot", EditorStyles.miniButtonMid, GUILayout.Width(80)))
                {
                    replacementModels.Add(null);
                }
                if (GUILayout.Button("Clear", EditorStyles.miniButtonRight, GUILayout.Width(60)))
                {
                    replacementModels.Clear();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                for (int i = 0; i < replacementModels.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    replacementModels[i] = (GameObject)EditorGUILayout.ObjectField($"Model {i + 1}", replacementModels[i], typeof(GameObject), false);
                    if (GUILayout.Button("X", GUILayout.Width(24)))
                    {
                        replacementModels.RemoveAt(i);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (replacementModels.Count == 0)
                {
                    EditorGUILayout.HelpBox("No replacement models assigned! Click 'Auto-Load Asteroids from Project' or drag FBX models / prefabs above.", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox($"Each debris chunk will randomly pick from these {replacementModels.Count} model(s).", MessageType.None);
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawMaterialsSection()
        {
            showMaterialsSection = EditorGUILayout.BeginFoldoutHeaderGroup(showMaterialsSection, $"3. RANDOM MATERIALS ({randomMaterials.Count})");
            if (showMaterialsSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Auto-Load Materials from Project", EditorStyles.miniButtonLeft))
                {
                    LoadMaterialsFromProject();
                }
                if (GUILayout.Button("+ Add Slot", EditorStyles.miniButtonMid, GUILayout.Width(80)))
                {
                    randomMaterials.Add(null);
                }
                if (GUILayout.Button("Clear", EditorStyles.miniButtonRight, GUILayout.Width(60)))
                {
                    randomMaterials.Clear();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                for (int i = 0; i < randomMaterials.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    randomMaterials[i] = (Material)EditorGUILayout.ObjectField($"Material {i + 1}", randomMaterials[i], typeof(Material), false);
                    if (GUILayout.Button("X", GUILayout.Width(24)))
                    {
                        randomMaterials.RemoveAt(i);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (randomMaterials.Count == 0)
                {
                    EditorGUILayout.HelpBox("No materials assigned! Click 'Auto-Load Materials from Project' or drag 2-3 materials above to randomize.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox($"Each debris chunk will be randomly assigned 1 of these {randomMaterials.Count} material(s).", MessageType.None);
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawTransformSection()
        {
            showTransformSection = EditorGUILayout.BeginFoldoutHeaderGroup(showTransformSection, "4. SCALE & ROTATION SETTINGS");
            if (showTransformSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                scaleMode = (ScaleMode)EditorGUILayout.EnumPopup("Scale Mode", scaleMode);

                if (scaleMode == ScaleMode.UniformRandom)
                {
                    EditorGUILayout.BeginHorizontal();
                    minUniformScale = EditorGUILayout.FloatField("Min Scale", minUniformScale);
                    maxUniformScale = EditorGUILayout.FloatField("Max Scale", maxUniformScale);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.MinMaxSlider("Scale Range", ref minUniformScale, ref maxUniformScale, 0.1f, 15f);
                    minUniformScale = Mathf.Max(0.01f, minUniformScale);
                    maxUniformScale = Mathf.Max(minUniformScale, maxUniformScale);
                }
                else if (scaleMode == ScaleMode.NonUniformRandom)
                {
                    minNonUniformScale = EditorGUILayout.Vector3Field("Min Scale (XYZ)", minNonUniformScale);
                    maxNonUniformScale = EditorGUILayout.Vector3Field("Max Scale (XYZ)", maxNonUniformScale);
                }
                else // MultiplyExisting
                {
                    EditorGUILayout.BeginHorizontal();
                    minScaleMultiplier = EditorGUILayout.FloatField("Min Multiplier", minScaleMultiplier);
                    maxScaleMultiplier = EditorGUILayout.FloatField("Max Multiplier", maxScaleMultiplier);
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(4);
                rotationMode = (RotationMode)EditorGUILayout.EnumPopup("Rotation Mode", rotationMode);

                EditorGUILayout.Space(4);
                applyPositionJitter = EditorGUILayout.Toggle("Apply Position Jitter", applyPositionJitter);
                if (applyPositionJitter)
                {
                    positionJitterRadius = EditorGUILayout.FloatField("Jitter Radius (m)", positionJitterRadius);
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawPhysicsSection()
        {
            showPhysicsSection = EditorGUILayout.BeginFoldoutHeaderGroup(showPhysicsSection, "5. PHYSICS & COLLIDERS (6DOF ZERO-G)");
            if (showPhysicsSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                colliderType = (ColliderType)EditorGUILayout.EnumPopup("Collider Type", colliderType);

                EditorGUILayout.Space(4);
                configureRigidbody = EditorGUILayout.Toggle("Configure Rigidbody", configureRigidbody);
                if (configureRigidbody)
                {
                    EditorGUI.indentLevel++;
                    debrisMass = EditorGUILayout.FloatField("Mass", debrisMass);
                    linearDamping = EditorGUILayout.FloatField("Linear Damping", linearDamping);
                    angularDamping = EditorGUILayout.FloatField("Angular Damping", angularDamping);
                    useGravity = EditorGUILayout.Toggle("Use Gravity", useGravity);
                    isKinematic = EditorGUILayout.Toggle("Is Kinematic", isKinematic);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawActionButtons()
        {
            // Primary Action Button (Replace All)
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.5f);
            if (GUILayout.Button("REPLACE ALL DEBRIS (Model + Re-Scale + Random Material)", GUILayout.Height(38)))
            {
                ExecuteFullReplacement();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(4);

            // Secondary Quick-Tweak Buttons
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.4f, 0.7f, 1.0f);
            if (GUILayout.Button("Re-Roll Scales Only", GUILayout.Height(26)))
            {
                ExecuteReRollScalesOnly();
            }

            GUI.backgroundColor = new Color(1.0f, 0.75f, 0.3f);
            if (GUILayout.Button("Re-Roll Materials Only", GUILayout.Height(26)))
            {
                ExecuteReRollMaterialsOnly();
            }

            GUI.backgroundColor = new Color(0.85f, 0.5f, 1.0f);
            if (GUILayout.Button("Re-Roll Rotations Only", GUILayout.Height(26)))
            {
                ExecuteReRollRotationsOnly();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        // =========================================================================
        // CORE LOGIC & OPERATIONS
        // =========================================================================

        private void ExecuteFullReplacement()
        {
            List<GameObject> targetObjects = GetTargetObjectsToProcess();
            if (targetObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("No Targets Found", "No debris objects found to replace! Please check your Target Selection.", "OK");
                return;
            }

            List<GameObject> validModels = replacementModels.FindAll(m => m != null);
            if (validModels.Count == 0)
            {
                EditorUtility.DisplayDialog("No Models Assigned", "Please assign at least one replacement model (e.g. Asteroid FBX) before replacing.", "OK");
                return;
            }

            List<Material> validMaterials = randomMaterials.FindAll(m => m != null);

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Replace Debris Models & Rescale");
            int undoGroup = Undo.GetCurrentGroup();

            int replacedCount = 0;

            for (int i = 0; i < targetObjects.Count; i++)
            {
                GameObject oldObj = targetObjects[i];
                if (oldObj == null) continue;

                Transform oldTransform = oldObj.transform;
                Transform parent = oldTransform.parent;
                Vector3 position = oldTransform.position;
                Quaternion rotation = oldTransform.rotation;
                Vector3 originalScale = oldTransform.localScale;

                // Pick random model
                GameObject modelPrefab = validModels[Random.Range(0, validModels.Count)];

                // Instantiate new instance
                GameObject newObj = null;
                if (PrefabUtility.IsPartOfPrefabAsset(modelPrefab))
                {
                    newObj = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
                }
                
                if (newObj == null)
                {
                    newObj = Instantiate(modelPrefab);
                }

                newObj.name = $"{modelPrefab.name}_{i + 1}";
                Undo.RegisterCreatedObjectUndo(newObj, "Instantiate Replacement Debris");

                // Parent & Transform
                newObj.transform.SetParent(parent, false);
                
                // Position with optional jitter
                if (applyPositionJitter)
                {
                    position += Random.insideUnitSphere * positionJitterRadius;
                }
                newObj.transform.position = position;

                // Rotation
                switch (rotationMode)
                {
                    case RotationMode.FullRandom360:
                        newObj.transform.rotation = Random.rotation;
                        break;
                    case RotationMode.RandomYawOnly:
                        newObj.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                        break;
                    case RotationMode.KeepOriginal:
                        newObj.transform.rotation = rotation;
                        break;
                }

                // Scale
                newObj.transform.localScale = CalculateScale(originalScale);

                // Material Assignment
                if (validMaterials.Count > 0)
                {
                    Material chosenMat = validMaterials[Random.Range(0, validMaterials.Count)];
                    ApplyMaterialToObject(newObj, chosenMat);
                }

                // Colliders
                SetupCollider(newObj);

                // Rigidbody
                if (configureRigidbody)
                {
                    SetupRigidbody(newObj);
                }

                // Destroy old placeholder cube/object
                Undo.DestroyObjectImmediate(oldObj);
                replacedCount++;
            }

            Undo.CollapseUndoOperations(undoGroup);
            MarkSceneDirty();

            Debug.Log($"<color=#00FF99><b>[Debris Replacer]</b></color> Successfully replaced <b>{replacedCount}</b> debris objects with {validModels.Count} model(s) and {validMaterials.Count} material(s)!");
            EditorUtility.DisplayDialog("Debris Replaced!", $"Successfully replaced {replacedCount} debris object(s) with new models, scales, and materials.\n\nYou can press Ctrl+Z to undo if needed.", "Awesome!");
        }

        private void ExecuteReRollScalesOnly()
        {
            List<GameObject> targetObjects = GetTargetObjectsToProcess();
            if (targetObjects.Count == 0) return;

            Undo.RecordObjects(targetObjects.ConvertAll(go => (Object)go.transform).ToArray(), "Re-roll Debris Scale");

            foreach (var go in targetObjects)
            {
                if (go == null) continue;
                go.transform.localScale = CalculateScale(go.transform.localScale);
            }

            MarkSceneDirty();
            Debug.Log($"<color=#00FF99><b>[Debris Replacer]</b></color> Re-rolled scale for {targetObjects.Count} debris objects.");
        }

        private void ExecuteReRollMaterialsOnly()
        {
            List<GameObject> targetObjects = GetTargetObjectsToProcess();
            if (targetObjects.Count == 0) return;

            List<Material> validMaterials = randomMaterials.FindAll(m => m != null);
            if (validMaterials.Count == 0)
            {
                EditorUtility.DisplayDialog("No Materials", "Please assign at least one material to randomize.", "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Re-roll Debris Materials");

            int changedCount = 0;
            foreach (var go in targetObjects)
            {
                if (go == null) continue;
                Material chosenMat = validMaterials[Random.Range(0, validMaterials.Count)];
                ApplyMaterialToObject(go, chosenMat);
                changedCount++;
            }

            MarkSceneDirty();
            Debug.Log($"<color=#00FF99><b>[Debris Replacer]</b></color> Randomly assigned {validMaterials.Count} materials to {changedCount} debris objects.");
        }

        private void ExecuteReRollRotationsOnly()
        {
            List<GameObject> targetObjects = GetTargetObjectsToProcess();
            if (targetObjects.Count == 0) return;

            Undo.RecordObjects(targetObjects.ConvertAll(go => (Object)go.transform).ToArray(), "Re-roll Debris Rotation");

            foreach (var go in targetObjects)
            {
                if (go == null) continue;
                if (rotationMode == RotationMode.RandomYawOnly)
                {
                    go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                }
                else
                {
                    go.transform.rotation = Random.rotation;
                }
            }

            MarkSceneDirty();
            Debug.Log($"<color=#00FF99><b>[Debris Replacer]</b></color> Re-rolled rotation for {targetObjects.Count} debris objects.");
        }

        // =========================================================================
        // HELPER METHODS
        // =========================================================================

        private Vector3 CalculateScale(Vector3 originalScale)
        {
            switch (scaleMode)
            {
                case ScaleMode.UniformRandom:
                    float uniformFactor = Random.Range(minUniformScale, maxUniformScale);
                    return Vector3.one * uniformFactor;

                case ScaleMode.NonUniformRandom:
                    return new Vector3(
                        Random.Range(minNonUniformScale.x, maxNonUniformScale.x),
                        Random.Range(minNonUniformScale.y, maxNonUniformScale.y),
                        Random.Range(minNonUniformScale.z, maxNonUniformScale.z)
                    );

                case ScaleMode.MultiplyExisting:
                    float mult = Random.Range(minScaleMultiplier, maxScaleMultiplier);
                    return originalScale * mult;

                default:
                    return Vector3.one;
            }
        }

        private void ApplyMaterialToObject(GameObject targetObj, Material mat)
        {
            if (targetObj == null || mat == null) return;

            MeshRenderer[] renderers = targetObj.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                Undo.RecordObject(r, "Assign Material");
                Material[] mats = new Material[r.sharedMaterials.Length];
                for (int m = 0; m < mats.Length; m++)
                {
                    mats[m] = mat;
                }
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
            }
        }

        private void SetupCollider(GameObject targetObj)
        {
            if (colliderType == ColliderType.None) return;

            // Remove any existing colliders to prevent duplication
            var existingColliders = targetObj.GetComponentsInChildren<Collider>(true);
            foreach (var col in existingColliders)
            {
                Undo.DestroyObjectImmediate(col);
            }

            MeshFilter meshFilter = targetObj.GetComponentInChildren<MeshFilter>(true);
            GameObject colliderHolder = meshFilter != null ? meshFilter.gameObject : targetObj;

            switch (colliderType)
            {
                case ColliderType.MeshColliderConvex:
                    var meshCol = Undo.AddComponent<MeshCollider>(colliderHolder);
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        meshCol.sharedMesh = meshFilter.sharedMesh;
                    }
                    meshCol.convex = true;
                    break;

                case ColliderType.BoxCollider:
                    Undo.AddComponent<BoxCollider>(colliderHolder);
                    break;

                case ColliderType.SphereCollider:
                    Undo.AddComponent<SphereCollider>(colliderHolder);
                    break;
            }
        }

        private void SetupRigidbody(GameObject targetObj)
        {
            Rigidbody rb = targetObj.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = Undo.AddComponent<Rigidbody>(targetObj);
            }
            else
            {
                Undo.RecordObject(rb, "Configure Rigidbody");
            }

            rb.mass = debrisMass;
            rb.useGravity = useGravity;
            rb.isKinematic = isKinematic;
            rb.linearDamping = linearDamping;
            rb.angularDamping = angularDamping;

            EditorUtility.SetDirty(rb);
        }

        private List<GameObject> GetTargetObjectsToProcess()
        {
            List<GameObject> results = new List<GameObject>();

            switch (targetScope)
            {
                case TargetScope.SingleClusterChildren:
                    if (targetClusterRoot != null)
                    {
                        foreach (Transform child in targetClusterRoot.transform)
                        {
                            results.Add(child.gameObject);
                        }
                    }
                    break;

                case TargetScope.AllClustersInScene:
                    List<GameObject> clusters = FindClustersInScene(clusterSearchName);
                    foreach (var cluster in clusters)
                    {
                        foreach (Transform child in cluster.transform)
                        {
                            results.Add(child.gameObject);
                        }
                    }
                    break;

                case TargetScope.CurrentHierarchySelection:
                    if (Selection.gameObjects != null)
                    {
                        results.AddRange(Selection.gameObjects);
                    }
                    break;
            }

            return results;
        }

        private List<GameObject> FindClustersInScene(string nameToFind)
        {
            List<GameObject> clusters = new List<GameObject>();
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in allObjects)
            {
                if (go.name.IndexOf(nameToFind, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    clusters.Add(go);
                }
            }
            return clusters;
        }

        public void AutoDetectTargetCluster()
        {
            if (targetClusterRoot != null) return;

            GameObject found = GameObject.Find("Space_Debris_Cluster");
            if (found != null)
            {
                targetClusterRoot = found;
                return;
            }

            var all = FindClustersInScene("Debris");
            if (all.Count > 0)
            {
                targetClusterRoot = all[0];
            }
        }

        public void AutoDetectProjectAssets()
        {
            if (replacementModels.Count == 0)
            {
                LoadAsteroidModelsFromProject();
            }

            if (randomMaterials.Count == 0)
            {
                LoadMaterialsFromProject();
            }
        }

        public void LoadAsteroidModelsFromProject()
        {
            replacementModels.Clear();
            string[] guids = AssetDatabase.FindAssets("Astreroid_ t:Model", new[] { "Assets/Assets/Astreroid" });
            if (guids == null || guids.Length == 0)
            {
                guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Assets/Astreroid" });
            }

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null && !replacementModels.Contains(model))
                {
                    replacementModels.Add(model);
                }
            }

            Debug.Log($"<color=#00FF99><b>[Debris Replacer]</b></color> Loaded {replacementModels.Count} Asteroid models from Assets/Assets/Astreroid.");
        }

        public void LoadMaterialsFromProject()
        {
            randomMaterials.Clear();

            // 1. Pixel Art Asteroid Material
            string[] asteroidMatGuids = AssetDatabase.FindAssets("M_Asteroid_PixelArt t:Material");
            foreach (var guid in asteroidMatGuids)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat != null && !randomMaterials.Contains(mat)) randomMaterials.Add(mat);
            }

            // 2. PSX Derelict Hull Materials
            string[] derelictDark = AssetDatabase.FindAssets("PSX_DerelictHull_Dark t:Material");
            foreach (var guid in derelictDark)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat != null && !randomMaterials.Contains(mat)) randomMaterials.Add(mat);
            }

            string[] derelictRust = AssetDatabase.FindAssets("PSX_DerelictHull_Rust t:Material");
            foreach (var guid in derelictRust)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat != null && !randomMaterials.Contains(mat)) randomMaterials.Add(mat);
            }

            Debug.Log($"<color=#00FF99><b>[Debris Replacer]</b></color> Loaded {randomMaterials.Count} Materials from project.");
        }

        private void MarkSceneDirty()
        {
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }
    }
}
#endif
