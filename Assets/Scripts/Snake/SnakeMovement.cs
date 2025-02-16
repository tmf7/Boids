using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Freehill.SnakeLand
{
    // TODO: react to collisions between body and head (not body and body), and head + head (not in this class)
    // include collision between head an own body? hard mode?
    // TODO: use events to which Snake can subscribe (deathHit, foodHit)
    public class SnakeMovement : MonoBehaviour
    {
        private List<Transform> _snakeParts = new List<Transform>();
        private Terrain _terrain;
        private float _partScale = 1.0f;

        public Transform Head => _snakeParts.FirstOrDefault();

        /// <summary>
        /// Every snake part is sperical and symmetrically scaled, 
        /// so this adds/subtracts space betweeen parts beyond Scale/radius
        /// </summary>
        public float LinkLengthOffset { get; set; }

        public float LinkLength => Scale + LinkLengthOffset;

        public float Scale
        {
            get => _partScale;
            set
            { 
                _partScale = value;

                foreach (Transform snakePart in _snakeParts) 
                {
                    snakePart.localScale = _partScale * Vector3.one;    
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (Head != null)
            {
                for (int i = 1; i < _snakeParts.Count; ++i)
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireSphere(_snakeParts[i].position, LinkLength);

                    float linkLength = (_snakeParts[i - 1].position - _snakeParts[i].position).magnitude;
                    Gizmos.color = linkLength <= LinkLength ? Color.white : Color.red;
                    Gizmos.DrawLine(_snakeParts[i].position, _snakeParts[i - 1].position);
                }
            }
        }

        public void SetTerrain(Terrain terrain)
        { 
            _terrain = terrain;
        }

        /// <summary> Adds a single part to the tail of the snake (or adds the head if there's no body) </summary>
        /// <param name="newPart"></param>
        public void AddPart(Transform newPart)
        {
            if (!_snakeParts.Contains(newPart))
            {
                _snakeParts.Add(newPart);
            }
        }

        /// <summary> Adds given parts to the tail of the snake in the given order </summary>
        public void AddParts(IEnumerable<Transform> newParts)
        {
            foreach (Transform part in newParts)
            { 
                AddPart(part);
            }
        }

        /// <summary> Removes all parts at and after the given part, and returns all removed parts in tail-to-cut order. </summary>
        /// <param name="firstCutPart"></param>
        public List<Transform> CutAt(Transform firstCutPart)
        { 
            var cutParts = new List<Transform>();

            int firstCutIndex = _snakeParts.IndexOf(firstCutPart);

            if (firstCutIndex >= 0)
            {
                for (int i = _snakeParts.Count - 1; i >= firstCutIndex; --i)
                {
                    cutParts.Add(_snakeParts[i]);
                    _snakeParts.RemoveAt(i);
                }
            }

            return cutParts;
        }

        /// <summary> Moves the head, then cascades the body movement according to how the head moved, preserving <see cref="LinkLength"/>. </summary>
        /// <param name="headMovement">Position offset to apply to the head.</param>
        public void MoveBody(Vector3 headMovement)
        {
            Vector3 nextPosition = Head.position + headMovement;
            nextPosition.y = _terrain.SampleHeight(nextPosition) + (Scale * 0.5f);

            Head.LookAt(nextPosition);
            Head.position = nextPosition;

            Vector3 link;
            Vector3 linkDir;
            Vector3 axialLinkCatchUp; // move enough to get link.magnitude back to LinkLength
            Vector3 jointMovement = headMovement;

            for (int i = 1; i < _snakeParts.Count; ++i)
            {
                // to perserve link lengths and only allow axial movement along a link,
                // jointMovement perpendicular to the joint is negated and added to the axial movement to keep the whole snake in motion
                link = _snakeParts[i - 1].position - _snakeParts[i].position;

                // allows joints to squish/move past eachother
                if (link.magnitude >= LinkLength)
                {
                    // FIXME(?): ensure axialLinkCatchUp is setup to ensure the final link is LinkLength (maybe calc after perpJointPull offset?)
                    linkDir = link.normalized;
                    axialLinkCatchUp = link - (linkDir * LinkLength);
                    Vector3 axialJointPull = Vector3.Project(jointMovement, linkDir);
                    Vector3 perpJointPull = jointMovement - axialJointPull;
                    jointMovement = axialLinkCatchUp - perpJointPull;

                    nextPosition = _snakeParts[i].position + jointMovement;
                    nextPosition.y = _terrain.SampleHeight(nextPosition) + (Scale * 0.5f);

                    _snakeParts[i].position = nextPosition;
                    _snakeParts[i].LookAt(_snakeParts[i - 1]);
                }
            }
        }
    }
}
