using UnityEngine;
using System.Collections.Generic;

namespace PaperFolding
{
    /// <summary>
    /// Visualizes fold axes and provides interactive fold preview
    /// </summary>
    public class FoldVisualizer : MonoBehaviour
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
        [SerializeField] private Color startColor = Color.green;
        [SerializeField] private Color endColor = Color.blue;
        [SerializeField] private Color handleColor = Color.cyan;
        [SerializeField] private Color previewColor = new Color(1f, 1f, 0f, 0.3f);
        [SerializeField] private float axisThickness = 0.02f;
        [SerializeField] private bool showPreview = true;
        
        [Header("Interaction")]
        [SerializeField] private InputMode inputMode = InputMode.ClickPlacement;
        [SerializeField] private bool enableMousePlacement = true;
        [SerializeField] private Camera overrideCamera = null;
        
        [Header("Drag Handle Settings")]
        [SerializeField] private float handleU = 0.5f;
        [SerializeField] private float handleV = 0.5f;
        [SerializeField] private float handleGrabRadius = 0.05f;
        [SerializeField] private float dragFoldAngle = 180f; // Can be toggled to -180
        [SerializeField] private bool autoApplyOnRelease = true;
        
        private bool handleSnappedToEdge = false;
        
        [Header("Tag Visualization")]
        [SerializeField] private bool showTagHighlights = false;
        [SerializeField] private string selectedTag = "";
        [SerializeField] private Color tagHighlightColor = Color.yellow;
        [SerializeField] private float tagHighlightSize = 0.02f;
        [SerializeField] private string quickFoldTag = "";
        
        [Header("Current Fold Axis")]
        [SerializeField] private FoldAxis currentAxis = new FoldAxis(0.2f, 0f, 0.8f, 1f);
        [SerializeField] private float previewDegrees = 90f;
        [SerializeField] private string previewTag = "";

        private GameObject axisLineObject;
        private LineRenderer axisLineRenderer;
        private Mesh previewMesh;
        private GameObject previewObject;

        // Drag handle state
        private bool isDraggingHandle = false;
        private Vector3 handleOriginalWorld;
        private Vector3 handleDragPlaneNormal;
        private Vector3 handleCurrentWorld;

        private void Start()
        {
            if (paperMesh == null)
            {
                paperMesh = GetComponent<PaperMesh>();
            }

            SetupAxisLine();
            SetupPreviewMesh();
            
            // Snap handle to nearest edge on start
            if (inputMode == InputMode.DragHandle)
            {
                SnapHandleToEdge();
            }
        }

        private void SetupAxisLine()
        {
            axisLineObject = new GameObject("Fold Axis Line");
            axisLineObject.transform.SetParent(transform);
            axisLineRenderer = axisLineObject.AddComponent<LineRenderer>();
            
            axisLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            axisLineRenderer.startColor = axisColor;
            axisLineRenderer.endColor = axisColor;
            axisLineRenderer.startWidth = axisThickness;
            axisLineRenderer.endWidth = axisThickness;
            axisLineRenderer.positionCount = 2;
            axisLineRenderer.useWorldSpace = true;
        }

        private void SetupPreviewMesh()
        {
            previewObject = new GameObject("Fold Preview");
            previewObject.transform.SetParent(transform);
            
            var meshFilter = previewObject.AddComponent<MeshFilter>();
            var meshRenderer = previewObject.AddComponent<MeshRenderer>();
            
            previewMesh = new Mesh();
            meshFilter.mesh = previewMesh;
            
            var material = new Material(Shader.Find("Standard"));
            material.color = previewColor;
            material.SetFloat("_Mode", 3); // Transparent mode
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;

            meshRenderer.material = previewMaterial ?? material;
        }

        private void Update()
        {
            if (paperMesh != null)
            {
                if (inputMode == InputMode.ClickPlacement)
                {
                    HandleMousePlacement();
                }
                else if (inputMode == InputMode.DragHandle)
                {
                    HandleDragInput();
                }

                UpdateAxisVisualization();
                
                if (showPreview)
                {
                    UpdatePreviewMesh();
                }
                else
                {
                    previewObject.SetActive(false);
                }
            }
        }

        private void UpdateAxisVisualization()
        {
            var (start, end) = currentAxis.ToWorldSpace(paperMesh);
            axisLineRenderer.SetPosition(0, start);
            axisLineRenderer.SetPosition(1, end);
        }

        #region Click Placement Mode

