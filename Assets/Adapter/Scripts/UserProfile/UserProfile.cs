// ============================================================
// ADAPTER Project — Phase 7 (Persistent Identity Profile — PIP)
// UserProfile.cs
//
// PURPOSE:
//   Plain serializable data class representing a single learner's
//   persistent profile. Survives closing and reopening the app.
//   Wraps values already consumed by Phase 5 (CADE) and Phase 6 (PGL)
//   without changing their internal logic — PIP supplies/restores them.
//
// SERIALIZATION NOTE:
//   JsonUtility is used for all save/load (built into Unity, no packages).
//   - Dictionary is NOT supported by JsonUtility — avoided intentionally.
//   - HashSet is NOT supported by JsonUtility — level2SelectedObjectCount
//     stores the count instead (Phase 6 only needs to know count >= 2).
// ============================================================

using System;
using UnityEngine;

namespace Adapter.UserProfile
{
    [Serializable]
    public class UserProfile
    {
        // ── Identity ───────────────────────────────────────────────────
        /// <summary>
        /// Unique learner identifier. Generated once on profile creation (GUID string).
        /// </summary>
        public string profileId;

        // ── Phase 6 (PGL) State ────────────────────────────────────────
        /// <summary>
        /// Current PGL level (1–5). Maps to GestureLevel enum in Phase 6.
        /// </summary>
        public int currentLevel;

        // Level 1 task progress
        /// <summary>
        /// Whether the learner has opened the Info Panel at least once (Level 1 task).
        /// Mirrors _level1OpenedPanel in GesturePGLManager.
        /// </summary>
        public bool level1OpenedPanel;

        /// <summary>
        /// Whether the learner has opened the Description Modal at least once (Level 1 task).
        /// Mirrors _level1OpenedDescription in GesturePGLManager.
        /// </summary>
        public bool level1OpenedDescription;

        // Level 2 task progress
        /// <summary>
        /// How many distinct objects have been selected with Point at Level 2.
        /// Mirrors _level2SelectedObjects.Count in GesturePGLManager.
        /// (HashSet not serializable by JsonUtility — count stored instead.)
        /// </summary>
        public int level2SelectedObjectCount;

        /// <summary>
        /// Whether the learner has performed at least one Swipe at Level 2.
        /// Mirrors _level2Swiped in GesturePGLManager.
        /// </summary>
        public bool level2Swiped;

        // Level 3 task progress
        /// <summary>
        /// Whether the learner has performed a Pinch gesture at Level 3.
        /// Mirrors _level3Resetted in GesturePGLManager.
        /// </summary>
        public bool level3Resetted;

        // Level 4 task progress
        /// <summary>
        /// Whether the learner has performed a Fist grab/move at Level 4.
        /// Mirrors _level4Grabbed in GesturePGLManager.
        /// </summary>
        public bool level4Grabbed;

        // ── Phase 5 (CADE) Sensitivity ─────────────────────────────────
        /// <summary>
        /// Gesture confidence sensitivity (0–1, default 0.5).
        /// This is the tolerance value Phase 5's ContextAwareDecisionEngine reads
        /// as its base confidence threshold. PIP makes it persistent and per-user
        /// instead of being a hardcoded Inspector field.
        /// </summary>
        [Range(0f, 1f)]
        public float gestureSensitivity;

        // ── Session Metadata ───────────────────────────────────────────
        /// <summary>
        /// Total number of sessions this profile has been loaded across.
        /// Incremented on every Awake/Start of ProfileManager.
        /// </summary>
        public int sessionCount;

        /// <summary>
        /// ISO 8601 UTC timestamp of when this profile was last loaded.
        /// Updated on every session start (DateTime.UtcNow.ToString("o")).
        /// </summary>
        public string lastUsedTimestamp;

        // ── Factory: Create a fresh default profile ────────────────────
        /// <summary>
        /// Creates a brand-new default UserProfile with safe starting values.
        /// Called by ProfileManager when no saved file exists.
        /// </summary>
        public static UserProfile CreateDefault()
        {
            return new UserProfile
            {
                profileId              = Guid.NewGuid().ToString(),
                currentLevel           = 1,
                level1OpenedPanel      = false,
                level1OpenedDescription = false,
                level2SelectedObjectCount = 0,
                level2Swiped           = false,
                level3Resetted         = false,
                level4Grabbed          = false,
                gestureSensitivity     = 0.5f,
                sessionCount           = 0,
                lastUsedTimestamp      = DateTime.UtcNow.ToString("o")
            };
        }
    }
}
