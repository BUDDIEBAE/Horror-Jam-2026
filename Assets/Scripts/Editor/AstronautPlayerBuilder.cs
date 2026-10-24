#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SpaceHorror.Player;
using SpaceHorror.Rendering;
using SpaceHorror.UI;
using SpaceHorror.Audio;
using SpaceHorror.Interaction;
using UnityEngine.UI;
using TMPro;

namespace SpaceHorror.Editor
{
    /// <summary>
    /// Editor utility to generate a fully configured 6DoF Astronaut Player in the scene with 1 click.
    /// Creates Rigidbody, Collider, 6DoF movement, Camera Rig, and Helmet Flashlight.
    /// </summary>
    public static class AstronautPlayerBuilder
    {
        [MenuItem("GameObject/3D Object/Space Horror - 6DoF Astronaut Player", false, 10)]
        [MenuItem("Tools/Space Horror/1. Astronaut & Survival/Create 6DoF Astronaut Player", false, 1)]
        public static void CreateAstronautPlayer()
        {
            // 0. Clean up any existing incomplete player to avoid duplicates
            Vector3 spawnPos = Vector3.zero;
            GameObject existingPlayer = GameObject.Find("Astronaut_Player_6DoF");
            if (existingPlayer != null)
            {
                spawnPos = existingPlayer.transform.position;
                Undo.DestroyObjectImmediate(existingPlayer);
            }
            else if (SceneView.lastActiveSceneView != null)
            {
                spawnPos = SceneView.lastActiveSceneView.pivot;
            }

            // 1. Create Player Root
            GameObject player = new GameObject("Astronaut_Player_6DoF");
            player.transform.position = spawnPos;
            Undo.RegisterCreatedObjectUndo(player, "Create 6DoF Astronaut");

            // 2. Add Capsule Collider (Spacesuit dimensions)
            CapsuleCollider collider = player.AddComponent<CapsuleCollider>();
            collider.height = 1.9f;
            collider.radius = 0.45f;
            collider.center = new Vector3(0f, 0f, 0f);

            // 3. Add Rigidbody (Physics settings)
            Rigidbody rb = player.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.mass = 120f; // Typical EVA suit + astronaut mass in kg
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.1f;

            // 4. Add Player Scripts
            AstronautInputHandler inputHandler = player.AddComponent<AstronautInputHandler>();
            AstronautMovement6D movement = player.AddComponent<AstronautMovement6D>();
            AstronautSurvival survival = player.AddComponent<AstronautSurvival>();
            AstronautAudioSystem audioSystem = player.AddComponent<AstronautAudioSystem>();
            AstronautInteraction interaction = player.AddComponent<AstronautInteraction>();

            // 5. Create Camera Mount & First Person Camera
            GameObject cameraObj = new GameObject("PlayerCamera");
            cameraObj.transform.SetParent(player.transform);
            cameraObj.transform.localPosition = new Vector3(0f, 0.45f, 0.1f); // Eye height in suit
            cameraObj.transform.localRotation = Quaternion.identity;

            Camera cam = cameraObj.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 75f;
            cam.clearFlags = CameraClearFlags.Skybox;

            cameraObj.AddComponent<AudioListener>();

            // Disable old main cameras in scene if any to prevent conflicts
            Camera oldCam = Camera.main;
            if (oldCam != null && oldCam != cam)
            {
                Undo.RecordObject(oldCam.gameObject, "Disable old MainCamera");
                oldCam.gameObject.tag = "Untagged";
                oldCam.enabled = false;
            }
            cameraObj.tag = "MainCamera";

            // Add Camera Rig script
            AstronautCameraRig cameraRig = cameraObj.AddComponent<AstronautCameraRig>();
            cameraRig.Initialize(movement, cameraObj.transform);

            // 6. Create Helmet Flashlight
            GameObject flashlightObj = new GameObject("HelmetFlashlight");
            flashlightObj.transform.SetParent(player.transform);
            flashlightObj.transform.position = cameraObj.transform.position + cameraObj.transform.forward * 0.1f;
            flashlightObj.transform.rotation = cameraObj.transform.rotation;

            Light spotLight = flashlightObj.AddComponent<Light>();
            spotLight.type = LightType.Spot;
            spotLight.range = 35f;
            spotLight.spotAngle = 48f;
            spotLight.innerSpotAngle = 28f;
            spotLight.intensity = 4.0f;
            spotLight.color = new Color(0.92f, 0.95f, 1.0f); // Cold space LED
            spotLight.shadows = LightShadows.Soft;

            GameObject beamObj = CreateVolumetricBeamObject(flashlightObj.transform);

            HelmetFlashlight flashlightScript = flashlightObj.AddComponent<HelmetFlashlight>();
            flashlightScript.Initialize(cameraObj.transform, spotLight, beamObj);

            // 7. Setup Helmet Visor HUD
            SetupVisorHUDInternal(player, cam);

            // 8. Select newly created player
            Selection.activeGameObject = player;
            Debug.Log("<color=#00FF99><b>[Space Horror]</b></color> 6DoF Astronaut Player successfully generated in scene!");
        }

        [MenuItem("Tools/Space Horror/1. Astronaut & Survival/Attach Survival System (O2 & Heartbeat)", false, 2)]
        public static void AttachSurvivalSystem()
        {
            GameObject player = GameObject.Find("Astronaut_Player_6DoF");
            if (player == null)
            {
                var movement = Object.FindFirstObjectByType<AstronautMovement6D>();
                if (movement != null) player = movement.gameObject;
            }

            if (player == null)
            {
                Debug.LogWarning("[Space Horror] Could not find Astronaut Player in the scene. Please create one using 'Tools > Space Horror > Create 6DoF Astronaut Player'.");
                return;
            }

            AstronautSurvival survival = player.GetComponent<AstronautSurvival>();
            if (survival == null)
            {
                survival = Undo.AddComponent<AstronautSurvival>(player);
                Debug.Log("<color=#00FF99><b>[Space Horror]</b></color> AstronautSurvival component successfully attached to Player!");
            }
            else
            {
                Debug.Log("<color=#00FF99><b>[Space Horror]</b></color> AstronautSurvival component already exists on Player.");
            }

            AstronautAudioSystem audioSystem = player.GetComponent<AstronautAudioSystem>();
            if (audioSystem == null)
            {
                audioSystem = Undo.AddComponent<AstronautAudioSystem>(player);
                Debug.Log("<color=#00FF99><b>[Space Horror]</b></color> AstronautAudioSystem component successfully attached to Player!");
            }

            Selection.activeGameObject = player;
        }

