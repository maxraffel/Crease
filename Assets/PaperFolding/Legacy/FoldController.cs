using UnityEngine;
using System.Collections.Generic;

namespace PaperFolding
{
    /// <summary>
    /// Controls fold visualization and interactive folding.
    /// 
    /// Architecture:
    /// - All input modes (ClickPlacement, DragHandle) modify the current fold axis
    /// - The fold axis can be set directly via API methods (SetFoldAxis, SetFoldAxisWorld, SetFoldAxisLocal)
    /// - ApplyFold() uses the current fold axis to perform the actual fold on the paper
    /// - Preview mesh and gizmos visualize the current fold axis in real-time
    /// 
    /// Input Modes:
    /// - ClickPlacement: Left-click for axis start, right-click for axis end
    /// - DragHandle: Drag from handle position to compute perpendicular fold axis
    /// </summary>
    public class FoldController : MonoBehaviour
    {
        public enum InputMode
        {
            ClickPlacement,
            DragHandle
        }

        [Header("References")]
        [SerializeField] private PaperMesh paperMesh;
        
        [Header("Visualization Settings")]
        [SerializeField] private Material previewMaterial;
        [SerializeField] private Color axisColor = Color.red;
        [SerializeField] private Color handleColor = Color.cyan;
        [SerializeField] private Color previewColor = new Color(1f, 1f, 0f, 0.3f);
        [SerializeField] private float axisThickness = 0.02f;
        [SerializeField] private bool showPreview = true;
        [SerializeField] private bool showAxisGizmos = true;
        
        [Header("Interaction")]
        [SerializeField] private InputMode inputMode = InputMode.ClickPlacement;
        [SerializeField] private bool enableInput = true;
        [SerializeField] private Camera overrideCamera = null;
        
        [Header("Drag Handle Settings")]
        [SerializeField] private float handleU = 0.5f;
        [SerializeField] private float handleV = 0.5f;
        [SerializeField] private float handleGrabRadius = 0.05f;
        [SerializeField] private float dragFoldAngle = 180f;
        [SerializeField] private bool autoApplyOnRelease = true;
        [SerializeField] private float releaseSnapDistance = 0.1f; // Distance from start to consider "released close"
        [SerializeField] private bool useCameraPlane = false; // Use camera view plane instead of paper normal for drag plane
        
        [Header("Current Fold Axis")]
        [SerializeField] private FoldAxis currentAxis = new FoldAxis(0.2f, 0f, 0.8f, 1f);
        [SerializeField] private float previewDegrees = 90f;
        [SerializeField] private string previewTag = "";
        [SerializeField] private string tagExpression = ""; // Tag filter expression

        [Header("Correct Axis (for accuracy scoring)")]
        [SerializeField] private bool showCorrectAxis = false;
        [SerializeField] private FoldAxis correctAxis = new FoldAxis(0f, 0.5f, 1f, 0.5f);
        [SerializeField] private Color correctAxisColor = Color.green;
        [SerializeField] private float correctAxisWidth = 0.05f;
        [SerializeField] private float correctAxisZOffset = 0.01f; // Offset towards camera

        // Visualization objects
        private GameObject handleSphere;
        private GameObject previewObject;
        private Mesh previewMesh;
        private GameObject correctAxisLine;
        private LineRenderer correctAxisLineRenderer;

        // Drag handle state
        private bool isDraggingHandle = false;
        private Vector3 originWorldPosition; // The origin point in world space (where drag started)
        private Plane dragPlane;
        private bool dragConfirmed = false; // Whether the drag has been confirmed (not released close to start)

        // Cached spatial axis (used during dragging to avoid UV conversion loss)
        private Vector3 cachedAxisStartLocal;
        private Vector3 cachedAxisEndLocal;
        private bool useCachedAxis = false;

        private void Start()
        {
            if (paperMesh == null)
            {
                paperMesh = GetComponent<PaperMesh>();
            }

            SetupVisualization();
        }

