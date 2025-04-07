using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VelocityTurningRadius_TEST : MonoBehaviour
{
    public enum HeadingTurnMethod : int
    { 
        DECAY_ASYMPTOTE,
        CENTRIPETAL,
        ROTATION,
        CIRCLE_SLERP,
        SHAPED_PERSIST
    }

    public enum Heading : int
    { 
        FORWARD,
        BACK,
        LEFT,
        RIGHT
    }

    [SerializeField] private float _turningRadius = 1.0f;
    [SerializeField][Min(0.0f)] private float _speed = 5.0f;
    [SerializeField] private HeadingTurnMethod _turningMethod = HeadingTurnMethod.DECAY_ASYMPTOTE;
    [SerializeField] private Heading _heading = Heading.FORWARD;
    [SerializeField] GameObject _pathDropPrefab;

    // headings on the XZ plane, rotate around y-axis
    private Vector3[] _headings = new Vector3[]{ Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
    private Vector3 _currentVelocity = Vector3.forward;
    private Vector3 _turningAxis = Vector3.up;

    private const float SPAWN_DELAY = 0.25f;

    private List<GameObject> _path = new List<GameObject>();
    private HeadingTurnMethod _methodLastFrame;

    /// <summary>
    /// Spherically lerps current towards target using an exponential decay curve. 
    /// NOTE: The magnitude of current is preserved if equal to the magnitude of target.
    /// </summary>
    /// <param name="decay">useful range 1(slow) to 25(fast)</param>
    /// <param name="deltaTime">The most recent time between frames</param>

    // WINNER? (fewest operations, most stable, just adjust the tuning fraction)
    // non-circular exponential decay
    // requires 1x re-normalization to avoid slowdown
    // accounting for speed in decay provides predicatble radius regardless of speed
    // really stable path even with speed changes
    // FIXME: degenerates if angle between current and target is exactly 0 or 180
    // SOLUTION: the issue is that the offset (current - target) doesn't have any lateral (centripetal) movement
    // ...so maybe add a small/vanishing lateral force proportional to the dot(current, target): 0 dot => no lateral, 1 dot full lateral
    private void SmoothLerp(ref Vector3 current, Vector3 target, float turningRadius, float speed)
    {
        const float TUNING_FRACTION = 0.7f; // emperically matched to radius of RotateTowards result
        float decay = speed / (TUNING_FRACTION * turningRadius);

        // DEBUG: do not use slerp or lerp in place of the base formula: a = b + (a - b)t
        // because it reverses the intended non-linear recurrent lerp smoothing back to x = a + (b - a)t

        // DEBUG: approaches but never quite precisely matches
        float alpha = Mathf.Exp(-decay * Time.deltaTime);

        // FIXME: ensure nudge is in direction of target
        // FIXME: the nudge needs to disappear quickly
        // SOLUTION: is there a way to implicitly nudge w/o if-statement? never FULLY align
        //if (Mathf.Approximately(Mathf.Clamp01(-Vector3.Dot(current, target)), 1.0f))
        //{ 
        //    // this works, adds a temp nudge that vanishes almost immediately
        //    Vector3 lateralNudge = new Vector3(target.z, target.y, target.x); 
        //    current += lateralNudge;    
        //}   

        current = (target + (current - target) * alpha).normalized * speed;
    }

    // clearly a circle
    // doesn't require re-normalization
    // requires a messy coroutine instead of frame-to-frame values
    // stable path when accounting for speed
    private IEnumerator ShapedLerp(Vector3 target, float turningRadius, float speed)
    {
        Vector3 initial = _currentVelocity;
        float alpha = 0.0f;
        const float TUNING_FRACTION = 0.7f; // emperically matched to radius of RotateTowards result
        
        while (alpha <= 1.0f)
        {
            yield return null;
            alpha += (Time.deltaTime * speed) / (TUNING_FRACTION * turningRadius);
            _currentVelocity = Vector3.Slerp(initial, target, alpha);
        }
        _circle = null;
    }

    // non-circular exponential decay (has radius even when performing perfect 180 change in heading)
    // doesn't require re-normalization
    // requires 3x sine calls, 2x divisions for slerp (radius fraction can be const a load and multiplied)
    private void ShapedLerpPersist(ref Vector3 current, Vector3 target, float turningRadius, float speed)
    {
        const float TUNING_FRACTION = 0.7f; // emperically matched to radius of RotateTowards result
        current = Vector3.Slerp(current, target, (Time.deltaTime * speed) / (TUNING_FRACTION * turningRadius));
    }

    // clearly a circle (even when performing perfect 180 change in heading)
    // requires 1x re-normalizing to avoid destabilazation
    // requires, 1x sqrt, 1x arccos, 3x if-statements
    // predictable radius regardless of speed
    private void ApplyTurningAcceleration(ref Vector3 current, Vector3 target, float turningRadius, float speed)
    {
        // assumes uniform circular motion in any moment
        // final velocity be normalized to maintain constant speed around turns
        float angle = Vector3.SignedAngle(current, target, _turningAxis);
        float angularSpeed = System.Math.Sign(angle) * (speed / turningRadius); 

        Vector3 acceleration = Vector3.Cross(_turningAxis * angularSpeed, current);
        current = (current + acceleration * Time.deltaTime).normalized * speed;
        //current += acceleration * Time.deltaTime; // can go runaway without normalization
    }

    // NEW WINNER (optimized quaternion math, clearly cirlce even with 180 heading change, stable, uses heading intead of bulk velocity logic)
    // clearly a circle (even when performing perfect 180 change in heading)
    // preserves current speed without need of renormalizing
    // predictable radius regardless of speed
    private void RotateTowards(ref Vector3 current, Vector3 target, float turningRadius, float speed)
    {
        float angle = Vector3.SignedAngle(current, target, _turningAxis);
        float angularSpeed = System.Math.Sign(angle) * (speed / turningRadius) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.AngleAxis(angularSpeed * Time.deltaTime, _turningAxis);
        current = rotation * current;
    }

    private IEnumerator Start()
    {
        _methodLastFrame = _turningMethod;

        while (Application.isPlaying)
        {
            yield return new WaitForSeconds(SPAWN_DELAY);
            _path.Add(Instantiate(_pathDropPrefab, transform.position, Quaternion.identity));
        }
    }

    private void ResetPathSpawns() 
    { 
        foreach (GameObject pathObj in _path) 
        { 
            Destroy(pathObj);
        }
        _path.Clear();
    }

    private Coroutine _circle;
    private Vector3 _currentHeading = Vector3.forward;
    private void Update()
    {
        Vector3 targetVelocity = _headings[(int)_heading] * _speed;
        
        if (_turningMethod == HeadingTurnMethod.CIRCLE_SLERP && _circle == null)
        {
            // Vector3.SmoothDamp position & velocity, (not acceleration)
            _circle = StartCoroutine(ShapedLerp(targetVelocity, _turningRadius, _speed));
        }

        if (_turningMethod != _methodLastFrame) 
        {
            ResetPathSpawns();
        }

        switch (_turningMethod)
        {
            case HeadingTurnMethod.DECAY_ASYMPTOTE: 
                SmoothLerp(ref _currentVelocity, targetVelocity, _turningRadius, _speed); 
                break;
            case HeadingTurnMethod.CENTRIPETAL:
                ApplyTurningAcceleration(ref _currentVelocity, targetVelocity, _turningRadius, _speed);
                break;
            case HeadingTurnMethod.ROTATION:
                RotateTowards(ref _currentHeading, _headings[(int)_heading], _turningRadius, _speed);
                break;
            case HeadingTurnMethod.SHAPED_PERSIST:
                ShapedLerpPersist(ref _currentVelocity, targetVelocity, _turningRadius, _speed);
                break;
            default: 
                break;
        }

        if (_turningMethod != HeadingTurnMethod.ROTATION)
        {
            transform.position += _currentVelocity * Time.deltaTime;
        }
        else
        {
            transform.position += _currentHeading * _speed * Time.deltaTime;
        }
        _methodLastFrame = _turningMethod;
    }
}
