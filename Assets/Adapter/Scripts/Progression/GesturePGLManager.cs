// ============================================================
// ADAPTER Project — Phase 6 (PGL Level Rearrangement & Description Modal)
// GesturePGLManager.cs
//
// Level unlocks (Rearranged per user specification):
//   Level 1: Palms Only (Single Palm = Info Panel, Double Palm = Description Modal)
//   Level 2: + Point (Select) & Swipe (Navigate selection)
//   Level 3: + Pinch (Reset / Cancel hold)
//   Level 4: + Fist (Grab & Move object to drop zone)
//   Level 5: All 5 gestures active (Mastery Mode)
//
// Features:
//   - Level Description Modal opens automatically before each level.
//   - 4-second transition delay with countdown timer between level changes.
//   - Palm gestures operate and toggle the description modal.
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Adapter.Gesture;
using Adapter.UserProfile; // Phase 7 (PIP) — profile restore & save

namespace Adapter.Progression
{
    public enum GestureLevel
    {
        Level1_PalmsOnly = 1,
        Level2_NavSelect = 2,
        Level3_Pinch     = 3,
        Level4_Fist      = 4,
        Level5_Mastery   = 5
    }

    public class GesturePGLManager : MonoBehaviour
    {
        [Header("Progression State")]
        public GestureLevel currentLevel = GestureLevel.Level1_PalmsOnly;

        [Header("UI References")]
        public Text levelBadgeText;
        public Text instructionBannerText;
        public Text taskChecklistText;
        public GameObject levelUpToastBanner;
        public GameObject levelDescriptionModal;

        // ---- Level task tracking ----
        private bool _level1OpenedPanel       = false;
        private bool _level1OpenedDescription = false;

        private HashSet<string> _level2SelectedObjects = new HashSet<string>();
        private bool _level2Swiped            = false;

        private bool _level3Resetted          = false;
        private bool _level4Grabbed           = false;

        // ---- Transition & Double Palm State ----
        private bool  _isTransitioning  = false;
        private float _lastPalmTime     = 0f;
        private float _lastLockedLogTime = 0f;

        // ---- Events ----
        public event Action<GestureLevel> OnLevelUp;
        public event Action<string>       OnPGLTaskProgress;

        private void Start()
        {
            // ── Phase 7 (PIP): Restore saved level & task progress ─────────
            if (ProfileManager.Instance != null)
            {
                Adapter.UserProfile.UserProfile profile = ProfileManager.Instance.ActiveProfile;

                // Clamp to valid enum range (1–5) in case of corrupted data
                int savedLevel = Mathf.Clamp(profile.currentLevel, 1, 5);
                currentLevel = (GestureLevel)savedLevel;

                // Restore Level 1 task flags
                _level1OpenedPanel       = profile.level1OpenedPanel;
                _level1OpenedDescription = profile.level1OpenedDescription;

                // Restore Level 2 task state.
                // HashSet is not JsonUtility-serializable, so we stored the count.
                // Pre-fill with dummy keys equal to the saved count so Count >= 2 works.
                _level2SelectedObjects = new HashSet<string>();
                for (int i = 0; i < profile.level2SelectedObjectCount; i++)
                    _level2SelectedObjects.Add($"restored_obj_{i}");
                _level2Swiped = profile.level2Swiped;

                // Restore Level 3 & 4 task flags
                _level3Resetted = profile.level3Resetted;
                _level4Grabbed  = profile.level4Grabbed;

                Debug.Log($"[PGL] Restored from profile: Level={savedLevel}, " +
                          $"L1Panel={_level1OpenedPanel}, L1Desc={_level1OpenedDescription}, " +
                          $"L2Objs={_level2SelectedObjects.Count}, L2Swiped={_level2Swiped}, " +
                          $"L3Reset={_level3Resetted}, L4Grab={_level4Grabbed}");
            }
            else
            {
                Debug.LogWarning("[PGL] ProfileManager not found — starting fresh at Level 1.");
            }

            ShowLevelDescriptionModal(currentLevel, 4f);
            UpdateUI();
            Debug.Log($"[PGL] Initialized Progressive Gesture Learning at Level {(int)currentLevel} ({currentLevel})");
        }

