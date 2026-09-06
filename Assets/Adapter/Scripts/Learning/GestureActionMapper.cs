// ============================================================
// ADAPTER Project — Phase 4 + Phase 5 Integration
// GestureActionMapper.cs
//
// PURPOSE:
//   Maps recognized gestures to simple scene actions under the
//   governance of the Phase 5 ContextAwareDecisionEngine.
//   Includes side-docked UI formatting and a 2.5s persistent hint banner.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Adapter.Gesture;
using Adapter.Environment;
using Adapter.Adaptation;
using Adapter.Context;

namespace Adapter.Learning
{
    public class GestureActionMapper : MonoBehaviour
    {
        private List<InteractableObject> _interactables = new List<InteractableObject>();
        private int _selectedIndex = -1;
        private bool _isGrabbed = false;

        [Header("UI References")]
        [Tooltip("Side info panel for object inspection and task state.")]
        public GameObject infoPanel;

        [Tooltip("Optional bottom banner dedicated to visual hints.")]
        public GameObject hintBanner;

        [Header("Decision Engine Reference")]
        [SerializeField] private ContextAwareDecisionEngine _decisionEngine;

        [Header("Pacing & Timing")]
        [Tooltip("Cooldown between gesture actions (seconds) to prevent rapid triggering.")]
        public float gestureCooldown = 1.2f;
        private float _lastGestureTime = 0f;

        [Tooltip("How long a visual hint or adaptation message stays on screen.")]
        public float hintDisplayDuration = 2.5f;
        private Coroutine _activeHintCoroutine;

        public ContextAwareDecisionEngine DecisionEngine
        {
            get => _decisionEngine;
            set => _decisionEngine = value;
        }

        private void Start()
        {
            _interactables = new List<InteractableObject>(FindObjectsOfType<InteractableObject>());

            if (_decisionEngine == null)
            {
                _decisionEngine = GetComponent<ContextAwareDecisionEngine>();
                if (_decisionEngine == null)
                {
                    _decisionEngine = gameObject.AddComponent<ContextAwareDecisionEngine>();
                }
            }

            // Adjust info panel position to side-docked if not already set
            SetupSideDockedUI();
        }

        private void SetupSideDockedUI()
        {
            if (infoPanel != null)
            {
                RectTransform rect = infoPanel.GetComponent<RectTransform>();
                if (rect != null)
                {
                    // Dock to Right Side
                    rect.anchorMin = new Vector2(1f, 0.5f);
                    rect.anchorMax = new Vector2(1f, 0.5f);
                    rect.pivot = new Vector2(1f, 0.5f);
                    rect.anchoredPosition = new Vector2(-25, 0);
                    rect.sizeDelta = new Vector2(340, 360);
                }

                // If no separate hint banner exists, dynamically create a clean bottom hint banner
                if (hintBanner == null && infoPanel.transform.parent != null)
                {
                    Transform canvasTransform = infoPanel.transform.parent;
                    Transform existingHint = canvasTransform.Find("HintBanner");
                    if (existingHint != null)
                    {
                        hintBanner = existingHint.gameObject;
                    }
                    else
                    {
                        GameObject bannerObj = new GameObject("HintBanner");
                        bannerObj.transform.SetParent(canvasTransform, false);
                        Image bannerBg = bannerObj.AddComponent<Image>();
                        bannerBg.color = new Color(0.06f, 0.10f, 0.16f, 0.92f);

                        RectTransform bannerRect = bannerObj.GetComponent<RectTransform>();
                        bannerRect.anchorMin = new Vector2(0.5f, 0f);
                        bannerRect.anchorMax = new Vector2(0.5f, 0f);
                        bannerRect.pivot = new Vector2(0.5f, 0f);
                        bannerRect.anchoredPosition = new Vector2(0, 30);
                        bannerRect.sizeDelta = new Vector2(620, 60);

                        GameObject textObj = new GameObject("HintText");
                        textObj.transform.SetParent(bannerObj.transform, false);
                        Text text = textObj.AddComponent<Text>();
                        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                        text.fontSize = 17;
                        text.alignment = TextAnchor.MiddleCenter;
                        text.color = new Color(0.4f, 0.9f, 1f);

                        RectTransform textRect = textObj.GetComponent<RectTransform>();
                        textRect.anchorMin = Vector2.zero;
                        textRect.anchorMax = Vector2.one;
                        textRect.offsetMin = new Vector2(15, 6);
                        textRect.offsetMax = new Vector2(-15, -6);

                        bannerObj.SetActive(false);
                        hintBanner = bannerObj;
                    }
                }
            }
        }

