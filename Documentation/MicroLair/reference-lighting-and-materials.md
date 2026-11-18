# Micro's lair: reference lighting and full-resolution materials

## Reading the reference

The central enclosure is the brightest broad zone, with a cool blue-green cast. The fluorescent strips are inside the cage and high overhead, set among the secondary ceiling grid. Their reflected light reveals the ceiling and workstation surfaces; it does not flood the outer fascia or foreground pillar. Earlier portfolio images show the physical tubes, ceiling wells, and missing panels more clearly than the dark production still. Screen glow is localized to the equipment and is secondary to the overhead sources.

The small pendant lamps outside the hub read warm cream/yellow. They follow the stair approaches and longitudinal aisles. More warm practical lamps punctuate the rear utility wall and living side. Their bright lenses are visible, but their light falls into separated pools rather than lighting every wall uniformly. The upper perimeter and foreground remain dark, with blue-grey reflected fill preserving silhouettes. The production still includes atmospheric haze; this pass uses restrained bloom for lamp glow, not a volumetric-fog simulation.

## Scene implementation

`MicroLair_Graybox/12_SurfacePolish` contains eight simple fluorescent fixtures inside the hub, four cool downlights with shadows, four low-intensity ceiling-bounce lights, six warm aisle pendants, two rear utility lamps, and one low-intensity foreground fill. Positions follow the existing scaled graybox, including its taller ceilings and extended aisles. They are reconstructed estimates, not surveyed film-set positions or measured color temperatures. Existing window rows, doorway dimensions, structural meshes and gameplay collision remain unchanged. Decorative fixture meshes have no gameplay colliders.

The hub uses restrained cyan-blue white; pendant lenses and pools use amber-white. Two baked, box-projected HDR reflection probes give the concrete and steel local reflections. URP's existing ambient occlusion is retained. The scene-specific Volume uses ACES, mild contrast, slight desaturation and subtle bloom. No motion blur, chromatic aberration or depth-of-field blur obscures the texture detail.

## Materials and resolution

The scene uses dedicated `- Lair` material variants in `Assets/Environment/MicroLair/SurfacePolish`. Original material assets and downloaded source images are retained. The floor's color, normal, height and packed roughness/occlusion maps are 8192 square. Wall, pillar and rusty-steel maps are 4096 square. Generated surface maps now match those resolutions rather than the earlier 2048-square intermediate maps. Import settings retain mipmaps, 16x anisotropic filtering and high-quality BC7/BC5 GPU compression; source resolution is not reduced by global mip limits or streaming.

Roughness remains varied across each material, with a modest smoothness lift for readable highlights. Concrete stays nonmetallic; the rusty-steel source metallic channel is retained. Relief comes from normal maps and restrained floor parallax, not exaggerated geometry displacement. Full-resolution import preserves available detail; it does not create additional detail absent from the source or guarantee every distant surface samples its largest mip.

## Verification and restoration

`surface-polish-validation.txt` records imported runtime texture dimensions and preservation checks. Rendered comparisons are `surface-before.png`, `reference-lighting-front.png`, `reference-lighting-aisle.png`, and `reference-material-closeup.png`. The profile's overrides are saved as persistent subassets.

Scene restore point: `Diagnostics/MicroLair/MicroLair_before_reference_lighting.unity`. Earlier pre-polish scene backup and original material assets are also retained. Pipeline was added to Packages/manifest.json to enable the requested Unity plug-in connection; its original manifest copy is `Diagnostics/MicroLair/manifest-before-pipeline.json`.

The local Unity plug-in guides used were `unity-cli.md`, `integration-advanced.md`, `urp-postprocessing.md`, and `effect-reference.md`.

Final brightness adjustment (18 November): hub downlights increased from 5.5 to 8 and ceiling bounce from 0.9 to 1.2. Direct aisle pools reduced from 3.2 to 1.2. Four warm side-hub lights and six restrained reflected-fill lights provide spill into the corridors. Reflection cubemaps use persistent Custom probe assignments. The three Volume overrides are persistent subassets. Console check found no new errors; one pre-existing Unity AI authentication error remains. Final preview: reference-lighting-front.png. Final live adjustments are recorded in Diagnostics/MicroLair/BrightenHubs.cs, FinalizeReflections.cs and HubBounce.cs; the base authoring menu recreates the earlier lighting baseline.

Pendant revision: replaced six corridor pendants with two, located inside the left and right hubs at nominal X ±8, Z 4.3. Consolidated side-hub direct spotlights into these two sources. Existing low reflected fill and rear utility lamps retained. Updated local reflection cubemaps and saved scene. No new console errors since cursor 66.
