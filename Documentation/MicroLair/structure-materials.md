# Wall, ceiling, beam and pillar materials

Sources downloaded on 16 November 2026:

- `C:/Users/shari/Downloads/concrete_wall_006_4k.zip`
- `C:/Users/shari/Downloads/brushed_concrete_4k.zip`

Each archive contains six 4K PNG maps, extracted unchanged into its own folder under `Assets/Materials/`.

`Assets/Materials/concrete_wall_006.mat` covers all outer wall sections and doorway lintels, ceiling slabs and recesses, the curved upper fascia, and structural beams. It repeats once per four nominal/design metres on the existing mesh UVs.

`Assets/Materials/brushed concrete.mat` covers all four round pillars, including their caps. It repeats once per two nominal/design metres on the existing cylindrical UVs. These are starting artistic scales, not verified scan dimensions.

Both materials use opaque URP Lit with white tint and zero metallic. Diffuse is sRGB, normal uses the OpenGL normal-map import without a green-channel flip, and data maps are linear. A generated texture under each source folder's `Generated/` directory stores R = 0, G = ambient occlusion, B = 0, A = 1 - roughness. It is used for both occlusion and metallic/smoothness, with smoothness multiplier 1. The height map provides subtle parallax at 0.006; normal strength is 1. Geometry is not displaced.

The runtime maps retain 4096-pixel import limits, mipmaps, repeat wrapping and anisotropic filtering. Standalone imports use BC7 for color, height and packed data, and BC5 for normals. Original ARM maps are preserved, while the separate AO and roughness PNGs are used for Unity channel packing.

The existing damaged-concrete floor and stair material, player, cameras, lighting and geometry are preserved. The curved upper fascia uses a separate mesh asset under `Assets/Environment/MicroLair/Meshes/StructureUVs/` to correct its interleaved UV layout: outer and inner faces now follow continuous arc lengths, with planar mapping on its top and underside. Only UV0 and tangents change; positions, normals, triangles, lightmap UVs and collision shape are preserved. Other mesh references remain unchanged. A scene backup before application is stored at `Diagnostics/MicroLair/MicroLair_Graybox_before_structure_materials.unity`.

Reapply through **Tools > Micro Lair > Apply Wall And Pillar Materials**. Verification results are in `structure-materials-validation.txt`. The current previews are `structure-materials-interior.png` and `structure-materials-corridor.png`.
