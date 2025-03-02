using System.Collections.Generic;
using UnityEngine;

namespace Freehill.SnakeLand
{
    [RequireComponent(typeof(SnakeMovement))]
    public class Snake : MonoBehaviour
    {
        public SnakeHead _snakeHeadPrefab;
        public Transform _snakePartPrefab;

        [SerializeField] private VelocitySource _velocitySource; // PlayerMovment or BoidMovement

        private SnakeMovement _snakeMovement;
        private SnakeHead _snakeHead;
        private SpawnPoint _spawnPoint;

        public SnakeHead Head => _snakeHead;
        public SnakeMovement SnakeMovement => _snakeMovement;

        private void Awake()
        {
            _snakeMovement = GetComponent<SnakeMovement>();

            // DEBUG: head added first instead of having a unique function
            _snakeHead = Instantiate(_snakeHeadPrefab, transform.position, Quaternion.identity);
            _snakeHead.transform.SetParent(transform);
            _snakeMovement.AddHead(_snakeHead);
        }

        // DEBUG: Call this in Start to allow PlayerMovement to initialize CameraPositionConstraint
        public void Init(SnakesManager snakesManager)
        {
            _spawnPoint = SpawnPointManager.GetRandomSpawnPoint();
            transform.position = SpawnPointManager.GetJitteredPosition(_spawnPoint);
            transform.rotation = Quaternion.identity;

            if (_velocitySource is PlayerMovement)
            {
                // ensure the player camera follows the player's head
                ((PlayerMovement)_velocitySource).SetCameraConstraintSource(_snakeHead.transform);
            }
            else if (_velocitySource is AIMovement)
            {
                ((AIMovement)_velocitySource).Init(snakesManager, this);
            }

            GrowBy(SnakeMovement.MIN_SNAKE_LENGTH + 100);
        }

        private void Update()
        {
            Vector3 headMovement = _velocitySource.Facing * _velocitySource.Speed * Time.deltaTime;
            //_snakeHead.AddMovementHistory(headMovement);
            _snakeMovement.MoveCascade(headMovement);

            if (_spawnPoint != null && _velocitySource.Facing !=  Vector3.zero) 
            { 
                SpawnPointManager.FreeSpawnPoint(_spawnPoint);
                _spawnPoint = null;
            }
        }

        // FIXME: love picked up as soon as its spawned if running over self
        // ...also an activation gap is being left which is messing with the ActiveLength calculation
        public void HitSnake(Snake hitSnake, Transform hitPart)
        {
            List<Transform> cutParts = hitSnake._snakeMovement.CutAt(hitPart);
            PickupManager.SpawnLove(cutParts);

            // FIXME: the hitSnake Head's history MAY need to remove older waypoints and shift data
            // ...unless each snake part is steering fine using the array as-is reglardless of cuts
            // ...List.Inserts at capacity will push out the cut data over time anyway
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

            //const int INTER_JOINT_WAYPOINTS = 2;
            //_snakeHead.ExpandHistory(_snakeMovement.ActiveLength * INTER_JOINT_WAYPOINTS);
        }
    }
}
