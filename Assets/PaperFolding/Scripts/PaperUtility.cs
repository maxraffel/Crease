using UnityEngine;
using System.Collections.Generic;

namespace NewPaperFoldingEngine
{
    public static class PaperUtility
    {
        /// <summary>
        /// Determines whether a direct edge connection exists between two specific vertices.
        /// Iterates through the outgoing edges of the start vertex to check for connectivity.
        /// </summary>
        /// <param name="vA">The starting vertex.</param>
        /// <param name="vB">The target vertex.</param>
        /// <returns>True if an edge exists from vA to vB; otherwise, false.</returns>
        public static bool AreVerticesConnected(HE_Vertex vA, HE_Vertex vB)
        {
            HE_Edge runner = vA.edge;
            if (runner == null) return false;
            
            int safety = 0;
            HE_Edge current = runner;
            do
            {
                if (current.next.vert == vB) return true;
                
                if (current.twin != null) current = current.twin.next;
                else break;

                safety++;
            } while (current != runner && safety < 50);

            return false;
        }

        /// <summary>
        /// Calculates the line of intersection between two planes.
        /// Returns the intersection point closest to the origin and the direction of the line.
        /// </summary>
        /// <param name="p1">The first plane.</param>
        /// <param name="p2">The second plane.</param>
        /// <param name="point">Output point on the intersection line.</param>
        /// <param name="direction">Output direction vector of the intersection line.</param>
        /// <returns>True if the planes intersect; false if they are parallel.</returns>
        public static bool GetPlaneIntersection(Plane p1, Plane p2, out Vector3 point, out Vector3 direction)
        {
            point = Vector3.zero;
            direction = Vector3.zero;

            Vector3 n1 = p1.normal;
            Vector3 n2 = p2.normal;
            direction = Vector3.Cross(n1, n2);

            if (direction.sqrMagnitude < 0.0001f) return false;

            direction.Normalize();

            float n1n2 = Vector3.Dot(n1, n2);
            float n1sqr = Vector3.Dot(n1, n1); 
            float n2sqr = Vector3.Dot(n2, n2); 
            float det = n1sqr * n2sqr - n1n2 * n1n2;

            if (Mathf.Abs(det) < 0.0001f) return false;

            float c1 = -p1.distance;
            float c2 = -p2.distance;

            float a = (c1 * n2sqr - c2 * n1n2) / det;
            float b = (c2 * n1sqr - c1 * n1n2) / det;

            point = a * n1 + b * n2;
            return true;
        }

        /// <summary>
        /// Finds the intersection point between two line segments (p1-p2 and p3-p4) in 3D space, ignoring height difference errors.
        /// Primarily used for finding where a cut line intersects a mesh edge on the XZ plane approximation.
        /// </summary>
        /// <param name="p1">Start of first segment.</param>
        /// <param name="p2">End of first segment.</param>
        /// <param name="p3">Start of second segment.</param>
        /// <param name="p4">End of second segment.</param>
        /// <param name="intersection">Output intersection point.</param>
        /// <returns>True if the segments intersect within their bounds; otherwise, false.</returns>
        public static bool GetLineIntersection(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, out Vector3 intersection)
        {
            intersection = Vector3.zero;
            
            float den = (p4.z - p3.z) * (p2.x - p1.x) - (p4.x - p3.x) * (p2.z - p1.z);
            if (den == 0) return false;

            float ua = ((p4.x - p3.x) * (p1.z - p3.z) - (p4.z - p3.z) * (p1.x - p3.x)) / den;
            float ub = ((p2.x - p1.x) * (p1.z - p3.z) - (p2.z - p1.z) * (p1.x - p3.x)) / den;

            if (ua >= 0 && ua <= 1 && ub >= 0 && ub <= 1)
            {
                intersection = p1 + ua * (p2 - p1);
                return true;
            }
            return false;
        } 

