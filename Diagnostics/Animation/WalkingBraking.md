# Walking release braking — 2026-10-22

Historical implementation, superseded by ThreeStateWalking.md. The user rejected the 80 ms brake because it left the feet stepping in place, and removed Start Walking. Use ThreeStateMovementProbe for the current implementation.

The user requested almost immediate stopping on W release, preserving the working walking step sync and Start Walking. Only PunisherMovement.cs was changed in gameplay assets. The Animator, FBX imports, scene, calibrated walking speed, and start-walking timing were preserved.

The current saved Walk -> Stop transition has a fixed duration of 0.4592491 seconds, no exit-time requirement, and destination offset 0.13161877. Its 2.783334-second clip starts about 0.36634 seconds in. Start Walking -> Stop has a 0.4359355-second blend and offset 0.1794266. The previous script treated the later Stop -> Idle exit point as the movement-stop deadline, so it continued translating through the settling poses. The copied-controller baseline took roughly 2.23–2.27 seconds to stop and drifted about 0.70 meters.

The updated script starts braking on input release, with a new Walk Braking Time of 0.08 seconds. Existing Stopping Time is a fallback capped by this new limit, so old serialized 0.5-second values cannot preserve the glide. An observed stop blend may shorten the remaining brake, but it cannot restart or extend the release deadline. The old walkStopEndNormalizedTime and stop-clip-duration cache were removed because the visual stop-to-idle timing no longer controls forward translation. Running stopping behavior and start/walk/restart timing remain on their previous paths.

Walk Braking Time can be reduced further; zero requests immediate forward stopping. Gravity continues normally and the Animator remains responsible for playing the full stop pose.

WalkBrakeProbe.cs compiled the actual updated script in the isolated project using copies of the current gameplay controller and clip imports. It compared the previous and updated scripts frame by frame before release, then measured braking. At 30/60/144 Hz the start and steady-walk speed differences were exactly zero. New stopping took 0.1000/0.0833/0.0833 seconds and traveled 0.01550/0.02213/0.02436 meters. Eight release phases, re-pressing during the stop transition, one-frame release/repress, release during startup, and eventual return to Idle passed. This validates speed envelopes and Animator progression, not a visual full-scene foot-lock guarantee. See walk-braking-results.txt.
