using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace PaperFolding
{
    /// <summary>
    /// Executes a FoldingInstructions sequence at runtime
    /// </summary>
    public class FoldingInstructionsPlayer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FoldingInstructions instructions;
        [SerializeField] private PaperMesh paperMesh;
        [SerializeField] private FoldController foldController;
        [SerializeField] private Camera targetCamera;

        [Header("Playback Control")]
        [SerializeField] private bool playOnStart = false;
        [SerializeField] private int currentStepIndex = 0;
        [SerializeField] private bool isPlaying = false;

        [Header("Camera Settings")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float defaultCameraDistance = 10f;

        private Coroutine playbackCoroutine;
        private Vector3 initialCameraPosition;
        private Quaternion initialCameraRotation;

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

            if (playOnStart && instructions != null)
            {
                Play();
            }
        }

        /// <summary>
        /// Start playing the instruction sequence from the beginning
        /// </summary>
        public void Play()
        {
            if (instructions == null)
            {
                Debug.LogWarning("No FoldingInstructions assigned!");
                return;
            }

            Stop();
            currentStepIndex = 0;
            playbackCoroutine = StartCoroutine(PlaySequence());
        }

        /// <summary>
        /// Resume playing from current step
        /// </summary>
        public void Resume()
        {
            if (instructions == null) return;
            
            if (!isPlaying)
            {
                playbackCoroutine = StartCoroutine(PlaySequence());
            }
        }

        /// <summary>
        /// Pause playback
        /// </summary>
        public void Pause()
        {
            if (playbackCoroutine != null)
            {
                StopCoroutine(playbackCoroutine);
                playbackCoroutine = null;
                isPlaying = false;
            }
        }

        /// <summary>
        /// Stop playback and reset
        /// </summary>
        public void Stop()
        {
            if (playbackCoroutine != null)
            {
                StopCoroutine(playbackCoroutine);
                playbackCoroutine = null;
            }
            isPlaying = false;
            currentStepIndex = 0;
        }

        /// <summary>
        /// Execute a single step
        /// </summary>
        public void ExecuteStep(int stepIndex)
        {
            if (instructions == null || stepIndex < 0 || stepIndex >= instructions.steps.Count)
                return;

            StartCoroutine(ExecuteStepCoroutine(instructions.steps[stepIndex], stepIndex));
        }

        /// <summary>
        /// Reset paper and camera to initial state
        /// </summary>
        public void Reset()
        {
            Stop();
            
            if (paperMesh != null)
            {
                paperMesh.Reset();
            }

            if (targetCamera != null)
            {
                targetCamera.transform.position = initialCameraPosition;
                targetCamera.transform.rotation = initialCameraRotation;
            }

            currentStepIndex = 0;
        }

        private IEnumerator PlaySequence()
        {
            isPlaying = true;

            while (currentStepIndex < instructions.steps.Count)
            {
                var step = instructions.steps[currentStepIndex];
                yield return ExecuteStepCoroutine(step, currentStepIndex);

                currentStepIndex++;

                // Small delay between steps
                yield return new WaitForSeconds(0.1f);
            }

            isPlaying = false;

            // Loop if enabled
            if (instructions.loop)
            {
                Reset();
                Play();
            }
        }

        private IEnumerator ExecuteStepCoroutine(FoldStep step, int stepIndex)
        {
            if (step is FoldStepData foldStep)
            {
                yield return ExecuteFoldStep(foldStep, stepIndex);
            }
            else if (step is CameraMoveStep cameraStep)
            {
                yield return ExecuteCameraStep(cameraStep);
            }
        }

        private IEnumerator ExecuteFoldStep(FoldStepData foldStep, int stepIndex)
        {
            if (paperMesh == null)
            {
                Debug.LogWarning("PaperMesh not assigned!");
                yield break;
            }

            // Compute fold axis from handle position
            Vector2 handleUV = foldStep.handleUV;
            
            // For now, we need to compute the fold axis
            // This is a simplified version - you may want to use FoldController's logic
            FoldAxis axis = ComputeFoldAxisFromHandle(handleUV);

            // Build predicate from tag expression
            System.Func<HashSet<string>, bool> predicate = null;
            if (!string.IsNullOrEmpty(foldStep.tagExpression))
            {
                predicate = (tags) => BooleanExpressionEvaluator.Evaluate(foldStep.tagExpression, tags);
            }

            // Execute fold
            if (foldStep.duration > 0f)
            {
                // Animated fold
                yield return AnimatedFold(axis, foldStep.foldAngle, foldStep.duration, foldStep.tagName, predicate);
            }
            else
            {
                // Instant fold
                paperMesh.Fold(axis, foldStep.foldAngle, foldStep.tagName, predicate);
            }
        }

        private IEnumerator AnimatedFold(FoldAxis axis, float targetAngle, float duration, string tag, 
            System.Func<HashSet<string>, bool> predicate)
        {
            float elapsed = 0f;
            float startAngle = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float currentAngle = Mathf.Lerp(startAngle, targetAngle, t);

                // Apply incremental fold
                float deltaAngle = currentAngle - startAngle;
                if (Mathf.Abs(deltaAngle) > 0.01f)
                {
                    paperMesh.Fold(axis, deltaAngle, t >= 1f ? tag : null, predicate);
                    startAngle = currentAngle;
                }

                yield return null;
            }

            // Ensure final angle and tag application
            paperMesh.Fold(axis, targetAngle, tag, predicate);
        }

        private IEnumerator ExecuteCameraStep(CameraMoveStep cameraStep)
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
        }

        private Vector3 CalculateCameraPosition(float distance, Vector3 rotation)
        {
            // Calculate camera position based on rotation and distance
            Quaternion rot = Quaternion.Euler(rotation);
            Vector3 direction = rot * Vector3.back; // Camera looks along -Z
            
            Vector3 targetPoint = paperMesh != null ? paperMesh.transform.position : Vector3.zero;
            if (cameraPivot != null)
            {
                targetPoint = cameraPivot.position;
            }

            return targetPoint + direction * distance;
        }

        private FoldAxis ComputeFoldAxisFromHandle(Vector2 handleUV)
        {
            // Snap handle to nearest edge
            Vector2 snapped = SnapToEdge(handleUV);
            
            // Create a fold axis perpendicular to the edge
            // This is a simple implementation - you may want more sophisticated logic
            
            if (Mathf.Approximately(snapped.y, 0f))
            {
                // Bottom edge - horizontal fold
                return new FoldAxis(0f, snapped.x, 1f, snapped.x);
            }
            else if (Mathf.Approximately(snapped.y, 1f))
            {
                // Top edge - horizontal fold
                return new FoldAxis(0f, snapped.x, 1f, snapped.x);
            }
            else if (Mathf.Approximately(snapped.x, 0f))
            {
                // Left edge - vertical fold
                return new FoldAxis(snapped.y, 0f, snapped.y, 1f);
            }
            else
            {
                // Right edge - vertical fold
                return new FoldAxis(snapped.y, 0f, snapped.y, 1f);
            }
        }

        private Vector2 SnapToEdge(Vector2 uv)
        {
            float minDist = float.MaxValue;
            Vector2 snapped = uv;

            Vector2[] edges = new Vector2[]
            {
                new Vector2(uv.x, 0f),
                new Vector2(uv.x, 1f),
                new Vector2(0f, uv.y),
                new Vector2(1f, uv.y)
            };

            foreach (var edge in edges)
            {
                float dist = Vector2.Distance(uv, edge);
                if (dist < minDist)
                {
                    minDist = dist;
                    snapped = edge;
                }
            }

            return snapped;
        }

        #region Properties

        public FoldingInstructions Instructions
        {
            get => instructions;
            set => instructions = value;
        }

        public int CurrentStepIndex
        {
            get => currentStepIndex;
            set => currentStepIndex = Mathf.Clamp(value, 0, instructions != null ? instructions.steps.Count : 0);
        }

        public bool IsPlaying => isPlaying;

        public int TotalSteps => instructions != null ? instructions.steps.Count : 0;

        #endregion
    }
}
