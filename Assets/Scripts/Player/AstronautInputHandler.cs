using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceHorror.Player
{
    /// <summary>
    /// Handles player inputs for 6DoF zero-gravity astronaut movement using Unity's New Input System.
    /// Provides immediate plug-and-play support via Keyboard/Mouse with zero setup required,
    /// while exposing clean properties for AstronautMovement6D and Camera systems.
    /// </summary>
    public class AstronautInputHandler : MonoBehaviour
    {
        [Header("Mouse Settings")]
        [Tooltip("Horizontal look sensitivity (Yaw)")]
        [SerializeField] private float mouseSensitivityX = 1.8f;

        [Tooltip("Vertical look sensitivity (Pitch)")]
        [SerializeField] private float mouseSensitivityY = 1.8f;

        [Tooltip("Invert vertical look axis")]
        [SerializeField] private bool invertPitch = false;

        [Tooltip("Lock and hide cursor when game starts")]
        [SerializeField] private bool autoLockCursor = true;

        [Header("Roll Settings")]
        [Tooltip("Roll speed multiplier for Q/E keys")]
        [SerializeField] private float rollInputSensitivity = 1.0f;

        // Current Input States
        public Vector3 MovementInput { get; private set; }   // X = Strafe (A/D), Y = Vertical (Space/Ctrl), Z = Forward (W/S)
        public Vector3 RotationInput { get; private set; }   // X = Pitch (Mouse Y), Y = Yaw (Mouse X), Z = Roll
        public float RollInput => RotationInput.z;
        public bool IsBoosting { get; private set; }         // Left Shift
        public bool IsBraking { get; private set; }          // X (Emergency RCS Brake)
        public bool ToggleStabilizerPressed { get; private set; } // T (Toggle automatic counter-thrust)
        public bool ToggleFlashlightPressed { get; private set; } // F (Flashlight)
        public bool InteractPressed { get; private set; }    // E (Tap interaction this frame)
        public bool IsInteracting { get; private set; }      // E (Held interaction)
        public bool SuppressRollRight { get; set; }          // True when looking at an interactable objective
        public bool InputEnabled { get; set; } = true;        // Can be toggled for cinematic waking transitions

        private void Start()
        {
            if (autoLockCursor)
            {
                SetCursorLock(true);
            }
        }

        private void Update()
        {
            HandleCursorToggle();
            ReadInputs();
        }

        private void ReadInputs()
        {
            if (!InputEnabled)
            {
                MovementInput = Vector3.zero;
                RotationInput = Vector3.zero;
                IsBoosting = false;
                IsBraking = false;
                return;
            }

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard == null || mouse == null)
            {
                // Fallback if devices aren't detected in frame
                return;
            }

            // 1. Translation: WASD + Space (Up) + Left Ctrl (Down)
            float strafe = 0f;
            if (keyboard.dKey.isPressed) strafe += 1f;
            if (keyboard.aKey.isPressed) strafe -= 1f;

            float vertical = 0f;
            if (keyboard.spaceKey.isPressed) vertical += 1f;
            if (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) vertical -= 1f;

            float forward = 0f;
            if (keyboard.wKey.isPressed) forward += 1f;
            if (keyboard.sKey.isPressed) forward -= 1f;

            MovementInput = new Vector3(strafe, vertical, forward).normalized;

            // 2. Rotation: Mouse (Pitch & Yaw) + Roll (Q = Left, E = Right)
            Vector2 mouseDelta = mouse.delta.ReadValue() * 0.05f;
            float pitch = mouseDelta.y * mouseSensitivityY * (invertPitch ? 1f : -1f);
            float yaw = mouseDelta.x * mouseSensitivityX;

            float roll = 0f;
            if (keyboard.qKey.isPressed) roll += 1f * rollInputSensitivity;
            if (keyboard.eKey.isPressed || keyboard.cKey.isPressed) roll -= 1f * rollInputSensitivity;

            RotationInput = new Vector3(pitch, yaw, roll);

            // 3. Modifiers & Actions
            IsBoosting = keyboard.leftShiftKey.isPressed;
            IsBraking = keyboard.xKey.isPressed;

            ToggleStabilizerPressed = keyboard.tKey.wasPressedThisFrame;
            ToggleFlashlightPressed = keyboard.fKey.wasPressedThisFrame;

            // Interact: Left Mouse Button (when cursor is locked)
            bool isCursorLocked = Cursor.lockState == CursorLockMode.Locked;
            InteractPressed = isCursorLocked && mouse.leftButton.wasPressedThisFrame;
            IsInteracting = isCursorLocked && mouse.leftButton.isPressed;
        }

        private void HandleCursorToggle()
        {
            // Toggle cursor lock with Escape or Alt
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetCursorLock(Cursor.lockState != CursorLockMode.Locked);
            }

            // Re-lock on mouse click inside window
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                SetCursorLock(true);
            }
        }

        public void SetCursorLock(bool locked)
        {
            if (locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
