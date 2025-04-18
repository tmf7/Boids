using UnityEngine;

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
        /// Rotates CurrentFacing towards TargetFacing by an angular velocty defined by the Snake's turning radius and its current speed.
        /// </summary>
        public void RotateToFaceTargetHeading(float turningRadius)
        {
            float angle = Vector3.SignedAngle(CurrentFacing, TargetFacing, TURNING_AXIS);
            float angularSpeed = System.Math.Sign(angle) * (Speed / turningRadius) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.AngleAxis(Mathf.Clamp(angularSpeed * Time.deltaTime, angle, -angle), TURNING_AXIS);
            _currentFacing = rotation * _currentFacing;
        }
    }
}
