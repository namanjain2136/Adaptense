// ============================================================
// ADAPTER Project — Phase 5
// ContextTracker.cs
//
// PURPOSE:
//   Gathers real-time interaction context (failed attempt counts,
//   learning task, user progress score, landmark-based distance,
//   and lighting conditions).
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Adapter.Gesture;
using Mediapipe;

namespace Adapter.Context
{
    public class ContextTracker : MonoBehaviour
    {
        [Header("Learning Activity State")]
        [Tooltip("Current learning activity / objective.")]
        [SerializeField] private LearningTask _currentTask = LearningTask.ExploreLab;

        [Tooltip("Number of completed interaction tasks.")]
        [SerializeField] private int _completedTasksCount = 0;

        [Tooltip("Overall progress percentage (0.0 to 1.0).")]
        [Range(0f, 1f)]
        [SerializeField] private float _progressScore = 0f;

        [Header("Environmental Context")]
        [Tooltip("MediaPipe landmarks cannot directly measure lighting lux. " +
                 "This field provides a manual/simulated lighting condition.")]
        [SerializeField] private LightingCondition _lighting = LightingCondition.Optimal;

        [Header("References")]
        [SerializeField] private HandLandmarkReader _landmarkReader;

        // Interaction history / failure tracking per gesture type
        private readonly Dictionary<GestureType, int> _failedAttempts = new Dictionary<GestureType, int>();
        private readonly Dictionary<GestureType, int> _successfulAttempts = new Dictionary<GestureType, int>();

        public LearningTask CurrentTask
        {
            get => _currentTask;
            set => _currentTask = value;
        }

        public LightingCondition Lighting
        {
            get => _lighting;
            set => _lighting = value;
        }

        public int CompletedTasksCount => _completedTasksCount;
        public float ProgressScore => _progressScore;

        private void Awake()
        {
            // Initialize counters for all gesture types
            foreach (GestureType type in System.Enum.GetValues(typeof(GestureType)))
            {
                _failedAttempts[type] = 0;
                _successfulAttempts[type] = 0;
            }
        }

        private void Start()
        {
            if (_landmarkReader == null)
            {
                _landmarkReader = FindObjectOfType<HandLandmarkReader>();
            }
        }

        /// <summary>
        /// Assembles a complete context snapshot for the given gesture candidate.
        /// </summary>
        public ContextSnapshot GetSnapshot(GestureType candidateGesture, float rawConfidence)
        {
            float handScale = ComputeHandScale();
            DistanceEstimate distance = EstimateDistance(handScale);

            _failedAttempts.TryGetValue(candidateGesture, out int fails);

            return new ContextSnapshot
            {
                CandidateGesture = candidateGesture,
                RawConfidence = rawConfidence,
                FailedAttemptsForGesture = fails,
                CurrentTask = _currentTask,
                CompletedTasksCount = _completedTasksCount,
                ProgressScore = _progressScore,
                Lighting = _lighting,
                Distance = distance,
                HandScale = handScale
            };
        }

        /// <summary>
        /// Records a successful gesture execution, resetting the failure streak for this gesture.
        /// </summary>
        public void RecordSuccess(GestureType gesture)
        {
            _failedAttempts[gesture] = 0;
            if (_successfulAttempts.ContainsKey(gesture))
                _successfulAttempts[gesture]++;
            else
                _successfulAttempts[gesture] = 1;

            _completedTasksCount++;
            _progressScore = Mathf.Clamp01(_completedTasksCount / 10f); // 10 steps for full progress

            Debug.Log($"[ContextTracker] Success recorded for {gesture}. Total completed tasks: {_completedTasksCount}");
        }

        /// <summary>
        /// Records a failed gesture attempt (e.g. low confidence, ambiguous pose, or canceled action).
        /// </summary>
        public void RecordFailure(GestureType gesture)
        {
            if (_failedAttempts.ContainsKey(gesture))
                _failedAttempts[gesture]++;
            else
                _failedAttempts[gesture] = 1;

            Debug.Log($"[ContextTracker] Failure recorded for {gesture}. Recent failed streak: {_failedAttempts[gesture]}");
        }

        /// <summary>
        /// Advances the current learning activity to the next task.
        /// </summary>
        public void AdvanceToNextTask()
        {
            int next = ((int)_currentTask + 1) % System.Enum.GetValues(typeof(LearningTask)).Length;
            _currentTask = (LearningTask)next;
            Debug.Log($"[ContextTracker] Advanced to new task: {_currentTask}");
        }

        /// <summary>
        /// Computes hand size scale in normalized screen coordinates (wrist to middle MCP distance).
        /// Returns ~0.1 - 0.5 depending on distance from camera.
        /// </summary>
        private float ComputeHandScale()
        {
            if (_landmarkReader == null) return 0.25f;

            var hands = _landmarkReader.GetLatestLandmarks();
            if (hands == null || hands.Count == 0) return 0.25f;

            var hand = hands[0].Landmark;
            if (hand.Count <= 9) return 0.25f;

            // Distance between Wrist (0) and Middle MCP (9)
            Vector2 wrist = new Vector2(hand[0].X, hand[0].Y);
            Vector2 middleMcp = new Vector2(hand[9].X, hand[9].Y);
            return Vector2.Distance(wrist, middleMcp);
        }

        /// <summary>
        /// Estimates user distance from camera based on hand landmark screen span.
        /// </summary>
        private DistanceEstimate EstimateDistance(float handScale)
        {
            if (handScale > 0.38f) return DistanceEstimate.Close;
            if (handScale < 0.14f) return DistanceEstimate.Far;
            return DistanceEstimate.Optimal;
        }
    }
}