        /// <summary>
        /// Decomposes a convex polygon (face) into a list of triangles for rendering.
        /// Uses a simple fan triangulation method centered at the first vertex.
        /// </summary>
        /// <param name="poly">List of vertices composing the face.</param>
        /// <param name="verts">Target list for mesh vertices.</param>
        /// <param name="tris">Target list for mesh triangle indices.</param>
        /// <param name="uvs">Target list for mesh UVs.</param>
        public static void TriangulateFace(List<HE_Vertex> poly, List<Vector3> verts, List<int> tris, List<Vector2> uvs)
        {
            int baseIndex = verts.Count;
            
            foreach(var v in poly) {
                verts.Add(v.position);
                uvs.Add(v.uv);
            }

            for(int i = 1; i < poly.Count - 1; i++) {
                tris.Add(baseIndex);
                tris.Add(baseIndex + i);
                tris.Add(baseIndex + i + 1);
            }
        }

        /// <summary>
        /// Splits an existing edge at a specific position by inserting a new vertex.
        /// Handles the splitting of the primary edge and its twin (if it exists), maintaining topological validity.
        /// </summary>
        /// <param name="vertices">The master list of vertices to register the new vertex.</param>
        /// <param name="edges">The master list of edges to register new edges.</param>
        /// <param name="edge">The edge to split.</param>
        /// <param name="splitPos">The world position for the new vertex.</param>
        /// <param name="splitUV">The UV coordinate for the new vertex.</param>
        /// <returns>The newly created vertex inserted at the split position.</returns>
        public static HE_Vertex SplitEdge(List<HE_Vertex> vertices, List<HE_Edge> edges, HE_Edge edge, Vector3 splitPos, Vector2 splitUV)
        {
            HE_Vertex newVert = new HE_Vertex { position = splitPos, uv = splitUV };
            HE_Edge newEdge = new HE_Edge();

            vertices.Add(newVert);
            edges.Add(newEdge);

            // Configure primary split
            newEdge.vert = newVert;
            newEdge.face = edge.face;
            newEdge.next = edge.next;
            newEdge.prev = edge;
            
            if (newEdge.next != null) newEdge.next.prev = newEdge;
            
            edge.next = newEdge;
            newVert.edge = newEdge;

            // Handle twin split if necessary
            if (edge.twin != null)
            {
                HE_Edge originalTwin = edge.twin;
                HE_Edge newTwin = new HE_Edge();
                edges.Add(newTwin);

                newTwin.vert = newVert;
                newTwin.face = originalTwin.face;
                newTwin.next = originalTwin.next;
                newTwin.prev = originalTwin;

                if (newTwin.next != null) newTwin.next.prev = newTwin;

                originalTwin.next = newTwin;
                
                edge.twin = newTwin;
                newTwin.twin = edge;

                newEdge.twin = originalTwin;
                originalTwin.twin = newEdge;
            }

            return newVert;
        }

        /// <summary>
        /// Splits a face into two by creating a new edge (crease) between two non-adjacent boundary vertices.
        /// This modifies the topology to create a new face and updates all edge-to-face references.
        /// </summary>
        /// <param name="faces">The master list of faces.</param>
        /// <param name="edges">The master list of edges.</param>
        /// <param name="originalFace">The face to be split.</param>
        /// <param name="v1">The start vertex of the split.</param>
        /// <param name="v2">The end vertex of the split.</param>
        public static void SplitFace(List<HE_Face> faces, List<HE_Edge> edges, HE_Face originalFace, HE_Vertex v1, HE_Vertex v2)
        {
            HE_Edge v1_out = GetOutgoingEdgeOnFace(v1, originalFace);
            HE_Edge v2_out = GetOutgoingEdgeOnFace(v2, originalFace);

            if (v1_out == null || v2_out == null)
            {
                Debug.LogError("SplitFace Failed: Could not find valid edges on the specified face. Topology is likely broken.");
                return;
            }

            HE_Edge v1_in = v1_out.prev;
            HE_Edge v2_in = v2_out.prev;

            HE_Edge creaseA = new HE_Edge(); 
            HE_Edge creaseB = new HE_Edge(); 

            edges.Add(creaseA);
            edges.Add(creaseB);

            HE_Face newFace = new HE_Face();
            faces.Add(newFace);

            // Configure Crease A
            creaseA.vert = v1;
            creaseA.face = originalFace;
            creaseA.twin = creaseB;
            creaseA.next = v2_out; 
            creaseA.prev = v1_in;  
            
            v1_in.next = creaseA;
            v2_out.prev = creaseA;

            // Configure Crease B
            creaseB.vert = v2;
            creaseB.face = newFace;
            creaseB.twin = creaseA;
            creaseB.next = v1_out; 
            creaseB.prev = v2_in;  

            v2_in.next = creaseB;
            v1_out.prev = creaseB;

            originalFace.edge = creaseA; 
            newFace.edge = creaseB;      

            // Assign edge ownership for the new face
            HE_Edge runner = newFace.edge;
            int safety = 0;
            do
            {
                runner.face = newFace;
                runner = runner.next;
                safety++;
            } while (runner != newFace.edge && safety < 100);
        }