        private void SetupVisualization()
        {
            // Setup drag handle sphere (only for DragHandle mode)
            handleSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handleSphere.name = "Drag Handle Sphere";
            handleSphere.transform.SetParent(transform);
            handleSphere.transform.localScale = Vector3.one * (handleGrabRadius * 0.5f); // Smaller sphere
            
            // Remove collider from sphere - we use custom grab detection
            var sphereCollider = handleSphere.GetComponent<Collider>();
            if (sphereCollider != null)
            {
                Destroy(sphereCollider);
            }
            
            // Replace the default material with our own
            var handleRenderer = handleSphere.GetComponent<MeshRenderer>();
            if (handleRenderer != null)
            {
                // Create and assign our custom material (replacing the default)
                Shader handleShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
                Material handleMaterial = new Material(handleShader);
                handleMaterial.color = handleColor;
                handleRenderer.sharedMaterial = handleMaterial;
            }
            
            // Initially hide if not in DragHandle mode
            handleSphere.SetActive(inputMode == InputMode.DragHandle);

            // Setup preview mesh
            previewObject = new GameObject("Fold Preview");
            previewObject.transform.SetParent(transform);
            
            var meshFilter = previewObject.AddComponent<MeshFilter>();
            var meshRenderer = previewObject.AddComponent<MeshRenderer>();
            
            previewMesh = new Mesh();
            meshFilter.mesh = previewMesh;
            
            // Only create fallback material if previewMaterial is not assigned
            if (previewMaterial == null)
            {
                Shader previewShader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
                var material = new Material(previewShader);
                material.color = previewColor;
                
                // Only set Standard shader properties if we're actually using Standard shader
                if (previewShader.name == "Standard")
                {
                    material.SetFloat("_Mode", 3);
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite", 0);
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.EnableKeyword("_ALPHABLEND_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                }
                material.renderQueue = 3000;
                previewMaterial = material;
            }

            meshRenderer.material = previewMaterial;

            // Setup correct axis line renderer
            correctAxisLine = new GameObject("Correct Axis Line");
            correctAxisLine.transform.SetParent(transform);
            correctAxisLineRenderer = correctAxisLine.AddComponent<LineRenderer>();
            
            // Configure LineRenderer with increased width for better visibility
            correctAxisLineRenderer.positionCount = 2;
            correctAxisLineRenderer.startWidth = correctAxisWidth;
            correctAxisLineRenderer.endWidth = correctAxisWidth;
            
            // Use unlit shader for better visibility and always render on top
            Shader lineShader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Diffuse");
            Material lineMat = new Material(lineShader);
            lineMat.color = correctAxisColor;
            lineMat.renderQueue = 4000; // Render after transparent objects to always be on top
            
            // Disable depth testing so line always renders on top
            lineMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            lineMat.SetInt("_ZWrite", 0);
            
            correctAxisLineRenderer.material = lineMat;
            correctAxisLineRenderer.startColor = correctAxisColor;
            correctAxisLineRenderer.endColor = correctAxisColor;
            correctAxisLineRenderer.useWorldSpace = true;
            correctAxisLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            correctAxisLineRenderer.receiveShadows = false;
            correctAxisLineRenderer.sortingOrder = 1000; // High sorting order for 2D rendering
            correctAxisLineRenderer.numCapVertices = 5; // Rounded caps for better visibility
            correctAxisLineRenderer.alignment = LineAlignment.View; // Always face camera
            
            // Initially hide the correct axis line
            correctAxisLine.SetActive(false);
        }

        private void Update()
        {
            if (paperMesh == null) return;

            if (enableInput)
            {
                if (inputMode == InputMode.ClickPlacement)
                {
                    HandleClickPlacement();
                }
                else if (inputMode == InputMode.DragHandle)
                {
                    HandleDragInput();
                }
            }

            UpdateVisualization();
        }

        private void UpdateVisualization()
        {
            // Update handle sphere position
            if (handleSphere != null && inputMode == InputMode.DragHandle)
            {
                handleSphere.SetActive(true);
                Vector3 handleWorld = paperMesh.UVToWorld(handleU, handleV);
                handleSphere.transform.position = handleWorld;
                handleSphere.transform.localScale = Vector3.one * (handleGrabRadius * 0.5f);
                
                // Update handle color
                var handleRenderer = handleSphere.GetComponent<MeshRenderer>();
                if (handleRenderer != null && handleRenderer.sharedMaterial != null)
                {
                    handleRenderer.sharedMaterial.color = handleColor;
                }
            }
            else if (handleSphere != null)
            {
                handleSphere.SetActive(false);
            }

            // Update preview mesh
            if (showPreview)
            {
                UpdatePreviewMesh();
                previewObject.SetActive(true);
            }
            else
            {
                previewObject.SetActive(false);
            }

            // Update correct axis line
            if (showCorrectAxis && correctAxisLineRenderer != null)
            {
                (Vector3 correctStart, Vector3 correctEnd) = correctAxis.ToWorldSpace(paperMesh);
                
                // Apply Z-offset towards camera to ensure line is always in front
                Camera cam = GetActiveCamera();
                if (cam != null)
                {
                    Vector3 toCameraDir = (cam.transform.position - correctStart).normalized;
                    correctStart += toCameraDir * correctAxisZOffset;
                    correctEnd += toCameraDir * correctAxisZOffset;
                }
                
                correctAxisLineRenderer.SetPosition(0, correctStart);
                correctAxisLineRenderer.SetPosition(1, correctEnd);
                correctAxisLineRenderer.startColor = correctAxisColor;
                correctAxisLineRenderer.endColor = correctAxisColor;
                correctAxisLineRenderer.startWidth = correctAxisWidth;
                correctAxisLineRenderer.endWidth = correctAxisWidth;
                correctAxisLine.SetActive(true);
            }
            else if (correctAxisLine != null)
            {
                correctAxisLine.SetActive(false);
            }
        }

        #region Click Placement Mode

        private void HandleClickPlacement()
        {
            if (Input.GetMouseButtonDown(0))
            {
                PlaceAxisPoint(true);
            }
            else if (Input.GetMouseButtonDown(1))
            {
                PlaceAxisPoint(false);
            }
        }

        private void PlaceAxisPoint(bool isStart)
        {
            Camera cam = GetActiveCamera();
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            
            if (paperMesh.RaycastPaper(ray, out RaycastHit hit))
            {
                Vector2 uv = paperMesh.WorldToUV(hit.point);
                
                if (isStart)
                {
                    currentAxis.u1 = uv.x;
                    currentAxis.v1 = uv.y;
                }
                else
                {
                    currentAxis.u2 = uv.x;
                    currentAxis.v2 = uv.y;
                }
                
                // Clear cached axis when manually placing points
                useCachedAxis = false;
            }
        }

        #endregion

        #region Drag Handle Mode

        private void HandleDragInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                TryStartDrag();
            }
            else if (Input.GetMouseButton(0) && isDraggingHandle)
            {
                UpdateDrag();
            }
            else if (Input.GetMouseButtonUp(0) && isDraggingHandle)
            {
                EndDrag();
            }
        }

