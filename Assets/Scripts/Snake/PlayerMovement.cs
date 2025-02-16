using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;

namespace Freehill.SnakeLand
{
    // FIXME: PlayerMovement shouldn't be on a generic head model, 
    // however the camera should have a script to attach itself to the PlayerMovement when it spawns
    // ...or PlayerMovement finds it, or Snake combines them via a pre-assigned cam ref? NO, that's wasteful
    // ...or put PlayerMovement on the camera, not the head object, then assign a follow object when it spawns via SNAKE...on the camera?

    // TODO: keep one main camera always, then spawn a snake, and constrain the camera to follow that snake head
    // TODO: allow the camera to be zoomed in/out and rotated with separate touch inputs (pinch, second finger swipe up/down)
    // TODO: add a sprint button UI (hold to sprint)
    // TODO(?): fireball UI, magnet UI
    [RequireComponent(typeof (PositionConstraint))]
    public class PlayerMovement : VelocitySource
    {
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private InputAction _touchAction;
        [SerializeField] private InputAction _dragAction;

        private PositionConstraint _cameraPositionConstraint;
        private Vector3 _velocity;
        private Vector2 _rotationCenter;
        private float _baseSpeed = 5.0f;
        private float _sprintSpeed = 7.0f;
        private bool _isSprinting = false;

        private const float MOVE_THRESHOLD = 0.1f;

        private float CurrentSpeed => _isSprinting ? _sprintSpeed : _baseSpeed;

        public override Vector3 Velocity => _velocity;

        private void Awake()
        {
            _cameraPositionConstraint = GetComponent<PositionConstraint>();
            _touchAction.performed += TouchAction;
            _dragAction.performed += MoveAction;
        }

        /// <summary>
        /// Sets the PositionConstraint on this transform to only evaluate relative to the given <paramref name="constraintSource"/>
        /// </summary>
        public void SetCameraConstraintSource(Transform constraintSource)
        {
            _cameraPositionConstraint.AddSource(new ConstraintSource { sourceTransform = constraintSource, weight = 1.0f });
        }

        private void OnEnable()
        {
            _touchAction.Enable();
            _dragAction.Enable();
        }

        private void OnDisable()
        {
            _touchAction.Disable();
            _dragAction.Disable();
        }

        private void OnDestroy()
        {
            _touchAction.performed -= TouchAction;
            _dragAction.performed -= MoveAction;
        }

        private void TouchAction(InputAction.CallbackContext context)
        {
            // primary touch start position
            _rotationCenter = context.ReadValue<Vector2>(); 
        }

        private void MoveAction(InputAction.CallbackContext context)
        {
            // primary touch current position
            Vector2 dragPosition = context.ReadValue<Vector2>();
            Vector2 screenMoveDirection = dragPosition - _rotationCenter;

            // terrain is a height map on the X-Z plane so worldMoveDirection is moved to that plane
            Vector3 worldMoveDirection = new Vector3(screenMoveDirection.x, 0.0f, screenMoveDirection.y);

            // always move relative to the camera forward along the X-Z plane
            worldMoveDirection = Vector3.ProjectOnPlane(_playerCamera.transform.rotation * worldMoveDirection, Vector3.up);

            if (worldMoveDirection.sqrMagnitude > MOVE_THRESHOLD)
            {
                _velocity = worldMoveDirection.normalized * CurrentSpeed;
            }
        }
    }
}
