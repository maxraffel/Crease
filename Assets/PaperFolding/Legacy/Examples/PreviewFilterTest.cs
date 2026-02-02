using UnityEngine;

namespace PaperFolding.Examples
{
    /// <summary>
    /// Test script to demonstrate tag-filtered preview functionality
    /// Shows how to create a series of folds and preview subsequent folds with tag filters
    /// </summary>
    public class PreviewFilterTest : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PaperMesh paperMesh;
        [SerializeField] private FoldController foldController;

        [Header("Test Controls")]
        [SerializeField] private bool runTestOnStart = false;

        private void Start()
        {
            if (paperMesh == null)
                paperMesh = GetComponent<PaperMesh>();

            if (foldController == null)
                foldController = GetComponent<FoldController>();

            if (runTestOnStart)
            {
                RunTest();
            }
        }

        [ContextMenu("Run Tag Filter Preview Test")]
        public void RunTest()
        {
            if (paperMesh == null || foldController == null)
            {
                Debug.LogError("PaperMesh or FoldController not assigned!");
                return;
            }

            Debug.Log("=== Starting Tag Filter Preview Test ===");

            // Reset paper
            paperMesh.Reset();

            // Step 1: Create first fold (vertical center) - tags all vertices as "fold_1_moved" or "fold_1_static"
            Debug.Log("Step 1: Creating vertical center fold (tag: fold_1)");
            FoldAxis axis1 = new FoldAxis(0.5f, 0f, 0.5f, 1f);
            paperMesh.Fold(axis1, 180f, "fold_1");

            // Step 2: Preview a second fold that ONLY affects vertices tagged "fold_1_moved"
            Debug.Log("Step 2: Setting up preview for fold_2 that only affects fold_1_moved vertices");
            
            FoldAxis axis2 = new FoldAxis(0f, 0.5f, 1f, 0.5f); // Horizontal center
            foldController.SetFoldAxis(axis2);
            foldController.SetPreviewDegrees(90f);
            foldController.SetPreviewTag("fold_2");
            
            // Set tag expression to only affect moved vertices from fold_1
            foldController.SetTagExpression("fold_1_moved");
            
            Debug.Log("Preview configured! Tag expression: 'fold_1_moved'");
            Debug.Log("The preview should only show the right half of the paper folding (the moved side from fold_1)");

            // Step 3: Test with complex expression
            StartCoroutine(TestComplexExpressionAfterDelay());
        }

        private System.Collections.IEnumerator TestComplexExpressionAfterDelay()
        {
            yield return new UnityEngine.WaitForSeconds(3f);

            Debug.Log("Step 3: Testing complex tag expression");
            
            // Apply the second fold
            foldController.ApplyFold();
            
            yield return new UnityEngine.WaitForSeconds(1f);

            // Now setup a third preview that affects: (fold_1_moved OR fold_2_moved) AND NOT fold_2_static
            FoldAxis axis3 = new FoldAxis(0.25f, 0f, 0.75f, 1f); // Diagonal-ish
            foldController.SetFoldAxis(axis3);
            foldController.SetPreviewDegrees(45f);
            foldController.SetPreviewTag("fold_3");
            foldController.SetTagExpression("(fold_1_moved OR fold_2_moved) AND NOT fold_2_static");
            
            Debug.Log("Preview configured with complex expression: '(fold_1_moved OR fold_2_moved) AND NOT fold_2_static'");
            Debug.Log("This should affect vertices that were moved by either fold_1 or fold_2, but NOT those marked static by fold_2");
        }

        [ContextMenu("Test Invalid Expression")]
        public void TestInvalidExpression()
        {
            if (foldController == null) return;

            Debug.Log("Testing invalid expression handling...");
            
            // Test various invalid expressions
            string[] invalidExpressions = new string[]
            {
                "tag1 AND AND tag2",  // Double operator
                "tag1 OR",            // Trailing operator
                "(tag1 AND tag2",     // Unmatched parenthesis
                "tag1 AND (tag2 OR)", // Trailing operator in group
                "NOT NOT NOT tag1",   // Multiple NOTs (could be valid but might be confusing)
            };

            foreach (var expr in invalidExpressions)
            {
                var (isValid, error) = BooleanExpressionEvaluator.ValidateExpression(expr);
                Debug.Log($"Expression: '{expr}' - Valid: {isValid} - Error: {error}");
            }
        }

        [ContextMenu("Clear Tag Expression")]
        public void ClearTagExpression()
        {
            if (foldController != null)
            {
                foldController.SetTagExpression("");
                Debug.Log("Tag expression cleared - preview will affect all vertices");
            }
        }

        [ContextMenu("Reset Paper")]
        public void ResetPaper()
        {
            if (paperMesh != null)
            {
                paperMesh.Reset();
                Debug.Log("Paper reset to initial state");
            }
        }
    }
}
