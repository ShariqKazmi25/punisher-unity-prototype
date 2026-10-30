# Pelvis and abdomen stabilization

Updated 2026-10-29, after reviewing `20261029-0421-01.6473883.mp4`.

The 15-second recording shows the pelvis rocking/twisting beneath the steadier chest. The earlier shoulder fix deliberately preserved the animated pelvis and anchored the spine separately. That left the abdomen counter-bending between them, making the waist motion more conspicuous. Frame samples from the supplied recording are in `HipClipFrames/`; `hip-motion-contact-sheet.jpg` shows the observed posture across the clip.

## Current correction

During walking aim/fire, the pelvis rotation and lateral position now follow the same standing reference as the torso. The spine/chest move with the corrected pelvis, preserving the standing relationship through the abdomen. Natural forward and vertical weight transfer from the walking animation remains. Standing aim/fire and ordinary walking with the weapon layer inactive receive no pelvis correction.

Before changing the pelvis, the solver captures both animated ankle positions, foot rotations and knee directions. Two-bone leg IK then retains those foot targets using unchanged bone lengths and the original knee directions as guides. If necessary, the body lowers just enough to keep both feet reachable. This changes some thigh/knee angles as needed to support the stabilized pelvis; it does not preserve every leg joint transform. Foot placement, toe pose, clip phase, movement speed and step timing are retained.

The current sampled gait needed at most approximately **1.54 mm** of additional stance lowering. Keeping the original gait height/depth reduced knee adjustment compared with locking the entire pelvis to a fixed idle position. The rifle's live shoulder/grip/elbow targets and original AR15 recoil remain in use.

No scene, Animator graph/mask, FBX import, movement or weapon fitting values were edited. The supplied standing tune is still used directly.

## Controls

On **Rifle Hand IK → Moving Aim Posture**, existing Stabilize Moving Aim and Moving Aim Stability continue to control the whole correction. The new **Moving Pelvis Stability** defaults to **1**. Lower values retain more pelvis motion; 0 restores the earlier shoulder-only compensation. No manual setup is required for the new default.

## Verification

At **30, 60 and 144 FPS**:

- Pelvis roll range reduced from approximately **5.50° to 0.43°**; corrected pelvis rotation matches the standing reference.
- Lateral pelvis difference from standing was below floating-point measurement precision, and waist rotation/local-position relationships matched standing.
- Standing bone/rifle transforms showed zero measured changes.
- Animated ankle/toe positions differed by less than **0.0004 mm**, with zero measured foot rotation change. Leg lengths remained unchanged within floating-point precision.
- Starts/stops, moving aim/fire, aim release and changing aim direction passed.

A separate **540-frame real Play Mode** test passed without ExecuteAlways changes to production scripts. It included walking before aiming, shooting, fast movement restart, character translation/turning, camera pitch, release and disabling/re-enabling IK. The test fired 56 rounds across comparison actors. Main gameplay build: zero warnings/errors.

Four gait phases were rendered from behind and from the side, using neutral diagnostic materials, and visually reviewed. `pelvis-before-after.jpg` compares the earlier shoulder-only fix with the new correction; the source renders remain in the isolated probe project. In-game materials were not changed.

Results: `pelvis-stability-results.txt`, `pelvis-stability-play-results.txt`, `pelvis-build.log`.

Current tests: `PelvisStabilityProbe.cs`, `PelvisStabilityPlayProbe.cs`, `PelvisStabilityDriver.cs`. Earlier ShoulderStability tests/results document the shoulder-only implementation and its previous requirement that hips remain unchanged; use the new pelvis tests for the current behavior.

Backups: `RifleStandingAimPose-before-pelvis-fix.cs` and `StandingPose-before-pelvis-fix.unity`. Prior shoulder-change backups also remain. All tests and renders run in `Diagnostics/Animation/.walking-probe`, outside production Assets.
