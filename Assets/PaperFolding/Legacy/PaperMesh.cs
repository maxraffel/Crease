using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PaperFolding
{
    /// <summary>
    /// Represents a fold axis on the paper using parametric coordinates (u,v)
    /// Can be created from UV coordinates or directly from spatial positions
    /// </summary>
    [System.Serializable]
    public struct FoldAxis
    {
        public float u1, v1; // Start point (0-1 range)
        public float u2, v2; // End point (0-1 range)

        public FoldAxis(float u1, float v1, float u2, float v2)
        {
            this.u1 = Mathf.Clamp01(u1);
            this.v1 = Mathf.Clamp01(v1);
            this.u2 = Mathf.Clamp01(u2);
            this.v2 = Mathf.Clamp01(v2);
        }

        /// <summary>
        /// Create a FoldAxis from two world space points
        /// </summary>
        public static FoldAxis FromWorldSpace(Vector3 start, Vector3 end, PaperMesh paper)
        {
            Vector2 startUV = paper.WorldToUV(start);
            Vector2 endUV = paper.WorldToUV(end);
            return new FoldAxis(startUV.x, startUV.y, endUV.x, endUV.y);
        }

        /// <summary>
        /// Create a FoldAxis from two local space points
        /// </summary>
        public static FoldAxis FromLocalSpace(Vector3 start, Vector3 end, PaperMesh paper)
        {
            Vector2 startUV = paper.LocalToUV(start);
            Vector2 endUV = paper.LocalToUV(end);
            return new FoldAxis(startUV.x, startUV.y, endUV.x, endUV.y);
        }

        /// <summary>
        /// Convert parametric coordinates to world space based on current mesh state
        /// </summary>
        public (Vector3 start, Vector3 end) ToWorldSpace(PaperMesh paper)
        {
            Vector3 start = paper.UVToWorld(u1, v1);
            Vector3 end = paper.UVToWorld(u2, v2);
            return (start, end);
        }

        /// <summary>
        /// Convert parametric coordinates to local space based on current mesh state
        /// </summary>
        public (Vector3 start, Vector3 end) ToLocalSpace(PaperMesh paper)
        {
            Vector3 start = paper.UVToLocal(u1, v1);
            Vector3 end = paper.UVToLocal(u2, v2);
            return (start, end);
        }
    }

    /// <summary>
    /// Tracks vertex data including tags for fold history
    /// </summary>
    public class VertexData
    {
        public Vector3 originalPosition; // Position in flat space (u,v,0)
        public HashSet<string> tags;
        public float u, v; // Parametric coordinates

        public VertexData(float u, float v)
        {
            this.u = u;
            this.v = v;
            this.originalPosition = new Vector3(u, v, 0);
            this.tags = new HashSet<string>();
        }
    }

    /// <summary>
    /// Main paper mesh class that handles folding operations
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class PaperMesh : MonoBehaviour
    {
        [Header("Paper Dimensions")]
        [SerializeField] private float width = 1f;
        [SerializeField] private float height = 1f;
        
        [Header("Mesh Resolution")]
        [SerializeField] private int resolutionX = 20;
        [SerializeField] private int resolutionY = 20;

        [Header("180° Fold Settings")]
        [SerializeField] private float flatFoldOffset = 0.002f;

        private Mesh mesh;
        private List<VertexData> vertexDataList;
        private Vector3[] vertices;
        private int foldCounter = 0;
        private MeshCollider meshCollider;

        private void Awake()
        {
            meshCollider = GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = gameObject.AddComponent<MeshCollider>();
            }

            GeneratePaperMesh();
        }

        /// <summary>
        /// Generate a subdivided plane mesh for the paper
        /// </summary>
        public void GeneratePaperMesh()
        {
            mesh = new Mesh();
            mesh.name = "Paper Mesh";
            GetComponent<MeshFilter>().mesh = mesh;

            int vertexCount = (resolutionX + 1) * (resolutionY + 1);
            vertices = new Vector3[vertexCount];
            vertexDataList = new List<VertexData>(vertexCount);
            Vector2[] uvs = new Vector2[vertexCount];
            
            // Generate vertices
            for (int y = 0; y <= resolutionY; y++)
            {
                for (int x = 0; x <= resolutionX; x++)
                {
                    float u = (float)x / resolutionX;
                    float v = (float)y / resolutionY;
                    
                    vertices[y * (resolutionX + 1) + x] = new Vector3(
                        (u - 0.5f) * width,
                        (v - 0.5f) * height,
                        0
                    );
                    
                    uvs[y * (resolutionX + 1) + x] = new Vector2(u, v);
                    vertexDataList.Add(new VertexData(u, v));
                }
            }

            // Generate triangles
            int[] triangles = new int[resolutionX * resolutionY * 6];
            int triIndex = 0;
            
            for (int y = 0; y < resolutionY; y++)
            {
                for (int x = 0; x < resolutionX; x++)
                {
                    int i = y * (resolutionX + 1) + x;
                    
                    triangles[triIndex++] = i;
                    triangles[triIndex++] = i + resolutionX + 1;
                    triangles[triIndex++] = i + 1;
                    
                    triangles[triIndex++] = i + 1;
                    triangles[triIndex++] = i + resolutionX + 1;
                    triangles[triIndex++] = i + resolutionX + 2;
                }
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = mesh;
        }

        #region Coordinate Mapping Functions

        /// <summary>
        /// Convert world position to parametric UV coordinates
        /// </summary>
        public Vector2 WorldToUV(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            return LocalToUV(local);
        }

        /// <summary>
        /// Convert local position to parametric UV coordinates
        /// </summary>
        public Vector2 LocalToUV(Vector3 localPoint)
        {
            float u = (localPoint.x / width) + 0.5f;
            float v = (localPoint.y / height) + 0.5f;
            return new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
        }

        /// <summary>
        /// Convert parametric UV to local position (based on current vertex positions)
        /// </summary>
        public Vector3 UVToLocal(float u, float v)
        {
            // Find the closest vertex and use interpolation for better accuracy
            int closestIndex = FindClosestVertexIndex(u, v);
            
            if (closestIndex >= 0)
            {
                return vertices[closestIndex];
            }
            
            // Fallback to parametric calculation
            return new Vector3((u - 0.5f) * width, (v - 0.5f) * height, 0);
        }

        /// <summary>
        /// Convert parametric UV to world position
        /// </summary>
        public Vector3 UVToWorld(float u, float v)
        {
            return transform.TransformPoint(UVToLocal(u, v));
        }

        /// <summary>
        /// Raycast against the paper mesh collider
        /// </summary>
        public bool RaycastPaper(Ray ray, out RaycastHit hit)
        {
            if (meshCollider != null && meshCollider.Raycast(ray, out hit, Mathf.Infinity))
            {
                return true;
            }
            
            hit = new RaycastHit();
            return false;
        }

        /// <summary>
        /// Get the local normal at a specific UV coordinate
        /// </summary>
        public Vector3 GetLocalNormalAtUV(float u, float v)
        {
            int closestIndex = FindClosestVertexIndex(u, v);
            
            if (closestIndex >= 0 && closestIndex < mesh.normals.Length)
            {
                return mesh.normals[closestIndex];
            }

            return Vector3.forward;
        }

        /// <summary>
        /// Get the world normal at a specific UV coordinate
        /// </summary>
        public Vector3 GetWorldNormalAtUV(float u, float v)
        {
            return transform.TransformDirection(GetLocalNormalAtUV(u, v));
        }

        /// <summary>
        /// Find the closest vertex index to given UV coordinates
        /// </summary>
        private int FindClosestVertexIndex(float u, float v)
        {
            int closestIndex = -1;
            float minDist = float.MaxValue;
            
            for (int i = 0; i < vertexDataList.Count; i++)
            {
                float dist = Mathf.Abs(vertexDataList[i].u - u) + Mathf.Abs(vertexDataList[i].v - v);
                if (dist < minDist)
                {
                    minDist = dist;
                    closestIndex = i;
                }
            }
            
            return closestIndex;
        }

        /// <summary>
        /// Snap UV coordinates to the nearest edge of the paper
        /// </summary>
        public Vector2 SnapUVToEdge(Vector2 uv)
        {
            float distToLeft = Mathf.Abs(uv.x - 0f);
            float distToRight = Mathf.Abs(uv.x - 1f);
            float distToBottom = Mathf.Abs(uv.y - 0f);
            float distToTop = Mathf.Abs(uv.y - 1f);

            float minDist = Mathf.Min(distToLeft, distToRight, distToBottom, distToTop);

            Vector2 snapped = uv;

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

        #endregion

        #region Fold Operations

        /// <summary>
        /// Check if a fold angle is effectively 180 degrees (flat fold)
        /// </summary>
        private bool IsFlatFold(float degrees)
        {
            float normalizedAngle = Mathf.Abs(degrees % 360f);
            return Mathf.Abs(normalizedAngle - 180f) < 0.1f;
        }

        /// <summary>
        /// Core fold logic - computes which vertices to move and applies the rotation
        /// Returns the indices of moved and eligible vertices for tagging
        /// </summary>
        private (List<int> movedIndices, List<int> eligibleIndices) ComputeFoldTransform(
            Vector3 localStart, Vector3 localEnd, float degrees, 
            System.Func<HashSet<string>, bool> predicate)
        {
            Vector3 foldDirection = (localEnd - localStart).normalized;
            Vector3 foldNormal = Vector3.Cross(foldDirection, Vector3.forward).normalized;

            List<int> movedIndices = new List<int>();
            List<int> eligibleIndices = new List<int>();

            // Determine which vertices are eligible and which to rotate
            for (int i = 0; i < vertices.Length; i++)
            {
                bool eligible = predicate == null || predicate(vertexDataList[i].tags);
                Vector3 toVertex = vertices[i] - localStart;
                float side = Vector3.Dot(toVertex, foldNormal);

                if (eligible)
                {
                    eligibleIndices.Add(i);
                    
                    if (side > 0.0001f)
                    {
                        movedIndices.Add(i);
                    }
                }
            }

            return (movedIndices, eligibleIndices);
        }

        /// <summary>
        /// Apply rotation to moved vertices
        /// </summary>
        private void ApplyRotation(Vector3 localStart, Vector3 foldDirection, float degrees, List<int> movedIndices)
        {
            if (IsFlatFold(degrees))
            {
                Vector3 foldNormal = Vector3.Cross(foldDirection, Vector3.forward).normalized;
                ApplyFlatFold(localStart, foldDirection, foldNormal, movedIndices, degrees);
            }
            else
            {
                Quaternion rotation = Quaternion.AngleAxis(degrees, foldDirection);
                
                foreach (int i in movedIndices)
                {
                    Vector3 toVertex = vertices[i] - localStart;
                    vertices[i] = localStart + rotation * toVertex;
                }
            }
        }

        /// <summary>
        /// Apply tags to vertices after a fold
        /// </summary>
        private void ApplyFoldTags(List<int> movedIndices, List<int> eligibleIndices, string movedTag, string staticTag)
        {
            foreach (int i in movedIndices)
            {
                vertexDataList[i].tags.Add(movedTag);
            }

            foreach (int i in eligibleIndices)
            {
                if (!movedIndices.Contains(i))
                {
                    vertexDataList[i].tags.Add(staticTag);
                }
            }
        }

        /// <summary>
        /// Perform a fold operation using a FoldAxis (automatically converts UV to local space)
        /// </summary>
        public void Fold(FoldAxis axis, float degrees, string optionalTag = null, System.Func<HashSet<string>, bool> predicate = null)
        {
            var (localStart, localEnd) = axis.ToLocalSpace(this);
            FoldInternal(localStart, localEnd, degrees, optionalTag, predicate);
        }

        /// <summary>
        /// Perform a fold operation using local space coordinates directly
        /// </summary>
        public void FoldLocal(Vector3 localStart, Vector3 localEnd, float degrees, string optionalTag = null, System.Func<HashSet<string>, bool> predicate = null)
        {
            FoldInternal(localStart, localEnd, degrees, optionalTag, predicate);
        }

        /// <summary>
        /// Perform a fold operation using world space coordinates
        /// </summary>
        public void FoldWorld(Vector3 worldStart, Vector3 worldEnd, float degrees, string optionalTag = null, System.Func<HashSet<string>, bool> predicate = null)
        {
            Vector3 localStart = transform.InverseTransformPoint(worldStart);
            Vector3 localEnd = transform.InverseTransformPoint(worldEnd);
            FoldInternal(localStart, localEnd, degrees, optionalTag, predicate);
        }

        /// <summary>
        /// Internal fold implementation - all fold methods route through here
        /// </summary>
        private void FoldInternal(Vector3 localStart, Vector3 localEnd, float degrees, string optionalTag, System.Func<HashSet<string>, bool> predicate)
        {
            Vector3 foldDirection = (localEnd - localStart).normalized;

            string movedTag = optionalTag != null ? optionalTag + "_moved" : $"fold_{foldCounter}_moved";
            string staticTag = optionalTag != null ? optionalTag + "_static" : $"fold_{foldCounter}_static";
            foldCounter++;

            var (movedIndices, eligibleIndices) = ComputeFoldTransform(localStart, localEnd, degrees, predicate);
            ApplyRotation(localStart, foldDirection, degrees, movedIndices);
            ApplyFoldTags(movedIndices, eligibleIndices, movedTag, staticTag);

            UpdateMesh();
        }

        /// <summary>
        /// Apply a flat fold (180° or -180°) with offset to prevent self-intersection
        /// </summary>
        private void ApplyFlatFold(Vector3 localStart, Vector3 foldDirection, Vector3 foldNormal, 
                                   List<int> movedIndices, float degrees)
        {
            float offsetSign = Mathf.Sign(degrees);
            Vector3 offsetDirection = Vector3.Cross(foldDirection, foldNormal).normalized * offsetSign;
            Quaternion rotation = Quaternion.AngleAxis(degrees, foldDirection);
            
            foreach (int i in movedIndices)
            {
                Vector3 toVertex = vertices[i] - localStart;
                Vector3 rotated = localStart + rotation * toVertex;
                vertices[i] = rotated + offsetDirection * flatFoldOffset;
            }
        }

        /// <summary>
        /// Animate a fold over time using a FoldAxis
        /// </summary>
        public IEnumerator AnimateFold(FoldAxis axis, float degrees, string optionalTag = null, 
                                       System.Func<HashSet<string>, bool> predicate = null, float duration = 1f)
        {
            var (localStart, localEnd) = axis.ToLocalSpace(this);
            return AnimateFoldInternal(localStart, localEnd, degrees, optionalTag, predicate, duration);
        }

        /// <summary>
        /// Animate a fold over time using local space coordinates
        /// </summary>
        public IEnumerator AnimateFoldLocal(Vector3 localStart, Vector3 localEnd, float degrees, string optionalTag = null, 
                                            System.Func<HashSet<string>, bool> predicate = null, float duration = 1f)
        {
            return AnimateFoldInternal(localStart, localEnd, degrees, optionalTag, predicate, duration);
        }

        /// <summary>
        /// Internal animated fold implementation
        /// </summary>
        private IEnumerator AnimateFoldInternal(Vector3 localStart, Vector3 localEnd, float degrees, 
                                                string optionalTag, System.Func<HashSet<string>, bool> predicate, float duration)
        {
            Vector3 foldDirection = (localEnd - localStart).normalized;
            Vector3 foldNormal = Vector3.Cross(foldDirection, Vector3.forward).normalized;

            string movedTag = optionalTag != null ? optionalTag + "_moved" : $"fold_{foldCounter}_moved";
            string staticTag = optionalTag != null ? optionalTag + "_static" : $"fold_{foldCounter}_static";
            foldCounter++;

            var (movedIndices, eligibleIndices) = ComputeFoldTransform(localStart, localEnd, degrees, predicate);

            // Store original relative positions
            var originalRel = new Dictionary<int, Vector3>(movedIndices.Count);
            foreach (int i in movedIndices)
            {
                originalRel[i] = vertices[i] - localStart;
            }

            bool isFlatFold = IsFlatFold(degrees);
            float offsetSign = Mathf.Sign(degrees);
            Vector3 offsetDirection = Vector3.Cross(foldDirection, foldNormal).normalized * offsetSign;

            // Animate the fold
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float currentAngle = degrees * t;
                Quaternion rotation = Quaternion.AngleAxis(currentAngle, foldDirection);

                foreach (var kv in originalRel)
                {
                    int i = kv.Key;
                    Vector3 rel = kv.Value;
                    Vector3 rotated = localStart + rotation * rel;

                    if (isFlatFold)
                    {
                        float offsetAmount = t * flatFoldOffset;
                        vertices[i] = rotated + offsetDirection * offsetAmount;
                    }
                    else
                    {
                        vertices[i] = rotated;
                    }
                }

                UpdateMesh();
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Apply final state
            ApplyRotation(localStart, foldDirection, degrees, movedIndices);
            ApplyFoldTags(movedIndices, eligibleIndices, movedTag, staticTag);
            UpdateMesh();
        }

        /// <summary>
        /// Update the mesh and collider
        /// </summary>
        private void UpdateMesh()
        {
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh = mesh;
            }
        }

        #endregion

        #region Tag Management

        /// <summary>
        /// Get all vertices with a specific tag
        /// </summary>
        public List<int> GetVerticesWithTag(string tag)
        {
            List<int> result = new List<int>();
            for (int i = 0; i < vertexDataList.Count; i++)
            {
                if (vertexDataList[i].tags.Contains(tag))
                {
                    result.Add(i);
                }
            }
            return result;
        }

        /// <summary>
        /// Get all unique tags in the mesh
        /// </summary>
        public HashSet<string> GetAllTags()
        {
            var result = new HashSet<string>();
            foreach (var vd in vertexDataList)
            {
                foreach (var tag in vd.tags)
                {
                    result.Add(tag);
                }
            }
            return result;
        }

        /// <summary>
        /// Get all tags for a specific vertex
        /// </summary>
        public HashSet<string> GetVertexTags(int vertexIndex)
        {
            if (vertexIndex >= 0 && vertexIndex < vertexDataList.Count)
            {
                return new HashSet<string>(vertexDataList[vertexIndex].tags);
            }
            return new HashSet<string>();
        }

        #endregion

        /// <summary>
        /// Clear all fold history and reset the paper
        /// </summary>
        public void Reset()
        {
            foldCounter = 0;
            foreach (var vd in vertexDataList)
            {
                vd.tags.Clear();
            }
            GeneratePaperMesh();
        }

        // Public getters and setters
        public Mesh GetMesh() => mesh;
        public List<VertexData> GetVertexData() => vertexDataList;
        public Vector3[] GetVertices() => vertices;
        public float Width => width;
        public float Height => height;
        
        public float FlatFoldOffset
        {
            get => flatFoldOffset;
            set => flatFoldOffset = value;
        }
    }
}