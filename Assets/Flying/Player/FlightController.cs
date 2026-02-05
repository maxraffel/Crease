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

        // gravity
        velocity.y -= gravity;

        // lift
        velocity.y += cosPitch * cosPitch * lift;

        // convert dive speed into forward speed
        if (velocity.y < 0 && cosPitch > 0) {
            float yAcc = velocity.y * -diveRate * cosPitch * cosPitch;
            velocity.y += yAcc;
            velocity.x += lookDirection.x * yAcc / cosPitch;
            velocity.z += lookDirection.z * yAcc / cosPitch;
        }
        // climbing
        if (pitchRadians < 0) {
            float yAcc = horizontalSpeed * -sinPitch * climbRate;
            velocity.y += yAcc * climbEfficiency;
            velocity.x -= lookDirection.x * yAcc / cosPitch;
            velocity.z -= lookDirection.z * yAcc / cosPitch;
        }
        // redirect horizontal speed
        if (cosPitch > 0) {
            velocity.x += (lookDirection.x / cosPitch * horizontalSpeed - velocity.x) * turnInterpolation;
            velocity.z += (lookDirection.z / cosPitch * horizontalSpeed - velocity.z) * turnInterpolation;
        }

        // drag
        velocity.x *= xDrag;
        velocity.y *= yDrag;
        velocity.z *= zDrag;

        rb.linearVelocity = velocity;
    }

    private void UpdateRotation() {
        rb.MoveRotation(Quaternion.Euler(pitch, yaw, 0f));
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
            roll -= rollSpeed * Time.fixedDeltaTime;
        }
        if (Keyboard.current.dKey.isPressed) {
            yaw += yawSpeed * Time.fixedDeltaTime;
            roll += rollSpeed * Time.fixedDeltaTime;
        } if (Keyboard.current.spaceKey.isPressed) {
            Boost();
        }


        if (!Keyboard.current.aKey.isPressed && !Keyboard.current.dKey.isPressed) {
            // return to level roll
            if (roll > 0f) {
                roll -= rollBackSpeed * Time.fixedDeltaTime;
                if (roll < 0f) roll = 0f;
            } else if (roll < 0f) {
                roll += rollBackSpeed * Time.fixedDeltaTime;
                if (roll > 0f) roll = 0f;
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