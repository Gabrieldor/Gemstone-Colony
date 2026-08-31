using UnityEngine;
using UnityEngine.InputSystem;

namespace GemstoneColony.Player
{
    /// <summary>
    /// Free-fly debug camera: WASD relative movement, mouse look while the right mouse
    /// button is held (matches Scene View navigation), Q/E for vertical, Shift to move
    /// faster. No physics/collision -- this is a dev/debug camera, not gameplay movement.
    /// Polls the new Input System directly (Keyboard.current / Mouse.current) since this
    /// project's Active Input Handling is set to "Input System Package (New)" only.
    /// </summary>
    public class FlyCamera : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float fastMoveMultiplier = 3f;
        [SerializeField] private float lookSensitivity = 0.15f;

        private float yaw;
        private float pitch;

        private void OnEnable()
        {
            Vector3 euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null)
                return;

            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * lookSensitivity;
                pitch -= delta.y * lookSensitivity;
                pitch = Mathf.Clamp(pitch, -89f, 89f);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            Vector3 move = Vector3.zero;
            if (keyboard.wKey.isPressed) move += transform.forward;
            if (keyboard.sKey.isPressed) move -= transform.forward;
            if (keyboard.dKey.isPressed) move += transform.right;
            if (keyboard.aKey.isPressed) move -= transform.right;
            if (keyboard.eKey.isPressed || keyboard.spaceKey.isPressed) move += transform.up;
            if (keyboard.qKey.isPressed || keyboard.leftCtrlKey.isPressed) move -= transform.up;

            float speed = moveSpeed;
            if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
                speed *= fastMoveMultiplier;

            transform.position += move.normalized * (speed * Time.deltaTime);
        }
    }
}
