using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Handles all player collisions. Obstacle hits apply knockback and trigger speed
/// recovery. Ground hits while crashed delegate to PlayerCrashHandler.Land().
/// All tuning values are exposed to the Inspector for rapid iteration.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class FlightCollisionController : MonoBehaviour
{
    // ------------------------------------------------------------------ Refs
    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private PlayerCrashHandler crashHandler;

    // ------------------------------------------------------------------ Tags
    [Header("Tags")]
    [SerializeField] private string obstacleTag = "Obstacle";
    [SerializeField] private string groundTag = "Ground";

    // ------------------------------------------------------------------ Knockback
    [Header("Knockback")]
    [Tooltip("Multiplier applied to the reflected collision normal to produce the knockback impulse.")]
    [SerializeField] private float knockbackForce = 20f;

    [Tooltip("How much of the pre-collision speed is added on top of the knockback direction. " +
             "0 = pure normal bounce, 1 = full reflection.")]
    [Range(0f, 1f)]
    [SerializeField] private float reflectionBlend = 0.3f;

    [Tooltip("Minimum knockback impulse magnitude (prevents weak glancing blows).")]
    [SerializeField] private float minKnockbackMagnitude = 5f;

    [Tooltip("Maximum knockback impulse magnitude.")]
    [SerializeField] private float maxKnockbackMagnitude = 60f;

    // ------------------------------------------------------------------ Recovery
    [Header("Speed Recovery")]
    [Tooltip("Fraction of pre-collision speed that the player will recover to (0-1).")]
    [Range(0f, 1f)]
    [SerializeField] private float speedRetention = 0.5f;

    [Tooltip("Time in seconds to accelerate from knockback speed to the target recovery speed.")]
    [SerializeField] private float recoveryDuration = 2f;

    [Tooltip("Time in seconds after knockback before recovery acceleration begins.")]
    [SerializeField] private float recoveryDelay = 0.3f;

    // ------------------------------------------------------------------ Invulnerability
    [Header("Invulnerability")]
    [Tooltip("Seconds of invulnerability after a knockback hit (prevents rapid repeated hits).")]
    [SerializeField] private float invulnerabilityDuration = 0.5f;

    // ------------------------------------------------------------------ Ground crash
    [Header("Ground Crash")]
    [Tooltip("If true, hitting the ground while in the crashed state triggers PlayerCrashHandler.Land().")]
    [SerializeField] private bool landOnGroundAfterCrash = true;

    // ------------------------------------------------------------------ Events
    [Header("Events")]
    public UnityEvent OnKnockback;
    public UnityEvent OnRecoveryStarted;
    public UnityEvent OnRecoveryComplete;

    // ------------------------------------------------------------------ State
    public bool IsRecovering => _isRecovering;
    public bool IsInvulnerable => Time.time < _invulnerableUntil;
    public float PreCollisionSpeed => _preCollisionSpeed;

    private bool _isRecovering;
    private float _preCollisionSpeed;
    private float _targetRecoverySpeed;
    private float _recoveryStartTime;
    private float _invulnerableUntil;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

    // ================================================================== Collision
    private void OnCollisionEnter(Collision collision)
    {
        // --- Ground landing while crashed ---
        if (landOnGroundAfterCrash
            && crashHandler != null
            && crashHandler.IsCrashed
            && collision.collider.CompareTag(groundTag))
        {
            crashHandler.Land();
            return;
        }

        // --- Obstacle knockback ---
        if (collision.collider.CompareTag(obstacleTag))
        {
            if (IsInvulnerable) return;
            ApplyKnockback(collision);
        }
    }

    // ================================================================== Knockback
    private void ApplyKnockback(Collision collision)
    {
        Vector3 velocity = rb.linearVelocity;
        _preCollisionSpeed = velocity.magnitude;

        // Collision normal pointing away from the obstacle
        Vector3 contactNormal = collision.contacts[0].normal.normalized;

        // Build knockback direction: blend between pure normal and reflected velocity
        Vector3 reflected = Vector3.Reflect(velocity.normalized, contactNormal);
        Vector3 knockbackDir = Vector3.Lerp(contactNormal, reflected, reflectionBlend).normalized;

        // Compute impulse magnitude, scaled by incoming speed
        float impulseMagnitude = Mathf.Clamp(
            knockbackForce + _preCollisionSpeed * reflectionBlend,
            minKnockbackMagnitude,
            maxKnockbackMagnitude);

        // Apply
        rb.linearVelocity = knockbackDir * impulseMagnitude;

        // Start recovery state
        _targetRecoverySpeed = _preCollisionSpeed * speedRetention;
        _recoveryStartTime = Time.time + recoveryDelay;
        _isRecovering = true;
        _invulnerableUntil = Time.time + invulnerabilityDuration;

        OnKnockback?.Invoke();
    }

    // ================================================================== Recovery
    private void FixedUpdate()
    {
        if (!_isRecovering) return;
        if (Time.time < _recoveryStartTime) return; // still in delay window

        // Fire event on the first recovery frame
        if (Time.time - _recoveryStartTime < Time.fixedDeltaTime * 1.5f)
        {
            OnRecoveryStarted?.Invoke();
        }

        float currentSpeed = rb.linearVelocity.magnitude;
        float speedDelta = _targetRecoverySpeed - currentSpeed;

        // Check if we've reached or exceeded target speed
        if (speedDelta <= 0f)
        {
            _isRecovering = false;
            OnRecoveryComplete?.Invoke();
            return;
        }

        // Calculate time remaining in recovery window
        float elapsedRecoveryTime = Time.time - _recoveryStartTime;
        float remainingTime = recoveryDuration - elapsedRecoveryTime;

        // If we've exceeded recovery duration, clamp to target and complete
        if (remainingTime <= 0f)
        {
            Vector3 forward = rb.linearVelocity.normalized;
            if (forward.sqrMagnitude < 0.001f)
                forward = transform.forward;

            rb.linearVelocity = forward * _targetRecoverySpeed;
            _isRecovering = false;
            OnRecoveryComplete?.Invoke();
            return;
        }

        // Calculate required acceleration to reach target in remaining time
        // a = Δv / Δt
        float requiredAcceleration = speedDelta / remainingTime;

        // Accelerate along current heading
        Vector3 heading = rb.linearVelocity.normalized;
        if (heading.sqrMagnitude < 0.001f)
            heading = transform.forward;

        rb.linearVelocity += heading * requiredAcceleration * Time.fixedDeltaTime;
    }
}
