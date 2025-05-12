using UnityEngine;

namespace Freehill.SnakeLand
{
    public class SnakeHead : SnakePart
    {
        private Snake _owner;
        private Snake Owner => _owner ??= GetComponentInParent<Snake>();

        private void OnTriggerEnter(Collider other)
        {
            if (!other.gameObject.activeSelf)
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
    }
}
