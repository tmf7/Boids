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

        [SerializeField] private SnakeMovement _snakeMovement;

        private SnakeHead _snakeHead;

        public SnakeHead Head => _snakeHead;
        public SnakeMovement SnakeMovement => _snakeMovement;

        //private static List<int> _lengths = new List<int>(50);
        //private static int _availableLengthIndex = 0;
        //private static Random rand = new Random();
        //static Snake()
        //{
        //    _lengths.Clear();
        //    for (int i = 0; i < 47; i++)
        //    {
        //        _lengths.Add(rand.Next(0, 16));
        //    }
        //    _lengths.Add(200);
        //    _lengths.Add(200);
        //    _lengths.Add(200);
        //    _availableLengthIndex = 0;
        //    Debug.Log("Snake Static CTOR Called");
        //}

        //private static int GetLength()
        //{
        //    return _lengths[_availableLengthIndex++];
        //}

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

            // 50 snakes @ 25 length for ~60fps for quad terrain
            // 50 snakes @ 20 length for ~60fps for terrain
            // SOLUTION(~): compute shader snake movement for all snakes at once? (would that be possible in unreal? on a smartphone?)
            // SOLUTION(simple): 1000 snake parts (50 of which are heads doing sqrt and terrain sample each frame)
            // just assume most are short (<20 length) and a rare few (~3 snakes) are long (200 length)
            // ...basic static ctor test bears that out ~60fps with some lag spikes probably from garbage collection
            // ...also will be more perfomant in a build (not in editor)
            // SOLUTION: 50 @ 105 length for ~30fps quad terrain when ONLY commenting AIMovement.FixedUpdate and OnTriggerStay
            // ...left SnakeHead trigger events, head rigidbodies all in place
            // ...so consider that logic of pickups, heads, and parts awareness (need it be physics? maybe just math?)
            GrowBy(SnakeMovement.MIN_SNAKE_LENGTH);
        }

        private void Update()
        {
            _snakeMovement.UpdateBody();
        }

        // FIXME: don't spawn love if part hasn't started moving yet (ie still in process of growing)
        public void HitSnake(Snake hitSnake, Transform hitPart)
        {
           // List<Transform> cutParts = hitSnake._snakeMovement.CutAt(hitPart);
           // PickupManager.SpawnLove(cutParts);
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

        // FIXME: make this Grow(), which only ever grows by 1, call it from SnakeMovement, give SnakeMovement a target ActiveLength
        // such that SnakeMovement will activate as path is available, and if any further length is needed then Grow() is called
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
