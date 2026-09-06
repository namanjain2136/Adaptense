# Adaptense

Adaptense is a Unity-based virtual learning environment that utilizes MediaPipe hand tracking to teach and adapt to user gestures. It features a Context-Aware Decision Engine (CADE) and a Progressive Gesture Learning (PGL) system to guide users through complex interactions without traditional controllers.

## Key Features

- **MediaPipe Integration:** Real-time hand tracking and 3D landmark recognition via webcam.
- **Context-Aware Decision Engine (CADE):** Dynamically adjusts gesture confidence thresholds based on environmental factors (e.g., simulated lighting, distance from camera) and user success rates. Provides real-time visual hints for ambiguous poses.
- **Progressive Gesture Learning (PGL):** A 5-level structured unlock progression. Users must master basic gestures (e.g., Open Palm) before advancing to complex actions (e.g., Grab, Swipe), preventing cognitive overload.
- **Interactive Virtual Environment:** A dynamic 3D learning lab overlaid on the camera feed. Features interactable objects, snap-to drop zones, animations, and responsive UI panels.

## Architecture & Systems

- **Phase 1 & 2:** Hand tracking pipeline and core gesture recognition (Point, Pinch, Fist, Open Palm, Swipe).
- **Phase 3:** Overlaid 3D virtual environment with lifecycle-managed objects (Idle, Selected, PickedUp, Placed).
- **Phase 4:** Gesture-to-Action Mapping (Select, Navigate, Reset, Grab, UI Toggle).
- **Phase 5:** CADE integration for adaptive thresholding and intelligent fallbacks.
- **Phase 6:** PGL integration gating gestures behind task-based level unlocks.

## Setup & Requirements

1. **Unity Version:** 2021.3.22f1 LTS
2. **Dependencies:** com.github.homuler.mediapipe (v0.11.0)
3. **Hardware:** Standard webcam.

**To run the project:**
1. Open the project in Unity 2021.3.22f1.
2. Navigate to Assets/MediaPipeUnity/Samples/Scenes/Hand Tracking/ and open the Hand Tracking scene.
3. Press **Play**. The custom environment and gesture systems will be dynamically loaded over the tracking feed.

## Project Structure

- Assets/Adapter/Scripts/Gesture/: Raw gesture detection and landmark parsing.
- Assets/Adapter/Scripts/Progression/: PGL Manager and level logic.
- Assets/Adapter/Scripts/Adaptation/: Context-Aware Decision Engine and adaptation rules.
- Assets/Adapter/Scripts/Environment/: Interactable objects, drop zones, and dynamic scene loaders.
- Assets/Adapter/Scripts/Learning/: Action mapping translating gestures to object interactions.
- Assets/Adapter/Scripts/Editor/: Editor scripts for generating the Phase 6 environment automatically.

## Documentation

For a comprehensive breakdown of the algorithms, performance matrix, and design choices, please refer to:
- ADAPTENSE_PROJECT_REPORT.md
- Adaptense_Detailed_Project_Report.docx
- ADAPTER_PROGRESS.md

