using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace PaperFolding
{
    [CustomEditor(typeof(FoldingInstructions))]
    public class FoldingInstructionsEditor : Editor
    {
        private FoldingInstructions instructions;
        private SerializedProperty sequenceNameProp;
        private SerializedProperty descriptionProp;
        private SerializedProperty stepsProp;
        private SerializedProperty autoPlayProp;
        private SerializedProperty loopProp;

        private Vector2 scrollPosition;
        private int selectedStepIndex = -1;
        private bool showValidation = false;
        private bool showTagAnalysis = false;
        
        // For adding new steps
        private bool showAddMenu = false;

        // Color scheme
        private static readonly Color foldColor = new Color(0.4f, 0.7f, 1f, 0.3f);
        private static readonly Color cameraColor = new Color(1f, 0.7f, 0.4f, 0.3f);
        private static readonly Color selectedColor = new Color(0.3f, 1f, 0.3f, 0.3f);
        private static readonly Color warningColor = new Color(1f, 1f, 0.4f, 0.3f);
        private static readonly Color errorColor = new Color(1f, 0.4f, 0.4f, 0.3f);

        private void OnEnable()
        {
            instructions = (FoldingInstructions)target;
            sequenceNameProp = serializedObject.FindProperty("sequenceName");
            descriptionProp = serializedObject.FindProperty("description");
            stepsProp = serializedObject.FindProperty("steps");
            autoPlayProp = serializedObject.FindProperty("autoPlay");
            loopProp = serializedObject.FindProperty("loop");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawMetadata();
            EditorGUILayout.Space(10);

            DrawPlaybackSettings();
            EditorGUILayout.Space(10);

            DrawToolbar();
            EditorGUILayout.Space(5);

            DrawStepsList();
            EditorGUILayout.Space(10);

            DrawAddStepButton();

            if (showValidation)
            {
                EditorGUILayout.Space(10);
                DrawValidation();
            }

            if (showTagAnalysis)
            {
                EditorGUILayout.Space(10);
                DrawTagAnalysis();
            }

            serializedObject.ApplyModifiedProperties();

            // Handle delete key for selected step
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Delete && selectedStepIndex >= 0)
            {
                DeleteStep(selectedStepIndex);
                Event.current.Use();
            }
        }

        private new void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            
            EditorGUILayout.LabelField("Folding Instructions", titleStyle);
            
            var summaryStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            
            EditorGUILayout.LabelField(instructions.GetSummary(), summaryStyle);
            
            EditorGUILayout.EndVertical();
        }

        private void DrawMetadata()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Metadata", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(sequenceNameProp, new GUIContent("Sequence Name"));
            EditorGUILayout.PropertyField(descriptionProp, new GUIContent("Description"));
            
            EditorGUILayout.EndVertical();
        }

        private void DrawPlaybackSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(autoPlayProp, new GUIContent("Auto Play"));
            EditorGUILayout.PropertyField(loopProp, new GUIContent("Loop"));
            
            EditorGUILayout.EndVertical();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField($"Steps ({instructions.steps.Count})", EditorStyles.boldLabel, GUILayout.Width(100));
            
            GUILayout.FlexibleSpace();

            // Validation toggle
            GUI.backgroundColor = showValidation ? Color.green : Color.white;
            if (GUILayout.Button(showValidation ? "Hide Validation" : "Show Validation", GUILayout.Width(120)))
            {
                showValidation = !showValidation;
            }
            GUI.backgroundColor = Color.white;

            // Tag analysis toggle
            GUI.backgroundColor = showTagAnalysis ? Color.cyan : Color.white;
            if (GUILayout.Button(showTagAnalysis ? "Hide Tag Analysis" : "Show Tag Analysis", GUILayout.Width(140)))
            {
                showTagAnalysis = !showTagAnalysis;
            }
            GUI.backgroundColor = Color.white;

            // Clear all button
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Clear All", GUILayout.Width(80)))
            {
                if (EditorUtility.DisplayDialog("Clear All Steps", 
                    "Are you sure you want to remove all steps?", "Yes", "No"))
                {
                    instructions.steps.Clear();
                    selectedStepIndex = -1;
                    EditorUtility.SetDirty(instructions);
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStepsList()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(200), GUILayout.MaxHeight(500));

            if (instructions.steps.Count == 0)
            {
                EditorGUILayout.HelpBox("No steps added yet. Click 'Add Step' below to begin.", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < instructions.steps.Count; i++)
                {
                    DrawStep(i);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawStep(int index)
        {
            var step = instructions.steps[index];
            bool isSelected = (index == selectedStepIndex);

            // Determine background color
            Color bgColor = step is FoldStepData ? foldColor : cameraColor;
            if (isSelected) bgColor = selectedColor;

            // Check for warnings
            bool hasWarning = false;
            if (step is FoldStepData foldStep)
            {
                var undefinedTags = instructions.GetUndefinedTagsAtStep(index);
                if (undefinedTags.Count > 0)
                {
                    bgColor = warningColor;
                    hasWarning = true;
                }

                if (!string.IsNullOrEmpty(foldStep.tagExpression))
                {
                    var (isValid, _) = BooleanExpressionEvaluator.ValidateExpression(foldStep.tagExpression);
                    if (!isValid)
                    {
                        bgColor = errorColor;
                    }
                }
            }

            GUI.backgroundColor = bgColor;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            // Header row
            EditorGUILayout.BeginHorizontal();

            // Step number and type icon
            string icon = step is FoldStepData ? "📄" : "🎥";
            if (GUILayout.Button($"{icon} [{index}]", EditorStyles.miniButtonLeft, GUILayout.Width(60)))
            {
                selectedStepIndex = (selectedStepIndex == index) ? -1 : index;
            }

            // Step display name
            EditorGUILayout.LabelField(step.GetDisplayName(), EditorStyles.wordWrappedLabel);

            // Move up button
            GUI.enabled = index > 0;
            if (GUILayout.Button("▲", EditorStyles.miniButtonMid, GUILayout.Width(25)))
            {
                instructions.MoveStep(index, index - 1);
                if (selectedStepIndex == index) selectedStepIndex--;
                EditorUtility.SetDirty(instructions);
            }

            // Move down button
            GUI.enabled = index < instructions.steps.Count - 1;
            if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(25)))
            {
                instructions.MoveStep(index, index + 1);
                if (selectedStepIndex == index) selectedStepIndex++;
                EditorUtility.SetDirty(instructions);
            }
            GUI.enabled = true;

            // Delete button
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(25)))
            {
                DeleteStep(index);
                return;
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            // Warning message
            if (hasWarning && step is FoldStepData foldStepData)
            {
                var undefinedTags = instructions.GetUndefinedTagsAtStep(index);
                if (undefinedTags.Count > 0)
                {
                    EditorGUILayout.HelpBox($"⚠ Undefined tags: {string.Join(", ", undefinedTags)}", MessageType.Warning);
                }
            }

            // Expanded details
            if (isSelected)
            {
                EditorGUILayout.Space(5);
                DrawStepDetails(step, index);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void DrawStepDetails(FoldStep step, int index)
        {
            EditorGUI.indentLevel++;

            if (step is FoldStepData foldStep)
            {
                DrawFoldStepDetails(foldStep, index);
            }
            else if (step is CameraMoveStep cameraStep)
            {
                DrawCameraStepDetails(cameraStep, index);
            }

            EditorGUI.indentLevel--;
        }

        private void DrawFoldStepDetails(FoldStepData foldStep, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Fold Details", EditorStyles.boldLabel);

            // Handle UV
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Handle UV", GUILayout.Width(100));
            foldStep.handleUV = EditorGUILayout.Vector2Field("", foldStep.handleUV);
            if (GUILayout.Button("Snap to Edge", GUILayout.Width(100)))
            {
                foldStep.handleUV = SnapToEdge(foldStep.handleUV);
                EditorUtility.SetDirty(instructions);
            }
            EditorGUILayout.EndHorizontal();

            // Tag name
            foldStep.tagName = EditorGUILayout.TextField("Tag Name", foldStep.tagName);

            // Available tags helper
            var availableTags = instructions.GetTagsUpToStep(index - 1);
            if (availableTags.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Available Tags:", EditorStyles.miniLabel, GUILayout.Width(100));
                EditorGUILayout.SelectableLabel(string.Join(", ", availableTags), EditorStyles.miniLabel, GUILayout.Height(16));
                EditorGUILayout.EndHorizontal();
            }

            // Tag expression
            EditorGUILayout.LabelField("Tag Filter Expression", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Use: AND, OR, NOT, ( )\nExample: (tag1 OR tag2) AND NOT tag3\nLeave empty to affect all vertices", MessageType.Info);
            
            foldStep.tagExpression = EditorGUILayout.TextArea(foldStep.tagExpression, GUILayout.Height(40));

            // Expression validation
            if (!string.IsNullOrEmpty(foldStep.tagExpression))
            {
                var (isValid, errorMessage) = BooleanExpressionEvaluator.ValidateExpression(foldStep.tagExpression);
                if (!isValid)
                {
                    EditorGUILayout.HelpBox($"Expression error: {errorMessage}", MessageType.Error);
                }
                else
                {
                    var referencedTags = BooleanExpressionEvaluator.ExtractTagNames(foldStep.tagExpression);
                    EditorGUILayout.HelpBox($"✓ Valid expression. References: {string.Join(", ", referencedTags)}", MessageType.Info);
                }
            }

            // Quick expression builder
            if (availableTags.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Quick Add:", GUILayout.Width(70));
                foreach (var tag in availableTags.Take(5))
                {
                    if (GUILayout.Button(tag, EditorStyles.miniButton))
                    {
                        if (string.IsNullOrEmpty(foldStep.tagExpression))
                            foldStep.tagExpression = tag;
                        else
                            foldStep.tagExpression += " AND " + tag;
                        EditorUtility.SetDirty(instructions);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            // Fold angle
            foldStep.foldAngle = EditorGUILayout.Slider("Fold Angle", foldStep.foldAngle, -180f, 180f);

            // Duration
            foldStep.duration = EditorGUILayout.FloatField("Duration (sec)", foldStep.duration);

            // Camera plane option
            foldStep.useCameraPlane = EditorGUILayout.Toggle(
                new GUIContent("Use Camera Plane", 
                    "If enabled, the drag plane will be parallel to the camera's view instead of aligned to the paper surface"),
                foldStep.useCameraPlane);

            EditorGUILayout.EndVertical();

            // Accuracy tracking section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Accuracy Tracking", EditorStyles.boldLabel);
            
            foldStep.hasCorrectAxis = EditorGUILayout.Toggle(
                new GUIContent("Enable Scoring", 
                    "If enabled, this fold will be scored based on how close the player's fold is to the correct axis"),
                foldStep.hasCorrectAxis);

            EditorGUI.indentLevel++;
            
            // Always show the fields, but disable them when scoring is not enabled
            EditorGUI.BeginDisabledGroup(!foldStep.hasCorrectAxis);
            
            EditorGUILayout.HelpBox("Define the correct fold axis in UV coordinates (0-1 range). This will be shown to the player as a green line.", MessageType.Info);
            
            // Correct axis start
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Correct Axis Start", GUILayout.Width(120));
            EditorGUI.BeginChangeCheck();
            foldStep.correctAxisStart = EditorGUILayout.Vector2Field("", foldStep.correctAxisStart);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(instructions);
            }
            if (GUILayout.Button("Snap to Edge", GUILayout.Width(100)))
            {
                foldStep.correctAxisStart = SnapToEdge(foldStep.correctAxisStart);
                EditorUtility.SetDirty(instructions);
            }
            EditorGUILayout.EndHorizontal();

            // Correct axis end
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Correct Axis End", GUILayout.Width(120));
            EditorGUI.BeginChangeCheck();
            foldStep.correctAxisEnd = EditorGUILayout.Vector2Field("", foldStep.correctAxisEnd);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(instructions);
            }
            if (GUILayout.Button("Snap to Edge", GUILayout.Width(100)))
            {
                foldStep.correctAxisEnd = SnapToEdge(foldStep.correctAxisEnd);
                EditorUtility.SetDirty(instructions);
            }
            EditorGUILayout.EndHorizontal();

            // Preset correct axes
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Presets:", GUILayout.Width(60));
            if (GUILayout.Button("Horizontal Center", EditorStyles.miniButton))
            {
                foldStep.correctAxisStart = new Vector2(0f, 0.5f);
                foldStep.correctAxisEnd = new Vector2(1f, 0.5f);
                EditorUtility.SetDirty(instructions);
            }
            if (GUILayout.Button("Vertical Center", EditorStyles.miniButton))
            {
                foldStep.correctAxisStart = new Vector2(0.5f, 0f);
                foldStep.correctAxisEnd = new Vector2(0.5f, 1f);
                EditorUtility.SetDirty(instructions);
            }
            if (GUILayout.Button("Diagonal \\", EditorStyles.miniButton))
            {
                foldStep.correctAxisStart = new Vector2(0f, 0f);
                foldStep.correctAxisEnd = new Vector2(1f, 1f);
                EditorUtility.SetDirty(instructions);
            }
            if (GUILayout.Button("Diagonal /", EditorStyles.miniButton))
            {
                foldStep.correctAxisStart = new Vector2(1f, 0f);
                foldStep.correctAxisEnd = new Vector2(0f, 1f);
                EditorUtility.SetDirty(instructions);
            }
            EditorGUILayout.EndHorizontal();

            // Score modifier
            EditorGUILayout.Space(5);
            foldStep.scoreModifier = EditorGUILayout.Slider(
                new GUIContent("Score Modifier", 
                    "Flat adjustment applied to the final score (-100 to +100). Use this to make certain folds easier or harder."),
                foldStep.scoreModifier, -100f, 100f);
            
            if (foldStep.scoreModifier != 0)
            {
                string modText = foldStep.scoreModifier > 0 ? $"+{foldStep.scoreModifier:F0}" : $"{foldStep.scoreModifier:F0}";
                EditorGUILayout.HelpBox($"Score will be adjusted by {modText} points", MessageType.Info);
            }

            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
        }

        private void DrawCameraStepDetails(CameraMoveStep cameraStep, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Camera Movement Details", EditorStyles.boldLabel);

            // Rotation
            cameraStep.rotation = EditorGUILayout.Vector3Field("Rotation", cameraStep.rotation);

            // Preset buttons
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Presets:", GUILayout.Width(60));
            if (GUILayout.Button("Front", EditorStyles.miniButton))
            {
                cameraStep.rotation = Vector3.zero;
                EditorUtility.SetDirty(instructions);
            }
            if (GUILayout.Button("Top", EditorStyles.miniButton))
            {
                cameraStep.rotation = new Vector3(90, 0, 0);
                EditorUtility.SetDirty(instructions);
            }
            if (GUILayout.Button("Side", EditorStyles.miniButton))
            {
                cameraStep.rotation = new Vector3(0, 90, 0);
                EditorUtility.SetDirty(instructions);
            }
            if (GUILayout.Button("Iso", EditorStyles.miniButton))
            {
                cameraStep.rotation = new Vector3(30, 45, 0);
                EditorUtility.SetDirty(instructions);
            }
            EditorGUILayout.EndHorizontal();

            // Distance
            cameraStep.distance = EditorGUILayout.FloatField("Distance", cameraStep.distance);

            // Duration
            cameraStep.duration = EditorGUILayout.FloatField("Duration (sec)", cameraStep.duration);

            // Ease curve
            cameraStep.easeCurve = EditorGUILayout.CurveField("Ease Curve", cameraStep.easeCurve);

            EditorGUILayout.EndVertical();
        }

        private void DrawAddStepButton()
        {
            EditorGUILayout.BeginHorizontal();
            
            GUILayout.FlexibleSpace();

            GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
            if (GUILayout.Button("➕ Add Fold Step", GUILayout.Height(30), GUILayout.Width(150)))
            {
                AddFoldStep();
            }

            if (GUILayout.Button("➕ Add Camera Move", GUILayout.Height(30), GUILayout.Width(150)))
            {
                AddCameraStep();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawValidation()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Validation Report", EditorStyles.boldLabel);

            var errors = instructions.ValidateAllExpressions();
            
            if (errors.Count == 0)
            {
                EditorGUILayout.HelpBox("✓ All expressions are valid!", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"Found {errors.Count} expression error(s)", MessageType.Error);
                foreach (var (stepIndex, errorMessage) in errors)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Step {stepIndex}:", GUILayout.Width(60));
                    EditorGUILayout.LabelField(errorMessage, EditorStyles.miniLabel);
                    if (GUILayout.Button("Go", EditorStyles.miniButton, GUILayout.Width(40)))
                    {
                        selectedStepIndex = stepIndex;
                        Repaint();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            // Check for undefined tags
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Tag References", EditorStyles.boldLabel);

            bool hasUndefinedTags = false;
            for (int i = 0; i < instructions.steps.Count; i++)
            {
                var undefinedTags = instructions.GetUndefinedTagsAtStep(i);
                if (undefinedTags.Count > 0)
                {
                    hasUndefinedTags = true;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Step {i}:", GUILayout.Width(60));
                    EditorGUILayout.LabelField($"⚠ {string.Join(", ", undefinedTags)}", EditorStyles.miniLabel);
                    if (GUILayout.Button("Go", EditorStyles.miniButton, GUILayout.Width(40)))
                    {
                        selectedStepIndex = i;
                        Repaint();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            if (!hasUndefinedTags)
            {
                EditorGUILayout.HelpBox("✓ All tag references are valid!", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTagAnalysis()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Tag Analysis", EditorStyles.boldLabel);

            var allCreatedTags = instructions.GetAllTags();
            var allReferencedTags = instructions.GetAllReferencedTags();

            EditorGUILayout.LabelField($"Tags Created: {allCreatedTags.Count}");
            if (allCreatedTags.Count > 0)
            {
                EditorGUILayout.SelectableLabel(string.Join(", ", allCreatedTags), EditorStyles.helpBox, GUILayout.Height(30));
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField($"Tags Referenced: {allReferencedTags.Count}");
            if (allReferencedTags.Count > 0)
            {
                EditorGUILayout.SelectableLabel(string.Join(", ", allReferencedTags), EditorStyles.helpBox, GUILayout.Height(30));
            }

            // Unreferenced tags
            var unreferenced = allCreatedTags.Except(allReferencedTags).ToList();
            if (unreferenced.Count > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox($"Unreferenced tags: {string.Join(", ", unreferenced)}", MessageType.Info);
            }

            // Tag timeline
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Tag Timeline", EditorStyles.boldLabel);
            
            HashSet<string> currentTags = new HashSet<string>();
            for (int i = 0; i < instructions.steps.Count; i++)
            {
                if (instructions.steps[i] is FoldStepData foldStep && !string.IsNullOrEmpty(foldStep.tagName))
                {
                    currentTags.Add(foldStep.tagName);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Step {i}:", GUILayout.Width(60));
                    EditorGUILayout.LabelField($"Adds '{foldStep.tagName}' → Total: [{string.Join(", ", currentTags)}]", 
                        EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void AddFoldStep()
        {
            var newStep = new FoldStepData
            {
                handleUV = new Vector2(0.5f, 0f),
                tagName = $"fold_{instructions.steps.Count + 1}",
                tagExpression = "",
                foldAngle = 180f,
                duration = 0f
            };

            instructions.AddFoldStep(newStep);
            selectedStepIndex = instructions.steps.Count - 1;
            EditorUtility.SetDirty(instructions);
        }

        private void AddCameraStep()
        {
            var newStep = new CameraMoveStep
            {
                rotation = Vector3.zero,
                distance = 10f,
                duration = 1f,
                easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };

            instructions.AddCameraMoveStep(newStep);
            selectedStepIndex = instructions.steps.Count - 1;
            EditorUtility.SetDirty(instructions);
        }

        private void DeleteStep(int index)
        {
            if (EditorUtility.DisplayDialog("Delete Step", 
                $"Delete step {index}?", "Yes", "No"))
            {
                instructions.RemoveStep(index);
                if (selectedStepIndex >= instructions.steps.Count)
                {
                    selectedStepIndex = instructions.steps.Count - 1;
                }
                EditorUtility.SetDirty(instructions);
            }
        }

        private Vector2 SnapToEdge(Vector2 uv)
        {
            // Snap to nearest edge
            float minDist = float.MaxValue;
            Vector2 snapped = uv;

            // Try each edge
            Vector2[] edges = new Vector2[]
            {
                new Vector2(uv.x, 0f),      // Bottom
                new Vector2(uv.x, 1f),      // Top
                new Vector2(0f, uv.y),      // Left
                new Vector2(1f, uv.y)       // Right
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
    }
}
