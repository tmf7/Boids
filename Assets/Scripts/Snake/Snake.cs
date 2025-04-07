using System.Collections.Generic;
using UnityEngine;

namespace Freehill.SnakeLand
{
    [RequireComponent(typeof(SnakeMovement))]
    public class Snake : MonoBehaviour
    {
        // TODO: make these dynamic via a menu select
        public SnakeHead _snakeHeadPrefab;
        public Transform _snakePartPrefab;

        [SerializeField] private SnakeMovement _snakeMovement;

        private SnakeHead _snakeHead;

        public SnakeHead Head => _snakeHead;
        public SnakeMovement SnakeMovement => _snakeMovement;

        public void Init(SnakesManager snakesManager)
        {
            // DEBUG: head added first instead of having a unique function
            _snakeHead = Instantiate(_snakeHeadPrefab, transform.position, Quaternion.identity);
            _snakeHead.transform.SetParent(transform);

            // DEBUG: snake spawn points are never freed for re-use
            SpawnPoint spawnPoint = SpawnPointManager.GetRandomSpawnPoint();
            transform.position = SpawnPointManager.GetJitteredPosition(spawnPoint);
            transform.rotation = Quaternion.identity;

            _snakeMovement.Init(snakesManager, this);

            GrowBy(SnakeMovement.MIN_SNAKE_LENGTH);
        }

        private void Update()
        {
            _snakeMovement.UpdateBody();
        }

        // FIXME: don't spawn love if part hasn't started moving yet (ie still in process of growing)
        public void HitSnake(Snake hitSnake, Transform hitPart)
        {
            List<Transform> cutParts = hitSnake._snakeMovement.CutAt(hitPart);
            PickupManager.SpawnLove(cutParts);
        }

        public void HitPickup(Pickup pickup)
        {
            pickup.SetUsed();

            switch (pickup.Power)
            {
                // TODO(~): love is 1 growth, fruit is partial growth (0.3, 0.5, etc) and only grow at whole # accumulation
                case Pickup.POWER.GROW: GrowBy(1); break;
                case Pickup.POWER.BLAST_MAGNET: break;
                case Pickup.POWER.FIREBALL: break;
                case Pickup.POWER.TEMP_IMMUNITY: break;
            }
        }

        public void GrowBy(int length)
        {
            int growth = _snakeMovement.TryActivateParts(length);

            for (int i = growth; i < length; ++i)
            {
                Transform snakePart = Instantiate(_snakePartPrefab, _snakeMovement.TailPosition, Quaternion.identity);
                snakePart.transform.SetParent(transform);
                _snakeMovement.AddPart(snakePart);
            }
        }
    }
}
