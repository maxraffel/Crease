using UnityEngine;
using PhysicsHelpers; // Namespace where FrustumTrigger resides

[RequireComponent(typeof(FrustumTrigger))]
public class FrustumWindZone : WindProvider
{
    [Header("Wind Settings")]
    [Tooltip("The strength of the wind force pushing from bottom to top.")]
    public float windStrength = 10f;

    [Tooltip("If true, wind strength fades out near the edges of the cone.")]
    public bool featherEdges = true;

    private FrustumTrigger _shape;

    private void Awake()
    {
        _shape = GetComponent<FrustumTrigger>();
    }

    private void Start()
    {
        // Hook into FrustumTrigger events
        if (_shape != null)
        {
            _shape.onTriggerEnter.AddListener(OnEnterZone);
            _shape.onTriggerExit.AddListener(OnExitZone);
        }
    }

    private void OnDestroy()
    {
        if (_shape != null)
        {
            _shape.onTriggerEnter.RemoveListener(OnEnterZone);
            _shape.onTriggerExit.RemoveListener(OnExitZone);
        }
    }

    private void OnEnterZone(Collider other)
    {
        // Try to find the Aerodynamics component on the entering object (or its parent/root)
        // Aerodynamics script is usually on the root rigidbody.
        Aerodynamics aero = other.attachedRigidbody ? other.attachedRigidbody.GetComponent<Aerodynamics>() : other.GetComponent<Aerodynamics>();
        
        if (aero != null)
        {
            aero.AddWindZone(this);
        }
    }

    private void OnExitZone(Collider other)
    {
        Aerodynamics aero = other.attachedRigidbody ? other.attachedRigidbody.GetComponent<Aerodynamics>() : other.GetComponent<Aerodynamics>();
        
        if (aero != null)
        {
            aero.RemoveWindZone(this);
        }
    }

    /// <summary>
    /// Calculates wind force for a specific point in world space.
    /// Returns Vector3.zero if the point is outside the frustum volume.
    /// </summary>
    public override Vector3 GetWindForceAtPoint(Vector3 worldPosition)
    {
        if (_shape == null) return Vector3.zero;

        // 1. Transform World Point to Local Space of the cone
        Vector3 localPos = transform.InverseTransformPoint(worldPosition);

        // 2. Check Height Bounds
        // FrustumTrigger creates a mesh centered at 0. Height goes from -h/2 to +h/2.
        float halfHeight = _shape.height * 0.5f;
        if (localPos.y < -halfHeight || localPos.y > halfHeight)
        {
            return Vector3.zero;
        }

        // 3. Check Radius at this specific height
        // Map Y from [-halfH, halfH] to [0, 1] for Lerp
        float t = Mathf.InverseLerp(-halfHeight, halfHeight, localPos.y);
        
        // Calculate the maximum radius at this Y level
        float maxRadiusAtY = Mathf.Lerp(_shape.bottomRadius, _shape.topRadius, t);
        
        // Calculate distance of the point from the center axis (XZ plane)
        float distSq = localPos.x * localPos.x + localPos.z * localPos.z;

        if (distSq > maxRadiusAtY * maxRadiusAtY)
        {
            // Point is outside the cone radius
            return Vector3.zero;
        }

        // 4. Calculate Force
        // Direction: Local Up (Bottom -> Top) transformed to World Direction
        Vector3 forceDirection = transform.up;
        float strength = windStrength;

        // Optional: Feather edges for softer entry
        if (featherEdges)
        {
            float dist = Mathf.Sqrt(distSq);
            float normalizedDist = dist / maxRadiusAtY; // 0 at center, 1 at edge
            // Simple ease-out curve: full strength at center, 0 at edge
            strength *= Mathf.Clamp01(1.0f - normalizedDist);
        }

        return forceDirection * strength;
    }
}