        private void TryStartDrag()
        {
            Camera cam = GetActiveCamera();
            if (cam == null) return;

            Vector3 handleWorld = paperMesh.UVToWorld(handleU, handleV);

            // Check if mouse is near the handle
            Vector3 screenPos = cam.WorldToScreenPoint(handleWorld);
            float screenDist = Vector2.Distance(new Vector2(screenPos.x, screenPos.y), Input.mousePosition);
            float screenRadius = handleGrabRadius * Screen.height / 2f;

            if (screenDist <= screenRadius)
            {
                isDraggingHandle = true;
                // The origin is the initial position of the handle when drag starts
                originWorldPosition = handleWorld;
                dragConfirmed = false; // Reset confirmation state

                // Create drag plane - either using camera view plane or paper normal
                Vector3 planeNormal;
                if (useCameraPlane)
                {
                    // Use camera's forward direction as the plane normal (parallel to camera view)
                    planeNormal = cam.transform.forward;
                }
                else
                {
                    // Use the paper's normal at the handle location
                    planeNormal = paperMesh.GetWorldNormalAtUV(handleU, handleV);
                }
                
                dragPlane = new Plane(planeNormal, handleWorld);
            }
        }

        private void UpdateDrag()
        {
            Camera cam = GetActiveCamera();
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            if (dragPlane.Raycast(ray, out float enter))
            {
                Vector3 dragPoint = ray.GetPoint(enter);
                
                // Compute fold axis orthogonal to the line from origin to drag point
                ComputeFoldAxisFromDrag(originWorldPosition, dragPoint);
            }
        }

