using UnityEngine;

/// <summary>
/// Handles the crash state of the player — disabling flight, enabling gravity, and
/// stabilizing the rigidbody. Does NOT handle collision detection; that is done by
/// FlightCollisionController, which may call Crash() when appropriate.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerCrashHandler : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MonoBehaviour flightController;
    [SerializeField] private Rigidbody rb;

    [Header("Crash Tuning")]
    [SerializeField] private bool zeroVelocityOnCrash = true;
    [SerializeField] private bool stopCompletelyOnLand = true;
    [SerializeField] private bool setKinematicOnLand = true;

    [Header("Physics Stabilization")]
    [SerializeField] private bool clearAngularVelocityOnCrash = true;
    [SerializeField] private bool freezeRotationOnCrash = true;
    [SerializeField] private bool clearAngularVelocityOnLand = true;
    [SerializeField] private bool freezeRotationOnLand = true;

    [SerializeField] private CameraController cameraController;

    public bool IsCrashed => crashed;
    public bool IsLanded => landed;

    private bool crashed = false;
    private bool landed = false;

    private RigidbodyConstraints originalConstraints;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        originalConstraints = rb.constraints;
    }

    /// <summary>
    /// Enters the crashed state — disables flight and lets the plane fall.
    /// </summary>
    public void Crash()
    {
        if (crashed) return;

        crashed = true;

        if (flightController != null)
            flightController.enabled = false;

        rb.useGravity = true;
        rb.isKinematic = false;

        if (zeroVelocityOnCrash)
            rb.linearVelocity = Vector3.zero;

        if (clearAngularVelocityOnCrash)
            rb.angularVelocity = Vector3.zero;

        if (freezeRotationOnCrash)
            rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    /// <summary>
    /// Called when the crashed plane touches the ground.
    /// </summary>
    public void Land()
    {
        if (!crashed || landed) return;

        landed = true;

        if (stopCompletelyOnLand)
            rb.linearVelocity = Vector3.zero;

        if (clearAngularVelocityOnLand)
            rb.angularVelocity = Vector3.zero;

        if (freezeRotationOnLand)
            rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (setKinematicOnLand)
            rb.isKinematic = true;
    }

    /// <summary>
    /// Resets the crash state (useful for scene reloads or respawns).
    /// </summary>
    public void ResetCrash()
    {
        crashed = false;
        landed = false;
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.constraints = originalConstraints;

        if (flightController != null)
            flightController.enabled = true;
    }
}
