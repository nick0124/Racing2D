using UnityEngine;
using System.Collections;
using UnityEditor;
using System.Collections.Generic;
/*/////////////////////////////////////////100-chars////////////////////////////////////////////////
* This script should live in a folder named Editor so that it is deleted at runtime

*///////////////////////////////////////////////////////////////////////////////////////////////////
namespace PlanarMeshGenerator {
    [CustomEditor(typeof(MeshGenerator))]
    public class MeshGeneratorEditor : Editor {
        public static bool inEditMode = false;
        public static int hash = "MeshGenerator2DEditor".GetHashCode();
        private static GUIStyle ToggleButtonNormal = null;
        private static GUIStyle ToggleButtonToggled = null;
        public static Object lastTarget = null;
        //*******************************EDITMODE VARS*************************************************//
        public static Color editmodeColor = Color.cyan;
        public static float distFromPointToGrab = 40;
        public Vector3 pointSnap = Vector3.zero;// Vector3.one * .2f;
        public static Camera cam;
        public static float defaultCapSize = .5f;
        public static float capSize = .1f;
        public static bool mouseButtonDown_L = false;
        public static bool deletePressed = false;
        //***************CLOSEST TO MOUSE VARS*********//
        public static Vector3 mousePos;
        int indBeforeClosestPt;
        //***************DRAGGING POINT VARS**********//
        public static bool isDragging = false;
        public static int indOfPtBeingDragged;
        public static Vector3 ptOffsetFromMouse = Vector3.zero;

