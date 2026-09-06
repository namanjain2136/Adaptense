// ============================================================
// ADAPTER Project — Phase 6 (UI Polish & Object Scale Fix)
// LabSceneBuilder.cs
//
// Adapter > Build Phase 6 Lab Scene
//
// Fixes:
//  1. Positioned objects closer to camera (Z=1.4) & scaled 4x larger
//     so they are big, clear 3D items on screen instead of tiny dots.
//  2. Fixed "PLACE HERE" UI text overlap under top status bar.
//  3. Polished UI canvas styling & spacing.
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
            var defaultLight = GameObject.Find("Directional Light");
            if (defaultLight != null) GameObject.DestroyImmediate(defaultLight);

            // ─────────────────────────────────────────────────────
            // 6 LARGE INTERACTABLE OBJECTS (closer to camera Z=1.4)
            // ─────────────────────────────────────────────────────
            // X coordinates spread across screen width at Z=1.4
            float[] xCoords = new float[] { -1.35f, -0.81f, -0.27f, 0.27f, 0.81f, 1.35f };
            float objY = -0.55f;
            float dzY  = 0.45f;
            float zPos = 1.4f;

            var defs = new (string name, string shape, Color color)[]
            {
                ("Beaker",     "Cylinder", new Color(0.20f, 0.80f, 0.95f)),
                ("Book",       "Cube",     new Color(0.88f, 0.25f, 0.25f)),
                ("Flask",      "Sphere",   new Color(0.25f, 0.85f, 0.40f)),
                ("Battery",    "Cube",     new Color(0.95f, 0.80f, 0.15f)),
                ("Lens",       "Cylinder", new Color(0.75f, 0.35f, 0.95f)),
                ("SampleTube", "Cylinder", new Color(1.0f,  0.55f, 0.15f)),
            };

            GameObject[] objects   = new GameObject[defs.Length];
            GameObject[] dropZones = new GameObject[defs.Length];

            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                float x = xCoords[i];

                // --- Very prominent source object ---
                Vector3 objScale = ProminentObjectScale(d.shape);
                var obj = MakePrimitive(d.name, d.shape,
                    new Vector3(x, objY, zPos), objScale, d.color);
                var interactable = obj.AddComponent<InteractableObject>();
                interactable.objectName = d.name;
                objects[i] = obj;

                // --- Drop zone (prominent target marker) ---
                var dz = MakePrimitive($"DropZone_{d.name}", "Cylinder",
                    new Vector3(x, dzY, zPos),
                    new Vector3(0.38f, 0.02f, 0.38f),
                    new Color(0.2f, 0.9f, 1f, 0.38f));
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

            // Row headers (placed cleanly without cut-offs)
            BuildZoneLabels(canvasObj.transform, font);
            BuildObjectLabels(canvasObj.transform, font);

            // Score counter — top-left
            var scoreGo   = BuildScoreCounter(canvasObj.transform, font);
            // Status bar  — top-center
            var statusGo  = BuildStatusBar(canvasObj.transform, font);
            // Hint banner — bottom-center
            var hintGo    = BuildHintBanner(canvasObj.transform, font);
            // Info panel  — right edge (starts HIDDEN)
            var infoPanelGo = BuildInfoPanel(canvasObj.transform, font);

            // Divider line
            BuildDividerLine(canvasObj.transform);

            // ─────────────────────────────────────────────────────
            // GESTURE CONTROLLER
            // ─────────────────────────────────────────────────────
            var controllerObj = new GameObject("HandGestureController");
            var controller    = controllerObj.AddComponent<GestureController>();
            controller.infoPanel = infoPanelGo;

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

            Debug.Log("[Adaptense] Phase 6 Lab Scene (Prominent UI & Objects) built at " + scenePath);
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

        private static Vector3 ProminentObjectScale(string shape)
        {
            // Significantly larger so items are easily visible at Z=1.4
            return shape == "Cylinder" ? new Vector3(0.28f, 0.38f, 0.28f)
                 : shape == "Sphere"   ? new Vector3(0.35f, 0.35f, 0.35f)
                                       : new Vector3(0.36f, 0.22f, 0.44f);
        }

        // ---- UI builders ----

        private static void BuildZoneLabels(Transform canvas, Font font)
        {
            // Positioned cleanly below the status bar (y = -72px from top)
            MakeLabel(canvas, "DropZoneHeader",
                "▼  DESTINATION DROP ZONES  ▼",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -78f), new Vector2(460f, 32f),
                14, new Color(0.35f, 0.92f, 1f), font);
        }

        private static void BuildObjectLabels(Transform canvas, Font font)
        {
            // Positioned cleanly near bottom above camera controls (y = 82px from bottom)
            MakeLabel(canvas, "ObjectRowHeader",
                "▲  SOURCE OBJECTS (POINT TO SELECT)  ▲",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 82f), new Vector2(480f, 32f),
                14, new Color(0.98f, 0.88f, 0.35f), font);
        }

        private static void BuildDividerLine(Transform canvas)
        {
            var go = new GameObject("Divider");
            go.transform.SetParent(canvas, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.5f, 0.8f, 1f, 0.22f);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin        = new Vector2(0.08f, 0.48f);
            r.anchorMax        = new Vector2(0.92f, 0.48f);
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
            img.color = new Color(0.04f, 0.07f, 0.13f, 0.85f);
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
            go.AddComponent<Image>().color = new Color(0.04f, 0.07f, 0.13f, 0.90f);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(0f, 1f);
            r.pivot     = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(16f, -16f);
            r.sizeDelta = new Vector2(170f, 44f);

            var tGo = new GameObject("ScoreText");
            tGo.transform.SetParent(go.transform, false);
            var t   = tGo.AddComponent<Text>();
            t.font      = font; t.fontSize = 18; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
            t.text      = "Placed: 0 / 6";
            var tr  = tGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(6, 4); tr.offsetMax = new Vector2(-6, -4);
            return go;
        }

        private static GameObject BuildStatusBar(Transform canvas, Font font)
        {
            var go = new GameObject("StatusBar");
            go.transform.SetParent(canvas, false);
            go.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.11f, 0.92f);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 1f);
            r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot     = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -16f);
            r.sizeDelta = new Vector2(640f, 44f);

            var tGo = new GameObject("StatusText");
            tGo.transform.SetParent(go.transform, false);
            var t   = tGo.AddComponent<Text>();
            t.font = font; t.fontSize = 16;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.85f, 0.95f, 1f);
            t.text  = "Point to select an object  |  Fist to move  |  Pinch to reset";
            var tr  = tGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(10, 4); tr.offsetMax = new Vector2(-10, -4);
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
            r.sizeDelta = new Vector2(660f, 50f);

            var tGo = new GameObject("HintText");
            tGo.transform.SetParent(go.transform, false);
            var t   = tGo.AddComponent<Text>();
            t.font = font; t.fontSize = 16;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.4f, 0.92f, 1f);
            t.text  = "Hint will appear here.";
            var tr  = tGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(12, 4); tr.offsetMax = new Vector2(-12, -4);

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
