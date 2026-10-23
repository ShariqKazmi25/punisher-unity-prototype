# Current three-state walking — 2026-10-22

The user removed Start Walking because of its rotation bug. Current states are Rifle Idle, Rifle Walk, and Rifle Walk To Stop. PunisherMovement.cs now supports the direct graph without caching/requiring Start Walking or exposing its old timing fields. W requests walking and release requests Idle; Animator owns the transitions and interrupts. The calibrated steady speed remains 0.6222 m/s and the 0..61 walk loop was preserved.

Copied saved transition timings used for analysis:

| Transition | Fixed duration | Destination offset | Exit time |
|---|---:|---:|---|
| Idle -> Walk | 1.0978575 s | 0.81140864 | disabled |
| Walk -> Stop | 0.4592491 s | 0.13161877 | disabled |
| Stop -> Idle | 0.25 s | 0 | 0.9101796 |
| Stop -> Walk | 0.7104838 s | 0 | disabled |

The stop clip is still 0..167 at 60 fps, lasting 2.783334 seconds. The Walk -> Stop offset enters at about 0.36634 seconds of clip time. Measuring the clip's extracted forward velocity and low-foot movement shows that the final stepping continues well beyond the former 0.08-second body brake. Forward motion slows around normalized time 0.3 and is nearly zero around 0.575; after that the body mostly settles/rocks in place. The previous quick brake stopped translation while the feet still moved backward relative to the body.

The new Walk Stop Speed Profile stores the measured forward-speed fraction against normalized stop-clip time. During release, the script samples the active stop time, playback speed, and transition progress, and blends its speed with the outgoing walk. It reaches zero at normalized time 0.58 and stays there through settling/Idle. Movement cannot accelerate or reverse while input stays released. Repressing W preserves the current speed and follows the actual Stop -> Walk blend. Missing Animator/profile falls back to the normal finite stopping ramp. Direct starts/restarts continue using the active blend duration. CharacterController still owns translation; animation root motion stays disabled, and no clip rotation is applied by this change.

ThreeStateStopAudit.cs generated the raw stop-motion CSV in the isolated project. ThreeStateMovementProbe.cs evaluated the actual source and copied three-state Animator with the updated profile, comparing to the prior 80 ms brake across eight release phases at 30/60/144 Hz. It moved the test actor and sampled foot poses together. Mean low planted-foot forward drift decreased from 0.38438/0.37729/0.37614 m/s to 0.11331/0.11495/0.11413 m/s, about 70%. Direct-start/steady speed curves matched exactly. Body stopped in 1.3333/1.2833/1.2639 seconds and traveled 0.5513/0.5389/0.5307 m, following the remaining stepping rather than the settling tail. Early release, one-frame release/repress, later restarts, eventual Idle, and monotonic nonnegative braking passed. See three-state-movement-results.txt.

These are measured foot-drift improvements, not a full foot-lock solution or visual confirmation in the gameplay scene. With this clip/entry point, stopping the body almost instantly necessarily leaves animated stepping in place. A genuinely shorter final step requires a later stop-clip entry or a different/trimmed animation, followed by remeasuring this profile. Animator/FBX import/scene files were not edited by the agent.