        private void EndDrag()
        {
            if (!isDraggingHandle)
                return;

            // Check if released close to starting point (origin)
            Camera cam = GetActiveCamera();
            if (cam != null)
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (dragPlane.Raycast(ray, out float enter))
                {
                    Vector3 releasePoint = ray.GetPoint(enter);
                    float distanceFromOrigin = Vector3.Distance(releasePoint, originWorldPosition);

                    if (distanceFromOrigin < releaseSnapDistance)
                    {
                        // Released close to origin - cancel the drag
                        isDraggingHandle = false;
                        dragConfirmed = false;
                        return;
                    }
                }
            }

            // Released far from origin - keep the drag and mark as confirmed
            dragConfirmed = true;
            isDraggingHandle = false;

            // Only auto-apply if enabled
            if (autoApplyOnRelease && paperMesh != null)
            {
                ApplyFold();
            }
        }

        /// <summary>
        /// Compute the fold axis orthogonal to the line connecting origin to target position
        /// The axis lies on the drag plane and is perpendicular to the displacement vector
        /// No UV snapping or edge intersection needed - just pure spatial computation
        /// </summary>
        private void ComputeFoldAxisFromDrag(Vector3 originWorld, Vector3 targetWorld)
        {
            // Convert to local space for calculations
            Vector3 originLocal = paperMesh.transform.InverseTransformPoint(originWorld);
            Vector3 targetLocal = paperMesh.transform.InverseTransformPoint(targetWorld);

            Vector3 displacement = targetLocal - originLocal;
            
            if (displacement.magnitude < 0.0001f)
            {
                return;
            }

            // Get the plane normal in local space
            Vector3 planeNormalLocal = paperMesh.transform.InverseTransformDirection(dragPlane.normal);

            // Fold axis is perpendicular to displacement and lies in the drag plane
            // This is the cross product of the plane normal and the displacement
            Vector3 foldAxisDirection = Vector3.Cross(planeNormalLocal, displacement).normalized;
            
            if (foldAxisDirection.magnitude < 0.0001f)
            {
                // Fallback if vectors are parallel
                foldAxisDirection = new Vector3(-displacement.y, displacement.x, 0).normalized;
            }

            // The fold axis passes through the midpoint of the displacement
            Vector3 midpoint = (originLocal + targetLocal) / 2f;

            // Create axis endpoints extending far enough to cover the entire paper
            // Use a large distance to ensure the axis spans the whole fold region
            float axisLength = Mathf.Max(paperMesh.Width, paperMesh.Height) * 2f;
            Vector3 axisStart = midpoint - foldAxisDirection * axisLength;
            Vector3 axisEnd = midpoint + foldAxisDirection * axisLength;

            // Ensure correct ordering: origin point should be on the moved side
            EnsureCorrectAxisOrdering(ref axisStart, ref axisEnd, originLocal, targetLocal);

            // Cache the spatial axis directly (no UV conversion loss)
            cachedAxisStartLocal = axisStart;
            cachedAxisEndLocal = axisEnd;
            useCachedAxis = true;

            // Also update currentAxis for compatibility (but preview will use cached version)
            Vector2 startUV = paperMesh.LocalToUV(axisStart);
            Vector2 endUV = paperMesh.LocalToUV(axisEnd);

            currentAxis.u1 = startUV.x;
            currentAxis.v1 = startUV.y;
            currentAxis.u2 = endUV.x;
            currentAxis.v2 = endUV.y;
        }