        /// <summary>
        /// Handle left/right mouse clicks to place start/end axis points on the paper.
        /// Left click -> start point, Right click -> end point.
        /// </summary>
        private void HandleMousePlacement()
        {
            if (!enableMousePlacement) return;

            if (Input.GetMouseButtonDown(0)) // left click -> start
            {
                TryPlacePoint(true);
            }
            else if (Input.GetMouseButtonDown(1)) // right click -> end
            {
                TryPlacePoint(false);
            }
        }

        private void TryPlacePoint(bool isStart)
        {
            if (paperMesh == null) return;

            if (TryGetMouseHitOnPaper(out Vector3 worldHit))
            {
                Vector2 uv = MapWorldToUV(worldHit);
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

                // Refresh visualization immediately
                UpdateAxisVisualization();
            }
        }

        #endregion

        #region Drag Handle Mode

        /// <summary>
        /// Snap the handle to the nearest edge or corner of the paper
        /// </summary>
        private void SnapHandleToEdge()
        {
            // Define the four edges and check which is closest
            float distToLeft = Mathf.Abs(handleU - 0f);
            float distToRight = Mathf.Abs(handleU - 1f);
            float distToBottom = Mathf.Abs(handleV - 0f);
            float distToTop = Mathf.Abs(handleV - 1f);

            float minDist = Mathf.Min(distToLeft, distToRight, distToBottom, distToTop);

            // Snap to nearest edge
            if (minDist == distToLeft)
            {
                handleU = 0f;
            }
            else if (minDist == distToRight)
            {
                handleU = 1f;
            }
            else if (minDist == distToBottom)
            {
                handleV = 0f;
            }
            else if (minDist == distToTop)
            {
                handleV = 1f;
            }

            // Clamp to ensure it stays on the edge
            if (handleU == 0f || handleU == 1f)
            {
                handleV = Mathf.Clamp01(handleV);
            }
            else if (handleV == 0f || handleV == 1f)
            {
                handleU = Mathf.Clamp01(handleU);
            }

            handleSnappedToEdge = true;
        }

        /// <summary>
        /// Handle drag input for the handle-based folding mode
        /// </summary>
        private void HandleDragInput()
        {
            if (!enableMousePlacement) return;

            if (Input.GetMouseButtonDown(0))
            {
                TryStartDrag();
            }
            else if (Input.GetMouseButton(0) && isDraggingHandle)
            {
                // Continue updating drag while the mouse button is held
                UpdateDrag();
            }
            else if (Input.GetMouseButtonUp(0) && isDraggingHandle)
            {
                // If auto-apply is enabled, end drag on mouse-up as before.
                if (autoApplyOnRelease)
                {
                    EndDrag();
                }
                else
                {
                    // If auto-apply is disabled, only end the drag if the handle is
                    // back within the grab radius of its original position. This
                    // ensures quick click-drag-release that didn't move still ends.
                    float dist = Vector3.Distance(handleCurrentWorld, handleOriginalWorld);
                    if (dist <= handleGrabRadius)
                    {
                        EndDrag();
                    }
                    // Otherwise, keep dragging until the user moves back close.
                }
            }
        }

        private void TryStartDrag()
        {
            if (paperMesh == null) return;

            // Ensure handle is snapped to edge
            if (!handleSnappedToEdge)
            {
                SnapHandleToEdge();
            }

            // Get handle world position
            Vector3 handleWorld = paperMesh.UVToWorld(handleU, handleV);

            // Check if mouse is near the handle
            Camera cam = GetActiveCamera();
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(handleWorld);
            float screenDist = Vector2.Distance(new Vector2(screenPos.x, screenPos.y), Input.mousePosition);
            
            // Convert grab radius to screen space for consistent interaction
            float screenRadius = handleGrabRadius * Screen.height / 2f;

            if (screenDist <= screenRadius)
            {
                isDraggingHandle = true;
                handleOriginalWorld = handleWorld;
                handleCurrentWorld = handleWorld;

                // Compute the drag plane normal (perpendicular to current paper surface at handle)
                handleDragPlaneNormal = ComputeLocalNormalAtUV(handleU, handleV);
            }
        }

