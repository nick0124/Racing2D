using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
/*/////////////////////////////////////////100-chars////////////////////////////////////////////////
* A 2D mesh generator that implemented using a clipping algorithm with concave abilities.
* It's a good idea to delete this script from gameobjects once the finalized mesh has been 
* designed in-editor.
*///////////////////////////////////////////////////////////////////////////////////////////////////
namespace PlanarMeshGenerator {
    [ExecuteInEditMode, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class MeshGenerator : MonoBehaviour {
        [HideInInspector]
        public List<Vector2> points; //the points in this polygon, in CCW order ;)
        [HideInInspector]
        public MeshFilter meshFilter; //the meshfilter of the mesh being used

        //user settings
        public bool linkedToClones = false;
        private bool wasLinkedToClones = true;
        public bool generateRim = false;
        public float rimWidth = 0.7f;
        public float rimFadeModifier = 1.5f;

        List<Vector3> vertices;
        List<int> tris;
        List<Vector2> rimUV;
        int numOfPolygonPointsOnLastMesh = 0; //how large was the points List when the mesh was last built
        void OnEnable() {
            Setup();
        }
        /// <summary>
        /// Sets up the game object, making sure the components are alright. If it rebuilt the mesh during the process, returns true
        /// </summary>
        /// <returns></returns>
        bool Setup() {
            bool needToRebuild = false;
            if (meshFilter == null) {
                meshFilter = this.GetComponent<MeshFilter>();
                if (meshFilter == null) {
                    meshFilter = this.gameObject.AddComponent<MeshFilter>();
                    needToRebuild = true;
                }
            }

            if (points == null || points.Count < 3) {
                points = new List<Vector2>();
                points.Add(Vector2.right * 3);
                points.Add(Vector2.up * 3);
                points.Add(Vector2.left * 3);
                numOfPolygonPointsOnLastMesh = 3;
                needToRebuild = true;
            }
            if ((!linkedToClones && wasLinkedToClones) || meshFilter.sharedMesh == null) {
                wasLinkedToClones = linkedToClones;
                meshFilter.sharedMesh = new Mesh();
                needToRebuild = true;
            }
            if (needToRebuild) BuildMesh();
            return needToRebuild;
        }
        void Reset() {
            if (points != null) points.Clear();
            else points = new List<Vector2>();
            linkedToClones = false;
            generateRim = false;
            rimWidth = .7f;
            rimFadeModifier = 1.5f;
            Setup();
        }


        //***************************MESH BUILDING LOGIC********************************//
        public void UserRebuildMesh() {
            if (Setup()) return;
            Object[] objs = new Object[2];
            objs[0] = this;
            objs[1] = meshFilter.sharedMesh;
            Undo.RegisterCompleteObjectUndo(this.meshFilter, "Rebuilding mesh"); //TODO: REGISTER REBUILD TO UNDO
            BuildMesh();
        }

        public void BuildMesh() {
            vertices = new List<Vector3>();
            rimUV = new List<Vector2>();
            for (int i = 0; i < points.Count; i++) {
                vertices.Add(points[i]);
                rimUV.Add(Vector2.zero);
            }
            tris = new List<int>(EarClipping(vertices.ToArray()));

            //Generate rim if necessary
            if (generateRim)
                GenerateRimMesh();
            Vector3[] norms = new Vector3[vertices.Count];
            Vector2[] uvs = new Vector2[vertices.Count];
            for (int i = 0; i < norms.Length; i++) {
                norms[i] = Vector3.back;
                uvs[i] = vertices[i];
            }

            //to ensure it's unique, give it its own shared mesh
            if (!linkedToClones)
                this.meshFilter.sharedMesh = new Mesh();
            numOfPolygonPointsOnLastMesh = this.points.Count;
            meshFilter.sharedMesh.Clear();
            meshFilter.sharedMesh.vertices = (vertices.ToArray());
            meshFilter.sharedMesh.triangles = (tris.ToArray());
            meshFilter.sharedMesh.uv = uvs;
            meshFilter.sharedMesh.normals = norms;
            meshFilter.sharedMesh.uv2 = rimUV.ToArray();
        }
        //Struct used during clipping algorithm
        struct ClippingElem {
            public Vector3 pt;
            public int ind;
        }
        //Generates tris via the ear-clipping method
        public int[] EarClipping(Vector3[] pts) {
            List<int> tris = new List<int>();//stores the tris(clockwise! Remember, clockwise)
            List<ClippingElem> points = new List<ClippingElem>();
            for (int i = 0; i < pts.Length; i++) {
                ClippingElem toAdd = new ClippingElem();
                toAdd.pt = pts[i];
                toAdd.ind = i;
                points.Add(toAdd);
            }

            while (true) {
                //Terminating cases
                if (points.Count < 3) break;
                if (points.Count == 3) {
                    tris.Add(points[2].ind);
                    tris.Add(points[1].ind);
                    tris.Add(points[0].ind);
                    break;
                }

                bool clipped = false;//tracks if a triangle was clipped this round
                for (int i = 0; i < points.Count; i++) {
                    int prevInd = i - 1;
                    if (prevInd < 0) prevInd += points.Count;
                    int nextInd = (i + 1) % points.Count;

                    Vector2 prev = points[prevInd].pt;
                    Vector2 cur = points[i].pt;
                    Vector2 next = points[nextInd].pt;
                    Vector2 toPrev = cur - prev;//position vector from cur to prev
                    Vector2 toNext = next - cur;//position vector from cur to next

                    //if cur not interior vertex, nope
                    if (SignedAngleBetween(new Vector3(toPrev.x, toPrev.y, transform.position.z), new Vector3(toNext.x, toNext.y, transform.position.z), Vector3.back) >= 0)
                        continue;
                    //if there's a different point sitting inside this triangle, nope 
                    bool pointInTri = false;
                    for (int j = 0; j < points.Count; j++) {
                        if (j == i || j == prevInd || j == nextInd) continue; //dont check the points being considered
                        if (PointInTri2(prev, cur, next, points[j].pt)) {
                            pointInTri = true;
                            break;
                        }
                    }
                    //if there were no points in tri, and this is not an interior point, clip it!
                    if (!pointInTri) {
                        clipped = true;
                        tris.Add(points[nextInd].ind);
                        tris.Add(points[i].ind);
                        tris.Add(points[prevInd].ind);
                        points.RemoveAt(i);
                        i--;
                        break;
                    }
                }
                if (!clipped) break;//if nothing was trimmed this round, algorithm is finished
            }
            return tris.ToArray();
        }
        //Generate's mesh at for the rim
        public void GenerateRimMesh() {
            if (this.vertices == null || this.tris == null) return;
            Vector3[] origVerts = vertices.ToArray();
            for (int i = 0; i < origVerts.Length; i++) {
                int prevInd = i - 1;
                if (prevInd < 0) prevInd += origVerts.Length;
                int nextInd = (i + 1) % origVerts.Length;
                Vector2 prev = origVerts[prevInd];
                Vector2 cur = origVerts[i];
                Vector2 next = origVerts[nextInd];
                Vector2 toPrev = prev - cur;//position vector from cur to prev
                Vector2 toNext = next - cur;//position vector from cur to next
                                            //generate at the angle
                float angle = SignedAngleBetween(new Vector3(toPrev.x, toPrev.y, transform.position.z), new Vector3(toNext.x, toNext.y, transform.position.z), Vector3.back);
                if (angle == 0 || angle == 180)//if straight line, continue
                    continue;
                else if (angle < 0)//if interior angle
                    GenerateRimAtInteriorAngle(prevInd, i, cur, toPrev, toNext, angle);
                else//else if exterior
                    GenerateRimAtExteriorAngle(prevInd, i, cur, toPrev, toNext);
            }
            //Close the rim loop
            tris.Add(origVerts.Length - 1);
            tris.Add(origVerts.Length);
            tris.Add(vertices.Count - 1);
        }
        //Generates the rim at an interior angle, adding the vertices and tris generated to the  vertices and tris Lists
        private void GenerateRimAtInteriorAngle(int prevInd, int curInd, Vector2 cur, Vector2 toPrev, Vector2 toNext, float angle) {
            angle *= -.5f * Mathf.Deg2Rad;
            Vector3 normAtVert = (toPrev.normalized + toNext.normalized);
            normAtVert.z = 0;
            normAtVert.Normalize();
            float normLen = rimWidth / Mathf.Sin(angle);
            Vector3 vertToAdd = new Vector3(cur.x, cur.y, 0) + normAtVert * normLen;
            vertToAdd.z = 0;// this.transform.position.z;
                            //add it to the mesh and generate the new tris
            tris.Add(vertices.Count - 1);
            tris.Add(prevInd);
            tris.Add(curInd);
            tris.Add(vertices.Count);
            tris.Add(vertices.Count - 1);
            tris.Add(curInd);
            vertices.Add(vertToAdd);
            rimUV.Add(Vector2.right * rimFadeModifier);
        }
        //Generates the rim at an exterior angle, adding the vertices and tris generated to the vertices and tris Lists
        private void GenerateRimAtExteriorAngle(int prevInd, int curInd, Vector2 cur, Vector2 toPrev, Vector2 toNext) {
            Vector3 tPrev = toPrev;
            Vector3 tNext = toNext;
            Vector3 curV3 = cur;
            Vector3 prevNorm = Vector3.Cross(tPrev, Vector3.back).normalized;
            Vector3 nextNorm = Vector3.Cross(Vector3.back, tNext).normalized;
            //Add vert before the corner
            Vector3 newVert = curV3 + prevNorm * rimWidth;
            newVert.z = 0;//this.transform.position.z;
            tris.Add(vertices.Count - 1);
            tris.Add(prevInd);
            tris.Add(curInd);
            tris.Add(curInd);
            tris.Add(vertices.Count);
            tris.Add(vertices.Count - 1);
            vertices.Add(newVert);
            rimUV.Add(Vector2.right * rimFadeModifier);
            //Add vert at the corner
            newVert = curV3 + (prevNorm + nextNorm).normalized * rimWidth;
            newVert.z = 0;//this.transform.position.z;
            tris.Add(vertices.Count);
            tris.Add(vertices.Count - 1);
            tris.Add(curInd);
            vertices.Add(newVert);
            rimUV.Add(Vector2.right * rimFadeModifier);
            //Add vert after the corner
            newVert = curV3 + nextNorm * rimWidth;
            newVert.z = 0;//this.transform.position.z;
            tris.Add(curInd);
            tris.Add(vertices.Count);
            tris.Add(vertices.Count - 1);
            vertices.Add(newVert);
            rimUV.Add(Vector2.right * rimFadeModifier);
        }
        //gets or creates a meshfilter if needed
        void GetMeshFilter() {
            meshFilter = this.GetComponent<MeshFilter>();
            if (meshFilter == null) {
                meshFilter = this.gameObject.AddComponent<MeshFilter>();
            }
            if (meshFilter.sharedMesh == null)
                meshFilter.sharedMesh = new Mesh();
        }
        //Determines the angle(in degrees) between 2 vectors.  This is for CCW?
        float SignedAngleBetween(Vector3 a, Vector3 b, Vector3 n) {
            // angle in [0,180]
            a.z = 0;
            b.z = 0;
            float angle = Vector3.Angle(a, b);
            float sign = Mathf.Sign(Vector3.Dot(n, Vector3.Cross(a, b)));
            // angle in [-179,180]
            float signed_angle = angle * sign;
            //float angle360 =  (signed_angle + 180) % 360; // option for angle in [0,360]
            return signed_angle;
        }
        //checks if the point p is in the triangle ABC
        bool PointInTri2(Vector2 A, Vector2 B, Vector2 C, Vector2 P) {
            Vector2 v0 = C - A;
            Vector2 v1 = B - A;
            Vector2 v2 = P - A;

            float dot00 = Vector2.Dot(v0, v0);
            float dot01 = Vector2.Dot(v0, v1);
            float dot02 = Vector2.Dot(v0, v2);
            float dot11 = Vector2.Dot(v1, v1);
            float dot12 = Vector2.Dot(v1, v2);

            float invDenom = 1 / (dot00 * dot11 - dot01 * dot01);
            float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
            float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

            return (u >= 0) && (v >= 0) && (u + v < 1);
        }

        /// <summary>
        /// Generates and adds an EdgeCollider to the gameobject based on the mesh's current shape
        /// </summary>
        public void AddEdgeCollider(int startInd, int endInd) {
            if (this.gameObject.GetComponent<Collider>() != null) {
                Debug.Log("Cannot add EdgeCollider because gameobject already contains a Collider(3D)");
                return;
            }
            if (vertices == null || this.points == null || this.points.Count < 3) {
                Debug.Log("MeshGenerator needs at least 3 points to build mesh");
                return;
            }
            EdgeCollider2D pc = this.gameObject.AddComponent<EdgeCollider2D>();
            Undo.RegisterCreatedObjectUndo(pc, "Undo add EdgeCollider");
            Vector2[] pts;
            //If generating around a rim
            if (vertices.Count >= numOfPolygonPointsOnLastMesh * 2) {
                pts = new Vector2[vertices.Count - numOfPolygonPointsOnLastMesh];
                for (int i = 0; i < pts.Length; i++) {
                    pts[i] = vertices[i + numOfPolygonPointsOnLastMesh];
                }
            }//if generating without a rim
            else {
                pts = new Vector2[vertices.Count];
                for (int i = 0; i < vertices.Count; i++) {
                    pts[i] = vertices[i];
                }
            }
            pc.points = pts;
        }
        /// <summary>
        /// Generates and adds a PolygonCollider to the gameobject based on the mesh's current shape
        /// </summary>
        public void AddPolygonCollider() {
            if (this.gameObject.GetComponent<Collider>() != null) {
                Debug.Log("Cannot add PolygonCollider because gameobject already contains a Collider(3D)");
                return;
            }
            if (vertices == null || this.points == null || this.points.Count < 3) {
                Debug.Log("MeshGenerator needs at least 3 points to build mesh");
                return;
            }
            PolygonCollider2D pc = this.gameObject.AddComponent<PolygonCollider2D>();
            Undo.RegisterCreatedObjectUndo(pc, "Undo add PolygonCollider");
            Vector2[] pts;
            //If generating around a rim
            if (vertices.Count >= numOfPolygonPointsOnLastMesh * 2) {
                pts = new Vector2[vertices.Count - numOfPolygonPointsOnLastMesh];
                for (int i = 0; i < pts.Length; i++) {
                    pts[i] = vertices[i + numOfPolygonPointsOnLastMesh];
                }
            }//if generating without a rim
            else {
                pts = new Vector2[vertices.Count];
                for (int i = 0; i < vertices.Count; i++) {
                    pts[i] = vertices[i];
                }
            }
            pc.points = pts;
        }
        /// <summary>
        /// Generates and adds a MeshCollider component based on the mesh's current shape
        /// </summary>
        public void GenerateMeshCollider() {
            if (this.gameObject.GetComponent<Collider2D>() != null) {
                Debug.Log("Cannot add Mesh Collider because gameobject already contains a Collider2D");
                return;
            }
            MeshCollider mc = this.gameObject.AddComponent<MeshCollider>();
            Undo.RegisterCreatedObjectUndo(mc, "Undo add MeshCollider");
            mc.sharedMesh = meshFilter.sharedMesh;
        }
    }
}