        /// <summary>
        /// Ensure the axis is oriented so the start position is on the moved side
        /// </summary>
        private void EnsureCorrectAxisOrdering(ref Vector3 axisStart, ref Vector3 axisEnd, 
                                               Vector3 startLocal, Vector3 targetLocal)
        {
            Vector3 foldDir = (axisEnd - axisStart).normalized;
            Vector3 foldNormal = Vector3.Cross(foldDir, Vector3.forward).normalized;

            float dotStart = Vector3.Dot(startLocal - axisStart, foldNormal);
            float dotTarget = Vector3.Dot(targetLocal - axisStart, foldNormal);

            // We want start > 0 (moved side) and target <= 0 (static side)
            if (!(dotStart > 0f && dotTarget <= 0f))
            {
                var tmp = axisStart;
                axisStart = axisEnd;
                axisEnd = tmp;
            }
        }

        #endregion

        #region Preview Mesh

        private void UpdatePreviewMesh()
        {
            // Use cached spatial axis if available (during dragging), otherwise convert from UV
            Vector3 localStart, localEnd;
            if (useCachedAxis)
            {
                localStart = cachedAxisStartLocal;
                localEnd = cachedAxisEndLocal;
            }
            else
            {
                (localStart, localEnd) = currentAxis.ToLocalSpace(paperMesh);
            }

            Vector3 foldDirection = (localEnd - localStart).normalized;
            Vector3 foldNormal = Vector3.Cross(foldDirection, Vector3.forward).normalized;

            Vector3[] originalVertices = paperMesh.GetVertices();
            Vector3[] previewVertices = new Vector3[originalVertices.Length];
            
            float angleToUse = (inputMode == InputMode.DragHandle && isDraggingHandle) 
                ? dragFoldAngle 
                : previewDegrees;
            
            Quaternion rotation = Quaternion.AngleAxis(angleToUse, foldDirection);
            
            // Build predicate from tag expression if provided
            System.Func<HashSet<string>, bool> predicate = null;
            if (!string.IsNullOrEmpty(tagExpression))
            {
                predicate = (tags) => BooleanExpressionEvaluator.Evaluate(tagExpression, tags);
            }
            
            for (int i = 0; i < originalVertices.Length; i++)
            {
                Vector3 toVertex = originalVertices[i] - localStart;
                float side = Vector3.Dot(toVertex, foldNormal);
                
                // Check if vertex is on the moved side (positive side of fold normal)
                bool isOnMovedSide = side > 0.0001f;
                
                // Check if vertex passes the tag filter
                bool passesFilter = true;
                if (predicate != null)
                {
                    HashSet<string> vertexTags = paperMesh.GetVertexTags(i);
                    passesFilter = predicate(vertexTags);
                }
                
                // Only fold vertices that are on moved side AND pass the filter
                if (isOnMovedSide && passesFilter)
                {
                    previewVertices[i] = localStart + rotation * toVertex;
                }
                else
                {
                    previewVertices[i] = originalVertices[i];
                }
            }

            previewMesh.vertices = previewVertices;
            previewMesh.triangles = paperMesh.GetMesh().triangles;
            previewMesh.RecalculateNormals();
            
            previewObject.transform.position = paperMesh.transform.position;
            previewObject.transform.rotation = paperMesh.transform.rotation;
        }

        #endregion

        #region Public API - Fold Axis Manipulation

        /// <summary>
        /// Set the fold axis directly using UV coordinates
        /// </summary>
        public void SetFoldAxis(FoldAxis axis)
        {
            currentAxis = axis;
            useCachedAxis = false; // Clear cache when setting axis externally
        }

        /// <summary>
        /// Set the fold axis using world space coordinates
        /// </summary>
        public void SetFoldAxisWorld(Vector3 worldStart, Vector3 worldEnd)
        {
            currentAxis = FoldAxis.FromWorldSpace(worldStart, worldEnd, paperMesh);
            useCachedAxis = false; // Clear cache when setting axis externally
        }

