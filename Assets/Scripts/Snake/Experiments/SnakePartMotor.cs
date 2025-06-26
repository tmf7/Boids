using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Complex = System.Numerics.Complex;

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
        [SerializeField] private float SPEED = 0.01f;

        [Header("Linear Spring Response")]
        [SerializeField] private float _omega = 10.0f;
        [SerializeField] private float _eta = 0.1f;

        [Header("Torsion Spring Response")]
        [SerializeField] private float _alpha = 20.0f;
        [SerializeField] private float _naturalFrequency = 10.0f;

        [Header("Joint Rotation Constraint")]
        [SerializeField][Range(0.0f, 89.0f)] private float _maxJointAngle = 15.0f; // total circular cone angle of 30 degrees

        private Vector3 _lastKnownPosition;
        private Quaternion _lastKnownRotation;
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
            _lastKnownRotation = transform.rotation;
            _dependent.LookAt(transform);
        }

        private IEnumerator Start()
        {
            if (_isHead) 
            { 
                StartCoroutine(PositionCacheCoroutine());
            }

            while (Application.isPlaying)
            {
                yield return new WaitForSeconds(_maxHeadingTime);
                Vector2 xyHeading = Random.insideUnitCircle.normalized;
                _currentHeading.x = xyHeading.x;
                _currentHeading.z = xyHeading.y;
            }
        }

        private IEnumerator PositionCacheCoroutine()
        {
            while (Application.isPlaying)
            {
                yield return new WaitForSeconds(_positionTime);
                _positions.Add(transform.position);
            }
        }

        private List<Vector3> _positions = new List<Vector3> ();
        private const float _positionTime = 0.5f;
        private void OnDrawGizmos()
        {
            if (!_isHead)
            {
                return;
            }

            Gizmos.color = Color.red;
            foreach (var position in _positions) 
            { 
                Gizmos.DrawSphere(position, 0.5f);
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

            // FIXME(?): the spring solutions only overshoot for snakey movement if
            // (1) each part has different constants, and
            // (2) the initialAngle is manipulated to amplify/encourage overshoot
            // SOLUTION: if treated as muscle contractions moving down the the snake, then not all parts move at once
            // and even if treated as rope in space not all parts move at once...
            // in fact parts further along MAY feel some pull from the parts ahead and only move very slightly until
            // the full force of the wave hits that part
            // THEREFORE: DO NOT treat as a tower of springs, but as a rope...what is the movement propogation then?

            if (!Mathf.Approximately(Vector3.Dot(_dependent.forward, transform.forward), 1.0f))
            {
                //TorsionSpringDamperRotation();
                //LinearSpringDamperRotation();

                TranslationInducedRotation(translation);
                RotationInducedRotation();
            }

            _lastKnownPosition = transform.position;
            _lastKnownRotation = transform.rotation;
        }

        // rotation-induced rotation
        private void LinearSpringDamperRotation()
        {          
            // FIXME(~): switch to using forward vectors for the angle, and rotate the localOffset (not the final offset)
            Vector3 finalOffset = -transform.forward * _linkLength;
            Vector3 initialOffset = -_dependent.forward * _linkLength; // enforce linkLength, assumes _dependent is facing this
            float initialAngle = Vector3.Angle(initialOffset, finalOffset);
            float finalAngle = 0.0f;
            // NOTE: omega ~= 15, eta ~= 0.1 should overshoot, but doesn't really (even at different timescale SPEED)
            // probably because the rotational delta frame-to-frame is very small near the head, but amplified near the tail
            // IE: the constant that multiplies the equation represents the amplitude, and its never large
            // NOTE: 15/0.1 does exhibit snakelike motion if already rotating side to side
            Complex lambda = -_omega * (_eta + Complex.Sqrt((_eta * _eta) - 1.0f)); // calc once, keep const

            // FIXME: under what circumstance does currentAngle instantly become initialAngle
            // FIXME(?): NO overshoot unless setup extremely stiff/fast
            // DEBUG: real and imaginary oscillate together out of phase (but imaginary starts at 0)
            float currentAngle = finalAngle + (initialAngle - finalAngle) * (float)Complex.Exp(lambda * Time.deltaTime * SPEED).Real;

            // FIXME: maybe use initialOffest more directly?
            // DEBUG: rotation axis not normalized, doesn't have a significant effect
            Vector3 rotationAxis = Vector3.Cross(finalOffset, initialOffset);
            Quaternion currentRotation = Quaternion.AngleAxis(currentAngle, rotationAxis); 
            Vector3 currentOffset = currentRotation * finalOffset;

            _dependent.position = transform.position + currentOffset;
            _dependent.rotation = Quaternion.LookRotation(transform.position - _dependent.position, transform.up);
            //_dependent.LookAt(transform.position);
        }

        // rotation-induced rotation
        private void TorsionSpringDamperRotation()
        {
            Quaternion rotationDelta = Quaternion.Inverse(_lastKnownRotation) * transform.rotation;

            Vector3 finalOffset = -transform.forward * _linkLength;
            Vector3 initialOffset = -_dependent.forward * _linkLength; // enforce linkLength, assumes _dependent is facing this
            float initialAngle = Vector3.Angle(initialOffset, finalOffset);
            float finalAngle = 0.0f;
            // NOTE: alpha ~= 2, naturalFrequency ~= 16.125 (sqrt(260)) should overshoot, but doesn't really (even at different timescale SPEED)
            // probably because the rotational delta frame-to-frame is very small near the head, but amplified near the tail
            // IE: the constant that multiplies the equation represents the amplitude, and its never large
            // NOTE: alpha == 9, naturalFrequency = 10 also doesn't oveshoot, but looks very smooth
            // and 2/16 does exhibit snakelike motion if already rotating side to side
            Complex omega = Complex.Sqrt((_naturalFrequency * _naturalFrequency) - (_alpha * _alpha));

            // FIXME(?): NO overshoot unless setup extremely stiff/fast
            // DEBUG: real and imaginary oscillate together out of phase (but imaginary starts at 0)
            // DEBUG: phase angle in cos(omega * dt + phase) is set to 0
            float currentAngle = finalAngle 
                + (initialAngle - finalAngle) 
                * (float)(Complex.Exp(-_alpha * Time.deltaTime * SPEED)
                * Complex.Cos(omega * Time.deltaTime * SPEED)).Real;

            // DEBUG: rotation axis not normalized, doesn't have a significant effect
            Quaternion currentRotation = Quaternion.AngleAxis(currentAngle, Vector3.Cross(finalOffset, initialOffset));
            Vector3 currentOffset = currentRotation * finalOffset;

            _dependent.position = transform.position + currentOffset;
            _dependent.rotation = Quaternion.LookRotation(transform.position - _dependent.position, transform.up);
            //_dependent.LookAt(transform.position);
        }

        private void TranslationInducedRotation(Vector3 movementDelta)
        {
            // TODO(~): use the same Mathf.Exp logic to interpolate, but at a scaled rate based on movementDelta
            // TODO(~): apply the same max joint angle logic to translation-induced rotation?

            // DEBUG: assumes movement only occurs in facing direction
            Vector3 localOffset = -_dependent.forward * _linkLength; // enforce linkLength, assumes _dependent is facing this
            float inducedAngle = 0.0f;
            Vector3 rotationAxis = transform.up;

            Vector3 catchUp = localOffset - movementDelta;
            inducedAngle = Vector3.Angle(catchUp, localOffset);
            rotationAxis = Vector3.Cross(-_dependent.forward, catchUp); // sets rotation direction implicitly

            //if (Vector3.Dot(transform.forward, -_dependent.forward) <= 0.0f) 
            //{
            //    Vector3 catchUp = localOffset - movementDelta;
            //    inducedAngle = Vector3.Angle(catchUp, localOffset);
            //    rotationAxis = Vector3.Cross(-_dependent.forward, catchUp); // sets rotation direction implicitly
            //}
            //else
            //{
            //    Debug.Log("ALT");
            //    // option 0: works, but develops square loops that roll down the snake
            //    //const float STIFFNESS = 2.0f * 1.0f; // 100% stiff is just 2.0f
            //    //inducedAngle = STIFFNESS * (90.0f - Vector3.Angle(transform.forward, -_dependent.forward));
            //    //rotationAxis = Vector3.Cross(transform.forward, -_dependent.forward);

            //    // option 1: works, allows joints to rotate 360
            //    // around eachother without breaking linkLength (may allow angle contstraints)
            //    Vector3 depInitial = _dependent.position - movementDelta; // TODO: pass this in
            //    Vector3 depAsPivotOffset = depInitial - transform.position;
            //    inducedAngle = Vector3.Angle(localOffset, depAsPivotOffset);
            //    rotationAxis = Vector3.Cross(transform.forward, -_dependent.forward);

            //    // option 2: works, but developts triangular loops that roll down the snake (even with non-zero FLEX)
            //    //const float FLEX = 0.0f;
            //    //Vector3 depInitial = _dependent.position - movementDelta; // TODO: pass this in
            //    //float facingAngle = Vector3.Angle(transform.forward, -_dependent.forward);
            //    //Vector3 transverseDelta = movementDelta / Mathf.Cos((90.0f - facingAngle) * Mathf.Deg2Rad);
            //    //Vector3 axialDelta = movementDelta - transverseDelta;
            //    //Vector3 pushedDepPosition = depInitial - transverseDelta + axialDelta * FLEX;
            //    //Vector3 depAsWallOffset = pushedDepPosition - transform.position;
            //    //inducedAngle = Vector3.Angle(depAsWallOffset, localOffset);
            //    //rotationAxis = Vector3.Cross(transform.forward, -_dependent.forward);


            //    // THIS SHOULD NEVER HIT
            //    if (inducedAngle > 90.0f)
            //    {
            //        Debug.Log("OVER 90 INDUCED ANGLE");
            //        rotationAxis = -rotationAxis;
            //    }
            //}



            // OPTION A: whips
            //float scaledMovementDelta = Mathf.Sqrt(rotationAxis.sqrMagnitude * movementDelta.sqrMagnitude);
            //float angle = Mathf.Atan(scaledMovementDelta / _linkLength) * Mathf.Rad2Deg;

            // OPTION B: whips
            //float angle = Vector3.Angle(transform.forward, _dependent.forward);
            //angle *= Mathf.Min(1.0f, Mathf.Sqrt(movementDelta.sqrMagnitude / localOffset.sqrMagnitude)); // rough alignment "angular velocity"


            Quaternion inducedRotation = Quaternion.AngleAxis(inducedAngle, rotationAxis);
            _dependent.position = transform.position + (inducedRotation * localOffset);
            _dependent.rotation = Quaternion.LookRotation(transform.position - _dependent.position, transform.up);
            //_dependent.LookAt(transform);
        }

        // TODO: in final version leave eta at 1.0f (no need for overshoot which never happens or complex numbers
        private void RotationInducedRotation()
        {
            // keep _dependent within a region behind this transform
            // FIXME: don't make it instant, either interpolate, or apply a turn speed (or acceleration)
            float currentAngle = Vector3.Angle(transform.forward, _dependent.forward);
            if (currentAngle > _maxJointAngle)
            {
                float correctionAngle = _maxJointAngle - currentAngle;
                Vector3 localOffset = -_dependent.forward * _linkLength; // enforce linkLength, assumes _dependent is facing this
                Vector3 rotationAxis = Vector3.Cross(transform.forward, _dependent.forward); // sets the rotation direction implicitly


                // linear spring damper response of correctionAngle to 0
                // DEBUG: real and imaginary oscillate together out of phase (but imaginary starts at 0)
                Complex lambda = -_omega * (_eta + Complex.Sqrt((_eta * _eta) - 1.0f)); // TODO: calc once, keep const
                correctionAngle = correctionAngle + (0.0f - correctionAngle) * (float)Complex.Exp(lambda * Time.deltaTime * SPEED).Real;



                Quaternion inducedRotation = Quaternion.AngleAxis(correctionAngle, rotationAxis);
                _dependent.position = transform.position + (inducedRotation * localOffset);
                _dependent.rotation = Quaternion.LookRotation(transform.position - _dependent.position, transform.up);
                //_dependent.LookAt(transform);
            }
        }

        private void MuscleInducedRotation()
        { 
        
        }
    }
}