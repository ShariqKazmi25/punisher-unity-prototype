# AR15 built-in firing animation

The original **Armature.003|shooting** clip from Assets/punisher/ar15/source/ar15.fbx now plays once for each accepted shot. It runs on the rifle, alongside the existing Mixamo character firing clips. No additional Animator wiring is required.

The later shouldered pose controls are described in **StandingAimTuning.md**. RifleWeaponAnimation now also reparents the generated StockContact marker under the animated rifle body, preserving its placement so the shoulder constraint follows the built-in recoil.

## Wiring

- Assets/Resources/Weapons/AR15Animation.asset references the original imported clip directly (fileID -7768389078912668132, FBX GUID 5b8bcc55ebe5eec4fae4d9b88cb0a549). This is not a copied or generated replacement clip.
- PunisherShooter finds the current child named ar15 and configures RifleWeaponAnimation automatically. It creates a dedicated gun Animator and manually evaluated animation graph at runtime. The character Animator and its calibrated walking graph remain unchanged.
- FireIfReady starts the gun cycle only after the firing cooldown accepts a round. Holding/releasing the input without another accepted shot cannot restart the gun animation. The last round finishes its cycle after release. Disabling the shooter restores the rifle's rest pose.
- The source clip is 0.291667 seconds at 24 FPS, non-looping, and animates the rifle body's recoil, bolt and casing. Its gun-root transform is not animated. The internal Armature.003/Bone is the rifle body.
- Playback completes within min(original clip duration, shot interval). At the current five rounds/second, the entire clip plays in 0.2 seconds (approximately 1.4583x speed). Sparse shots use the original duration. No firing-rate change was made.
- Rifle animation updates at order 125, before the character's final grip/aim solve at 150 and shooting at 200. LeftHandGrip and Muzzle are reparented under the animated rifle body at runtime with their world fit preserved. RifleHandIK keeps ar15 as the weapon reference and uses the animated Muzzle.forward for barrel alignment. This lets markers follow the actual gun recoil while retaining the crosshair correction.
- Bone transforms return to their saved rest pose at completion, including resetting the casing for reuse. The animation graph is destroyed with the weapon.

## Reviewing it

Enter Play Mode and use LMB, standing or moving. Inspect the bolt/casing movement on ar15. The character's recoil still comes from Firing Rifle/Firing Rifle2. The gun has its own RifleWeaponAnimation component/Animator during gameplay; it does not use humanoid clips.

To change the source clip, select **Assets/Resources/Weapons/AR15Animation.asset** and edit Shooting Clip. Keep Body Bone Path as Armature.003/Bone for this import. Fire cadence remains PunisherShooter.Rounds Per Second. A permanent RifleWeaponAnimation component or explicitly assigned shooter Weapon Animation reference is also supported.

The runtime grip/muzzle markers now have Bone as their parent, so their local Inspector numbers differ from their old ar15-root coordinates. Their initial world placement is preserved. The existing AR15 Muzzle Local Position field remains expressed relative to ar15 and is used before reparenting.

## Verification

An isolated copy of the actual fitted character, original rifle FBX, settings asset, controller and current scripts passed at 30, 60 and 144 FPS. Each run fired ten rounds over two seconds; the imported bolt and casing bones moved; the fitted gun root stayed unchanged; measured hand target error remained below 0.002 mm, barrel direction error below 0.03 degrees, and final foot displacement zero. Release, faster/slower firing intervals, disable/reset and graph destruction checks passed. Existing hitscan/damage/cover/Animator/camera checks also passed. See ar15-fire-results.txt and shooter-results.txt.

The main gameplay project build passed with zero errors; its nine warnings come from existing Cinemachine package code. CompileWeaponAnimation.targets scopes the two new source-file includes to Assembly-CSharp while Unity's generated IDE project awaits refresh; it does not edit that project or package sources.
