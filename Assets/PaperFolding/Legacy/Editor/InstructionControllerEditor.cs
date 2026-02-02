using UnityEngine;
using UnityEditor;

namespace PaperFolding
{
    [CustomEditor(typeof(InstructionController))]
    public class InstructionControllerEditor : Editor
    {
        private InstructionController controller;
        private FoldingInstructions testInstructions;

        private void OnEnable()
        {
            controller = (InstructionController)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            DrawWalkthroughControls();
        }

        private void DrawWalkthroughControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Interactive Walkthrough Controls", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use walkthrough controls", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            // Status display
            if (controller.IsWalkingThrough)
            {
                EditorGUILayout.LabelField("Status:", "Walking Through", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Step:", $"{controller.CurrentStepIndex + 1} / {controller.TotalSteps}", EditorStyles.miniLabel);
                
                var currentStep = controller.GetCurrentStep();
                if (currentStep != null)
                {
                    EditorGUILayout.LabelField("Current Step:", currentStep.GetDisplayName(), EditorStyles.wordWrappedMiniLabel);
                }
            }
            else
            {
                EditorGUILayout.LabelField("Status:", "Not Active", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(5);

            // Instruction selector
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Instructions:", GUILayout.Width(80));
            testInstructions = (FoldingInstructions)EditorGUILayout.ObjectField(testInstructions, typeof(FoldingInstructions), false);
            EditorGUILayout.EndHorizontal();

            // Start walkthrough button
            GUI.backgroundColor = Color.green;
            GUI.enabled = testInstructions != null && !controller.IsWalkingThrough;
            if (GUILayout.Button("Start Walkthrough", GUILayout.Height(30)))
            {
                controller.WalkThrough(testInstructions);
            }
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            // Control buttons
            EditorGUILayout.BeginHorizontal();

            // Confirm button
            GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
            GUI.enabled = controller.IsWalkingThrough;
            if (GUILayout.Button("Confirm Step", GUILayout.Height(25)))
            {
                controller.ConfirmCurrentStep();
            }
            GUI.enabled = true;

            // Stop button
            GUI.backgroundColor = Color.red;
            GUI.enabled = controller.IsWalkingThrough;
            if (GUILayout.Button("Stop", GUILayout.Height(25)))
            {
                controller.StopWalkthrough();
            }
            GUI.enabled = true;

            // Reset button
            GUI.backgroundColor = new Color(0.7f, 0.7f, 1f);
            if (GUILayout.Button("Reset", GUILayout.Height(25)))
            {
                controller.ResetToInitial();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // Help text
            if (controller.IsWalkingThrough)
            {
                var currentStep = controller.GetCurrentStep();
                if (currentStep is FoldStepData)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox(
                        "1. Drag the handle to position the fold\n" +
                        "2. Click 'Confirm Step' when ready\n" +
                        "3. The fold will be applied automatically",
                        MessageType.Info
                    );
                }
                else if (currentStep is CameraMoveStep)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox(
                        "Camera is moving...\n" +
                        "Will advance automatically when complete",
                        MessageType.Info
                    );
                }
            }

            EditorGUILayout.EndVertical();
        }
    }
}
