# Concrete floor material — PNG rebuild

Source: the redownloaded `C:/Users/shari/Downloads/concrete_floor_damaged_01_8k.zip`. All six files are 8192 x 8192, 16-bit PNGs. They are extracted unchanged to `Assets/Materials/concrete_floor_damaged_01_8k/textures/`. The previous JPG/EXR set and material had been removed before this rebuild.

`Assets/Materials/concrete floor.mat` uses URP Lit with the matching diffuse, OpenGL normal, roughness, ambient occlusion and height maps. The generated `Generated/concrete_floor_damaged_01_URP_PNG_8k.png` stores R = 0 metallic, G = ambient occlusion, B = 0, and A = 1 - roughness. It serves both the metallic/smoothness and occlusion slots; smoothness multiplier is 1. Color is sRGB; data maps are linear. The normal is imported as a normal map without flipping green.

Compared with the first material:

- Tiling changes from 0.5 to 1/6, so a repeat covers six nominal/design metres rather than two. This is three times fewer repetitions per axis, with larger damaged patches. It is conventional repeating texture mapping, not procedural randomization.
- Normal strength is 1.15. The supplied height map drives subtle parallax at 0.012 to strengthen the impression of chipped surface depth. This affects shading only, not geometry or collision.
- All active source maps now come from the newly downloaded PNG set. The supplied ARM map is retained; the separate AO and roughness maps are packed into Unity's required channels instead.
- Horizontal faces use continuous world-coordinate UVs rotated 13 degrees. Stair risers use world X/Y coordinates, so each step samples a different part of the map. These UV/tangent edits are saved as separate meshes under `Assets/Environment/MicroLair/Meshes/ConcreteUVs/`; vertex positions, normals, triangle indices, lightmap UVs and collision shapes are preserved.

The same shared material covers the lower floor, five raised slabs/aisles, twelve stair steps (including treads and risers), and rear deck: 19 renderers and 57 material slots. Walls, ceilings, pillars, lighting, player and gameplay are unchanged. The website's sphere preview uses different geometry and lighting, so exact brightness cannot be reproduced by material settings alone.

Runtime maps retain an 8192-pixel import limit with mipmaps, repeat wrapping and anisotropic filtering. PC uses BC7 for diffuse, height and packed data, and BC5 for the normal. AO and roughness are imported as linear R8 to pack the data; the original 16-bit PNG source files are untouched.

Reapply via **Tools > Micro Lair > Apply Concrete Floor Material**. The scene backup before this rebuild is `Diagnostics/MicroLair/MicroLair_Graybox_before_png_concrete_floor.unity`.

Current reports and previews have the `concrete-floor-png-` prefix. Older `concrete-floor-` images/reports document the initial material and are not the current result.

The shading check found valid normal/tangent frames on all 19 renderers. A temporary sphere preview with controlled directional lights confirmed that enabling the normal/height maps changes the rendered surface. `concrete-floor-png-sphere.png` shows that material test; its lights are not added to the graybox. The existing graybox uses flat ambient illumination (0.65 gray), which softens relief compared with the supplied website reference.