        /// <summary>
        /// Locates the specific outgoing edge from a vertex that belongs to a specific face.
        /// Necessary because a vertex can share multiple faces/edges.
        /// </summary>
        /// <param name="v">The vertex to search around.</param>
        /// <param name="face">The underlying face to match.</param>
        /// <returns>The edge starting at v that borders the given face, or null if not found.</returns>
        public static HE_Edge GetOutgoingEdgeOnFace(HE_Vertex v, HE_Face face)
        {
            HE_Edge start = v.edge;
            if (start == null) return null;

            HE_Edge current = start;
            int safety = 0;

            // Try swinging around the vertex
            do
            {
                if (current.face == face) return current;

                if (current.twin != null)
                {
                    current = current.twin.next;
                }
                else
                {
                    break; 
                }

                safety++;
            } while (current != start && safety < 20);

            // Fallback: Iterate the face loop
            HE_Edge runner = face.edge;
            int safety2 = 0;
            do
            {
                if (runner.vert == v) return runner;
                runner = runner.next;
                safety2++;
            } while (runner != face.edge && safety2 < 100);

            return null;
        }

        /// <summary>
        /// Finds all intersections between a defined cut line and the current mesh edges.
        /// It iterates through all edges in the mesh, checking for intersection with the segment defined by start and end.
        /// </summary>
        /// <param name="edges">The complete list of edges in the mesh.</param>
        /// <param name="start">The start point of the cutting line segment (in world space, usually).</param>
        /// <param name="end">The end point of the cutting line segment (in world space, usually).</param>
        /// <param name="transform">The transform of the paper object, used to convert edge positions to world space.</param>
        /// <param name="intersections">Output list of intersection details: (distance from start, edge hit, point of intersection).</param>
        /// <param name="affectedFaces">Output set of faces that belong to the edges hit by the cut line.</param>
        public static void FindEdgeIntersections(
            List<HE_Edge> edges, 
            Vector3 start, 
            Vector3 end, 
            Transform transform,
            out List<(float dist, HE_Edge edge, Vector3 point)> intersections,
            out HashSet<HE_Face> affectedFaces)
        {
            intersections = new List<(float, HE_Edge, Vector3)>();
            affectedFaces = new HashSet<HE_Face>();
            HashSet<HE_Edge> processedEdges = new HashSet<HE_Edge>();

            foreach (var edge in edges)
            {
                if (processedEdges.Contains(edge)) continue;
                if (edge.twin != null) processedEdges.Add(edge.twin);

                Vector3 p1 = transform.TransformPoint(edge.vert.position);
                Vector3 p2 = transform.TransformPoint(edge.next.vert.position);

                if (GetLineIntersection(start, end, p1, p2, out Vector3 hitPoint))
                {
                    float d = Vector3.Distance(start, hitPoint);
                    intersections.Add((d, edge, transform.InverseTransformPoint(hitPoint)));
                    
                    if (edge.face != null) affectedFaces.Add(edge.face);
                    if (edge.twin != null && edge.twin.face != null) affectedFaces.Add(edge.twin.face);
                }
            }

            intersections.Sort((a, b) => a.dist.CompareTo(b.dist));
        }

