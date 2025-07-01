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
            _dependent.rotation = Quaternion.LookRotation(transform.position - _dependent.position, _dependent.up);
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

            //if (_isHead)
            //{
            //    // face the head and move the head along a sine wave
            //    _currentSerpentineRadian += _angularSpeedRads * Time.deltaTime;
            //    float angle = _maxSerpentineAngle * Mathf.Sin(_currentSerpentineRadian);
            //    Vector3 trueHeading = Quaternion.AngleAxis(angle, transform.up) * _currentHeading;
            //    transform.rotation = Quaternion.FromToRotation(transform.forward, trueHeading) * transform.rotation;
            //    transform.position += transform.forward * _speed * Time.deltaTime;
            //}

            // TODO: combine all three motions, ending with a final FACE the leader + linkLength offset
            GravityInducedTranslation();
            TranslationInducedRotation();

            if (!Mathf.Approximately(Vector3.Dot(_dependent.forward, transform.forward), 1.0f))
            {
                RotationInducedRotation();
            }
        }

        private void TranslationInducedRotation()
        {
            _dependent.rotation = Quaternion.LookRotation(transform.position - _dependent.position, _dependent.up);
            _dependent.position = transform.position - _dependent.forward * _linkLength;
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
                _dependent.rotation = Quaternion.LookRotation(transform.position - _dependent.position, _dependent.up);
            }
        }

        private Vector3 _dependentVelocity = Vector2.zero;
        private readonly float GRAVITY_MAG = -Physics.gravity.magnitude;
        private readonly Vector3 GRAVITY_DIR = Physics.gravity.normalized;
        [SerializeField] private float DRAG_COEFFICIENT = 0.25f;

        // chain forces propogation
        private void GravityInducedTranslation()
        {
            // TODO: get xzForward once for the head and apply to all parts
            //...maybe not because they won't all face on a line
            Vector3 xzForward = Quaternion.FromToRotation(_dependent.up, -GRAVITY_DIR) * _dependent.forward;

            
            // TODO: needs more oomph, add the extra gravity force of everything BELOW a part
            // ...in other words, account for O<-->O<-->O instead of the current O-->O-->O
            // SOLUTION: go over the snake forward then backward in one frame, treating dependent as the leader 
            // going backward (ensure original _dependent still faces its leader in the end)
            float currentAngle = Vector3.Angle(xzForward, -_dependent.forward) * Mathf.Deg2Rad;
            float sin = Mathf.Sin(currentAngle);
            float cos = Mathf.Cos(currentAngle);
            float accelerationX = GRAVITY_MAG * sin * cos;
            float accelerationY = GRAVITY_MAG * sin * sin - GRAVITY_MAG;
            _dependentVelocity.x += accelerationX * Time.deltaTime; // velocity along xzForward
            _dependentVelocity.y += accelerationY * Time.deltaTime; // velocity along GRAVITY_DIR

            // FIXME: Using Max(0,vel.x) prevents the wild sine oscillations, but also prevents reasonable overshoot
            //_dependentVelocity.x = Mathf.Max(0.0f, _dependentVelocity.x - Mathf.Sign(_dependentVelocity.x) * DRAG_COEFFICIENT * _dependentVelocity.x * _dependentVelocity.x * Time.deltaTime);
            //_dependentVelocity.y = Mathf.Max(0.0f, _dependentVelocity.y - Mathf.Sign(_dependentVelocity.y) * DRAG_COEFFICIENT * _dependentVelocity.y * _dependentVelocity.y * Time.deltaTime);

            Vector3 gravityMovementDelta = ((xzForward * _dependentVelocity.x) + (GRAVITY_DIR * _dependentVelocity.y)) * Time.deltaTime;
            _dependent.position += gravityMovementDelta;
            _dependent.rotation = Quaternion.LookRotation(transform.position - _dependent.position, _dependent.up);
            _dependent.position = transform.position - _dependent.forward * _linkLength;
        }

        private void MuscleInducedRotation()
        { 
        
        }
    }
}