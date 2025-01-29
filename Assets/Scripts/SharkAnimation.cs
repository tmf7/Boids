using System.Collections.Generic;
using UnityEngine;

namespace Freehill.Boids
{
    public class SharkAnimation : MonoBehaviour
    {
        // TODO: trigger mouth open/close once only lower jaw)
        private struct LocalBonePose
        {
            public Transform bone;
            public Vector3 boneDirection;
            public Quaternion initialRotation;
            public Vector3 rotationAxis;
            public Vector3 offsetAxis; // rotates the bone body around the rotationAxis in the rotationAxis plane

            public bool translateOnOffsetAxis;
            public Vector3 initialWorldOffsetAxis;
            public Vector3 initialPosition;

            /// <summary> Sets the bone localRotation around the rotationAxis in the bone's local space </summary>
            /// <param name="offset"> How far along the offsetAxis to push the boneDirection </param>
            public void SetRotation(float offset)
            {
                const float TRANSLATING_ROTATION_DAMPING = 0.33f;

                // push the tip of the bone along the bone's up-axis plane
                Vector3 offsetVector = offset * offsetAxis * (translateOnOffsetAxis ? TRANSLATING_ROTATION_DAMPING : 1.0f);
                Vector3 rotationVector = offsetVector + boneDirection; // define the new bone body vector
                bone.localRotation = initialRotation * Quaternion.FromToRotation(boneDirection, rotationVector);
            }

            /// <summary> Sets the bone localPosition along the initial offsetAxis in the bone parent's local space </summary>
            /// <param name="offset"> How far along the worldOffsetAxis to push the bone position </param>
            public void SetPosition(float offset)
            {
                const float TRANSLATION_DAMPING = 0.1f;

                if (translateOnOffsetAxis)
                {
                    // subtract to translate opposite to the rotation as if the body is pushing off something
                    bone.localPosition = initialPosition - bone.parent.InverseTransformVector(initialWorldOffsetAxis) * offset * TRANSLATION_DAMPING;
                }
            }
        }

        [SerializeField] private float _frequency = 1.0f;
        [SerializeField] private float _amplitude = 1.0f;
        [SerializeField] private float _perBonePhaseShiftDegrees = 10.0f;
        [SerializeField] private Transform _body2; // do not rotate, only translate on its forward axis
        [SerializeField] private Transform _body1;
        [SerializeField] private Transform _neck;
        [SerializeField] private Transform _topJaw;
        [SerializeField] private Transform _tail1;
        [SerializeField] private Transform _tail2;
        [SerializeField] private Transform _tail3;
        [SerializeField] private Transform _tail4;

        private List<LocalBonePose> _bonePoses = new List<LocalBonePose>();

        private void Awake()
        {
            // the particular shark armature used here has bones
            // such that rotating directly around one local axis keeps the shark mesh
            // very nearly on a plane defined by the root bone world up axis
            //AddBoneMotionVectors(_topJaw);
            //AddBoneMotionVectors(_neck);
            //AddBoneMotionVectors(_body1);

            // DEBUG: adding the bones in this specific order yields the best visual of a sine wave moving down the shark body
            AddBoneMotionVectors(_tail1);
            AddBoneMotionVectors(_tail2);
            AddBoneMotionVectors(_tail3);
            AddBoneMotionVectors(_tail4);
            AddBoneMotionVectors(_body2, true);
        }

