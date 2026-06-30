#AI_S&R robot simulation

A Unity robotics/demo project built in Unity 2022.3.60f1. This workspace contains autonomous and teleoperation systems for a two-wheeled agent, including pathfinding, victim detection, audio/visual sensing, and camera follow behavior.

## Key Features

- Autonomous two-wheel drive with path following and anti-drift stabilization (`StableTwoWheelDrive.cs`)
- High-level state machine for patrol, audio investigation, area search, victim examination, and manual teleoperation (`BotBrain.cs`)
- Smooth third-person camera follow support (`TeleopCamera.cs`)
- Modular sensory systems for audio and visual detection (`AudioSensorySystem.cs`, `VisualSensorySystem.cs`, `VictimDetection.cs`)
- Support for manual drive control and operator override
- Editor gizmos for wheel alignment and visual debugging

## Unity Version

- Unity Editor: `2022.3.60f1`

## Packages Used

The project includes standard Unity modules plus the following packages:

- `com.unity.ai.navigation` 1.1.7
- `com.unity.cloud.gltfast` 6.14.1
- `com.unity.collab-proxy` 2.12.4
- `com.unity.feature.development` 1.0.1
- `com.unity.textmeshpro` 3.0.7
- `com.unity.timeline` 1.7.6
- `com.unity.ugui` 1.0.0
- `com.unity.visualscripting` 1.9.4

## Project Structure

- `Assets/code/` - core robot behavior scripts
- `Assets/` - prefabs, materials, models, and other scene assets
- `Packages/manifest.json` - package manifest for Unity package manager
- `ProjectSettings/` - Unity project settings

## Setup & Usage

1. Open `My project (3).sln` or the root folder in Unity Hub.
2. Open the project in Unity Editor `2022.3.60f1`.
3. Add the main robot GameObject and attach the following scripts/components:
   - `StableTwoWheelDrive`
   - `PurePathfinder` (required by `StableTwoWheelDrive`)
   - `BotBrain`
   - `VictimDetection` / `AudioSensorySystem` / `VisualSensorySystem`
4. Configure the robot's `target` transforms in the Inspector (`patrolCarrot`, `audioCarrot`, `visualCarrot`).
5. Use `T` to switch to manual control and `P` to resume autonomous patrol.

## Notes

- `StableTwoWheelDrive` freezes X/Z rotation on the Rigidbody to prevent tipping.
- Autonomous navigation only drives when the agent is aligned with the waypoint.
- `BotBrain` transitions automatically based on audio events, victim detection, and operator input.

## Behavior Logic

- `BotBrain` implements a finite state machine with states: `Patrolling`, `InvestigatingAudio`, `SearchingArea`, `ExaminingVictim`, and `ManualControl`.
- In `Patrolling`, the robot follows a patrol target using pathfinding and keeps vision inactive to reduce processing.
- When audio input triggers an event, `BotBrain` switches to `InvestigatingAudio`, activates vision, and sends the robot toward the reported sound source.
- If a victim is detected as horizontal or unconscious, the state changes to `ExaminingVictim`, and the robot approaches the victim to stop and request help.
- Manual control can be toggled with `T` to take over driving and `P` to resume autonomous behavior.

## Code and Model Architecture

- `StableTwoWheelDrive.cs` uses Unity physics and a simple steering model:
  - it applies forward force only when the robot is roughly aligned with the next path waypoint,
  - uses lateral anti-drift forces for stability,
  - and rotates visual wheel meshes by converting forward displacement into wheel rotation.
- `PurePathfinder.cs` is a lightweight wrapper around `NavMesh.CalculatePath`.
  - It samples `NavMeshPath.corners` as waypoints,
  - keeps the robot on a flat plane by flattening Y coordinates,
  - and advances to the next waypoint once it is within a configurable tolerance.
- `VictimDetection.cs` and `VisualSensorySystem.cs` both connect to external vision services over TCP.
  - They capture camera frames, encode them as JPEG, send them to a backend, and parse detection responses.
  - `VictimDetection` converts a detected screen-space box into a world-space location using a raycast from the camera.
  - `VisualSensorySystem` is explicitly optimized for a 640×640 inference resolution and mentions YOLO-style detection, indicating a bounding-box object detection model on the backend.
- `AudioSensorySystem.cs` records in-game audio buffers, converts them to WAV format, and posts them to an HTTP service at `/analyze-audio`.
  - The backend returns a trigger result and text, and when a trigger is detected, the system localizes the likely sound source in the Unity scene.

## Models Used

- Vision: an external object detection model, likely YOLO-style, that returns bounding boxes, confidence, and whether a person is downed/horizontal.
- Audio: an external audio classification/trigger detection model hosted behind a REST endpoint that analyzes buffered game audio and reports if an event should be investigated.

## Contact

If you want help extending this project, inspect the scripts in `Assets/code/` and configure the scene objects accordingly.
