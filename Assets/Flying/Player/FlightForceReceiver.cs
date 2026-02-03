using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(FlightController))]
public class FlightForceReceiver : MonoBehaviour
{
    [Header("Wind Source")]
    [Tooltip("Current active wind zones affecting this plane. Automatically managed by triggers.")]
    public List<WindProvider> activeWindZones = new List<WindProvider>();

    [Header("Settings")]
    [Tooltip("Multiplier for how much the wind affects the physics.")]
    public float windForceMultiplier = 1.0f;

    private Rigidbody _rb;
    private FlightController _flightController;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _flightController = GetComponent<FlightController>();
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

        // Sum up forces from all active zones at the plane's center position
        for (int i = activeWindZones.Count - 1; i >= 0; i--)
        {
            WindProvider zone = activeWindZones[i];
            if (zone == null) 
            {
                activeWindZones.RemoveAt(i);
                continue;
            }
            // Use transform.position (Center of the plane) for detection
            totalWindForce += zone.GetWindForceAtPoint(transform.position);
        }

        // Apply Physics Force
        if (totalWindForce.sqrMagnitude > 0.01f)
        {
            Vector3 finalForce = totalWindForce * windForceMultiplier;
            _rb.AddForce(finalForce, ForceMode.Force);
        }
    }

    /// <summary>
    /// Apply an instantaneous force (Impulse).
    /// Great for explosions, collisions, or strong wind gusts.
    /// </summary>
    /// <param name="force">The force vector.</param>
    public void AddImpact(Vector3 force)
    {
        _rb.AddForce(force, ForceMode.Impulse);
    }

    /// <summary>
    /// Apply a continuous force.
    /// </summary>
    public void AddExternalForce(Vector3 force, ForceMode mode = ForceMode.Force)
    {
        _rb.AddForce(force, mode);
    }

    /// <summary>
    /// Adds a standard explosion force.
    /// </summary>
    public void AddExplosionForce(float explosionForce, Vector3 explosionPosition, float explosionRadius, float upwardsModifier = 0.0f)
    {
        _rb.AddExplosionForce(explosionForce, explosionPosition, explosionRadius, upwardsModifier, ForceMode.Impulse);
    }
}
