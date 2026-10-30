# Aiming grip and crosshair alignment

Updated 2026-10-26. The fixes are in Assets/Scripts/RifleHandIK.cs and PunisherShooter.cs. The Animator graph, animation imports, movement calibration, rifle mounting transforms and saved idle IK weights are unchanged.

**Current posture tuning (2026-10-27):** see **StandingAimTuning.md**. RifleHandIK now shoulders the stock, prevents locked-out elbows, slides the aiming support wrist back 0.04 units, and provides independent aiming elbow poles and a Create Aim Pose Targets component command. These controls supersede the old unrestricted arm reach described below; the idle fit is still preserved.

## What was wrong

The saved left-hand position weight is 0.354 and rotation weight is 0.226. These partial weights retained much of each new clip's hand pose. Native torso/head IK also moved the rifle after its right-hand-parented support target had been read. Finally, looking toward the camera did not align the rifle itself: sampled poses were approximately 25–26 degrees off the crosshair direction. A simple shoulder rotation made the support target unreachable in some poses.

## Current correction

The component first lets the original animation and native IK finish. It remembers the first settled idle wrist position/orientation relative to LeftHandGrip, including the existing partial idle weights. While the Weapon Actions layer blends in, it aims the rifle from the trigger hand and solves both arm chains so both grips remain reachable. It then solves the support hand against the final rifle position for that frame. The head faces the same world target. Only upper-body bones are changed.

The idle finger curl is also retained while aiming/firing so a different Mixamo finger pose does not release the fitted weapon. All corrections blend with the weapon layer weight and stop when it fades to zero. Existing idle fitting is preserved.

The target is now the surface under the actual camera-centre crosshair, using the same self-filtered ray as the shot. This includes close-target parallax and Cinemachine's final camera placement. Camera Brain runs at order 100, final hand/aim correction at 150, and PunisherShooter's shot/effects at 200.

## Inspector settings

RifleHandIK stays on the character model that owns the Animator. Its new fields initialize automatically; no Animator rewiring is required.

| Setting | Current/default value |
|---|---|
| Left Hand Grip | Existing LeftHandGrip under ar15 |
| Position Weight / Rotation Weight | Keep saved idle values: 0.354 / 0.226 |
| Follow Camera Aim | ON |
| Preserve Idle Grip | ON |
| Preserve Idle Finger Pose | ON |
| Aiming Position Weight | 1 |
| Aiming Rotation Weight | 1 |
| Weapon Transform | Auto: LeftHandGrip.parent (ar15) |
| Local Barrel Direction | (0, 0, -1), verified from the actual asset |
| Local Head Forward | (0, 0, 1), for this Mixamo model |
| Max Aim Correction | 85 degrees |
| Create AR15 Muzzle | ON |
| AR15 Muzzle Local Position | (0.00055, 0.68964, -5.96547) |

If no Muzzle was assigned, the helper creates one under this ar15 at runtime and assigns it to PunisherShooter. The local coordinates come from the actual scaled/skinned asset's barrel tip, not its overall bounds. An explicitly assigned barrel-tip marker takes precedence. Barrel Tip can be assigned independently too. These coordinates are specific to this AR15 import; another weapon should have its own correctly positioned Muzzle and local barrel axis.

Move/rotate LeftHandGrip to refine the support wrist. Preserve Idle Grip retains the working idle wrist offset; switch it OFF if the marker itself should be the exact wrist target. Preserve Idle Finger Pose can be switched OFF to audition the new clips' own finger motion. The first settled idle pose is captured anew when entering Play Mode. If gameplay begins immediately in aim without an idle reference, the marker itself is used until an idle reference is available.

## Preview and firing animations

Evaluate this correction in **Play Mode**. An FBX/Animator Inspector preview displays the source animation without these gameplay scripts, camera target or scene rifle/grip setup, so the original leftward cant can still be visible there.

The AR15's **Armature.003|shooting** clip (0.291667 seconds) now plays on every accepted shot, using its own runtime animation player. Character recoil still uses the Mixamo Firing Rifle/Firing Rifle2 clips, and code supplies hitscan, damage and basic effects. Reload and movement clips from the weapon are not enabled. See **AR15FiringAnimation.md** for the source reference, timing and playback setup.

The new gun animation player moves LeftHandGrip and Muzzle under Armature.003/Bone at runtime, preserving their world placement so they follow the rifle body's recoil. The root-coordinate Muzzle field above is still used to create the marker before reparenting. Final barrel alignment now follows Muzzle.forward, which includes the built-in animation's recoil rotation.

## Verification

Tests use a prefab snapshot of the live character, rifle mount and grip, with the actual saved controller/imports and the source scripts. At 30, 60 and 144 FPS: idle wrist difference from the old IK was zero; settled aim/firing grip error stayed below 0.002 mm; measured barrel/head error stayed below 0.03 degrees in the sampled poses. Vertical aim at -70/-30/+30/+70 degrees and a surface about 0.9 units ahead passed. Final arm correction caused zero foot displacement. Standing/moving aim, moving/standing fire, release and missing-reference checks passed. The existing shooting/graph/camera regressions also passed. Full project build: zero warnings/errors.

See aim-fit-results.txt and shooter-results.txt. aim-before.png and aim-after.png are diagnostic renders using simplified materials, not gameplay screenshots. Finger contact/stock placement can still be visually refined using the controls above; they do not implement physical finger collision.
