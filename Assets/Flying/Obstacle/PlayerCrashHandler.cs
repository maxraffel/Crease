using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerCrashHandler : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MonoBehaviour flightController;
    [SerializeField] private Rigidbody rb;

    [Header("Tags")]
    [SerializeField] private string obstacleTag = "Obstacle";
    [SerializeField] private string groundTag = "Ground";

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

    private bool crashed = false;
    private bool landed = false;

    private RigidbodyConstraints originalConstraints;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        originalConstraints = rb.constraints;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!crashed && collision.collider.CompareTag(obstacleTag))
        {
            Crash();
            return;
        }

        if (crashed && !landed && collision.collider.CompareTag(groundTag))
        {
            Land();
        }
    }

    private void Crash()
    {
        crashed = true;
        // cameraController?.SetCrashed(true);
        
        if (flightController != null)
            flightController.enabled = false;
        
        rb.useGravity = true;
        rb.isKinematic = false;

        // Stop forward motion immediately
        if (zeroVelocityOnCrash)
        {
            rb.linearVelocity = Vector3.zero;
        }

        // Prevent spin after collision
        if (clearAngularVelocityOnCrash)
        {
            rb.angularVelocity = Vector3.zero;
        }

        if (freezeRotationOnCrash)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }
    }

    private void Land()
    {
        landed = true;
        
        if (stopCompletelyOnLand)
        {
            rb.linearVelocity = Vector3.zero;
        }

        // Stop remaining spin before freezing/kinematic
        if (clearAngularVelocityOnLand)
        {
            rb.angularVelocity = Vector3.zero;
        }

        if (freezeRotationOnLand)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        if (setKinematicOnLand)
        {
            rb.isKinematic = true;
        }
    }
}
