# Walk/run Animator review — 2026-10-19

This review uses the user's supplied six-state graph. The saved gameplay controller still has three walking states and only the iswalking parameter. No gameplay Animator, scene, or clip settings were edited. Save the six-state controller in Unity and apply these settings manually before testing keyboard running.

## Input contract

Both parameters are Bools with default false. The existing parameter iswalking means forward input, including running. It is not an exclusive walking flag.

| Input | RequestedState | iswalking | isrunning |
|---|---|---|---|
| Neither key, Shift alone, or application unfocused | Idle | false | false |
| W | Walking | true | false |
| W + Left Shift | Running | true | true |

Releasing Shift while retaining W requests Walking. Releasing W always requests Idle, even while Shift remains held. The invalid false/true Bool combination is never emitted by the movement script. RequestedState describes input intent: start/stop animation states may still be playing.

## Corrections and missing paths

- Transition 11 must use iswalking=true and isrunning=false. Its supplied false/false conditions duplicate transition 10 and do not allow return to walking.
- Transition 8 needs isrunning=false so it cannot compete with the new sprint path from Walk to Stop.
- Transition 7 can also include isrunning=false for explicit idle conditions; this is redundant when the input contract is obeyed.
- Add transition 12: Run to Stop -> Rifle Run on true/true for quick sprint restart.
- Add transition 13: Walk to Stop -> Idle to Run on true/true for sprinting while the walking stop is playing.
- Transitions 6 and 9 only need isrunning=false because both W release and Shift release should leave running under the chosen design.

## Proposed settings

Conditions in each cell are AND conditions. All transitions: Fixed Duration ON, Interruption Source Next State, Ordered Interruption ON if editable, Solo and Mute OFF. The duration values are starting points; preview foot alignment manually. Existing walking durations are retained below.

| # | Transition | iswalking | isrunning | Has Exit Time | Exit Time | Duration (seconds) |
|---|---|---|---|---|---|---|
| 1 | Rifle Idle -> Rifle Walk | true | false | OFF | ignored | 0.25 |
| 2 | Rifle Idle -> Idle to Run | true | true | OFF | ignored | 0.15 |
| 3 | Rifle Walk -> Walk to Stop | false | any | OFF | ignored | 0.37804615 |
| 4 | Rifle Walk -> Idle to Run | true | true | OFF | ignored | 0.15 |
| 5 | Idle to Run -> Rifle Run | true | true | ON | 0.85 | 0.15 |
| 6 | Idle to Run -> Run to Stop | any | false | OFF | ignored | 0.15 |
| 7 | Walk to Stop -> Rifle Idle | false | false | ON | 0.9318182 | 0.05 |
| 8 | Walk to Stop -> Rifle Walk | true | false | OFF | ignored | 0.25 |
| 9 | Rifle Run -> Run to Stop | any | false | OFF | ignored | 0.20 |
| 10 | Run to Stop -> Rifle Idle | false | false | ON | 0.90 | 0.10 |
| 11 | Run to Stop -> Rifle Walk | true | false | OFF | ignored | 0.15 |
| 12 | Run to Stop -> Rifle Run | true | true | OFF | ignored | 0.15 |
| 13 | Walk to Stop -> Idle to Run | true | true | OFF | ignored | 0.15 |

Use Transition Offset 0 for new transitions. Preserve any intentionally tuned existing offsets: the saved Walk -> Walk to Stop offset is 0.33817723.

Transition 11 intentionally interrupts the stop animation for an immediate return to walking after Shift release. If you want a full braking animation before walking, enable Has Exit Time around 0.85 for that transition, accepting a delay. Speed ramp timing would then also need matching to that delay. A direct Run -> Walk path is an alternative, not required for this graph.

State Speed: 1, no speed multiplier or Motion Time parameter. Default state: Rifle Idle. Loop Time ON for Rifle Idle, Rifle Walk, Rifle Run; OFF for Idle to Run, Walk to Stop, Run to Stop. Each state must have its matching Motion assigned.

## Movement script and verification

Assets/Scripts/PunisherMovement.cs now derives both Bools and target speed from a single MovementState enum. Default speeds are 1.5 walking and 4.5 running. Existing scene walking values are preserved. Separate run acceleration/stopping times default to 0.6 seconds. Speed changes begin from the current speed, preserving momentum on quick restarts. Animation playback is independent of physical speed and animation state names.

SprintReviewProbe.cs builds a proposed graph only in Temp/AnimatorProbe from copied clips, and evaluates it alongside the actual updated movement script. It checks input mapping, booleans, speed continuity, steady-state reachability from all six states, quick restarts, Shift release to walking, and disable reset at 30/60/144 Hz. Results are saved separately. This does not verify unsaved changes in the user's open Animator or full-scene visuals.
