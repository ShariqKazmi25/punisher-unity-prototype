# Taller, longer graybox — 12 November 2026

Historical revision: [revision D](revision-d.md) subsequently expands the lower level and extends the corridors slightly further. The ceiling heights established here are retained.

Revision C adapts the reference-based layout for gameplay spacing. It keeps the curved hub frontage, asymmetrical front pillars, room width and existing Punisher size, while extending the building along Z and raising the overhead structure.

Dimensions below are design meters at the established character calibration of approximately 0.419719 Unity units per meter. The actual Punisher remains approximately 0.768 units tall, with a 0.77-unit CharacterController.

| Dimension | Revision B | Revision C |
|---|---:|---:|
| Overall floor width × length | 18 × 16 m | 18 × 26.28 m |
| Stair-top to rear-wall aisle run | 10.50 m | 18.33 m |
| Ceiling underside above lower floor | 4.50 m | 6.80 m |
| Beam underside above lower floor | 3.95 m | 6.25 m |
| Ceiling clearance above raised floor | 3.48 m | 5.78 m |
| Minimum clearance under beams above raised floor | 2.93 m | 5.23 m |
| Stair/aisle width | 2.40 m | 2.40 m |
| Raised floor height | 1.02 m | 1.02 m |

The overall Z dimension grows about 64%; the usable aisle run grows about 75%. The minimum overhead clearance above the raised floor grows about 79%. Keeping the aisle widths gives the longer routes a more pronounced corridor shape.

The extra depth is concentrated in the straight portion behind the hub's curved front. The half-ellipse front and both six-step stair flights retain their dimensions. The lower front apron also grows by two design meters. Floors, side bays, back connection, walls, ceiling recesses, rear doorway and reservation markers follow the longer layout. Pillars become taller without becoming oval. Beams move upward with their vertical thickness retained; their lengths are refitted to the new spans. One additional overhead crossbeam establishes a middle structural bay.

Punisher, the rifle, movement/shooting scripts, camera settings and spawn transform are unchanged. The existing neutral inspection lights are repositioned and their ranges expanded to keep the larger graybox visible. No fine props, railings, windows or lighting design are added.

Meshes are saved separately in `Assets/Environment/MicroLair/Meshes/RevisionC`. Objects retain unit transform scales, existing material slots, colliders and UV sets. UV0 is rebuilt at nominal-meter density for the resized prism meshes; UV1 is regenerated. Earlier mesh assets remain available. The pre-change scene is backed up at `Diagnostics/MicroLair/MicroLair_Graybox_before_revision_c.unity`.

`revision-c-validation.txt` records structural mesh/collider checks, all twelve tread elevations, floor continuity and actual-player capsule clearance along both longer aisles, overhead-clearance samples, doorway openings and projectile-ray checks. `revision-c-before.png` and `revision-c-interior.png` use the same camera position for comparison. `revision-c-cutaway.png` and `revision-c-plan.png` show the expanded layout with the roof temporarily hidden; the saved scene retains the roof and walls.

Open `Assets/Scenes/MicroLair_Graybox.unity` and press Play to assess the new spacing with the existing character. The Tools > Micro Lair validation and preview commands recognize revision C. The revision command is guarded against applying its stretch twice.
