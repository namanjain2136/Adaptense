// ============================================================
// ADAPTER Project — Phase 1
// HandLandmarkReader.cs
//
// PURPOSE:
//   Reads hand landmark output from the already-running
//   MediaPipe HandTrackingGraph (found at runtime via
//   FindObjectOfType) and logs the 21 landmark positions for
//   each detected hand to the Unity Console every N seconds.
//
// RULES FOLLOWED:
//   - Zero modifications to the MediaPipe sample scene.
//   - Zero re-implementation of hand tracking or camera capture.
//   - Reads data exclusively from HandTrackingGraph's existing
//     OnHandLandmarksOutput event.
//   - Lives entirely under Assets/Adapter/.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Reference the MediaPipe namespaces so we can find
// HandTrackingGraph and use its output types without modifying it.
using Mediapipe.Unity;              // OutputEventArgs<>, OutputStream, etc.
using Mediapipe.Unity.HandTracking; // HandTrackingGraph
using Mediapipe;                    // NormalizedLandmarkList, NormalizedLandmark

namespace Adapter.Gesture
{
    /// <summary>
    /// Attach this MonoBehaviour to any GameObject in a scene
    /// that also contains the MediaPipe Hand Tracking setup
    /// (i.e. the same scene as Hand Tracking.unity, or any
    /// scene you add the HandTrackingGraph to).
    ///
    /// It subscribes to HandTrackingGraph.OnHandLandmarksOutput
    /// and logs all 21 landmark (x, y, z) coordinates per hand
    /// to the Unity Console at a configurable interval.
    /// </summary>
    public class HandLandmarkReader : MonoBehaviour
    {
        [Tooltip("How many seconds between Console log dumps. " +
                 "Set to 0 to disable (recommended for clean gameplay).")]
        [SerializeField] private float _logIntervalSeconds = 0f;

        // The graph runner we hook into — found at Start().
        private HandTrackingGraph _handTrackingGraph;

        // Thread-safe snapshot written from the MediaPipe callback
        // and read from the main thread for logging.
        private List<NormalizedLandmarkList> _latestLandmarks;
        private readonly object _lock = new object();

        // Tracks whether the first valid frame has arrived.
        private bool _receivedData = false;
        private System.DateTime _lastValidTime;

        // ── MediaPipe landmark index names (for readable Console output) ──
        private static readonly string[] LandmarkNames = new string[]
        {
            "WRIST",
            "THUMB_CMC",  "THUMB_MCP",  "THUMB_IP",  "THUMB_TIP",
            "INDEX_MCP",  "INDEX_PIP",  "INDEX_DIP", "INDEX_TIP",
            "MIDDLE_MCP", "MIDDLE_PIP", "MIDDLE_DIP","MIDDLE_TIP",
            "RING_MCP",   "RING_PIP",   "RING_DIP",  "RING_TIP",
            "PINKY_MCP",  "PINKY_PIP",  "PINKY_DIP", "PINKY_TIP",
        };

        // ── Unity lifecycle ───────────────────────────────────────────────

        private IEnumerator Start()
        {
            // ── Step 1: wait until HandTrackingGraph exists in the scene ──
            Debug.Log("[HandLandmarkReader] Searching for HandTrackingGraph...");

            while (_handTrackingGraph == null)
            {
                _handTrackingGraph = FindObjectOfType<HandTrackingGraph>();
                if (_handTrackingGraph == null)
                    yield return new WaitForSeconds(0.5f);
            }

            Debug.Log($"[HandLandmarkReader] Found HandTrackingGraph on " +
                      $"'{_handTrackingGraph.gameObject.name}'. " +
                      $"Waiting for internal streams to initialize...");

            // ── Step 2: wait until the graph's internal OutputStream fields
            //    are initialized by ConfigureCalculatorGraph().
            bool subscribed = false;
            while (!subscribed)
            {
                bool needsRetry = false;
                try
                {
                    _handTrackingGraph.OnHandLandmarksOutput += OnHandLandmarksOutput;
                    subscribed = true;  // only reached if no exception thrown
                }
                catch (System.NullReferenceException)
                {
                    needsRetry = true;  // streams not ready yet
                }

                if (needsRetry)
                    yield return new WaitForSeconds(0.2f);
            }

            Debug.Log("[HandLandmarkReader] Subscribed to OnHandLandmarksOutput. " +
                      "Show your hand to the webcam!");

            // Start the periodic Console dump coroutine.
            StartCoroutine(LogLandmarksPeriodically());
        }

