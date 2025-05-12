using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Freehill.SnakeLand
{
    [RequireComponent(typeof(SnakeMovement))]
    public class Snake : MonoBehaviour
    {
        // TODO: make these dynamic via a menu select
        public SnakeHead _snakeHeadPrefab;
        public Transform _snakePartPrefab;
        public Transform _snakeTailPrefab;

        [SerializeField] private SnakeMovement _snakeMovement;

        public SnakeMovement SnakeMovement => _snakeMovement;
        public Vector3 HeadPosition => _snakeMovement.HeadPosition;

        private static List<int> _lengths = new List<int>(50);
        private static int _availableLengthIndex = 0;
        private static Random rand = new Random();
        static Snake()
        {
            _lengths.Clear();
            for (int i = 0; i < 47; i++)
            {
                _lengths.Add(rand.Next(0, 16));
            }
            _lengths.Add(200);
            _lengths.Add(200);
            _lengths.Add(200);
            _availableLengthIndex = 0;
            Debug.Log("Snake Static CTOR Called");
        }

        private static int GetLength()
        {
            return _lengths[_availableLengthIndex++];
        }

        public void Init(SnakesManager snakesManager)
        {
            // DEBUG: snake spawn points are never freed for re-use
            SpawnPoint spawnPoint = SpawnPointManager.GetRandomSpawnPoint();
            transform.position = SpawnPointManager.GetJitteredPosition(spawnPoint);
            transform.rotation = Quaternion.identity;

            SnakeHead snakeHead = Instantiate(_snakeHeadPrefab, transform.position, Quaternion.identity);
            snakeHead.transform.SetParent(transform);

            Transform snakeTail = Instantiate(_snakeTailPrefab, transform.position, Quaternion.identity);
            snakeTail.transform.SetParent(transform);

            _snakeMovement.Init(snakesManager, this, snakeHead, snakeTail);
            _snakeMovement.AddToTargetLength(20);// GetLength());
        }

        private void Update()
        {
            _snakeMovement.UpdateBody();
        }

        public void HitSnake(Snake hitSnake, Transform hitPart)
        {
            bool tryKill = false;

            // determine attack intention
            if (hitSnake != this)
            {
                // FIXME: null ref _snakeMovement from Kill calls any given frame
                Vector3 selfFacing = _snakeMovement.VelocitySource.CurrentFacing;
                Vector3 selfCollisionFacing = (hitSnake._snakeMovement.HeadPosition - _snakeMovement.HeadPosition).normalized;
                bool isSelfAttacking = Vector3.Dot(selfFacing, selfCollisionFacing) > 0.0f; // somewhat intentional collision

                Vector3 otherFacing = hitSnake._snakeMovement.VelocitySource.CurrentFacing;
                Vector3 otherCollisionFacing = -selfCollisionFacing;
                bool isOtherAttacking = Vector3.Dot(otherFacing, otherCollisionFacing) > 0.0f; // somewhat intentional collision

                // tie-breaker stat
                bool isSelfBigger = _snakeMovement.ActiveLength > hitSnake._snakeMovement.ActiveLength;

                // DEBUG: if neither is attacking then snakes pass through eachother
                // (eg: zero-value collisionFacing vectors, or grazing collision with both facing away)
                tryKill = isSelfAttacking && (!isOtherAttacking || isSelfBigger);
            }

            List<Vector3> cutPartPositions = hitSnake._snakeMovement.CutAt(hitPart, tryKill);
            PickupManager.SpawnLove(cutPartPositions);
        }

        public void Kill() 
        {
            // FIXME: null refs as snake is dying from this
            _snakeMovement.Kill();
            _snakeMovement = null;
            Destroy(gameObject);
        }

        public void HitPickup(Pickup pickup)
        {
            pickup.SetUsed();

            switch (pickup.Power)
            {
                // TODO(~): love is 1 growth, fruit is partial growth (0.3, 0.5, etc) and only grow at whole # accumulation
                case Pickup.POWER.GROW: _snakeMovement.AddToTargetLength(1); break;
                case Pickup.POWER.BLAST_MAGNET: break;
                case Pickup.POWER.FIREBALL: break;
                case Pickup.POWER.TEMP_IMMUNITY: break;
            }
        }

        public void Grow()
        {
            if (!_snakeMovement.TryActivatePart())
            { 
                Transform snakePart = Instantiate(_snakePartPrefab, _snakeMovement.HeadPosition, Quaternion.identity);
                snakePart.transform.SetParent(transform);
                _snakeMovement.AddPart(snakePart);
            }
        }
    }
}