        /// <summary>
        /// Filters raw gestures based on current level's unlocked gestures.
        /// </summary>
        public List<DetectedGesture> FilterGesturesForCurrentLevel(List<DetectedGesture> rawGestures)
        {
            if (rawGestures == null) return rawGestures;

            // Block action execution during 3-5s level transition gap
            if (_isTransitioning)
            {
                List<DetectedGesture> blocked = new List<DetectedGesture>();
                foreach (var g in rawGestures)
                    blocked.Add(new DetectedGesture { Type = g.Type, Confidence = 0f });
                return blocked;
            }

            List<DetectedGesture> filtered = new List<DetectedGesture>();

            foreach (var g in rawGestures)
            {
                if (IsGestureUnlocked(g.Type))
                {
                    filtered.Add(g);
                }
                else
                {
                    filtered.Add(new DetectedGesture { Type = g.Type, Confidence = 0f });

                    if (g.Confidence > 0.45f && Time.time - _lastLockedLogTime > 2.0f)
                    {
                        _lastLockedLogTime = Time.time;
                        Debug.Log($"[PGL Gating] Gesture '{g.Type}' is LOCKED at Level {(int)currentLevel}. " +
                                  $"Complete task to unlock next level! (Current Level: {currentLevel})");
                    }
                }
            }

            return filtered;
        }

        /// <summary>
        /// Checks if a gesture is unlocked at current level.
        /// </summary>
        public bool IsGestureUnlocked(GestureType gesture)
        {
            switch (currentLevel)
            {
                case GestureLevel.Level1_PalmsOnly:
                    return gesture == GestureType.OpenPalm;

                case GestureLevel.Level2_NavSelect:
                    return gesture == GestureType.OpenPalm || gesture == GestureType.Point || gesture == GestureType.Swipe;

                case GestureLevel.Level3_Pinch:
                    return gesture == GestureType.OpenPalm || gesture == GestureType.Point || gesture == GestureType.Swipe || gesture == GestureType.Pinch;

                case GestureLevel.Level4_Fist:
                case GestureLevel.Level5_Mastery:
                    return true;

                default:
                    return true;
            }
        }

        /// <summary>
        /// Action execution callback — handles double palm, task checks, and level up.
        /// </summary>
        public void NotifyActionExecuted(GestureType gesture, string objectName, string actionDetails)
        {
            if (_isTransitioning) return;

            // Handle Palm logic
            if (gesture == GestureType.OpenPalm)
            {
                float timeSinceLastPalm = Time.time - _lastPalmTime;
                _lastPalmTime = Time.time;

                if (timeSinceLastPalm < 1.4f && timeSinceLastPalm > 0.2f)
                {
                    // Double Palm detected — toggle Level Description Modal
                    ToggleLevelDescriptionModal();
                    if (currentLevel == GestureLevel.Level1_PalmsOnly)
                    {
                        _level1OpenedDescription = true;
                        OnPGLTaskProgress?.Invoke("Double Palm: Opened Description Modal");
                    }
                }
                else
                {
                    // Single Palm — toggle Info Panel
                    if (currentLevel == GestureLevel.Level1_PalmsOnly)
                    {
                        _level1OpenedPanel = true;
                        OnPGLTaskProgress?.Invoke("Single Palm: Opened Info Panel");
                    }
                }
            }

            // Task Evaluation per Level
            switch (currentLevel)
            {
                case GestureLevel.Level1_PalmsOnly:
                    // Task: Open Info Panel (Single Palm) + Open Description Modal (Double Palm or Palm again)
                    if (_level1OpenedPanel && _level1OpenedDescription)
                    {
                        AdvanceToNextLevel();
                    }
                    break;

                case GestureLevel.Level2_NavSelect:
                    if (gesture == GestureType.Point && !string.IsNullOrEmpty(objectName))
                        _level2SelectedObjects.Add(objectName);
                    else if (gesture == GestureType.Swipe)
                        _level2Swiped = true;

                    if (_level2SelectedObjects.Count >= 2 && _level2Swiped)
                    {
                        AdvanceToNextLevel();
                    }
                    break;

                case GestureLevel.Level3_Pinch:
                    if (gesture == GestureType.Pinch)
                    {
                        _level3Resetted = true;
                        AdvanceToNextLevel();
                    }
                    break;

                case GestureLevel.Level4_Fist:
                    if (gesture == GestureType.Fist)
                    {
                        _level4Grabbed = true;
                        AdvanceToNextLevel();
                    }
                    break;

                case GestureLevel.Level5_Mastery:
                    break;
            }

            UpdateUI();
            SyncToProfile(); // Phase 7 (PIP) — persist task progress after every state change
        }

