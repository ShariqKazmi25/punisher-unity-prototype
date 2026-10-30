# Current shooting and aiming setup

Updated 2026-10-27. Punisher.controller contains the original three-state locomotion Base Layer and a three-state Weapon Actions layer shared by standing and walking. Your latest standing tuning is conserved in the scene and UserStandingPosePreset.json; see SharedStandingAim.md. Rifle Down To Aim and Rifle Aim To Down remain unused.

The subsequent grip/aim correction is described in **AimGripSetup.md**: preserves the working idle fit, solves both arms against the final rifle pose, aligns barrel/head to the camera-centre target and creates an AR15 barrel-tip Muzzle at runtime when none is assigned.

The original AR15 **Armature.003|shooting** clip now also runs on each accepted shot. It is configured automatically from Assets/Resources/Weapons/AR15Animation.asset and plays alongside the Mixamo character firing poses. See **AR15FiringAnimation.md**.

## What changed and why

The live, unsaved graph was inspected before editing and backed up as Punisher-before-direct-aim.controller. It had shooting/aiming states mixed into the locomotion layer. Standing fire from Idle required isaiming=false, although the script makes isaiming true while firing. Moving fire could immediately return to Rifle Walk whenever W was held. Several movement/fire/release combinations had no route, and aim release waited for an exit time.

Weapon poses now use an upper-body Override layer so these transitions cannot replace the calibrated legs. Base Layer still owns Rifle Idle, Rifle Walk and Rifle Walk To Stop, with their original four transitions, durations, offsets, and exit timing. PunisherMovement.cs, walk speed 0.6222 m/s, stop profile, animation imports, weapon placement and grip weights were preserved.

## Controls and parameters

Hold LMB to fire, RMB to aim/zoom, W to move. W+LMB keeps the standing upper-body firing pose over walking legs; W+RMB keeps the standing aiming pose over walking legs. Both buttons work together. Esc releases the cursor; the first capture click is consumed and must be released before firing. RMB controls normal zoom; Editor Scene Aim Preview also holds aim/zoom for tuning.

| Input | iswalking | isaiming | isfiring |
|---|---|---|---|
| None | false | false | false |
| W | true | false | false |
| RMB | false | true | false |
| W + RMB | true | true | false |
| LMB, with or without RMB | false | true | true |
| W + LMB, with or without RMB | true | true | true |

All four parameters (including isrunning) are Bools. isaiming means weapon readiness: RMB or firing/recoil hold. A tap keeps the firing pose for the remainder of the 0.2-second standing recoil window. isrunning stays false while Walking Only is enabled. One script owns each parameter: movement writes walking/running; shooter writes aiming/firing.

## Weapon Actions layer

Override, Sync OFF, initial Weight 0, IK Pass ON. PunisherShooter blends its weight to 1 while the weapon is ready and back to 0 over Layer Blend Time (default 0.1 seconds) on release, without waiting for a lowering state. Base Layer IK Pass remains ON.

RifleUpperBody.mask enables Body, Head, both Arms, Fingers and Hand IK. Root, Legs and Foot IK are disabled; there is no additional transform mask. The new clips supply the torso/arms, while the original walking gait supplies the legs.

| State | Motion | Purpose |
|---|---|---|
| Weapon Lowered (default) | Existing Rifle Idle | Neutral fallback, not a lowering clip |
| Rifle Aiming Idle | Rifle Aiming Idle | Standing and moving aim |
| Firing Rifle2 | Firing Rifle2 | Standing and moving fire |

All three states: Speed 1, Cycle Offset 0, Write Defaults OFF, no speed/time parameters, Foot IK OFF. Existing imported clips are looping Humanoid animations using the original Rifle Idle Avatar. Separate moving upper-body assets are retained but unused; no FBX import settings were changed.

## Transitions and tuning

There are six explicit transitions: each weapon state can reach the other two. Conditions are mutually exclusive and cover every aim/fire combination. Walking no longer switches upper-body states. No Any State or self transitions are used, so held fire loops without repeatedly restarting.

For every weapon transition: Has Exit Time OFF; Fixed Duration ON; Offset 0; Interruption Source Next State; Ordered Interruption ON; Can Transition To Self OFF. Transitions into firing use 0.04 seconds; transitions into aim or neutral use 0.1 seconds. Exact per-arrow conditions are in ShooterTransitions.md.

Tune these weapon blend durations and PunisherShooter.Layer Blend Time for visual feel. The existing Base Layer walk timing is independently calibrated. Changing base walking playback, offsets, trim or stop timing would require recalibrating movement; upper-body weapon blends do not.

## Components and gameplay

PunisherMovement adds PunisherShooter at runtime if absent. Add it to rigger in Edit Mode to save custom Inspector references/settings. References resolve automatically. A child named Muzzle at the barrel tip is used if present. RifleHandIK now creates a measured barrel-tip Muzzle for this AR15 at runtime when none is assigned; characters without this helper/marker use the right-hand fallback. A muzzle marker can be assigned explicitly.

Defaults: 5 rounds/sec, 25 damage, 150-unit range. Shots use a camera-centre aim ray, then muzzle-to-target and chest-to-muzzle obstruction checks. Player colliders are ignored. Optional ParticleSystem and AudioSource fields support supplied effects; a basic tracer, flash light and impact marker work by default. RifleDamageableTarget gives a collider target 100 health; other enemies can implement IRifleDamageable.TakeDamage(float).

CameraTarget's ThirdPersonCameraController turns the character with mouse yaw even while stationary. RMB blends camera distance to 60%, FOV to 45 and near clip to 0.05 over 0.18 seconds, then restores them. Current distance 0.5 becomes 0.3. RifleHandIK retains the left-hand grip settings and supports camera body/head aim on the final weapon layer.

## Verification and visual review

The actual saved controller, mask, clip imports and scripts were tested on copies at 30, 60 and 144 FPS. All input combinations, every state pair, rapid interruptions, aim release, held-fire looping, shooting cadence/damage/cover, camera facing and zoom passed. Measured lower-leg rotation and foot-position differences against the original locomotion controller were both zero. Full project build: zero errors and zero warnings. See shooter-results.txt.

In Play Mode, review stationary/moving aim and fire, releasing W while firing, releasing LMB while keeping RMB, releasing both buttons, and rapid re-aim during a blend. Final pose/weapon fitting and visual blend tuning remain for your review.
