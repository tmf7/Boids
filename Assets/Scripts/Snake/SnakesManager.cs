using Freehill.Boids;
using System.Collections.Generic;
using UnityEngine;

namespace Freehill.SnakeLand
{
    public class SnakesManager : MonoBehaviour
    {
        // TODO:
        // [] spawn multiple snakes at random points on the map (away from other snakes by some radius)
        // [] respond to individual snake collisions (cut snakes and turn their parts into MEAT food, grow snakes that eat food, give snakes powers)
        // [] spawn food prefabs (and spawn more after some time)
        // [] spawn power-up prefabs (and spawn more after some time)
        // [] snake facing UI arrow, on-screen UI for move, zoom camera, and rotate camera (per keaton)

        // OTHER TODO:
        // [] let player choose snake body prefab, and head prefab, then spawn THAT
        // [] let player choose ground and terrain
        // [] sprint UI button (and other power buttons, stacking hexes)
        // [] ai sprint (and other powers) usage (random intervals? proximity?)
        // [] give snakes unique names on spawn
        // [] let player name own snake
        // [] ui for top 5 (and player rank if > 5)

        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private List<GameObject> _aiSnakePrefabs = new List<GameObject>();

        [SerializeField] private int _aiSnakeSpawns = 50;
        [SerializeField][Min(0.0f)] private float _pickupSeekWeight;
        [SerializeField][Min(0.0f)] private float _wanderWeight;
        [SerializeField][Range(0.0f, 180.0f)] private float _wanderErraticness;
        [SerializeField][Min(0.0f)] private float _snakePartEvadeWeight;
        [SerializeField][Min(0.0f)] private float _snakeHeadPursueWeight;
        [SerializeField][Min(0.0f)] private float _snakeHeadEvadeWeight;
        [SerializeField][Min(0.0f)] private float _boundaryPushWeight;
        [SerializeField][Min(0.0f)] private float _boundsProximityRadius;

        public float PickupSeekWeight => _pickupSeekWeight;
        public float WanderWeight => _wanderWeight;
        public float WanderErraticness => _wanderErraticness;
        public float SnakePartEvadeWeight => _snakePartEvadeWeight;
        public float SnakeHeadPursueWeight => _snakeHeadPursueWeight;
        public float SnakeHeadEvadeWeight => _snakeHeadEvadeWeight;
        public float BoundaryPushWeight => _boundaryPushWeight;
        public float BoundsProximityRadius => _boundsProximityRadius;

        private List<Snake> _spawnedSnakeAIs = new List<Snake>();

        private void Start()
        {
            GameObject player = Instantiate(_playerPrefab);
            player.GetComponentInChildren<Snake>().Init(this);

            for (int i = 0; i < _aiSnakeSpawns; ++i)
            {
                GameObject aiSnakeGO = Instantiate(_aiSnakePrefabs[Random.Range(0, _aiSnakePrefabs.Count)]);
                aiSnakeGO.name += $" ({i})";
                Snake aiSnake = aiSnakeGO.GetComponent<Snake>();
                aiSnake.Init(this);
                _spawnedSnakeAIs.Add(aiSnake);
            }
        }

        public void Kill(Snake snake)
        {
            snake.Kill();
        }

        public void DestroyAISnakes()
        {
            for (int i = _spawnedSnakeAIs.Count - 1; i >= 0; --i)
            {
                _spawnedSnakeAIs[i].Kill();
                Destroy(_spawnedSnakeAIs[i].gameObject);
            }
            _spawnedSnakeAIs.Clear();
        }
    }
}
