// ============================================================
// ADAPTER Project — Phase 2
// GestureRecognizer.cs
//
// PURPOSE:
//   Evaluates the raw 21 hand landmarks provided by Phase 1
//   and calculates 0.0 to 1.0 confidence values for 5 specific
//   gestures (Open Palm, Fist, Point, Pinch, Swipe).
//
// RULES FOLLOWED:
//   - Zero modifications to the MediaPipe sample scene.
//   - Outputs continuous confidence values, not just booleans.
//   - Emits an event (callback) each frame with the results.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Mediapipe;

namespace Adapter.Gesture
{
    public enum GestureType
    {
        OpenPalm,
        Fist,
        Point,
        Pinch,
        Swipe
    }

    [System.Serializable]
    public struct DetectedGesture
    {
        public GestureType Type;
        public float Confidence; // 0.0 (none) to 1.0 (certain)
    }

    /// <summary>
    /// Attach this alongside HandLandmarkReader.
    /// It polls the reader every frame and computes gesture probabilities.
    /// </summary>
    public class GestureRecognizer : MonoBehaviour
    {
        [Tooltip("Reference to the Phase 1 reader. If null, will try to GetComponent it.")]
        [SerializeField] private HandLandmarkReader _landmarkReader;

        [Tooltip("How often to log gesture confidences to the Console (in seconds). Set to 0 to disable.")]
        [SerializeField] private float _debugLogIntervalSeconds = 0f;
        private float _lastLogTime = 0f;

        // ── Phase 2 Output Event ──────────────────────────────────────────
        // This callback is emitted every frame a hand is tracked.
        // It provides the calculated confidence (0-1) for every gesture.
        public System.Action<List<DetectedGesture>> OnGesturesEvaluated;

        // ── Internal state for motion-based gestures (Swipe) ──────────────
        private Queue<Vector3> _wristHistory = new Queue<Vector3>();
        private readonly int _swipeHistorySize = 10; // ~0.3s window at 30fps

        private void Start()
        {
            if (_landmarkReader == null)
            {
                _landmarkReader = GetComponent<HandLandmarkReader>();
            }
        }