        /// <summary>
        /// Advances to next level with a 4-second description modal gap.
        /// </summary>
        public void AdvanceToNextLevel()
        {
            if (currentLevel >= GestureLevel.Level5_Mastery) return;

            currentLevel = (GestureLevel)((int)currentLevel + 1);

            Debug.Log($"[PGL LEVEL UP!] Advanced to Level {(int)currentLevel}: {GetLevelTitle(currentLevel)}");
            OnLevelUp?.Invoke(currentLevel);

            ShowLevelUpToast();
            ShowLevelDescriptionModal(currentLevel, 4f);
            UpdateUI();
            SyncToProfile(); // Phase 7 (PIP) — persist new level immediately
        }

        /// <summary>
        /// Resets PGL back to Level 1 and clears all in-memory task progress.
        /// Called by ProfileManager when the user holds R for 2 seconds (demo reset).
        /// </summary>
        public void ResetToLevel1()
        {
            StopAllCoroutines();
            _isTransitioning = false;

            currentLevel = GestureLevel.Level1_PalmsOnly;

            // Clear all level task flags
            _level1OpenedPanel       = false;
            _level1OpenedDescription = false;
            _level2SelectedObjects   = new HashSet<string>();
            _level2Swiped            = false;
            _level3Resetted          = false;
            _level4Grabbed           = false;

            UpdateUI();
            ShowLevelDescriptionModal(currentLevel, 4f);
            Debug.Log("[PGL] ResetToLevel1: In-memory PGL state cleared. Starting from Level 1.");
        }


        // =====================================================
        // Phase 7 (PIP) — Profile Sync
        // =====================================================

        /// <summary>
        /// Writes all current in-memory PGL state into the active UserProfile
        /// and immediately saves it to disk. Called after every task-progress
        /// change and every level advance so nothing is lost on app close.
        /// </summary>
        private void SyncToProfile()
        {
            if (ProfileManager.Instance == null) return;

            Adapter.UserProfile.UserProfile p = ProfileManager.Instance.ActiveProfile;

            p.currentLevel              = (int)currentLevel;
            p.level1OpenedPanel         = _level1OpenedPanel;
            p.level1OpenedDescription   = _level1OpenedDescription;
            p.level2SelectedObjectCount = _level2SelectedObjects.Count;
            p.level2Swiped              = _level2Swiped;
            p.level3Resetted            = _level3Resetted;
            p.level4Grabbed             = _level4Grabbed;

            ProfileManager.Instance.SaveActiveProfile();
        }

        // =====================================================
        // Description Modal & Countdown
        // =====================================================

        public void ShowLevelDescriptionModal(GestureLevel lvl, float displayDurationSeconds)
        {
            if (levelDescriptionModal == null) return;

            StopAllCoroutines();
            StartCoroutine(CoDisplayLevelDescriptionModal(lvl, displayDurationSeconds));
        }

        public void ToggleLevelDescriptionModal()
        {
            if (levelDescriptionModal == null) return;
            bool active = !levelDescriptionModal.activeSelf;
            levelDescriptionModal.SetActive(active);

            if (active)
            {
                Text t = levelDescriptionModal.GetComponentInChildren<Text>();
                if (t != null)
                    t.text = GetFullModalDescriptionText(currentLevel, 0);
            }
        }