        private void OnDestroy()
        {
            // Always unsubscribe to avoid dangling references.
            if (_handTrackingGraph != null)
            {
                _handTrackingGraph.OnHandLandmarksOutput -= OnHandLandmarksOutput;
            }
        }

        // ── MediaPipe callback ────────────────────────────────────────────

        /// <summary>
        /// Called by the MediaPipe pipeline (possibly off the main thread)
        /// each time a new set of hand landmarks is ready.
        /// </summary>
        private void OnHandLandmarksOutput(object stream,
            OutputEventArgs<List<NormalizedLandmarkList>> eventArgs)
        {
            lock (_lock)
            {
                // MediaPipe often interleaves empty packets (null) between valid frames.
                // We ignore the null packets so our snapshot always holds the latest valid hand pose.
                if (eventArgs.value != null && eventArgs.value.Count > 0)
                {
                    _latestLandmarks = eventArgs.value;
                    _lastValidTime = System.DateTime.UtcNow;
                    _receivedData = true;
                }
            }
        }

        // ── Periodic logging ──────────────────────────────────────────────

        private IEnumerator LogLandmarksPeriodically()
        {
            if (_logIntervalSeconds <= 0)
            {
                Debug.Log("[HandLandmarkReader] Landmark logger disabled (Interval <= 0).");
                yield break;
            }

            var waitTime = new WaitForSeconds(_logIntervalSeconds);

            Debug.Log("[HandLandmarkReader] Landmark logger started. " +
                      $"Will log every {_logIntervalSeconds:F1}s. " +
                      "Show your hand to the webcam!");

            while (true)
            {
                yield return waitTime;
                LogLatestLandmarks();
            }
        }

        private void LogLatestLandmarks()
        {
            List<NormalizedLandmarkList> snapshot;
            bool hasData;
            double timeSinceLastValid;

            lock (_lock)
            {
                snapshot = _latestLandmarks;
                hasData  = _receivedData;
                timeSinceLastValid = (System.DateTime.UtcNow - _lastValidTime).TotalSeconds;
            }

            if (!hasData)
            {
                Debug.Log("[HandLandmarkReader] Waiting for first landmark " +
                          "frame... (is the webcam on and a hand visible?)");
                return;
            }

            // If data is older than 0.5s, the hand has left the frame.
            if (timeSinceLastValid > 0.5f || snapshot == null || snapshot.Count == 0)
            {
                Debug.Log("[HandLandmarkReader] No hands detected this frame.");
                return;
            }

            // ── Dump every hand and every landmark ────────────────────────
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[HandLandmarkReader] ── Landmark snapshot " +
                          $"({snapshot.Count} hand(s) detected) ──");

            for (int handIdx = 0; handIdx < snapshot.Count; handIdx++)
            {
                var handLandmarks = snapshot[handIdx];
                sb.AppendLine($"  Hand [{handIdx}]  " +
                              $"({handLandmarks.Landmark.Count} landmarks)");

                for (int lmIdx = 0; lmIdx < handLandmarks.Landmark.Count; lmIdx++)
                {
                    var lm = handLandmarks.Landmark[lmIdx];
                    string name = lmIdx < LandmarkNames.Length
                        ? LandmarkNames[lmIdx]
                        : $"LM_{lmIdx}";

                    sb.AppendLine(
                        $"    [{lmIdx:D2}] {name,-12} " +
                        $"x={lm.X:F4}  y={lm.Y:F4}  z={lm.Z:F6}");
                }
            }

            Debug.Log(sb.ToString());
        }

        // ── Public API for Phase 2 (Gesture Recognition) ──────────────────

        /// <summary>
        /// Returns the most recent valid landmark data, or null if no hand
        /// has been detected in the last 0.5 seconds (stale/missing).
        /// </summary>
        public List<NormalizedLandmarkList> GetLatestLandmarks()
        {
            lock (_lock)
            {
                if (!_receivedData) return null;
                double timeSinceLastValid = (System.DateTime.UtcNow - _lastValidTime).TotalSeconds;
                
                if (timeSinceLastValid > 0.5f || _latestLandmarks == null || _latestLandmarks.Count == 0)
                    return null;

                return _latestLandmarks;
            }
        }
    }
}