        /// <summary>
        /// Entry point: receives evaluated gestures from Phase 2, passes them through
        /// the Phase 5 Decision Engine, and executes actions or shows visual hints.
        /// </summary>
        public void HandleGestures(List<DetectedGesture> gestures)
        {
            if (Time.time - _lastGestureTime < gestureCooldown) return;

            if (_decisionEngine == null)
            {
                _decisionEngine = GetComponent<ContextAwareDecisionEngine>();
                if (_decisionEngine == null) return;
            }

            // Phase 5 Context-Aware Evaluation
            AdaptationResult adaptation = _decisionEngine.ProcessGestures(gestures);

            if (adaptation.ShouldExecuteAction)
            {
                ExecuteAction(adaptation.CandidateGesture, adaptation);
                _decisionEngine.Tracker?.RecordSuccess(adaptation.CandidateGesture);
                _lastGestureTime = Time.time;
            }
            else
            {
                // Show hint or alternative gesture guidance on UI
                if (adaptation.Decision == DecisionType.ShowVisualHint || adaptation.Decision == DecisionType.OfferAlternativeGesture)
                {
                    ShowHintUI(adaptation.HintMessage, adaptation.Decision == DecisionType.OfferAlternativeGesture);
                    _decisionEngine.Tracker?.RecordFailure(adaptation.CandidateGesture);
                    _lastGestureTime = Time.time;
                }
            }
        }

        private void ExecuteAction(GestureType gesture, AdaptationResult adaptation)
        {
            switch (gesture)
            {
                case GestureType.Point:
                    SelectNextObject();
                    break;
                case GestureType.Pinch:
                    GrabSelectedObject();
                    break;
                case GestureType.OpenPalm:
                    OpenInfoPanel();
                    break;
                case GestureType.Fist:
                    ResetActions();
                    break;
                case GestureType.Swipe:
                    Navigate();
                    break;
            }

            if (adaptation.Decision == DecisionType.MakeInteractionEasier)
            {
                ShowHintUI($"Adaptive mode: Widened tolerance (Threshold {adaptation.AdjustedThreshold:F2})", false);
            }
        }

        private void SelectNextObject()
        {
            if (_interactables.Count == 0)
            {
                _interactables = new List<InteractableObject>(FindObjectsOfType<InteractableObject>());
                if (_interactables.Count == 0) return;
            }

            // Unhover previous if not grabbed
            if (_selectedIndex >= 0 && _selectedIndex < _interactables.Count && !_isGrabbed)
            {
                _interactables[_selectedIndex].OnHoverExit();
            }

            // Only switch selection if we haven't grabbed the current one
            if (!_isGrabbed)
            {
                _selectedIndex = (_selectedIndex + 1) % _interactables.Count;
            }

            _interactables[_selectedIndex].OnHoverEnter();
            UpdateSidePanelText();
            Debug.Log($"[ActionMapper] Point -> Selected {_interactables[_selectedIndex].objectName}");
        }

