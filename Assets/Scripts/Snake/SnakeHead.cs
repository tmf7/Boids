using UnityEngine;

namespace Freehill.SnakeLand
{
    public class SnakeHead : SnakePart
    {
        private Snake _owner;
        private bool _initialized = false;

        private Snake Owner => _owner ??= GetComponentInParent<Snake>();

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
