# Five-node movement update

The saved Animator now has Rifle Idle, Rifle Walk, Rifle Walk To Stop, Rifle Run, and Running To Stop, with 11 transitions. Its file was not modified by this update.

PunisherMovement.cs keeps a single requested Idle/Walking/Running state. W requests walking; W + Left Shift requests running. iswalking remains true in both walking and running. Shift alone or releasing W clears both booleans. The Animator chooses its own stopping and restart paths from those parameters; no start animation is required.

Updated speed ramps:
- Idle or stopping -> running: runAccelerationTime (default 0.25 seconds).
- Walking -> running: new walkToRunTime (default 0.2 seconds).
- Running -> walking: new runToWalkTime (default 0.2 seconds).
- Running -> idle: existing runStoppingTime (default 0.6 seconds).
- Idle -> walking and walking -> idle retain accelerationTime/stoppingTime.

Existing serialized Inspector values remain in effect. All speed changes start from the current speed, preserving momentum during quick restarts. Defaults are walkSpeed 1.5 and runSpeed 4.5.

Verification: C# build passed with zero errors/warnings. FiveNodeMovementProbe.cs loaded a copy of the saved controller and the actual updated movement script in Temp/AnimatorProbe. All 15 source-state/input combinations, walk/run blend timing, quick restarts, run-to-walk, walk-stop-to-run, idle, and disable reset passed at 30, 60, and 144 Hz. See five-node-results.txt. These checks do not cover full-scene visual alignment or keyboard focus.

## Running ramps synchronized to the saved Animator
The latest script update replaces linear running ramps with eased (SmoothStep) ramps. Match Run Animation Timing is enabled by default and reads the active Animator transition duration at runtime, including offset/current transition progress, instead of relying on old serialized Inspector timings. Timed ramps continue on elapsed time after synchronization so movement cannot stay frozen on a stalled animation. Walking-only ramps remain linear.
Read timings: Idle -> Run 0.6006818 s; Walk -> Run 0.5784592 s; Running To Stop -> Walk 0.9270649 s; Running To Stop -> Run 1.1193687 s; Walk To Stop -> Run 1.4984394 s. Full braking spans the Running To Stop clip to normalized exit 0.8770492, about 1.7833 s, rather than only its 0.25 s entry blend. Run Stop End Normalized Time must match that exit point if it is retuned later.
Default fallback times were updated to the current graph. Active timing synchronization also overrides stale serialized fallback values after the blend becomes observable. The Animator and scene were not edited.
RunningTimingProbe.cs and running-timing-results.txt document successful compilation and isolated 30/60/144 Hz checks, including early run acceleration bounds, matched run-to-walk/stop timing, restarts, input/state reachability, manual timing fallback, and clearing both booleans when disabled. Full-scene appearance still needs Play Mode review.

## Walking ramps synchronized to the saved Animator
Walking now uses the same eased, transition-synchronized speed ramps as running. Match Walking Animation Timing is enabled by default. Idle -> Walk uses the active 0.25 s blend; Walk To Stop -> Walk uses its active 0.98519933 s blend. Full walking braking lasts until normalized exit 0.9318182 of Rifle Walk To Stop. With the saved entry offset 0.22699101 and 3.666667 s clip, this is about 2.584366 s after the stop begins. Walking fallback defaults are 0.25 s starting and 2.5843664 s stopping; active timing overrides stale Inspector values.
An early interrupted Walk -> Stop blend may still report Walk as its source while returning to Walk. Synchronization therefore identifies a walking restart from its destination, without waiting for the stop state to become current. This preserves momentum while using the correct restart blend. Run-to-walk timings remain controlled by Match Run Animation Timing.
WalkingTimingProbe.cs and walking-timing-results.txt include passing 30/60/144 Hz checks for eased walking start, complete offset-adjusted braking, early restart timing, repeated W taps, manual fallback, all 15 state/input combinations, and the previous running regressions. Build passed with zero errors/warnings. Gameplay Animator and scene were unchanged; full-scene visual review remains in Play Mode.
