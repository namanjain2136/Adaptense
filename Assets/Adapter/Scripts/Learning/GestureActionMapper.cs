// ============================================================
// ADAPTER Project — Phase 6
// GestureActionMapper.cs
//
// PURPOSE:
//   Maps recognized gestures to pick-and-place actions under
//   the governance of the Phase 5 ContextAwareDecisionEngine.
//
//   Gesture flow:
//     Point      -> Cycle selection (highlight + floating label)
//     Pinch (1)  -> Pick up selected object
//     Pinch (2)  -> Place picked-up object at drop zone
//     Fist       -> Cancel / return object to origin
//     Open Palm  -> Toggle side info panel
//     Swipe      -> Advance learning task
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
        // ---- object list ----
        private List<InteractableObject> _interactables = new List<InteractableObject>();
        private int  _selectedIndex = -1;
        private bool _isPickedUp    = false;

        [Header("UI References")]
        public GameObject infoPanel;
        public GameObject hintBanner;

        // Injected at runtime for the score counter
        [HideInInspector] public Text scoreText;
        [HideInInspector] public Text statusBarText;

        [Header("Decision Engine")]
        [SerializeField] private ContextAwareDecisionEngine _decisionEngine;

        [Header("Timing")]
        public float gestureCooldown    = 1.2f;
        public float hintDisplayDuration = 2.5f;

        private float     _lastGestureTime    = 0f;
        private Coroutine _activeHintCoroutine;
        private int       _placedCount         = 0;

        public ContextAwareDecisionEngine DecisionEngine
        {
            get => _decisionEngine;
            set => _decisionEngine = value;
        }

        // =====================================================
        // Lifecycle
        // =====================================================

        private void Start()
        {
            _interactables = new List<InteractableObject>(FindObjectsOfType<InteractableObject>());

            if (_decisionEngine == null)
            {
                _decisionEngine = GetComponent<ContextAwareDecisionEngine>()
                                  ?? gameObject.AddComponent<ContextAwareDecisionEngine>();
            }

            SetupSideDockedUI();
            UpdateScoreCounter();
            UpdateStatusBar("Point to select an object");
        }

        // =====================================================
        // Entry point — called by GestureController pipeline
        // =====================================================

        public void HandleGestures(List<DetectedGesture> gestures)
        {
            if (Time.time - _lastGestureTime < gestureCooldown) return;

            if (_decisionEngine == null)
            {
                _decisionEngine = GetComponent<ContextAwareDecisionEngine>();
                if (_decisionEngine == null) return;
            }

            AdaptationResult adaptation = _decisionEngine.ProcessGestures(gestures);

            if (adaptation.ShouldExecuteAction)
            {
                ExecuteAction(adaptation.CandidateGesture, adaptation);
                _decisionEngine.Tracker?.RecordSuccess(adaptation.CandidateGesture);
                _lastGestureTime = Time.time;
            }
            else if (adaptation.Decision == DecisionType.ShowVisualHint
                  || adaptation.Decision == DecisionType.OfferAlternativeGesture)
            {
                ShowHintUI(adaptation.HintMessage, adaptation.Decision == DecisionType.OfferAlternativeGesture);
                _decisionEngine.Tracker?.RecordFailure(adaptation.CandidateGesture);
                _lastGestureTime = Time.time;
            }
        }

        // =====================================================
        // Core action dispatcher
        // =====================================================

        private void ExecuteAction(GestureType gesture, AdaptationResult adaptation)
        {
            switch (gesture)
            {
                case GestureType.Point:      SelectNextObject();  break;
                case GestureType.Pinch:      TogglePinchAction(); break;
                case GestureType.OpenPalm:   OpenInfoPanel();     break;
                case GestureType.Fist:       CancelOrReset();     break;
                case GestureType.Swipe:      Navigate();          break;
            }

            if (adaptation.Decision == DecisionType.MakeInteractionEasier)
                ShowHintUI($"Adaptive mode: Threshold {adaptation.AdjustedThreshold:F2}", false);

            UpdateSidePanelText();
        }

        // =====================================================
        // Gesture actions
        // =====================================================

        private void SelectNextObject()
        {
            if (_isPickedUp) return; // don't cycle while holding something

            RefreshInteractables();
            if (_interactables.Count == 0) return;

            // Unhighlight previous
            if (_selectedIndex >= 0 && _selectedIndex < _interactables.Count)
                _interactables[_selectedIndex].OnHoverExit();

            // Skip already-placed objects
            int tries = 0;
            do
            {
                _selectedIndex = (_selectedIndex + 1) % _interactables.Count;
                tries++;
            }
            while (_interactables[_selectedIndex].IsPlaced && tries < _interactables.Count);

            if (_interactables[_selectedIndex].IsPlaced)
            {
                UpdateStatusBar("All objects placed! Use Fist to reset.");
                return;
            }

            _interactables[_selectedIndex].OnHoverEnter();
            UpdateStatusBar($"Selected: {_interactables[_selectedIndex].objectName}  |  Pinch to pick up");
            Debug.Log($"[ActionMapper] Point -> Selected: {_interactables[_selectedIndex].objectName}");
        }

        private void TogglePinchAction()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _interactables.Count) return;
            var obj = _interactables[_selectedIndex];

            if (!_isPickedUp)
            {
                // First pinch — pick up
                obj.OnPickUp();
                _isPickedUp = true;
                UpdateStatusBar($"Holding: {obj.objectName}  |  Pinch to place  |  Fist to cancel");
                ShowHintUI($"Ghost shows destination. Pinch again to place, or Fist to cancel.", false);
                Debug.Log($"[ActionMapper] Pinch -> Picked up: {obj.objectName}");
            }
            else
            {
                // Second pinch — place
                obj.OnPlace();
                _isPickedUp = false;
                _placedCount++;
                UpdateScoreCounter();

                int total = _interactables.Count;
                UpdateStatusBar($"Placed! {_placedCount}/{total} objects placed. Point to select next.");
                ShowHintUI($"{obj.objectName} placed successfully!", false);
                _decisionEngine?.Tracker?.AdvanceToNextTask();

                // Auto-deselect
                _selectedIndex = -1;
                Debug.Log($"[ActionMapper] Pinch -> Placed: {obj.objectName}");

                if (_placedCount >= total)
                    ShowHintUI("All objects placed! Lab complete! Use Fist to reset.", false);
            }
        }

        private void CancelOrReset()
        {
            if (_isPickedUp && _selectedIndex >= 0 && _selectedIndex < _interactables.Count)
            {
                // Cancel current pick-up
                _interactables[_selectedIndex].OnReturn();
                _isPickedUp = false;
                UpdateStatusBar("Cancelled. Point to select an object.");
                ShowHintUI("Object returned to its spot.", false);
                Debug.Log($"[ActionMapper] Fist -> Returned: {_interactables[_selectedIndex].objectName}");
            }
            else
            {
                // Hard reset — return all objects
                foreach (var obj in _interactables)
                    obj.OnReset();
                _selectedIndex = -1;
                _isPickedUp    = false;
                _placedCount   = 0;
                UpdateScoreCounter();
                if (infoPanel != null) infoPanel.SetActive(false);
                UpdateStatusBar("Reset! Point to select an object.");
                Debug.Log("[ActionMapper] Fist -> Full reset");
            }
        }

        private void OpenInfoPanel()
        {
            if (infoPanel != null)
            {
                infoPanel.SetActive(!infoPanel.activeSelf);
                UpdateSidePanelText();
                Debug.Log($"[ActionMapper] Open Palm -> Info Panel {(infoPanel.activeSelf ? "shown" : "hidden")}");
            }
        }

        private void Navigate()
        {
            _decisionEngine?.Tracker?.AdvanceToNextTask();
            string task = _decisionEngine?.Tracker?.CurrentTask.ToString() ?? "Unknown";
            UpdateStatusBar($"Task: {task}");
            ShowHintUI($"Switched to task: {task}", false);
            Debug.Log($"[ActionMapper] Swipe -> Task: {task}");
        }

        // =====================================================
        // UI helpers
        // =====================================================

        private void SetupSideDockedUI()
        {
            if (infoPanel == null) return;

            RectTransform rect = infoPanel.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin       = new Vector2(1f, 0.5f);
                rect.anchorMax       = new Vector2(1f, 0.5f);
                rect.pivot           = new Vector2(1f, 0.5f);
                rect.anchoredPosition = new Vector2(-20f, 0f);
                rect.sizeDelta       = new Vector2(320f, 400f);
            }

            // Create hint banner if missing
            if (hintBanner == null && infoPanel.transform.parent != null)
            {
                Transform canvas = infoPanel.transform.parent;
                Transform existing = canvas.Find("HintBanner");
                if (existing != null)
                {
                    hintBanner = existing.gameObject;
                }
                else
                {
                    hintBanner = BuildHintBanner(canvas);
                }
            }
        }

        private void UpdateSidePanelText()
        {
            if (infoPanel == null || !infoPanel.activeSelf) return;
            var textUI = infoPanel.GetComponentInChildren<Text>();
            if (textUI == null) return;

            string selName = (_selectedIndex >= 0 && _selectedIndex < _interactables.Count)
                ? _interactables[_selectedIndex].objectName : "None";
            string state   = _isPickedUp
                ? "<color=yellow>HOLDING — Pinch to place | Fist to cancel</color>"
                : (_selectedIndex >= 0 ? "<color=#88ff88>SELECTED — Pinch to pick up</color>" : "<color=#aaaaaa>None selected</color>");
            string task    = _decisionEngine?.Tracker?.CurrentTask.ToString() ?? "ExploreLab";
            int    done    = _decisionEngine?.Tracker?.CompletedTasksCount ?? 0;
            int    total   = _interactables.Count;

            textUI.text =
                $"<size=19><b>LAB INTERACTION</b></size>\n" +
                $"<color=#88ccff>────────────────────</color>\n" +
                $"<b>Object:</b> {selName}\n" +
                $"<b>State:</b> {state}\n\n" +
                $"<b>Task:</b> {task}\n" +
                $"<b>Placed:</b> {_placedCount}/{total}\n" +
                $"<b>Actions:</b> {done}\n\n" +
                $"<size=13><color=#aaaaaa><b>Controls:</b>\n" +
                $"• Point  → Select / Cycle\n" +
                $"• Pinch  → Pick up / Place\n" +
                $"• Fist   → Cancel / Reset\n" +
                $"• Palm   → Toggle this panel\n" +
                $"• Swipe  → Next Task</color></size>";
        }

        private void UpdateScoreCounter()
        {
            if (scoreText == null) return;
            int total = _interactables.Count > 0 ? _interactables.Count : 6;
            scoreText.text = $"Placed: {_placedCount} / {total}";
            scoreText.color = _placedCount >= total ? new Color(0.2f, 0.95f, 0.4f) : Color.white;
        }

        private void UpdateStatusBar(string message)
        {
            if (statusBarText != null)
                statusBarText.text = message;
        }

        private void ShowHintUI(string hintMessage, bool isAlternative)
        {
            GameObject target = hintBanner != null ? hintBanner : infoPanel;
            if (target == null || string.IsNullOrEmpty(hintMessage)) return;
            if (_activeHintCoroutine != null) StopCoroutine(_activeHintCoroutine);
            _activeHintCoroutine = StartCoroutine(DisplayHintBanner(target, hintMessage, isAlternative));
        }

        private IEnumerator DisplayHintBanner(GameObject banner, string message, bool isAlternative)
        {
            banner.SetActive(true);
            Text textUI = banner.GetComponentInChildren<Text>();
            if (textUI != null)
            {
                string prefix = isAlternative
                    ? "<color=#ffdd44><b>Suggestion:</b></color> "
                    : "<color=#55ddff><b>Tip:</b></color> ";
                textUI.text = prefix + message;
            }
            yield return new WaitForSeconds(hintDisplayDuration);
            if (banner == hintBanner) banner.SetActive(false);
            _activeHintCoroutine = null;
        }

        private void RefreshInteractables()
        {
            if (_interactables.Count == 0)
                _interactables = new List<InteractableObject>(FindObjectsOfType<InteractableObject>());
        }

        // ---- UI factory ----

        private GameObject BuildHintBanner(Transform canvas)
        {
            GameObject bannerObj = new GameObject("HintBanner");
            bannerObj.transform.SetParent(canvas, false);
            Image bg = bannerObj.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.14f, 0.93f);
            RectTransform r = bannerObj.GetComponent<RectTransform>();
            r.anchorMin       = new Vector2(0.5f, 0f);
            r.anchorMax       = new Vector2(0.5f, 0f);
            r.pivot           = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(0, 28f);
            r.sizeDelta       = new Vector2(680f, 58f);

            GameObject textObj = new GameObject("HintText");
            textObj.transform.SetParent(bannerObj.transform, false);
            Text t = textObj.AddComponent<Text>();
            t.font      = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize  = 17;
            t.alignment = TextAnchor.MiddleCenter;
            t.color     = new Color(0.4f, 0.92f, 1f);
            RectTransform tr = textObj.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(14f, 6f);
            tr.offsetMax = new Vector2(-14f, -6f);

            bannerObj.SetActive(false);
            return bannerObj;
        }
    }
}
