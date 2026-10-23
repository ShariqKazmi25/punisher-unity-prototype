# New Mixamo walking setup — 2026-10-21

Current setup: Start Walking was removed again on 2026-10-22. The current graph is Idle -> Walk -> Stop -> Idle, with Stop -> Walk restarts. See ThreeStateWalking.md for the current script and stopping profile; the four-state/start-clip instructions below are historical.

The supplied Mixamo trim totals do not equal the exported 60-fps sample counts. Unity measures Start Walking at 2.4 s, Rifle Walk at 1.533333 s, and Rifle Walk To Stop at 2.783334 s. Use imported AnimationClip.length for timing. The audit retargeted test copies to the unchanged Rifle Idle Avatar; production assets were not changed.

## Direction diagnosis

The Start Walking clip contains about 42.49 degrees of extracted root turning. Root Transform Rotation baked into the pose can preserve that turn in the visible character even while CharacterController movement is straight.

On test copies, Body Orientation with rotation NOT baked kept the body facing within about 0.1 degrees of forward. Original orientation retained a roughly 42-degree body heading offset, so Original is not the fix for this specific clip.

Manual import setup for all three clips:
- Rig: Humanoid, Copy From Other Avatar, source = the existing Rifle Idle Avatar (same skeleton required).
- Root Transform Rotation: Bake Into Pose OFF, Based Upon Body Orientation, Offset 0.
- Root Transform Position XZ: Bake Into Pose OFF, so trajectory stays in the extracted root rather than becoming pose travel. Rifle Walk was exported in place.
- Animator Apply Root Motion OFF for the current script, which owns translation and heading.
- Loop Time ON only for Rifle Walk; OFF for Start Walking and Rifle Walk To Stop.
- Rifle Walk loop range: Start 0, End 61, Loop Time ON, Loop Pose OFF. See the live investigation below; apply this manually, as production imports remain user-owned.

This extracts the clip's turning/trajectory; the script discards animation root translation and rotation and moves along camera-relative forward.

## Four-state walking graph to apply manually

Replace direct Idle -> Walk with Idle -> Start Walking. Replace the stop -> Walk restart with stop -> Start Walking. Do not leave competing walking paths active.

| Transition | iswalking | Has Exit Time | Exit Time | Duration (seconds) |
|---|---|---|---|---|
| Rifle Idle -> Start Walking | true | OFF | ignored | 0.08 |
| Start Walking -> Rifle Walk | true | ON | 0.9 | 0.1 |
| Start Walking -> Rifle Walk To Stop | false | OFF | ignored | 0.1 |
| Rifle Walk -> Rifle Walk To Stop | false | OFF | ignored | 0.1 |
| Rifle Walk To Stop -> Rifle Idle | false | ON | 0.9318182 | 0.1 |
| Rifle Walk To Stop -> Start Walking | true | OFF | ignored | 0.1 |

Starting settings: Fixed Duration ON, Transition Offset 0, Interruption Source Next State, Ordered Interruption ON if editable. Keep isrunning false during walking-only work. Review foot contact at the Start -> Walk handoff visually; a phase offset may need tuning. The durations are a tested initial fixture, not a claim of visually perfect full-scene blends.

## Script update

PunisherMovement.cs now has Walking Only enabled by default; Left Shift is ignored. Use Calibrated Walking Speed defaults ON, with 0.6222 m/s as the initial estimate from stance-foot motion on the new in-place Rifle Walk clip retargeted to the existing Rifle Idle Avatar at its current scale. This overrides the old serialized 1.5 m/s only in walking-only mode. If model scale, clip playback speed, or stride changes, remeasure/recalibrate.

The current script follows direct Idle -> Walk and Stop -> Walk blends. Walking release now follows a measured stop-clip speed profile and active blend timing, replacing the rejected 0.08-second brake. See ThreeStateWalking.md. These are speed envelopes, not exact per-foot motion warping or foot IK; residual sliding during transitions still needs visual tuning.

## Verification and limits

NewWalkingProbe.cs compiled the actual updated movement script in the isolated project with corrected copies of the three clips and the proposed four-state graph. Starts, walk loops, quick restarts, and stopping passed at 30, 60, and 144 Hz. It also checked body-facing stability with the proposed import settings.

