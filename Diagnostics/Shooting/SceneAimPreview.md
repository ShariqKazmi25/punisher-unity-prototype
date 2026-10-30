# Live Scene view aim tuning

The old workflow had two independent problems: pausing Play Mode stops the per-frame Animator/IK evaluation, while moving focus away from Game view normally releases aim. A new Editor-only **Preview Aim In Scene View** switch holds the aiming pose without requiring right click and leaves Play Mode running.

## Use it

1. Enter **Play Mode** and leave **Pause OFF**. Start from idle so the existing hand fit can be captured. Press **Esc** to release the captured game mouse.
2. Select **punisher_vest@Rifle Idle** in the Hierarchy. In **RifleHandIK → Editor Aim Preview**, enable **Preview Aim In Scene View**.
3. Switch to **Scene** view. The character keeps aiming, the game camera keeps its current aiming direction/zoom, and you can orbit the Scene camera freely. The animation and IK continue updating.
4. Move **StockShoulderTarget**, **LeftAimElbowTarget** or **RightAimElbowTarget**, or change the corresponding unassigned-target offset fields. The resulting pose updates live. Support Hand Slide Back, elbow bend ranges and IK weights also update while the game runs.
5. Disable **Preview Aim In Scene View** when returning to normal gameplay. Click the Game view and release that capture click before firing again.

The preview releases the cursor and blocks gameplay mouse/look/movement input through the existing camera input gate. Inspector clicks, gizmo drags and Scene navigation do not shoot. It is a Play Mode editing aid; player builds ignore it. It does not automatically unpause a paused editor.

## If a change still appears to do nothing

- **Assigned targets override offsets.** When Shoulder Anchor is assigned, move StockShoulderTarget; Shoulder Offset is then the fallback only. The same rule applies to the left/right aiming elbow hints and offsets.
- **Move targets rather than animated bones.** The Animator/IK solve rewrites arm bones every frame. The three target objects and the RifleHandIK settings are the tuning inputs.
- **Hints choose bend direction.** For a more bent left arm, increase Support Hand Slide Back or Minimum Left Elbow Bend. Moving only the elbow pole cannot shorten an arm with a fixed wrist target.
- **Keep Play running.** Pausing stops the live solve. If the toolbar Pause button is active, unpause it first.

If the target objects do not exist yet, use the RifleHandIK component menu's **Create Aim Pose Targets**. Edit Mode creation persists and supports Undo. Creation during Play Mode is also supported, but the new objects are temporary until recreated/saved in Edit Mode.

## Preserve the changes

Play Mode changes reset on exit. For targets created in Edit Mode, record/copy their changed **local Positions** before stopping, then apply those values to the same Edit Mode targets and save the scene. For component settings, Copy Component while playing, then Paste Component Values onto the existing component after stopping; turn the preview checkbox OFF before saving the normal setup. See StandingAimTuning.md for the pose controls.

## Verification

A test actually entered and exited Play Mode in an isolated copy of the project. With no Game-view input, preview held aim/zoom, left the cursor free and kept the gameplay aim direction unchanged. Editing the right elbow target while unpaused moved the elbow by about 0.015 m, and the Animator's playback time continued advancing. Preview clicks produced no shots. Turning the switch off restored the idle/lens state and required mouse-button release before normal fire rearmed. See scene-aim-preview-results.txt.
