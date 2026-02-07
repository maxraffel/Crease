using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Custom physics body that replaces Rigidbody-driven velocity/force management.
/// Attach this alongside a kinematic Rigidbody. All movement is manual —
/// other scripts read/write Velocity and apply forces through this component.
///
/// Usage:
///   body.Velocity          — get/set the current velocity
///   body.Speed             — shorthand for velocity magnitude
///   body.AddForce(v)       — continuous force (scaled by dt and mass internally)
///   body.AddImpulse(v)     — instant velocity change (scaled by mass)
///   body.SetVelocity(v)    — hard override (use sparingly)
///   body.MoveRotation(q)   — rotate the kinematic rigidbody
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class KinematicBody : MonoBehaviour
{
    // ------------------------------------------------------------------ Config
    [Header("Configuration")]
    [Tooltip("Mass used for force calculations. Does not use Rigidbody mass.")]
    [SerializeField] private float mass = 1f;

    [Tooltip("If true, the Rigidbody will be auto-configured as kinematic on Awake.")]
    [SerializeField] private bool autoConfigureRigidbody = true;

    // ------------------------------------------------------------------ State
    /// <summary>Current velocity in world space.</summary>
    public Vector3 Velocity { get; set; }

    /// <summary>Speed (magnitude of Velocity).</summary>
    public float Speed => Velocity.magnitude;

    /// <summary>Current mass used for force calculations.</summary>
    public float Mass
    {
        get => mass;
        set => mass = Mathf.Max(0.001f, value);
    }

    /// <summary>Whether physics integration is paused (e.g. during crash).</summary>
    public bool Frozen { get; set; }

    // ------------------------------------------------------------------ Internal
    private Rigidbody _rb;
    private Vector3 _accumulatedForce;

    // ================================================================== Lifecycle

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (autoConfigureRigidbody)
        {
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    private void FixedUpdate()
    {
        if (Frozen) return;

        // Integrate accumulated forces → velocity
        if (_accumulatedForce.sqrMagnitude > 0f)
        {
            Velocity += (_accumulatedForce / mass) * Time.fixedDeltaTime;
            _accumulatedForce = Vector3.zero;
        }

        // Integrate velocity → position
        _rb.MovePosition(_rb.position + Velocity * Time.fixedDeltaTime);
    }

    // ================================================================== Public API

    /// <summary>
    /// Apply a continuous force (like gravity or wind). Accumulated over the frame,
    /// integrated in FixedUpdate. Equivalent to Rigidbody.AddForce(ForceMode.Force).
    /// </summary>
    public void AddForce(Vector3 force)
    {
        _accumulatedForce += force;
    }

    /// <summary>
    /// Apply an instant velocity change scaled by mass.
    /// Equivalent to Rigidbody.AddForce(ForceMode.Impulse).
    /// </summary>
    public void AddImpulse(Vector3 impulse)
    {
        Velocity += impulse / mass;
    }

    /// <summary>
    /// Apply a direct velocity change (ignores mass).
    /// Equivalent to Rigidbody.AddForce(ForceMode.VelocityChange).
    /// </summary>
    public void AddVelocityChange(Vector3 delta)
    {
        Velocity += delta;
    }

    /// <summary>
    /// Apply a direct acceleration (ignores mass), integrated over dt.
    /// Equivalent to Rigidbody.AddForce(ForceMode.Acceleration).
    /// </summary>
    public void AddAcceleration(Vector3 acceleration)
    {
        Velocity += acceleration * Time.fixedDeltaTime;
    }

    /// <summary>
    /// Hard-set the velocity. Prefer AddForce/AddImpulse for gameplay behaviour.
    /// </summary>
    public void SetVelocity(Vector3 velocity)
    {
        Velocity = velocity;
    }

    /// <summary>
    /// Rotate the kinematic rigidbody.
    /// </summary>
    public void MoveRotation(Quaternion rotation)
    {
        _rb.MoveRotation(rotation);
    }

    /// <summary>
    /// Helper: apply a force using Unity's ForceMode enum for compatibility.
    /// </summary>
    public void AddForce(Vector3 force, ForceMode mode)
    {
        switch (mode)
        {
            case ForceMode.Force:
                AddForce(force);
                break;
            case ForceMode.Impulse:
                AddImpulse(force);
                break;
            case ForceMode.VelocityChange:
                AddVelocityChange(force);
                break;
            case ForceMode.Acceleration:
                AddAcceleration(force);
                break;
        }
    }
}
