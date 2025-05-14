using UnityEngine;
using UnityEngine.UIElements;

namespace Freehill.SnakeLand
{
    public abstract class VelocitySource : MonoBehaviour
    {
        [SerializeField][Min(0.01f)] private float _baseSpeed = 5.0f;
        [SerializeField][Min(0.01f)] private float _sprintSpeed = 7.0f;

        private Vector3 _currentFacing = Vector3.right;
        protected bool _isSprinting = false;
        protected bool _isStopped = true;

        // all movement is on the XZ plane
        private static Vector3 TURNING_AXIS = Vector3.up;

        public Vector3 CurrentFacing => _currentFacing;
        public abstract Vector3 TargetFacing { get; }
        public float Speed => _isSprinting ? _sprintSpeed : _baseSpeed;
        public bool IsSprinting => _isSprinting;
        public bool IsStopped => _isStopped;

        /// <summary>
        /// Rotates CurrentFacing towards TargetFacing by an angular velocty
        /// defined by the Snake's turning radius and its current speed.
        /// </summary>
        public void RotateToFaceTargetHeading(float turningRadius)
        {
            //Vector3 currentXZFacing = new Vector3(_currentFacing.x, 0.0f, _currentFacing.z);
            //Vector3 targetXZFacing = new Vector3(TargetFacing.x, 0.0f, TargetFacing.z);
            //float angle = Vector3.SignedAngle(currentXZFacing, targetXZFacing, TURNING_AXIS);
            //float angularSpeed = System.Math.Sign(angle) * (Speed / turningRadius) * Mathf.Rad2Deg;
            //Quaternion rotation = Quaternion.AngleAxis(Mathf.Clamp(angularSpeed * Time.deltaTime, angle, -angle), TURNING_AXIS);
            //currentXZFacing = rotation * currentXZFacing;
            //_currentFacing.x = currentXZFacing.x;
            //_currentFacing.z = currentXZFacing.z;
            //_currentFacing.Normalize();
            _currentFacing.x = TargetFacing.x;
            _currentFacing.z = TargetFacing.z;
            //_currentFacing.Normalize();
        }

        // TODO: alternatively, perform all jump logic in SnakeMovement wherein only the y-position changes over time
        public void ApplyGravityToFacing()
        {
            // FIXME: magic number for angular speed of facing vector
            Quaternion rotation = Quaternion.AngleAxis(-50.0f * Time.deltaTime, Vector3.Cross(_currentFacing, TURNING_AXIS));
            _currentFacing = rotation * _currentFacing;

            // FIXME: should be _isFalling, and that increments speed higher and higher?
            // ...or speed goes down to zero, then up for a human jump...but this is a crazy snake jump
            // so maybe crazy logic should apply
            _isSprinting = true;
        }

        public void Land()
        { 
            if (_currentFacing.y != 0.0f) 
            { 
                _currentFacing.y = 0.0f;
                _isSprinting = false;
            }
        }

        public void Jump()
        {
            Quaternion rotation = Quaternion.AngleAxis(45.0f, Vector3.Cross(_currentFacing, TURNING_AXIS));
            _currentFacing = rotation * _currentFacing;
        }
    }
}
