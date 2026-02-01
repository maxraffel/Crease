using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace PhysicsHelpers
{
    /// <summary>
    /// A physics helper that generates a frustum (truncated cone) shaped trigger collider.
    /// Uses a procedurally generated mesh and a convex MeshCollider.
    /// </summary>
    [RequireComponent(typeof(MeshCollider))]
    [RequireComponent(typeof(Rigidbody))] // Rigidbody is usually needed for triggers to catch static colliders, or vice-versa, depending on setup.
    public class FrustumTrigger : MonoBehaviour
    {
        [Header("Frustum Settings")]
        [Tooltip("Radius of the top circle of the frustum.")]
        [Min(0)] public float topRadius = 1.0f;

        [Tooltip("Radius of the bottom circle of the frustum.")]
        [Min(0)] public float bottomRadius = 2.0f;

        [Tooltip("Total height of the frustum.")]
        [Min(0)] public float height = 3.0f;

        [Tooltip("Number of segments for the circle approximation.")]
        [Range(3, 64)] public int segments = 18;

        [Header("Physics Settings")]
        [Tooltip("If true, the Rigidbody will be set to IsKinematic automatically.")]
        public bool autoConfigureRigidbody = true;

        [Header("Events")]
        public UnityEvent<Collider> onTriggerEnter;
        public UnityEvent<Collider> onTriggerExit;

        private MeshCollider _meshCollider;
        private Rigidbody _rigidbody;
        private Mesh _generatedMesh;

        private void Awake()
        {
            Initialize();
        }

        private void OnValidate()
        {
            // Allows real-time visualization in the Editor
            // Only rebuild if the component is already initialized/has references
            if (_meshCollider != null)
            {
                RebuildMesh();
            }
        }

        private void Initialize()
        {
            _meshCollider = GetComponent<MeshCollider>();
            _rigidbody = GetComponent<Rigidbody>();

            if (autoConfigureRigidbody && _rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
            }

            RebuildMesh();
        }

        /// <summary>
        /// Regenerates the mesh and assigns it to the collider.
        /// </summary>
        public void RebuildMesh()
        {
            if (_meshCollider == null) return;

            if (_generatedMesh == null)
            {
                _generatedMesh = new Mesh();
                _generatedMesh.name = "FrustumTriggerMesh";
            }

            GenerateFrustumMesh(_generatedMesh, topRadius, bottomRadius, height, segments);

            _meshCollider.sharedMesh = null; // Clear first to force update if needed
            _meshCollider.sharedMesh = _generatedMesh;
            _meshCollider.convex = true;
            _meshCollider.isTrigger = true;
        }

        private void GenerateFrustumMesh(Mesh mesh, float rTop, float rBottom, float h, int seg)
        {
            mesh.Clear();
        
            int vertexCount = (seg + 1) * 2 + 2; // +1 for UV wrapping, +2 for caps centers
            Vector3[] vertices = new Vector3[vertexCount];
            // We don't strictly need UVs/Normals for a trigger collider, but good practice.
            
            float halfH = h * 0.5f;
            int vIndex = 0;
        
            // 0: Top Center
            vertices[vIndex++] = new Vector3(0, halfH, 0);
            // 1: Bottom Center
            vertices[vIndex++] = new Vector3(0, -halfH, 0);
        
            // Ring vertices
            // We generate (seg + 1) vertices for the rings to handle closed loop if we were doing texturing,
            // but for a collider, distinct vertices are fine.
            // Vertices 2 to 2+seg: Top Ring
            // Vertices 2+seg+1 to ...: Bottom Ring
        
            int topRingStart = vIndex;
            for (int i = 0; i <= seg; i++)
            {
                float angle = (float)i / seg * Mathf.PI * 2;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
        
                vertices[vIndex++] = new Vector3(cos * rTop, halfH, sin * rTop);
            }
        
            int bottomRingStart = vIndex;
            for (int i = 0; i <= seg; i++)
            {
                float angle = (float)i / seg * Mathf.PI * 2;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
        
                vertices[vIndex++] = new Vector3(cos * rBottom, -halfH, sin * rBottom);
            }
        
            mesh.vertices = vertices;
        
            // Triangles
            List<int> tris = new List<int>();
        
            // Top Cap (Triangle Fan) - Center is index 0
            // Ring indices: topRingStart to topRingStart + seg
            for (int i = 0; i < seg; i++)
            {
                tris.Add(0);
                tris.Add(topRingStart + i);
                tris.Add(topRingStart + i + 1);
            }
        
            // Bottom Cap - Center is index 1
            // Winding order reversed for bottom
            for (int i = 0; i < seg; i++)
            {
                tris.Add(1);
                tris.Add(bottomRingStart + i + 1);
                tris.Add(bottomRingStart + i);
            }
        
            // Sides (Quads -> 2 Tris)
            for (int i = 0; i < seg; i++)
            {
                int currentTop = topRingStart + i;
                int nextTop = topRingStart + i + 1;
                int currentBottom = bottomRingStart + i;
                int nextBottom = bottomRingStart + i + 1;
        
                // Triangle 1
                tris.Add(currentTop);
                tris.Add(nextBottom);
                tris.Add(nextTop);
        
                // Triangle 2
                tris.Add(currentTop);
                tris.Add(currentBottom);
                tris.Add(nextBottom);
            }
        
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals(); // Fix: Gizmos.DrawMesh requires normals
            mesh.RecalculateBounds();
        }

        // Native Unity Trigger Callbacks
        private void OnTriggerEnter(Collider other)
        {
            onTriggerEnter?.Invoke(other);
        }

        private void OnTriggerExit(Collider other)
        {
            onTriggerExit?.Invoke(other);
        }

        // Editor Visualization
        private void OnDrawGizmos()
        {
            // Use Unity standard collider green color
            Gizmos.color = new Color(0.56f, 0.96f, 0.54f, 1.0f);
            Gizmos.matrix = transform.localToWorldMatrix;
            
            float halfH = height * 0.5f;

            // Draw top and bottom rings
            DrawCircle(new Vector3(0, halfH, 0), topRadius);
            DrawCircle(new Vector3(0, -halfH, 0), bottomRadius);

            // Draw 8 side lines to make the shape clearer
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 0.25f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                Gizmos.DrawLine(
                    new Vector3(cos * topRadius, halfH, sin * topRadius),
                    new Vector3(cos * bottomRadius, -halfH, sin * bottomRadius)
                );
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Add a semi-transparent fill effect when selected
            if (_generatedMesh != null)
            {
                Gizmos.color = new Color(0.56f, 0.96f, 0.54f, 0.2f);
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawMesh(_generatedMesh);
            }
            else if (!Application.isPlaying)
            {
                // Try to generate mesh once in editor mode if not already generated
                Initialize();
            }
        }

        private void DrawCircle(Vector3 center, float radius)
        {
            Vector3 prev = center + new Vector3(radius, 0, 0);
            int div = 24;
            for(int i = 1; i <= div; i++)
            {
                float angle = (float)i / div * Mathf.PI * 2;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
