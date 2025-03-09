using System.Collections.Generic;
using UnityEngine;

namespace Freehill.SnakeLand
{
    public class SnakeHead : SnakePart
    {
        private Snake _owner;
        private bool _initialized = false;

        private Snake Owner => _owner ??= GetComponentInParent<Snake>();

        private List<Vector3> _priorPositions = new List<Vector3>(SnakeMovement.DEFAULT_SNAKE_CAPACITY);

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

        private Vector3 _totalMovement;

        // FIXME: make this proportional to the #waypoints between links and linkLength
        private const float MOVEMENT_CACHE_THRESHOLD = 1.0f;

        /// <summary>
        /// Interpolates current towards target using an exponential decay curve. 
        /// Current is renormalized a magnitude of speed.
        /// </summary>
        /// <param name="turningRadius">How quick the turn should be from current to target</param>
        /// <param name="speed">Magnitude of the velocity (assumes constant speed from current to target)</param>
        private void SmoothLerp(ref Vector3 current, Vector3 target, float turningRadius, float speed)
        {
            const float TUNING_FRACTION = 0.7f;
            float decay = speed / (TUNING_FRACTION * turningRadius);

            // DEBUG: do not use slerp or lerp in place of the base formula: a = b + (a - b)t
            // because it reverses the intended non-linear recurrent lerp smoothing back to x = a + (b - a)t
            float alpha = Mathf.Exp(-decay * Time.deltaTime);
            current = (target + (current - target) * alpha).normalized * speed;
        }

        // FIXME: just cache every position every frame for every SnakePart, up to a total sqr path length of linkLenthSqr
        // and have each subsequent part START moving (but not stop) once there's enough length
        // ...original snake cached new snake head pos and popped tail pos, then rendered everything with new pos list
        // but variable frame-rate and continuous motion will pevent lock-step ring shuffle...unless they move on fixedupdate?
        // SOLUTION(?): give snake turning radius, no perfect 180 turns
        // (rivals seems to have small but non-zero turning radius proportional to maybe half the radius of the snake)
        // ...have the heading lerp over time to where the player sets the heading (rivals does this, the arrow moves before the head by a bit)
        // SOLUTION: give each part its own heading, or just the head

        /// <summary>
        /// Accumulates movement distance until MOVEMENT_CACHE_THRESHOLD is reached, which
        /// will cache the current position. 
        /// NOTE: applying movement before or after will result in a different cached position.
        /// </summary>
        public void AddMovementHistory(Vector3 movement)
        {
            _totalMovement += movement;
            if (_totalMovement.sqrMagnitude > MOVEMENT_CACHE_THRESHOLD) 
            {
                _totalMovement = Vector3.zero;
                CachePosition(transform.position);
            }
        }

        // TODO: produce and consume waypoints
        // once ALL active parts have consumed a waypoint, then List.Remove/RemoveAt it
        // ...should be safe no matter when new parts are added
        // SOLUTION:
        // (1) add waypoint w/num parts to consume
        // (2) decrement on every GetWaypoint call until 0
        // (3) remove on 0
        // SOLUTION: each part tracks 1 index (1 is 0, 2 is 1, etc), and a new part doesn't move until theres that many waypoints
        // ...if this is the solution, then use a ref instead of copy for a Vec3
        // PROBLEM: how does that ensure linkLength if waypoints are sub-linkLength? 
        // PROBLEM: the head may move such that two waypoints are sub-linkLength
        // SOLUTION: ???

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

        private void OnTriggerEnter(Collider other)
        {
            if (!_initialized || !other.gameObject.activeSelf)
            {
                return;
            }

            var hitSnake = other.transform.parent?.GetComponent<Snake>();
            var hitPickup = other.GetComponent<Pickup>();

            if (hitSnake != null)
            {
                Owner.HitSnake(hitSnake, other.transform);
            }
            else if (hitPickup != null)
            {
                Owner.HitPickup(hitPickup);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // when spawning, ignore collisions until away from own body
            if (_initialized || !other.gameObject.activeSelf)
            {
                return;
            }

            var exitSnake = other.transform.parent?.GetComponent<SnakeMovement>();

            if (exitSnake != null && exitSnake.IsOwnHead(transform))
            {
                _initialized = true;
            }
        }
    }
}