The in-place loop's estimated median planted-foot forward drift at 1.5 m/s was compared with 0.6222 m/s; see new-walking-results.txt. This is a stance-height/foot-velocity estimate, not an exact contact solver. Gameplay Animator/import settings were not edited, and the saved gameplay graph still bypasses Start Walking until you wire the four-state graph. Full-scene/camera/terrain/foot-phase verification remains required.

## First-frame timing and setup detection
The walking-only script also caches the exported Start Walking and Rifle Walk To Stop clip lengths when the Animator is configured. It uses these durations for its initial speed ramp before Unity exposes the active transition on the next frame, avoiding the old 0.25-second start fallback. After the transition appears, timing synchronizes to its actual clip progress and offset. The script now warns once at setup if the base layer does not contain Start Walking, and explicitly keeps isrunning false during walking-only tuning.

Verification was rerun from the current files in .walking-probe with the gameplay source script and corrected test-copy imports. New walking results replace the previous report. These remain a loop stance-foot estimate and four-state test fixture, not a claim of perfect foot locking or of changes to the gameplay Animator/import settings.

## Repeating walk snap investigation — 2026-10-22
A 20-second test of a copy of the user's saved four-state Animator with iswalking continuously true observed zero walk-state time resets and zero exits after reaching Rifle Walk. Twelve ordinary loop boundaries occurred. This test did not exercise real keyboard input, CharacterController.Move, the gameplay camera, or the live imported clip artifact; it does not establish the cause of the gameplay problem.

In an isolated copy with Loop Pose OFF, the loop-boundary median rotation RMS across ten major bones was 23.9809 degrees/frame versus an ordinary median of 1.9350. Enabling Loop Pose on that test copy reduced the boundary RMS to 1.7259 degrees/frame (ordinary median 2.0537). Boundary position RMS decreased from 0.04203 to 0.00806 meters/frame. These numeric changes did not resolve the user's issue. See walk-loop-audit.txt and WalkLoopAudit.cs for this limited experiment.

The user reports the restart only in Play Mode, with Rifle Walk remaining the active state. Loop Pose is not the accepted solution. A temporary Editor-only WalkingRuntimeTrace captured 12 seconds after actual walking began: animation time, transitions, speed, controller velocity, root/model/bone positions, pose changes, and camera position. It did not set inputs or alter movement, controller, scene, or import settings. The helper was removed from Assets after capture; its source is preserved beside this guide. The live imported clip settings were observed to differ from the disk meta file; isolated tests based solely on copied meta files may therefore use different settings.

## Live capture and corrected loop range — 2026-10-22
The user reproduced the issue. Across 1,570 steady Rifle Walk samples, normalized time never reset, movement speed remained exactly 0.6222 m/s, horizontal controller velocity stayed 0.62216..0.62224 m/s, and model local position stayed fixed. At all six loop boundaries, sampled bone-angle RMS jumped 26.15..26.44 degrees/frame versus an ordinary median of 0.86 at the recording's frame rate. This confirms a pose discontinuity during continuous runtime playback, rather than repeated speed ramps or movement-root resets. See walking-runtime-trace.csv and walking-runtime-analysis.txt.

WalkRangeAudit sampled the actual source in an isolated copy with the captured import settings (Loop Pose OFF, XZ Bake ON, Rotation Bake OFF). Matching gait poses recur 61 frames apart. The current 0..92 range ends about half a gait cycle past the matching pose. Retaining Start=0 and changing End=61 preserves the current starting phase and yields a complete ~1.0167-second cycle.

A 20-second playback comparison at 60 Hz, with Loop Pose OFF in both cases, reduced median boundary bone-angle RMS from 23.9809 degrees for 0..92 to 1.7045 degrees for 0..61; ordinary motion was 1.9381 degrees/frame for the trimmed clip. Boundary root-relative position RMS decreased from 0.04244 to 0.00904 m. Manual change: Rifle Walk FBX -> Animation -> Rifle Walk -> Start 0, End 61, Loop Time ON, Loop Pose OFF -> Apply. Preserve other settings and the non-looping start/stop clips. Production movement source, Animator, scene, and imports were not changed by the agent. Full gameplay confirmation after the user's manual trim is still pending.