        private void UpdateDrag()
        {
            if (paperMesh == null) return;

            Camera cam = GetActiveCamera();
            if (cam == null) return;

            // Cast ray from mouse and intersect with drag plane
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Plane dragPlane = new Plane(handleDragPlaneNormal, handleOriginalWorld);

            float enter;
            if (dragPlane.Raycast(ray, out enter))
            {
                Vector3 dragPoint = ray.GetPoint(enter);
                
                // Use the dragged point directly (do NOT snap while dragging).
                // The original handle location is snapped once when set/start-drag begins.
                Vector2 dragUV = MapWorldToUV(dragPoint);

                // Convert directly back to world space without snapping to edges
                handleCurrentWorld = paperMesh.UVToWorld(dragUV.x, dragUV.y);

                // Compute the fold axis that would move handleOriginalWorld to handleCurrentWorld
                ComputeFoldAxisFromDrag();
            }
        }

        private void EndDrag()
        {
            isDraggingHandle = false;

            if (autoApplyOnRelease && paperMesh != null)
            {
                // Apply the fold with the drag angle
                paperMesh.Fold(currentAxis, dragFoldAngle, string.IsNullOrEmpty(previewTag) ? null : previewTag);
            }
        }

        /// <summary>
        /// Snap a UV coordinate to the nearest edge of the paper
        /// </summary>
        private Vector2 SnapUVToEdge(Vector2 uv)
        {
            // Calculate distances to each edge
            float distToLeft = Mathf.Abs(uv.x - 0f);
            float distToRight = Mathf.Abs(uv.x - 1f);
            float distToBottom = Mathf.Abs(uv.y - 0f);
            float distToTop = Mathf.Abs(uv.y - 1f);

            float minDist = Mathf.Min(distToLeft, distToRight, distToBottom, distToTop);

            Vector2 snapped = uv;

            // Snap to the nearest edge
            if (minDist == distToLeft)
            {
                snapped.x = 0f;
                snapped.y = Mathf.Clamp01(uv.y);
            }
            else if (minDist == distToRight)
            {
                snapped.x = 1f;
                snapped.y = Mathf.Clamp01(uv.y);
            }
            else if (minDist == distToBottom)
            {
                snapped.y = 0f;
                snapped.x = Mathf.Clamp01(uv.x);
            }
            else if (minDist == distToTop)
            {
                snapped.y = 1f;
                snapped.x = Mathf.Clamp01(uv.x);
            }

            return snapped;
        }

