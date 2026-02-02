using UnityEngine;
using UnityEditor;

namespace PaperFolding
{
    [CustomEditor(typeof(FoldingInstructionsPlayer))]
    public class FoldingInstructionsPlayerEditor : Editor
    {
        private FoldingInstructionsPlayer player;

        private void OnEnable()
        {
            player = (FoldingInstructionsPlayer)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            DrawPlaybackControls();
            EditorGUILayout.Space(5);
            DrawProgressBar();
        }

        private void DrawPlaybackControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Playback Controls", EditorStyles.boldLabel);

            if (player.Instructions == null)
            {
                EditorGUILayout.HelpBox("No FoldingInstructions assigned!", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            // Status
            string status = player.IsPlaying ? "▶ Playing" : "⏸ Paused";
            EditorGUILayout.LabelField($"Status: {status}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Step: {player.CurrentStepIndex + 1} / {player.TotalSteps}", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            // Control buttons
            EditorGUILayout.BeginHorizontal();

            // Play button
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("▶ Play", GUILayout.Height(30)))
            {
                if (Application.isPlaying)
                {
                    player.Play();
                }
                else
                {
                    Debug.LogWarning("Enter Play Mode to execute folding instructions.");
                }
            }

            // Pause button
            GUI.backgroundColor = Color.yellow;
            GUI.enabled = player.IsPlaying;
            if (GUILayout.Button("⏸ Pause", GUILayout.Height(30)))
            {
                if (Application.isPlaying)
                {
                    player.Pause();
                }
            }
            GUI.enabled = true;

            // Stop button
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("⏹ Stop", GUILayout.Height(30)))
            {
                if (Application.isPlaying)
                {
                    player.Stop();
                }
            }

            // Reset button
            GUI.backgroundColor = new Color(0.7f, 0.7f, 1f);
            if (GUILayout.Button("⟲ Reset", GUILayout.Height(30)))
            {
                if (Application.isPlaying)
                {
                    player.Reset();
                }
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // Step controls
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField("Manual Step Control:", GUILayout.Width(130));
            
            GUI.enabled = player.CurrentStepIndex > 0;
            if (GUILayout.Button("◀ Prev", EditorStyles.miniButtonLeft, GUILayout.Width(60)))
            {
                if (Application.isPlaying)
                {
                    player.CurrentStepIndex--;
                    player.ExecuteStep(player.CurrentStepIndex);
                }
            }
            GUI.enabled = true;

            GUI.enabled = player.CurrentStepIndex < player.TotalSteps - 1;
            if (GUILayout.Button("Next ▶", EditorStyles.miniButtonRight, GUILayout.Width(60)))
            {
                if (Application.isPlaying)
                {
                    player.CurrentStepIndex++;
                    player.ExecuteStep(player.CurrentStepIndex);
                }
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            // Step list
            if (player.Instructions.steps.Count > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Steps:", EditorStyles.boldLabel);
                
                for (int i = 0; i < Mathf.Min(player.Instructions.steps.Count, 10); i++)
                {
                    var step = player.Instructions.steps[i];
                    bool isCurrent = (i == player.CurrentStepIndex);
                    
                    EditorGUILayout.BeginHorizontal();
                    
                    string icon = step is FoldStepData ? "📄" : "🎥";
                    string prefix = isCurrent ? "→ " : "  ";
                    
                    GUIStyle labelStyle = isCurrent ? EditorStyles.boldLabel : EditorStyles.miniLabel;
                    EditorGUILayout.LabelField($"{prefix}{icon} [{i}] {step.GetStepType()}", labelStyle, GUILayout.Width(80));
                    
                    if (GUILayout.Button("Execute", EditorStyles.miniButton, GUILayout.Width(60)))
                    {
                        if (Application.isPlaying)
                        {
                            player.ExecuteStep(i);
                        }
                        else
                        {
                            Debug.LogWarning("Enter Play Mode to execute steps.");
                        }
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }

                if (player.Instructions.steps.Count > 10)
                {
                    EditorGUILayout.LabelField($"... and {player.Instructions.steps.Count - 10} more steps", EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawProgressBar()
        {
            if (player.Instructions == null || player.TotalSteps == 0)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            float progress = player.TotalSteps > 0 ? (float)player.CurrentStepIndex / player.TotalSteps : 0f;
            
            Rect rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(rect, progress, $"{player.CurrentStepIndex} / {player.TotalSteps}");
            
            EditorGUILayout.EndVertical();
        }
    }
}
