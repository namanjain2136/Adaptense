// ============================================================
// ADAPTER Project — Phase 7 (Persistent Identity Profile — PIP)
// ProfileManager.cs
//
// PURPOSE:
//   MonoBehaviour singleton that owns save/load of the learner's
//   UserProfile to disk. Uses JsonUtility + Application.persistentDataPath.
//   No external packages required.
//
// STEP 2 (isolation): Loads profile on Awake, increments sessionCount,
//   saves immediately, and Debug.Logs all fields so the user can verify
//   in Play mode before Step 3 wiring begins.
//
// SAVE PATH:
//   Application.persistentDataPath/adapter_profile.json
//   (NOT inside Assets/ — that is not writable in a built player)
// ============================================================

using System;
using System.IO;
using UnityEngine;
using Adapter.UserProfile;

namespace Adapter.UserProfile
{
    public class ProfileManager : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────
        public static ProfileManager Instance { get; private set; }

        // ── Public accessor for the active profile ─────────────────────
        public UserProfile ActiveProfile { get; private set; }

        // ── Save file path ─────────────────────────────────────────────
        private string SavePath => Path.Combine(Application.persistentDataPath, "adapter_profile.json");

        // ── Reset shortcut state (Step 5 wired here already for safety) ─
        private float _resetHoldTimer = 0f;
        private const float ResetHoldDuration = 2f;

        // ==============================================================
        // Unity Lifecycle
        // ==============================================================

        private void Awake()
        {
            // Singleton enforcement — persist across scene loads
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Load profile immediately on Awake
            ActiveProfile = LoadProfile();

            // Increment session count and timestamp, then save right away
            // so sessionCount is correct even if the app is force-closed later.
            ActiveProfile.sessionCount++;
            ActiveProfile.lastUsedTimestamp = DateTime.UtcNow.ToString("o");
            SaveProfile(ActiveProfile);

            // ── Step 2 debug log — remove/comment after confirming ──────
            Debug.Log(
                $"[PIP ProfileManager] ===== PROFILE LOADED =====\n" +
                $"  Profile ID      : {ActiveProfile.profileId}\n" +
                $"  Current Level   : {ActiveProfile.currentLevel}\n" +
                $"  Sensitivity     : {ActiveProfile.gestureSensitivity:F2}\n" +
                $"  Session Count   : {ActiveProfile.sessionCount}\n" +
                $"  Last Used (UTC) : {ActiveProfile.lastUsedTimestamp}\n" +
                $"  L1 Panel        : {ActiveProfile.level1OpenedPanel}\n" +
                $"  L1 Description  : {ActiveProfile.level1OpenedDescription}\n" +
                $"  L2 SelectedObjs : {ActiveProfile.level2SelectedObjectCount}\n" +
                $"  L2 Swiped       : {ActiveProfile.level2Swiped}\n" +
                $"  L3 Resetted     : {ActiveProfile.level3Resetted}\n" +
                $"  L4 Grabbed      : {ActiveProfile.level4Grabbed}\n" +
                $"  Save path       : {SavePath}\n" +
                $"=========================================="
            );
        }

        // ==============================================================
        // Public API
        // ==============================================================

        /// <summary>
        /// Reads the profile JSON from disk. Returns a fresh default profile
        /// if the file doesn't exist or fails to parse.
        /// </summary>
        public UserProfile LoadProfile()
        {
            if (!File.Exists(SavePath))
            {
                Debug.Log($"[PIP ProfileManager] No save file found at '{SavePath}'. Creating default profile.");
                UserProfile fresh = UserProfile.CreateDefault();
                SaveProfile(fresh);
                return fresh;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                UserProfile loaded = JsonUtility.FromJson<UserProfile>(json);

                if (loaded == null || string.IsNullOrEmpty(loaded.profileId))
                {
                    Debug.LogWarning("[PIP ProfileManager] JSON parsed but profile was null/empty. Falling back to default.");
                    UserProfile fallback = UserProfile.CreateDefault();
                    SaveProfile(fallback);
                    return fallback;
                }

                Debug.Log($"[PIP ProfileManager] Profile loaded from disk. ID={loaded.profileId}, Level={loaded.currentLevel}, Session#{loaded.sessionCount}");
                return loaded;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PIP ProfileManager] Failed to load profile: {ex.Message}. Falling back to default.");
                UserProfile fallback = UserProfile.CreateDefault();
                SaveProfile(fallback);
                return fallback;
            }
        }

        /// <summary>
        /// Serializes the profile to JSON and writes it to
        /// Application.persistentDataPath/adapter_profile.json.
        /// Wrapped in try/catch — never crashes the app on failure.
        /// </summary>
        public void SaveProfile(UserProfile profile)
        {
            if (profile == null)
            {
                Debug.LogError("[PIP ProfileManager] SaveProfile called with null profile. Skipping.");
                return;
            }

            try
            {
                string json = JsonUtility.ToJson(profile, prettyPrint: true);
                File.WriteAllText(SavePath, json);
                // Uncomment the line below for verbose save logging during development:
                // Debug.Log($"[PIP ProfileManager] Profile saved. Path={SavePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PIP ProfileManager] Failed to save profile: {ex.Message}");
                // Do NOT rethrow — a save failure must never crash the app.
            }
        }

        /// <summary>
        /// Convenience: saves the currently active profile.
        /// Call this after modifying any field on ActiveProfile.
        /// </summary>
        public void SaveActiveProfile()
        {
            SaveProfile(ActiveProfile);
        }

        /// <summary>
        /// Wipes the save file and resets in-memory state to a fresh default.
        /// Called by the R-hold reset shortcut (Step 5) and any future reset UI.
        /// </summary>
        public void ResetProfile()
        {
            try
            {
                if (File.Exists(SavePath))
                    File.Delete(SavePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PIP ProfileManager] Could not delete save file: {ex.Message}");
            }

            ActiveProfile = UserProfile.CreateDefault();
            SaveProfile(ActiveProfile);
            Debug.Log("[PIP ProfileManager] Profile RESET to default. Level=1, all progress cleared.");
        }

        // ==============================================================
        // Step 5 — R-hold reset shortcut (already wired here)
        // ==============================================================

        private void Update()
        {
            if (Input.GetKey(KeyCode.R))
            {
                _resetHoldTimer += Time.unscaledDeltaTime;

                if (_resetHoldTimer >= ResetHoldDuration)
                {
                    _resetHoldTimer = 0f; // prevent repeated triggers
                    ResetProfile();

                    // Tell Phase 6 to reset its in-memory level as well
                    // (safe to call even before Step 4 — FindObjectOfType returns null gracefully)
                    var pgl = FindObjectOfType<Adapter.Progression.GesturePGLManager>();
                    if (pgl != null)
                    {
                        pgl.ResetToLevel1();
                    }
                }
            }
            else
            {
                _resetHoldTimer = 0f;
            }
        }
    }
}
