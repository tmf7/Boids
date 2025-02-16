using UnityEngine;

namespace Freehill.SnakeLand
{
    // TODO: handle snake growth, death, and other coordination
    [RequireComponent(typeof(SnakeMovement))]
    public class Snake : MonoBehaviour
    {
        public Transform _snakeHeadPrefab;
        public Transform _snakePartPrefab;
        public int _testSnakeLength = 2;

        [SerializeField] private VelocitySource _velocitySource; // PlayerMovment or BoidMovement

        private SnakeMovement _snakeMovement;

        private void Start()
        {
            _snakeMovement = GetComponent<SnakeMovement>();
            _snakeMovement.SetTerrain(FindAnyObjectByType<Terrain>());

            Transform head = Instantiate(_snakeHeadPrefab, transform.position, Quaternion.identity);
            head.SetParent(transform);
            _snakeMovement.AddPart(head);

            for (int i = 0; i < _testSnakeLength; ++i)
            {
                Transform snakePart = Instantiate(_snakePartPrefab, transform.position, Quaternion.identity);
                snakePart.SetParent(transform);
                _snakeMovement.AddPart(snakePart);
            }

            // ensure the player camera follows the player's head
            if (_velocitySource is PlayerMovement) 
            {
                ((PlayerMovement)_velocitySource).SetCameraConstraintSource(head);
            }
        }

        private void Update()
        {
            Vector3 headMovement = _velocitySource.Velocity * Time.deltaTime;
            _snakeMovement.MoveBody(headMovement);
        }
    }
}