        /// <summary>
        /// Set the fold axis using local space coordinates
        /// </summary>
        public void SetFoldAxisLocal(Vector3 localStart, Vector3 localEnd)
        {
            currentAxis = FoldAxis.FromLocalSpace(localStart, localEnd, paperMesh);
            useCachedAxis = false; // Clear cache when setting axis externally
        }

        /// <summary>
        /// Get the current fold axis
        /// </summary>
        public FoldAxis GetFoldAxis()
        {
            return currentAxis;
        }

        /// <summary>
        /// Set the preview angle for visualizing the fold
        /// </summary>
        public void SetPreviewDegrees(float degrees)
        {
            previewDegrees = degrees;
        }

        /// <summary>
        /// Apply the current fold axis to the paper mesh
        /// Uses the current previewDegrees and tagExpression
        /// </summary>
        public void ApplyFold()
        {
            if (paperMesh == null) return;

            // Build predicate from tag expression if provided
            System.Func<HashSet<string>, bool> predicate = null;
            if (!string.IsNullOrEmpty(tagExpression))
            {
                predicate = (tags) => BooleanExpressionEvaluator.Evaluate(tagExpression, tags);
            }

            // Use cached spatial axis if available (during/after dragging), otherwise use currentAxis
            if (useCachedAxis)
            {
                paperMesh.FoldLocal(cachedAxisStartLocal, cachedAxisEndLocal, previewDegrees, 
                    string.IsNullOrEmpty(previewTag) ? null : previewTag, predicate);
                
                // Clear cached axis after applying
                useCachedAxis = false;
            }
            else
            {
                paperMesh.Fold(currentAxis, previewDegrees, 
                    string.IsNullOrEmpty(previewTag) ? null : previewTag, predicate);
            }
        }

        /// <summary>
        /// Apply fold with a specific tag filter (overrides tagExpression)
        /// </summary>
        public void ApplyFoldWithTag(string tagToUse)
        {
            if (paperMesh == null) return;

            System.Func<HashSet<string>, bool> predicate = null;
            if (!string.IsNullOrEmpty(tagToUse))
            {
                predicate = (tags) => tags != null && tags.Contains(tagToUse);
            }

            paperMesh.Fold(currentAxis, previewDegrees, 
                string.IsNullOrEmpty(previewTag) ? null : previewTag, predicate);
        }

        /// <summary>
        /// Apply fold with full control over all parameters
        /// </summary>
        public void ApplyFoldCustom(FoldAxis axis, float degrees, string tagName = null, string tagExpr = null)
        {
            if (paperMesh == null) return;

            System.Func<HashSet<string>, bool> predicate = null;
            if (!string.IsNullOrEmpty(tagExpr))
            {
                predicate = (tags) => BooleanExpressionEvaluator.Evaluate(tagExpr, tags);
            }

            paperMesh.Fold(axis, degrees, tagName, predicate);
        }

        #endregion

        #region Public API - Handle and Tag Configuration

        public void SetHandleUV(Vector2 uv)
        {
            handleU = uv.x;
            handleV = uv.y;
        }

        public Vector2 GetHandleUV()
        {
            return new Vector2(handleU, handleV);
        }

        public void SetTagExpression(string expression)
        {
            tagExpression = expression;
        }

        public string GetTagExpression()
        {
            return tagExpression;
        }

        public void SetPreviewTag(string tag)
        {
            previewTag = tag;
        }

        public string GetPreviewTag()
        {
            return previewTag;
        }

        #endregion

        #region Public API - State Queries

        public void ResetDragState()
        {
            isDraggingHandle = false;
            dragConfirmed = false;
        }

        public bool IsDragConfirmed()
        {
            return dragConfirmed;
        }

        public bool IsDragging()
        {
            return isDraggingHandle;
        }

        public void TogglePreview(bool show)
        {
            showPreview = show;
        }

        public List<string> GetAllTags()
        {
            if (paperMesh == null) return new List<string>();
            return new List<string>(paperMesh.GetAllTags());
        }

        public void SetCorrectAxis(FoldAxis axis)
        {
            correctAxis = axis;
        }

        public FoldAxis GetCorrectAxis()
        {
            return correctAxis;
        }

