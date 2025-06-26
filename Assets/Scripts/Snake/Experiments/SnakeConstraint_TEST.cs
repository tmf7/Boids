using UnityEngine;

namespace Freehill.SnakeLand
{
    public class SnakeConstraint_TEST : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField][Min(0.0f)] private float _linkLength = 1.0f;
        [SerializeField] private Terrain _terrain;
        [SerializeField][Min(0.0f)] private float _speed = 5.0f;

        private Vector3 _lastTargetPosition;
        private Vector3 _targetHeading;

        private void Awake()
        {
            Vector3 groundedPosition = _target.position;
            groundedPosition.y = _terrain.SampleHeight(groundedPosition);
            _target.position = groundedPosition + Vector3.up * 0.5f;

            // move along the xz plane of the terrain
            Vector2 xyHeading = Random.insideUnitCircle.normalized;
            _targetHeading = new Vector3(xyHeading.x, 0.0f, xyHeading.y);
        }

        private void FixedUpdate()
        {
            // TODO: apply gravity movement, then adjust to linklength
            // possibly via gravity force/rigidbody, or maybe kinematic motion?
        }

        // NOTE: old school snake caches body positions, adds a new head position, and removes last tail position,
        // then draws everything at all remaining cached positions
        // which only works if the head moves a full linkLength each step to maintain the snake's overall length
        private Vector3 _fallingVelocity = Vector3.zero;
        private void Update()
        {
            if (_target == null)
            {
                return;
            }

            // TODO: apply gravitational torque to parts (ie: may overshoot, but also add stiffness in joints)
            // SOLUTION(no): use a hinge or spring
            if (Vector3.Dot(Vector3.up, transform.forward) < 0.99f)
            {
                // TODO: 
                _fallingVelocity += Physics.gravity * Time.deltaTime;
                transform.position += _fallingVelocity * Time.deltaTime;
            }
            else
            {
                _fallingVelocity = Vector3.zero;
            }

            // TODO: move to a position on the sphere around the target

            // option 1: straight behind target by linkLength (no sqrt) (target must rotate for relative rotation)
            //transform.position = _target.position - _target.forward * _linkLength;

            // option 2: closest point on sphere (has sqrt) (doesn't force rotation)
            //Vector3 link = transform.position - _target.position;
            //transform.position = _target.position + link.normalized * _linkLength;


            // option 2a: closest point on sphere (no sqrt) (forces rotation)
            transform.LookAt(_target.position);
            transform.position = _target.position - transform.forward * _linkLength;

            // option 3: move towards the last known position at a fixed speed (has sqrt) (misses key positions of head path)
            // snapshot the snake
            //_lastTargetPosition = _target.position;
            // move the head
            //Vector3 terrainNormal = SampleTerrainNormalAtTarget(); // inaccurate for suddenly steep slopes at varying speeds
            //_targetHeading = Vector3.ProjectOnPlane(_targetHeading, terrainNormal).normalized;
            //_target.position += _targetHeading * _speed * Time.deltaTime;
            //transform.position = Vector3.MoveTowards(transform.position, _lastTargetPosition, _speed * Time.deltaTime);

            // option 4: same as 3, except place a terrain point ahead of the head and have head perform MoveTowards
            // ???

            // option 5: no sudden terrain such that snapping to height had no significant affect on velocity magnitude

            // TODO: add a snake joint constraint that prevents joints from passing through eachother
            // SOLUTION: requires checking angle between pairs of parts is larger than some cone angle
        }

        // terrain is on the xz plane
        private Vector3 SampleTerrainNormalAtTarget()
        {
            Vector3 pivot = transform.position;
            Vector3 terrainSize = _terrain.terrainData.size;
            float x = pivot.x / terrainSize.x;
            float z = pivot.z / terrainSize.z;
            return _terrain.terrainData.GetInterpolatedNormal(x, z);
        }

        private void OnDrawGizmos()
        {
            if (_terrain == null || _target == null)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Vector3 pivot = _target.position;
            Vector3 terrainSize = _terrain.terrainData.size;
            float x = pivot.x / terrainSize.x;
            float z = pivot.z / terrainSize.z;
            Vector3 terrainNormal = _terrain.terrainData.GetInterpolatedNormal(x, z);
            Gizmos.DrawRay(pivot, terrainNormal);
        }
    }
}