        /// <summary>
        /// Processes the found intersection points to split edges or identify existing vertices.
        /// It creates new vertices where edges are split and returns a set of all vertices involved in the cut (cut path).
        /// </summary>
        /// <param name="vertices">The master list of vertices to update.</param>
        /// <param name="edges">The master list of edges to update.</param>
        /// <param name="intersections">The sorted list of intersection events found by FindEdgeIntersections.</param>
        /// <param name="snapThreshold">Distance threshold to reuse existing vertices instead of splitting.</param>
        /// <returns>A unique set of vertices (both new and existing) that lie along the cut path.</returns>
        public static HashSet<HE_Vertex> SplitEdgesByIntersections(
            List<HE_Vertex> vertices, 
            List<HE_Edge> edges, 
            List<(float dist, HE_Edge edge, Vector3 point)> intersections, 
            float snapThreshold)
        {
            List<HE_Vertex> cutVertices = new List<HE_Vertex>();
            HashSet<HE_Vertex> uniqueCutVertices = new HashSet<HE_Vertex>(); 
            
            foreach(var hit in intersections)
            {
                HE_Vertex finalVertex = null;

                if (Vector3.Distance(hit.point, hit.edge.vert.position) < snapThreshold)
                {
                    finalVertex = hit.edge.vert;
                }
                else if (Vector3.Distance(hit.point, hit.edge.next.vert.position) < snapThreshold)
                {
                    finalVertex = hit.edge.next.vert;
                }
                else
                {
                    finalVertex = SplitEdge(vertices, edges, hit.edge, hit.point, Vector2.zero);
                }

                if (cutVertices.Count > 0 && cutVertices[cutVertices.Count - 1] == finalVertex)
                {
                    continue;
                }

                cutVertices.Add(finalVertex);
                uniqueCutVertices.Add(finalVertex);
            }
            return uniqueCutVertices;
        }

        /// <summary>
        /// Iterates through the affected faces and splits them if the cut passes completely through them.
        /// It checks if a face has exactly two vertices on the cut path and creates a new edge (crease) connecting them.
        /// </summary>
        /// <param name="faces">The master list of faces to update.</param>
        /// <param name="edges">The master list of edges to update.</param>
        /// <param name="facesToCheck">The set of faces identified as being touched by the cut.</param>
        /// <param name="cutVerticesSet">The set of vertices lying on the cut path.</param>
        public static void SplitFacesAlongCrease(
            List<HE_Face> faces, 
            List<HE_Edge> edges, 
            HashSet<HE_Face> facesToCheck, 
            HashSet<HE_Vertex> cutVerticesSet)
        {
            foreach (var face in facesToCheck)
            {
                List<HE_Vertex> faceCutVerts = new List<HE_Vertex>();
                
                HE_Edge runner = face.edge;
                int safety = 0;
                do
                {
                    if (cutVerticesSet.Contains(runner.vert))
                    {
                        faceCutVerts.Add(runner.vert);
                    }
                    runner = runner.next;
                    safety++;
                } while (runner != face.edge && safety < 100);

                if (faceCutVerts.Count == 2)
                {
                    HE_Vertex vStart = faceCutVerts[0];
                    HE_Vertex vEnd = faceCutVerts[1];

                    if (!AreVerticesConnected(vStart, vEnd))
                    {
                        SplitFace(faces, edges, face, vStart, vEnd);
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the specific rotation axis and pivot point for a vertex during a fold.
        /// This accounts for non-planar folds by finding the intersection of the "Global Cut Plane" and the specific "Face Plane" the vertex belongs to.
        /// </summary>
        /// <param name="v">The vertex to be rotated.</param>
        /// <param name="cutPlane">The global cutting plane defined by the fold line.</param>
        /// <param name="transform">The transform of the paper object.</param>
        /// <param name="axis">Output rotation axis direction.</param>
        /// <param name="pivot">Output pivot point for rotation.</param>
        /// <returns>True if a valid hinge axis could be calculated; otherwise false.</returns>
        public static bool GetFoldRotationData(
            HE_Vertex v, 
            Plane cutPlane, 
            Transform transform,
            out Vector3 axis, 
            out Vector3 pivot)
        {
            axis = Vector3.zero;
            pivot = Vector3.zero;

            HE_Face refFace = null;
            if (v.edge != null && v.edge.face != null) refFace = v.edge.face;

            if (refFace != null)
            {
                Vector3 pA = transform.TransformPoint(refFace.edge.vert.position);
                Vector3 pB = transform.TransformPoint(refFace.edge.next.vert.position);
                Vector3 pC = transform.TransformPoint(refFace.edge.next.next.vert.position);
                Vector3 faceNormal = Vector3.Cross(pB - pA, pC - pA).normalized;
                Plane facePlane = new Plane(faceNormal, pA);

                if (GetPlaneIntersection(cutPlane, facePlane, out Vector3 linePt, out Vector3 lineDir))
                {
                    axis = lineDir;
                    pivot = linePt;
                    return true;
                }
            }
            return false;
        }
    }
}