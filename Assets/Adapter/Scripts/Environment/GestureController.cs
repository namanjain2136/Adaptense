// ============================================================
// ADAPTER Project — Pipeline Controller (with PGL Integration)
// GestureController.cs
//
// PURPOSE:
//   Orchestrates the complete ADAPTER pipeline:
//   Phase 2 (GestureRecognizer) 
//     ──> Phase 6 PGL (GesturePGLManager - Gesture Gating)
//     ──> Phase 5 (ContextAwareDecisionEngine + ContextTracker)
//     ──> Phase 4 (GestureActionMapper)
// ============================================================

using UnityEngine;
using System.Collections.Generic;
using Adapter.Gesture;
using Adapter.Adaptation;
using Adapter.Context;
using Adapter.Learning;
using Adapter.Progression;

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
        private GesturePGLManager _pglManager;
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
        public GesturePGLManager PGLManager => _pglManager;

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
            // 1. Attach and wire Phase 6 PGL Manager
            if (_pglManager == null)
            {
                _pglManager = gameObject.GetComponent<GesturePGLManager>();
                if (_pglManager == null) _pglManager = gameObject.AddComponent<GesturePGLManager>();
            }

            // 2. Attach and wire Phase 5 Context Tracking
            if (_tracker == null)
            {
                _tracker = gameObject.GetComponent<ContextTracker>();
                if (_tracker == null) _tracker = gameObject.AddComponent<ContextTracker>();
            }

            // 3. Attach and wire Phase 5 Decision Engine
            if (_decisionEngine == null)
            {
                _decisionEngine = gameObject.GetComponent<ContextAwareDecisionEngine>();
                if (_decisionEngine == null) _decisionEngine = gameObject.AddComponent<ContextAwareDecisionEngine>();
            }

            // 4. Attach and wire Phase 4 Action Mapper
            if (_actionMapper == null)
            {
                _actionMapper = gameObject.GetComponent<GestureActionMapper>();
                if (_actionMapper == null) _actionMapper = gameObject.AddComponent<GestureActionMapper>();
            }

            if (_actionMapper != null)
            {
                _actionMapper.infoPanel = infoPanel;
                _actionMapper.DecisionEngine = _decisionEngine;
                _actionMapper.pglManager = _pglManager;
            }
        }

        private void Subscribe()
        {
            if (_recognizer != null && _actionMapper != null && !_isSubscribed)
            {
                _recognizer.OnGesturesEvaluated += OnRawGesturesReceived;
                _isSubscribed = true;
                Debug.Log("[GestureController] Fully wired pipeline: GestureRecognizer -> GesturePGLManager -> DecisionEngine -> GestureActionMapper.");
            }
        }

        private void Unsubscribe()
        {
            if (_recognizer != null && _actionMapper != null && _isSubscribed)
            {
                _recognizer.OnGesturesEvaluated -= OnRawGesturesReceived;
                _isSubscribed = false;
            }
        }

        private void OnRawGesturesReceived(List<DetectedGesture> rawGestures)
        {
            // Gate gestures through Phase 6 PGL Manager first
            List<DetectedGesture> gatedGestures = rawGestures;
            if (_pglManager != null)
            {
                gatedGestures = _pglManager.FilterGesturesForCurrentLevel(rawGestures);
            }

            // Send gated gestures into Action Mapper -> Decision Engine
            if (_actionMapper != null)
            {
                _actionMapper.HandleGestures(gatedGestures);
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
