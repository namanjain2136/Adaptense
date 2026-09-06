using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Adapter.Environment;
using Adapter.Gesture;

namespace Adapter.EditorScripts
{
    public class LabSceneBuilder
    {
        [MenuItem("Adapter/Build Phase 3 Lab Scene")]
        public static void BuildLabScene()
        {
            // Ensure the Adapter/Scenes folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Adapter/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets/Adapter", "Scenes");
            }

            string scenePath = "Assets/Adapter/Scenes/Lab.unity";

            // Create a new empty scene
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 1. Remove Main Camera (Hand Tracking scene already has one, avoid conflicts)
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                GameObject.DestroyImmediate(mainCam.gameObject);
            }

            // 2. Laboratory placeholder geometry
            // Table
            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Table";
            table.transform.position = new Vector3(0, 0.5f, 0);
            table.transform.localScale = new Vector3(2.4f, 1f, 1.2f);
            Material tableMat = new Material(Shader.Find("Standard"));
            tableMat.color = new Color(0.35f, 0.28f, 0.22f);
            table.GetComponent<Renderer>().sharedMaterial = tableMat;

            // 3. Interactable Objects
            // Beaker
            GameObject beaker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beaker.name = "Beaker";
            beaker.transform.position = new Vector3(-0.6f, 1.25f, 0);
            beaker.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
            InteractableObject beakerInteractable = beaker.AddComponent<InteractableObject>();
            beakerInteractable.objectName = "Beaker";
            Material beakerMat = new Material(Shader.Find("Standard"));
            beakerMat.color = new Color(0.2f, 0.8f, 0.9f, 0.9f);
            beaker.GetComponent<Renderer>().sharedMaterial = beakerMat;

            // Book
            GameObject book = GameObject.CreatePrimitive(PrimitiveType.Cube);
            book.name = "Book";
            book.transform.position = new Vector3(0.6f, 1.05f, 0);
            book.transform.localScale = new Vector3(0.45f, 0.1f, 0.55f);
            InteractableObject bookInteractable = book.AddComponent<InteractableObject>();
            bookInteractable.objectName = "Book";
            Material bookMat = new Material(Shader.Find("Standard"));
            bookMat.color = new Color(0.85f, 0.25f, 0.25f);
            book.GetComponent<Renderer>().sharedMaterial = bookMat;

            // Switch
            GameObject switchObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            switchObj.name = "Switch";
            switchObj.transform.position = new Vector3(0, 1.2f, 0.2f);
            switchObj.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            InteractableObject switchInteractable = switchObj.AddComponent<InteractableObject>();
            switchInteractable.objectName = "Switch";
            Material switchMat = new Material(Shader.Find("Standard"));
            switchMat.color = new Color(0.95f, 0.8f, 0.2f);
            switchObj.GetComponent<Renderer>().sharedMaterial = switchMat;

            // 4. UI Canvas & Layout
            GameObject canvasObj = new GameObject("UI Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            canvasObj.AddComponent<GraphicRaycaster>();

            Font defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 4A. Side Info Panel (Anchored to Right Edge)
            GameObject infoPanel = new GameObject("InfoPanel");
            infoPanel.transform.SetParent(canvasObj.transform, false);
            Image panelBg = infoPanel.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.12f, 0.18f, 0.88f);
            RectTransform panelRect = infoPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 0.5f);
            panelRect.anchorMax = new Vector2(1f, 0.5f);
            panelRect.pivot = new Vector2(1f, 0.5f);
            panelRect.anchoredPosition = new Vector2(-25, 0);
            panelRect.sizeDelta = new Vector2(340, 360);
            infoPanel.SetActive(false); // Hidden initially until OpenPalm or task

            GameObject infoText = new GameObject("InfoText");
            infoText.transform.SetParent(infoPanel.transform, false);
            Text text = infoText.AddComponent<Text>();
            text.text = "<b>Lab Environment</b>\nNo object selected";
            text.font = defaultFont;
            text.fontSize = 18;
            text.alignment = TextAnchor.UpperLeft;
            text.color = Color.white;
            text.lineSpacing = 1.25f;
            RectTransform textRect = infoText.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18, 18);
            textRect.offsetMax = new Vector2(-18, -18);

            // 4B. Bottom Hint Banner (Anchored Bottom Center)
            GameObject hintBanner = new GameObject("HintBanner");
            hintBanner.transform.SetParent(canvasObj.transform, false);
            Image hintBg = hintBanner.AddComponent<Image>();
            hintBg.color = new Color(0.05f, 0.08f, 0.12f, 0.92f);
            RectTransform hintRect = hintBanner.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 0f);
            hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0, 30);
            hintRect.sizeDelta = new Vector2(640, 60);
            hintBanner.SetActive(false);

            GameObject hintTextObj = new GameObject("HintText");
            hintTextObj.transform.SetParent(hintBanner.transform, false);
            Text hintText = hintTextObj.AddComponent<Text>();
            hintText.text = "Visual guidance will appear here.";
            hintText.font = defaultFont;
            hintText.fontSize = 17;
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.color = new Color(0.4f, 0.9f, 1f);
            RectTransform hintTextRect = hintTextObj.GetComponent<RectTransform>();
            hintTextRect.anchorMin = Vector2.zero;
            hintTextRect.anchorMax = Vector2.one;
            hintTextRect.offsetMin = new Vector2(15, 8);
            hintTextRect.offsetMax = new Vector2(-15, -8);

            // 5. Hand/Gesture Controller
            GameObject gestureControllerObj = new GameObject("HandGestureController");
            GestureController controller = gestureControllerObj.AddComponent<GestureController>();
            controller.infoPanel = infoPanel;

            // Save the scene
            EditorSceneManager.SaveScene(newScene, scenePath);

            // Add to Build Settings
            EditorBuildSettingsScene[] original = EditorBuildSettings.scenes;
            bool found = false;
            foreach (var s in original)
            {
                if (s.path == scenePath) { found = true; break; }
            }
            if (!found)
            {
                EditorBuildSettingsScene[] newSettings = new EditorBuildSettingsScene[original.Length + 1];
                System.Array.Copy(original, newSettings, original.Length);
                newSettings[original.Length] = new EditorBuildSettingsScene(scenePath, true);
                EditorBuildSettings.scenes = newSettings;
            }

            Debug.Log($"[Adapter] Successfully generated Phase 3 Lab Scene with polished side-docked UI at {scenePath}");
        }
    }
}
