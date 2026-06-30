# My Project (3)

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

## Contact

If you want help extending this project, inspect the scripts in `Assets/code/` and configure the scene objects accordingly.
