using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class FlightControllerForces : MonoBehaviour
{
    [SerializeField] private Transform meshTransform;

    private Rigidbody rb;

    private float pitch = 0f;
    private float yaw = 0f;
    private float roll = 0f;

    private float targetPitch = 0f;
    private float targetYaw = 0f;
    private float targetRoll = 0f;

    [SerializeField] private float pitchSpeed = 5f;
    [SerializeField] private float yawSpeed = 5f;
    [SerializeField] private float rollSpeed = 5f;

    [SerializeField] private float gravity = 0.08f;
    [SerializeField] private float lift = 0.06f;

    [SerializeField] private float diveRate = 0.1f;
    [SerializeField] private float climbRate = 0.04f;
    [SerializeField] private float climbEfficiency = 3.5f;
    [SerializeField] private float turnInterpolation = 0.1f;

    [SerializeField] private float xDrag = 0.01f;
    [SerializeField] private float yDrag = 0.02f;
    [SerializeField] private float zDrag = 0.01f;

    private Vector3 meshRotation;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        meshRotation = meshTransform.eulerAngles; // save initial rotation
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void FixedUpdate() {
        HandleInput();
        HandleRotation();
        HandlePhysics();
    }

    private void HandleRotation()
    {
        pitch = Mathf.Lerp(pitch, targetPitch, Time.fixedDeltaTime * pitchSpeed);
        yaw = Mathf.Lerp(yaw, targetYaw, Time.fixedDeltaTime * yawSpeed);
        roll = Mathf.Lerp(roll, targetRoll, Time.fixedDeltaTime * rollSpeed);

        rb.MoveRotation(Quaternion.Euler(pitch, yaw, 0f));
        meshTransform.localRotation = Quaternion.Euler(meshRotation.x + roll, meshRotation.y, meshRotation.z);
        // Debug.Log("Pitch: " + pitch + " Yaw: " + yaw + " Roll: " + roll + " Mesh Rotation: " + meshRotation);
    }

    private void HandleInput()
    {
        targetRoll = 0f;
        if (Keyboard.current.wKey.isPressed) {
            targetPitch += pitchSpeed * Time.fixedDeltaTime;
        }
        if (Keyboard.current.sKey.isPressed) {
            targetPitch -= pitchSpeed * Time.fixedDeltaTime;
        }
        if (Keyboard.current.aKey.isPressed) {
            targetYaw -= yawSpeed * Time.fixedDeltaTime;
            targetRoll -= rollSpeed * Time.fixedDeltaTime;
        }
        if (Keyboard.current.dKey.isPressed) {
            targetYaw += yawSpeed * Time.fixedDeltaTime;
            targetRoll += rollSpeed * Time.fixedDeltaTime;
        } if (Keyboard.current.spaceKey.isPressed) {
            Boost();
        }

        targetPitch = Mathf.Clamp(targetPitch, -90f, 90f);
        targetRoll = Mathf.Clamp(targetRoll, -90f, 90f);
    }

    private void HandlePhysics()
    {
        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;
        float horizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
        Vector3 horizontalLookDirection = new Vector3(transform.forward.x, 0f, transform.forward.z);

        float cosPitch = Mathf.Cos(pitch * Mathf.Deg2Rad);
        float sinPitch = Mathf.Sin(pitch * Mathf.Deg2Rad);

        // gravity
        rb.AddForce(Vector3.down * gravity);

        // lift
        rb.AddForce(transform.up * lift * velocity.magnitude);

        // convert downward speed into forward speed
        if (velocity.y < 0 && cosPitch > 0)
        {
            float strippedVerticalSpeed = -velocity.y * cosPitch * cosPitch * diveRate;
            rb.AddForce(Vector3.up * strippedVerticalSpeed);
            rb.AddForce(horizontalLookDirection * strippedVerticalSpeed / cosPitch);
        }

        // climb
        if (pitch < 0)
        {
            float strippedHorizontalSpeed = horizontalSpeed * -sinPitch * climbRate;
            rb.AddForce(transform.up * strippedHorizontalSpeed * climbEfficiency);
            rb.AddForce(-horizontalLookDirection * strippedHorizontalSpeed);
        }

        // redirect velocity towards look direction
        if (cosPitch > 0)
        {
            Vector3 desiredHorizontalVelocity = horizontalLookDirection.normalized * horizontalSpeed;
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 velocityChange = (desiredHorizontalVelocity / cosPitch - horizontalVelocity) * turnInterpolation;
            rb.AddForce(velocityChange);
        }

        // drag
        rb.AddForce(velocity.x * -xDrag, velocity.y * -yDrag, velocity.z * -zDrag);
    }

    private void Boost()
    {
        rb.AddRelativeForce(Vector3.forward * 500f);
    }
}
