using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(KinematicBody))]
[RequireComponent(typeof(FlightController))]
public class FlightForceReceiver : MonoBehaviour
{
    [Header("Wind Source")]
    [Tooltip("Current active wind zones affecting this plane. Automatically managed by triggers.")]
    public List<WindProvider> activeWindZones = new List<WindProvider>();

    [Header("Settings")]
    [Tooltip("Multiplier for how much the wind affects the physics.")]
    public float windForceMultiplier = 1.0f;

    private KinematicBody _body;

    private void Awake()
    {
        _body = GetComponent<KinematicBody>();
    }

    public void AddWindZone(WindProvider zone)
    {
        if (!activeWindZones.Contains(zone))
        {
            activeWindZones.Add(zone);
        }
    }

    public void RemoveWindZone(WindProvider zone)
    {
        if (activeWindZones.Contains(zone))
        {
            activeWindZones.Remove(zone);
        }
    }

    private void FixedUpdate()
    {
        if (activeWindZones.Count == 0) return;

        Vector3 totalWindForce = Vector3.zero;

        for (int i = activeWindZones.Count - 1; i >= 0; i--)
        {
            WindProvider zone = activeWindZones[i];
            if (zone == null) 
            {
                activeWindZones.RemoveAt(i);
                continue;
            }
            totalWindForce += zone.GetWindForceAtPoint(transform.position);
        }

        if (totalWindForce.sqrMagnitude > 0.01f)
        {
            Vector3 finalForce = totalWindForce * windForceMultiplier;
            _body.AddForce(finalForce);
        }
    }

    /// <summary>
    /// Apply an instantaneous impulse (explosions, collisions, strong gusts).
    /// </summary>
    public void AddImpact(Vector3 force)
    {
        _body.AddImpulse(force);
    }

    /// <summary>
    /// Apply a continuous or instantaneous external force.
    /// </summary>
    public void AddExternalForce(Vector3 force, ForceMode mode = ForceMode.Force)
    {
        _body.AddForce(force, mode);
    }

    /// <summary>
    /// Simulates an explosion force by computing direction and falloff manually.
    /// </summary>
    public void AddExplosionForce(float explosionForce, Vector3 explosionPosition, float explosionRadius, float upwardsModifier = 0.0f)
    {
        Vector3 dir = transform.position - explosionPosition;
        float distance = dir.magnitude;

        if (distance > explosionRadius || distance < 0.001f) return;

        float falloff = 1f - (distance / explosionRadius);
        Vector3 force = dir.normalized * explosionForce * falloff;
        force.y += upwardsModifier * falloff;

        _body.AddImpulse(force);
    }
}
