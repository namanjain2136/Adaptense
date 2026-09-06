// ============================================================
// ADAPTER Project — Phase 5
// AdaptationDecision.cs
//
// PURPOSE:
//   Defines the adaptation outcomes produced by the
//   ContextAwareDecisionEngine.
// ============================================================

using System;
using Adapter.Gesture;

namespace Adapter.Adaptation
{
    /// <summary>
    /// The adaptive strategy chosen by the decision engine.
    /// </summary>
    public enum DecisionType
    {
        ExecuteNormally,          // Confidence is high and context is optimal
        ShowVisualHint,           // User is struggling or gesture is ambiguous
        MakeInteractionEasier,    // Relax confidence thresholds / tolerance due to distance/lighting
        OfferAlternativeGesture   // Suggest an easier alternative gesture for the target action
    }

    /// <summary>
    /// Output result of the decision engine's evaluation.
    /// </summary>
    [Serializable]
    public struct AdaptationResult
    {
        public DecisionType Decision;
        public GestureType CandidateGesture;
        public GestureType? AlternativeGesture;
        public float BaseConfidence;
        public float AdjustedThreshold;
        public bool ShouldExecuteAction;
        public string Explanation;
        public string HintMessage;

        public override string ToString()
        {
            return $"[Decision] {Decision} on {CandidateGesture} | Execute={ShouldExecuteAction} | " +
                   $"Threshold={AdjustedThreshold:F2} | Note: {Explanation}";
        }
    }
}
