using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace PaperFolding
{
    /// <summary>
    /// Interactive instruction controller that guides users through folding sequences step-by-step
    /// </summary>
    public class InstructionController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PaperMesh paperMesh;
        [SerializeField] private FoldController foldController;
        [SerializeField] private Camera targetCamera;

        [Header("Camera Settings")]
        [SerializeField] private Transform cameraPivot;

        [Header("Current State")]
        [SerializeField] private FoldingInstructions currentInstructions;
        [SerializeField] private int currentStepIndex = 0;
        [SerializeField] private bool isWalkingThrough = false;

        private Vector3 initialCameraPosition;
        private Quaternion initialCameraRotation;
        private Coroutine cameraMoveCoroutine;
        
        // Track if user has started interacting with current fold step
        private bool hasStartedDragging = false;
        private float targetFoldAngle = 0f;

        // Accuracy tracking
        private float lastFoldAccuracy = 0f;
        private List<float> accuracyScores = new List<float>();

        // Event for UI to subscribe to
        public System.Action<int, int> OnStepChanged; // currentStep, totalSteps
        public System.Action<FoldStepData> OnFoldStepSetup; // fired when fold is configured
        public System.Action<CameraMoveStep> OnCameraStepStarted;
        public System.Action OnWalkthroughComplete;
        public System.Action<float, float> OnAccuracyUpdated; // lastAccuracy, averageAccuracy

        private void Start()
        {
            if (paperMesh == null)
                paperMesh = GetComponent<PaperMesh>();

            if (foldController == null)
                foldController = GetComponent<FoldController>();

            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera != null)
            {
                initialCameraPosition = targetCamera.transform.position;
                initialCameraRotation = targetCamera.transform.rotation;
            }
        }

        private void Update()
        {
            // Monitor for drag start during walkthrough fold steps
            if (isWalkingThrough && foldController != null && !hasStartedDragging)
            {
                var currentStep = GetCurrentStep();
                if (currentStep is FoldStepData)
                {
                    // Check if user has started dragging
                    if (foldController.IsDragging())
                    {
                        hasStartedDragging = true;
                        // Set the proper fold angle now that user has started interacting
                        foldController.PreviewDegrees = targetFoldAngle;
                        foldController.DragFoldAngle = targetFoldAngle;
                    }
                }
            }
        }

        /// <summary>
        /// Begin interactive walkthrough of a FoldingInstructions sequence
        /// </summary>
        public void WalkThrough(FoldingInstructions instructions)
        {
            if (instructions == null)
            {
                Debug.LogWarning("Cannot walk through null instructions!");
                return;
            }

            if (isWalkingThrough)
            {
                Debug.LogWarning("Already walking through instructions. Call StopWalkthrough() first.");
                return;
            }

            currentInstructions = instructions;
            currentStepIndex = 0;
            isWalkingThrough = true;

            // Reset accuracy tracking
            lastFoldAccuracy = 0f;
            accuracyScores.Clear();

            // Ensure FoldController is in DragHandle mode
            if (foldController != null)
            {
                foldController.CurrentInputMode = FoldController.InputMode.DragHandle;
                foldController.EnableInput = true;
            }

            SetupCurrentStep();
        }

        /// <summary>
        /// Stop the current walkthrough
        /// </summary>
        public void StopWalkthrough()
        {
            isWalkingThrough = false;
            currentInstructions = null;
            currentStepIndex = 0;
            hasStartedDragging = false;
            targetFoldAngle = 0f;

            // Reset accuracy tracking
            lastFoldAccuracy = 0f;
            accuracyScores.Clear();

            if (cameraMoveCoroutine != null)
            {
                StopCoroutine(cameraMoveCoroutine);
                cameraMoveCoroutine = null;
            }
        }

        /// <summary>
        /// Reset paper and camera to initial state
        /// </summary>
        public void ResetToInitial()
        {
            StopWalkthrough();

            if (paperMesh != null)
            {
                paperMesh.Reset();
            }

            if (targetCamera != null)
            {
                targetCamera.transform.position = initialCameraPosition;
                targetCamera.transform.rotation = initialCameraRotation;
            }
        }

        /// <summary>
        /// Confirm and apply the current fold, then advance to next step
        /// </summary>
        public void ConfirmCurrentStep()
        {
            if (!isWalkingThrough || currentInstructions == null)
            {
                Debug.LogWarning("Not currently walking through instructions!");
                return;
            }

            if (currentStepIndex >= currentInstructions.steps.Count)
            {
                Debug.LogWarning("Already at end of instruction sequence!");
                return;
            }

            var step = currentInstructions.steps[currentStepIndex];

            if (step is FoldStepData foldStep)
            {
                ApplyCurrentFold(foldStep);
            }

            // Advance to next step
            currentStepIndex++;

            if (currentStepIndex >= currentInstructions.steps.Count)
            {
                // Walkthrough complete
                CompleteWalkthrough();
            }
            else
            {
                SetupCurrentStep();
            }
        }

        /// <summary>
        /// Skip current step without applying (useful for camera moves)
        /// </summary>
        public void SkipCurrentStep()
        {
            if (!isWalkingThrough || currentInstructions == null)
                return;

            currentStepIndex++;

            if (currentStepIndex >= currentInstructions.steps.Count)
            {
                CompleteWalkthrough();
            }
            else
            {
                SetupCurrentStep();
            }
        }

        private void SetupCurrentStep()
        {
            if (currentInstructions == null || currentStepIndex >= currentInstructions.steps.Count)
                return;

            var step = currentInstructions.steps[currentStepIndex];

            OnStepChanged?.Invoke(currentStepIndex, currentInstructions.steps.Count);

            if (step is FoldStepData foldStep)
            {
                SetupFoldStep(foldStep);
            }
            else if (step is CameraMoveStep cameraStep)
            {
                SetupCameraStep(cameraStep);
            }
        }

        private void SetupFoldStep(FoldStepData foldStep)
        {
            if (foldController == null)
            {
                Debug.LogWarning("FoldController not assigned!");
                return;
            }

            // Configure the FoldController for this fold
            foldController.SetHandleUV(foldStep.handleUV);
            
            // Store target angle but initially set to 0 (will be set when user starts dragging)
            targetFoldAngle = foldStep.foldAngle;
            hasStartedDragging = false;
            foldController.PreviewDegrees = 0f;
            foldController.DragFoldAngle = 0f;
            
            foldController.PreviewTag = foldStep.tagName;
            foldController.UseCameraPlane = foldStep.useCameraPlane;

            // Store the tag expression for later use
            foldController.SetTagExpression(foldStep.tagExpression);

            // Setup correct axis if this step has accuracy tracking
            if (foldStep.hasCorrectAxis)
            {
                foldController.SetCorrectAxis(foldStep.GetCorrectAxis());
                foldController.ShowCorrectAxis(true);
            }
            else
            {
                foldController.ShowCorrectAxis(false);
            }

            // Enable drag mode
            foldController.CurrentInputMode = FoldController.InputMode.DragHandle;
            foldController.EnableInput = true;

            // Fire event for UI
            OnFoldStepSetup?.Invoke(foldStep);
        }

        private void ApplyCurrentFold(FoldStepData foldStep)
        {
            if (foldController == null)
                return;

            // Calculate accuracy if this step has a correct axis
            if (foldStep.hasCorrectAxis)
            {
                lastFoldAccuracy = CalculateFoldAccuracy(foldStep);
                accuracyScores.Add(lastFoldAccuracy);
                
                float averageAccuracy = CalculateAverageAccuracy();
                
                Debug.Log($"Fold Accuracy: {lastFoldAccuracy:F1}% | Average: {averageAccuracy:F1}%");
                
                // Fire accuracy update event
                OnAccuracyUpdated?.Invoke(lastFoldAccuracy, averageAccuracy);
            }

            // Delegate to FoldController which will use the cached spatial axis if available
            foldController.ApplyFold();

            // Reset preview to 0 degrees after applying to prevent showing repeated fold
            foldController.PreviewDegrees = 0f;
            foldController.DragFoldAngle = 0f;
            hasStartedDragging = false;

            // Hide correct axis after applying
            foldController.ShowCorrectAxis(false);
        }

        /// <summary>
        /// Calculate the average accuracy across all scored folds
        /// </summary>
        private float CalculateAverageAccuracy()
        {
            if (accuracyScores.Count == 0)
                return 0f;

            float sum = 0f;
            foreach (float score in accuracyScores)
            {
                sum += score;
            }

            return sum / accuracyScores.Count;
        }

        /// <summary>
        /// Calculate accuracy of the player's fold compared to the correct axis
        /// Returns a score from 0-100
        /// Simple linear scoring based on angle and position in camera plane
        /// </summary>
        private float CalculateFoldAccuracy(FoldStepData foldStep)
        {
            if (foldController == null || paperMesh == null || targetCamera == null)
                return 0f;

            // Get the player's axis and the correct axis
            FoldAxis playerAxis = foldController.GetCurrentFoldAxis();
            FoldAxis correctAxis = foldStep.GetCorrectAxis();

            // Convert both axes to world space
            (Vector3 playerStart, Vector3 playerEnd) = playerAxis.ToWorldSpace(paperMesh);
            (Vector3 correctStart, Vector3 correctEnd) = correctAxis.ToWorldSpace(paperMesh);

            // Get camera plane normal (camera forward)
            Vector3 cameraPlaneNormal = targetCamera.transform.forward;

            // Project all points onto the camera plane
            Vector3 playerStartProj = Vector3.ProjectOnPlane(playerStart, cameraPlaneNormal);
            Vector3 playerEndProj = Vector3.ProjectOnPlane(playerEnd, cameraPlaneNormal);
            Vector3 correctStartProj = Vector3.ProjectOnPlane(correctStart, cameraPlaneNormal);
            Vector3 correctEndProj = Vector3.ProjectOnPlane(correctEnd, cameraPlaneNormal);

            // Get direction vectors in camera plane
            Vector3 playerDir = (playerEndProj - playerStartProj).normalized;
            Vector3 correctDir = (correctEndProj - correctStartProj).normalized;

            // Calculate angle between directions (0-180 degrees)
            float dot = Vector3.Dot(playerDir, correctDir);
            dot = Mathf.Abs(dot); // Fold can go either direction
            dot = Mathf.Clamp(dot, -1f, 1f);
            float angleDegrees = Mathf.Acos(dot) * Mathf.Rad2Deg;

            // Direction score: exponential penalty - extremely harsh even at small deviations
            // exp(-k * angle^2) where k=0.1 for very steep initial drop-off
            float directionScore = Mathf.Exp(-0.1f * angleDegrees * angleDegrees);

            // Calculate position error in camera plane
            Vector3 playerMid = (playerStartProj + playerEndProj) * 0.5f;
            Vector3 correctMid = (correctStartProj + correctEndProj) * 0.5f;
            
            // Perpendicular distance
            Vector3 offset = playerMid - correctMid;
            float perpDistance = Vector3.Cross(correctDir, offset).magnitude;
            
            // Normalize by paper size
            float paperSize = Mathf.Max(paperMesh.Width, paperMesh.Height);
            float normalizedDistance = perpDistance / paperSize;
            
            // Position score: linear from 1.0 at 0 distance to 0.0 at 50% of paper size
            float positionScore = Mathf.Clamp01(1f - (normalizedDistance / 0.5f));

            // Combine scores (50/50 weighting)
            float combinedScore = (directionScore * 0.5f + positionScore * 0.5f);

            // Convert to 0-100 scale
            float finalScore = combinedScore * 100f;

            // Apply score modifier
            finalScore += foldStep.scoreModifier;

            Debug.Log($"Angle: {angleDegrees:F2}°, Perp Dist: {perpDistance:F4}, " +
                      $"Dir: {directionScore:F3}, Pos: {positionScore:F3}, Modifier: {foldStep.scoreModifier:+0;-0;0}, Final: {finalScore:F1}");

            return Mathf.Clamp(finalScore, 0f, 100f);
        }

        private void SetupCameraStep(CameraMoveStep cameraStep)
        {
            OnCameraStepStarted?.Invoke(cameraStep);

            // Start camera movement coroutine
            if (cameraMoveCoroutine != null)
            {
                StopCoroutine(cameraMoveCoroutine);
            }

            cameraMoveCoroutine = StartCoroutine(ExecuteCameraMove(cameraStep));
        }

        private IEnumerator ExecuteCameraMove(CameraMoveStep cameraStep)
        {
            if (targetCamera == null)
            {
                Debug.LogWarning("No camera assigned!");
                yield break;
            }

            Vector3 startPosition = targetCamera.transform.position;
            Quaternion startRotation = targetCamera.transform.rotation;

            // Calculate target position and rotation
            Quaternion targetRotation = Quaternion.Euler(cameraStep.rotation);
            Vector3 targetPosition = CalculateCameraPosition(cameraStep.distance, cameraStep.rotation);

            float elapsed = 0f;
            while (elapsed < cameraStep.duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / cameraStep.duration);
                float easedT = cameraStep.easeCurve.Evaluate(t);

                targetCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, easedT);
                targetCamera.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, easedT);

                yield return null;
            }

            // Ensure final position
            targetCamera.transform.position = targetPosition;
            targetCamera.transform.rotation = targetRotation;

            cameraMoveCoroutine = null;

            // Auto-advance after camera move
            SkipCurrentStep();
        }

        private Vector3 CalculateCameraPosition(float distance, Vector3 rotation)
        {
            Quaternion rot = Quaternion.Euler(rotation);
            Vector3 direction = rot * Vector3.back; // Camera looks along -Z

            Vector3 targetPoint = paperMesh != null ? paperMesh.transform.position : Vector3.zero;
            if (cameraPivot != null)
            {
                targetPoint = cameraPivot.position;
            }

            return targetPoint + direction * distance;
        }

        private void CompleteWalkthrough()
        {
            isWalkingThrough = false;
            OnWalkthroughComplete?.Invoke();
            Debug.Log("Walkthrough complete!");
        }

        #region Public API

        /// <summary>
        /// Check if currently in walkthrough mode
        /// </summary>
        public bool IsWalkingThrough => isWalkingThrough;

        /// <summary>
        /// Get current step index
        /// </summary>
        public int CurrentStepIndex => currentStepIndex;

        /// <summary>
        /// Get total number of steps in current instructions
        /// </summary>
        public int TotalSteps => currentInstructions != null ? currentInstructions.steps.Count : 0;

        /// <summary>
        /// Get the current step
        /// </summary>
        public FoldStep GetCurrentStep()
        {
            if (currentInstructions == null || currentStepIndex >= currentInstructions.steps.Count)
                return null;

            return currentInstructions.steps[currentStepIndex];
        }

        /// <summary>
        /// Get the current instructions being walked through
        /// </summary>
        public FoldingInstructions CurrentInstructions => currentInstructions;

        /// <summary>
        /// Get the last fold's accuracy score (0-100)
        /// </summary>
        public float LastFoldAccuracy => lastFoldAccuracy;

        /// <summary>
        /// Get the average accuracy across all scored folds (0-100)
        /// </summary>
        public float AverageAccuracy => CalculateAverageAccuracy();

        /// <summary>
        /// Get the number of folds that have been scored
        /// </summary>
        public int ScoredFoldsCount => accuracyScores.Count;

        #endregion
    }
}