        /// <summary>
        /// Compute the fold axis that would move the handle from its original position to the dragged position
        /// when folded by dragFoldAngle degrees.
        /// 
        /// For a 180° fold:
        /// - The fold axis passes through the midpoint between original and target
        /// - The fold axis is perpendicular to the displacement vector
        /// </summary>
        private void ComputeFoldAxisFromDrag()
        {
            Vector3 originalLocal = paperMesh.transform.InverseTransformPoint(handleOriginalWorld);
            Vector3 targetLocal = paperMesh.transform.InverseTransformPoint(handleCurrentWorld);

            // Calculate the displacement vector
            Vector3 displacement = targetLocal - originalLocal;
            
            if (displacement.magnitude < 0.0001f)
            {
                // No movement, keep current axis
                return;
            }

            // The fold axis must:
            // 1. Pass through the midpoint of the displacement
            // 2. Be perpendicular to the displacement vector
            
            Vector3 midpoint = (originalLocal + targetLocal) / 2f;

            // Get the paper normal at this location (assuming it's mostly flat in XY plane)
            Vector3 paperNormal = Vector3.forward;
            
            // The fold axis direction is perpendicular to both:
            // - The displacement vector
            // - The paper normal
            Vector3 foldAxisDirection = Vector3.Cross(paperNormal, displacement).normalized;
            
            // Handle edge case where displacement is parallel to normal
            if (foldAxisDirection.magnitude < 0.0001f)
            {
                // If displacement is along Z, use a perpendicular direction in XY
                foldAxisDirection = new Vector3(-displacement.y, displacement.x, 0).normalized;
            }

            // Now we need to extend this axis to the edges of the paper
            // The paper bounds in local space are from (-width/2, -height/2) to (width/2, height/2)
            float halfWidth = paperMesh.Width / 2f;
            float halfHeight = paperMesh.Height / 2f;

            // Find intersections of the fold axis line with the paper boundary
            // Parametric line: P = midpoint + t * foldAxisDirection
            // We need to find t values where the line intersects the rectangle edges

            List<Vector3> intersections = new List<Vector3>();

            // Check intersection with left edge (x = -halfWidth)
            if (Mathf.Abs(foldAxisDirection.x) > 0.0001f)
            {
                float t = (-halfWidth - midpoint.x) / foldAxisDirection.x;
                Vector3 point = midpoint + t * foldAxisDirection;
                if (point.y >= -halfHeight && point.y <= halfHeight)
                {
                    intersections.Add(point);
                }
            }

            // Check intersection with right edge (x = halfWidth)
            if (Mathf.Abs(foldAxisDirection.x) > 0.0001f)
            {
                float t = (halfWidth - midpoint.x) / foldAxisDirection.x;
                Vector3 point = midpoint + t * foldAxisDirection;
                if (point.y >= -halfHeight && point.y <= halfHeight)
                {
                    intersections.Add(point);
                }
            }

            // Check intersection with bottom edge (y = -halfHeight)
            if (Mathf.Abs(foldAxisDirection.y) > 0.0001f)
            {
                float t = (-halfHeight - midpoint.y) / foldAxisDirection.y;
                Vector3 point = midpoint + t * foldAxisDirection;
                if (point.x >= -halfWidth && point.x <= halfWidth)
                {
                    intersections.Add(point);
                }
            }

            // Check intersection with top edge (y = halfHeight)
            if (Mathf.Abs(foldAxisDirection.y) > 0.0001f)
            {
                float t = (halfHeight - midpoint.y) / foldAxisDirection.y;
                Vector3 point = midpoint + t * foldAxisDirection;
                if (point.x >= -halfWidth && point.x <= halfWidth)
                {
                    intersections.Add(point);
                }
            }

            // We should have at least 2 intersection points
            if (intersections.Count >= 2)
            {
                // Use the first two intersections as our axis endpoints
                Vector3 axisStart = intersections[0];
                Vector3 axisEnd = intersections[1];

                // Ensure axis ordering causes the ORIGINAL handle position to be on the moved side
                // Compute fold normal from start->end so it matches UpdatePreviewMesh logic
                Vector3 foldDir = (axisEnd - axisStart).normalized;
                Vector3 foldNormal = Vector3.Cross(foldDir, Vector3.forward).normalized;

                float dotOriginal = Vector3.Dot(originalLocal - axisStart, foldNormal);
                float dotTarget = Vector3.Dot(targetLocal - axisStart, foldNormal);

                // In UpdatePreviewMesh, vertices with side > 0 are moved. We want the ORIGINAL
                // handle location to be on the moved side and the target location to be on the static side.
                // If that's not the case, swap the axis endpoints.
                if (!(dotOriginal > 0f && dotTarget <= 0f))
                {
                    // Swap
                    var tmp = axisStart;
                    axisStart = axisEnd;
                    axisEnd = tmp;
                }

                // Convert to UV coordinates
                Vector2 startUV = MapLocalToUV(axisStart);
                Vector2 endUV = MapLocalToUV(axisEnd);

                // Update current axis
                currentAxis.u1 = startUV.x;
                currentAxis.v1 = startUV.y;
                currentAxis.u2 = endUV.x;
                currentAxis.v2 = endUV.y;
            }
            else if (intersections.Count == 1)
            {
                // Shouldn't happen for a line through the interior, but handle gracefully
                // Extend from the midpoint in both directions
                Vector3 axisStart = midpoint - foldAxisDirection * 2f;
                Vector3 axisEnd = midpoint + foldAxisDirection * 2f;
                
                Vector2 startUV = MapLocalToUV(axisStart);
                Vector2 endUV = MapLocalToUV(axisEnd);
                
                // Ensure ordering for single-intersection fallback too
                Vector3 fallbackFoldDir = (axisEnd - axisStart).normalized;
                Vector3 fallbackFoldNormal = Vector3.Cross(fallbackFoldDir, Vector3.forward).normalized;
                float fallbackDotOriginal = Vector3.Dot(originalLocal - axisStart, fallbackFoldNormal);
                float fallbackDotTarget = Vector3.Dot(targetLocal - axisStart, fallbackFoldNormal);

                if (!(fallbackDotOriginal > 0f && fallbackDotTarget <= 0f))
                {
                    var tmp = axisStart;
                    axisStart = axisEnd;
                    axisEnd = tmp;

                    startUV = MapLocalToUV(axisStart);
                    endUV = MapLocalToUV(axisEnd);
                }

                currentAxis.u1 = Mathf.Clamp01(startUV.x);
                currentAxis.v1 = Mathf.Clamp01(startUV.y);
                currentAxis.u2 = Mathf.Clamp01(endUV.x);
                currentAxis.v2 = Mathf.Clamp01(endUV.y);
            }
            
        }

