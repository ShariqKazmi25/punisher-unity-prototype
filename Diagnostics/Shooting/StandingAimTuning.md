# Standing aim: stock and elbows

Updated 2026-10-27. RifleHandIK now includes a shouldered pose and manual tuning controls. These work in Play Mode; the FBX/Animator clip preview still shows the uncorrected source pose.

**Current user tuning:** the table below contains the original starting defaults. Your later custom values are conserved in the scene and UserStandingPosePreset.json; see SharedStandingAim.md. Standing upper-body aim/fire poses are now shared during walking. Keep your customized targets and values when using this guide.

The old standing pose measured about 176 degrees at the left elbow (almost straight) and 80 degrees at the right elbow (tightly folded). The stock's top and bottom lay beside the face. The revised default diagnostic pose measures approximately 137 degrees left / 90 degrees right, with the stock at a shoulder/chest pocket and both elbows directed down. The support wrist slides back along the handguard while preserving its fitted rotation/finger curl.

For **live Scene view editing without pausing**, use **RifleHandIK → Editor Aim Preview → Preview Aim In Scene View** during Play Mode. The game keeps aiming without RMB while you work in Scene view. Full workflow and troubleshooting: **SceneAimPreview.md**.

## Controls on the character's RifleHandIK component

| Control | Purpose | Default |
|---|---|---|
| Shoulder Stock While Aiming | Place the butt pad at the shoulder target while the weapon layer is active | ON |
| Shoulder Offset | Shoulder/chest target relative to the right shoulder joint, in character axes: X right, Y up, Z forward | (-0.015, -0.025, 0.055) |
| Shoulder Anchor | Optional draggable target overriding Shoulder Offset | Unassigned; uses offset |
| Stock Contact | A marker identifying the centre of the actual butt pad | Created automatically for this ar15 |
| AR15 Stock Local Position | Measured butt-pad centre in ar15-root coordinates, used at marker creation | (0, 0.44194, 5.51587) |
| Support Hand Slide Back | Move the aiming support wrist back along the handguard, reducing extension | 0.04 world units |
| Guide Aiming Elbows | Apply the aiming elbow direction guides | ON |
| Left Aim Elbow Offset | Fallback pole relative to the left shoulder, in character axes | (-0.04, -0.14, 0.10) |
| Right Aim Elbow Offset | Fallback pole relative to the right shoulder | (0.08, -0.16, 0.02) |
| Left Aim Elbow Hint / Right Elbow Hint | Optional draggable targets overriding the corresponding elbow offsets | Unassigned |
| Aiming Elbow Hint Weight | How strongly the elbow points toward its guide | 1 |
| Minimum/Maximum Left Elbow Bend | Bend range measured **from straight** | 35 / 135 degrees |
| Minimum/Maximum Right Elbow Bend | Bend range measured **from straight** | 30 / 90 degrees |

An assigned target overrides its corresponding offset field. For example, adjust Shoulder Anchor after creating it; Shoulder Offset is then the fallback only. Keep the old Position Weight, Rotation Weight and Left Elbow Hint settings for the working idle fit. Left Aim Elbow Hint is separate so manual aim tuning does not add an idle elbow constraint.

## Quick tuning with values

1. Select **punisher_vest@Rifle Idle**, which owns the Animator and RifleHandIK. Start Play Mode from idle, press Esc, then enable **Preview Aim In Scene View** on RifleHandIK. Leave Play Mode **unpaused** and use the Scene view to inspect from the front and side.
2. Adjust **Shoulder Offset** to place the stock. Increasing X moves the pocket toward the right shoulder; increasing Y raises it; increasing Z moves it forward from the chest. Start with changes of 0.005–0.01. Stock Contact identifies the butt pad; the shoulder target defines where that pad belongs on the body.
3. Adjust **Support Hand Slide Back** for left-arm extension. Increasing it brings the hand toward the rear of the handguard. Then adjust the left elbow offset's X/Y to choose how the elbow points.
4. Adjust **Right Aim Elbow Offset** to tuck the firing elbow: lower Y and move X toward the torso for less flare. The hint changes the elbow's bend plane; the grip position and bend limits determine how far the arm folds.
5. Before leaving Play Mode, use the component menu's **Copy Component**. Stop Play Mode, then **Paste Component Values** onto the same existing RifleHandIK component and save the scene. Check the next Play session. Automatically generated runtime stock/muzzle references may return to null; the helper recreates them.

## Dragging targets in the Scene view

1. In **Edit Mode**, right-click the RifleHandIK component header (or use its three-dot menu) and select **Create Aim Pose Targets**.
2. The helper creates and assigns **StockShoulderTarget**, **LeftAimElbowTarget** and **RightAimElbowTarget** under the upper chest. These stay outside both arm chains and support Undo.
3. Enter Play Mode and enable **Preview Aim In Scene View**. Keep it unpaused. Enable Scene view **Gizmos**, select a target, and move it with the Move tool to see the pose update live. Orange marks the shoulder pocket, magenta the left elbow pole, green the right elbow pole. Aim the stock at the right shoulder/chest and keep the right elbow guide below shoulder height for a tucked pose.
4. Copy/record each changed target's **local Position** before stopping. After stopping Play Mode, apply those same local values to the matching Edit Mode targets and save the scene. Rotation is irrelevant to these position-only targets.

The two elbow targets control direction, not arm length. Use Support Hand Slide Back and the elbow bend limits to reduce a locked-out support arm. Bend values use 0 = straight and 90 = a right angle; the visible inner elbow angle is approximately 180 minus bend. Both wrists must remain reachable. If a target cannot be reached, the solver prioritizes arm reach and hand contact, so the stock may stop slightly short of the shoulder target. Move the pocket/grip into a reachable position instead of increasing IK weights beyond 1.

## Behavior and verification

The new posture blends with the weapon layer, including standing/moving aim and firing, and fades out on release. Both hand contacts, camera-centre barrel/face alignment and the built-in AR15 shooting animation remain active. StockContact follows the animated rifle body, along with the muzzle and support grip. Idle fitting, locomotion source, Animator, scene mounting transforms and FBX imports were not edited.

Diagnostic front/side renders: standing-aim-before.png, standing-aim-before-side.png, standing-aim-after.png and standing-aim-after-side.png (simplified materials, not gameplay screenshots). The rendered standing default put the butt-pad centre within approximately 1.3 mm of its shoulder target. Across sampled standing aim/fire/stop poses the gap stayed below 1.7 cm. The elbow constraints kept the left inner angle below 145 degrees and the right at about 90 degrees or more. Face clearance was also checked visually; the numeric test uses head-bone-to-stock-edge distance, not a mesh collision system.

At 30/60/144 FPS, aim/vertical/near-surface/return-to-idle tests, draggable target creation and elbow response, AR15 bolt/casing/cadence, crosshair alignment and unchanged foot positions passed. Idle wrist positions matched the previous behavior exactly. Main build passed with zero warnings/errors on the final incremental build. Results: aim-posture-results.txt, ar15-fire-results.txt and shooter-results.txt.
