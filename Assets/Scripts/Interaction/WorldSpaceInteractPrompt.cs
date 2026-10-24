using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SpaceHorror.Interaction
{
    /// <summary>
    /// Diegetic World-Space Floating Interaction Prompt.
    /// Floats directly above/beside the active interactable object in 3D world space.
    /// Completely boxless, minimalist, and atmospheric (Dead Space / Alien Isolation style).
    /// </summary>
    public class WorldSpaceInteractPrompt : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Canvas worldCanvas;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI actionPromptText;
        [SerializeField] private TextMeshProUGUI objectiveTitleText;
        [SerializeField] private RectTransform progressBarRoot;
        [SerializeField] private Image progressBarFill;

        [Header("Animation & Behavior")]
        [SerializeField] private float positionSmoothSpeed = 16f;
        [SerializeField] private float fadeSpeed = 8f;
        [SerializeField] private Vector3 baseScale = new Vector3(0.0035f, 0.0035f, 0.0035f);
        [SerializeField] private LayerMask occlusionLayers = ~0;

        private Camera playerCamera;
        private Vector3 targetWorldPos;
        private Transform currentTargetTransform;
        private bool isVisible = false;

        private void Awake()
        {
            if (worldCanvas == null)
                worldCanvas = GetComponent<Canvas>();

            if (worldCanvas != null)
                worldCanvas.sortingOrder = 500;

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (playerCamera == null)
                playerCamera = Camera.main;

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            transform.localScale = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
                if (playerCamera == null) return;
            }

            // Billboard towards player camera
            transform.rotation = playerCamera.transform.rotation;

            // Smooth position tracking
            if (isVisible)
            {
                transform.position = Vector3.Lerp(transform.position, targetWorldPos, Time.deltaTime * positionSmoothSpeed);
            }

            // Always On Top: pure visibility fade, never blocked by objects
            float targetAlpha = isVisible ? 1f : 0f;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
            }

            // Subtle scale pop in / out
            Vector3 desiredScale = isVisible ? baseScale : Vector3.zero;
            transform.localScale = Vector3.Lerp(transform.localScale, desiredScale, Time.deltaTime * (fadeSpeed * 1.5f));
        }

        /// <summary>
        /// Shows the prompt floating at the specified world position with target tracking for occlusion.
        /// </summary>
        public void Show(Vector3 worldPosition, Transform targetTransform, string key, string actionPrompt, string objectiveTitle, float progress, bool isHold)
        {
            currentTargetTransform = targetTransform;
            targetWorldPos = worldPosition;

            if (!isVisible)
            {
                // Snap directly on first frame so it doesn't fly in from afar
                transform.position = worldPosition;
            }

            isVisible = true;

            if (actionPromptText != null)
            {
                string holdSuffix = isHold ? " (HOLD)" : "";
                actionPromptText.text = $"<color=#38BDF8>[{key}]</color> {actionPrompt}{holdSuffix}";
            }

            if (objectiveTitleText != null)
            {
                objectiveTitleText.text = objectiveTitle.ToUpperInvariant();
                objectiveTitleText.gameObject.SetActive(!string.IsNullOrEmpty(objectiveTitle));
            }

            if (progressBarRoot != null)
            {
                // Only show progress bar when holding or starting to hold
                bool showBar = isHold && (progress > 0.001f);
                progressBarRoot.gameObject.SetActive(showBar);

                if (progressBarFill != null)
                {
                    progressBarFill.fillAmount = progress;
                }
            }
        }

        public void Show(Vector3 worldPosition, string key, string actionPrompt, string objectiveTitle, float progress, bool isHold)
        {
            Show(worldPosition, null, key, actionPrompt, objectiveTitle, progress, isHold);
        }

        /// <summary>
        /// Hides the floating prompt smoothly.
        /// </summary>
        public void Hide()
        {
            isVisible = false;
        }

        /// <summary>
        /// Factory helper to build the world space prompt object with procedural UI elements.
        /// </summary>
        public static WorldSpaceInteractPrompt CreateDefault(Transform parent = null)
        {
            GameObject promptObj = new GameObject("WorldSpace_InteractPrompt");
            if (parent != null)
                promptObj.transform.SetParent(parent, false);

            Canvas canvas = promptObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = promptObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            CanvasGroup group = promptObj.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            RectTransform canvasRect = promptObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(300f, 100f);
            canvasRect.localScale = new Vector3(0.0035f, 0.0035f, 0.0035f);

            // Container for text & progress
            GameObject container = new GameObject("Container");
            container.transform.SetParent(promptObj.transform, false);
            RectTransform contRect = container.AddComponent<RectTransform>();
            contRect.anchorMin = new Vector2(0.5f, 0.5f);
            contRect.anchorMax = new Vector2(0.5f, 0.5f);
            contRect.pivot = new Vector2(0.5f, 0.5f);
            contRect.sizeDelta = new Vector2(300f, 80f);

            // Primary action prompt text
            GameObject promptTextObj = new GameObject("ActionPrompt_Text");
            promptTextObj.transform.SetParent(container.transform, false);
            RectTransform promptRect = promptTextObj.AddComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0.5f);
            promptRect.anchorMax = new Vector2(0.5f, 0.5f);
            promptRect.pivot = new Vector2(0.5f, 0.5f);
            promptRect.anchoredPosition = new Vector2(0f, 8f);
            promptRect.sizeDelta = new Vector2(300f, 32f);

            TextMeshProUGUI promptTmp = promptTextObj.AddComponent<TextMeshProUGUI>();
            promptTmp.text = "[LMB] REFILL OXYGEN";
            promptTmp.fontSize = 20f;
            promptTmp.alignment = TextAlignmentOptions.Center;
            promptTmp.color = new Color(0.92f, 0.96f, 1f, 1f);
            promptTmp.fontStyle = FontStyles.Bold;
            promptTmp.outlineWidth = 0.22f;
            promptTmp.outlineColor = new Color32(5, 8, 14, 230);
            promptTmp.textWrappingMode = TextWrappingModes.NoWrap;
            promptTmp.raycastTarget = false;

            // Subtitle objective text
            GameObject subTextObj = new GameObject("ObjectiveTitle_Text");
            subTextObj.transform.SetParent(container.transform, false);
            RectTransform subRect = subTextObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.5f);
            subRect.anchorMax = new Vector2(0.5f, 0.5f);
            subRect.pivot = new Vector2(0.5f, 0.5f);
            subRect.anchoredPosition = new Vector2(0f, -14f);
            subRect.sizeDelta = new Vector2(300f, 20f);

            TextMeshProUGUI subTmp = subTextObj.AddComponent<TextMeshProUGUI>();
            subTmp.text = "EMERGENCY O2 STATION";
            subTmp.fontSize = 12f;
            subTmp.alignment = TextAlignmentOptions.Center;
            subTmp.color = new Color(0.60f, 0.68f, 0.78f, 0.85f);
            subTmp.outlineWidth = 0.18f;
            subTmp.outlineColor = new Color32(5, 8, 14, 200);
            subTmp.textWrappingMode = TextWrappingModes.NoWrap;
            subTmp.raycastTarget = false;

            // Sleek hairline progress bar
            GameObject barBGObj = new GameObject("ProgressBar_BG");
            barBGObj.transform.SetParent(container.transform, false);
            RectTransform barBGRect = barBGObj.AddComponent<RectTransform>();
            barBGRect.anchorMin = new Vector2(0.5f, 0.5f);
            barBGRect.anchorMax = new Vector2(0.5f, 0.5f);
            barBGRect.pivot = new Vector2(0.5f, 0.5f);
            barBGRect.anchoredPosition = new Vector2(0f, -28f);
            barBGRect.sizeDelta = new Vector2(160f, 3f);

            Image barBGImage = barBGObj.AddComponent<Image>();
            barBGImage.color = new Color(0.10f, 0.15f, 0.22f, 0.60f);
            barBGImage.raycastTarget = false;

            GameObject barFillObj = new GameObject("ProgressBar_Fill");
            barFillObj.transform.SetParent(barBGObj.transform, false);
            RectTransform barFillRect = barFillObj.AddComponent<RectTransform>();
            barFillRect.anchorMin = Vector2.zero;
            barFillRect.anchorMax = Vector2.one;
            barFillRect.sizeDelta = Vector2.zero;

            Image barFillImage = barFillObj.AddComponent<Image>();
            barFillImage.color = new Color(0.35f, 0.85f, 1.0f, 0.95f);
            barFillImage.type = Image.Type.Filled;
            barFillImage.fillMethod = Image.FillMethod.Horizontal;
            barFillImage.fillOrigin = 0;
            barFillImage.fillAmount = 0f;
            barFillImage.raycastTarget = false;

            barBGObj.SetActive(false);

            // Always On Top: Ensure text and progress bar are NEVER occluded by any 3D geometry
            if (promptTmp.fontSharedMaterial != null)
            {
                Material overlayFontMat = new Material(promptTmp.fontSharedMaterial);
                overlayFontMat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
                promptTmp.fontMaterial = overlayFontMat;
            }

            if (subTmp.fontSharedMaterial != null)
            {
                Material overlaySubMat = new Material(subTmp.fontSharedMaterial);
                overlaySubMat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
                subTmp.fontMaterial = overlaySubMat;
            }

            Shader uiShader = Shader.Find("UI/Default");
            if (uiShader != null)
            {
                Material overlayUIMat = new Material(uiShader);
                overlayUIMat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
                barBGImage.material = overlayUIMat;
                barFillImage.material = overlayUIMat;
            }

            WorldSpaceInteractPrompt promptComp = promptObj.AddComponent<WorldSpaceInteractPrompt>();
            promptComp.worldCanvas = canvas;
            promptComp.canvasGroup = group;
            promptComp.actionPromptText = promptTmp;
            promptComp.objectiveTitleText = subTmp;
            promptComp.progressBarRoot = barBGRect;
            promptComp.progressBarFill = barFillImage;

            return promptComp;
        }
    }
}
