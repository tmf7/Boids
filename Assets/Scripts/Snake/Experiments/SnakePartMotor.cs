using System.Collections;
using UnityEngine;

namespace Freehill.SnakeLand
{
    public class SnakePartMotor : MonoBehaviour
    {
        [Header("Head-Only Logic")]
        [SerializeField] private bool _isHead = false;
        [SerializeField] private int _numParts = 50;
        [SerializeField] private SnakePartMotor _snakePartMotorPrefab;

        [SerializeField] private Transform _dependent;
        [SerializeField][Min(0.0f)] private float _linkLength = 1.0f;

        [Header("Joint Rotation Constraint")]
        [Tooltip("Higher is more stiff, lower is more loose.")]
        [SerializeField][Min(0.0f)] private float _jointStiffness = 10.0f;
        [Tooltip("Circular cone with apex at this transform and base at the dependent transform, " +
                 "which represents this joint's mobility. This is half the full cone angle.")]
        [SerializeField][Range(0.0f, 89.0f)] private float _maxJointAngle = 30.0f; // DEBUG: smaller will cause joint to whip


        private Vector3 _lastKnownPosition;
        private Vector3 _currentHeading = Vector3.forward;
        private float _currentSerpentineRadian = 0.0f;
        private const float _maxSerpentineAngle = 45.0f;
        private const float _speed = 5.0f;
        private const float _angularSpeedRads = 1.5f;
        private const float _maxHeadingTime = 8.0f;

        private void Awake()
        {
            if (_isHead)
            {
                SnakePartMotor leader = this;

                for (int i = 0; i < _numParts; i++) 
                { 
                    var newPart = Instantiate(_snakePartMotorPrefab, 
                                              leader.transform.position - leader.transform.forward * _linkLength, 
                                              leader.transform.rotation, 
                                              leader.transform.parent);
                    leader._dependent = newPart.transform;
                    leader.Init();
                    leader = newPart;
                }
            }
        }

        // DEBUG: guarantee execution order
        private void Init()
        { 
            _lastKnownPosition = transform.position;
            _dependent.LookAt(transform);
        }

        private IEnumerator Start()
        {
            while (Application.isPlaying)
            {
                yield return new WaitForSeconds(_maxHeadingTime);
                Vector2 xyHeading = Random.insideUnitCircle.normalized;
                _currentHeading.x = xyHeading.x;
                _currentHeading.z = xyHeading.y;
            }
        }

        private void Update()
        {
            if (_dependent == null)
            {
                return;
            }

            if (_isHead)
            {
                // face the head and move the head along a sine wave
                _currentSerpentineRadian += _angularSpeedRads * Time.deltaTime;
                float angle = _maxSerpentineAngle * Mathf.Sin(_currentSerpentineRadian);
                Vector3 trueHeading = Quaternion.AngleAxis(angle, transform.up) * _currentHeading;
                transform.rotation = Quaternion.FromToRotation(transform.forward, trueHeading) * transform.rotation;
                transform.position += transform.forward * _speed * Time.deltaTime;
            }

            Vector3 translation = transform.position - _lastKnownPosition;
            _dependent.position += translation;

            if (!Mathf.Approximately(Vector3.Dot(_dependent.forward, transform.forward), 1.0f))
            {
                TranslationInducedRotation(translation);
                RotationInducedRotation();
            }

            _lastKnownPosition = transform.position;
        }

        private void TranslationInducedRotation(Vector3 movementDelta)
        {
            // DEBUG: assumes all movement occurs in facing direction
            Vector3 localOffset = -_dependent.forward * _linkLength; // enforce linkLength, assumes _dependent is facing this
            Vector3 catchUp = localOffset - movementDelta;
            float inducedAngle = Vector3.Angle(catchUp, localOffset);
            Vector3 rotationAxis = Vector3.Cross(-_dependent.forward, catchUp); // sets rotation direction implicitly
            Quaternion inducedRotation = Quaternion.AngleAxis(inducedAngle, rotationAxis);

            _dependent.position = transform.position + (inducedRotation * localOffset);
            _dependent.rotation = Quaternion.LookRotation(transform.position - _dependent.position, transform.up);
        }

        private void RotationInducedRotation()
        {
            // keep _dependent within a region behind this transform
            float currentAngle = Vector3.Angle(transform.forward, _dependent.forward);
            if (currentAngle > _maxJointAngle)
            {
                float correctionAngle = _maxJointAngle - currentAngle;
                Vector3 localOffset = -_dependent.forward * _linkLength; // enforce linkLength, assumes _dependent is facing this
                Vector3 rotationAxis = Vector3.Cross(transform.forward, _dependent.forward); // sets rotation direction implicitly

                // DEBUG: critically damped linear spring response moves angle from correctionAngle to 0
                correctionAngle = correctionAngle + (0.0f - correctionAngle) * (float)Mathf.Exp(-_jointStiffness * Time.deltaTime);
                Quaternion inducedRotation = Quaternion.AngleAxis(correctionAngle, rotationAxis);

                _dependent.position = transform.position + (inducedRotation * localOffset);
                _dependent.rotation = Quaternion.LookRotation(transform.position - _dependent.position, transform.up);
            }
        }

        private void GravityInducedRotation()
        {
            // TODO: move/rotate dependent to align its localOffset with gravity vector
            // SOLUTION: assume a falling velocity or other movementDelta? and calculate an inducedAngle
            // SOLUTION: rotationAxis is FROM localOffset TO gravity vector
            // NOTE: only the head should be left alone because the _dependent is the affected object

            _dependent.position = _dependent.position;
            _dependent.rotation = Quaternion.LookRotation(transform.position - _dependent.position, transform.up);
        }

        private void MuscleInducedRotation()
        { 
        
        }
    }
}