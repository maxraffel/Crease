using UnityEngine;
using System.Collections.Generic;

namespace PaperFolding
{
    /// <summary>
    /// Game manager for folding instruction sequences with interactive UI.
    /// Allows selection of different folding patterns, step-by-step walkthrough with keyboard controls,
    /// and completion handling for folded meshes.
    /// </summary>
    public class FoldGameManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PaperMesh paperMesh;
        [SerializeField] private InstructionController instructionController;
        [SerializeField] private FoldController foldController;

        [Header("Folding Instructions")]
        [SerializeField] private List<FoldingInstructions> instructionsList = new List<FoldingInstructions>();

        [Header("UI Settings")]
        [SerializeField] private Rect menuAreaRect = new Rect(10, 10, 300, 500);
        [SerializeField] private float buttonHeight = 30f;
        [SerializeField] private float spacing = 5f;

        // State tracking
        private bool isInWalkthrough = false;
        private bool showContinueButton = false;
        private Vector2 scrollPosition = Vector2.zero;
        private MeshRenderer paperMeshRenderer;

        // Accuracy tracking for display
        private float displayLastAccuracy = 0f;
        private float displayAverageAccuracy = 0f;

        // Timer tracking
        private float walkthroughStartTime = 0f;
        private float walkthroughEndTime = 0f;
        private bool timerRunning = false;

        private void Start()
        {
            if (paperMesh == null)
                paperMesh = GetComponent<PaperMesh>();

            if (instructionController == null)
                instructionController = GetComponent<InstructionController>();

            if (foldController == null)
                foldController = GetComponent<FoldController>();

            // Cache the MeshRenderer
            if (paperMesh != null)
            {
                paperMeshRenderer = paperMesh.GetComponent<MeshRenderer>();
            }

            // Subscribe to walkthrough completion event
            if (instructionController != null)
            {
                instructionController.OnWalkthroughComplete += HandleWalkthroughComplete;
                instructionController.OnStepChanged += HandleStepChanged;
                instructionController.OnAccuracyUpdated += HandleAccuracyUpdated;
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (instructionController != null)
            {
                instructionController.OnWalkthroughComplete -= HandleWalkthroughComplete;
                instructionController.OnStepChanged -= HandleStepChanged;
                instructionController.OnAccuracyUpdated -= HandleAccuracyUpdated;
            }
        }

        private void Update()
        {
            // Keyboard controls for walkthrough
            if (isInWalkthrough && Input.GetKeyDown(KeyCode.Space))
            {
                if (instructionController != null)
                {
                    instructionController.ConfirmCurrentStep();
                }
            }

            // Escape to cancel walkthrough
            if (isInWalkthrough && Input.GetKeyDown(KeyCode.Escape))
            {
                CancelWalkthrough();
            }
        }

        private float GetElapsedTime()
        {
            if (timerRunning)
                return Time.time - walkthroughStartTime;
            else
                return walkthroughEndTime - walkthroughStartTime;
        }

        private string FormatTime(float timeInSeconds)
        {
            int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
            int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
            int milliseconds = Mathf.FloorToInt((timeInSeconds * 100f) % 100f);
            return string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, milliseconds);
        }

        private void OnGUI()
        {
            // Draw solid background
            GUI.Box(menuAreaRect, "");
            
            GUILayout.BeginArea(menuAreaRect);
            GUILayout.Box("Fold Game Manager");

            if (!isInWalkthrough)
            {
                DrawInstructionSelectionUI();
            }
            else
            {
                DrawWalkthroughUI();
            }

            GUILayout.EndArea();
        }