        [MenuItem("Tools/Space Horror/1. Astronaut & Survival/Setup In-Helmet Audio System (Heartbeat & Breathing)", false, 4)]
        public static void SetupAudioSystem()
        {
            GameObject player = GameObject.Find("Astronaut_Player_6DoF");
            if (player == null)
            {
                var movement = Object.FindFirstObjectByType<AstronautMovement6D>();
                if (movement != null) player = movement.gameObject;
            }

            if (player == null)
            {
                Debug.LogWarning("[Space Horror] Could not find Astronaut Player in the scene!");
                return;
            }

            AstronautSurvival survival = player.GetComponent<AstronautSurvival>();
            if (survival == null) survival = Undo.AddComponent<AstronautSurvival>(player);

            AstronautAudioSystem audioSystem = player.GetComponent<AstronautAudioSystem>();
            if (audioSystem == null)
            {
                audioSystem = Undo.AddComponent<AstronautAudioSystem>(player);
                Debug.Log("<color=#00FF99><b>[Space Horror]</b></color> AstronautAudioSystem attached to player with procedural audio synthesizer!");
            }
            else
            {
                Debug.Log("<color=#00FF99><b>[Space Horror]</b></color> AstronautAudioSystem already active on Player.");
            }

            AstronautInteraction interaction = player.GetComponent<AstronautInteraction>();
            if (interaction == null)
            {
                interaction = Undo.AddComponent<AstronautInteraction>(player);
                Debug.Log("<color=#00FF99><b>[Space Horror]</b></color> AstronautInteraction attached to Player!");
            }

            Selection.activeGameObject = player;
        }

        [MenuItem("Tools/Space Horror/1. Astronaut & Survival/Setup Astronaut Interaction System", false, 5)]
        public static void SetupInteractionSystem()
        {
            GameObject player = GameObject.Find("Astronaut_Player_6DoF");
            if (player == null)
            {
                var movement = Object.FindFirstObjectByType<AstronautMovement6D>();
                if (movement != null) player = movement.gameObject;
            }

            if (player == null)
            {
                Debug.LogWarning("[Space Horror] Could not find Astronaut Player in the scene!");
                return;
            }

            AstronautInteraction interaction = player.GetComponent<AstronautInteraction>();
            if (interaction == null)
            {
                interaction = Undo.AddComponent<AstronautInteraction>(player);
                Debug.Log("<color=#00FF99><b>[Space Horror]</b></color> AstronautInteraction component attached to player!");
            }
            else
            {
                Debug.Log("<color=#00FF99><b>[Space Horror]</b></color> AstronautInteraction component already active on Player.");
            }

            Selection.activeGameObject = player;
        }

        [MenuItem("Tools/Space Horror/1. Astronaut & Survival/Setup Helmet Visor HUD (Crisp Overlay)", false, 3)]
        public static void SetupVisorHUD()
        {
            GameObject player = GameObject.Find("Astronaut_Player_6DoF");
            if (player == null)
            {
                var movement = Object.FindFirstObjectByType<AstronautMovement6D>();
                if (movement != null) player = movement.gameObject;
            }

            if (player == null)
            {
                Debug.LogWarning("[Space Horror] Could not find Astronaut Player in the scene!");
                return;
            }

            Camera cam = player.GetComponentInChildren<Camera>();
            if (cam == null) cam = Camera.main;

            SetupVisorHUDInternal(player, cam);
        }

        public static GameObject SetupVisorHUDInternal(GameObject player, Camera cam)
        {
            // 1. Ensure AstronautSurvival component is present
            AstronautSurvival survival = player.GetComponent<AstronautSurvival>();
            if (survival == null) survival = Undo.AddComponent<AstronautSurvival>(player);

            AstronautAudioSystem audioSystem = player.GetComponent<AstronautAudioSystem>();
            if (audioSystem == null) audioSystem = Undo.AddComponent<AstronautAudioSystem>(player);

            AstronautInteraction interaction = player.GetComponent<AstronautInteraction>();
            if (interaction == null) interaction = Undo.AddComponent<AstronautInteraction>(player);

            AstronautMovement6D movement = player.GetComponent<AstronautMovement6D>();
            HelmetFlashlight flashlight = player.GetComponentInChildren<HelmetFlashlight>();

            // 2. Remove old canvas if any
            GameObject existingCanvas = GameObject.Find("Helmet_Visor_Canvas");
            if (existingCanvas != null)
            {
                Undo.DestroyObjectImmediate(existingCanvas);
            }

            // 3. Ensure UI asset folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Settings/UI"))
            {
                AssetDatabase.CreateFolder("Assets/Settings", "UI");
            }

            Sprite heartSprite = GetOrCreateHeartSprite();
            Sprite vignetteSprite = GetOrCreateVignetteSprite();
            Sprite flatWhiteSprite = GetOrCreateWhiteSprite();

            // 4. Create Screen-Space Overlay Canvas (Crisp Sci-Fi Visor like Signalis / Dead Space)
            GameObject canvasObj = new GameObject("Helmet_Visor_Canvas");
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Helmet Visor Canvas");
            canvasObj.transform.SetParent(player.transform, false);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // 5. Create Suffocation Tunnel Vision (Full stretch)
            Image suffocationImg = CreateImage(canvasObj.transform, "Suffocation_Tunnel_Vision", vignetteSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));

