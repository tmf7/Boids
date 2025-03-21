using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Freehill.SnakeLand
{
    public class SnakeMovement : MonoBehaviour
    {
        private SnakeHead _snakeHead;
        private List<Transform> _snakeParts = new List<Transform>(DEFAULT_SNAKE_CAPACITY);
        private Vector3 _totalMovement;
        private List<Vector3> _priorPositions = new List<Vector3>(DEFAULT_SNAKE_CAPACITY);
        //private Terrain _terrain;

        public const int MIN_SNAKE_LENGTH = 5;
        public const int DEFAULT_SNAKE_CAPACITY = 200;

        private const float PART_DIVISOR = 1.0f / MIN_SNAKE_LENGTH;
        private const float SCALE_MULTIPLIER = 1.5f;

        // scales from 1 @ 6 parts to ~5 @ 200 parts
        private float Scale => ActiveLength > MIN_SNAKE_LENGTH ? SCALE_MULTIPLIER * Mathf.Log(ActiveLength * PART_DIVISOR) + 1 : 1.0f;
        //private Terrain Terrain => _terrain ??= SpawnPointManager.WorldBounds.Terrain;

        /// <summary> Dynamic turning radius directly proportional to the snake's scale. </summary>
        public float TurningRadius => 0.5f * Scale;

        public SnakeHead Head => _snakeHead;
        public bool IsOwnHead(Transform part) => Head.transform == part;
        public bool IsSelf(Transform part) => _snakeParts.Contains(part);

        /// <summary> Returns the visible, active, length of the snake. </summary>
        public int ActiveLength => _snakeParts.Count(part => part.gameObject.activeSelf);

        // FIXME: grow from the neck not the tail, set a growth target and spawn **as room is available**
        // ...don't deactivate parts, just destroy (garbage collect)?
        // ...or figure out how to efficiently re-arrange the _snakeParts to insert inactive parts from the tail
        // to behind the neck...as they become active


        /// <summary> 
        /// Returns the active neck position behind the head of the snake
        /// at which new parts should be instantiated and positioned 
        /// </summary>
        public Vector3 NeckPosition => _snakeParts[1].position;

        /// <summary>
        /// Returns true if the given item is behind (not at) the given body part number along the length of the snake, with 0 being the head.
        /// Returns false if part is not part of the snake, or at/in-front-of the given part number.
        /// </summary>
        public bool IsPartBehind(Transform part, int partNumber) => _snakeParts.IndexOf(part) > partNumber;

        /// <summary>
        /// Every snake part is sperical and symmetrically scaled, 
        /// so this adds/subtracts space betweeen parts beyond Scale/radius
        /// </summary>
        public float LinkLengthOffset { get; set; } = 0.0f;

        public float LinkLength => Scale + LinkLengthOffset;

        private void UpdateScale()
        {
            foreach (Transform snakePart in _snakeParts)
            {
                snakePart.localScale = Scale * Vector3.one;
            }
        }

        /// <summary> 
        /// Returns the position in the history. Lower indexes are older positions. 
        /// Fallback waypoint is the head itself 
        /// </summary>
        public Vector3 GetWaypoint(int historyIndex)
        {
            if (historyIndex >= 0 && historyIndex < _priorPositions.Count)
            {
                return _priorPositions[historyIndex];
            }

            return transform.position;
        }

        // TODO: produce and consume head-path waypoints
        // once active tail passes a waypoint, then remove it
        // ...consider when new parts are added
        // ...consider maintaining link length

        // SOLUTION(?): cache every frame, or after some total movement
        // SOLUTION: give each part its own heading to follow the waypoint path laid by the head
        // SOLUTION(?): spawn new pieces only when there's enough waypoints to follow (spawn in motion)...or when "leader" is far enough away

        /// <summary>
        /// Accumulates movement distance until MOVEMENT_CACHE_THRESHOLD is reached, which
        /// will cache the current position. 
        /// NOTE: applying movement before or after will result in a different cached position.
        /// </summary>
        public void AddMovementHistory(Vector3 movement, float linkLengthSqr)
        {
            _totalMovement += movement;
            if (_totalMovement.sqrMagnitude > linkLengthSqr) // FIXME: base threshold on LinkLengthSqr?
            {
                _totalMovement = Vector3.zero;
                CachePosition(transform.position);
            }
        }

        /// <summary>
        /// Adds the given position at the end of the position history such that
        /// the older positions remain earlier in the history.
        /// NOTE: the oldest position is overwritten to insert the new position if the capacity is exceeded.
        /// </summary>
        private void CachePosition(Vector3 position)
        {
            if (_priorPositions.Count < _priorPositions.Capacity - 1)
            {
                _priorPositions.Add(position);
            }
            else
            {
                _priorPositions.Insert(_priorPositions.Count - 1, position);
            }
        }

        private void OnDrawGizmos()
        {
            if (Head != null)
            {
                for (int i = 1; i < _snakeParts.Count; ++i)
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireSphere(_snakeParts[i].position, LinkLength);

                    float linkLength = (_snakeParts[i - 1].position - _snakeParts[i].position).magnitude;
                    Gizmos.color = linkLength <= LinkLength ? Color.white : Color.red;
                    Gizmos.DrawLine(_snakeParts[i].position, _snakeParts[i - 1].position);
                }
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

        public void AddHead(SnakeHead head)
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

        /// <summary> 
        /// Moves the head, then cascades the body movement counter to how the head moved, nearly preserving <see cref="LinkLength"/> on flat surfaces.
        /// NOTE: does not perserve the original path traced by the head.
        /// NOTE: most stable on nearly-planar movement
        /// </summary>
        /// <param name="headMovement">Position offset to apply to the head.</param>
        public void MoveCascade(Vector3 headMovement)
        {
            // DEBUG: assumes headMovement is a vector on the XZ plane,
            // and all movement is on the XZ plane
            // account for suddent y-axis growth
            float groundOffset = /*Terrain.SampleHeight(nextPosition) +*/ (Scale * 0.5f);
            Vector3 leaderPosition = Head.transform.position;
            leaderPosition.y = groundOffset;
            Vector3 nextPosition = leaderPosition + headMovement;

            Head.transform.LookAt(nextPosition);
            Head.transform.position = nextPosition;

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

            Vector3 tailPosition = NeckPosition;

            // DEBUG: inactive parts just track the tail part to avoid visual and physics bugs when re-activated
            for (int i = activeLength; i < _snakeParts.Count; ++i)
            {
                _snakeParts[i].position = tailPosition;
            }
        }

        // TODO: reduce triangles
        // TODO: switch to mathematical animation on pickups (no bloated animator)
        // TODO: adjust lighting fidelity
        // TODO: Mesh LODs, and not rendering if off camera
        // TODO: less diverse pickups (eg: just apples), only change color and position and ==> use GPU instancing
        // TODO: compose the snake colliders differently? body under head = all one rigidbody with one composite collider
        // TODO: instead of physics collisions, just do distance checks along/on curves (composite line segment sequences)?
        /*
        public void MoveConstant(Vector3 headMovement, float speed)
        {
            // TODO: use the trail instead of projected leader points (they wiggle too much)
            // TODO: make SnakePart to cache velocities, and which SnakeHead waypoint index they're tracking (or by ref)
            // TODO: use seek steering to overshoot a waypoint
            // TODO: if dot product of velocity to last seek is negative seek next waypoint
            // TODO: ensure the head's path history is long enough to cover the length of the snake

            Vector3 nextPosition = Head.transform.position + headMovement;
            nextPosition.y = Terrain.SampleHeight(nextPosition) + (Scale * 0.5f);

            Head.transform.LookAt(nextPosition); // DEBUG: implicitly face tangent to the interpolated surface normal
            Head.transform.position = nextPosition;

            Vector3 target;
            Vector3 targetOffset;
            int activeLength = ActiveLength;
            float linkLength = LinkLength;
            float sqrLinkLength = linkLength * linkLength;

            for (int i = 1; i < activeLength; ++i)
            {
                Transform partTransform = _snakeParts[i].transform;
                target = Head.GetWaypoint();
                targetOffset = target - partTransform.position;

                // FIXME: if all parts start facing the same direction, but then the head goes the opposite
                // direction on start, then ...FollowIndex will immediately go out of bounds and just follow the head

                // FIXME: if a new waypoint is added and pushes the indexed waypoint back in the array
                // then the FollowIndex will be incorrect...maybe just give each a waypoint to ref or copy
                // ...then how is that Vector3 updated?
                while (Vector3.Dot(targetOffset, partTransform.forward) <= 0.0f)
                {
                    target = Head.GetWaypoint();
                }

                // FIXME: only move if distance from target or leader?
                // ...if target, then as soon as too close to a target it'll stop (could be useful)
                // ...if leader, then if leader squishes past then it'll stop despite the target being ahead
                // SOLUTION: only START movement after leaderOffset > linkLength, but don't STOP movement if leaderOffset < linkLength
                // ie CONTINUED movement should disregard leaderOffset
                if (targetOffset.sqrMagnitude >= sqrLinkLength)
                {
                    // move straight at/past the target, such that sampling the terrain height is (mostly) not necessary
                    partTransform.LookAt(target);
                    Vector3 partVelocity = _snakeParts[i].transform.forward * speed;
                    partTransform.position += partVelocity * Time.deltaTime;
                }
            }

            Vector3 tailPosition = TailPosition;

            // DEBUG: inactive parts just track the tail part to avoid visual and physics bugs when re-activated
            for (int i = activeLength; i < _snakeParts.Count; ++i)
            {
                _snakeParts[i].transform.position = tailPosition;
            }
        }
        */
    }
}
