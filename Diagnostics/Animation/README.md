# Animator regression check

Updated 2026-10-15: Start Walking was removed from the controller at the user's request. The clip asset remains available.

Current graph:
- Rifle Idle -> Rifle Walk: iswalking true, no exit time.
- Rifle Walk -> Rifle Walk To Stop: iswalking false, no exit time.
- Rifle Walk To Stop -> Rifle Idle: iswalking false, exit time 0.9318182.
- Rifle Walk To Stop -> Rifle Walk: iswalking true, no exit time.

All four transitions use Next State interruption. Existing transition durations and the latest Walk -> Stop offset (0.26342234) were retained. The two transitions retargeted to Rifle Walk start at offset zero rather than reusing offsets tuned for Start Walking.

AnimatorProbe.cs is an Editor-only diagnostic for the isolated project in Temp/AnimatorProbe; keep it outside production Assets. It loads copied assets and advances the actual Animator at 60 Hz. Sustained walking, quick tap/release, stopping, restarting during stop, and final Idle passed. Bone rotation changes confirmed pose playback. This is not an interactive full-scene test.

PunisherMovement.cs already drives the iswalking Bool independently of state names and needed no changes.
