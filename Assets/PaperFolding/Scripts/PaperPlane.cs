using System.Collections.Generic;
using UnityEngine;

namespace NewPaperFoldingEngine
{
    [System.Serializable]
    public class HE_Vertex 
    {
        public Vector3 position;
        public Vector2 uv;
        public HE_Edge edge; 
    }

    [System.Serializable]
    public class HE_Face 
    {
        public HE_Edge edge; 
    }

    [System.Serializable]
    public class HE_Edge 
    {
        public HE_Vertex vert;   
        public HE_Edge twin;     
        public HE_Edge next;     
        public HE_Edge prev;     
        public HE_Face face;     
    }

    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PaperPlane : MonoBehaviour
    {
        public List<HE_Vertex> vertices = new List<HE_Vertex>();
        public List<HE_Face> faces = new List<HE_Face>();
        public List<HE_Edge> edges = new List<HE_Edge>();

        [SerializeField] [Range(-1f, 1f)] private float cutLeftAnchorX;
        [SerializeField] [Range(-1f, 1f)] private float cutLeftAnchorY;
        [SerializeField] [Range(-1f, 1f)] private float cutRightAnchorX;
        [SerializeField] [Range(-1f, 1f)] private float cutRightAnchorY;
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private float snapThreshold = 0.0000000001f;
        [SerializeField] private float creaseWidth = 0.02f; 

        private void Awake()
        {
            GeneratePaper();
            UpdateUnityMesh();
        }

        public void GeneratePaper()
        {
            var v1 = new HE_Vertex { position = new Vector3(-1, 0, 1), uv = new Vector2(0, 1) }; 
            var v2 = new HE_Vertex { position = new Vector3(1, 0, 1), uv = new Vector2(1, 1) };  
            var v3 = new HE_Vertex { position = new Vector3(1, 0, -1), uv = new Vector2(1, 0) }; 
            var v4 = new HE_Vertex { position = new Vector3(-1, 0, -1), uv = new Vector2(0, 0) };

            vertices.AddRange(new[] { v1, v2, v3, v4 });

            HE_Edge e1 = new HE_Edge { vert = v1 };
            HE_Edge e2 = new HE_Edge { vert = v2 };
            HE_Edge e3 = new HE_Edge { vert = v3 };
            HE_Edge e4 = new HE_Edge { vert = v4 };

            e1.next = e2; e2.prev = e1;
            e2.next = e3; e3.prev = e2;
            e3.next = e4; e4.prev = e3;
            e4.next = e1; e1.prev = e4;

            HE_Face face = new HE_Face { edge = e1 };
            faces.Add(face);

            e1.face = face;
            e2.face = face;
            e3.face = face;
            e4.face = face;

            v1.edge = e1;
            v2.edge = e2;
            v3.edge = e3;
            v4.edge = e4;

            edges.AddRange(new[] { e1, e2, e3, e4 });
        }

        /// <summary>
        /// Reconstructs the Unity Mesh from the current Half-Edge Data Structure.
        /// Iterates through all faces, triangulates them, and updates the MeshFilter.
        /// </summary>
        public void UpdateUnityMesh()
        {
            List<Vector3> meshVerts = new List<Vector3>();
            List<int> meshTris = new List<int>();
            List<Vector2> meshUVs = new List<Vector2>();

            foreach (var face in faces)
            {
                List<HE_Vertex> faceVerts = new List<HE_Vertex>();
                HE_Edge startEdge = face.edge;
                HE_Edge current = startEdge;
                
                int safety = 0;
                do
                {
                    faceVerts.Add(current.vert);
                    current = current.next;
                    safety++;
                } while (current != startEdge && safety < 100);

                PaperUtility.TriangulateFace(faceVerts, meshVerts, meshTris, meshUVs);
            }

            Mesh m = new Mesh();
            m.vertices = meshVerts.ToArray();
            m.triangles = meshTris.ToArray();
            m.uv = meshUVs.ToArray();
            m.RecalculateNormals();
            GetComponent<MeshFilter>().mesh = m;
        }

        /// <summary>
        /// Performs a complete cut and fold operation on the paper.
        /// 1. Identifies edge intersections with the cut line.
        /// 2. Splits edges at intersection points.
        /// 3. Splits affected faces to create the fold crease.
        /// 4. Rotates the severed flap.
        /// </summary>
        [ContextMenu("Execute Multi-Face Fold")]
        public void ExecuteFold()
        {
            if (!CalculateCutLine(out Vector3 infiniteStart, out Vector3 infiniteEnd)) return;

            PaperUtility.FindEdgeIntersections(edges, infiniteStart, infiniteEnd, transform, out var intersections, out var facesToCheck);

            if (intersections.Count < 2) return;

            var cutVerticesSet = PaperUtility.SplitEdgesByIntersections(vertices, edges, intersections, snapThreshold);

            PaperUtility.SplitFacesAlongCrease(faces, edges, facesToCheck, cutVerticesSet);

            RotateFlap(infiniteStart, infiniteEnd, 179.9f);
            UpdateUnityMesh();
        }

        private bool CalculateCutLine(out Vector3 start, out Vector3 end)
        {
            start = Vector3.zero;
            end = Vector3.zero;

            Vector3 anchorA = new Vector3(cutLeftAnchorX, 0, cutLeftAnchorY);
            Vector3 anchorB = new Vector3(cutRightAnchorX, 0, cutRightAnchorY);
            if (Vector3.Distance(anchorA, anchorB) < 0.001f) return false;
            
            Vector3 cutDir = (anchorB - anchorA).normalized;
            start = anchorA - cutDir * 1000f;
            end = anchorA + cutDir * 1000f;
            return true;
        }

        /// <summary>
        /// Rotates vertices on the active side of the fold line.
        /// Calculates the rotation for each vertex based on its local face "hinge" to handle complex, non-planar geometries.
        /// </summary>
        /// <param name="axisStart">Start of the global fold axis.</param>
        /// <param name="axisEnd">End of the global fold axis.</param>
        /// <param name="angle">Angle to rotate in degrees.</param>
        void RotateFlap(Vector3 axisStart, Vector3 axisEnd, float angle)
        {
            Vector3 cutDir = (axisEnd - axisStart).normalized;
            Vector3 cutPlaneNormal = Vector3.Cross(cutDir, Vector3.up).normalized;
            Plane cutPlane = new Plane(cutPlaneNormal, axisStart);

            foreach (var v in vertices)
            {
                Vector3 worldPos = transform.TransformPoint(v.position);

                if (!cutPlane.GetSide(worldPos)) continue;
                if (Mathf.Abs(cutPlane.GetDistanceToPoint(worldPos)) < 0.001f) continue;

                Vector3 rotationAxis = cutDir;
                Vector3 rotationPivot = axisStart;

                if (PaperUtility.GetFoldRotationData(v, cutPlane, transform, out Vector3 axis, out Vector3 pivot))
                {
                    rotationAxis = axis;
                    rotationPivot = pivot;
                }

                Quaternion rot = Quaternion.AngleAxis(angle, rotationAxis);
                Vector3 relative = worldPos - rotationPivot;
                Vector3 rotated = rot * relative;
                Vector3 finalWorld = rotationPivot + rotated;

                v.position = transform.InverseTransformPoint(finalWorld);
            }
        }


        void OnDrawGizmos()
        {
            if (vertices != null)
            {
                Gizmos.color = Color.red;
                foreach (var v in vertices)
                {
                    Vector3 worldPos = transform.TransformPoint(v.position);
                    Gizmos.DrawSphere(worldPos, 0.05f);
                }
            }

            if (edges != null)
            {
                Gizmos.color = Color.green;
                foreach (var e in edges)
                {
                    if (e.vert == null || e.next == null || e.next.vert == null) continue;

                    Vector3 start = transform.TransformPoint(e.vert.position);
                    Vector3 end = transform.TransformPoint(e.next.vert.position);

                    Gizmos.DrawLine(start, end);
                }
            }

            Gizmos.color = Color.yellow;

            Vector3 foldStartPos = new Vector3(cutLeftAnchorX, 0, cutLeftAnchorY);
            Vector3 foldEndPos = new Vector3(cutRightAnchorX, 0, cutRightAnchorY);
            Vector3 MidPoint = (foldStartPos + foldEndPos) / 2f;
            Vector3 cutDir = (foldEndPos - foldStartPos).normalized;

            Gizmos.DrawLine(MidPoint + cutDir * 5f, MidPoint - cutDir * 5f);
            
        }
    }
    
}
