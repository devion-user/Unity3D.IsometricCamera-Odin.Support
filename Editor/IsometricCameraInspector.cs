using EngineModule.Editors;
using PowerTools;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace IsometricCameraModule.Editor
{
    [CustomEditor(typeof(IsometricCamera))]
    public class IsometricCameraInspector : OdinEditor
    {
        IsometricCamera t;
        
        protected override void OnEnable()
        {
            base.OnEnable();
            t = (IsometricCamera) target;
            SceneView.onSceneGUIDelegate += OnSceneGUIDelegate;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SceneView.onSceneGUIDelegate -= OnSceneGUIDelegate;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
        }

        private static Vector3[] quad = new Vector3[4];
        private void OnSceneGUIDelegate(SceneView sceneview)
        {
            var rect = t.WorldRect;
            quad[0] = new Vector3(rect.xMin, rect.yMax);
            quad[1] = new Vector3(rect.xMax, rect.yMax);
            quad[2] = new Vector3(rect.xMax, rect.yMin);
            quad[3] = new Vector3(rect.xMin, rect.yMin);

            Render.UseWorldMaterial();

            Render.color = Color.blue;
            Render.LineLoop(quad);

            Render.color = Color.blue.WithAlpha(.4f);
            Render.Polygon(quad);

            Render.color = Color.blue;
            Vector2 result;
            if (TrySlider(new Vector2(rect.xMin, rect.center.y), Vector2.left, out result))
            {
                Undo.RecordObject(t, "Change IsometricCamera WorldRect");
                rect.xMin = result.x;
                t.WorldRect = rect;
                EditorUtility.SetDirty(t);
            }
            if (TrySlider(new Vector2(rect.xMax, rect.center.y), Vector2.right, out result))
            {
                Undo.RecordObject(t, "Change IsometricCamera WorldRect");
                rect.xMax = result.x;
                t.WorldRect = rect;
                EditorUtility.SetDirty(t);
            }
            if (TrySlider(new Vector2(rect.center.x, rect.yMin), Vector2.up, out result))
            {
                Undo.RecordObject(t, "Change IsometricCamera WorldRect");
                rect.yMin = result.y;
                t.WorldRect = rect;
                EditorUtility.SetDirty(t);
            }
            if (TrySlider(new Vector2(rect.center.x, rect.yMax), Vector2.down, out result))
            {
                Undo.RecordObject(t, "Change IsometricCamera WorldRect");
                rect.yMax = result.y;
                t.WorldRect = rect;
                EditorUtility.SetDirty(t);
            }

            var cam = t.GetComponent<Camera>();
            var pixelSize = new Vector2(cam.orthographicSize * cam.aspect, cam.orthographicSize) * 2;
            var pixelBounds = new Bounds(new Vector3(cam.transform.position.x, cam.transform.position.y, 0), pixelSize);
            var pixelPos = pixelBounds.min;
            var pixelRect = new Rect(pixelPos, pixelSize);

            quad[0] = new Vector3(pixelRect.xMin, pixelRect.yMax);
            quad[1] = new Vector3(pixelRect.xMax, pixelRect.yMax);
            quad[2] = new Vector3(pixelRect.xMax, pixelRect.yMin);
            quad[3] = new Vector3(pixelRect.xMin, pixelRect.yMin);
            Render.color = Color.yellow;
            Render.LineLoop(quad);

            var deltaX = t.RubberDelta(cam.pixelWidth, pixelSize.x);
            var deltaY = t.RubberDelta(cam.pixelHeight, pixelSize.y);

            quad[0] = new Vector3(t.WorldRect.xMin - deltaX, t.WorldRect.yMax + deltaY);
            quad[1] = new Vector3(t.WorldRect.xMax + deltaX, t.WorldRect.yMax + deltaY);
            quad[2] = new Vector3(t.WorldRect.xMax + deltaX, t.WorldRect.yMin - deltaY);
            quad[3] = new Vector3(t.WorldRect.xMin - deltaX, t.WorldRect.yMin - deltaY);
            Render.color = Color.red;
            Render.LineLoop(quad);
        }

        const float scale = .1f;
        private bool TrySlider(Vector2 source, Vector2 direction, out Vector2 result)
        {
            var position = new Vector3(source.x, source.y);
            EditorGUI.BeginChangeCheck();
            position = Handles.Slider(position, direction, HandleUtility.GetHandleSize(position) * scale, Handles.CubeHandleCap, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                result = position;
                return true;
            }
            result = Vector2.zero;
            return false;
        }
    }
}