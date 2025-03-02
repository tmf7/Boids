using UnityEngine;

namespace Freehill.SnakeLand
{
    public abstract class VelocitySource : MonoBehaviour
    {
        public abstract Vector3 Facing { get; }
        public abstract float Speed { get; }
    }
}
