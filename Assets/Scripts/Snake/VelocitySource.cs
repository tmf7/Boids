using UnityEngine;

namespace Freehill.SnakeLand
{
    public abstract class VelocitySource : MonoBehaviour
    {
        [SerializeField][Min(0.01f)] private float _baseSpeed = 5.0f;
        [SerializeField][Min(0.01f)] private float _sprintSpeed = 7.0f;
        [SerializeField][Min(0.0f)] private float _jumpSpeed = 20.0f;

        private Vector3 _currentFacing = Vector3.right;
        private Vector3 _fallingVelocity = Vector3.zero;
        protected bool _isGrounded = true;
        protected bool _isSprinting = false;
        protected bool _isStopped = true;

        // all movement is on the XZ plane
        protected static Vector3 TURNING_AXIS = Vector3.up;

        public Vector3 FallingVelocity => _fallingVelocity;
        public Vector3 CurrentFacing => _currentFacing;
        public abstract Vector3 TargetFacing { get; }
        public float GroundSpeed => _isSprinting ? _sprintSpeed : _baseSpeed;
        public bool IsGrounded => _isGrounded;
        public bool IsSprinting => _isSprinting;
        public bool IsStopped => _isStopped;

        public abstract void Init(SnakesManager snakesManager, Snake ownerSnake);

        /// <summary>
        /// Rotates CurrentFacing towards TargetFacing by an angular velocty
        /// defined by the Snake's turning radius and its current speed.
        /// </summary>
        public void RotateToFaceTargetHeading(float turningRadius)
        {
            _currentFacing = Vector3.RotateTowards(_currentFacing, TargetFacing, (GroundSpeed / turningRadius) * Time.deltaTime, 0.0f);
        }

        public void StartFall()
        {
            _isGrounded = false;
        }

        public void UpdateFall()
        {
            const float GRAVITY_FEEL_MULTIPLIER = 5.0f;
            _fallingVelocity += GRAVITY_FEEL_MULTIPLIER * Physics.gravity * Time.deltaTime;
        }

        public void Land()
        { 
            _currentFacing.y = 0.0f;
            _fallingVelocity = Vector3.zero;
            _isGrounded = true;
        }

        // TODO: get rid of pure Jump for snake soccer and snake combat
        public void Jump()
        {
            // FIXME(~): fall grace magic number
            // FIXME(~): adding to velocity doesn't guarantee a jump height
            if (_isGrounded || _fallingVelocity.magnitude < 5.0f) 
            { 
                _fallingVelocity += _jumpSpeed * Vector3.up;
            }
        }
    }
}
