using UnityEngine;

namespace PaperFolding.Examples
{
    /// <summary>
    /// Demonstrates the delayed fold angle preview behavior
    /// The preview starts at 0 degrees and only shows the target angle once dragging begins
    /// </summary>
    public class DelayedAnglePreviewDemo : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InstructionController instructionController;
        [SerializeField] private FoldingInstructions testInstructions;

        [Header("Demo Controls")]
        [SerializeField] private bool startDemoOnPlay = false;

        private void Start()
        {
            if (instructionController == null)
                instructionController = GetComponent<InstructionController>();

            if (startDemoOnPlay && testInstructions != null)
            {
                StartDemo();
            }
        }

        [ContextMenu("Start Demo")]
        public void StartDemo()
        {
            if (instructionController == null)
            {
                Debug.LogError("InstructionController not assigned!");
                return;
            }

            if (testInstructions == null)
            {
                Debug.LogError("No test instructions assigned!");
                return;
            }

            Debug.Log("=== Starting Delayed Angle Preview Demo ===");
            Debug.Log("Notice how the preview starts flat (0°) and only shows the target angle once you drag the handle.");
            
            instructionController.WalkThrough(testInstructions);
        }

        [ContextMenu("Stop Demo")]
        public void StopDemo()
        {
            if (instructionController != null)
            {
                instructionController.StopWalkthrough();
                Debug.Log("Demo stopped");
            }
        }

        [ContextMenu("Reset")]
        public void Reset()
        {
            if (instructionController != null)
            {
                instructionController.ResetToInitial();
                Debug.Log("Reset to initial state");
            }
        }

        private void OnGUI()
        {
            if (!instructionController.IsWalkingThrough)
                return;

            // Display helpful info
            GUILayout.BeginArea(new Rect(10, 10, 400, 200));
            GUILayout.BeginVertical(GUI.skin.box);
            
            GUILayout.Label("Delayed Angle Preview Demo", GUI.skin.box);
            GUILayout.Space(5);
            
            var currentStep = instructionController.GetCurrentStep();
            if (currentStep is FoldStepData foldStep)
            {
                GUILayout.Label($"Step {instructionController.CurrentStepIndex + 1} of {instructionController.TotalSteps}");
                GUILayout.Label($"Target Angle: {foldStep.foldAngle}°");
                GUILayout.Space(5);
                GUILayout.Label("Instructions:", GUI.skin.box);
                GUILayout.Label("1. The preview is currently flat (0°)");
                GUILayout.Label("2. Click and drag the handle");
                GUILayout.Label("3. Preview will animate to target angle");
                GUILayout.Label("4. Release far from start to confirm");
                GUILayout.Label("5. Release near start to cancel");
            }
            else if (currentStep is CameraMoveStep)
            {
                GUILayout.Label($"Camera Move Step {instructionController.CurrentStepIndex + 1} of {instructionController.TotalSteps}");
                GUILayout.Label("Camera is moving...");
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
