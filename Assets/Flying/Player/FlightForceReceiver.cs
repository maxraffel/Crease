using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(FlightController))]
public class FlightForceReceiver : MonoBehaviour
{
    private Rigidbody _rb;
    private FlightController _flightController;
    private float _defaultStability;
    private Coroutine _recoveryCoroutine;

    [Header("Impact Settings")]
    [Tooltip("How fast stability recovers after an impact.")]
    [SerializeField] private float stabilityRecoverySpeed = 2.0f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _flightController = GetComponent<FlightController>();
        _defaultStability = _flightController.stability;
    }

    /// <summary>
    /// Apply an instantaneous force (Impulse) that also destabilizes the flight.
    /// Great for explosions, collisions, or strong wind gusts.
    /// </summary>
    /// <param name="force">The force vector.</param>
    /// <param name="destabilizeAmount">How much to reduce stability (0-1). 1 means set stability to 0.</param>
    public void AddImpact(Vector3 force, float destabilizeAmount = 1.0f)
    {
        _rb.AddForce(force, ForceMode.Impulse);
        ApplyInstability(destabilizeAmount);
    }

    /// <summary>
    /// Apply a continuous force. This works like standard AddForce but ensures the flight controller
    /// doesn't immediately fight it if destabilize is true.
    /// </summary>
    public void AddExternalForce(Vector3 force, ForceMode mode = ForceMode.Force, float destabilizeAmount = 0.1f)
    {
        _rb.AddForce(force, mode);
        ApplyInstability(destabilizeAmount);
    }

    /// <summary>
    /// Adds a standard explosion force and destabilizes flight.
    /// </summary>
    public void AddExplosionForce(float explosionForce, Vector3 explosionPosition, float explosionRadius, float upwardsModifier = 0.0f, float destabilizeAmount = 1.0f)
    {
        _rb.AddExplosionForce(explosionForce, explosionPosition, explosionRadius, upwardsModifier, ForceMode.Impulse);
        ApplyInstability(destabilizeAmount);
    }

    private void ApplyInstability(float amount)
    {
        // Reduce current stability
        float newStability = Mathf.Clamp01(_flightController.stability - amount);
        _flightController.stability = newStability;

        // Restart recovery routine
        if (_recoveryCoroutine != null) StopCoroutine(_recoveryCoroutine);
        _recoveryCoroutine = StartCoroutine(RecoverStability());
    }

    private IEnumerator RecoverStability()
    {
        while (_flightController.stability < _defaultStability)
        {
            _flightController.stability += Time.deltaTime * stabilityRecoverySpeed;
            if (_flightController.stability > _defaultStability)
                _flightController.stability = _defaultStability;
            
            yield return null;
        }
        _recoveryCoroutine = null;
    }
}