        private IEnumerator CoDisplayLevelDescriptionModal(GestureLevel lvl, float duration)
        {
            _isTransitioning = true;
            levelDescriptionModal.SetActive(true);

            Text t = levelDescriptionModal.GetComponentInChildren<Text>();

            float remaining = duration;
            while (remaining > 0f)
            {
                if (t != null)
                {
                    t.text = GetFullModalDescriptionText(lvl, Mathf.CeilToInt(remaining));
                }
                yield return new WaitForSeconds(0.2f);
                remaining -= 0.2f;
            }

            // Close modal & end transition
            levelDescriptionModal.SetActive(false);
            _isTransitioning = false;
        }

        private string GetFullModalDescriptionText(GestureLevel lvl, int countdownSec)
        {
            string timerStr = countdownSec > 0 ? $"<i>(Starting level in {countdownSec}s...)</i>" : "<i>(Perform Open Palm to dismiss)</i>";

            switch (lvl)
            {
                case GestureLevel.Level1_PalmsOnly:
                    return $"<size=22><b>LEVEL 1: PALM DISCOVERY</b></size>\n" +
                           $"<color=#55eefd>UNLOCKED: Single Open Palm & Double Open Palm</color>\n\n" +
                           $"<b>Controls:</b>\n" +
                           $"• Single Palm → Toggle Info Panel\n" +
                           $"• Double Palm → Toggle Description Modal\n\n" +
                           $"<b>Objective:</b> Open Info Panel once + Open Description Modal once.\n" +
                           $"{timerStr}";

                case GestureLevel.Level2_NavSelect:
                    return $"<size=22><b>LEVEL 2: NAVIGATION & SELECTION</b></size>\n" +
                           $"<color=#55eefd>UNLOCKED: Point (Select) + Swipe (Navigate)</color>\n\n" +
                           $"<b>Controls:</b>\n" +
                           $"• Point → Select object\n" +
                           $"• Swipe → Navigate object selection\n\n" +
                           $"<b>Objective:</b> Select 2 objects with Point & Navigate with Swipe once.\n" +
                           $"{timerStr}";

                case GestureLevel.Level3_Pinch:
                    return $"<size=22><b>LEVEL 3: CONTROL & CANCEL</b></size>\n" +
                           $"<color=#55eefd>UNLOCKED: Pinch (Reset / Cancel)</color>\n\n" +
                           $"<b>Controls:</b>\n" +
                           $"• Pinch → Reset lab or cancel object hold\n\n" +
                           $"<b>Objective:</b> Perform 1 Pinch gesture to reset/cancel.\n" +
                           $"{timerStr}";

                case GestureLevel.Level4_Fist:
                    return $"<size=22><b>LEVEL 4: FULL INTERACTION</b></size>\n" +
                           $"<color=#55eefd>UNLOCKED: Fist (Grab & Move Objects)</color>\n\n" +
                           $"<b>Controls:</b>\n" +
                           $"• Fist (1st) → Pick up object\n" +
                           $"• Fist (2nd) → Place object at drop zone\n\n" +
                           $"<b>Objective:</b> Move 1 object to drop zone using Fist.\n" +
                           $"{timerStr}";

                case GestureLevel.Level5_Mastery:
                    return $"<size=22><b>🎉 LEVEL 5: MASTERY MODE</b></size>\n" +
                           $"<color=#55ff55>ALL 5 GESTURES UNLOCKED & ACTIVE</color>\n\n" +
                           $"<b>Congratulations!</b> You have mastered all 5 gestures.\n" +
                           $"Enjoy free interaction in the lab!\n" +
                           $"{timerStr}";

                default:
                    return "";
            }
        }

        // =====================================================
        // UI Helpers
        // =====================================================

        public void UpdateUI()
        {
            if (levelBadgeText != null)
                levelBadgeText.text = $"LEVEL {(int)currentLevel} / 5\n<size=12><color=#88ccff>{GetLevelTitle(currentLevel)}</color></size>";

            if (instructionBannerText != null)
                instructionBannerText.text = GetLevelInstruction(currentLevel);

            if (taskChecklistText != null)
                taskChecklistText.text = GetTaskChecklistText(currentLevel);
        }

