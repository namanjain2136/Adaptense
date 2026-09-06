// ============================================================
// ADAPTER Project — Phase 5
// ContextAwareDecisionEngine.cs
//
// PURPOSE:
//   Core Novelty: Sits directly between Phase 2 (GestureRecognizer)
//   and Phase 4 (GestureActionMapper).
//   
//   Evaluates multi-modal interaction context (raw gesture confidence,
//   repeated failure counts, task state, user progress, distance &
//   lighting conditions) to adaptively decide whether to:
//     1. Execute Normally
//     2. Make Interaction Easier (widen matching tolerance / relax threshold)
//     3. Show Visual Hint (guidance for ambiguous/struggling gestures)
//     4. Offer Alternative Gesture (fallback when repeated failures occur)
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Adapter.Gesture;
using Adapter.Context;
using Adapter.UserProfile; // Phase 7 (PIP) — profile-driven sensitivity

namespace Adapter.Adaptation
{
    public class ContextAwareDecisionEngine : MonoBehaviour
    {
        [Header("Context Tracking Reference")]
        [SerializeField] private ContextTracker _contextTracker;

        [Header("Base Thresholds")]
        [Tooltip("Nominal confidence required under ideal conditions.")]
        [Range(0.4f, 0.9f)]
        [SerializeField] private float _baseConfidenceThreshold = 0.65f;

        [Tooltip("Minimum threshold when adapting for poor lighting/distance or struggle.")]
        [Range(0.2f, 0.6f)]
        [SerializeField] private float _relaxedConfidenceThreshold = 0.42f;

        [Header("Adaptation Parameters")]
        [Tooltip("Failed attempts before showing visual guidance hints.")]
        [SerializeField] private int _hintFailureThreshold = 1;

        [Tooltip("Failed attempts before suggesting an alternative gesture.")]
        [SerializeField] private int _alternativeGestureFailureThreshold = 4;

        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = true;

        // Callback emitted whenever an adaptation decision is produced
        public Action<AdaptationResult, DetectedGesture> OnAdaptationEvaluated;

        public ContextTracker Tracker => _contextTracker;

        private void Awake()
        {
            if (_contextTracker == null)
            {
                _contextTracker = GetComponent<ContextTracker>();
                if (_contextTracker == null)
                {
                    _contextTracker = gameObject.AddComponent<ContextTracker>();
                }
            }

            // ── Phase 7 (PIP): Load per-user gesture sensitivity from profile ──
            // gestureSensitivity is 0–1. We map it to the 0.4–0.9 threshold range
            // so that the default value of 0.5 produces exactly 0.65 — the same
            // as the previous hardcoded value — preserving existing behaviour.
            // Formula: threshold = Lerp(0.4, 0.9, sensitivity)
            //   sensitivity=0.0 → threshold=0.40  (very forgiving)
            //   sensitivity=0.5 → threshold=0.65  (default, unchanged)
            //   sensitivity=1.0 → threshold=0.90  (very strict)
            if (ProfileManager.Instance != null)
            {
                float s = ProfileManager.Instance.ActiveProfile.gestureSensitivity;
                _baseConfidenceThreshold = Mathf.Lerp(0.4f, 0.9f, s);
                Debug.Log($"[CADE] Loaded gestureSensitivity={s:F2} from profile → _baseConfidenceThreshold={_baseConfidenceThreshold:F2}");
            }
            else
            {
                Debug.LogWarning("[CADE] ProfileManager not found — using Inspector default _baseConfidenceThreshold=" + _baseConfidenceThreshold);
            }
            // NOTE: CADE does NOT dynamically change _baseConfidenceThreshold at runtime —
            // it only widens to _relaxedConfidenceThreshold as a temporary floor.
            // Therefore no SaveProfile() call is needed here — Step 3 is read-only.
        }

        /// <summary>
        /// Main Entry Point: Evaluates incoming Phase 2 gestures against active context.
        /// </summary>
        public AdaptationResult ProcessGestures(List<DetectedGesture> rawGestures)
        {
            if (rawGestures == null || rawGestures.Count == 0)
            {
                return new AdaptationResult
                {
                    Decision = DecisionType.ShowVisualHint,
                    ShouldExecuteAction = false,
                    Explanation = "No gesture input detected.",
                    HintMessage = "Place hand in view of camera."
                };
            }

            // Find the gesture with highest raw confidence
            DetectedGesture bestCandidate = rawGestures[0];
            foreach (var g in rawGestures)
            {
                if (g.Confidence > bestCandidate.Confidence)
                {
                    bestCandidate = g;
                }
            }

            // Capture context snapshot from Tracker
            ContextSnapshot snapshot = _contextTracker.GetSnapshot(bestCandidate.Type, bestCandidate.Confidence);

            // Execute the Decision Matrix
            AdaptationResult result = EvaluateDecision(bestCandidate, snapshot);

            if (_enableDebugLogs && result.Decision != DecisionType.ExecuteNormally)
            {
                Debug.Log($"[DecisionEngine] {result.Decision} => {result.Explanation} | Hint: {result.HintMessage}");
            }

            // Fire callback
            OnAdaptationEvaluated?.Invoke(result, bestCandidate);

            return result;
        }