        /// <summary>
        /// Compute the local normal of the paper surface at a given UV coordinate
        /// by averaging nearby triangle normals
        /// </summary>
        private Vector3 ComputeLocalNormalAtUV(float u, float v)
        {
            // Get the world position and use mesh normals
            Vector3 worldPos = paperMesh.UVToWorld(u, v);
            
            // Find the closest vertex and use its normal
            Vector3[] vertices = paperMesh.GetVertices();
            List<VertexData> vertexData = paperMesh.GetVertexData();
            
            int closestIndex = -1;
            float minDist = float.MaxValue;
            
            for (int i = 0; i < vertexData.Count; i++)
            {
                float dist = Mathf.Abs(vertexData[i].u - u) + Mathf.Abs(vertexData[i].v - v);
                if (dist < minDist)
                {
                    minDist = dist;
                    closestIndex = i;
                }
            }

            if (closestIndex >= 0)
            {
                // Get the normal by computing cross product of edges
                // For simplicity, use the mesh's computed normal
                Mesh mesh = paperMesh.GetMesh();
                Vector3[] normals = mesh.normals;
                if (closestIndex < normals.Length)
                {
                    Vector3 localNormal = normals[closestIndex];
                    return paperMesh.transform.TransformDirection(localNormal);
                }
            }

            // Fallback to forward direction
            return paperMesh.transform.forward;
        }

        private Vector2 MapLocalToUV(Vector3 localPoint)
        {
            float u = (localPoint.x / paperMesh.Width) + 0.5f;
            float v = (localPoint.y / paperMesh.Height) + 0.5f;
            return new Vector2(u, v);
        }

        #endregion

        #region Common Utilities

        private Camera GetActiveCamera()
        {
            if (overrideCamera != null) return overrideCamera;
            return Camera.main;
        }

        private bool TryGetMouseHitOnPaper(out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;
            Camera cam = GetActiveCamera();
            if (cam == null || paperMesh == null) return false;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            Collider col = paperMesh.GetComponent<Collider>();
            if (col != null)
            {
                if (col.Raycast(ray, out hit, Mathf.Infinity))
                {
                    worldPoint = hit.point;
                    return true;
                }
                return false;
            }

            if (Physics.Raycast(ray, out hit, Mathf.Infinity))
            {
                if (hit.collider != null && hit.collider.gameObject == paperMesh.gameObject)
                {
                    worldPoint = hit.point;
                    return true;
                }
            }

            return false;
        }

