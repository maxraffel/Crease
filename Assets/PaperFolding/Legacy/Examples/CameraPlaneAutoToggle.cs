using UnityEngine;

namespace PaperFolding.Examples
{
    /// <summary>
    /// Example script demonstrating dynamic camera plane toggling based on camera angle
    /// </summary>
    public class CameraPlaneAutoToggle : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FoldController foldController;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private PaperMesh paperMesh;

        [Header("Settings")]
        [Tooltip("Angle threshold (in degrees) - camera plane mode activates when camera is more oblique than this")]
        [SerializeField] private float angleThreshold = 30f;
        
        [Tooltip("Enable automatic toggling based on camera angle")]
        [SerializeField] private bool autoToggle = true;

        private void Start()
        {
            if (foldController == null)
                foldController = GetComponent<FoldController>();

            if (paperMesh == null)
                paperMesh = GetComponent<PaperMesh>();

            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        private void Update()
        {
            if (!autoToggle || foldController == null || targetCamera == null || paperMesh == null)
                return;

            // Calculate angle between camera forward and paper normal
            Vector3 paperNormal = paperMesh.transform.TransformDirection(Vector3.forward);
            Vector3 cameraForward = targetCamera.transform.forward;
            
            float angle = Vector3.Angle(paperNormal, -cameraForward); // Negative because camera looks along -Z

            // Enable camera plane mode when viewing at an oblique angle
            bool shouldUseCameraPlane = angle > angleThreshold;
            
            if (foldController.UseCameraPlane != shouldUseCameraPlane)
            {
                foldController.UseCameraPlane = shouldUseCameraPlane;
                Debug.Log($"Camera plane mode: {(shouldUseCameraPlane ? "ENABLED" : "DISABLED")} (angle: {angle:F1}°)");
            }
        }

        /// <summary>
        /// Manually toggle camera plane mode
        /// </summary>
        public void ToggleCameraPlane()
        {
            if (foldController != null)
            {
                foldController.UseCameraPlane = !foldController.UseCameraPlane;
                Debug.Log($"Camera plane mode manually toggled: {foldController.UseCameraPlane}");
            }
        }

        /// <summary>
        /// Enable camera plane mode
        /// </summary>
        public void EnableCameraPlane()
        {
            if (foldController != null)
                foldController.UseCameraPlane = true;
        }

        /// <summary>
        /// Disable camera plane mode (use paper normal)
        /// </summary>
        public void DisableCameraPlane()
        {
            if (foldController != null)
                foldController.UseCameraPlane = false;
        }
    }
}
