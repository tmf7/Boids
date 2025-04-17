using Codice.CM.Common;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Codice.CM.Common.CmCallContext;
using static UnityEngine.GraphicsBuffer;

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

        private Snake _owner;
        private SnakeHead _snakeHead;
        private List<Transform> _snakeParts = new List<Transform>(DEFAULT_SNAKE_CAPACITY);
        private Terrain _terrain;
        private int _targetLength = MIN_SNAKE_LENGTH;

        private float _accumulatedMovement = 0.0f;
        //private float _accumulatedNeckGrowth = 0.0f;
        private List<(Vector3 position, float pathLength)> _pathWaypoints = new List<(Vector3, float)>(DEFAULT_SNAKE_CAPACITY);

        public const int MIN_SNAKE_LENGTH = 5;
        public const int DEFAULT_SNAKE_CAPACITY = 200;

        private const float PART_DIVISOR = 1.0f / MIN_SNAKE_LENGTH;
        private const float SCALE_MULTIPLIER = 1.5f;
        private const float WAYPOINTS_PER_LINK = 2.0f;
        private const float LINK_LENGTH_TOLERANCE = 0.01f;

        // scales from 1 @ 6 parts to ~5 @ 200 parts
        private float _currentScale = 1.0f;
        private float TargetScale => ActiveLength > MIN_SNAKE_LENGTH ? SCALE_MULTIPLIER * Mathf.Log(ActiveLength * PART_DIVISOR) + 1 : 1.0f;
        private Terrain Terrain => _terrain ??= SpawnPointManager.WorldBounds.Terrain;

        /// <summary> Dynamic turning radius directly proportional to the snake's scale. </summary>
        private float TurningRadius => 1.0f * _currentScale;

        public VelocitySource VelocitySource => _velocitySource;
        public bool IsOwnHead(Transform part) => _snakeHead.transform == part;
        public bool IsSelf(Transform part) => _snakeParts.Contains(part);

        /// <summary> Returns the visible, active, length of the snake. </summary>
        public int ActiveLength => _snakeParts.Count(part => part.gameObject.activeSelf);

        /// <summary> 
        /// Returns the active head position of the snake
        /// at which new parts should be instantiated and positioned as they move into their final ordered position
        /// </summary>
        public Vector3 HeadPosition => _snakeParts[0].position; // DEBUG: equivalent to _snakeHead.transform.position;

        /// <summary>
        /// Returns true if the given item is behind (not at) the given body part number along the length of the snake, with 0 being the head.
        /// Returns false if part is not part of the snake, or at/in-front-of the given part number.
        /// </summary>
        public bool IsPartBehind(Transform part, int partNumber) => _snakeParts.IndexOf(part) > partNumber;
        public float LinkLength => _currentScale + _linkLengthOffset;

        public void Init(SnakesManager snakesManager, Snake owner, SnakeHead head, Transform tail)
        {
            _owner = owner;
            _snakeHead = head;

            // DEBUG: head SpawnPoint must place the head's pivot on the Terrain surface
            // such that this places the head and tail tangent to the terrain surface
            head.transform.position += Vector3.up * (_currentScale * 0.5f);
            head.transform.forward = _velocitySource.CurrentFacing;

            Vector3 tailOffset = new Vector3(-0.707f, 0.0f, 0.707f) * LinkLength;
            Vector3 tailPosition = tail.position + tailOffset;
            tailPosition.y = Terrain.SampleHeight(tail.position) + (_currentScale * 0.5f);
            tail.position = tailPosition;
            tail.LookAt(head.transform);

            AddPart(_snakeHead.transform);
            AddPart(tail);

            _growthLinkLength = (_snakeHead.transform.position - tail.position).magnitude;
            _pathWaypoints.Add((tail.position, 0.0f));
            _pathWaypoints.Add((_snakeHead.transform.position, _growthLinkLength));

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
            // DEBUG: approaches but never quite precisely matches
            float alpha = Mathf.Exp(-_growthRate * Time.deltaTime);
            _currentScale = TargetScale + (_currentScale - TargetScale) * alpha;

            foreach (Transform snakePart in _snakeParts)
            {
                snakePart.localScale = _currentScale * Vector3.one;
            }
        }

        /// <summary>
        /// Accumulates movement distance regardless of direction.
        /// When a threshold is reached the current position is cached. 
        /// <para/>
        /// DEBUG: applying movement before or after will result in a different cached position.
        /// </summary>
        private void AddMovementHistory(float movementMagnitude)
        {
            float frameMovementThreshold = LinkLength / WAYPOINTS_PER_LINK;

            _accumulatedMovement += movementMagnitude;

            if (_accumulatedMovement > frameMovementThreshold)
            {
                _pathWaypoints.Add((_snakeHead.transform.position, _accumulatedMovement));
                _accumulatedMovement = 0.0f;

                while (_pathWaypoints.Count > (_targetLength * WAYPOINTS_PER_LINK))
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
                Gizmos.DrawSphere(_pathWaypoints[i].position, _currentScale * 0.25f);
            }
        }

        /// <summary>
        /// Snake will grow by the given amount as path length becomes available (this is not instant length addition).
        /// NOTE: negative numbers are zeroed
        /// </summary>
        public void AddToTargetLength(int addLength)
        {
            _targetLength += Mathf.Max(0, addLength);
        }

        /// <summary> Returns true if a pre-instantiated, inactive, part was activated. Otherwise, returns false. </summary>
        public bool TryActivatePart()
        {
            int firstInactiveIndex = ActiveLength;

            if (firstInactiveIndex < _snakeParts.Count)
            { 
                _snakeParts[firstInactiveIndex].gameObject.SetActive(true);
                return true;
            }

            return false;
        }

        /// <summary> Adds a single part to the tail of the snake. </summary>
        public void AddPart(Transform newPart)
        {
            if (!_snakeParts.Contains(newPart))
            {
                _snakeParts.Add(newPart);
                newPart.LookAt(HeadPosition);
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
        private float _growthLinkLength = 0.0f;
        private float _growthRate = 0.5f;//1.43f;
        public void UpdateBody()
        {
            if (VelocitySource.IsStopped) 
            {
                return;
            }

            _velocitySource.RotateToFaceTargetHeading(TurningRadius);
            Vector3 headMovement = _velocitySource.CurrentFacing * _velocitySource.Speed * Time.deltaTime;

            // DEBUG: assumes headMovement is a vector on the XZ plane,
            // and all movement is on the XZ plane
            // account for sudden y-axis growth
            Vector3 initialHeadPosition = HeadPosition;
            Vector3 finalHeadPosition = initialHeadPosition + headMovement;
            finalHeadPosition.y = Terrain.SampleHeight(finalHeadPosition) + (_currentScale * 0.5f);
            headMovement = finalHeadPosition - initialHeadPosition;

            float headMovementMagnitude = headMovement.magnitude;
            int activeLength = ActiveLength;
            float linkLength = LinkLength;

            // SOLUTION(!): once the FULLY STOPPED implementation is in place, then allow a "growth rate" where the new part moving into place isn't dictated by snake speed
            // but instead by "growth rate", and allow the whole snake to keep moving (slightly less than full speed for body, full speed for head)
            // SOLUTION: this means...the neck wont hold still
            // ..apply some fraction of the headMovement to the new neck, and use that for the rest of the body along the path

            // FIXME(meh): spawned parts gets smushed a bit if A LOT of growth is needed such that the Scale notably changes (ie: Linklength changes over growth length)
            // SOLUTION: keep moving such that ALL parts are "pulled apart" during growth
            // growth in motion logic
            //if (activeLength < _targetLength)
            //{
            //    // if the neck is positioned correctly along path, then Grow, if not, do nothing
            //    if (_accumulatedNeckGrowth >= linkLength)
            //    {
            //        _owner.Grow();
            //        _accumulatedNeckGrowth = 0.0f;
            //    }

            //    _snakeHead.transform.LookAt(finalHeadPosition); 
            //    _snakeHead.transform.position = finalHeadPosition;
            //    AddMovementHistory(headMovementMagnitude);

            //    // ensure neck keeps aimed at head as it moves
            //    Vector3 finalNeckPosition = _snakeParts[activeLength - 1].position + headMovement;
            //    _snakeParts[activeLength - 1].LookAt(finalNeckPosition);
            //    _snakeParts[activeLength - 1].position = finalNeckPosition;
            //    UpdateScale(); // FIXME: doesn't account for sudden cuts to length

            //    _accumulatedNeckGrowth += headMovementMagnitude;
            //    return;
            //}

            _snakeHead.transform.LookAt(finalHeadPosition);
            _snakeHead.transform.position = finalHeadPosition;
            AddMovementHistory(headMovementMagnitude);

            if (_pathWaypoints.Count < 1)
            {
                return;
            }

            float pathLength = (_pathWaypoints[_pathWaypoints.Count - 1].position - finalHeadPosition).magnitude;
            float waypointLength = 0.0f;
            bool growing = activeLength < _targetLength;

            // TODO: if growing, then require a fraction of linkLength when placing the new part, and interpolate as normal
            // smooth lerp from 0 to linkLength until the new part is interpolated ~linkLength, then add the next part and reset
            // otherwise interpolate as normal
            if (growing)
            {
                if (Mathf.Abs(_growthLinkLength - linkLength) < linkLength * 0.1f)
                {
                    _owner.Grow();
                    _growthLinkLength = 0.0f;
                    activeLength++;
                }
                else
                { 
                    // FIXME: if growth rate is too high then the body can wind up moving backward
                    // FIXME: also at some inflection point parts start appearing on the tail and the immediate neck is Scale == 1.0f?????
                    // DEBUG: approaches but never quite precisely matches
                    float alpha = Mathf.Exp(-_growthRate * VelocitySource.Speed * Time.deltaTime);
                    _growthLinkLength = linkLength + (_growthLinkLength - linkLength) * alpha;
                    UpdateScale(); // FIXME: doesn't account for suddent cuts to length
                }
            }


            // interpolates positions of all parts along the cached path of the head
            // iterating pathIndex from [Count - 1, 0] is the path head to tail
            // _snakeParts[0] is always head object, _snakeParts[1] is always tail object, all other parts are "neck" parts
            // eg: the snakeParts array is iterated as it expands like this => [0][1], then [0][2][1], then [0][3][2][1], then [0][4][3][2][1]
            // with [0] (head) being updated outside this loop
            for (int i = activeLength - 1, pathIndex = _pathWaypoints.Count - 1; i > 0 && pathIndex >= 1; --i)
            {
                float targetLinkLength = growing && i == activeLength - 1 ? _growthLinkLength : linkLength;

                // never use the 0th waypointLength because it cant provide a inter-waypoint path direction
                while (pathIndex >= 1)
                {
                    waypointLength = _pathWaypoints[pathIndex].pathLength;

                    // never use the 0th waypointLength
                    if (pathLength + waypointLength > targetLinkLength || pathIndex == 1)
                    {
                        break;
                    }

                    pathLength += waypointLength;
                    pathIndex--;
                }

                // [0,1] fraction of current waypointLength needed to match LinkLength
                float t = (targetLinkLength - pathLength) / waypointLength;

                if (pathLength + (t * waypointLength) < targetLinkLength - LINK_LENGTH_TOLERANCE)
                {
                    break;
                }

                Vector3 offset = t * (_pathWaypoints[pathIndex - 1].position - _pathWaypoints[pathIndex].position);
                _snakeParts[i].position = _pathWaypoints[pathIndex].position + offset;
                _snakeParts[i].LookAt(_snakeParts[i + 1 < activeLength ? i + 1 : 0].position);

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