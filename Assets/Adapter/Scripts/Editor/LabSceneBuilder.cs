// ============================================================
// ADAPTER Project — Phase 6 (fixed)
// LabSceneBuilder.cs
//
// Adapter > Build Phase 6 Lab Scene
//
// KEY DESIGN:  The Lab scene is loaded additively over the
// Hand Tracking webcam feed.  Any opaque 3D geometry is
// rendered by the same camera and will block the webcam.
// Therefore we use NO opaque large geometry (no floor, no
// wall, no solid tables).  All spatial cues are provided
// through transparent/glowing UI-canvas overlays and small
// coloured object spheres/cylinders.
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

            // Remove default camera — Hand Tracking scene provides one
            if (Camera.main != null) GameObject.DestroyImmediate(Camera.main.gameObject);
            // Remove default directional light to avoid double-lighting
            var defaultLight = GameObject.Find("Directional Light");
            if (defaultLight != null) GameObject.DestroyImmediate(defaultLight);

            // ─────────────────────────────────────────────────────
            // 6 SMALL INTERACTABLE OBJECTS  (source positions)
            // Spread across the bottom third of screen-space
            // (small size so they don't block the webcam too much)
            // ─────────────────────────────────────────────────────
            var defs = new (string name, string shape, Color color, float x, float y, float z)[]
            {
                ("Beaker",     "Cylinder", new Color(0.20f, 0.80f, 0.95f), -1.6f, -1.2f,  3.0f),
                ("Book",       "Cube",     new Color(0.88f, 0.25f, 0.25f), -0.9f, -1.2f,  3.0f),
                ("Flask",      "Sphere",   new Color(0.25f, 0.85f, 0.40f), -0.2f, -1.2f,  3.0f),
                ("Battery",    "Cube",     new Color(0.95f, 0.80f, 0.15f),  0.5f, -1.2f,  3.0f),
                ("Lens",       "Cylinder", new Color(0.75f, 0.35f, 0.95f),  1.2f, -1.2f,  3.0f),
                ("SampleTube", "Cylinder", new Color(1.0f,  0.55f, 0.15f),  1.9f, -1.2f,  3.0f),
            };

            // Drop zone positions (upper row, same x-spread)
            var dzPositions = new float[] { -1.6f, -0.9f, -0.2f, 0.5f, 1.2f, 1.9f };
            float dzY = 0.8f;
            float dzZ = 3.0f;

            GameObject[] objects   = new GameObject[defs.Length];
            GameObject[] dropZones = new GameObject[defs.Length];

            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                Vector3 objScale = ObjectScale(d.shape);
                var obj = MakePrimitive(d.name, d.shape,
                    new Vector3(d.x, d.y, d.z), objScale, d.color);
                var interactable = obj.AddComponent<InteractableObject>();
                interactable.objectName = d.name;
                objects[i] = obj;

                // Drop zone — flat cylinder marker above the objects row
                var dz = MakePrimitive($"DropZone_{d.name}", "Cylinder",
                    new Vector3(dzPositions[i], dzY, dzZ),
                    new Vector3(0.22f, 0.015f, 0.22f),
                    new Color(0.2f, 0.9f, 1f, 0.3f));
                var dropZone = dz.AddComponent<DropZone>();
                dropZone.zoneName = d.name;
                dropZone.assignedObject = interactable;
                dropZones[i] = dz;

                interactable.placementTarget = dz.transform;
            }

            // ─────────────────────────────────────────────────────
            // UI CANVAS
            // ─────────────────────────────────────────────────────
            var canvasObj = new GameObject("UICanvas");
            var canvas    = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            canvasObj.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Row labels above drop zones (canvas overlay — not 3D text)
            BuildZoneLabels(canvasObj.transform, font, defs);

            // Row labels below objects
            BuildObjectLabels(canvasObj.transform, font, defs);

            // Score counter — top-left
            var scoreGo   = BuildScoreCounter(canvasObj.transform, font);
            // Status bar  — top-center
            var statusGo  = BuildStatusBar(canvasObj.transform, font);
            // Hint banner — bottom-center
            var hintGo    = BuildHintBanner(canvasObj.transform, font);
            // Info panel  — right edge (starts HIDDEN)
            var infoPanelGo = BuildInfoPanel(canvasObj.transform, font);

            // Divider line — thin horizontal bar to visually separate rows
            BuildDividerLine(canvasObj.transform);

            // ─────────────────────────────────────────────────────
            // GESTURE CONTROLLER
            // ─────────────────────────────────────────────────────
            var controllerObj  = new GameObject("HandGestureController");
            var controller     = controllerObj.AddComponent<GestureController>();
            controller.infoPanel = infoPanelGo;

            // Pre-add the ActionMapper so GestureController.EnsureInitialized
            // finds it via GetComponent instead of creating a second one
            var actionMapper = controllerObj.AddComponent<GestureActionMapper>();
            actionMapper.infoPanel     = infoPanelGo;
            actionMapper.hintBanner    = hintGo;
            actionMapper.scoreText     = scoreGo.GetComponentInChildren<Text>();
            actionMapper.statusBarText = statusGo.GetComponentInChildren<Text>();

            // ─────────────────────────────────────────────────────
            // SAVE
            // ─────────────────────────────────────────────────────
            EditorSceneManager.SaveScene(scene, scenePath);

            var original = EditorBuildSettings.scenes;
            bool found   = false;
            foreach (var s in original) if (s.path == scenePath) { found = true; break; }
            if (!found)
            {
                var updated = new EditorBuildSettingsScene[original.Length + 1];
                System.Array.Copy(original, updated, original.Length);
                updated[original.Length] = new EditorBuildSettingsScene(scenePath, true);
                EditorBuildSettings.scenes = updated;
            }

            Debug.Log("[Adaptense] Phase 6 Lab Scene (no-block) built at " + scenePath);
        }

        // =====================================================
        // Helpers
        // =====================================================

        private static GameObject MakePrimitive(string name, string shape,
            Vector3 pos, Vector3 scale, Color color)
        {
            PrimitiveType pt = shape == "Sphere"   ? PrimitiveType.Sphere
                             : shape == "Cylinder" ? PrimitiveType.Cylinder
                                                   : PrimitiveType.Cube;
            var go = GameObject.CreatePrimitive(pt);
            go.name = name;
            go.transform.position   = pos;
            go.transform.localScale = scale;
            var mat   = new Material(Shader.Find("Standard")) { color = color };
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static Vector3 ObjectScale(string shape)
        {
            return shape == "Cylinder" ? new Vector3(0.14f, 0.18f, 0.14f)
                 : shape == "Sphere"   ? new Vector3(0.18f, 0.18f, 0.18f)
                                       : new Vector3(0.20f, 0.10f, 0.28f);
        }

        // ---- UI builders ----

        private static void BuildZoneLabels(Transform canvas, Font font,
            (string name, string shape, Color color, float x, float y, float z)[] defs)
        {
            // "PLACE HERE ▼" header above the drop zone row
            var header = MakeLabel(canvas, "DropZoneHeader",
                "▼  PLACE HERE  ▼",
                new Vector2(0, 0.75f), new Vector2(0.5f, 1f),
                new Vector2(0, 68f), new Vector2(640f, 38f),
                14, new Color(0.3f, 0.9f, 1f), font);
        }

        private static void BuildObjectLabels(Transform canvas, Font font,
            (string name, string shape, Color color, float x, float y, float z)[] defs)
        {
            // "PICK UP ▲" footer below the objects row
            var footer = MakeLabel(canvas, "ObjectRowHeader",
                "▲  PICK UP  ▲",
                new Vector2(0, 0.2f), new Vector2(0.5f, 0f),
                new Vector2(0, -30f), new Vector2(500f, 34f),
                14, new Color(0.9f, 0.9f, 0.5f), font);
        }

        private static void BuildDividerLine(Transform canvas)
        {
            var go = new GameObject("Divider");
            go.transform.SetParent(canvas, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.5f, 0.8f, 1f, 0.25f);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin        = new Vector2(0.1f, 0.5f);
            r.anchorMax        = new Vector2(0.9f, 0.5f);
            r.pivot            = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(0, 0);
            r.sizeDelta        = new Vector2(0, 2f);
        }

        private static GameObject MakeLabel(Transform canvas, string objName,
            string text, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 sizeDelta,
            int fontSize, Color textColor, Font font)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(canvas, false);
            var img  = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.45f);
            var r    = go.GetComponent<RectTransform>();
            r.anchorMin        = anchorMin;
            r.anchorMax        = anchorMax;
            r.pivot            = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = anchoredPos;
            r.sizeDelta        = sizeDelta;

            var tGo  = new GameObject("Text");
            tGo.transform.SetParent(go.transform, false);
            var t    = tGo.AddComponent<Text>();
            t.font      = font;
            t.fontSize  = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color     = textColor;
            t.text      = text;
            var tr   = tGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(8, 4);
            tr.offsetMax = new Vector2(-8, -4);
            return go;
        }

        private static GameObject BuildScoreCounter(Transform canvas, Font font)
        {
            var go = new GameObject("ScoreCounter");
            go.transform.SetParent(canvas, false);
            go.AddComponent<Image>().color = new Color(0.04f, 0.07f, 0.13f, 0.88f);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(0f, 1f);
            r.pivot     = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(16f, -16f);
            r.sizeDelta = new Vector2(180f, 50f);

            var tGo = new GameObject("ScoreText");
            tGo.transform.SetParent(go.transform, false);
            var t   = tGo.AddComponent<Text>();
            t.font      = font; t.fontSize = 20; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
            t.text      = "Placed: 0 / 6";
            var tr  = tGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(8, 5); tr.offsetMax = new Vector2(-8, -5);
            return go;
        }

        private static GameObject BuildStatusBar(Transform canvas, Font font)
        {
            var go = new GameObject("StatusBar");
            go.transform.SetParent(canvas, false);
            go.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.11f, 0.88f);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 1f);
            r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot     = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -16f);
            r.sizeDelta = new Vector2(620f, 46f);

            var tGo = new GameObject("StatusText");
            tGo.transform.SetParent(go.transform, false);
            var t   = tGo.AddComponent<Text>();
            t.font = font; t.fontSize = 16;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.85f, 0.95f, 1f);
            t.text  = "Point to select an object";
            var tr  = tGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(12, 4); tr.offsetMax = new Vector2(-12, -4);
            return go;
        }

        private static GameObject BuildHintBanner(Transform canvas, Font font)
        {
            var go = new GameObject("HintBanner");
            go.transform.SetParent(canvas, false);
            go.AddComponent<Image>().color = new Color(0.04f, 0.07f, 0.13f, 0.93f);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 0f);
            r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot     = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(0f, 26f);
            r.sizeDelta = new Vector2(660f, 52f);

            var tGo = new GameObject("HintText");
            tGo.transform.SetParent(go.transform, false);
            var t   = tGo.AddComponent<Text>();
            t.font = font; t.fontSize = 16;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.4f, 0.92f, 1f);
            t.text  = "Hint will appear here.";
            var tr  = tGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(12, 5); tr.offsetMax = new Vector2(-12, -5);

            go.SetActive(false);
            return go;
        }

        private static GameObject BuildInfoPanel(Transform canvas, Font font)
        {
            var go = new GameObject("InfoPanel");
            go.transform.SetParent(canvas, false);
            go.AddComponent<Image>().color = new Color(0.06f, 0.10f, 0.17f, 0.92f);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(1f, 0.5f);
            r.anchorMax = new Vector2(1f, 0.5f);
            r.pivot     = new Vector2(1f, 0.5f);
            r.anchoredPosition = new Vector2(-16f, 0f);
            r.sizeDelta = new Vector2(300f, 380f);

            var tGo = new GameObject("InfoText");
            tGo.transform.SetParent(go.transform, false);
            var t   = tGo.AddComponent<Text>();
            t.font = font; t.fontSize = 16;
            t.alignment   = TextAnchor.UpperLeft;
            t.lineSpacing = 1.3f;
            t.color = Color.white;
            t.text  = "<b>LAB INTERACTION</b>\nNo object selected";
            var tr  = tGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(14, 14); tr.offsetMax = new Vector2(-14, -14);

            go.SetActive(false); // ALWAYS starts hidden
            return go;
        }
    }
}