        private void DrawInstructionSelectionUI()
        {
            GUILayout.Label("Select a Folding Pattern:", GUI.skin.box);
            GUILayout.Space(spacing);

            // Scrollable list of instruction buttons
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));

            for (int i = 0; i < instructionsList.Count; i++)
            {
                var instructions = instructionsList[i];
                if (instructions == null)
                    continue;

                string buttonLabel = string.IsNullOrEmpty(instructions.sequenceName) 
                    ? $"Instructions {i + 1}" 
                    : instructions.sequenceName;

                if (GUILayout.Button(buttonLabel, GUILayout.Height(buttonHeight)))
                {
                    StartWalkthrough(instructions);
                }

                GUILayout.Space(spacing);
            }

            GUILayout.EndScrollView();

            GUILayout.Space(spacing * 2);

            if (GUILayout.Button("Reset Paper", GUILayout.Height(buttonHeight)))
            {
                ResetPaper();
            }
        }

        private void DrawWalkthroughUI()
        {
            // Only show walkthrough UI if not complete
            if (!showContinueButton)
            {
                GUILayout.Label("Walkthrough in Progress", GUI.skin.box);
                GUILayout.Space(spacing);

                if (instructionController != null)
                {
                    // Display current step number
                    int currentStep = instructionController.CurrentStepIndex + 1;
                    int totalSteps = instructionController.TotalSteps;

                    GUILayout.Label($"Step {currentStep} of {totalSteps}", GUI.skin.box);

                    GUILayout.Space(spacing * 2);

                    // Display timer
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Time:", GUILayout.Width(80));
                    var timeStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
                    GUILayout.Label(FormatTime(GetElapsedTime()), timeStyle);
                    GUILayout.EndHorizontal();

                    GUILayout.Space(spacing * 2);

                    // Display accuracy scores if any folds have been scored
                    if (instructionController.ScoredFoldsCount > 0)
                    {
                        GUILayout.Label("Accuracy Scores:", GUI.skin.box);
                        
                        // Last fold accuracy
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Last Fold:", GUILayout.Width(80));
                        GUILayout.Label($"{displayLastAccuracy:F1}%", EditorColoredLabel(displayLastAccuracy));
                        GUILayout.EndHorizontal();

                        // Average accuracy
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Average:", GUILayout.Width(80));
                        GUILayout.Label($"{displayAverageAccuracy:F1}%", EditorColoredLabel(displayAverageAccuracy));
                        GUILayout.EndHorizontal();

                        GUILayout.Space(spacing * 2);
                    }

                    // Controls
                    GUILayout.Label("Controls:", GUI.skin.box);
                    GUILayout.Label("SPACE - Confirm & Next Step");
                    GUILayout.Label("ESC - Cancel Walkthrough");
                    GUILayout.Label("CLICK on the red sphere to drag and fold. Click on it again to restart the fold.");

                    GUILayout.Space(spacing * 2);

                    // Manual confirm button
                    if (GUILayout.Button("Confirm Step (Space)", GUILayout.Height(buttonHeight)))
                    {
                        instructionController.ConfirmCurrentStep();
                    }

                    GUILayout.Space(spacing);

                    if (GUILayout.Button("Cancel Walkthrough (Esc)", GUILayout.Height(buttonHeight)))
                    {
                        CancelWalkthrough();
                    }
                }
            }

            // Show continue button if walkthrough is complete
            if (showContinueButton)
            {
                GUILayout.Label("Walkthrough Complete!", GUI.skin.box);
                GUILayout.Space(spacing * 2);

                // Display final time
                GUILayout.Label("Final Scores:", GUI.skin.box);
                
                GUILayout.BeginHorizontal();
                GUILayout.Label("Time:", GUILayout.Width(130));
                var timeStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
                GUILayout.Label(FormatTime(GetElapsedTime()), timeStyle);
                GUILayout.EndHorizontal();

                // Display final accuracy scores if any folds were scored
                if (instructionController != null && instructionController.ScoredFoldsCount > 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Average Accuracy:", GUILayout.Width(130));
                    GUILayout.Label($"{displayAverageAccuracy:F1}%", EditorColoredLabel(displayAverageAccuracy));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Folds Scored:", GUILayout.Width(130));
                    GUILayout.Label($"{instructionController.ScoredFoldsCount}");
                    GUILayout.EndHorizontal();

                    GUILayout.Space(spacing * 2);
                }

                if (GUILayout.Button("Continue", GUILayout.Height(buttonHeight * 1.5f)))
                {
                    OnContinuePressed();
                }
            }
        }

        /// <summary>
        /// Get a colored GUIStyle based on accuracy score
        /// </summary>
        private GUIStyle EditorColoredLabel(float accuracy)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 24; // Larger font size for accuracy numbers

            if (accuracy >= 90f)
                style.normal.textColor = Color.green;
            else if (accuracy >= 75f)
                style.normal.textColor = Color.yellow;
            else if (accuracy >= 50f)
                style.normal.textColor = new Color(1f, 0.5f, 0f); // Orange
            else
                style.normal.textColor = Color.red;

            return style;
        }

        #region Walkthrough Control

        private void StartWalkthrough(FoldingInstructions instructions)
        {
            if (instructionController == null)
            {
                Debug.LogWarning("InstructionController not assigned!");
                return;
            }

            // Reset paper before starting
            ResetPaper();

            // Reset accuracy display
            displayLastAccuracy = 0f;
            displayAverageAccuracy = 0f;

            // Disable paper mesh renderer and enable preview
            if (paperMeshRenderer != null)
            {
                paperMeshRenderer.enabled = false;
            }

            if (foldController != null)
            {
                foldController.TogglePreview(true);
            }

            // Start the walkthrough
            instructionController.WalkThrough(instructions);
            isInWalkthrough = true;
            showContinueButton = false;

            // Start timer
            walkthroughStartTime = Time.time;
            timerRunning = true;

            Debug.Log($"Started walkthrough: {instructions.sequenceName}");
        }

        private void CancelWalkthrough()
        {
            if (instructionController != null)
            {
                instructionController.StopWalkthrough();
            }

            // Re-enable paper mesh renderer and disable preview
            if (paperMeshRenderer != null)
            {
                paperMeshRenderer.enabled = true;
            }

            if (foldController != null)
            {
                foldController.TogglePreview(false);
            }

            isInWalkthrough = false;
            showContinueButton = false;

            // Stop timer
            timerRunning = false;

            Debug.Log("Walkthrough cancelled");
        }

        private void ResetPaper()
        {
            if (paperMesh != null)
            {
                paperMesh.Reset();
            }

            if (instructionController != null)
            {
                instructionController.ResetToInitial();
            }

            // Re-enable paper mesh renderer and disable preview
            if (paperMeshRenderer != null)
            {
                paperMeshRenderer.enabled = true;
            }

            if (foldController != null)
            {
                foldController.TogglePreview(false);
            }

            isInWalkthrough = false;
            showContinueButton = false;

            // Reset timer
            timerRunning = false;
            walkthroughStartTime = 0f;
            walkthroughEndTime = 0f;
        }

        #endregion

        #region Event Handlers

        private void HandleWalkthroughComplete()
        {
            Debug.Log("Walkthrough complete! Showing continue button.");
            showContinueButton = true;

            // Stop timer
            timerRunning = false;
            walkthroughEndTime = Time.time;

            // Re-enable paper mesh renderer and disable preview
            if (paperMeshRenderer != null)
            {
                paperMeshRenderer.enabled = true;
            }

            if (foldController != null)
            {
                foldController.TogglePreview(false);
            }
        }

        private void HandleStepChanged(int currentStep, int totalSteps)
        {
            Debug.Log($"Step changed: {currentStep + 1}/{totalSteps}");
        }

        private void HandleAccuracyUpdated(float lastAccuracy, float averageAccuracy)
        {
            displayLastAccuracy = lastAccuracy;
            displayAverageAccuracy = averageAccuracy;
        }

        #endregion

        #region Continue Handler (Stub)

        /// <summary>
        /// Called when the user presses the "Continue" button after completing a folding sequence.
        /// This is a stub function - customize it to use the folded mesh in your game.
        /// </summary>
        private void OnContinuePressed()
        {
            if (paperMesh == null)
            {
                Debug.LogWarning("No paper mesh available!");
                return;
            }

            // Get the folded mesh
            Mesh foldedMesh = paperMesh.GetMesh();

            // Get the final accuracy score and time
            float finalAccuracy = displayAverageAccuracy;
            int scoredFoldsCount = instructionController != null ? instructionController.ScoredFoldsCount : 0;
            float completionTime = GetElapsedTime();

            // TODO: Use the folded mesh and accuracy score in your game
            // Examples:
            // - Save the mesh as an asset
            // - Pass it to a flight controller with accuracy-based modifiers
            // - Instantiate a new object with this mesh
            // - Store accuracy score for leaderboards/progression
            // - Unlock content based on accuracy thresholds
            
            Debug.Log($"Continue pressed! Folded mesh has {foldedMesh.vertexCount} vertices and {foldedMesh.triangles.Length / 3} triangles.");
            Debug.Log($"Final accuracy: {finalAccuracy:F1}% (based on {scoredFoldsCount} scored folds)");
            Debug.Log($"Completion time: {FormatTime(completionTime)}");
            Debug.Log("TODO: Implement custom logic to use the folded mesh, accuracy score, and completion time in your game.");

            // Reset state for next folding session
            isInWalkthrough = false;
            showContinueButton = false;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Add a folding instruction to the list
        /// </summary>
        public void AddInstruction(FoldingInstructions instruction)
        {
            if (instruction != null && !instructionsList.Contains(instruction))
            {
                instructionsList.Add(instruction);
            }
        }

        /// <summary>
        /// Remove a folding instruction from the list
        /// </summary>
        public void RemoveInstruction(FoldingInstructions instruction)
        {
            instructionsList.Remove(instruction);
        }

        /// <summary>
        /// Clear all instructions
        /// </summary>
        public void ClearInstructions()
        {
            instructionsList.Clear();
        }

        /// <summary>
        /// Get the current folded mesh (if walkthrough is complete)
        /// </summary>
        public Mesh GetFoldedMesh()
        {
            return paperMesh != null ? paperMesh.GetMesh() : null;
        }

        /// <summary>
        /// Check if currently in a walkthrough
        /// </summary>
        public bool IsInWalkthrough => isInWalkthrough;

        /// <summary>
        /// Check if walkthrough is complete and ready to continue
        /// </summary>
        public bool IsComplete => showContinueButton;

        #endregion
    }
}
