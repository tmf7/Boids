using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Freehill.SnakeLand
{
    public class SnakeMovement : MonoBehaviour
    {
        [SerializeField] private VelocitySource _velocitySource; // PlayerMovment or AIMovement

        /// <summary>
        /// Every snake part is spherical and symmetrically scaled, 
        /// so this adds/subtracts space betweeen parts beyond Scale/radius
        /// </summary>
        [SerializeField] private float _linkLengthOffset = 0.5f;

        private SnakeHead _snakeHead;
        private List<Transform> _snakeParts = new List<Transform>(DEFAULT_SNAKE_CAPACITY);
        //private Terrain _terrain;

        private float _accumulatedMovement = 0.0f;
        private List<(Vector3 position, float pathLength)> _pathWaypoints = new List<(Vector3, float)>(DEFAULT_SNAKE_CAPACITY);

        public const int MIN_SNAKE_LENGTH = 5;
        public const int DEFAULT_SNAKE_CAPACITY = 200;

        private const float PART_DIVISOR = 1.0f / MIN_SNAKE_LENGTH;
        private const float SCALE_MULTIPLIER = 1.5f;
        private const float WAYPOINTS_PER_LINK = 2.0f;

        // scales from 1 @ 6 parts to ~5 @ 200 parts
        private float Scale => ActiveLength > MIN_SNAKE_LENGTH ? SCALE_MULTIPLIER * Mathf.Log(ActiveLength * PART_DIVISOR) + 1 : 1.0f;
        //private Terrain Terrain => _terrain ??= SpawnPointManager.WorldBounds.Terrain;

        /// <summary> Dynamic turning radius directly proportional to the snake's scale. </summary>
        private float TurningRadius => 1.0f * Scale;

        public VelocitySource VelocitySource => _velocitySource;
        public bool IsOwnHead(Transform part) => _snakeHead.transform == part;
        public bool IsSelf(Transform part) => _snakeParts.Contains(part);

        /// <summary> Returns the visible, active, length of the snake. </summary>
        public int ActiveLength => _snakeParts.Count(part => part.gameObject.activeSelf);

        // FIXME: grow from the neck not the tail, set a growth target and spawn/activate **as path length is available**
        /// <summary> 
        /// Returns the active tail position of the snake
        /// at which new parts should be instantiated and positioned
        /// </summary>
        public Vector3 TailPosition => _snakeParts[_snakeParts.Count - 1].position;// _snakeParts[0].position;

        /// <summary>
        /// Returns true if the given item is behind (not at) the given body part number along the length of the snake, with 0 being the head.
        /// Returns false if part is not part of the snake, or at/in-front-of the given part number.
        /// </summary>
        public bool IsPartBehind(Transform part, int partNumber) => _snakeParts.IndexOf(part) > partNumber;
        public float LinkLength => Scale + _linkLengthOffset;

        public void Init(SnakesManager snakesManager, Snake owner)
        {
            AddHead(owner.Head);

            if (_velocitySource is PlayerMovement)
            {
                // ensure the player camera follows the player's head
                ((PlayerMovement)_velocitySource).SetCameraConstraintSource(_snakeHead.transform);
            }
            else if (_velocitySource is AIMovement)
            {
                ((AIMovement)_velocitySource).Init(snakesManager, owner);
            }
        }

        private void UpdateScale()
        {
            foreach (Transform snakePart in _snakeParts)
            {
                snakePart.localScale = Scale * Vector3.one;
            }
        }

        /// <summary>
        /// Accumulates movement distance regardless of direction.
        /// When a threshold is reached the current position is cached. 
        /// <para/>
        /// DEBUG: applying movement before or after will result in a different cached position.
        /// </summary>
        private void AddMovementHistory(Vector3 movement)
        {
            float frameMovementThreshold = LinkLength / WAYPOINTS_PER_LINK;
            float movementMag = movement.magnitude; // PERF: once per frame

            _accumulatedMovement += movementMag;

            if (_accumulatedMovement > frameMovementThreshold)
            {
                _pathWaypoints.Add((_snakeHead.transform.position, _accumulatedMovement));
                _accumulatedMovement = 0.0f;

                while (_pathWaypoints.Count > (ActiveLength * WAYPOINTS_PER_LINK))
                {
                    _pathWaypoints.RemoveAt(0);
                }
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 0.5f);
            for (int i = 0; i < _pathWaypoints.Count; i++) 
            {
                Gizmos.DrawSphere(_pathWaypoints[i].position, Scale * 0.25f);
            }
        }

        /// <summary> Returns the number of pre-instantiated, inactive, parts that were activated, up to the given length </summary>
        public int TryActivateParts(int length)
        {
            int activatedParts = 0;

            for (int i = ActiveLength; i < _snakeParts.Count && activatedParts < length; ++i)
            {
                if (!_snakeParts[i].gameObject.activeSelf)
                {
                    _snakeParts[i].gameObject.SetActive(true);
                    activatedParts++;
                }
            }

            UpdateScale();

            return activatedParts;
        }

        private void AddHead(SnakeHead head)
        {
            _snakeHead = head;
            AddPart(_snakeHead.transform);
        }

        /// <summary> Adds a single part to the tail of the snake. </summary>
        public void AddPart(Transform newPart)
        {
            if (!_snakeParts.Contains(newPart))
            {
                _snakeParts.Add(newPart);
                UpdateScale();
            }
        }

        /// <summary> Deactivates all parts at and after the given part, and returns all removed parts in tail-to-cut order. </summary>
        public List<Transform> CutAt(Transform firstCutPart)
        {
            // PERF: cuts not occuring often enough to merit a private data member to avoid memory allocation overhead
            var cutParts = new List<Transform>();

            int firstCutIndex = _snakeParts.IndexOf(firstCutPart);

            if (firstCutIndex >= MIN_SNAKE_LENGTH)
            {
                for (int i = ActiveLength - 1; i >= firstCutIndex; --i)
                {
                    cutParts.Add(_snakeParts[i]);
                    _snakeParts[i].gameObject.SetActive(false);
                }
                UpdateScale();
            }

            return cutParts;
        }

        // ===== PERF LIST ======
        // TODO: reduce triangles
        // TODO: switch to mathematical animation on pickups (no bloated animator)
        // TODO: adjust lighting fidelity
        // TODO: Mesh LODs, and not rendering if off camera
        // TODO: less diverse pickups (eg: just apples), only change color and position and ==> use GPU instancing
        // TODO: compose the snake colliders differently? body under head = all one rigidbody with one composite collider
        // TODO: instead of physics collisions, just do distance checks along/on curves (composite line segment sequences)?
        public void UpdateBody()
        {
            _velocitySource.RotateToFaceTargetHeading(TurningRadius);
            Vector3 headMovement = _velocitySource.CurrentFacing * _velocitySource.Speed * Time.deltaTime;

            // DEBUG: assumes headMovement is a vector on the XZ plane,
            // and all movement is on the XZ plane
            // account for suddent y-axis growth
            float groundOffset = /*Terrain.SampleHeight(nextPosition) +*/ (Scale * 0.5f);
            Vector3 leaderPosition = _snakeHead.transform.position;
            leaderPosition.y = groundOffset;
            Vector3 nextPosition = leaderPosition + headMovement;

            _snakeHead.transform.LookAt(nextPosition);
            _snakeHead.transform.position = nextPosition;
            AddMovementHistory(headMovement);

            if (_pathWaypoints.Count < 1)
            {
                return;
            }

            int activeLength = ActiveLength;
            float linkLength = LinkLength;
            float pathLength = (_pathWaypoints[_pathWaypoints.Count - 1].position - _snakeHead.transform.position).magnitude;
            float waypointLength = 0.0f;
            const float PATH_LENGTH_TOLERANCE = 0.01f;

            // iterating pathIndex from [Count - 1, 0] is the path head to tail
            // interpolate positions of all parts along the cached path of the head
            for (int i = 1, pathIndex = _pathWaypoints.Count - 1; i < activeLength && pathIndex >= 1; ++i)
            {
                // never use the 0th waypointLength because it cant provide a inter-waypoint path direction
                while (pathIndex >= 1)
                {
                    waypointLength = _pathWaypoints[pathIndex].pathLength;

                    // never use the 0th waypointLength
                    if (pathLength + waypointLength > linkLength || pathIndex == 1)
                    {
                        break;
                    }

                    pathLength += waypointLength;
                    pathIndex--;
                }

                // [0,1] fraction of current waypointLength needed to match LinkLength
                float t = (linkLength - pathLength) / waypointLength;

                if (pathLength + (t * waypointLength) < linkLength - PATH_LENGTH_TOLERANCE)
                {
                    break;
                }

                Vector3 offset = t * (_pathWaypoints[pathIndex - 1].position - _pathWaypoints[pathIndex].position);
                _snakeParts[i].position = _pathWaypoints[pathIndex].position + offset;
                _snakeParts[i].LookAt(_snakeParts[i - 1].position);

                // preserve any remainder for the next part
                // ensure pathIndex (ie one waypointLength) isnt counted twice
                pathLength = (1.0f - t) * waypointLength;
                pathIndex--;
            }
        }
    }

    #region EXPENSIVE_DO_NOT_USE
#if EXPENSIVE_MOVEMENT
        /// <summary> 
        /// Moves the head, then cascades the body movement counter to how the head moved, nearly preserving <see cref="LinkLength"/> on flat surfaces.
        /// NOTE: does not perserve the original path traced by the head.
        /// NOTE: most stable on nearly-planar movement
        /// </summary>
        public void MoveCascade()
        {
            _velocitySource.RotateToFaceTargetHeading(TurningRadius);
            Vector3 headMovement = _velocitySource.CurrentFacing * _velocitySource.Speed * Time.deltaTime;

            // DEBUG: assumes headMovement is a vector on the XZ plane,
            // and all movement is on the XZ plane
            // account for suddent y-axis growth
            float groundOffset = /*Terrain.SampleHeight(nextPosition) +*/ (Scale * 0.5f);
            Vector3 leaderPosition = _snakeHead.transform.position;
            leaderPosition.y = groundOffset;
            Vector3 nextPosition = leaderPosition + headMovement;

            _snakeHead.transform.LookAt(nextPosition);
            _snakeHead.transform.position = nextPosition;
            AddMovementHistory(headMovement);

            Vector3 link;
            Vector3 selfOldPosition;
            Vector3 jointMovement = headMovement;

            int activeLength = ActiveLength;
            float linkLength = LinkLength;
            float sqrLinkLength = linkLength * linkLength;

            for (int i = 1; i < activeLength; ++i)
            {
                leaderPosition = _snakeParts[i - 1].position;
                selfOldPosition = _snakeParts[i].position;

                // account for suddent y-axis growth
                selfOldPosition.y = groundOffset; 
                _snakeParts[i].position = selfOldPosition;

                _snakeParts[i].position -= jointMovement;
                _snakeParts[i].LookAt(leaderPosition); // PERF: avoids square root

                // allow joints to squish/move past eachother
                link = leaderPosition - _snakeParts[i].position;
                if (link.sqrMagnitude >= sqrLinkLength)
                {
                    // TODO: set pos.y here if non-planar movement
                    _snakeParts[i].position = leaderPosition - _snakeParts[i].forward * LinkLength;
                }

                jointMovement = _snakeParts[i].position - selfOldPosition;
            }

            Vector3 tailPosition = TailPosition;

            // DEBUG: inactive parts just track the tail part to avoid visual and physics bugs when re-activated
            for (int i = activeLength; i < _snakeParts.Count; ++i)
            {
                _snakeParts[i].position = tailPosition;
            }
        }
#endif
    #endregion EXPENSIVE_DO_NOT_USE
}