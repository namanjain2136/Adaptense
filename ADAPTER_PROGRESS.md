# ADAPTER_PROGRESS.md

> **Antigravity: read this entire file before doing anything.**
> Find the FIRST unchecked box under "To-Do". Do only that one task.
> Do not skip ahead, do not batch multiple tasks, do not touch anything
> marked under "Hard Rules". When the task is done, update this file
> yourself following the instructions in "How to update this file" below,
> then stop and wait for the next instruction.

---

## Hard Rules (never violate)

- Unity Editor version is fixed at **2021.3.22f1 LTS**. Never upgrade to
  Unity 6 / 6.x, never suggest it.
- MediaPipe package is fixed at **com.github.homuler.mediapipe v0.11.0**.
  Never change its version, never reinstall it, never replace it with
  another framework.
- Never modify `Library/PackageCache` or
  `Packages/com.github.homuler.mediapipe`.
- Never modify the working sample at
  `Assets/MediaPipeUnity/Samples/Scenes/Hand Tracking/`.
- Never delete a dependency just to silence an error -- log it under
  "Known Issues" instead and stop for review.
- All new code/assets go under `Assets/Adapter/` only.

## Environment snapshot

- Unity: 2021.3.22f1 LTS
- MediaPipe: com.github.homuler.mediapipe v0.11.0
- Project root: C:\Users\M S P\Downloads\Adapter
- Sample confirmed working: Yes (webcam + hand landmarks, tested manually)

---

## To-Do

### Phase 1 -- MediaPipe data access
- [x] Create `Assets/Adapter/Scripts/Gesture/` folder
- [x] Read hand landmark output from the existing MediaPipe sample pipeline
      (do not reimplement tracking) -- via `HandTrackingGraph.OnHandLandmarksOutput`
- [x] Log landmark positions to Console for manual verification
      (user must open Hand Tracking scene, add LandmarkReader GO, press Play)

### Phase 2 -- Gesture recognition
- [x] Detect Open Palm (with confidence value 0-1)
- [x] Detect Fist (with confidence value 0-1)
- [x] Detect Point (with confidence value 0-1)
- [x] Detect Pinch (with confidence value 0-1)
- [x] Detect Swipe (with confidence value 0-1)
- [x] Emit a gesture event/callback per detection (no action mapping yet)

### Phase 3 -- Virtual learning environment
- [x] Create `Assets/Adapter/Scenes/Lab.unity`
- [x] Add Main Camera, Laboratory placeholder geometry
- [x] Add a few interactable Objects (beaker, book, switch)
- [x] Add empty UI canvas
- [x] Add Hand/Gesture Controller GameObject wired to Phase 2 events

### Phase 4 -- Gesture action mapping
- [x] Point -> Select object
- [x] Pinch -> Grab/interact
- [x] Open Palm -> Open info panel
- [x] Fist -> Reset/cancel
- [x] Swipe -> Navigate

### Phase 5 -- Context-aware decision engine (core novelty)
- [x] Collect gesture confidence + failed-attempt count per interaction
- [x] Track current learning activity/task state
- [x] Track basic user progress
- [x] Stub lighting/distance condition (flag if MediaPipe cannot provide it)
- [x] Build ContextAwareDecisionEngine class
- [x] Wire decision engine between Phase 2 (gestures) and Phase 4 (actions)

### Phase 6 -- Polished lab environment with pick-and-place
- [x] Add second table (Source Table on left, Destination Table on right)
- [x] Add 6 interactable objects: Beaker, Book, Flask, Battery, Lens, Sample Tube
      (distinct shapes and colors for easy identification)
- [x] Add floating name labels (TextMesh) above each object
- [x] Add floor + walls for spatial grounding
- [x] Create DropZone.cs -- semi-transparent target markers on destination table
      (pulsing cyan = empty, solid green = occupied)
- [x] Upgrade InteractableObject.cs for pick-and-place:
      - `placementTarget` (Transform) for assigned drop zone
      - `isPlaced` state tracking
      - `OnPickUp()` -- lifts object, shows ghost preview at drop zone
      - `OnPlace()` -- smoothly moves object to destination
      - Distinct visual states: Idle → Selected → Picked Up → Placed (green glow)
- [x] Upgrade GestureActionMapper.cs with toggle-pinch flow:
      - Point → cycle selection with name label highlight
      - Pinch (1st) → pick up object (lifts off table, ghost at destination)
      - Pinch (2nd) → place object at drop zone (smooth animation)
      - Fist → cancel pick-up, return object to original spot
      - Info panel shows clear state: "PICKED UP → Pinch to place, Fist to cancel"
- [x] Add on-screen score counter ("3/6 objects placed")
- [x] Add top-center status bar with live action instructions
- [x] Update LabSceneBuilder.cs to generate scene with all new objects/tables/zones
- [x] Add new LearningTask entries: TransferObjects, SortByColor, FreeExplore

### Phase 7 -- GitHub polish & documentation
- [ ] Rewrite README.md for Adaptense branding (project overview, setup, architecture)
- [ ] Add screenshots / GIFs of working interactions
- [ ] Clean up ADAPTER_PROGRESS.md formatting

---

## Completed Log

### [2026-09-04 13:00] Task: UX/UI Polish, Side-Docked Panel, 2.5s Hints & 3D Animations
Files changed:
- Assets/Adapter/Scripts/Learning/GestureActionMapper.cs [MODIFIED]
- Assets/Adapter/Scripts/Environment/InteractableObject.cs [MODIFIED]
- Assets/Adapter/Scripts/Editor/LabSceneBuilder.cs [MODIFIED]
- Assets/Adapter/Scripts/Gesture/HandLandmarkReader.cs [MODIFIED]
- Assets/Adapter/Scripts/Gesture/GestureRecognizer.cs [MODIFIED]
- ADAPTER_PROGRESS.md [MODIFIED]

