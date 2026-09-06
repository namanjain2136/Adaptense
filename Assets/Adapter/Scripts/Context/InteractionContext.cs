// ============================================================
// ADAPTER Project — Phase 5
// InteractionContext.cs
//
// PURPOSE:
//   Data models and enums representing the multi-modal context
//   factors gathered for each gesture interaction attempt.
// ============================================================

using System;
using UnityEngine;
using Adapter.Gesture;

namespace Adapter.Context
{
    /// <summary>
    /// Represents the current learning module activity or target task.
    /// </summary>
    public enum LearningTask
    {
        ExploreLab,
        SelectBeaker,
        GrabApparatus,
        OpenManual,
        FreeExploration
    }

    /// <summary>
    /// Environmental lighting condition.
    /// NOTE: MediaPipe normalized landmarks do not provide camera exposure/lux values.
    /// This is stubbed with manual override capability for context-aware simulation.
    /// </summary>
    public enum LightingCondition
    {
        Optimal,
        Dim,
        HighGlare,
        UnknownManualEstimate
    }

    /// <summary>
    /// User distance estimate relative to webcam (derived from hand landmark bounding box scale).
    /// </summary>
    public enum DistanceEstimate
    {
        Optimal,
        Close,
        Far,
        Unknown
    }

    /// <summary>
    /// Snapshot of all contextual parameters at the moment a gesture is evaluated.
    /// </summary>
    [Serializable]
    public struct ContextSnapshot
    {
        public GestureType CandidateGesture;
        public float RawConfidence;
        public int FailedAttemptsForGesture;
        public LearningTask CurrentTask;
        public int CompletedTasksCount;
        public float ProgressScore;
        public LightingCondition Lighting;
        public DistanceEstimate Distance;
        public float HandScale; // Normalized landmark span (approx distance proxy)

        public override string ToString()
        {
            return $"[Context] Gesture={CandidateGesture} (Conf={RawConfidence:F2}) | " +
                   $"Fails={FailedAttemptsForGesture} | Task={CurrentTask} | " +
                   $"Progress={CompletedTasksCount} | Dist={Distance} | Light={Lighting}";
        }
    }
}
