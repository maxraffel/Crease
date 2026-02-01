using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(FlightForceReceiver))]
public class Aerodynamics : MonoBehaviour
{
    [Header("Wind Source")]
    [Tooltip("Current active wind zones affecting this plane. Automatically managed by triggers.")]
    public List<WindProvider> activeWindZones = new List<WindProvider>();

    [Header("Settings")]
    [Tooltip("Multiplier for how much the wind affects the physics.")]
    public float windForceMultiplier = 1.0f;
    
    [Tooltip("How much the wind turbulence reduces flight stability.")]
    public float instabilityFactor = 0.5f;

    private Rigidbody _rb;
    private FlightForceReceiver _receiver;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _receiver = GetComponent<FlightForceReceiver>();
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

    void FixedUpdate()
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
            
            // Apply force to the center (No Torque/Rotation generated)
            // Use ForceMode.Force for continuous pushing
            _rb.AddForce(finalForce, ForceMode.Force);

            // Apply Instability
            // Even without rotation, strong wind should still make the flight feel less stable/controlled
            float turbulence = finalForce.magnitude;
            if (turbulence > 0)
            {
                float destabilizeAmount = (turbulence / _rb.mass) * instabilityFactor * Time.fixedDeltaTime;
                _receiver.AddExternalForce(Vector3.zero, ForceMode.Force, destabilizeAmount);
            }
        }
    }
}