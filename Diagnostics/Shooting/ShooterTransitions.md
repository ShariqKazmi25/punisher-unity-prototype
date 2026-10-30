# Shared standing/moving aim and fire

Weapon Actions shares the standing upper-body clips for standing and walking. iswalking is used by Base Layer, not by these six upper-body transitions.

Every transition: Has Exit Time OFF; Fixed Duration ON; Offset 0; Interruption Source Next State; Ordered Interruption ON; Can Transition To Self OFF. No Any State routes. Weapon Lowered uses Rifle Idle as the neutral fallback while the layer fades out.

| From | To | Conditions (AND) | Duration (seconds) |
|---|---|---|---:|
| Weapon Lowered | Firing Rifle2 | isfiring=true | 0.04 |
| Weapon Lowered | Rifle Aiming Idle | isfiring=false, isaiming=true | 0.1 |
| Rifle Aiming Idle | Firing Rifle2 | isfiring=true | 0.04 |
| Rifle Aiming Idle | Weapon Lowered | isfiring=false, isaiming=false | 0.1 |
| Firing Rifle2 | Rifle Aiming Idle | isfiring=false, isaiming=true | 0.1 |
| Firing Rifle2 | Weapon Lowered | isfiring=false, isaiming=false | 0.1 |

Your existing standing clip/blend settings were preserved. W no longer switches/restarts upper-body states; the original Base Layer still selects Rifle Idle, Rifle Walk and Rifle Walk To Stop. See SharedStandingAim.md for the conserved tuning and verification.