        private void Update()
        {
            if (_landmarkReader == null) return;

            var hands = _landmarkReader.GetLatestLandmarks();
            if (hands == null || hands.Count == 0)
            {
                _wristHistory.Clear(); // Hand lost, reset motion history
                return;
            }

            // For simplicity in Phase 2, we evaluate the primary hand (index 0)
            var hand = hands[0].Landmark;

            // Build the evaluation results
            List<DetectedGesture> results = new List<DetectedGesture>
            {
                new DetectedGesture { Type = GestureType.OpenPalm, Confidence = DetectOpenPalm(hand) },
                new DetectedGesture { Type = GestureType.Fist,     Confidence = DetectFist(hand) },
                new DetectedGesture { Type = GestureType.Point,    Confidence = DetectPoint(hand) },
                new DetectedGesture { Type = GestureType.Pinch,    Confidence = DetectPinch(hand) },
                new DetectedGesture { Type = GestureType.Swipe,    Confidence = DetectSwipe(hand) }
            };

            // Emit the event so Phase 4/5 can consume it
            OnGesturesEvaluated?.Invoke(results);

            // Optional debug logging to prove it works (time-based to avoid spam)
            if (_debugLogIntervalSeconds > 0 && Time.time - _lastLogTime >= _debugLogIntervalSeconds)
            {
                _lastLogTime = Time.time;
                string log = "[GestureRecognizer] ";
                foreach (var res in results)
                {
                    if (res.Confidence > 0.1f)
                        log += $"{res.Type}: {res.Confidence:F2} | ";
                }
                if (log.Length > 22)
                    Debug.Log(log);
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // ── GESTURE DETECTORS ─────────────────────────────────────────────
        // ──────────────────────────────────────────────────────────────────

        private float DetectOpenPalm(Google.Protobuf.Collections.RepeatedField<NormalizedLandmark> hand)
        {
            // Open Palm = ALL fingers are extended.
            float thumbExt  = GetExtension(hand, 1, 4);
            float indexExt  = GetExtension(hand, 5, 8);
            float middleExt = GetExtension(hand, 9, 12);
            float ringExt   = GetExtension(hand, 13, 16);
            float pinkyExt  = GetExtension(hand, 17, 20);

            // Average extension of all 5 digits
            return (thumbExt + indexExt + middleExt + ringExt + pinkyExt) / 5f;
        }

        private float DetectFist(Google.Protobuf.Collections.RepeatedField<NormalizedLandmark> hand)
        {
            // Fist = ALL fingers are curled (the exact mathematical inverse of Open Palm).
            return 1.0f - DetectOpenPalm(hand);
        }

        private float DetectPoint(Google.Protobuf.Collections.RepeatedField<NormalizedLandmark> hand)
        {
            // Point = Index finger is extended, but Middle, Ring, and Pinky are curled.
            // (Thumb state is somewhat irrelevant for a basic point, but often curled).
            float indexExt   = GetExtension(hand, 5, 8);
            float middleCurl = 1.0f - GetExtension(hand, 9, 12);
            float ringCurl   = 1.0f - GetExtension(hand, 13, 16);
            float pinkyCurl  = 1.0f - GetExtension(hand, 17, 20);

            return (indexExt + middleCurl + ringCurl + pinkyCurl) / 4f;
        }

        private float DetectPinch(Google.Protobuf.Collections.RepeatedField<NormalizedLandmark> hand)
        {
            // Pinch = Distance between Thumb Tip (4) and Index Tip (8) is very small.
            float dist = Vector2.Distance(ToVector2(hand[4]), ToVector2(hand[8]));
            
            // Normalized screen distance: < 0.03 is a tight pinch. > 0.15 is wide open.
            float confidence = 1.0f - Mathf.Clamp01((dist - 0.02f) / 0.10f);
            return confidence;
        }

        private float DetectSwipe(Google.Protobuf.Collections.RepeatedField<NormalizedLandmark> hand)
        {
            // Swipe = The wrist (0) is moving rapidly across the normalized screen space over time.
            Vector3 current = new Vector3(hand[0].X, hand[0].Y, Time.time); // Z holds timestamp
            _wristHistory.Enqueue(current);
            
            if (_wristHistory.Count > _swipeHistorySize)
                _wristHistory.Dequeue();

            if (_wristHistory.Count == _swipeHistorySize)
            {
                Vector3 oldest = _wristHistory.Peek();
                float dist = Vector2.Distance(new Vector2(current.x, current.y), new Vector2(oldest.x, oldest.y));
                float timeDelta = current.z - oldest.z;

                if (timeDelta > 0.01f) // avoid division by zero
                {
                    // Speed in normalized screen units per second
                    float speed = dist / timeDelta; 
                    
                    // 1.5 units/sec is a very fast, deliberate swipe
                    return Mathf.Clamp01(speed / 1.5f);
                }
            }
            return 0f;
        }

        // ──────────────────────────────────────────────────────────────────
        // ── MATH UTILITIES ────────────────────────────────────────────────
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Calculates how "extended" a finger is by comparing the distance of its Tip to the Wrist
        /// vs the distance of its MCP (base knuckle) to the Wrist.
        /// Returns 0.0 (fully curled) to 1.0 (fully extended).
        /// </summary>
        private float GetExtension(Google.Protobuf.Collections.RepeatedField<NormalizedLandmark> hand, int mcpIdx, int tipIdx)
        {
            Vector2 wrist = ToVector2(hand[0]);
            Vector2 mcp = ToVector2(hand[mcpIdx]);
            Vector2 tip = ToVector2(hand[tipIdx]);

            float mcpDist = Vector2.Distance(wrist, mcp);
            float tipDist = Vector2.Distance(wrist, tip);

            // Ratio of tip distance to MCP distance.
            // When fully extended, the tip is roughly twice as far from the wrist as the MCP.
            // When curled into a fist, the tip is often closer to the wrist than the MCP.
            float ratio = mcpDist > 0.001f ? (tipDist / mcpDist) : 0f;
            
            // Map the ratio (1.2 to 2.0) to a clean 0.0 to 1.0 confidence score.
            return Mathf.Clamp01((ratio - 1.2f) / (2.0f - 1.2f));
        }

        private Vector2 ToVector2(NormalizedLandmark lm)
        {
            return new Vector2(lm.X, lm.Y);
        }
    }
}
