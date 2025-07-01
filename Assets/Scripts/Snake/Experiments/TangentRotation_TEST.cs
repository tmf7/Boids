using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEngine;

public class TangentRotation_TEST : MonoBehaviour
{
    [SerializeField] private Transform _hit; // treat its forward as the hit normal

    private void Start()
    {
        // rotate this transform to put its forward axis on the hit plane
        // DO NOT align this transform.forward with hitTangent

        // using gravity for the cross guarantees lateral motion to the slideDir
        // but more importantly even if the hit normal is directly opposed to moveDir, then the tangent remains non-zero
        // ...with the risk of hit normal being aligned with gravity instead
        // SOLUTION: cross of hit normal and ANY vector is a vector in the plane
        // ...so jumble the hit normal to always get a non-aligned vector
        Vector3 GRAVITY_DIR_UP = -Physics.gravity.normalized;
        Vector3 hitTangent = Vector3.Cross(GRAVITY_DIR_UP, _hit.forward);

        // NOTE: this is not the solution to projecting moveDir on the hit plane cleanly
        //float angle = Vector3.Angle(hitTangent, transform.forward);
        //transform.forward = Quaternion.AngleAxis(angle, hitTangent) * transform.forward;
    }

    private void OnDrawGizmos()
    {
        // ALL are vectors in the hit normal plane
        // GOAL: rotate moveDir into the plane while mostly preserving its direction (like plane projection)
        // PROBLEM: what is the axis of rotation? and what is the angle that would put moveDir in the plane?

        // always perpendicular to gravity (lateral push), breaks if normal is aligned with gravity
        //Vector3 GRAVITY_DIR_UP = -Physics.gravity.normalized;
        //Vector3 gravityHitTangent = Vector3.Cross(GRAVITY_DIR_UP, _hit.forward);
        //float angle = Vector3.Angle(transform.forward, gravityHitTangent);
        //Vector3 rotationAxis = Vector3.Cross(transform.forward, gravityHitTangent);
        //Vector3 slideDir = Quaternion.AngleAxis(angle, rotationAxis) * transform.forward;
        //Gizmos.color = Color.yellow;
        //Gizmos.DrawRay(_hit.position, gravityHitTangent);
        //Gizmos.color = Color.red;
        //Gizmos.DrawRay(_hit.position, rotationAxis);
        //Gizmos.color = Color.cyan;
        //Gizmos.DrawRay(_hit.position, slideDir);

        //// semi-arbitrary tangent, breaks if moveDir is aligned with normal
        //Vector3 moveHitTangent = Vector3.Cross(transform.forward, _hit.forward);
        //Gizmos.color = Color.red;
        //Gizmos.DrawRay(_hit.position, moveHitTangent);

        // cleanest projection, frequently breaks when moveDir is aligned with normal
        Vector3 projMove = Vector3.ProjectOnPlane(transform.forward, _hit.forward);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(_hit.position, projMove);

        //// truly random, rarest chance to break, but can break if random is aligned with normal
        //Vector3 randomTangent = Vector3.Cross(Random.onUnitSphere, _hit.forward);
        //Gizmos.color = Color.cyan;
        //Gizmos.DrawRay(_hit.position, randomTangent);

        // assumes moveDir is [0,90] degrees to the hit normal, if outside that (even at 90)
        // then moveDir is moving away from the hit anyway.
        // therefore this is the angle that rotates moveDir down into the hit normal plane

        // FIXME: the only axis this angle works for to put in-plane is the cross of these two
        // ...which if aligned is 0..but so is the angle...which then becomes 90 degrees...but which way? (back to square one)
        // ...NOTE: at alignment with normal the moveDir is just returned (no exception at least)
        // RESULT: this is cleaner than projection because no magnitude of moveDir is lost
        // and only has one failure case...which projection also has
        // but also more calculations (no sqrt, but cos)
        //float planeAngle = 90.0f - Vector3.Angle(transform.forward, -_hit.forward); 
        //Vector3 rotationAxis = Vector3.Cross(-_hit.forward, transform.forward);
        //Vector3 slideDir = Quaternion.AngleAxis(planeAngle, rotationAxis) * transform.forward;
        //Gizmos.color = Color.red;
        //Gizmos.DrawRay(_hit.position, slideDir);
        //Gizmos.color = Color.cyan;
        //Gizmos.DrawRay(_hit.position, rotationAxis);

        // rotate transform.up to align with hit.forward, not nearly as clean as proj, but doesn't break
        Vector3 selfAxis = transform.up;
        Quaternion rotation = Quaternion.FromToRotation(selfAxis, _hit.forward);
        Vector3 slideDir = rotation * transform.forward;

        if (Vector3.Dot(slideDir, transform.forward) < 0.0f)
        {
            rotation = Quaternion.FromToRotation(-selfAxis, _hit.forward);
            slideDir = rotation * transform.forward;
            //slideDir = -slideDir; // no
        }

        Gizmos.color = Color.red;
        Gizmos.DrawRay(_hit.position, slideDir);
    }
}
