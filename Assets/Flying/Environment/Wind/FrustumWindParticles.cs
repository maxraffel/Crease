using UnityEngine;
using PhysicsHelpers;

[RequireComponent(typeof(ParticleSystem))]
[RequireComponent(typeof(FrustumTrigger))]
[ExecuteAlways]
public class FrustumWindParticles : MonoBehaviour
{
    private ParticleSystem _particleSystem;
    private FrustumTrigger _frustumTrigger;
    
    // Cache to detect changes
    private float _lastTopRadius;
    private float _lastBottomRadius;
    private float _lastHeight;

    private void OnEnable()
    {
        _particleSystem = GetComponent<ParticleSystem>();
        _frustumTrigger = GetComponent<FrustumTrigger>();
        UpdateParticleShape();
    }

    private void OnValidate()
    {
        if (_particleSystem == null) _particleSystem = GetComponent<ParticleSystem>();
        if (_frustumTrigger == null) _frustumTrigger = GetComponent<FrustumTrigger>();
        UpdateParticleShape();
    }

    private void Update()
    {
        // Check if FrustumTrigger dimensions have changed
        if (_frustumTrigger != null && HasFrustumChanged())
        {
            UpdateParticleShape();
        }
    }

    private bool HasFrustumChanged()
    {
        return _frustumTrigger.topRadius != _lastTopRadius ||
               _frustumTrigger.bottomRadius != _lastBottomRadius ||
               _frustumTrigger.height != _lastHeight;
    }

    private void UpdateParticleShape()
    {
        if (_particleSystem == null || _frustumTrigger == null) return;

        // Update cache
        _lastTopRadius = _frustumTrigger.topRadius;
        _lastBottomRadius = _frustumTrigger.bottomRadius;
        _lastHeight = _frustumTrigger.height;

        // Configure shape module
        var shape = _particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        
        // Frustum has bottom (small radius) at Y=0 and top (large radius) at Y=height
        // We need a cone that starts with bottomRadius and expands to topRadius over the height
        
        float radiusDiff = _frustumTrigger.topRadius - _frustumTrigger.bottomRadius;
        
        if (Mathf.Abs(radiusDiff) < 0.001f)
        {
            // It's a cylinder - use minimal angle
            shape.angle = 0.1f;
            shape.radius = _frustumTrigger.bottomRadius;
            shape.length = _frustumTrigger.height;
            shape.position = Vector3.zero;
        }
        else
        {
            // Calculate cone angle: at distance=height from start, radius grows by radiusDiff
            // tan(angle) = radiusDiff / height
            float halfAngle = Mathf.Atan(radiusDiff / _frustumTrigger.height) * Mathf.Rad2Deg;
            
            shape.angle = halfAngle;
            shape.radius = _frustumTrigger.bottomRadius;
            shape.length = _frustumTrigger.height;
            shape.position = Vector3.zero;
        }

        // Configure start speed so particles reach the top exactly at end of lifetime
        var main = _particleSystem.main;
        float lifetime = main.startLifetime.constant;
        if (lifetime > 0)
        {
            main.startSpeed = _frustumTrigger.height / lifetime;
        }
    }
}
