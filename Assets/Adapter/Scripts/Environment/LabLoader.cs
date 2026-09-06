using UnityEngine;
using UnityEngine.SceneManagement;
using Adapter.Environment;
using Adapter.Gesture;

namespace Adapter
{
    public class LabLoader : MonoBehaviour
    {
        public GestureRecognizer recognizer;

        private void Start()
        {
            if (Application.isPlaying)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.LoadScene("Lab", LoadSceneMode.Additive);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Lab")
            {
                // Wire up the Phase 2 Recognizer to the Phase 3 Controller
                GestureController controller = FindObjectOfType<GestureController>();
                if (controller != null && recognizer != null)
                {
                    controller.recognizer = recognizer;
                    Debug.Log("[LabLoader] Successfully wired GestureRecognizer to Lab's GestureController.");
                }
                else
                {
                    Debug.LogWarning("[LabLoader] Could not find GestureController in Lab scene or recognizer is missing.");
                }
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}
