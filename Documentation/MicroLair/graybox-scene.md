# Micro’s lair graybox scene

**Updated 12 November 2026:** the current scene uses [revision E](revision-e.md): the upper floor now extends to both side walls, filling the former side trenches. Revision D's larger lower front area and longer corridors, and revision C's taller ceiling, are retained. Dimensions below describe the initial layout; revision D gives the current footprint.

Open `Assets/Scenes/MicroLair_Graybox.unity` in Unity. This separate scene now includes a copy of the complete SampleScene Punisher gameplay rig. The original SampleScene is unchanged. See [gameplay setup](gameplay-setup.md) for controls.

## Included

- Lower floor, rounded-front hub platform, living and control platforms, and two upper aisles.
- Two 2.4 m wide stair flights, each with six 0.17 m rises and 0.30 m treads, meeting the upper floor at Y=1.02 m.
- Four round 0.8 m diameter pillars, large supporting beams, outer walls, door openings, and simple ceiling slabs with two coarse recesses.
- A separately grouped, provisional rear connecting deck.
- A fixed inspection camera and neutral work lights to make the gray geometry visible. These are not finished environment lighting.

There are no window frames, metal grilles, railings, door leaves, computers, furniture, pipes, or fine ceiling details. Interior equipment areas and their access routes are left open. Empty transforms in `07_ReservedSpaces_NoGeometry` identify their intended locations without adding visible placeholders.

## Editing and materials

Every structural piece is an ordinary GameObject with a MeshFilter, MeshRenderer and collider. No runtime generator or custom component is required for the scene to work. Individual steps, wall segments, columns and ceiling pieces can be moved, duplicated, removed or assigned different materials.

Saved mesh assets are in `Assets/Environment/MicroLair/Meshes`. UV0 is mapped at approximately one repeat per meter at the authored scale; UV1 is a separate lightmap unwrap. The default transforms have unit scale. Scaling a GameObject later also scales its texture density, so adjust material tiling if needed.

MeshRenderer material slots:

1. Top surface.
2. Vertical sides, including the curved platform fascia.
3. Underside.

The five neutral gray URP Lit materials are in `Assets/Environment/MicroLair/Materials`. Replace the relevant slots with your texture materials. A change to a shared material affects all objects using it; duplicate the material when only one piece should change.

Use **Tools → Micro Lair → Toggle Ceiling Visibility (Scene View)** to inspect the interior from above without disabling the ceiling in Game view. Alternatively, use the Hierarchy visibility controls. `05_Ceilings` contains the roof/recesses; the large beams are in `04_PillarsAndBeams`.

The fixed inspection camera is disabled for gameplay; its neutral work lights remain enabled. The `Gameplay_Punisher` root contains the character, weapon and third-person camera. The stairs use real tread colliders.

## Assumptions retained from the plan

This is revision A of the photograph-derived plan, not an exact production-set reconstruction. The rear deck, rear columns, and front access opening are labeled as assumptions in the hierarchy. The living/control platforms extend 0.1 m farther outward than the diagram to meet the inside wall faces without narrow floor gaps. The unlocated red-door service corridor is not fabricated as an extra room.

No cages are replaced with opaque walls: their absence is intentional, preserving the open views and leaving space for later frames and railings. Raised platform edges are intentionally unguarded in this structural pass.

## Verification

`graybox-validation.txt` records the Unity checks for saved mesh references, material slots, UV sets, colliders, upward surface winding, floor and tread heights, aisle clearance and unobstructed openings.

`graybox-cutaway.png` and `graybox-top.png` temporarily hide ceiling and near walls for inspection. The saved scene retains the full shell and ceiling. `graybox-interior.png` uses the scene's inspection camera.

The authoring tool is `Assets/Environment/MicroLair/Editor/MicroLairGrayboxBuilder.cs`. Its batch build entry refuses to overwrite an existing scene. The Tools menu provides open, validate, preview and ceiling visibility commands; it does not automatically regenerate on import or Play.