        public string GetLevelTitle(GestureLevel lvl)
        {
            switch (lvl)
            {
                case GestureLevel.Level1_PalmsOnly: return "Palm Discovery (Single/Double Palm)";
                case GestureLevel.Level2_NavSelect: return "Select & Navigate (Point & Swipe)";
                case GestureLevel.Level3_Pinch:     return "Control & Reset (Pinch)";
                case GestureLevel.Level4_Fist:      return "Object Interaction (Fist Move)";
                case GestureLevel.Level5_Mastery:   return "🎉 MASTERY (All 5 Active)";
                default: return "Level " + (int)lvl;
            }
        }

        public string GetLevelInstruction(GestureLevel lvl)
        {
            switch (lvl)
            {
                case GestureLevel.Level1_PalmsOnly:
                    return "<b>Level 1:</b> Single Palm = Info Panel  |  Double Palm = Description Modal";
                case GestureLevel.Level2_NavSelect:
                    return "<b>Level 2:</b> Point to select objects  |  Swipe to navigate";
                case GestureLevel.Level3_Pinch:
                    return "<b>Level 3:</b> Pinch to reset / cancel hold";
                case GestureLevel.Level4_Fist:
                    return "<b>Level 4:</b> Fist to pick up and move objects to drop zones";
                case GestureLevel.Level5_Mastery:
                    return "<b>Level 5:</b> 🎉 All 5 Gestures Unlocked! Free Mastery Mode";
                default:
                    return "";
            }
        }

        public string GetTaskChecklistText(GestureLevel lvl)
        {
            switch (lvl)
            {
                case GestureLevel.Level1_PalmsOnly:
                    string c1 = _level1OpenedPanel ? "<color=#55ff55>✓</color>" : "<color=#ffaa44>○</color>";
                    string c2 = _level1OpenedDescription ? "<color=#55ff55>✓</color>" : "<color=#ffaa44>○</color>";
                    return $"{c1} Open Info Panel (Single Palm)  |  {c2} Open Description (Double Palm)";

                case GestureLevel.Level2_NavSelect:
                    string s1 = _level2SelectedObjects.Count >= 2 ? "<color=#55ff55>✓</color>" : "<color=#ffaa44>○</color>";
                    string s2 = _level2Swiped ? "<color=#55ff55>✓</color>" : "<color=#ffaa44>○</color>";
                    return $"{s1} Point at 2 objects ({_level2SelectedObjects.Count}/2)  |  {s2} Swipe once";

                case GestureLevel.Level3_Pinch:
                    return "<color=#ffaa44>○</color> Perform a Pinch gesture to reset";

                case GestureLevel.Level4_Fist:
                    return "<color=#ffaa44>○</color> Perform a Fist gesture to move an object";

                case GestureLevel.Level5_Mastery:
                    return "<color=#55ff55>✓ All 5 Gestures Unlocked & Mastered!</color>";

                default:
                    return "";
            }
        }

        private void ShowLevelUpToast()
        {
            if (levelUpToastBanner != null)
            {
                levelUpToastBanner.SetActive(true);
                Text t = levelUpToastBanner.GetComponentInChildren<Text>();
                if (t != null)
                {
                    string newUnlocked = GetNewlyUnlockedGestureName(currentLevel);
                    t.text = $"<size=22><b>🎉 LEVEL UP! Level {(int)currentLevel} Reached</b></size>\n" +
                             $"<color=#55eefd>UNLOCKED: {newUnlocked}</color>";
                }
                CancelInvoke(nameof(HideToast));
                Invoke(nameof(HideToast), 3.5f);
            }
        }

        private void HideToast()
        {
            if (levelUpToastBanner != null)
                levelUpToastBanner.SetActive(false);
        }

        private string GetNewlyUnlockedGestureName(GestureLevel lvl)
        {
            switch (lvl)
            {
                case GestureLevel.Level1_PalmsOnly: return "Single & Double Open Palm";
                case GestureLevel.Level2_NavSelect: return "Point (Select) + Swipe (Navigate)";
                case GestureLevel.Level3_Pinch:     return "Pinch (Reset/Cancel)";
                case GestureLevel.Level4_Fist:      return "Fist (Grab & Move)";
                case GestureLevel.Level5_Mastery:   return "All Gestures Mastered!";
                default: return "";
            }
        }
    }
}