        private Vector2 MapWorldToUV(Vector3 worldPoint)
        {
            Vector3 local = paperMesh.transform.InverseTransformPoint(worldPoint);
            float u = (local.x / paperMesh.Width) + 0.5f;
            float v = (local.y / paperMesh.Height) + 0.5f;
            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);
            return new Vector2(u, v);
        }

        #endregion

        #region Preview and Mesh Updates

        private void UpdatePreviewMesh()
        {
            previewObject.SetActive(true);
            
            var (worldStart, worldEnd) = currentAxis.ToWorldSpace(paperMesh);
            Vector3 localStart = paperMesh.transform.InverseTransformPoint(worldStart);
            Vector3 localEnd = paperMesh.transform.InverseTransformPoint(worldEnd);
            Vector3 foldDirection = (localEnd - localStart).normalized;
            Vector3 foldNormal = Vector3.Cross(foldDirection, Vector3.forward).normalized;

            Vector3[] originalVertices = paperMesh.GetVertices();
            Vector3[] previewVertices = new Vector3[originalVertices.Length];
            
            // Use drag angle in drag mode, otherwise use preview degrees
            float angleToUse = (inputMode == InputMode.DragHandle && isDraggingHandle) ? dragFoldAngle : previewDegrees;
            Quaternion rotation = Quaternion.AngleAxis(angleToUse, foldDirection);
            
            for (int i = 0; i < originalVertices.Length; i++)
            {
                Vector3 toVertex = originalVertices[i] - localStart;
                float side = Vector3.Dot(toVertex, foldNormal);
                
                if (side > 0.0001f)
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

        #region Public API

        /// <summary>
        /// Set the fold axis to visualize
        /// </summary>
        public void SetFoldAxis(FoldAxis axis)
        {
            currentAxis = axis;
        }

        /// <summary>
        /// Set the preview angle in degrees
        /// </summary>
        public void SetPreviewDegrees(float degrees)
        {
            previewDegrees = degrees;
        }

        /// <summary>
        /// Apply the current preview as an actual fold
        /// </summary>
        public void ApplyFold()
        {
            if (isDraggingHandle) {
                EndDrag();
            }

            if (paperMesh != null)
            {
                paperMesh.Fold(currentAxis, previewDegrees, string.IsNullOrEmpty(previewTag) ? null : previewTag);
            }
        }

        /// <summary>
        /// Toggle preview visibility
        /// </summary>
        public void TogglePreview(bool show)
        {
            showPreview = show;
        }

        /// <summary>
        /// Return a list of all distinct tags that exist on the current paper mesh.
        /// Useful for demo UI to populate a selection list.
        /// </summary>
        public List<string> GetAllTags()
        {
            var result = new HashSet<string>();
            if (paperMesh == null) return new List<string>();

            var vdata = paperMesh.GetVertexData();
            if (vdata == null) return new List<string>();

            foreach (var vd in vdata)
            {
                if (vd.tags == null) continue;
                foreach (var t in vd.tags)
                {
                    result.Add(t);
                }
            }

            return new List<string>(result);
        }

        /// <summary>
        /// Apply the current preview as an actual fold, optionally restricting the fold to vertices that contain the provided tag.
        /// If tagToUse is null or empty, the fold affects all vertices (same as ApplyFold()).
        /// </summary>
        public void ApplyFoldWithTag(string tagToUse)
        {
            if (paperMesh == null) return;

            if (isDraggingHandle) {
                EndDrag();
            }

            System.Func<HashSet<string>, bool> predicate = null;
            if (!string.IsNullOrEmpty(tagToUse))
            {
                predicate = (tags) => tags != null && tags.Contains(tagToUse);
            }

            paperMesh.Fold(currentAxis, previewDegrees, string.IsNullOrEmpty(previewTag) ? null : previewTag, predicate);
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (paperMesh == null) return;

            // Draw axis endpoints
            var (start, end) = currentAxis.ToWorldSpace(paperMesh);
            // Draw start and end with distinct colors for easier editing
            Gizmos.color = startColor;
            Gizmos.DrawSphere(start, axisThickness * 2);

            Gizmos.color = endColor;
            Gizmos.DrawSphere(end, axisThickness * 2);

            // Draw connecting axis line in the configured axis color
            Gizmos.color = axisColor;
            Gizmos.DrawLine(start, end);

            // Draw handle in drag mode
            if (inputMode == InputMode.DragHandle)
            {
                Vector3 handleWorld = paperMesh.UVToWorld(handleU, handleV);
                Gizmos.color = handleColor;
                Gizmos.DrawWireSphere(handleWorld, handleGrabRadius);
                Gizmos.DrawSphere(handleWorld, handleGrabRadius * 0.5f);
                
                if (isDraggingHandle)
                {
                    // Draw line from original to current position
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(handleOriginalWorld, handleCurrentWorld);
                    Gizmos.DrawSphere(handleCurrentWorld, handleGrabRadius * 0.7f);
                    
                    // Draw the midpoint
                    Vector3 originalLocal = paperMesh.transform.InverseTransformPoint(handleOriginalWorld);
                    Vector3 targetLocal = paperMesh.transform.InverseTransformPoint(handleCurrentWorld);
                    Vector3 midpointLocal = (originalLocal + targetLocal) / 2f;
                    Vector3 midpointWorld = paperMesh.transform.TransformPoint(midpointLocal);
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawSphere(midpointWorld, handleGrabRadius * 0.3f);
                }
            }

            // Draw tag highlights (if enabled)
            if (showTagHighlights && !string.IsNullOrEmpty(selectedTag))
            {
                var vdata = paperMesh.GetVertexData();
                var verts = paperMesh.GetVertices();
                if (vdata != null && verts != null)
                {
                    int count = Mathf.Min(vdata.Count, verts.Length);
                    Gizmos.color = tagHighlightColor;
                    for (int i = 0; i < count; i++)
                    {
                        if (vdata[i].tags != null && vdata[i].tags.Contains(selectedTag))
                        {
                            Vector3 worldPos = paperMesh.transform.TransformPoint(verts[i]);
                            Gizmos.DrawSphere(worldPos, tagHighlightSize);
                        }
                    }
                }
            }
        }

        #endregion

        #region Properties

        // Public getters/setters for inspector updates
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
            set => inputMode = value;
        }

        public float DragFoldAngle
        {
            get => dragFoldAngle;
            set => dragFoldAngle = value;
        }

        #endregion
    }
}
