using UnityEngine;

namespace SpaceHorror.Player
{
    /// <summary>
    /// Helmet-mounted flashlight designed for deep space horror.
    /// Features inertia sway (drags realistically behind fast head turns),
    /// toggle functionality (F key), horror micro-flicker, and impact disruption.
    /// </summary>
    public class HelmetFlashlight : MonoBehaviour
    {
        [Header("Light References")]
        [Tooltip("The main Spotlight component representing the helmet beam")]
        [SerializeField] private Light flashlightLight;

        [Tooltip("Optional faint secondary fill light (broad visor illumination)")]
        [SerializeField] private Light fillLight;

        [Tooltip("Visual volumetric beam cone cutting through dark space")]
        [SerializeField] private GameObject volumetricBeam;

        [Header("Inertia & Lag")]
        [Tooltip("Transform that the light follows (usually Main Camera)")]
        [SerializeField] private Transform targetHead;

        [Tooltip("How quickly the beam catches up to the head rotation (lower = more eerie drag)")]
        [SerializeField] private float beamFollowSpeed = 16f;

        [Tooltip("Max angle the light can drift away from center view")]
        [SerializeField] private float maxSwayAngle = 8f;

        [Header("Horror Atmosphere & Flickering")]
        [Tooltip("Enable subtle atmospheric flicker")]
        [SerializeField] private bool enableHorrorFlicker = true;

        [Tooltip("Base light intensity")]
        [SerializeField] private float baseIntensity = 3.5f;

        [Tooltip("Chance per second of a micro-flicker occurring")]
        [SerializeField] private float flickerFrequency = 0.15f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip toggleOnSound;
        [SerializeField] private AudioClip toggleOffSound;
        [SerializeField] private AudioClip flickerBuzzSound;

        // Runtime states
        private bool isLightOn = true;
        private float flickerTimer = 0f;
        private bool isFlickering = false;
        private AstronautMovement6D movementController;
        private AstronautInputHandler inputHandler;

        public bool IsLightOn => isLightOn;
        public event System.Action<bool> OnFlashlightToggled;

        public void Initialize(Transform head, Light lightComp, GameObject beam = null)
        {
            targetHead = head;
            flashlightLight = lightComp;
            volumetricBeam = beam;
        }

        private void Awake()
        {
            if (flashlightLight == null)
            {
                flashlightLight = GetComponentInChildren<Light>();
            }

            movementController = GetComponentInParent<AstronautMovement6D>();
            inputHandler = GetComponentInParent<AstronautInputHandler>();

            if (targetHead == null && Camera.main != null)
            {
                targetHead = Camera.main.transform;
            }
        }

        private void Start()
        {
            SetLightActive(isLightOn);
        }

        private void OnEnable()
        {
            if (movementController != null)
            {
                movementController.OnSuitImpact += HandleSuitImpact;
            }
        }

        private void OnDisable()
        {
            if (movementController != null)
            {
                movementController.OnSuitImpact -= HandleSuitImpact;
            }
        }

        private void Update()
        {
            HandleInput();
            UpdateBeamSway();
            UpdateFlicker();
        }

        private void HandleInput()
        {
            if (inputHandler != null && inputHandler.ToggleFlashlightPressed)
            {
                ToggleLight();
            }
        }

        private void UpdateBeamSway()
        {
            if (targetHead == null) return;

            // Lag behind the target head rotation
            Quaternion targetRot = targetHead.rotation;
            float angleDiff = Quaternion.Angle(transform.rotation, targetRot);

            if (angleDiff > maxSwayAngle)
            {
                // Clamp max lag so the light never falls too far off screen
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, angleDiff - maxSwayAngle);
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * beamFollowSpeed);
            transform.position = targetHead.position;
        }

        private void UpdateFlicker()
        {
            if (!isLightOn || !enableHorrorFlicker || flashlightLight == null) return;

            if (isFlickering)
            {
                flickerTimer -= Time.deltaTime;
                float flickerMultiplier = Random.Range(0.15f, 0.85f);
                flashlightLight.intensity = baseIntensity * flickerMultiplier;
                if (volumetricBeam != null) volumetricBeam.SetActive(flickerMultiplier > 0.35f);

                if (flickerTimer <= 0f)
                {
                    isFlickering = false;
                    flashlightLight.intensity = baseIntensity;
                    if (volumetricBeam != null) volumetricBeam.SetActive(true);
                }
            }
            else
            {
                // Randomly trigger subtle micro-flicker
                if (Random.value < flickerFrequency * Time.deltaTime)
                {
                    isFlickering = true;
                    flickerTimer = Random.Range(0.05f, 0.25f);
                    PlaySound(flickerBuzzSound, 0.3f);
                }
            }
        }

        private void HandleSuitImpact(float magnitude, Vector3 contactPoint)
        {
            // Violent bump temporarily disrupts flashlight
            if (isLightOn && magnitude > 3f)
            {
                isFlickering = true;
                flickerTimer = Random.Range(0.2f, 0.6f);
                PlaySound(flickerBuzzSound, 0.6f);
            }
        }

        public void ToggleLight()
        {
            isLightOn = !isLightOn;
            SetLightActive(isLightOn);
            PlaySound(isLightOn ? toggleOnSound : toggleOffSound, 0.7f);
        }

        private void SetLightActive(bool active)
        {
            if (flashlightLight != null)
            {
                flashlightLight.enabled = active;
                flashlightLight.intensity = baseIntensity;
            }

            if (fillLight != null)
            {
                fillLight.enabled = active;
            }

            if (volumetricBeam != null)
            {
                volumetricBeam.SetActive(active);
            }

            OnFlashlightToggled?.Invoke(active);
        }

        private void PlaySound(AudioClip clip, float volume = 1f)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }
    }
}
