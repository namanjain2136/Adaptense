// ============================================================
// ADAPTER Project — Phase 6
// LabSceneBuilder.cs
//
// Editor utility: Adapter > Build Phase 6 Lab Scene
//
// Generates a polished two-table lab scene:
//   Source Table (left)      — 6 objects to pick up
//   Destination Table (right)— 6 drop zones to place them in
//   Floor + back wall        — for spatial grounding
//   UI Canvas                — info panel, hint banner, score
//                              counter, status bar
// ============================================================

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Adapter.Environment;
using Adapter.Gesture;
using Adapter.Learning;

namespace Adapter.EditorScripts
{
    public class LabSceneBuilder
    {
        [MenuItem("Adapter/Build Phase 6 Lab Scene")]
        public static void BuildLabScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Adapter/Scenes"))
                AssetDatabase.CreateFolder("Assets/Adapter", "Scenes");

            string scenePath = "Assets/Adapter/Scenes/Lab.unity";
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Remove default camera (Hand Tracking scene provides one)
            if (Camera.main != null) GameObject.DestroyImmediate(Camera.main.gameObject);

            // ─────────────────────────────────────────────
            // ENVIRONMENT
            // ─────────────────────────────────────────────

            // Floor
            var floor = MakeCube("Floor",
                new Vector3(0, 0, 0),
                new Vector3(8f, 0.1f, 5f),
                new Color(0.22f, 0.22f, 0.24f));

            // Back wall (visual backdrop)
            var wall = MakeCube("BackWall",
                new Vector3(0, 1.5f, -2.4f),
                new Vector3(8f, 3.2f, 0.15f),
                new Color(0.15f, 0.16f, 0.18f));

            // ─────────────────────────────────────────────
            // SOURCE TABLE (left)
            // ─────────────────────────────────────────────
            var srcTable = MakeCube("SourceTable",
                new Vector3(-2.0f, 0.52f, 0),
                new Vector3(2.6f, 1.05f, 1.4f),
                new Color(0.38f, 0.28f, 0.20f));
            MakeTableLabel("Source Table", new Vector3(-2.0f, 1.15f, -0.78f));

            // Table legs
            MakeTableLegs(srcTable.transform.position, srcTable.transform.localScale);

            // ─────────────────────────────────────────────
            // DESTINATION TABLE (right)
            // ─────────────────────────────────────────────
            var dstTable = MakeCube("DestinationTable",
                new Vector3(2.2f, 0.52f, 0),
                new Vector3(2.6f, 1.05f, 1.4f),
                new Color(0.18f, 0.22f, 0.28f));
            MakeTableLabel("Destination Table", new Vector3(2.2f, 1.15f, -0.78f));
            MakeTableLegs(dstTable.transform.position, dstTable.transform.localScale);

            // ─────────────────────────────────────────────
            // 6 OBJECTS + 6 DROP ZONES
            // ─────────────────────────────────────────────
            // Object definitions: (name, shape, color, src-x, src-z, dst-x, dst-z)
            var defs = new (string name, string shape, Color color, float sx, float sz, float dx, float dz)[]
            {
                ("Beaker",      "Cylinder", new Color(0.20f, 0.80f, 0.95f),  -2.8f,  0.3f,  1.45f,  0.35f),
                ("Book",        "Cube",     new Color(0.88f, 0.25f, 0.25f),  -2.1f,  0.3f,  2.05f,  0.35f),
                ("Flask",       "Sphere",   new Color(0.25f, 0.85f, 0.40f),  -1.4f,  0.3f,  2.65f,  0.35f),
                ("Battery",     "Cube",     new Color(0.95f, 0.80f, 0.15f),  -2.8f, -0.3f,  1.45f, -0.35f),
                ("Lens",        "Cylinder", new Color(0.75f, 0.35f, 0.95f),  -2.1f, -0.3f,  2.05f, -0.35f),
                ("SampleTube",  "Cylinder", new Color(1.0f,  0.55f, 0.15f),  -1.4f, -0.3f,  2.65f, -0.35f),
            };

            float srcTableTopY = 1.07f;   // top surface of source table
            float dstTableTopY = 1.07f;   // top surface of destination table

            GameObject[] objects   = new GameObject[defs.Length];
            GameObject[] dropZones = new GameObject[defs.Length];

            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];

                // --- source object ---
                Vector3 objPos  = new Vector3(d.sx, srcTableTopY + ObjectHalfHeight(d.shape), d.sz);
                Vector3 objScale = ObjectScale(d.shape);
                var obj = MakePrimitive(d.name, d.shape, objPos, objScale, d.color);
                var interactable = obj.AddComponent<InteractableObject>();
                interactable.objectName = d.name;
                objects[i] = obj;

