# Reference revision B — 10 November 2026

Historical revision: [revision C](revision-c.md) now raises the ceilings and extends the Z layout for gameplay. The character scale, hub frontage and aisle widths established here are retained.

The three additional reference images supersede revision A's hub frontage and front-column symmetry.

- The hub is 6.4 nominal meters wide instead of 8. Its entire front is a half ellipse with a 3.2 m horizontal radius and 1.8 m depth, tangent to the side runs. Both the top and bottom platform outlines follow this curve; the former long straight front segment is gone.
- A simple matching curved overhead fascia establishes the upper silhouette. It is part of the ceiling group and contains no glazing or metal lattice.
- Living and control bays widen from 2.3 to 3.1 nominal meters. Both stair/aisle centerlines move inward by 0.8 m, preserving their 2.4 m clear width.
- Facing into the room from the lower corridor, the left front column moves inward to nominal X=-2.65, beside the hub, while the right front column sits at X=+7.3, nearer the outer wall. Both retain Z=-4.2. These are intentionally asymmetrical.

The existing gameplay Punisher mesh was measured from baked skin vertices, including its scene transforms: 0.76809 Unity units from boots to hair. Its existing CharacterController is 0.77 units tall and 0.36 units in diameter. Using 1.83 m as the adult reference height gives approximately **0.419719 world units per nominal meter**. The environment geometry, positions, colliders and inspection camera are resized to this convention; the gameplay model and controller are not resized.

Approximate final world dimensions:

| Dimension | Unity units |
|---|---:|
| Room floor | 7.555 × 6.716 |
| Hub width | 2.686 |
| Side bay width, each | 1.301 |
| Stair and aisle width | 1.007 |
| Raised floor height | 0.428 |
| Stair riser | 0.0714 |
| Ordinary ceiling underside | 1.889 |
| Front left column X | -1.112 |
| Front right column X | +3.064 |

Structural transforms remain at unit scale; dimensions are baked into new mesh assets under `Assets/Environment/MicroLair/Meshes/RevisionB`. Material assignments are preserved. UV0 retains nominal-meter texture tiling so textures keep their apparent size relative to the character; UV1 remains a separate unwrap.

An optional static copy of the measured Punisher mesh is stored under `10_Optional_PunisherScaleReference`, disabled by default. Enable it to compare proportions directly. It has no gameplay scripts or controller. Revision previews temporarily show this reference standing on the upper platform.

The pre-edit scene is backed up at `Diagnostics/MicroLair/MicroLair_Graybox_before_revision_b.unity`. The original generated mesh assets remain available. `revision-b-scale.txt` records the measurement and `revision-b-validation.txt` records the geometry/collision checks. Character movement itself still needs a play test after adding the gameplay character to this scene.

Earlier revision A drawings and dimension tables describe the original layout; use these revision B values for the current scene.
