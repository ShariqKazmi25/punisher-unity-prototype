# Larger lower level — 12 November 2026

Historical revision: [revision E](revision-e.md) fills the side strips to match the upper deck, while retaining the enlarged lower front area and this revision's overall dimensions.

Revision D widens the whole outer shell and deepens the lower front apron, while adding a small amount of length to the upper corridors. Dimensions use the established character calibration: approximately 0.419719 Unity units per design meter, with the original 0.77-unit Punisher controller.

| Dimension | Revision C | Revision D |
|---|---:|---:|
| Lower floor width | 18 m | 22 m |
| Overall floor length | 26.28 m | 31.38 m |
| Front floor-edge to stair foot | 5.20 m | 8.20 m |
| Upper stair-top to rear-wall run | 18.33 m | 20.31 m |
| Clear lower strip outside each raised side bay | 0 m | 2 m |

The lower floor is 22% wider; the front apron is approximately 58% deeper. The upper corridor runs are approximately 11% longer. The ceiling heights, stairs, hub front curve, raised platform widths and character scale remain as in revision C. Straight platform sections extend in Z with the corridors.

The outer walls, front/back wall panels, ceiling slabs and crossbeams follow the wider shell. Raised side bays retain their width, leaving lower-level walkways between their outer edges and the new side walls. These strips join the enlarged lower front area. The right front pillar moves outward by two design meters to remain near the right wall. The left front pillar stays beside the curved hub, preserving the asymmetrical arrangement. Pillars remain round, and beam ends are refitted to their moved supports.

The gameplay rig, camera settings and player spawn are unchanged. Structural objects have unit scales, persistent mesh assets, original material slots, regenerated UV sets and matching colliders. No equipment or fine details were added.

Mesh assets are in `Assets/Environment/MicroLair/Meshes/RevisionD`. The previous scene is backed up at `Diagnostics/MicroLair/MicroLair_Graybox_before_revision_d.unity`. `revision-d-validation.txt` records floor, player-capsule and roof-clearance checks across the enlarged apron, both lower side strips, their connections around the front pillars and both upper corridors. It also checks the unchanged stair treads, doorway openings, shooting lanes and side-wall collision.

`revision-d-interior.png` shows the expanded lower area with the existing Punisher for scale. `revision-d-cutaway.png` shows the layout with the roof and near walls temporarily hidden. The saved scene retains the full shell. The Tools > Micro Lair validation and preview commands recognize revision D, and its authoring command is guarded against applying the expansion twice.
