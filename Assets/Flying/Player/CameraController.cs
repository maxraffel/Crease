using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    
    [Header("Position")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.5f, -7f);
    [SerializeField] private float followSpeed = 10f;
    
    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float lookAheadAmount = 2f;
    
    [Header("Crash Mode")]
    [SerializeField] private bool isCrashed = false;
    
    private Vector3 smoothVelocity;

    void LateUpdate()
    {
        if (target == null) return;
        
        // Calculate desired position behind the plane
        // Vector3 desiredPosition = target.position + target.rotation * offset;
        
        // Normal flight: offset follows target rotation (camera sits behind plane)
        // Crash: offset uses WORLD space (do NOT orbit wildly with target roll/pitch)
        Vector3 desiredPosition = isCrashed
            ? target.position + offset
            : target.position + target.rotation * offset;
        
        // Smoothly move to position
        transform.position = Vector3.SmoothDamp(
            transform.position, 
            desiredPosition, 
            ref smoothVelocity, 
            1f / followSpeed
        );
        
        // Look at a point ahead of the plane
        // Vector3 lookPoint = target.position + target.forward * lookAheadAmount;
        
        // Normal: look ahead of plane
        // Crash: just look at the plane itself (stable)
        Vector3 lookPoint = isCrashed
            ? target.position
            : target.position + target.forward * lookAheadAmount;
        Vector3 direction = lookPoint - transform.position;
        
        // Smoothly rotate to face the plane
        // Quaternion targetRotation = Quaternion.LookRotation(direction);
        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);

        transform.rotation = Quaternion.Slerp(
            transform.rotation, 
            targetRotation, 
            Time.deltaTime * rotationSpeed
        );
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    
    public void SetCrashed(bool crashed)
    {
        isCrashed = crashed;
    }
}