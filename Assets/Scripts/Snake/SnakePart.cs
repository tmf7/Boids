using UnityEngine;

namespace Freehill.SnakeLand
{
    public class SnakePart : MonoBehaviour
    {
        // FIXME: set this externally, and use it exernally
        // SOLUTION: don't cache this, just have partIndex == followIndex, eg: 1st part follows 0th waypoint ALWAYS,
        // which ideally is replaced before part moves past it (problematic for edge cases)
        // FIXME: ensure theres always enough waypoints...worst case use head itself as fallback?

        /// <summary> Worldspace position this will move toward </summary>
        public Vector3 MoveTarget; 
    }
}
