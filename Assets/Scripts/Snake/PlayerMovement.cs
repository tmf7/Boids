using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Freehill.SnakeLand
{
    // TODO: add a sprint button UI (hold to sprint)
    // TODO: fireball UI, magnet UI, immune UI, and timeouts/etc
    [RequireComponent(typeof(PositionConstraint))]
    public class PlayerMovement : VelocitySource
    {
        [SerializeField] private Camera _playerCamera;
        [SerializeField][Min(0.01f)] private float _baseSpeed = 5.0f;
        [SerializeField][Min(0.01f)] private float _sprintSpeed = 7.0f;
        
        // CAMERA
        private PositionConstraint _cameraPositionConstraint;
        private Vector3 _maxCameraPosition;
        private float _cameraZoom;

        // BODY MOVEMENT
        private Vector3 _facing;
        private bool _isSprinting = false;

        private const float MOVE_THRESHOLD = 2500.0f; // square root is 50 pixels
        private const float CAMERA_ANGULAR_SPEED_DEGREES = 90.0f;
        private const float CAMERA_ROTATION_THRESHOLD = 0.1f;
        private const float CAMERA_MAX_ZOOM = 0.8f;

        public override float Speed => _isSprinting ? _sprintSpeed : _baseSpeed;
        public override Vector3 Facing => _facing;

        /// <summary>
        /// Sets the PositionConstraint on this transform to only evaluate relative to the given <paramref name="constraintSource"/>
        /// </summary>
        public void SetCameraConstraintSource(Transform constraintSource)
        {
            _cameraPositionConstraint.AddSource(new ConstraintSource { sourceTransform = constraintSource, weight = 1.0f });
        }

        private void Awake()
        {
            EnhancedTouchSupport.Enable(); // must be manually enabled

            _cameraPositionConstraint = GetComponent<PositionConstraint>();
            _maxCameraPosition = _playerCamera.transform.localPosition;
            _cameraZoom = 0.0f;
        }

        private void Update()
        {
            if (!EnhancedTouchSupport.enabled)
            {
                EnhancedTouchSupport.Enable();
            }

            if (Touch.activeTouches.Count == 1)
            {
                Move(Touch.activeTouches[0]);
            }
            else if (Touch.activeTouches.Count == 2)
            {
                ZoomCamera(Touch.activeTouches[0], Touch.activeTouches[1]);
                RotateCamera(Touch.activeTouches[0], Touch.activeTouches[1]);
            }
        }

        private void RotateCamera(Touch firstTouch, Touch secondTouch)
        {
            Vector2 initialPairOffset = firstTouch.screenPosition - secondTouch.screenPosition;
            Vector2 finalPairOffset = (firstTouch.screenPosition - firstTouch.delta) - (secondTouch.screenPosition - secondTouch.delta);
            float angle = Vector2.SignedAngle(initialPairOffset, finalPairOffset);

            if (Mathf.Abs(angle) > CAMERA_ROTATION_THRESHOLD)
            {
                angle = Mathf.Sign(angle) * CAMERA_ANGULAR_SPEED_DEGREES * Time.deltaTime;
                transform.rotation *= Quaternion.AngleAxis(angle, transform.up);
            }
        }

        private void ZoomCamera(Touch firstTouch, Touch secondTouch)
        {
            float initialPinchOffset = ((firstTouch.screenPosition - firstTouch.delta) - (secondTouch.screenPosition - secondTouch.delta)).sqrMagnitude;
            float currentPinchOffset = (firstTouch.screenPosition - secondTouch.screenPosition).sqrMagnitude;

            // zoomDelta > -CAMERA_ZOOM_SPEED, without limit but generally [-CAMERA_ZOOM_SPEED, CAMERA_ZOOM_SPEED]
            float zoomDelta = ((currentPinchOffset / initialPinchOffset) - 1.0f);

            _cameraZoom = Mathf.Clamp(_cameraZoom + zoomDelta, 0.0f, CAMERA_MAX_ZOOM);
            _playerCamera.transform.localPosition = Vector3.Lerp(_maxCameraPosition, Vector3.zero, _cameraZoom);
        }

        private void Move(Touch touch)
        {
            Vector2 screenMoveDirection = touch.screenPosition - touch.startScreenPosition;

            // terrain is a height map on the X-Z plane so worldMoveDirection is moved to that plane
            Vector3 worldMoveDirection = new Vector3(screenMoveDirection.x, 0.0f, screenMoveDirection.y);

            // always move relative to the camera forward along the X-Z plane
            worldMoveDirection = Vector3.ProjectOnPlane(_playerCamera.transform.rotation * worldMoveDirection, Vector3.up);
            float moveAmountSqr = worldMoveDirection.sqrMagnitude;

            if (moveAmountSqr >= MOVE_THRESHOLD)
            {
                _facing = worldMoveDirection / Mathf.Sqrt(moveAmountSqr);
            }
        }
    }
}