        /// <summary> 
        /// Returns the axis of the transform most aligned with the up-axis of the root transform of this object, 
        /// such that it will act as the rotation axis
        /// </summary>
        private void AddBoneMotionVectors(Transform bone, bool translateOnOffsetAxis = false)
        {
            float nearestAxisAlignment = 0.0f;
            Vector3 boneUpAxis = Vector3.up;

            // PERF: slower than a single list that's cleared on each call, but only occurs once at startup so minmal benefit to refactor
            var axes = new List<Vector3> { bone.up, -bone.up, bone.forward, -bone.forward, bone.right, -bone.right };

            foreach (var axis in axes) 
            {
                // DEBUG: if the model up axis changes relative to the root up axis this needs to update
                float axisAlignment = Vector3.Dot(transform.up, axis);
                if (axisAlignment > nearestAxisAlignment)
                {
                    nearestAxisAlignment = axisAlignment;
                    boneUpAxis = axis; 
                }
            }

            boneUpAxis = bone.InverseTransformDirection(boneUpAxis);

            // DEBUG: blender bones y-axis is along the bone, so that's what unity imports for the local transforms
            Vector3 boneDirection = bone.InverseTransformDirection(bone.up);
            Vector3 boneOffsetAxis = Vector3.Cross(boneDirection, boneUpAxis).normalized;
            
            // ensure all offset axes point in the same direction (right or left of the shark body)
            if (_bonePoses.Count > 0) 
            {
                var firstRigMotionVector = _bonePoses[0];

                if (Vector3.Dot(bone.TransformDirection(boneOffsetAxis), firstRigMotionVector.bone.TransformDirection(firstRigMotionVector.offsetAxis)) < 0.0f)
                {
                    boneOffsetAxis *= -1.0f;
                }
            }

            _bonePoses.Add(new LocalBonePose 
            { 
                bone = bone,
                boneDirection = boneDirection,
                initialRotation = bone.localRotation,
                rotationAxis = boneUpAxis,
                offsetAxis = boneOffsetAxis,

                translateOnOffsetAxis = translateOnOffsetAxis,
                initialPosition = bone.localPosition,
                initialWorldOffsetAxis = bone.TransformVector(boneOffsetAxis)
            });
        }

        // creates vector offsets that dictate how each bone rotates (not translates) with various phase angle offsets
        private void Update()
        {
            float boneRotationDegrees = _frequency * Mathf.Rad2Deg * Time.time;
            float phaseShiftDegrees = 0.0f;

            foreach (LocalBonePose bonePose in _bonePoses) 
            { 
                float offset = _amplitude * Mathf.Sin(Mathf.Deg2Rad * (boneRotationDegrees - phaseShiftDegrees)); // [-_amplitude to _amplitude] on repeat
                bonePose.SetRotation(offset);
                bonePose.SetPosition(offset);
                phaseShiftDegrees += _perBonePhaseShiftDegrees;
            }
        }

        private void OnDrawGizmos()
        {
            if (_bonePoses.Count == 0)
            {
                return;
            }

            float boneRotationDegrees = _frequency * Mathf.Rad2Deg * Time.time;
            float phaseShiftDegrees = 0.0f;

            foreach (var bonePose in _bonePoses)
            {
                float offset = _amplitude * Mathf.Sin(Mathf.Deg2Rad * (boneRotationDegrees - phaseShiftDegrees)); // [-_amplitude to _amplitude] on repeat
                Vector3 offsetVector = offset * bonePose.offsetAxis; // push the tip of the bone along the bone's up-axis plane
                Vector3 rotationVector = offsetVector + bonePose.boneDirection; // define the new bone body vector

                Gizmos.color = Color.red;
                Gizmos.DrawLine(bonePose.bone.position, bonePose.bone.position + bonePose.bone.TransformDirection(bonePose.boneDirection));

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(bonePose.bone.position, bonePose.bone.position + bonePose.bone.TransformDirection(bonePose.rotationAxis) * 3.0f);

                Gizmos.color = Color.blue;
                Gizmos.DrawLine(bonePose.bone.position, bonePose.bone.position + bonePose.bone.TransformDirection(bonePose.offsetAxis) * 3.0f);

                Gizmos.color = Color.green;
                Gizmos.DrawLine(bonePose.bone.position, bonePose.bone.position + bonePose.bone.TransformDirection(rotationVector));
                phaseShiftDegrees += _perBonePhaseShiftDegrees;
            }
        }
    }
}