Summary: 
1. Console Optimization: Silenced redundant landmark coordinate dumps and frame polling logs so the console is clean and readable.
2. Side-Docked UI: Moved the main info panel to the right side edge so it no longer obscures the user's webcam view.
3. Persistent Bottom Hint Banner: Created a dedicated bottom banner for visual hints and adaptive guidance that persists for 2.5 seconds with clear color-coded messages.
4. Smooth 3D Object Animation: Added real-time floating, elevation, and rotation animations to 3D objects when hovered and pinched/grabbed.
5. Pacing: Increased gesture cooldown to 1.2s to prevent rapid accidental triggering.

Manual Unity steps required: None (can optionally click 'Adapter > Build Phase 3 Lab Scene' to update the scene hierarchy, but all changes automatically take effect at runtime).
Verified by user: No

### [2026-09-04 12:45] Task: Phase 5 (Context-aware decision engine)
Files changed:
- Assets/Adapter/Scripts/Context/InteractionContext.cs [NEW]
- Assets/Adapter/Scripts/Context/ContextTracker.cs [NEW]
- Assets/Adapter/Scripts/Adaptation/AdaptationDecision.cs [NEW]
- Assets/Adapter/Scripts/Adaptation/ContextAwareDecisionEngine.cs [NEW]
- Assets/Adapter/Scripts/Learning/GestureActionMapper.cs [MODIFIED]
- Assets/Adapter/Scripts/Environment/GestureController.cs [MODIFIED]
- ADAPTER_PROGRESS.md [MODIFIED]

Summary: Implemented the Context-Aware Decision Engine (core novelty). ContextTracker collects multi-modal state (gesture confidence, failure counts, learning task state, progress score, landmark-based hand distance estimate, and lighting stub). ContextAwareDecisionEngine processes these factors against a 4-outcome adaptation decision matrix: Execute Normally, Make Interaction Easier (widens confidence tolerance for poor lighting/distance/struggles), Show Visual Hint (contextual guidance for ambiguous poses), and Offer Alternative Gesture (fallback when repeated failures occur). Updated GestureController and GestureActionMapper to connect Phase 2 -> Phase 5 -> Phase 4 with interactive UI hints.

Manual Unity steps required: None (automatically instantiated and wired by GestureController / LabLoader).
Verified by user: Yes

### [2026-09-04 12:00] Task: Phase 4 (Gesture action mapping)
Files changed:
- Assets/Adapter/Scripts/Learning/GestureActionMapper.cs [NEW]
- Assets/Adapter/Scripts/Environment/GestureController.cs [MODIFIED]
- ADAPTER_PROGRESS.md [MODIFIED]

Summary: Created GestureActionMapper to handle mapping Phase 2 gestures to Phase 3 environment objects. Point cycles selection, Pinch grabs, Open Palm opens Info Panel, Fist resets, and Swipe navigates. Updated GestureController to dynamically attach and wire events to this new mapper.

Manual Unity steps required: None (LabLoader automatically connects it).
Verified by user: Yes

### [2026-09-04 00:03] Task: Fix Console Spam
Files changed:
- Assets/Adapter/Scripts/Gesture/HandLandmarkReader.cs [MODIFIED]
- Assets/Adapter/Scripts/Gesture/GestureRecognizer.cs [MODIFIED]
- ADAPTER_PROGRESS.md [MODIFIED]

Summary: Changed the logging interval in GestureRecognizer from frame-based (which spam logged 4 times a second at 120+fps) to a clean time-based cooldown (0.5 seconds). Also allowed HandLandmarkReader to be fully disabled by setting its interval to 0.

Manual Unity steps required: None.
Verified by user: Yes

### [Current] Task: Phase 3 (Virtual Learning Environment)
Files changed:
- Assets/Adapter/Scripts/Environment/InteractableObject.cs [NEW]
- Assets/Adapter/Scripts/Environment/GestureController.cs [NEW]
- Assets/Adapter/Scripts/Environment/LabLoader.cs [NEW]
- Assets/Adapter/Scripts/Editor/LabSceneBuilder.cs [NEW]

Summary: Created the base interactive environment structure. I wrote a custom Editor script `LabSceneBuilder` that automatically generates the Lab scene, saving you from a ton of manual GameObject creation. 

---

## Manual Steps Pending

1. Press **Play** in the Unity Editor (in the **Hand Tracking** sample scene).
2. Test the Phase 5 Context-Aware Decision Engine:
   - **Normal Execution**: Perform Point/Pinch/Open Palm cleanly -> objects interact normally and task progress increments.
   - **Adaptive Tolerance (Make Interaction Easier)**: Stand further from the camera (or simulate Dim light in ContextTracker) -> notice the engine adaptively lowers the confidence threshold (e.g. from 0.65 to ~0.42-0.50) so gestures still succeed smoothly.
   - **Visual Hints**: Perform an ambiguous or struggling hand pose -> the Info Panel displays cyan guidance hints (e.g., "Extend index finger fully...", "Move hand closer to camera").
   - **Alternative Gestures**: Trigger repeated failures on a gesture (4+ times) -> the Info Panel displays a yellow adaptation prompt suggesting a fallback gesture (e.g., suggesting Swipe when Pointing repeatedly fails).
   - **Task Navigation**: Open the Info Panel (Open Palm) and Swipe -> advances through learning activity tasks and displays updated progress state.

---

## Known Issues / Errors
(none)
