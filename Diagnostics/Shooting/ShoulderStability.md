# Stable shoulders during walking aim and fire

Updated 2026-10-29.

**Superseded later that day:** The shoulder-only compensation below left visible pelvis/abdomen sway. The current correction steadies the pelvis and torso together and retains animated footfalls with leg IK. See **PelvisStability.md** for current behavior, controls and verification. Statements below about leaving the hips/leg joints unchanged describe the earlier implementation.

The standing upper-body clips already remained active during walking, but their spine still inherited the animated pelvis's sway. The fix compensates above the pelvis, before the existing barrel, stock and arm IK solve.

`RifleHandIK` now uses a hidden, transform-only standing reference rig. It samples the existing standing Animator poses with the same upper-state phases, aim/fire transition progress, layer weight, native look-at settings and grip fitting. The spine and upper bones blend toward that reference during locomotion. Walking hips and legs remain entirely under the original Animator. Standing frames receive no posture correction.

No scene, Animator controller, avatar mask, animation import, movement speed, step sync or user fitting values were edited. The original AR15 shooting/recoil animation and the existing shoulder/grip/elbow targets remain active. The current unsaved scene was left open without saving it.

## Controls

On the character model's **Rifle Hand IK → Moving Aim Posture**:

- **Stabilize Moving Aim:** enabled by default.
- **Moving Aim Stability:** **1** by default, matching the standing upper-body pose. Lower values retain more of the walking sway; 0 bypasses compensation.

No manual component or Animator wiring is needed. The reference rig contains bones and an Animator only, with no graphics, physics, gun or gameplay behaviours. It is cleaned up when the owning IK component is disabled.

## Verification

The final code passed pose comparisons at **30, 60 and 144 FPS**. Standing bones/rifle and moving hips/legs showed zero measured changes. During sampled steady walking aim/fire, chest position/rotation matched the standing reference to floating-point precision. Shoulder roll range fell from approximately **7.2° to 1.4°**, retaining the standing animation's natural motion.

A separate **540-frame real Play Mode test**, using the production callbacks without ExecuteAlways modifications, passed walking before aiming, standing/moving fire, quick walking restarts, changing camera pitch, character translation/heading, aim release and IK disable/re-enable. During the steady moving comparison, chest and rifle matched standing to floating-point precision; 56 accepted shots across the comparison actors exercised the original rifle animation. Existing shooting/cover/damage/camera and Animator regressions passed at 30/60/144 FPS. The main gameplay assembly built with zero warnings/errors.

Results: `shoulder-stability-results.txt`, `shoulder-stability-play-results.txt`, `shooter-post-stability-results.txt`, `shoulder-build.log`.

All simulation tests run on isolated copies in `Diagnostics/Animation/.walking-probe`; only two production scripts were changed/added: `RifleHandIK.cs` and `RifleStandingAimPose.cs` (plus the new script metadata). Visual preference can be adjusted with Moving Aim Stability.

Backups: `RifleHandIK-before-shoulder-stability.cs`, `StandingPose-before-shoulder-stability.unity`, `Punisher-before-shoulder-stability.controller`. Existing standing fitting values remain documented in `UserStandingPosePreset.json`; the live scene values continue to be used without replacement.
