#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace PaperFolding
{
    /// <summary>
    /// Custom editor for PaperMesh component
    /// </summary>
    [CustomEditor(typeof(PaperMesh))]
    public class PaperMeshEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            PaperMesh paper = (PaperMesh)target;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mesh Generation", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Regenerate Mesh"))
            {
                paper.GeneratePaperMesh();
                EditorUtility.SetDirty(paper);
            }
            
            if (GUILayout.Button("Reset All Folds"))
            {
                paper.Reset();
                EditorUtility.SetDirty(paper);
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Vertex Data Info", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Total Vertices: {paper.GetVertexData()?.Count ?? 0}");
            
            // Show unique tags
            if (paper.GetVertexData() != null)
            {
                var allTags = paper.GetAllTags();
                
                if (allTags.Count > 0)
                {
                    EditorGUILayout.LabelField($"Unique Tags: {allTags.Count}");
                    foreach (var tag in allTags)
                    {
                        EditorGUILayout.LabelField($"  • {tag}");
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No folds applied yet");
                }
            }
        }
    }

    /// <summary>
    /// Custom editor for FoldController component
    /// </summary>
    [CustomEditor(typeof(FoldController))]
    public class FoldControllerEditor : Editor
    {
        private bool showAxisControls = true;
        private string selectedTag = "";
        private string quickFoldTag = "";
        
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            FoldController controller = (FoldController)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Quick Fold Controls", EditorStyles.boldLabel);

            // Tag dropdowns
            var tags = controller.GetAllTags();
            var options = new System.Collections.Generic.List<string> { "<None>" };
            options.AddRange(tags);

            // Visualization tag
            int selectedIndex = 0;
            if (!string.IsNullOrEmpty(selectedTag))
            {
                int idx = tags.IndexOf(selectedTag);
                if (idx >= 0) selectedIndex = idx + 1;
            }
            int newSelectedIndex = EditorGUILayout.Popup(new GUIContent("Visualization Tag"), selectedIndex, options.ToArray());
            if (newSelectedIndex != selectedIndex)
            {
                selectedTag = newSelectedIndex == 0 ? string.Empty : options[newSelectedIndex];
            }

            // Quick fold tag
            int quickIndex = 0;
            if (!string.IsNullOrEmpty(quickFoldTag))
            {
                int idx = tags.IndexOf(quickFoldTag);
                if (idx >= 0) quickIndex = idx + 1;
            }
            int newQuickIndex = EditorGUILayout.Popup(new GUIContent("Quick Fold Tag"), quickIndex, options.ToArray());
            if (newQuickIndex != quickIndex)
            {
                quickFoldTag = newQuickIndex == 0 ? string.Empty : options[newQuickIndex];
            }

            // Axis controls
            showAxisControls = EditorGUILayout.Foldout(showAxisControls, "Axis Control Panel");

            if (showAxisControls)
            {
                EditorGUI.indentLevel++;

                FoldAxis currentAxis = controller.CurrentAxis;

                EditorGUILayout.LabelField("Start Point (U, V)");
                EditorGUI.indentLevel++;
                currentAxis.u1 = EditorGUILayout.Slider("U1", currentAxis.u1, 0f, 1f);
                currentAxis.v1 = EditorGUILayout.Slider("V1", currentAxis.v1, 0f, 1f);
                EditorGUI.indentLevel--;

                EditorGUILayout.LabelField("End Point (U, V)");
                EditorGUI.indentLevel++;
                currentAxis.u2 = EditorGUILayout.Slider("U2", currentAxis.u2, 0f, 1f);
                currentAxis.v2 = EditorGUILayout.Slider("V2", currentAxis.v2, 0f, 1f);
                EditorGUI.indentLevel--;

                controller.CurrentAxis = currentAxis;

                EditorGUI.indentLevel--;

                EditorGUILayout.Space();

                controller.PreviewDegrees = EditorGUILayout.Slider("Fold Angle", controller.PreviewDegrees, -180f, 180f);
                controller.PreviewTag = EditorGUILayout.TextField("Optional Tag", controller.PreviewTag);
            }

            EditorGUILayout.Space();

            // Tag expression validation and info
            string tagExpr = controller.GetTagExpression();
            if (!string.IsNullOrEmpty(tagExpr))
            {
                EditorGUILayout.LabelField("Active Tag Filter", EditorStyles.boldLabel);
                
                var (isValid, errorMessage) = BooleanExpressionEvaluator.ValidateExpression(tagExpr);
                if (!isValid)
                {
                    EditorGUILayout.HelpBox($"Expression Error: {errorMessage}\n\nExpression: {tagExpr}", MessageType.Error);
                }
                else
                {
                    var referencedTags = BooleanExpressionEvaluator.ExtractTagNames(tagExpr);
                    EditorGUILayout.HelpBox(
                        $"✓ Valid Expression\n" +
                        $"Expression: {tagExpr}\n" +
                        $"References: {string.Join(", ", referencedTags)}\n\n" +
                        $"Preview will only show vertices matching this filter.",
                        MessageType.Info);
                }
                
                EditorGUILayout.Space();
            }

            // Apply fold button
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Apply Fold", GUILayout.Height(30)))
            {
                Undo.RecordObject(controller, "Apply Fold");

                if (!string.IsNullOrEmpty(quickFoldTag))
                {
                    controller.ApplyFoldWithTag(quickFoldTag);
                }
                else
                {
                    controller.ApplyFold();
                }

                EditorUtility.SetDirty(controller);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Common Fold Presets", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Horizontal Center"))
            {
                controller.CurrentAxis = new FoldAxis(0f, 0.5f, 1f, 0.5f);
                EditorUtility.SetDirty(controller);
            }
            if (GUILayout.Button("Vertical Center"))
            {
                controller.CurrentAxis = new FoldAxis(0.5f, 0f, 0.5f, 1f);
                EditorUtility.SetDirty(controller);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Diagonal ↗"))
            {
                controller.CurrentAxis = new FoldAxis(0f, 0f, 1f, 1f);
                EditorUtility.SetDirty(controller);
            }
            if (GUILayout.Button("Diagonal ↖"))
            {
                controller.CurrentAxis = new FoldAxis(1f, 0f, 0f, 1f);
                EditorUtility.SetDirty(controller);
            }
            EditorGUILayout.EndHorizontal();
            
            if (GUILayout.Button("Swap Axis Points"))
            {
                FoldAxis axis = controller.CurrentAxis;
                controller.CurrentAxis = new FoldAxis(axis.u2, axis.v2, axis.u1, axis.v1);
                EditorUtility.SetDirty(controller);
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(controller);
            }
        }
    }

    /// <summary>
    /// Menu items for creating paper folding objects
    /// </summary>
    public static class PaperFoldingMenuItems
    {
        [MenuItem("GameObject/Paper Folding/Create Paper", false, 10)]
        static void CreatePaper(MenuCommand menuCommand)
        {
            GameObject paperObj = new GameObject("Paper");
            paperObj.AddComponent<PaperMesh>();
            
            var renderer = paperObj.GetComponent<MeshRenderer>();
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = Color.white;
            
            Undo.RegisterCreatedObjectUndo(paperObj, "Create Paper");
            Selection.activeObject = paperObj;
        }
        
        [MenuItem("GameObject/Paper Folding/Create Paper with Controller", false, 11)]
        static void CreatePaperWithController(MenuCommand menuCommand)
        {
            GameObject paperObj = new GameObject("Paper with Controller");
            paperObj.AddComponent<PaperMesh>();
            paperObj.AddComponent<FoldController>();
            
            var renderer = paperObj.GetComponent<MeshRenderer>();
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = Color.white;
            
            Undo.RegisterCreatedObjectUndo(paperObj, "Create Paper with Controller");
            Selection.activeObject = paperObj;
        }
    }
}
#endif