using BulletFury;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BulletFury.Samples
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class DemoPlayerController : MonoBehaviour
    {
        [SerializeField] private BulletSpawner spawner;
        [SerializeField, Min(0f)] private float moveSpeed = 6f;

        private Rigidbody2D _rigidbody2D;
        private Vector2 _moveInput;

#if ENABLE_INPUT_SYSTEM
        private InputAction _moveAction;
        private InputAction _fireAction;
#endif

        private void Awake()
        {
            _rigidbody2D = GetComponent<Rigidbody2D>();
            _rigidbody2D.gravityScale = 0f;
            _rigidbody2D.freezeRotation = true;
        }

        private void Start()
        {
            spawner?.Stop();
        }

        private void FixedUpdate()
        {
            var delta = _moveInput * (moveSpeed * Time.fixedDeltaTime);
            _rigidbody2D.MovePosition(_rigidbody2D.position + delta);
        }

#if ENABLE_INPUT_SYSTEM
        private void OnEnable()
        {
            if (_moveAction == null)
            {
                _moveAction = new InputAction("Move", expectedControlType: "Vector2");
                _moveAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/upArrow")
                    .With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow")
                    .With("Right", "<Keyboard>/rightArrow");
                _moveAction.performed += OnMovePerformed;
                _moveAction.canceled += OnMoveCanceled;

                _fireAction = new InputAction("Fire", InputActionType.Button, "<Keyboard>/x");
                _fireAction.started += OnFireStarted;
                _fireAction.canceled += OnFireCanceled;
            }

            _moveAction.Enable();
            _fireAction.Enable();
        }

        private void OnDisable()
        {
            _moveAction?.Disable();
            _fireAction?.Disable();
            _moveInput = Vector2.zero;
            spawner?.Stop();
        }

        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            _moveInput = context.ReadValue<Vector2>();
        }

        private void OnMoveCanceled(InputAction.CallbackContext _)
        {
            _moveInput = Vector2.zero;
        }

        private void OnFireStarted(InputAction.CallbackContext _)
        {
            spawner?.Play();
        }

        private void OnFireCanceled(InputAction.CallbackContext _)
        {
            spawner?.Stop();
        }
#else
        private void OnEnable()
        {
            Debug.LogWarning("DemoPlayerController requires the Input System package.", this);
        }
#endif
    }
}
