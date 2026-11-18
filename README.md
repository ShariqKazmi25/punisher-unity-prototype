# Punisher Unity Prototype

An unofficial third-person gameplay prototype in Unity. The Micro Lair scene combines a playable character rig and Cinemachine shoulder camera with an industrial interior, upper walkways, and steel-mullion windows.

![Cutaway of the Micro Lair](Documentation/MicroLair/revision-e-cutaway.png)

## Open the project

1. Install Git LFS and run `git lfs install` then clone the repository. If you already cloned it, pull the latest commit and run `git lfs pull` to download the large textures.
2. Add the repository root to Unity Hub. Open it with **Unity 6000.6.0f1**, as recorded in [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt). Unity resolves the dependencies in [Packages/manifest.json](Packages/manifest.json).
3. Open [MicroLair_Graybox.unity](Assets/Scenes/MicroLair_Graybox.unity) by itself and press Play. [SampleScene.unity](Assets/Scenes/SampleScene.unity) contains the original gameplay setup.

## Controls

| Input | Action |
| --- | --- |
| W | Walk forward |
| Mouse | Turn and look |
| Right mouse button | Aim |
| Left mouse button | Fire |
| Esc | Release the cursor. Click the Game view to capture it again. |

## Project layout

- [Assets/Scripts](Assets/Scripts) contains the character controller, camera, animation, hand IK, and shooting logic.
- [Assets/Environment/MicroLair](Assets/Environment/MicroLair) contains the Micro Lair meshes, materials, and editor tools.
- [Documentation/MicroLair](Documentation/MicroLair) records the [gameplay setup](Documentation/MicroLair/gameplay-setup.md), [level layout](Documentation/MicroLair/revision-e.md), [concrete floor](Documentation/MicroLair/concrete-floor.md), and [steel-mullion windows](Documentation/MicroLair/steel-mullion-windows.md).

## History

The project was imported from Unity Version Control. The imported commit messages reference the original changeset numbers. The [earlier portfolio snapshot](https://github.com/ShariqKazmi25/punisher-unity-prototype/tree/a915e737276a37c7108d34d6b69d315c75fe3f78) remains in the Git history.
