# Micro lair gameplay setup

The environment now uses [revision E](revision-e.md), with the upper floor extended to both side walls, a larger lower front area, a taller ceiling and longer corridors. This structural revision preserves the gameplay rig and its settings. Current layout checks are in `revision-e-validation.txt`.

Open `Assets/Scenes/MicroLair_Graybox.unity` by itself and press Play. Click the Game view to capture the mouse if necessary.

- W: walk forward, using the existing rifle-walk animation.
- Mouse: turn the character and camera.
- Right mouse button: aim.
- Left mouse button: fire (hold for repeated fire).
- Escape: release the cursor; click the Game view to recapture it.

These are the current SampleScene controls. Its walking-only controller does not implement A/S/D strafing or backward movement. The original gameplay scripts and assets are shared, so future changes to those scripts or animation assets affect both scenes.

`Gameplay_Punisher` contains independent copies of the complete `rigger` hierarchy (Punisher, AR15, hand IK and aim targets), `Main Camera` with Cinemachine Brain, and `thirdper` virtual camera. All cross-root scene references are remapped to the copied objects. A serialized PunisherShooter component makes its camera and Animator references explicit; this is the same component the source movement script normally adds at runtime. The character retains the source scale and 0.77-unit controller height. He starts on the lower floor facing the right staircase.

The destination's editor aim-preview flag is off, so it does not suppress player input. The copied camera retains its source follow and aiming settings, with obstacle avoidance enabled, 0.08-unit collision radius, and a 0.04-unit near clip for the small interior. The player colliders use the built-in Player tag so camera collision ignores the character. The fixed inspection camera and its AudioListener are disabled; neutral graybox work lights remain on. The optional static scale-reference model remains inactive.

The source SampleScene is unchanged. The destination before gameplay is backed up at `Diagnostics/MicroLair/MicroLair_Graybox_before_gameplay.unity`. Reference checks are in `gameplay-validation.txt`; the Play-mode check is recorded separately in `gameplay-playtest.txt`.

Gameplay integration was verified in Play mode on 10 November, before the revision C structural expansion: the copied camera became the active camera, the player settled on the lower floor, W input drove the walking animation up both staircases, RMB/LMB input aimed and fired eight shots into the graybox walls, and the player remained grounded after stopping on the upper deck. Camera collision kept the camera inside the front wall at spawn. The automated check used temporary virtual input devices and fixed the mouse heading during the route; the test helpers and their play-only changes were removed afterward. Normal mouse-look remains enabled in the saved scene. Revision C has separate floor, capsule, headroom and shooting-lane physics checks in `revision-c-validation.txt`.

`Tools > Micro Lair > Copy Sample Scene Gameplay` is a guarded editor authoring command. It does not add another rig if one already exists. `Validate Gameplay References` checks the copied references and active camera/listener counts. No automatic importer or generator is needed to play the saved scene.
