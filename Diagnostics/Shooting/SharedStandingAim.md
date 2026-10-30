# Conserved standing pose while walking

2026-10-29: Additional compensation now removes inherited walking-pelvis sway above the hips. See **ShoulderStability.md** for the current implementation, controls and checks. The standing fitting below remains conserved.

Updated 2026-10-27. Your live standing setup was captured before editing. The Animator now uses the same standing upper-body aim/fire motions during walking. The existing walking layer still supplies the legs.

## Conserved tuning

The scene values, referenced target/grip/gun transforms and runtime IK/movement source matched exactly before and after the edit. The scene save succeeded and its file contents stayed identical. Full settings: **UserStandingPosePreset.json**; full reference/transform report: **user-standing-conserved-settings.txt**.

| Setting | Your value |
|---|---:|
| Idle Position / Rotation Weight | 0 / 0.226 |
| Aim Body / Head Weight | 1 / 0 |
| Aiming Position / Rotation Weight | 1 / 0.063 |
| Support Hand Slide Back | 0.0175 |
| Aiming Elbow Hint Weight | 0.769 |
| Left elbow bend min / max | 82 / 95 |
| Right elbow bend min / max | 9.4 / 127.7 |
| Max Aim Correction | 25 |

Targets remain under mixamorig:Spine2 with identity local rotation and unit scale:

| Target | Local Position |
|---|---|
| StockShoulderTarget | (0.0892, 0.0973, 0.0477) |
| LeftAimElbowTarget | (-0.09, -0.042, 0.101) |
| RightAimElbowTarget | (0.15, -0.069, 0.012) |

LeftHandGrip remains under ar15 at local Position (0.0127, -0.0273, -0.0048), Quaternion (0.9709675, 0, 0, -0.2392118). The rifle mount position/rotation/scale are unchanged. Existing runtime reparenting to the animated rifle body is retained.

## Fix

Previously, W switched the upper body from Rifle Aiming Idle to Walking, or from Firing Rifle2 to Firing Rifle. Those clips have different torso/arm poses and moved the chest-parented tuning targets. Each switch also changed the upper animation phase.

Weapon Actions now has three states: Weapon Lowered, Rifle Aiming Idle (standing and moving aim), and Firing Rifle2 (standing and moving fire). Its six transitions use only isaiming/isfiring. The standing motions, state IDs, speeds, blend durations, offsets and interruption settings were retained. W still drives the original locomotion Base Layer, and no longer switches/restarts the upper-body pose. The old moving clip assets remain available in the project.

The AR15 Armature.003|shooting clip still plays on each accepted round. Small gait motion and IK compensation remain as the body walks; the gun is not frozen in world space.

## Verification and backups

The actual saved controller and your captured actor/settings passed at 30/60/144 FPS, with aim, walking, standing/moving fire and the original rifle shooting clip. Standing bone/gun transforms changed by zero; walking legs changed by zero. W caused zero upper-state changes and no normalized-time resets. Gun-relative-to-chest deviation during walking dropped from about 4.9 cm / 16.8 degrees to below 0.9 cm / 3 degrees in the sampled cases. All raw input combinations, rapid interrupts, held fire, damage/cover and camera checks also passed.

Results: shared-standing-aim-results.txt and shooter-results.txt. Exact transitions: ShooterTransitions.md.

Backups outside Assets: UserStanding-before-walk-fix.unity, UserStanding-conserved.unity, UserStandingLiveFixture.prefab, UserStandingConservedFixture.prefab, Punisher-before-shared-aim.controller and Punisher-live-before-shared-aim.controller.
