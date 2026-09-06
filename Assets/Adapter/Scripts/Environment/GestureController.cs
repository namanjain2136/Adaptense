// ============================================================
// ADAPTER Project — Pipeline Controller
// GestureController.cs
//
// PURPOSE:
//   Orchestrates the complete ADAPTER pipeline:
//   Phase 2 (GestureRecognizer) 
//     ──> Phase 5 (ContextAwareDecisionEngine + ContextTracker)
//     ──> Phase 4 (GestureActionMapper)
// ============================================================

using UnityEngine;
using System.Collections.Generic;
using Adapter.Gesture;
using Adapter.Adaptation;
using Adapter.Context;
using Adapter.Learning;

namespace Adapter.Environment
{
    public class GestureController : MonoBehaviour
    {
        [Header("Phase 2 Reference")]
        [Tooltip("Reference to the Phase 2 GestureRecognizer.")]
        [SerializeField] private GestureRecognizer _recognizer;

        [Header("UI Reference")]
        [Tooltip("The UI canvas/panel to show info and adaptive hints on.")]
        public GameObject infoPanel;

        private ContextTracker _tracker;
        private ContextAwareDecisionEngine _decisionEngine;
        private GestureActionMapper _actionMapper;
        private bool _isSubscribed = false;

        public GestureRecognizer recognizer
        {
            get => _recognizer;
            set
            {
                if (_recognizer != value)
                {
                    Unsubscribe();
                    _recognizer = value;
                    EnsureInitialized();
                    Subscribe();
                }
            }
        }

        public ContextAwareDecisionEngine DecisionEngine => _decisionEngine;
        public ContextTracker ContextTracker => _tracker;
        public GestureActionMapper ActionMapper => _actionMapper;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            EnsureInitialized();
            Subscribe();

            if (infoPanel != null)
            {
                infoPanel.SetActive(false); // Start hidden
            }
        }

        private void EnsureInitialized()
        {
            // 1. Attach and wire Phase 5 Context Tracking
            if (_tracker == null)
            {
                _tracker = gameObject.GetComponent<ContextTracker>();
                if (_tracker == null) _tracker = gameObject.AddComponent<ContextTracker>();
            }

            // 2. Attach and wire Phase 5 Decision Engine
            if (_decisionEngine == null)
            {
                _decisionEngine = gameObject.GetComponent<ContextAwareDecisionEngine>();
                if (_decisionEngine == null) _decisionEngine = gameObject.AddComponent<ContextAwareDecisionEngine>();
            }

            // 3. Attach and wire Phase 4 Action Mapper
            if (_actionMapper == null)
            {
                _actionMapper = gameObject.GetComponent<GestureActionMapper>();
                if (_actionMapper == null) _actionMapper = gameObject.AddComponent<GestureActionMapper>();
            }

            if (_actionMapper != null)
            {
                _actionMapper.infoPanel = infoPanel;
                _actionMapper.DecisionEngine = _decisionEngine;
            }
        }

        private void Subscribe()
        {
            if (_recognizer != null && _actionMapper != null && !_isSubscribed)
            {
                _recognizer.OnGesturesEvaluated += _actionMapper.HandleGestures;
                _isSubscribed = true;
                Debug.Log("[GestureController] Fully wired pipeline: GestureRecognizer -> DecisionEngine -> GestureActionMapper.");
            }
        }

        private void Unsubscribe()
        {
            if (_recognizer != null && _actionMapper != null && _isSubscribed)
            {
                _recognizer.OnGesturesEvaluated -= _actionMapper.HandleGestures;
                _isSubscribed = false;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
