using UnityEngine;
using UnityEngine.InputSystem;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class TopDownPlayerController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 6f;
        private Rigidbody2D body;
        private Vector2 moveInput;

        public float MoveSpeed => moveSpeed;
        public Vector2 MoveInput => moveInput;

        private void Awake() => body = GetComponent<Rigidbody2D>();

        private void Update()
        {
            Vector2 input = Vector2.zero;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                input.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                input.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
            }
            Gamepad gamepad = Gamepad.current;
            if (input.sqrMagnitude <= 0f && gamepad != null) input = gamepad.leftStick.ReadValue();
            moveInput = input.sqrMagnitude > 1f ? input.normalized : input;
        }

        private void FixedUpdate()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            body.linearVelocity = moveInput * moveSpeed;
        }

        private void OnDisable()
        {
            moveInput = Vector2.zero;
            if (body != null) body.linearVelocity = Vector2.zero;
        }
    }
}