        public void ShowCorrectAxis(bool show)
        {
            showCorrectAxis = show;
        }

        /// <summary>
        /// Get the current fold axis (accounts for cached spatial axis during dragging)
        /// </summary>
        public FoldAxis GetCurrentFoldAxis()
        {
            if (useCachedAxis && paperMesh != null)
            {
                // Convert cached local space axis back to UV
                Vector2 startUV = paperMesh.LocalToUV(cachedAxisStartLocal);
                Vector2 endUV = paperMesh.LocalToUV(cachedAxisEndLocal);
                return new FoldAxis(startUV.x, startUV.y, endUV.x, endUV.y);
            }
            return currentAxis;
        }

        #endregion

        #region Utilities

        private Camera GetActiveCamera()
        {
            return overrideCamera != null ? overrideCamera : Camera.main;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (paperMesh == null || !showAxisGizmos) return;

            // Draw the fold axis - use cached spatial axis if available, otherwise convert from UV
            Vector3 start, end;
            if (useCachedAxis)
            {
                start = paperMesh.transform.TransformPoint(cachedAxisStartLocal);
                end = paperMesh.transform.TransformPoint(cachedAxisEndLocal);
            }
            else
            {
                (start, end) = currentAxis.ToWorldSpace(paperMesh);
            }
            
            // Draw axis line
            Gizmos.color = axisColor;
            Gizmos.DrawLine(start, end);
            
            // Draw axis endpoints (green = start/moved side, blue = end/static side)
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(start, axisThickness * 2);

            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(end, axisThickness * 2);

            // In DragHandle mode, visualize the drag interaction
            if (inputMode == InputMode.DragHandle && isDraggingHandle)
            {
                // Show drag origin (where the drag started) - magenta sphere
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(originWorldPosition, handleGrabRadius * 1.5f);
                
                // Show current drag point and displacement vector
                Camera cam = GetActiveCamera();
                if (cam != null)
                {
                    Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                    if (dragPlane.Raycast(ray, out float enter))
                    {
                        Vector3 currentDragPoint = ray.GetPoint(enter);
                        
                        // Yellow line shows the displacement (origin -> cursor)
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawLine(originWorldPosition, currentDragPoint);
                        Gizmos.DrawSphere(currentDragPoint, handleGrabRadius * 0.7f);
                        
                        // Cyan cross at midpoint shows axis is perpendicular to displacement
                        Vector3 originLocal = paperMesh.transform.InverseTransformPoint(originWorldPosition);
                        Vector3 targetLocal = paperMesh.transform.InverseTransformPoint(currentDragPoint);
                        Vector3 midpointLocal = (originLocal + targetLocal) / 2f;
                        Vector3 midpointWorld = paperMesh.transform.TransformPoint(midpointLocal);
                        
                        Vector3 axisDir = (end - start).normalized;
                        float crossSize = handleGrabRadius * 1.0f;
                        
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawLine(midpointWorld - axisDir * crossSize, midpointWorld + axisDir * crossSize);
                        Gizmos.DrawSphere(midpointWorld, axisThickness);
                    }
                }
            }
        }

        #endregion

        #region Properties

        public FoldAxis CurrentAxis
        {
            get => currentAxis;
            set => currentAxis = value;
        }

        public float PreviewDegrees
        {
            get => previewDegrees;
            set => previewDegrees = value;
        }

        public string PreviewTag
        {
            get => previewTag;
            set => previewTag = value;
        }

        public InputMode CurrentInputMode
        {
            get => inputMode;
            set
            {
                inputMode = value;
                if (handleSphere != null)
                {
                    handleSphere.SetActive(inputMode == InputMode.DragHandle);
                }
            }
        }

        public float DragFoldAngle
        {
            get => dragFoldAngle;
            set => dragFoldAngle = value;
        }

        public bool EnableInput
        {
            get => enableInput;
            set => enableInput = value;
        }

        public bool UseCameraPlane
        {
            get => useCameraPlane;
            set => useCameraPlane = value;
        }

        #endregion
    }
}