        private void GrabSelectedObject()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _interactables.Count)
            {
                _interactables[_selectedIndex].OnGrab();
                _isGrabbed = true;
                UpdateSidePanelText();
                Debug.Log($"[ActionMapper] Pinch -> Grabbed {_interactables[_selectedIndex].objectName}");
            }
        }

        private void OpenInfoPanel()
        {
            if (infoPanel != null)
            {
                infoPanel.SetActive(!infoPanel.activeSelf);
                UpdateSidePanelText();
                Debug.Log($"[ActionMapper] Open Palm -> Toggled Info Panel ({infoPanel.activeSelf})");
            }
        }

        private void UpdateSidePanelText()
        {
            if (infoPanel == null || !infoPanel.activeSelf) return;

            var textUI = infoPanel.GetComponentInChildren<Text>();
            if (textUI != null)
            {
                string selectedName = (_selectedIndex >= 0 && _selectedIndex < _interactables.Count)
                    ? _interactables[_selectedIndex].objectName
                    : "None";

                string taskName = _decisionEngine?.Tracker != null ? _decisionEngine.Tracker.CurrentTask.ToString() : "ExploreLab";
                int progress = _decisionEngine?.Tracker != null ? _decisionEngine.Tracker.CompletedTasksCount : 0;
                string distStr = _decisionEngine?.Tracker != null ? _decisionEngine.Tracker.GetSnapshot(GestureType.Point, 0).Distance.ToString() : "Optimal";

                textUI.text = $"<size=20><b>LAB INTERACTION</b></size>\n" +
                              $"<color=#88ccff>────────────────────</color>\n" +
                              $"<b>Target Object:</b> {selectedName}\n" +
                              $"<b>State:</b> {(_isGrabbed ? "<color=yellow>GRABBED</color>" : "<color=#88ff88>SELECTED</color>")}\n" +
                              $"<b>Activity:</b> {taskName}\n" +
                              $"<b>Progress:</b> {progress} actions completed\n" +
                              $"<b>Distance:</b> {distStr}\n\n" +
                              $"<size=14><color=#aaaaaa><b>Gesture Legend:</b>\n" +
                              $"• Point: Cycle Objects\n" +
                              $"• Pinch: Grab Object\n" +
                              $"• Open Palm: Toggle Panel\n" +
                              $"• Fist: Reset/Release\n" +
                              $"• Swipe: Next Task</color></size>";
            }
        }

        private void ShowHintUI(string hintMessage, bool isAlternative)
        {
            GameObject target = hintBanner != null ? hintBanner : infoPanel;
            if (target != null && !string.IsNullOrEmpty(hintMessage))
            {
                if (_activeHintCoroutine != null)
                {
                    StopCoroutine(_activeHintCoroutine);
                }
                _activeHintCoroutine = StartCoroutine(DisplayHintBanner(target, hintMessage, isAlternative));
            }
        }

        private IEnumerator DisplayHintBanner(GameObject banner, string message, bool isAlternative)
        {
            banner.SetActive(true);
            Text textUI = banner.GetComponentInChildren<Text>();
            if (textUI != null)
            {
                string prefix = isAlternative 
                    ? "<color=#ffdd44><b>Adaptive Fallback:</b></color> " 
                    : "<color=#55ddff><b>Guidance:</b></color> ";
                textUI.text = $"{prefix}{message}";
            }

            yield return new WaitForSeconds(hintDisplayDuration);

            // Hide hint banner after duration (if it's the dedicated banner)
            if (banner == hintBanner)
            {
                banner.SetActive(false);
            }
            _activeHintCoroutine = null;
        }

        private void ResetActions()
        {
            if (infoPanel != null)
                infoPanel.SetActive(false);

            if (_selectedIndex >= 0 && _selectedIndex < _interactables.Count)
            {
                _interactables[_selectedIndex].OnRelease();
                _interactables[_selectedIndex].OnHoverExit();
            }
            _selectedIndex = -1;
            _isGrabbed = false;
            Debug.Log("[ActionMapper] Fist -> Reset actions");
        }

        private void Navigate()
        {
            Debug.Log("[ActionMapper] Swipe -> Navigating");
            if (_decisionEngine?.Tracker != null)
            {
                _decisionEngine.Tracker.AdvanceToNextTask();
                UpdateSidePanelText();
                ShowHintUI($"Switched to task: {_decisionEngine.Tracker.CurrentTask}", false);
            }
            else
            {
                SelectNextObject();
            }
        }
    }
}
