using UnityEngine;
using System.Collections;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PaperFolding
{
    /// <summary>
    /// Example controller demonstrating how to use the paper folding system at runtime
    /// </summary>
    public class PaperFoldingDemo : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PaperMesh paperMesh;
        [SerializeField] private FoldVisualizer visualizer;
        
        [Header("Demo Settings")]
        [SerializeField] private bool autoFoldDemo = false;
        [SerializeField] private float foldInterval = 2f;
        
        private float timer = 0f;
        private int demoStep = 0;
    private Coroutine airplaneCoroutine = null;
    private bool isAnimatingAirplane = false;

        private void Start()
        {
            if (paperMesh == null)
            {
                paperMesh = GetComponent<PaperMesh>();
            }
            
            if (visualizer == null)
            {
                visualizer = GetComponent<FoldVisualizer>();
            }
        }

        private void Update()
        {
            if (autoFoldDemo)
            {
                timer += Time.deltaTime;
                if (timer >= foldInterval)
                {
                    timer = 0f;
                    PerformDemoFold();
                }
            }
            
            // Manual controls
            HandleInput();
        }

        private void HandleInput()
        {
            // Space to apply current fold
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (visualizer != null)
                {
                    visualizer.ApplyFold();
                    Debug.Log("Fold applied!");
                }
            }
            
            // R to reset
            if (Input.GetKeyDown(KeyCode.R))
            {
                if (paperMesh != null)
                {
                    paperMesh.Reset();
                    demoStep = 0;
                    Debug.Log("Paper reset!");
                }
            }
            
            // Arrow keys to adjust fold angle
            if (visualizer != null)
            {
                if (Input.GetKey(KeyCode.UpArrow))
                {
                    visualizer.PreviewDegrees += 90f * Time.deltaTime;
                }
                if (Input.GetKey(KeyCode.DownArrow))
                {
                    visualizer.PreviewDegrees -= 90f * Time.deltaTime;
                }
            }
            
            // Number keys for preset folds
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ApplyPresetFold(new FoldAxis(0f, 0.5f, 1f, 0.5f), 180f); // Horizontal fold
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                ApplyPresetFold(new FoldAxis(0.5f, 0f, 0.5f, 1f), 180f); // Vertical fold
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                ApplyPresetFold(new FoldAxis(0f, 0f, 1f, 1f), 180f); // Diagonal fold
            }
        }

        private void PerformDemoFold()
        {
            if (paperMesh == null) return;
            
            switch (demoStep)
            {
                case 0:
                    // Fold in half horizontally
                    paperMesh.Fold(new FoldAxis(0f, 0.5f, 1f, 0.5f), 180f);
                    Debug.Log("Demo: Horizontal fold");
                    break;
                    
                case 1:
                    // Fold in half vertically
                    paperMesh.Fold(new FoldAxis(0.5f, 0f, 0.5f, 1f), 180f);
                    Debug.Log("Demo: Vertical fold");
                    break;
                    
                case 2:
                    // Diagonal fold
                    paperMesh.Fold(new FoldAxis(0f, 0f, 1f, 1f), 90f);
                    Debug.Log("Demo: Diagonal fold");
                    break;
                    
                case 3:
                    // Reset and start over
                    paperMesh.Reset();
                    demoStep = -1;
                    Debug.Log("Demo: Reset");
                    break;
            }
            
            demoStep++;
        }

        /// <summary>
        /// Apply a preset fold configuration
        /// </summary>
        public void ApplyPresetFold(FoldAxis axis, float degrees, string tag = null)
        {
            if (paperMesh != null)
            {
                paperMesh.Fold(axis, degrees, tag);
                Debug.Log($"Applied fold: {axis.u1},{axis.v1} to {axis.u2},{axis.v2} at {degrees}°");
            }
        }

        private System.Func<System.Collections.Generic.HashSet<string>, bool> generatePredicate(string requiredTag)
        {
            return (tags) => tags.Contains(requiredTag);
        }

        /// <summary>
        /// Example: Create a paper airplane sequence
        /// </summary>
        public void CreatePaperAirplane()
        {
            if (paperMesh == null) return;
            
            paperMesh.Reset();
            
            // Step 1: Fold in corners
            paperMesh.Fold(new FoldAxis(1f, 0.5f, 0.5f, 1f), 179f, "right_corner");
            paperMesh.Fold(new FoldAxis(0.5f, 1f, 0f, 0.5f), 179f, "left_corner");

            
            // Step 2: Fold in half
            paperMesh.Fold(new FoldAxis(0.5f, 1f, 0.5f, 0f), -179f, "half");

            // Step 3: right wing
            paperMesh.Fold(new FoldAxis(0.91f, 0f, 0.5f, 1f), -179f, "right_wing", generatePredicate("half_moved"));
            paperMesh.Fold(new FoldAxis(0.667f, 0f, 0.5f, 1f), -80f, "right_wing", generatePredicate("half_moved"));

            // Step 4: left wing
            paperMesh.Fold(new FoldAxis(0.91f, 0f, 0.5f, 1f), 179f, "left_wing", generatePredicate("half_static"));
            paperMesh.Fold(new FoldAxis(0.667f, 0f, 0.5f, 1f), 80f, "left_wing", generatePredicate("half_static"));


            
            Debug.Log("Paper airplane created!");
        }

        /// <summary>
        /// Create the paper airplane but animate through each fold using a coroutine.
        /// </summary>
        public void CreatePaperAirplaneAnimated()
        {
            if (paperMesh == null) return;
            if (isAnimatingAirplane) return;

            airplaneCoroutine = StartCoroutine(AnimatePaperAirplane());
        }

        private IEnumerator AnimatePaperAirplane()
        {
            isAnimatingAirplane = true;

            paperMesh.Reset();

            // Step 1: Fold in corners (animated)
            yield return StartCoroutine(paperMesh.AnimateFold(new FoldAxis(1f, 0.5f, 0.5f, 1f), 179f, "right_corner", null, foldInterval));
            yield return StartCoroutine(paperMesh.AnimateFold(new FoldAxis(0.5f, 1f, 0f, 0.5f), 179f, "left_corner", null, foldInterval));

            // Step 2: Fold in half (animated)
            yield return StartCoroutine(paperMesh.AnimateFold(new FoldAxis(0.5f, 1f, 0.5f, 0f), -179f, "half", null, foldInterval));

            // Rotate the paper mesh to face the other direction before wing folds (smoothly)
            float rotateDuration = Mathf.Max(0.1f, foldInterval * 0.5f);
            // Rotate 180 degrees around the Y axis (flip) — adjust axis if your paper orientation differs
            yield return StartCoroutine(RotatePaperMeshTo(Quaternion.Euler(-39.6f, -109.8f, 59.5f), rotateDuration));

            // Step 3: right wing (animated)
            yield return StartCoroutine(paperMesh.AnimateFold(new FoldAxis(0.91f, 0f, 0.5f, 1f), -179f, "right_wing", generatePredicate("half_moved"), foldInterval));
            yield return StartCoroutine(paperMesh.AnimateFold(new FoldAxis(0.667f, 0f, 0.5f, 1f), -80f, "right_wing", generatePredicate("half_moved"), foldInterval));

            yield return StartCoroutine(RotatePaperMeshTo(Quaternion.Euler(20.578f, -17.967f, 102.446f), rotateDuration));

            // Step 4: left wing (animated)
            yield return StartCoroutine(paperMesh.AnimateFold(new FoldAxis(0.91f, 0f, 0.5f, 1f), 179f, "left_wing", generatePredicate("half_static"), foldInterval));
            yield return StartCoroutine(paperMesh.AnimateFold(new FoldAxis(0.667f, 0f, 0.5f, 1f), 80f, "left_wing", generatePredicate("half_static"), foldInterval));

            Debug.Log("Paper airplane created (animated)!");

            isAnimatingAirplane = false;
            airplaneCoroutine = null;
        }

        /// <summary>
        /// Smoothly rotate the paper mesh transform to the given target rotation over duration seconds.
        /// </summary>
        private IEnumerator RotatePaperMeshTo(Quaternion targetRotation, float duration)
        {
            if (paperMesh == null) yield break;

            Transform t = paperMesh.transform;
            Quaternion start = t.rotation;
            float elapsed = 0f;
            if (duration <= 0f)
            {
                t.rotation = targetRotation;
                yield break;
            }

            while (elapsed < duration)
            {
                float u = Mathf.Clamp01(elapsed / duration);
                t.rotation = Quaternion.Slerp(start, targetRotation, u);
                elapsed += Time.deltaTime;
                yield return null;
            }

            t.rotation = targetRotation;
        }

        /// <summary>
        /// Example: Query vertices by tag
        /// </summary>
        public void PrintVerticesWithTag(string tag)
        {
            if (paperMesh == null) return;
            
            var vertices = paperMesh.GetVerticesWithTag(tag);
            Debug.Log($"Found {vertices.Count} vertices with tag '{tag}'");
            
            foreach (int vertexIndex in vertices)
            {
                var tags = paperMesh.GetVertexTags(vertexIndex);
                Debug.Log($"  Vertex {vertexIndex}: {string.Join(", ", tags)}");
            }
        }

        /// <summary>
        /// Example: Create a fan fold pattern
        /// </summary>
        public void CreateFanFold(int numFolds = 5)
        {
            if (paperMesh == null) return;
            
            paperMesh.Reset();
            
            float step = 1f / numFolds;
            for (int i = 0; i < numFolds; i++)
            {
                float v = step * i + step * 0.5f;
                float angle = (i % 2 == 0) ? 180f : -180f;
                paperMesh.Fold(new FoldAxis(0f, v, 1f, v), angle, $"fan_fold_{i}");
            }
            
            Debug.Log($"Created fan fold with {numFolds} folds");
        }

        public static void SaveMeshAsset(Mesh mesh, string fileName)
        {
#if UNITY_EDITOR
            if (mesh == null) { Debug.LogWarning("No mesh to save"); return; }

            var dir = "Assets/GeneratedMeshes";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // Duplicate the mesh so we don't accidentally modify a shared/scene instance
            var meshCopy = Object.Instantiate(mesh);
            meshCopy.name = Path.GetFileNameWithoutExtension(fileName);

            var path = Path.Combine(dir, fileName);
            AssetDatabase.CreateAsset(meshCopy, path);
            EditorUtility.SetDirty(meshCopy);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Saved mesh asset to {path}");
#else
            Debug.LogWarning("SaveMeshAsset is only available in the Unity Editor");
#endif
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 400));
            GUILayout.Box("Paper Folding Demo Controls");
            
            GUILayout.Label("Keyboard Controls:");
            GUILayout.Label("SPACE - Apply current fold");
            GUILayout.Label("R - Reset paper");
            GUILayout.Label("↑/↓ - Adjust fold angle");
            GUILayout.Label("1 - Horizontal fold");
            GUILayout.Label("2 - Vertical fold");
            GUILayout.Label("3 - Diagonal fold");
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Create Paper Airplane"))
            {
                CreatePaperAirplane();
            }

            if (GUILayout.Button("Create Paper Airplane (Animated)"))
            {
                CreatePaperAirplaneAnimated();
            }
            
            if (GUILayout.Button("Create Fan Fold"))
            {
                CreateFanFold(5);
            }
            
            if (GUILayout.Button("Reset Paper"))
            {
                if (paperMesh != null)
                {
                    paperMesh.Reset();
                    demoStep = 0;
                }
            }

            if (GUILayout.Button("Save mesh asset"))
            {
                if (paperMesh != null && paperMesh.GetComponent<MeshFilter>() != null)
                {
                    SaveMeshAsset(paperMesh.GetComponent<MeshFilter>().sharedMesh, "PaperMeshAsset.asset");
                }
            }
            
            GUILayout.Space(10);
            autoFoldDemo = GUILayout.Toggle(autoFoldDemo, "Auto Fold Demo");
            
            GUILayout.EndArea();
        }
    }
}