            // 6. Create Subtle Visor Curvature Vignette
            // 6. Create Subtle Visor Curvature Vignette (Noir Shadow Lens)
            Image visorFrameImg = CreateImage(canvasObj.transform, "Visor_Curved_Frame", vignetteSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.01f, 0.015f, 0.02f, 0.40f));

            // 7. Top Telemetry Pod (RCS & Lamp)
            GameObject topGroup = new GameObject("Top_Telemetry_Panel");
            topGroup.transform.SetParent(canvasObj.transform, false);
            RectTransform topRect = topGroup.AddComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0.5f, 1f);
            topRect.anchorMax = new Vector2(0.5f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.anchoredPosition = new Vector2(0f, -30f);
            topRect.sizeDelta = new Vector2(440f, 44f);

            // Dark Film Noir obsidian backing pod
            CreateImage(topGroup.transform, "Top_BG", flatWhiteSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.03f, 0.03f, 0.04f, 0.88f));

            // Silver center divider
            CreateImage(topGroup.transform, "Top_Divider", flatWhiteSprite,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, -14f), new Color(0.6f, 0.65f, 0.7f, 0.4f));

            TMP_Text rcsText = CreateTMPText(topGroup.transform, "RCS_Status_Text", "RCS: STABILIZED",
                new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(185f, 26f), 16f,
                TextAlignmentOptions.Right, new Color(0.92f, 0.94f, 0.96f, 0.95f));

            TMP_Text lampText = CreateTMPText(topGroup.transform, "Lamp_Status_Text", "LAMP: ON",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(185f, 26f), 16f,
                TextAlignmentOptions.Left, new Color(1f, 1f, 1f, 0.95f));

            // 8. Bottom-Left Life Support Pod (Oxygen Bar & Suit Status)
            GameObject lifeSupportGroup = new GameObject("LifeSupport_Panel");
            lifeSupportGroup.transform.SetParent(canvasObj.transform, false);
            RectTransform lsRect = lifeSupportGroup.AddComponent<RectTransform>();
            lsRect.anchorMin = new Vector2(0f, 0f);
            lsRect.anchorMax = new Vector2(0f, 0f);
            lsRect.pivot = new Vector2(0f, 0f);
            lsRect.anchoredPosition = new Vector2(45f, 40f);
            lsRect.sizeDelta = new Vector2(340f, 105f);

            // Dark Film Noir obsidian backing pod
            CreateImage(lifeSupportGroup.transform, "LifeSupport_BG", flatWhiteSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.03f, 0.03f, 0.04f, 0.88f));

            // Silver accent bar
            CreateImage(lifeSupportGroup.transform, "Accent_Bar", flatWhiteSprite,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, 0f), new Color(0.92f, 0.94f, 0.96f, 0.95f));

            TMP_Text o2Text = CreateTMPText(lifeSupportGroup.transform, "Oxygen_Value_Text", "O2: 100%",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -14f), new Vector2(295f, 26f), 20f,
                TextAlignmentOptions.Left, new Color(0.95f, 0.97f, 1f, 0.95f));

            Image o2BarBG = CreateImage(lifeSupportGroup.transform, "Oxygen_Bar_BG", flatWhiteSprite,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -48f), new Vector2(295f, 14f),
                new Color(0.12f, 0.13f, 0.15f, 0.95f));

            Image o2BarFill = CreateImage(o2BarBG.transform, "Oxygen_Bar_Fill", flatWhiteSprite,
                Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero,
                new Color(0.95f, 0.97f, 1f, 0.95f), Image.Type.Filled);
            o2BarFill.fillMethod = Image.FillMethod.Horizontal;
            o2BarFill.fillOrigin = 0;
            o2BarFill.fillAmount = 1f;

            TMP_Text suitText = CreateTMPText(lifeSupportGroup.transform, "Suit_Status_Text", "SUIT: 100% OK",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(22f, 14f), new Vector2(295f, 22f), 16f,
                TextAlignmentOptions.Left, new Color(0.85f, 0.88f, 0.92f, 0.9f));

            // 9. Bottom-Right Biometrics Pod (Heartbeat & BPM)
            GameObject biometricsGroup = new GameObject("Biometrics_Panel");
            biometricsGroup.transform.SetParent(canvasObj.transform, false);
            RectTransform bioRect = biometricsGroup.AddComponent<RectTransform>();
            bioRect.anchorMin = new Vector2(1f, 0f);
            bioRect.anchorMax = new Vector2(1f, 0f);
            bioRect.pivot = new Vector2(1f, 0f);
            bioRect.anchoredPosition = new Vector2(-45f, 40f);
            bioRect.sizeDelta = new Vector2(240f, 75f);

            // Dark Film Noir obsidian backing pod
            CreateImage(biometricsGroup.transform, "Biometrics_BG", flatWhiteSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.03f, 0.03f, 0.04f, 0.88f));

            // Silver accent bar
            CreateImage(biometricsGroup.transform, "Accent_Bar", flatWhiteSprite,
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(4f, 0f), new Color(0.92f, 0.94f, 0.96f, 0.95f));

            Image heartImg = CreateImage(biometricsGroup.transform, "Heart_Icon", heartSprite,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(28f, 28f),
                new Color(0.95f, 0.97f, 1f, 0.95f));

            TMP_Text heartText = CreateTMPText(biometricsGroup.transform, "HeartRate_Text", "72 BPM",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(65f, 0f), new Vector2(150f, 32f), 24f,
                TextAlignmentOptions.Left, new Color(0.95f, 0.97f, 1f, 0.95f));

            // 10. Warning Alert Panel (Center Screen)
            GameObject warningObj = new GameObject("Warning_Alert_Panel");
            warningObj.transform.SetParent(canvasObj.transform, false);
            RectTransform warnRect = warningObj.AddComponent<RectTransform>();
            warnRect.anchorMin = new Vector2(0.5f, 0.5f);
            warnRect.anchorMax = new Vector2(0.5f, 0.5f);
            warnRect.pivot = new Vector2(0.5f, 0.5f);
            warnRect.anchoredPosition = new Vector2(0f, 130f);
            warnRect.sizeDelta = new Vector2(620f, 54f);

            CanvasGroup warnGroup = warningObj.AddComponent<CanvasGroup>();
            warnGroup.alpha = 0f;

            // Warning dark red backing pod
            CreateImage(warningObj.transform, "Warning_BG", flatWhiteSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.25f, 0.02f, 0.04f, 0.92f));

            TMP_Text warnText = CreateTMPText(warningObj.transform, "Warning_Text", "[!] WARNING: OXYGEN LEVEL CRITICAL",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 44f), 22f,
                TextAlignmentOptions.Center, new Color(1f, 0.22f, 0.25f, 1f));

            // 11. Interaction Prompt Pod (Center-Bottom Reticle Pod)
            GameObject interactObj = new GameObject("Interaction_Prompt_Panel");
            interactObj.transform.SetParent(canvasObj.transform, false);
            RectTransform interactRect = interactObj.AddComponent<RectTransform>();
            interactRect.anchorMin = new Vector2(0.5f, 0.5f);
            interactRect.anchorMax = new Vector2(0.5f, 0.5f);
            interactRect.pivot = new Vector2(0.5f, 0.5f);
            interactRect.anchoredPosition = new Vector2(0f, -90f);
            interactRect.sizeDelta = new Vector2(380f, 62f);

            CanvasGroup interactGroup = interactObj.AddComponent<CanvasGroup>();
            interactGroup.alpha = 0f;

            // Dark Film Noir obsidian backing pod
            CreateImage(interactObj.transform, "Interact_BG", flatWhiteSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.02f, 0.025f, 0.03f, 0.90f));

            // Silver left accent line
            CreateImage(interactObj.transform, "Interact_Accent", flatWhiteSprite,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(3f, 0f), new Color(0.92f, 0.94f, 0.96f, 0.95f));

            TMP_Text interactPromptText = CreateTMPText(interactObj.transform, "Prompt_Text", "[LMB] REPAIR OXYGEN LINE (HOLD)",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 8f), new Vector2(350f, 26f), 17f,
                TextAlignmentOptions.Left, new Color(1f, 1f, 1f, 1f));

            TMP_Text interactSubtext = CreateTMPText(interactObj.transform, "Sub_Text", "PRIMARY O2 LIFE SUPPORT",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, -14f), new Vector2(350f, 20f), 12f,
                TextAlignmentOptions.Left, new Color(0.68f, 0.72f, 0.78f, 0.85f));

            // Progress bar
            Image interactBarBG = CreateImage(interactObj.transform, "Progress_Bar_BG", flatWhiteSprite,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(0f, 4f),
                new Color(0.12f, 0.13f, 0.15f, 0.95f));

            Image interactBarFill = CreateImage(interactBarBG.transform, "Progress_Bar_Fill", flatWhiteSprite,
                Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero,
                new Color(1f, 1f, 1f, 0.95f), Image.Type.Filled);
            interactBarFill.fillMethod = Image.FillMethod.Horizontal;
            interactBarFill.fillOrigin = 0;
            interactBarFill.fillAmount = 0f;
            interactBarBG.gameObject.SetActive(false);

            // 12. Status Message Toast (Below Prompt)
            GameObject statusObj = new GameObject("Status_Message_Panel");
            statusObj.transform.SetParent(canvasObj.transform, false);
            RectTransform statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 0.5f);
            statusRect.anchorMax = new Vector2(0.5f, 0.5f);
            statusRect.pivot = new Vector2(0.5f, 0.5f);
            statusRect.anchoredPosition = new Vector2(0f, -155f);
            statusRect.sizeDelta = new Vector2(480f, 40f);

            CanvasGroup statusGroup = statusObj.AddComponent<CanvasGroup>();
            statusGroup.alpha = 0f;

            TMP_Text statusMsgText = CreateTMPText(statusObj.transform, "Status_Text", "[+] OXYGEN REFILLED +40%",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 32f), 19f,
                TextAlignmentOptions.Center, new Color(0.96f, 0.98f, 1f, 1f));

            // 13. Mission Transmission / Audio Log Popup (Center-Top)
            GameObject transObj = new GameObject("Transmission_Log_Panel");
            transObj.transform.SetParent(canvasObj.transform, false);
            RectTransform transRect = transObj.AddComponent<RectTransform>();
            transRect.anchorMin = new Vector2(0.5f, 0.5f);
            transRect.anchorMax = new Vector2(0.5f, 0.5f);
            transRect.pivot = new Vector2(0.5f, 0.5f);
            transRect.anchoredPosition = new Vector2(0f, 195f);
            transRect.sizeDelta = new Vector2(560f, 120f);

            CanvasGroup transGroup = transObj.AddComponent<CanvasGroup>();
            transGroup.alpha = 0f;

            CreateImage(transObj.transform, "Transmission_BG", flatWhiteSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.02f, 0.025f, 0.03f, 0.92f));

            CreateImage(transObj.transform, "Transmission_TopBar", flatWhiteSprite,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 3f), new Color(0.85f, 0.88f, 0.92f, 0.95f));

            TMP_Text transTitleText = CreateTMPText(transObj.transform, "Title_Text", ">> INCOMING TRANSMISSION LOG",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -14f), new Vector2(520f, 24f), 16f,
                TextAlignmentOptions.Left, new Color(0.96f, 0.98f, 1f, 1f));

            TMP_Text transBodyText = CreateTMPText(transObj.transform, "Body_Text", "Transcript text here...",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 14f), new Vector2(520f, 65f), 14f,
                TextAlignmentOptions.Left, new Color(0.85f, 0.88f, 0.92f, 0.95f));

            // 14. Attach AstronautHelmetHUD script and wire bindings
            AstronautHelmetHUD hud = canvasObj.AddComponent<AstronautHelmetHUD>();
            hud.ConfigureBindings(
                canvas,
                visorFrameImg,
                o2BarFill,
                null,
                o2Text,
                heartImg,
                heartText,
                suitText,
                rcsText,
                lampText,
                warnGroup,
                warnText,
                suffocationImg,
                interactGroup,
                interactPromptText,
                interactBarFill,
                interactSubtext,
                statusGroup,
                statusMsgText,
                transGroup,
                transTitleText,
                transBodyText
            );

            hud.Initialize(survival, movement, flashlight);
            Selection.activeGameObject = canvasObj;

            Debug.Log("<color=#00FF99><b>[Space Horror]</b></color> Crisp Sci-Fi Helmet Visor HUD with Interaction Telemetry successfully created!");
            return canvasObj;
        }

        private static TMP_Text CreateTMPText(Transform parent, string name, string text, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size, Color color, Image.Type type = Image.Type.Simple)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            Image img = obj.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = type;
            img.raycastTarget = false;
            return img;
        }

        private static Sprite GetOrCreateHeartSprite()
        {
            string path = "Assets/Settings/UI/PSX_PixelHeart.png";
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) return s;

            Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            string[] pattern = new string[]
            {
                "................",
                "................",
                "...##......##...",
                "..####....####..",
                ".######..######.",
                ".##############.",
                ".##############.",
                ".##############.",
                "..############..",
                "...##########...",
                "....########....",
                ".....######.....",
                "......####......",
                ".......##.......",
                "................",
                "................"
            };

            for (int y = 0; y < 16; y++)
            {
                string row = pattern[15 - y];
                for (int x = 0; x < 16; x++)
                {
                    tex.SetPixel(x, y, row[x] == '#' ? Color.white : Color.clear);
                }
            }
            tex.Apply();

            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.filterMode = FilterMode.Point;
                ti.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite GetOrCreateVignetteSprite()
        {
            string path = "Assets/Settings/UI/PSX_VisorVignette.png";
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) return s;

            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.SmoothStep(0.2f, 1.0f, dist);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.filterMode = FilterMode.Bilinear;
                ti.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite GetOrCreateWhiteSprite()
        {
            string path = "Assets/Settings/UI/PSX_WhitePixel.png";
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) return s;

            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    tex.SetPixel(x, y, Color.white);
            tex.Apply();

            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.filterMode = FilterMode.Point;
                ti.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        [MenuItem("Tools/Space Horror/3. Environment & Space/Spawn Test Space Debris Cluster", false, 22)]
        public static void CreateTestDebrisCluster()
        {
            GameObject debrisRoot = new GameObject("Space_Debris_Cluster");
            Undo.RegisterCreatedObjectUndo(debrisRoot, "Create Debris Cluster");

            Vector3 center = Vector3.zero;
            if (SceneView.lastActiveSceneView != null)
            {
                center = SceneView.lastActiveSceneView.pivot + SceneView.lastActiveSceneView.rotation * Vector3.forward * 10f;
            }

            debrisRoot.transform.position = center;

            // Generate several floating blocks to navigate around and test 6DoF collisions
            int count = 15;
            for (int i = 0; i < count; i++)
            {
                GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = $"Derelict_Module_{i + 1}";
                block.transform.SetParent(debrisRoot.transform);

                Vector3 randomOffset = new Vector3(
                    Random.Range(-25f, 25f),
                    Random.Range(-20f, 20f),
                    Random.Range(5f, 50f)
                );
                block.transform.position = center + randomOffset;
                block.transform.localScale = new Vector3(
                    Random.Range(1.5f, 8f),
                    Random.Range(1.5f, 8f),
                    Random.Range(2f, 15f)
                );
                block.transform.rotation = Random.rotation;

                // Add non-kinematic or static physics for collision
                Rigidbody blockRb = block.AddComponent<Rigidbody>();
                blockRb.useGravity = false;
                blockRb.mass = 500f;
                blockRb.linearDamping = 0.5f;
                blockRb.angularDamping = 0.5f;
            }

            Selection.activeGameObject = debrisRoot;
            Debug.Log("<color=#00FF99><b>[Space Horror]</b></color> Test Debris Cluster created for 6DoF navigation testing!");
        }

        [MenuItem("Tools/Space Horror/3. Environment & Space/Spawn Sample Objective - Oxygen Station", false, 24)]
        public static void CreateSampleOxygenStation()
        {
            Vector3 spawnPos = Vector3.forward * 4f;
            if (SceneView.lastActiveSceneView != null)
            {
                spawnPos = SceneView.lastActiveSceneView.pivot + SceneView.lastActiveSceneView.rotation * Vector3.forward * 3.5f;
            }

            GameObject station = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            station.name = "Station_O2_Recharger";
            station.transform.position = spawnPos;
            station.transform.localScale = new Vector3(0.5f, 0.75f, 0.5f);
            Undo.RegisterCreatedObjectUndo(station, "Spawn Sample O2 Station");

            // Point Light for sci-fi atmosphere
            GameObject lightObj = new GameObject("O2_Indicator_Light");
            lightObj.transform.SetParent(station.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            Light pLight = lightObj.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.range = 6f;
            pLight.color = new Color(0.92f, 0.95f, 1.0f); // Cold Film Noir starlight silver
            pLight.intensity = 2.5f;

            // MissionInteractable
            MissionInteractable interactable = station.AddComponent<MissionInteractable>();
            SerializedObject so = new SerializedObject(interactable);
            so.FindProperty("objectiveTitle").stringValue = "Emergency O2 Station Alpha";
            so.FindProperty("actionPrompt").stringValue = "RECHARGE OXYGEN TANK";
            so.FindProperty("completionMessage").stringValue = "OXYGEN SUPPLY RECHARGED +40%";
            so.FindProperty("objectiveType").enumValueIndex = (int)ObjectiveType.RefillOxygen;
            so.FindProperty("interactionMethod").enumValueIndex = (int)InteractionMethod.HoldToComplete;
            so.FindProperty("holdDuration").floatValue = 1.5f;
            so.FindProperty("oxygenAmount").floatValue = 40f;
            so.FindProperty("visualRenderer").objectReferenceValue = station.GetComponent<MeshRenderer>();
            so.ApplyModifiedProperties();

            Selection.activeGameObject = station;
            Debug.Log("<color=#00FF99><b>[Space Horror]</b></color> Sample Oxygen Station created! Look at it and hold [Left Click] to test.");
        }

        [MenuItem("Tools/Space Horror/3. Environment & Space/Spawn Sample Objective - Data Log Terminal", false, 25)]
        public static void CreateSampleDataTerminal()
        {
            Vector3 spawnPos = Vector3.forward * 4f;
            if (SceneView.lastActiveSceneView != null)
            {
                spawnPos = SceneView.lastActiveSceneView.pivot + SceneView.lastActiveSceneView.rotation * Vector3.forward * 3.5f;
            }

            GameObject terminal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            terminal.name = "Derelict_Data_Terminal";
            terminal.transform.position = spawnPos;
            terminal.transform.localScale = new Vector3(0.6f, 1.0f, 0.4f);
            Undo.RegisterCreatedObjectUndo(terminal, "Spawn Sample Data Terminal");

            // Point Light for amber horror beacon
            GameObject lightObj = new GameObject("Terminal_Light");
            lightObj.transform.SetParent(terminal.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.6f, 0.3f);
            Light pLight = lightObj.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.range = 5f;
            pLight.color = new Color(0.95f, 0.90f, 0.82f); // Vintage Film Noir tungsten silver
            pLight.intensity = 2.0f;

            // MissionInteractable
            MissionInteractable interactable = terminal.AddComponent<MissionInteractable>();
            SerializedObject so = new SerializedObject(interactable);
            so.FindProperty("objectiveTitle").stringValue = "Derelict Flight Recorder #07";
            so.FindProperty("actionPrompt").stringValue = "EXTRACT AUDIO LOG";
            so.FindProperty("completionMessage").stringValue = "AUDIO LOG EXTRACTED - RECORD SAVED";
            so.FindProperty("objectiveType").enumValueIndex = (int)ObjectiveType.CollectDataLog;
            so.FindProperty("interactionMethod").enumValueIndex = (int)InteractionMethod.HoldToComplete;
            so.FindProperty("holdDuration").floatValue = 2.0f;
            so.FindProperty("dataLogTranscript").stringValue = "TRANSMISSION RECOVERED:\n'Reactor containment is breached... If anyone receives this transmission, do not approach Sector 4.'";
            so.FindProperty("visualRenderer").objectReferenceValue = terminal.GetComponent<MeshRenderer>();
            so.ApplyModifiedProperties();

            Selection.activeGameObject = terminal;
            Debug.Log("<color=#00FF99><b>[Space Horror]</b></color> Sample Data Log Terminal created! Look at it and hold [Left Click] to test.");
        }

        [MenuItem("Tools/Space Horror/3. Environment & Space/Setup Deep Space Lighting & Skybox", false, 21)]
        public static void SetupDeepSpaceSkybox()
        {
            // 1. Create or load Deep Space Skybox Material
            string matPath = "Assets/Settings/DeepSpace_Skybox.mat";
            Material skyMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (skyMat == null)
            {
                Shader proceduralShader = Shader.Find("Skybox/Procedural");
                if (proceduralShader == null) proceduralShader = Shader.Find("Universal Render Pipeline/Procedural");

                skyMat = new Material(proceduralShader);
                skyMat.name = "DeepSpace_Skybox";

                // Configure for deep space void: no atmosphere, pitch black void, tiny distant sun
                skyMat.SetFloat("_SunSize", 0.005f);
                skyMat.SetFloat("_SunSizeConvergence", 10f);
                skyMat.SetFloat("_AtmosphereThickness", 0.0f); // Vacuum = 0 atmosphere
                skyMat.SetColor("_SkyTint", new Color(0.01f, 0.015f, 0.03f)); // Near black void
                skyMat.SetColor("_GroundColor", Color.black);
                skyMat.SetFloat("_Exposure", 0.4f);

                AssetDatabase.CreateAsset(skyMat, matPath);
                AssetDatabase.SaveAssets();
            }

            // 2. Apply to RenderSettings
            RenderSettings.skybox = skyMat;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.02f, 0.025f, 0.04f); // Very faint starlight glow
            RenderSettings.reflectionIntensity = 0.2f;

            // 3. Darken or adjust Directional Light in scene to mimic distant starlight
            Light dirLight = Object.FindFirstObjectByType<Light>();
            if (dirLight != null && dirLight.type == LightType.Directional)
            {
                Undo.RecordObject(dirLight, "Adjust Directional Light for Space");
                dirLight.intensity = 0.15f; // Dim cold starlight
                dirLight.color = new Color(0.6f, 0.7f, 0.9f);
            }

            Debug.Log("<color=#00FF99><b>[Space Horror]</b></color> Deep Space Lighting & Skybox successfully applied!");
        }

        [MenuItem("Tools/Space Horror/2. PSX Retro & Graphics/Apply Film Noir Volume Profile", false, 14)]
        public static void ApplyFilmNoirStyle()
        {
            // 1. Find or create Global Volume in scene
            Volume volume = Object.FindFirstObjectByType<Volume>();
            if (volume == null)
            {
                GameObject volumeObj = new GameObject("Global Volume");
                Undo.RegisterCreatedObjectUndo(volumeObj, "Create Film Noir Global Volume");
                volume = volumeObj.AddComponent<Volume>();
                volume.isGlobal = true;
            }

            // 2. Create or load Film Noir Volume Profile
            string profilePath = "Assets/Settings/FilmNoir_Profile.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "FilmNoir_Profile";
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            Undo.RecordObject(profile, "Configure Film Noir Overrides");

            // A. Color Adjustments: Pure Black & White + High Contrast Chiaroscuro
            if (!profile.TryGet(out ColorAdjustments colorAdjustments))
            {
                colorAdjustments = profile.Add<ColorAdjustments>(true);
            }
            colorAdjustments.saturation.overrideState = true;
            colorAdjustments.saturation.value = -100f; // Pure Film Noir Monochrome

            colorAdjustments.contrast.overrideState = true;
            colorAdjustments.contrast.value = 50f; // Crushed blacks & striking highlights

            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.postExposure.value = 0.2f;

            // B. Film Grain: 1940s analog cinema texture
            if (!profile.TryGet(out FilmGrain filmGrain))
            {
                filmGrain = profile.Add<FilmGrain>(true);
            }
            filmGrain.type.overrideState = true;
            filmGrain.type.value = FilmGrainLookup.Medium1;
            filmGrain.intensity.overrideState = true;
            filmGrain.intensity.value = 0.7f;
            filmGrain.response.overrideState = true;
            filmGrain.response.value = 0.75f;

            // C. Vignette: Heavy dark edges for claustrophobic helmet feel
            if (!profile.TryGet(out Vignette vignette))
            {
                vignette = profile.Add<Vignette>(true);
            }
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.45f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.38f;

            // D. Bloom: Halation around flashlights and metallic reflections
            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = profile.Add<Bloom>(true);
            }
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.0f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.6f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.65f;

            // E. Lens Distortion: Curved vintage optical lens / astronaut visor glass
            if (!profile.TryGet(out LensDistortion lensDistortion))
            {
                lensDistortion = profile.Add<LensDistortion>(true);
            }
            lensDistortion.intensity.overrideState = true;
            lensDistortion.intensity.value = -0.1f;

            // F. Tonemapping: ACES for high dynamic range
            if (!profile.TryGet(out Tonemapping tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>(true);
            }
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.ACES;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            // 3. Assign profile to volume
            Undo.RecordObject(volume, "Assign Film Noir Profile");
            volume.profile = profile;

            // 4. Ensure Main / Player Cameras have Post-Processing enabled
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                var camData = cam.GetComponent<UniversalAdditionalCameraData>();
                if (camData != null)
                {
                    camData.renderPostProcessing = true;
                }
            }

            Debug.Log("<color=#00FF99><b>[Space Horror]</b></color> Film Noir Post-Processing applied successfully!");
        }

        [MenuItem("Tools/Space Horror/2. PSX Retro & Graphics/Enable PSX Post-Processing (Feature)", false, 11)]
        public static void EnablePSXPostProcessing()
        {
            string[] rendererPaths = new string[] 
            { 
                "Assets/Settings/PC_Renderer.asset",
                "Assets/Settings/Mobile_Renderer.asset"
            };

            int attachedCount = 0;

            foreach (var rendererPath in rendererPaths)
            {
                ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
                if (rendererData == null) continue;

                // Check if feature already exists
                bool exists = false;
                foreach (var feat in rendererData.rendererFeatures)
                {
                    if (feat is PSXPostProcessRendererFeature)
                    {
                        exists = true;
                        feat.SetActive(true);
                        break;
                    }
                }

                if (!exists)
                {
                    Undo.RecordObject(rendererData, "Add PSX Retro PostProcess Feature");

                    var feature = ScriptableObject.CreateInstance<PSXPostProcessRendererFeature>();
                    feature.name = "PSXPostProcessRendererFeature";
                    AssetDatabase.AddObjectToAsset(feature, rendererData);

                    SerializedObject so = new SerializedObject(rendererData);
                    SerializedProperty prop = so.FindProperty("m_RendererFeatures");
                    int index = prop.arraySize;
                    prop.InsertArrayElementAtIndex(index);
                    prop.GetArrayElementAtIndex(index).objectReferenceValue = feature;
                    so.ApplyModifiedProperties();

                    EditorUtility.SetDirty(rendererData);
                    attachedCount++;
                }
            }

            AssetDatabase.SaveAssets();

            // Ensure cameras have PostProcessing enabled
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                var camData = cam.GetComponent<UniversalAdditionalCameraData>();
                if (camData != null)
                {
                    camData.renderPostProcessing = true;
                }
            }

            Debug.Log($"<color=#00FF99><b>[Space Horror]</b></color> PSX Retro Post-Processing (Phase 1) successfully enabled on {attachedCount} Renderers!");
        }

        [MenuItem("Tools/Space Horror/2. PSX Retro & Graphics/Apply PSX Shaders to Space Debris", false, 12)]
        public static void ApplyPSXShadersToDebris()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/Settings", "Materials");
            }

            Shader litShader = Shader.Find("PSX/PSX_Lit_URP");
            if (litShader == null)
            {
                Debug.LogError("PSX/PSX_Lit_URP shader not found!");
                return;
            }

            // 1. Create or load sample PSX Materials
            string matPath1 = "Assets/Settings/Materials/PSX_DerelictHull_Dark.mat";
            Material mat1 = AssetDatabase.LoadAssetAtPath<Material>(matPath1);
            if (mat1 == null)
            {
                mat1 = new Material(litShader);
                mat1.name = "PSX_DerelictHull_Dark";
                mat1.SetColor("_BaseColor", new Color(0.75f, 0.78f, 0.82f));
                mat1.SetFloat("_GridResolution", 200f);
                mat1.SetFloat("_TextureWarping", 1f);
                AssetDatabase.CreateAsset(mat1, matPath1);
            }

            string matPath2 = "Assets/Settings/Materials/PSX_DerelictHull_Rust.mat";
            Material mat2 = AssetDatabase.LoadAssetAtPath<Material>(matPath2);
            if (mat2 == null)
            {
                mat2 = new Material(litShader);
                mat2.name = "PSX_DerelictHull_Rust";
                mat2.SetColor("_BaseColor", new Color(0.85f, 0.65f, 0.48f));
                mat2.SetFloat("_GridResolution", 200f);
                mat2.SetFloat("_TextureWarping", 1f);
                AssetDatabase.CreateAsset(mat2, matPath2);
            }

            AssetDatabase.SaveAssets();

            // 2. Assign to all MeshRenderers in Space_Debris_Cluster
            GameObject cluster = GameObject.Find("Space_Debris_Cluster");
            MeshRenderer[] renderers;
            if (cluster != null)
            {
                renderers = cluster.GetComponentsInChildren<MeshRenderer>();
            }
            else
            {
                renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            }

            int count = 0;
            Material[] sampleMats = new Material[] { mat1, mat2 };
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r.GetComponentInParent<AstronautMovement6D>() != null) continue; // Skip player

                Undo.RecordObject(r, "Apply PSX Material");
                r.sharedMaterial = sampleMats[i % sampleMats.Length];
                count++;
            }

            Debug.Log($"<color=#00FF99><b>[Space Horror]</b></color> PSX Lit Shaders (Phase 2) applied to {count} space debris objects!");
        }

        [MenuItem("Tools/Space Horror/2. PSX Retro & Graphics/Setup Central PSX Manager & Presets", false, 13)]
        public static void SetupPSXManager()
        {
            // 1. Ensure PostProcess Feature is enabled first
            EnablePSXPostProcessing();

            // 2. Find or create PSX_Manager object
            GameObject managerObj = GameObject.Find("PSX_Manager");
            if (managerObj == null)
            {
                managerObj = new GameObject("PSX_Manager");
                Undo.RegisterCreatedObjectUndo(managerObj, "Create PSX Manager");
            }

            PSXManager manager = managerObj.GetComponent<PSXManager>();
            if (manager == null)
            {
                manager = managerObj.AddComponent<PSXManager>();
            }

            manager.ApplyPreset(PSXManager.PSXPreset.FilmNoirSpaceHorror);
            Selection.activeGameObject = managerObj;

            Debug.Log("<color=#00FF99><b>[Space Horror]</b></color> PSX Manager successfully installed! Live preset & slider controls are ready in the Inspector.");
        }

        [MenuItem("Tools/Space Horror/3. Environment & Space/Fix & Rebuild Volumetric Flashlight Beam", false, 23)]
        public static void FixAndAddVolumetricBeam()
        {
            GameObject flashlightObj = GameObject.Find("HelmetFlashlight");
            if (flashlightObj == null)
            {
                Debug.LogWarning("[Space Horror] Could not find 'HelmetFlashlight' in the active scene!");
                return;
            }

            // Remove any old beam
            Transform oldBeam = flashlightObj.transform.Find("VolumetricBeamCone");
            if (oldBeam != null)
            {
                Undo.DestroyObjectImmediate(oldBeam.gameObject);
            }

            // Create new volumetric beam
            GameObject beamObj = CreateVolumetricBeamObject(flashlightObj.transform);
            Undo.RegisterCreatedObjectUndo(beamObj, "Create Volumetric Beam Cone");

            Light spotLight = flashlightObj.GetComponent<Light>();
            if (spotLight != null)
            {
                spotLight.type = LightType.Spot;
                spotLight.range = 35f;
                spotLight.spotAngle = 48f;
                spotLight.innerSpotAngle = 28f;
                spotLight.intensity = 4.5f;
                spotLight.color = new Color(0.92f, 0.95f, 1.0f);
            }

            HelmetFlashlight flashlightScript = flashlightObj.GetComponent<HelmetFlashlight>();
            Camera mainCam = Camera.main;
            Transform head = mainCam != null ? mainCam.transform : flashlightObj.transform.parent;

            if (flashlightScript != null)
            {
                flashlightScript.Initialize(head, spotLight, beamObj);
                EditorUtility.SetDirty(flashlightScript);
            }

            Debug.Log("<color=#00FF99><b>[Space Horror]</b></color> Volumetric Flashlight Beam successfully created and attached to HelmetFlashlight!");
        }

        public static GameObject CreateVolumetricBeamObject(Transform parent)
        {
            GameObject beamObj = new GameObject("VolumetricBeamCone");
            beamObj.transform.SetParent(parent, false);
            beamObj.transform.localPosition = Vector3.zero;
            beamObj.transform.localRotation = Quaternion.identity;

            MeshFilter filter = beamObj.AddComponent<MeshFilter>();
            filter.sharedMesh = CreateConeMesh(30f, 0.05f, 13f, 24);

            MeshRenderer mr = beamObj.AddComponent<MeshRenderer>();
            Shader beamShader = Shader.Find("PSX/PSX_VolumetricBeam");
            if (beamShader != null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Settings/Materials"))
                {
                    AssetDatabase.CreateFolder("Assets/Settings", "Materials");
                }

                string beamMatPath = "Assets/Settings/Materials/PSX_VolumetricBeam_Mat.mat";
                Material beamMat = AssetDatabase.LoadAssetAtPath<Material>(beamMatPath);
                if (beamMat == null)
                {
                    beamMat = new Material(beamShader);
                    beamMat.name = "PSX_VolumetricBeam_Mat";
                    beamMat.SetColor("_BeamColor", new Color(0.85f, 0.92f, 1.0f, 0.18f));
                    AssetDatabase.CreateAsset(beamMat, beamMatPath);
                }
                mr.sharedMaterial = beamMat;
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            return beamObj;
        }

        public static Mesh CreateConeMesh(float length, float startRadius, float endRadius, int segments)
        {
            Mesh mesh = new Mesh();
            mesh.name = "VolumetricConeMesh";

            System.Collections.Generic.List<Vector3> vertices = new System.Collections.Generic.List<Vector3>();
            System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>();

            float angleStep = (2f * Mathf.PI) / segments;

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                // Near vertex (lens)
                vertices.Add(new Vector3(cos * startRadius, sin * startRadius, 0.05f));
                // Far vertex (cone base)
                vertices.Add(new Vector3(cos * endRadius, sin * endRadius, length));
            }

            for (int i = 0; i < segments; i++)
            {
                int i0 = i * 2;
                int i1 = i0 + 1;
                int i2 = i0 + 2;
                int i3 = i0 + 3;

                // Two triangles per segment
                triangles.Add(i0);
                triangles.Add(i1);
                triangles.Add(i2);

                triangles.Add(i2);
                triangles.Add(i1);
                triangles.Add(i3);
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
#endif
