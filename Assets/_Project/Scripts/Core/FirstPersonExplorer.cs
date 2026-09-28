using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastMachinist.Core
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonExplorer : MonoBehaviour
    {
        private CharacterController body;
        private Transform eye;
        private ExplorationRegionDefinition region;
        private InputActionMap inputs;
        private InputAction move, look, interact, escape;
        private float pitch, verticalSpeed;
        public float Pitch => pitch;
        public event Action InteractRequested;
        public event Action InspectionCloseRequested;
        public bool InspectionOpen { get; private set; }
        private int suppressInteractionThroughFrame = -1;
        public bool CanInteract => isActiveAndEnabled && !MenuOpen && !InspectionOpen && Time.frameCount > suppressInteractionThroughFrame;
        public bool MenuOpen { get; private set; }
        public void Initialize(ExplorationRegionDefinition definition, Transform cameraTransform)
        {
            region = definition; eye = cameraTransform; body = GetComponent<CharacterController>();
            body.height = 1.8f; body.radius = 0.3f; body.center = new Vector3(0, 0.9f, 0);
            body.stepOffset = 0.3f; body.skinWidth = 0.04f;
            inputs = new InputActionMap("Exploration");
            move = inputs.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            look = inputs.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            interact = inputs.AddAction("Interact", InputActionType.Button, "<Mouse>/leftButton");
            escape = inputs.AddAction("Menu", InputActionType.Button, "<Keyboard>/escape");
            inputs.Enable(); SetMenu(false);
        }
        public void SetMenu(bool open)
        {
            MenuOpen = open;
            RefreshCursor();
            if (!open) suppressInteractionThroughFrame = Time.frameCount;
        }
        public void SetInspectionOpen(bool open)
        {
            InspectionOpen = open;
            RefreshCursor();
            if (!open) suppressInteractionThroughFrame = Time.frameCount;
        }
        private void RefreshCursor()
        {
            bool free = MenuOpen || InspectionOpen;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }
        private void Update()
        {
            if (inputs == null) return;
            if (escape.WasPressedThisFrame())
            {
                if (InspectionOpen) InspectionCloseRequested?.Invoke();
                else SetMenu(!MenuOpen);
            }
            // Batch tests have no focused Game window and cannot retain cursor lock.
            if (!Application.isBatchMode && Cursor.lockState != CursorLockMode.Locked && !MenuOpen && !InspectionOpen) SetMenu(true);
            if (!MenuOpen && !InspectionOpen)
            {
                ApplyLook(look.ReadValue<Vector2>());
                if (interact.WasPressedThisFrame()) RequestInteract();
            }
            MoveStep(MenuOpen ? Vector2.zero : move.ReadValue<Vector2>(), Time.deltaTime);
        }
        public void ApplyLook(Vector2 delta)
        {
            if (InspectionOpen) return;
            transform.Rotate(0, delta.x * region.MouseSensitivity, 0);
            pitch = Mathf.Clamp(pitch - delta.y * region.MouseSensitivity, -85, 85);
            eye.localRotation = Quaternion.Euler(pitch, 0, 0);
        }
        public void MoveStep(Vector2 direction, float seconds)
        {
            if (InspectionOpen) return;
            direction = Vector2.ClampMagnitude(direction, 1);
            if (body.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed = Mathf.Max(-40, verticalSpeed - 9.81f * seconds);
            Vector3 planar = (transform.right * direction.x + transform.forward * direction.y) * region.MoveSpeed;
            var collision = body.Move((planar + Vector3.up * verticalSpeed) * seconds);
            if ((collision & CollisionFlags.Above) != 0 && verticalSpeed > 0) verticalSpeed = 0;
        }
        // UI input cannot fall through into world interaction, including its closing frame.
        public void RequestInteract() { if (CanInteract) InteractRequested?.Invoke(); }
        private void OnApplicationFocus(bool focused) { if (!Application.isBatchMode && !focused && inputs != null) SetMenu(true); }
        private void OnDisable() { inputs?.Disable(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        private void OnDestroy() { inputs?.Dispose(); }
    }
}
