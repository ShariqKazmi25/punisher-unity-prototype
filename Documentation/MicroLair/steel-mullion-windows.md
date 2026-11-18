# Rusted steel mullion windows

The four references supplied on 17 November, together with the earlier portfolio images, show thin rectangular steel glazing bars: curved continuous horizontal rails, vertical mullions following the platform edges, heavier jambs and heads, and doorway-sized interruptions beside the stair landings. These are steel window frames, not chain-link mesh or tubular handrails. Missing glass in the photographs leaves the grid visible; this pass models the bars only.

## Saved scene and organization

Scene: `Assets/Scenes/MicroLair_Graybox.unity`.

Hierarchy: `MicroLair_Graybox/11_SteelMullionWindows`, containing `CentreHub`, `LeftBay`, and `RightBay`. Each has editable vertical mullion/jamb objects and separate horizontal rail objects, plus named doorway markers. The current geometry is stored in `Assets/Environment/MicroLair/Meshes/SteelWindows/LargePanesSixRows/`. These are saved mesh objects with UV0, UV1, normals, tangents and colliders; no runtime generator is needed.

The frame paths are extracted from the current raised-platform meshes and inset 50 mm from their edges, in design dimensions. Thus the central half-ellipse, both side returns, widened fronts and extended straight corridor runs follow the actual revised graybox. Concrete shell geometry is preserved.

## Spacing and circulation

- Regular verticals: 40 mm wide, approximately 50 mm deep. Main posts and doorway jambs: 55 mm wide, up to 65 mm deep.
- Intermediate horizontal bars: 28 mm high, 40 mm deep. Sills, heads and doorway header bands: 50 mm high.
- Enlarged panes, per the user's revision: vertical bay spacing up to 2.0 design m, adjusted evenly between doorway jambs. The accepted column layout is retained. Six pane rows span the enclosure: four lower rows at 0.9 m spacing and two upper rows approximately 0.79 m high. The fourth row aligns with the unchanged 3.6 m clear doorway header.
- The top still follows the existing curved upper fascia/beam underside. The larger grid spans the same enclosure footprint and height.
- Four open entrances: one on each side of the hub and one into each side bay. Each now provides 1.85 m clear width and 3.6 m clear height in design dimensions, increased from 1.55 x 2.4 m. Door placement follows the existing hub entrance markers near the stair landings. Header bands continue above the openings; sills and low rails stop at the jambs.
- Both 2.4 m stair/aisle gaps remain open. The frame runs finish at the rear of their platforms, leaving access around their ends through the shared rear deck. The existing building doorways remain untouched.

These are photograph-based modeling estimates adapted to the current gameplay shell, not surveyed dimensions of the original set. The exact doorway measurements and unseen rear enclosure closures are not established by the photographs.

## Rusty metal material

All six PNGs from `C:/Users/shari/Downloads/rusty_metal_05_4k.zip` are extracted unchanged to `Assets/Materials/rusty_metal_05_4k/`.

Material: `Assets/Materials/rusty metal.mat`, opaque URP Lit. Color is sRGB; the OpenGL normal is imported as a linear normal map with no green-channel flip. The ARM texture is converted to Unity packing: R = source metallic, G = source AO, A = 1 - source roughness. The supplied metallic channel is preserved rather than imposing a uniform metallic value; the sampled pixels in this rust texture report zero metallic.

The material has a neutral 0.45 tint for the dark aged frames in the references, normal strength 0.7, and nominal 0.5 m texture repeats. Individual members use deterministic UV offsets to vary their rust patches. The 4K sources remain intact; PC runtime textures use BC7 color/data and BC5 normals. Height is retained as a source map; these thin bars do not use parallax or geometric displacement.

## Collision and verification

Each rail/post object uses its actual mesh for static non-convex collision. There are no invisible full-window box colliders or glass planes: projectiles can pass through empty panes and strike the steel. The larger panes are wider than the player; real horizontal crossbars still block standing passage, verified with the player's collision capsule. Door openings remain clear.

`steel-windows-validation.txt` records actual player-capsule probes through all four doors, stair/aisle clearance probes, open-pane raycasts, steel-hit raycasts, material assignment and mesh integrity checks. Existing meshes, concrete materials and colliders are checked for preservation. Previews are `steel-windows-front.png`, `steel-windows-right-aisle.png`, `steel-windows-left-aisle.png`, and `steel-windows-cutaway.png`.

Scene backup before changing to six rows: `Diagnostics/MicroLair/MicroLair_Graybox_before_six_window_rows.unity`. Earlier generated meshes are retained so the scene backups remain usable.

Rebuild through **Tools > Micro Lair > Build Rusty Steel Window Frames**. This rebuilds only the generated window group; edit the spacing constants and doorway placement in the editor authoring scripts before invoking it if a different layout is desired. Manual edits inside the generated group will be replaced when rebuilding.