        public void OnSceneGUI() {
            if (target != lastTarget) {
                lastTarget = target;
                inEditMode = false;
                isDragging = false;
            }
            MeshGenerator meshGen = (MeshGenerator)target;
            if (meshGen.points == null) meshGen.points = new List<Vector2>();
            if (inEditMode)
                EditMode();
        }
        public override void OnInspectorGUI() {
            MeshGenerator meshGen = (MeshGenerator)target;
            if (ToggleButtonNormal == null || ToggleButtonToggled == null) {
                ToggleButtonNormal = "Button";
                ToggleButtonToggled = new GUIStyle(ToggleButtonNormal);
                ToggleButtonToggled.normal.background = ToggleButtonToggled.active.background;
            }
            base.OnInspectorGUI();
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("   Edit   \nMode"), inEditMode ? ToggleButtonToggled : ToggleButtonNormal)) {
                inEditMode = !inEditMode;
                if (SceneView.sceneViews.Count > 0)
                    ((SceneView)SceneView.sceneViews[0]).Focus();
            }
            if (GUILayout.Button(new GUIContent("Add Poly\nCollider"))) { meshGen.AddPolygonCollider(); }
            if (GUILayout.Button(new GUIContent("Add Edge\nCollider"))) { meshGen.AddEdgeCollider(0, 0); }
            if (GUILayout.Button(new GUIContent("Add Mesh\nCollider"))) { meshGen.GenerateMeshCollider(); }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("\nRebuild Mesh\n"))) { meshGen.UserRebuildMesh(); }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        /// <summary>
        /// The scene-editor logic for when the user is editing the outline of the mesh
        /// </summary>
        void EditMode() {
            Cursor.visible = true;
            Handles.color = editmodeColor;
            MeshGenerator meshGen = (MeshGenerator)target;
            Selection.activeGameObject = meshGen.gameObject; //keeps this as the active gameobject
            AlignSceneCamera(meshGen.transform);
            DrawPolyLines(meshGen.points.ToArray(), meshGen.transform);
            UpdateUserInput();
            //Resize caps so they look the same size regardless of distance between the cam and the meshGenerator
            Vector3 pos = SceneView.currentDrawingSceneView.camera.WorldToScreenPoint(meshGen.transform.position);
            pos.x += 10;
            pos = SceneView.currentDrawingSceneView.camera.ScreenToWorldPoint(pos);
            capSize = (pos - meshGen.transform.position).magnitude * defaultCapSize;
            if (isDragging)
                Dragging();
            else
                EditModeUpdate();
            SceneView.RepaintAll();
        }
        /// <summary>
        /// Handles dragging a point around the screen
        /// </summary>
        void Dragging() {
            MeshGenerator meshGen = (MeshGenerator)target;
            if (!mouseButtonDown_L || indOfPtBeingDragged < 0 || meshGen.points == null || indOfPtBeingDragged >= meshGen.points.Count) {
                isDragging = false;
                indOfPtBeingDragged = -1;
            } else {
                Vector3 screenPos = mousePos + ptOffsetFromMouse;
                Vector3 worldPos = SceneView.currentDrawingSceneView.camera.ScreenToWorldPoint(screenPos);
                Handles.DotCap(indOfPtBeingDragged, worldPos, Quaternion.identity, capSize);
                meshGen.points[indOfPtBeingDragged] = meshGen.transform.InverseTransformPoint(worldPos);
                Handles.DotCap(indOfPtBeingDragged, worldPos, Quaternion.identity, capSize);
            }
        }
        /// <summary>
        /// Looks through the points, updating which the mouse is closest to and 
        /// checking if the user has grabbed any or is adding one
        /// </summary>
        void EditModeUpdate() {
            MeshGenerator meshGen = (MeshGenerator)target;
            bool inGrabbingRange = false; //true iff there the mouse is within grabbing distance of an existing point
            indBeforeClosestPt = -1; //starting index of the line seg on which the closest point lies
            float closestPtsDist = float.MaxValue;//how close the closest point is to the mouse cursor
            float parameterizationOfPointOnLine = 0; //How far along its line seg the closest point is located

            Vector3 currScreenPos = Vector3.zero; //cur point in loop
            Vector3 nextScreenPos = meshGen.transform.TransformPoint(meshGen.points[0]); //next point in loop
            nextScreenPos = SceneView.currentDrawingSceneView.camera.WorldToScreenPoint(nextScreenPos);
            nextScreenPos.z = 0;
            for (int i = 0; i < meshGen.points.Count; i++) {
                currScreenPos = nextScreenPos;
                nextScreenPos = meshGen.transform.TransformPoint(meshGen.points[(i + 1) % meshGen.points.Count]);
                nextScreenPos = SceneView.currentDrawingSceneView.camera.WorldToScreenPoint(nextScreenPos);
                nextScreenPos.z = 0;
                //**************************find closest grabbable point or point on line seg to add**************
                //if the current point is in grabbing distance and closer than all other grabbables in distance, we'll say it's the closest point
                if ((currScreenPos - mousePos).magnitude <= distFromPointToGrab) {
                    float dist = (currScreenPos - mousePos).magnitude;
                    if (!inGrabbingRange || dist < closestPtsDist) {
                        closestPtsDist = dist;
                        inGrabbingRange = true;
                        indBeforeClosestPt = i;
                        parameterizationOfPointOnLine = 0;
                    }
                }
                Undo.RecordObject(meshGen, "Undo edit polygon");
                //if the next point is in grabbing distance and closer than all other grabbables in distance, we'll say it's the closest point
                if ((nextScreenPos - mousePos).magnitude <= distFromPointToGrab) {
                    float dist = (nextScreenPos - mousePos).magnitude;
                    if (!inGrabbingRange || dist < closestPtsDist) {
                        closestPtsDist = dist;
                        inGrabbingRange = true;
                        indBeforeClosestPt = (i + 1) % meshGen.points.Count;
                        parameterizationOfPointOnLine = 0;
                    }
                }
                //if no grabbable points have been found in range, find a point on the line segment closest
                if (!inGrabbingRange) {
                    Vector3 closestPoint = ClosestPointOnLineSeg(currScreenPos, nextScreenPos, mousePos);
                    float dist = (closestPoint - mousePos).magnitude;
                    if (dist < closestPtsDist) {//becomes the new closest point
                        indBeforeClosestPt = i;
                        closestPtsDist = dist;
                        parameterizationOfPointOnLine = (currScreenPos - closestPoint).magnitude / (currScreenPos - nextScreenPos).magnitude;
                    }
                }
            }
            //*******************************************REACTIONS***********************************************//
            if (indBeforeClosestPt < 0) return;  //No close points were found
            Vector3 prev = meshGen.transform.TransformPoint(meshGen.points[indBeforeClosestPt]);
            Vector3 next = meshGen.transform.TransformPoint(meshGen.points[(indBeforeClosestPt + 1) % meshGen.points.Count]);
            Vector3 closestPt = Vector3.Lerp(prev, next, parameterizationOfPointOnLine);
            Handles.DotCap(0, closestPt, Quaternion.identity, capSize);
            //If moving or adding a point
            if (mouseButtonDown_L) {
                if (closestPtsDist > distFromPointToGrab) EndEditMode(); //if click too far from mesh point or line seg, quit editmode
                else {
                    if (inGrabbingRange) {           //start dragging the closest point
                        indOfPtBeingDragged = indBeforeClosestPt;
                    } else {                         //add an new point on a line segment
                        Handles.color = Color.white;
                        Handles.DotCap(1, closestPt, Quaternion.identity, capSize + .1f);
                        meshGen.points.Insert(indBeforeClosestPt + 1, Vector2.Lerp(meshGen.points[indBeforeClosestPt],
                            meshGen.points[(indBeforeClosestPt + 1) % meshGen.points.Count], parameterizationOfPointOnLine));
                        indOfPtBeingDragged = indBeforeClosestPt + 1;
                    }
                    //calc offset from point being dragged to mouse
                    Vector3 pt = meshGen.transform.TransformPoint(meshGen.points[indOfPtBeingDragged]);
                    pt = SceneView.currentDrawingSceneView.camera.WorldToScreenPoint(pt);
                    ptOffsetFromMouse = pt - mousePos;
                    isDragging = true;//start dragging
                }
            }//else if deleting a point
            else if (inGrabbingRange && deletePressed && meshGen.points.Count > 3)
                meshGen.points.RemoveAt(indBeforeClosestPt);

        }

        /// <summary>
        /// Updates variables which store user key and mouse input
        /// </summary>
        void UpdateUserInput() {
            //Check if "delete" was pressed
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Delete) {
                deletePressed = true;
                Event.current.Use();
            } else
                deletePressed = false;
            //update mouse's position
            mousePos = Event.current.mousePosition;
            mousePos.y = SceneView.currentDrawingSceneView.camera.pixelHeight - mousePos.y;
            mousePos.z = 0;
            //check if left mouse button up or down
            UpdateLeftMousebuttonStatus();

        }
        /// <summary>
        /// Monitors whether left mouse button is up or down
        /// </summary>
        void UpdateLeftMousebuttonStatus() {
            int ID = GUIUtility.GetControlID(hash, FocusType.Passive);
            Event current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0) {
                mouseButtonDown_L = true;
                Cursor.visible = false;
                GUIUtility.hotControl = ID;
                Event.current.Use();
            } else if (current.type == EventType.MouseUp && current.button == 0) {
                mouseButtonDown_L = false;
                Cursor.visible = true;
                if (GUIUtility.hotControl == ID)
                    GUIUtility.hotControl = 0;
                Event.current.Use();
            }
        }
        /// <summary>
        /// Aligns the scene view's rotation with the meshgenerator
        /// </summary>
        /// <param name="objTrans"></param>
        void AlignSceneCamera(Transform objTrans) {
            if (SceneView.currentDrawingSceneView.camera.transform.localRotation != objTrans.localRotation) {
                SceneView.currentDrawingSceneView.rotation = objTrans.rotation;
            }
        }

        /// <summary>
        /// Called when leaving editmode
        /// </summary>
        void EndEditMode() {
            isDragging = false;
            indOfPtBeingDragged = -1;
            Cursor.visible = true;
            mouseButtonDown_L = false;
            inEditMode = false;
            Selection.activeGameObject = ((MeshGenerator)target).gameObject;
        }
        /// <summary>
        /// Draws lines between the points
        /// </summary>
        /// <param name="points"></param>
        /// <param name="trnsfrm"></param>
        public void DrawPolyLines(Vector2[] points, Transform trnsfrm) {
            if (points == null || points.Length == 0) return;
            Vector3 start;
            Vector3 end = trnsfrm.TransformPoint(points[0]);
            for (int i = 0; i < points.Length; i++) {
                start = end;
                end = trnsfrm.TransformPoint(points[(i + 1) % points.Length]);
                Handles.DrawLine(start, end);
            }
        }
        /// <summary>
        /// Calculates the point on the line segment between "startPt" and "endPt" which is closest to "point"
        /// </summary>
        /// <param name="startPt"></param>
        /// <param name="endPt"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        private Vector3 ClosestPointOnLineSeg(Vector3 startPt, Vector3 endPt, Vector3 point) {
            Vector3 toPoint = point - startPt;
            Vector3 lineVector = (endPt - startPt).normalized;
            float segLen = Vector3.Distance(startPt, endPt);
            float t = Vector3.Dot(lineVector, toPoint) / segLen;

            Mathf.Clamp(t, 0, 1);
            return Vector3.Lerp(startPt, endPt, t);
        }

        //Generates a polyCollider around the mesh
        void GeneratePolyCollider() {
            MeshGenerator meshGen = (MeshGenerator)target;
            PolygonCollider2D col = meshGen.gameObject.AddComponent<PolygonCollider2D>();
            col.points = meshGen.points.ToArray();
        }
        //Generates an edgeCollider around the mesh
        void GenerateEdgeCollider() {
            MeshGenerator meshGen = (MeshGenerator)target;
            EdgeCollider2D col = meshGen.gameObject.AddComponent<EdgeCollider2D>();
            col.points = meshGen.points.ToArray();
        }


    }
}