                // --- drop zone on destination table ---
                Vector3 dzPos   = new Vector3(d.dx, dstTableTopY + 0.055f, d.dz);
                Vector3 dzScale = new Vector3(objScale.x * 1.1f, 0.04f, objScale.z * 1.1f);
                var dz = MakePrimitive($"DropZone_{d.name}", "Cube", dzPos, dzScale, new Color(0.2f, 0.9f, 1f, 0.35f));
                var dropZone   = dz.AddComponent<DropZone>();
                dropZone.zoneName = d.name;
                dropZone.assignedObject = interactable;
                dropZones[i] = dz;

                // Wire placement target
                interactable.placementTarget = dz.transform;
            }

            // ─────────────────────────────────────────────
            // UI CANVAS
            // ─────────────────────────────────────────────
            var canvasObj = new GameObject("UICanvas");
            var canvas    = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode       = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            canvasObj.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 1. Score counter — top-left
            var scoreCounter = BuildScoreCounter(canvasObj.transform, font);

            // 2. Status bar — top-center
            var statusBar = BuildStatusBar(canvasObj.transform, font);

            // 3. Hint banner — bottom-center
            var hintBanner = BuildHintBanner(canvasObj.transform, font);

            // 4. Side info panel — right edge
            var infoPanel = BuildInfoPanel(canvasObj.transform, font);

            // ─────────────────────────────────────────────
            // GESTURE CONTROLLER
            // ─────────────────────────────────────────────
            var controllerObj = new GameObject("HandGestureController");
            var controller    = controllerObj.AddComponent<GestureController>();
            controller.infoPanel = infoPanel;

            // Wire score counter and status bar into GestureActionMapper
            // (GestureController creates GestureActionMapper in Awake — we pass refs after)
            var actionMapper = controllerObj.AddComponent<GestureActionMapper>();
            actionMapper.infoPanel        = infoPanel;
            actionMapper.hintBanner       = hintBanner;
            actionMapper.scoreText        = scoreCounter.GetComponentInChildren<Text>();
            actionMapper.statusBarText    = statusBar.GetComponentInChildren<Text>();

            // ─────────────────────────────────────────────
            // DIRECTIONAL LIGHT
            // ─────────────────────────────────────────────
            var lightObj = new GameObject("DirectionalLight");
            var light    = lightObj.AddComponent<Light>();
            light.type      = LightType.Directional;
            light.intensity = 1.1f;
            light.color     = new Color(1f, 0.97f, 0.90f);
            lightObj.transform.rotation = Quaternion.Euler(42f, -30f, 0f);

            // ─────────────────────────────────────────────
            // SAVE + BUILD SETTINGS
            // ─────────────────────────────────────────────
            EditorSceneManager.SaveScene(scene, scenePath);

            var original = EditorBuildSettings.scenes;
            bool found = false;
            foreach (var s in original) if (s.path == scenePath) { found = true; break; }
            if (!found)
            {
                var updated = new EditorBuildSettingsScene[original.Length + 1];
                System.Array.Copy(original, updated, original.Length);
                updated[original.Length] = new EditorBuildSettingsScene(scenePath, true);
                EditorBuildSettings.scenes = updated;
            }

            Debug.Log("[Adaptense] Phase 6 Lab Scene built at " + scenePath);
        }

        // =====================================================
        // Helpers
        // =====================================================

        private static GameObject MakeCube(string name, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position   = pos;
            go.transform.localScale = scale;
            ApplyColor(go, color);
            return go;
        }

        private static GameObject MakePrimitive(string name, string shape, Vector3 pos, Vector3 scale, Color color)
        {
            PrimitiveType pType = shape == "Sphere"   ? PrimitiveType.Sphere
                                : shape == "Cylinder" ? PrimitiveType.Cylinder
                                                      : PrimitiveType.Cube;
            var go = GameObject.CreatePrimitive(pType);
            go.name = name;
            go.transform.position   = pos;
            go.transform.localScale = scale;
            ApplyColor(go, color);
            return go;
        }

        private static void ApplyColor(GameObject go, Color color)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void MakeTableLegs(Vector3 tablePos, Vector3 tableScale)
        {
            float legH = tablePos.y - 0.05f;  // height from floor to bottom of table top
            float hw   = tableScale.x * 0.5f - 0.12f;
            float hd   = tableScale.z * 0.5f - 0.12f;
            Color legColor = new Color(0.28f, 0.20f, 0.14f);

            Vector3[] corners = {
                new Vector3( hw,  0,  hd),
                new Vector3(-hw,  0,  hd),
                new Vector3( hw,  0, -hd),
                new Vector3(-hw,  0, -hd)
            };

            for (int i = 0; i < 4; i++)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "Leg";
                float cx = tablePos.x + corners[i].x;
                float cz = tablePos.z + corners[i].z;
                leg.transform.position   = new Vector3(cx, legH * 0.5f + 0.05f, cz);
                leg.transform.localScale = new Vector3(0.10f, legH, 0.10f);
                ApplyColor(leg, legColor);
            }
        }

        private static void MakeTableLabel(string text, Vector3 pos)
        {
            var go  = new GameObject($"Label_{text}");
            var tm  = go.AddComponent<TextMesh>();
            tm.text          = text;
            tm.fontSize      = 28;
            tm.characterSize = 0.065f;
            tm.anchor        = TextAnchor.MiddleCenter;
            tm.alignment     = TextAlignment.Center;
            tm.color         = new Color(0.8f, 0.85f, 1f);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, 180f, 0);
        }

        private static float ObjectHalfHeight(string shape)
        {
            return shape == "Cylinder" ? 0.25f
                 : shape == "Sphere"   ? 0.15f
                                       : 0.08f;
        }

        private static Vector3 ObjectScale(string shape)
        {
            return shape == "Cylinder" ? new Vector3(0.18f, 0.25f, 0.18f)
                 : shape == "Sphere"   ? new Vector3(0.22f, 0.22f, 0.22f)
                                       : new Vector3(0.28f, 0.16f, 0.38f);
        }

        // ---- UI factories ----

        private static GameObject BuildScoreCounter(Transform canvas, Font font)
        {
            var go = new GameObject("ScoreCounter");
            go.transform.SetParent(canvas, false);
            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.14f, 0.88f);
            RectTransform r = go.GetComponent<RectTransform>();
            r.anchorMin       = new Vector2(0f, 1f);
            r.anchorMax       = new Vector2(0f, 1f);
            r.pivot           = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(20f, -20f);
            r.sizeDelta       = new Vector2(190f, 56f);

            var textGo = new GameObject("ScoreText");
            textGo.transform.SetParent(go.transform, false);
            var t = textGo.AddComponent<Text>();
            t.font      = font;
            t.fontSize  = 20;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color     = Color.white;
            t.text      = "Placed: 0 / 6";
            var tr = textGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(10f, 6f);
            tr.offsetMax = new Vector2(-10f, -6f);
            return go;
        }

        private static GameObject BuildStatusBar(Transform canvas, Font font)
        {
            var go = new GameObject("StatusBar");
            go.transform.SetParent(canvas, false);
            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.07f, 0.12f, 0.90f);
            RectTransform r = go.GetComponent<RectTransform>();
            r.anchorMin       = new Vector2(0.5f, 1f);
            r.anchorMax       = new Vector2(0.5f, 1f);
            r.pivot           = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -20f);
            r.sizeDelta       = new Vector2(640f, 50f);

            var textGo = new GameObject("StatusText");
            textGo.transform.SetParent(go.transform, false);
            var t = textGo.AddComponent<Text>();
            t.font      = font;
            t.fontSize  = 17;
            t.alignment = TextAnchor.MiddleCenter;
            t.color     = new Color(0.85f, 0.95f, 1f);
            t.text      = "Point to select an object";
            var tr = textGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(14f, 5f);
            tr.offsetMax = new Vector2(-14f, -5f);
            return go;
        }

        private static GameObject BuildHintBanner(Transform canvas, Font font)
        {
            var go = new GameObject("HintBanner");
            go.transform.SetParent(canvas, false);
            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.14f, 0.93f);
            RectTransform r = go.GetComponent<RectTransform>();
            r.anchorMin       = new Vector2(0.5f, 0f);
            r.anchorMax       = new Vector2(0.5f, 0f);
            r.pivot           = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(0f, 28f);
            r.sizeDelta       = new Vector2(680f, 58f);

            var textGo = new GameObject("HintText");
            textGo.transform.SetParent(go.transform, false);
            var t = textGo.AddComponent<Text>();
            t.font      = font;
            t.fontSize  = 17;
            t.alignment = TextAnchor.MiddleCenter;
            t.color     = new Color(0.4f, 0.92f, 1f);
            t.text      = "Hint will appear here.";
            var tr = textGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(14f, 6f);
            tr.offsetMax = new Vector2(-14f, -6f);

            go.SetActive(false);
            return go;
        }

        private static GameObject BuildInfoPanel(Transform canvas, Font font)
        {
            var go = new GameObject("InfoPanel");
            go.transform.SetParent(canvas, false);
            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.07f, 0.11f, 0.18f, 0.90f);
            RectTransform r = go.GetComponent<RectTransform>();
            r.anchorMin       = new Vector2(1f, 0.5f);
            r.anchorMax       = new Vector2(1f, 0.5f);
            r.pivot           = new Vector2(1f, 0.5f);
            r.anchoredPosition = new Vector2(-20f, 0f);
            r.sizeDelta       = new Vector2(320f, 400f);

            var textGo = new GameObject("InfoText");
            textGo.transform.SetParent(go.transform, false);
            var t = textGo.AddComponent<Text>();
            t.font        = font;
            t.fontSize    = 17;
            t.alignment   = TextAnchor.UpperLeft;
            t.color       = Color.white;
            t.lineSpacing = 1.3f;
            t.text        = "<b>LAB INTERACTION</b>\nNo object selected";
            var tr = textGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(16f, 16f);
            tr.offsetMax = new Vector2(-16f, -16f);

            go.SetActive(false);
            return go;
        }
    }
}
