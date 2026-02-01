using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class FlightController : MonoBehaviour
{
    private Rigidbody rb;

    [Header("Control Mode")]
    [SerializeField] private bool useMouseControl = false;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float mouseSmoothing = 0.1f;

    [SerializeField] private float pitch = 0f;

    [SerializeField] private Transform meshTransform;
    private Vector3 meshRotation;
    private float yaw = 0f;
    private float targetPitch = 0f;
    private float targetYaw = 0f;

    private float roll = 0f;
    private float targetRoll = 0f;


    [SerializeField] private float gravity = 0.08f;
    [SerializeField] private float lift = 0.06f;
    [SerializeField] private float diveRate = 0.1f;
    [SerializeField] private float climbRate = 0.04f;
    [SerializeField] private float climbEfficiency = 3.5f;
    [SerializeField] private float turnInterpolation = 0.1f;
    [SerializeField] private float xDrag = 0.99f;
    [SerializeField] private float yDrag = 0.98f;
    [SerializeField] private float zDrag = 0.99f;

    [SerializeField] private float pitchSpeed = 45f;
    [SerializeField] private float maxPitch = 90f;
    [SerializeField] private float yawSpeed = 45f;
    [SerializeField] private float rollSpeed = 45f;
    [SerializeField] private float rollBackSpeed = 45f;

    [SerializeField] private float maxRoll = 90f;

    [SerializeField] private float boostSpeed = 150f;

    [Header("Physics Settings")]
    [Tooltip("Control the stability of the flight. 1 = Perfect aerodynamics, 0 = Free tumbling/sliding.")]
    [Range(0f, 1f)] public float stability = 1.0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.linearVelocity = transform.forward * 10f;
        
        if (useMouseControl)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        meshRotation = meshTransform.eulerAngles; // save initial rotation
    }

    void FixedUpdate()
    {
        ProcessInput();
        UpdateVelocity();
        UpdateRotation();
    }

    private void UpdateVelocity() {
        Vector3 velocity = rb.linearVelocity;

        float pitchRadians = pitch * Mathf.Deg2Rad;
        float cosPitch = Mathf.Cos(pitchRadians);
        float sinPitch = Mathf.Sin(pitchRadians);

        Vector3 lookDirection = transform.forward;

        float horizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;

        // gravity (Constant, acts regardless of stability)
        velocity.y -= gravity;

        // lift (Dependent on Stability)
        // If unstable, wings don't generate coherent lift.
        velocity.y += cosPitch * cosPitch * lift * stability;

        // convert dive speed into forward speed (Dependent on Stability)
        // Unstable planes tumble, they don't efficiently convert potential energy to kinetic.
        if (velocity.y < 0 && cosPitch > 0) {
            float yAcc = velocity.y * -diveRate * cosPitch * cosPitch * stability;
            velocity.y += yAcc;
            velocity.x += lookDirection.x * yAcc / cosPitch;
            velocity.z += lookDirection.z * yAcc / cosPitch;
        }
        
        // climbing (Dependent on Stability)
        if (pitchRadians < 0) {
            float yAcc = horizontalSpeed * -sinPitch * climbRate * stability;
            velocity.y += yAcc * climbEfficiency;
            velocity.x -= lookDirection.x * yAcc / cosPitch;
            velocity.z -= lookDirection.z * yAcc / cosPitch;
        }
        
        // redirect horizontal speed (Turn Interpolation)
        if (cosPitch > 0) {
            // Scale turn capability by stability. Lower stability = less redirection (more drift)
            float currentTurnInterp = turnInterpolation * stability;
            velocity.x += (lookDirection.x / cosPitch * horizontalSpeed - velocity.x) * currentTurnInterp;
            velocity.z += (lookDirection.z / cosPitch * horizontalSpeed - velocity.z) * currentTurnInterp;
        }

        // drag
        // Drag remains effective even when unstable, but we might optionally reduce it to allow 'flinging'
        // For now, standard drag is fine.
        velocity.x *= xDrag;
        velocity.y *= yDrag;
        velocity.z *= zDrag;

        rb.linearVelocity = velocity;
    }

    private void UpdateRotation() {
        Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0f);

        // If stability is perfect (1.0), we snap to target (Arcade style).
        // If stability is compromised (< 1.0), we Slerp towards target, allowing physics (wind) to affect rotation.
        // We use a high base lerp speed so it feels responsive, but scaled by stability.
        if (stability < 0.99f)
        {
            // Slerp factor: 
            // - High stability: fast correction (stiff spring)
            // - Low stability: slow correction (loose spring, wind can overpower it)
            float correctionStrength = stability * 10f * Time.fixedDeltaTime; 
            Quaternion smoothedRotation = Quaternion.Slerp(rb.rotation, targetRotation, correctionStrength);
            rb.MoveRotation(smoothedRotation);
            
            // Optional: Sync internal pitch/yaw with actual physics rotation to prevent 'snapping' when stability returns?
            // For now, we act as a "Righting Moment" (Spring) which is aerodynamically correct. 
            // The plane tries to fly straight, wind fights it.
        }
        else
        {
            rb.MoveRotation(targetRotation);
        }

        if (meshTransform != null) {
            meshTransform.localRotation = Quaternion.Euler(roll + meshRotation.x, meshRotation.y, meshRotation.z);
        }
    }

    private void ProcessInput() {
        if (useMouseControl)
        {
            ProcessMouseInput();
        }
        else
        {
            ProcessKeyboardInput();
        }
        
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
        roll = Mathf.Clamp(roll, -maxRoll, maxRoll);
    }

    private void ProcessKeyboardInput() {
        if (Keyboard.current.wKey.isPressed) {
            pitch += pitchSpeed * Time.fixedDeltaTime;
        }
        if (Keyboard.current.sKey.isPressed) {
            pitch -= pitchSpeed * Time.fixedDeltaTime;
        }
        if (Keyboard.current.aKey.isPressed) {
            yaw -= yawSpeed * Time.fixedDeltaTime;
            targetRoll -= rollSpeed * Time.fixedDeltaTime;
        }
        if (Keyboard.current.dKey.isPressed) {
            yaw += yawSpeed * Time.fixedDeltaTime;
            targetRoll += rollSpeed * Time.fixedDeltaTime;
        } if (Keyboard.current.spaceKey.isPressed) {
            Boost();
        }


        if (!Keyboard.current.aKey.isPressed && !Keyboard.current.dKey.isPressed) {
            // return to level roll
            if (roll > 0f) {
                targetRoll -= rollBackSpeed * Time.fixedDeltaTime;
                if (targetRoll < 0f) targetRoll = 0f;
            } else if (roll < 0f) {
                targetRoll += rollBackSpeed * Time.fixedDeltaTime;
                if (targetRoll > 0f) targetRoll = 0f;
            }
        }
    }

    private void ProcessMouseInput() {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        
        // Update target angles based on mouse movement
        targetYaw += mouseDelta.x * mouseSensitivity * Time.fixedDeltaTime;
        targetPitch -= mouseDelta.y * mouseSensitivity * Time.fixedDeltaTime;
        
        targetPitch = Mathf.Clamp(targetPitch, -90f, 90f);
        
        // Smooth interpolation to target angles
        pitch = Mathf.Lerp(pitch, targetPitch, mouseSmoothing);
        yaw = Mathf.Lerp(yaw, targetYaw, mouseSmoothing);
        
        // Toggle mouse lock with Escape
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private void Boost() {
        Vector3 boostVelocity = transform.forward * boostSpeed;
        rb.linearVelocity += boostVelocity;
    }
}