        /// <summary>
        /// Rule-Based Adaptive Decision Matrix (Clean and easy to explain in viva/demo).
        /// </summary>
        private AdaptationResult EvaluateDecision(DetectedGesture candidate, ContextSnapshot context)
        {
            float targetThreshold = _baseConfidenceThreshold;
            bool isEnvironmentalDifficulty = false;

            // ── Factor 1: Environmental & Distance Adjustment ───────────────
            // If distance is far/close or lighting is dim, widen tolerance
            if (context.Distance == DistanceEstimate.Far || context.Distance == DistanceEstimate.Close)
            {
                targetThreshold = Mathf.Min(targetThreshold, _relaxedConfidenceThreshold + 0.08f);
                isEnvironmentalDifficulty = true;
            }

            if (context.Lighting == LightingCondition.Dim || context.Lighting == LightingCondition.HighGlare)
            {
                targetThreshold = Mathf.Min(targetThreshold, _relaxedConfidenceThreshold);
                isEnvironmentalDifficulty = true;
            }

            // ── Factor 2: Repeated Failure Recovery (Widen tolerance) ────────
            if (context.FailedAttemptsForGesture >= _hintFailureThreshold)
            {
                targetThreshold = Mathf.Min(targetThreshold, _relaxedConfidenceThreshold);
            }

            // ── Decision Rule 1: Offer Alternative Gesture ───────────────────
            // If user has failed 4+ times on this gesture in the current learning task,
            // suggest an alternate gesture that maps to a similar interaction.
            if (context.FailedAttemptsForGesture >= _alternativeGestureFailureThreshold)
            {
                GestureType alternative = GetAlternativeGesture(candidate.Type, context.CurrentTask);
                return new AdaptationResult
                {
                    Decision = DecisionType.OfferAlternativeGesture,
                    CandidateGesture = candidate.Type,
                    AlternativeGesture = alternative,
                    BaseConfidence = candidate.Confidence,
                    AdjustedThreshold = targetThreshold,
                    ShouldExecuteAction = false,
                    Explanation = $"Repeated struggles ({context.FailedAttemptsForGesture} fails) with {candidate.Type}.",
                    HintMessage = $"Having trouble with {candidate.Type}? Try using {alternative} instead!"
                };
            }

            // ── Decision Rule 2: Execute Normally (Optimal Context) ──────────
            if (candidate.Confidence >= _baseConfidenceThreshold && !isEnvironmentalDifficulty && context.FailedAttemptsForGesture == 0)
            {
                return new AdaptationResult
                {
                    Decision = DecisionType.ExecuteNormally,
                    CandidateGesture = candidate.Type,
                    BaseConfidence = candidate.Confidence,
                    AdjustedThreshold = _baseConfidenceThreshold,
                    ShouldExecuteAction = true,
                    Explanation = "High confidence under optimal conditions.",
                    HintMessage = string.Empty
                };
            }

            // ── Decision Rule 3: Make Interaction Easier (Adaptive Tolerance) ─
            // If confidence meets the relaxed/adjusted threshold due to environmental difficulty or prior struggle
            if (candidate.Confidence >= targetThreshold)
            {
                return new AdaptationResult
                {
                    Decision = DecisionType.MakeInteractionEasier,
                    CandidateGesture = candidate.Type,
                    BaseConfidence = candidate.Confidence,
                    AdjustedThreshold = targetThreshold,
                    ShouldExecuteAction = true,
                    Explanation = $"Lowered tolerance to {targetThreshold:F2} (Env/Failure adaptation: Dist={context.Distance}, Light={context.Lighting}).",
                    HintMessage = "Tolerance widened for smooth interaction."
                };
            }

            // ── Decision Rule 4: Show Visual Hint (Guidance) ─────────────────
            // Confidence is below threshold or user is struggling (failed 2+ times)
            string specificHint = GenerateGestureHint(candidate.Type, context);
            return new AdaptationResult
            {
                Decision = DecisionType.ShowVisualHint,
                CandidateGesture = candidate.Type,
                BaseConfidence = candidate.Confidence,
                AdjustedThreshold = targetThreshold,
                ShouldExecuteAction = false,
                Explanation = $"Confidence ({candidate.Confidence:F2}) below threshold ({targetThreshold:F2}). Fails={context.FailedAttemptsForGesture}.",
                HintMessage = specificHint
            };
        }

        /// <summary>
        /// Provides an intuitive alternative gesture mapping when a gesture consistently fails.
        /// </summary>
        private GestureType GetAlternativeGesture(GestureType failedGesture, LearningTask task)
        {
            switch (failedGesture)
            {
                case GestureType.Point:
                    return GestureType.Swipe; // Swipe can cycle selection as an alternative
                case GestureType.Pinch:
                    return GestureType.Fist;  // Fist grab fallback
                case GestureType.OpenPalm:
                    return GestureType.Point; // Pointing at info button fallback
                case GestureType.Swipe:
                    return GestureType.Point; // Point to step next
                default:
                    return GestureType.OpenPalm;
            }
        }

        /// <summary>
        /// Generates targeted visual guidance hints based on the candidate gesture and context.
        /// </summary>
        private string GenerateGestureHint(GestureType gesture, ContextSnapshot context)
        {
            if (context.Distance == DistanceEstimate.Far)
                return "Move hand closer to the camera.";
            if (context.Distance == DistanceEstimate.Close)
                return "Move hand slightly further from the camera.";
            if (context.Lighting == LightingCondition.Dim)
                return "Low lighting detected. Increase room lighting or hold hand steady.";

            switch (gesture)
            {
                case GestureType.Point:
                    return "Extend index finger fully while curling middle, ring, and pinky.";
                case GestureType.Pinch:
                    return "Bring index finger and thumb tips close together.";
                case GestureType.OpenPalm:
                    return "Spread all 5 fingers openly facing the camera.";
                case GestureType.Fist:
                    return "Curl all fingers tightly toward palm.";
                case GestureType.Swipe:
                    return "Move hand briskly horizontally across the camera view.";
                default:
                    return "Perform gesture clearly in the camera frame.";
            }
        }
    }
}
