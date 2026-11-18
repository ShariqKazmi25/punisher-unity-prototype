using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class MicroLairStructureMaterials
{
    const string Reports = "Documentation/MicroLair/";
    const int Size = 4096;
    public const string WallMaterialPath = "Assets/Materials/concrete_wall_006.mat";
    public const string PillarMaterialPath = "Assets/Materials/brushed concrete.mat";

    [MenuItem("Tools/Micro Lair/Apply Wall And Pillar Materials")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        Directory.CreateDirectory(Reports);
        var report = new StringBuilder();
        var wall = BuildMaterial("concrete_wall_006", WallMaterialPath, .25f, report);
        var pillar = BuildMaterial("brushed_concrete", PillarMaterialPath, .5f, report);
        Progress("Assigning wall, ceiling, beam and pillar materials.");
        var scene = SceneManager.GetSceneByPath(MicroLairGrayboxBuilder.ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(MicroLairGrayboxBuilder.ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var root = scene.GetRootGameObjects().First(g => g.name == "MicroLair_Graybox").transform;
        const string backup = "Diagnostics/MicroLair/MicroLair_Graybox_before_structure_materials.unity";
        Directory.CreateDirectory(Path.GetDirectoryName(backup));
        if (!File.Exists(backup)) Need(EditorSceneManager.SaveScene(scene, backup, true), "Scene backup saved.");

        var structure = root.Find("04_PillarsAndBeams").GetComponentsInChildren<MeshRenderer>(true);
        var pillars = structure.Where(r => r.name.Contains("Pillar")).ToArray();
        var beams = structure.Where(r => r.name.Contains("Beam")).ToArray();
        Need(pillars.Length == 4 && beams.Length == 9 && pillars.Length + beams.Length == structure.Length, "All four pillars and nine beams identified.");
        var walls = root.Find("06_OuterWalls").GetComponentsInChildren<MeshRenderer>(true);
        var ceilings = root.Find("05_Ceilings").GetComponentsInChildren<MeshRenderer>(true);
        var shell = walls.Concat(ceilings).Concat(beams).Distinct().ToArray();
        var targets = shell.Concat(pillars).ToArray();
        Need(walls.Length == 9 && ceilings.Length == 14 && shell.Length == 32, "All wall and ceiling sections identified.");
        AlignCurvedTrimUVs(root);
        var others = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true))
            .Where(r => !targets.Contains(r as MeshRenderer)).ToDictionary(r => r, r => r.sharedMaterials);
        var meshes = targets.ToDictionary(r => r, r => r.GetComponent<MeshFilter>().sharedMesh);
        var collision = targets.ToDictionary(r => r, r => EditorJsonUtility.ToJson(r.GetComponent<Collider>()));
        int shellSlots = Assign(shell, wall), pillarSlots = Assign(pillars, pillar);
        foreach (var renderer in targets)
        {
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            Need(mesh == meshes[renderer], "Mesh retained: " + renderer.name);
            Need(EditorJsonUtility.ToJson(renderer.GetComponent<Collider>()) == collision[renderer], "Collider retained: " + renderer.name);
            Need(renderer.sharedMaterials.Length == mesh.subMeshCount, "Every submesh has a material: " + renderer.name);
            Need(mesh.uv.Length == mesh.vertexCount && mesh.tangents.Length == mesh.vertexCount, "Texture coordinates and tangents: " + renderer.name);
        }
        Need(shell.All(r => r.sharedMaterials.All(m => m == wall)), "All shell slots use concrete_wall_006.");
        Need(pillars.All(r => r.sharedMaterials.All(m => m == pillar)), "All pillar slots use brushed concrete.");
        Need(others.All(pair => pair.Key.sharedMaterials.SequenceEqual(pair.Value)), "Existing floor, stair and gameplay materials retained.");
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Need(EditorSceneManager.SaveScene(scene), "Scene saved.");
        report.AppendLine($"PASS concrete_wall_006: {walls.Length} wall sections, {ceilings.Length} ceiling/fascia/recess sections, {beams.Length} beams; {shellSlots} material slots.");
        report.AppendLine($"PASS brushed concrete: {pillars.Length} pillars; {pillarSlots} material slots.");
        report.AppendLine("PASS Existing floor/stair/player materials and geometry unchanged. Curved upper trim uses a separate continuous-UV mesh; other mesh references are retained and collision shapes are unchanged.");
        report.AppendLine("PASS Valid UVs and tangents on every target mesh. All slots covered, including beam/ceiling undersides and pillar caps.");
        report.AppendLine("Tiling uses existing nominal-meter UVs: wall/ceiling/beam texture repeats every 4 design metres; pillar texture every 2 design metres.");
        report.AppendLine("Source PNGs are retained unmodified; no lighting or gameplay changes.");
        File.WriteAllText(Reports + "structure-materials-validation.txt", report.ToString());
        Progress("Rendering the textured structure previews.");
        Capture(root);
        Need(EditorSceneManager.SaveScene(scene), "Preview objects cleaned up and scene saved.");
        Selection.activeObject = wall;
        SceneView.RepaintAll();
        Progress("COMPLETE");
        Debug.Log("MICRO_LAIR_STRUCTURE_MATERIALS_COMPLETE: concrete_wall_006 on 32 wall/ceiling/beam objects; brushed concrete on 4 pillars.");
    }

    static void AlignCurvedTrimUVs(Transform root)
    {
        const string path = "Assets/Environment/MicroLair/Meshes/StructureUVs/Hub_UpperFascia_ContinuousUVs.asset";
        var trim = root.Find("05_Ceilings/Hub_MatchingCurvedUpperFascia");
        var filter = trim.GetComponent<MeshFilter>();
        var original = filter.sharedMesh;
        if (AssetDatabase.GetAssetPath(original) == path) return;
        var mesh = Object.Instantiate(original);
        var vertices = mesh.vertices; var uv = mesh.uv;
        Need(vertices.Length == 48 * 16 + 8, "Known curved-trim segment layout.");
        float s = root.Find(MicroLairRevisionB.MarkerName).localScale.x, outerU = 0, innerU = 0;
        Vector3 World(int i) => trim.TransformPoint(vertices[i]) / s;
        for (int segment = 0; segment < 48; segment++)
        {
            int a = segment * 16;
            float outerLength = Vector3.Distance(World(a), World(a + 3));
            float innerLength = Vector3.Distance(World(a + 4), World(a + 7));
            // The original UV counter interleaved outer, inner, top and bottom faces.
            // Separate arc lengths keep neighboring panels continuous on each side.
            uv[a] = new Vector2(outerU, World(a).y);
            uv[a + 1] = new Vector2(outerU, World(a + 1).y);
            uv[a + 2] = new Vector2(outerU + outerLength, World(a + 2).y);
            uv[a + 3] = new Vector2(outerU + outerLength, World(a + 3).y);
            uv[a + 4] = new Vector2(innerU + innerLength, World(a + 4).y);
            uv[a + 5] = new Vector2(innerU + innerLength, World(a + 5).y);
            uv[a + 6] = new Vector2(innerU, World(a + 6).y);
            uv[a + 7] = new Vector2(innerU, World(a + 7).y);
            for (int i = a + 8; i < a + 16; i++) { var p = World(i); uv[i] = new Vector2(p.x, p.z); }
            if (segment > 0)
            {
                Need(Vector2.Distance(uv[a - 13], uv[a]) < .0001f, "Continuous outer trim UVs.");
                Need(Vector2.Distance(uv[a - 12], uv[a + 7]) < .0001f, "Continuous inner trim UVs.");
            }
            outerU += outerLength; innerU += innerLength;
        }
        for (int i = 48 * 16; i < vertices.Length; i++) { var p = World(i); uv[i] = new Vector2(p.x, p.y); }
        mesh.uv = uv; mesh.RecalculateTangents(); mesh.name = "Hub_UpperFascia_ContinuousUVs";
        Need(mesh.vertices.SequenceEqual(original.vertices) && mesh.normals.SequenceEqual(original.normals)
            && mesh.triangles.SequenceEqual(original.triangles) && mesh.uv2.SequenceEqual(original.uv2), "Trim geometry and lightmap UVs preserved.");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
        else AssetDatabase.CreateAsset(mesh, path);
        Undo.RecordObject(filter, "Correct curved trim texture continuity"); filter.sharedMesh = mesh;
        var collider = trim.GetComponent<MeshCollider>();
        Undo.RecordObject(collider, "Retain trim collision with updated UVs"); collider.sharedMesh = mesh;
        EditorUtility.SetDirty(filter); EditorUtility.SetDirty(collider);
    }

    static int Assign(MeshRenderer[] renderers, Material material)
    {
        int slots = 0;
        foreach (var renderer in renderers)
        {
            Undo.RecordObject(renderer, "Apply structural concrete materials");
            slots += renderer.sharedMaterials.Length;
            renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            EditorUtility.SetDirty(renderer);
        }
        return slots;
    }

    static Material BuildMaterial(string sourceName, string materialPath, float tileRate, StringBuilder report)
    {
        string source = "Assets/Materials/" + sourceName + "_4k/";
        string prefix = source + "textures/" + sourceName + "_";
        string packed = source + "Generated/" + sourceName + "_URP_4k.png";
        Progress("Importing " + sourceName + " texture maps.");
        Configure(prefix + "diff_4k.png", true, false, TextureImporterFormat.BC7);
        Configure(prefix + "nor_gl_4k.png", false, true, TextureImporterFormat.BC5);
        Configure(prefix + "disp_4k.png", false, false, TextureImporterFormat.BC7);
        if (!File.Exists(packed) || new[] { "ao", "rough" }.Any(map => File.GetLastWriteTimeUtc(prefix + map + "_4k.png") > File.GetLastWriteTimeUtc(packed)))
        {
            Configure(prefix + "ao_4k.png", false, false, TextureImporterFormat.R8);
            Configure(prefix + "rough_4k.png", false, false, TextureImporterFormat.R8);
            Progress("Packing " + sourceName + " roughness and occlusion for URP.");
            Pack(prefix, packed, report);
        }
        Configure(packed, false, false, TextureImporterFormat.BC7);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        Need(shader && shader.isSupported, "URP Lit available.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        Undo.RecordObject(material, "Configure structural concrete material");
        material.shader = shader;
        material.name = Path.GetFileNameWithoutExtension(materialPath);
        material.SetFloat("_WorkflowMode", 1);
        material.SetFloat("_Surface", 0); material.SetFloat("_AlphaClip", 0);
        material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", 1);
        material.SetFloat("_SmoothnessTextureChannel", 0);
        material.SetFloat("_BumpScale", 1); material.SetFloat("_OcclusionStrength", 1);
        material.SetFloat("_Parallax", .006f);
        material.SetColor("_BaseColor", Color.white); material.SetColor("_Color", Color.white);
        material.SetTexture("_BaseMap", Load(prefix + "diff_4k.png"));
        material.SetTexture("_MainTex", Load(prefix + "diff_4k.png"));
        material.SetTexture("_BumpMap", Load(prefix + "nor_gl_4k.png"));
        material.SetTexture("_ParallaxMap", Load(prefix + "disp_4k.png"));
        material.SetTexture("_MetallicGlossMap", Load(packed)); material.SetTexture("_OcclusionMap", Load(packed));
        foreach (string property in new[] { "_BaseMap", "_MainTex", "_BumpMap", "_ParallaxMap", "_MetallicGlossMap", "_OcclusionMap" })
        {
            material.SetTextureScale(property, Vector2.one * tileRate);
            material.SetTextureOffset(property, Vector2.zero);
            Need(material.GetTexture(property).width == Size && material.GetTexture(property).height == Size, "4K import: " + property);
        }
        foreach (string keyword in new[] { "_NORMALMAP", "_PARALLAXMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP" }) material.EnableKeyword(keyword);
        foreach (string keyword in new[] { "_SPECULAR_SETUP", "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", "_SURFACE_TYPE_TRANSPARENT", "_ALPHATEST_ON" }) material.DisableKeyword(keyword);
        material.renderQueue = -1;
        EditorUtility.SetDirty(material);
        Need(((TextureImporter)AssetImporter.GetAtPath(prefix + "diff_4k.png")).sRGBTexture, "Color is sRGB.");
        Need(!((TextureImporter)AssetImporter.GetAtPath(packed)).sRGBTexture, "Packed data is linear.");
        Need(((TextureImporter)AssetImporter.GetAtPath(prefix + "nor_gl_4k.png")).textureType == TextureImporterType.NormalMap, "Normal import.");
        report.AppendLine("PASS " + materialPath + ": 4K color, OpenGL normal, AO/smoothness and height; opaque, nonmetallic URP Lit.");
        return material;
    }

    static void Configure(string path, bool srgb, bool normal, TextureImporterFormat format)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); importer = AssetImporter.GetAtPath(path) as TextureImporter; }
        Need(importer != null, "Texture importer: " + path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = srgb; importer.maxTextureSize = Size;
        importer.mipmapEnabled = true; importer.isReadable = false;
        importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 8;
        importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = false;
        if (normal) { importer.convertToNormalmap = false; importer.flipGreenChannel = false; }
        importer.textureCompression = format == TextureImporterFormat.R8 ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
        var platform = importer.GetPlatformTextureSettings("Standalone");
        platform.name = "Standalone"; platform.overridden = true; platform.maxTextureSize = Size;
        platform.format = format; platform.compressionQuality = 100; platform.crunchedCompression = false;
        importer.SetPlatformTextureSettings(platform);
        importer.SaveAndReimport();
    }

    static void Pack(string prefix, string output, StringBuilder report)
    {
        var shader = Shader.Find("Hidden/MicroLair/PackConcreteData");
        Need(shader && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "Packing shader compiled.");
        var packer = new Material(shader);
        packer.SetTexture("_OcclusionTex", Load(prefix + "ao_4k.png"));
        var rt = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var readback = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
        var previous = RenderTexture.active; bool previousSRGB = GL.sRGBWrite;
        try
        {
            GL.sRGBWrite = false;
            Graphics.Blit(Load(prefix + "rough_4k.png"), rt, packer);
            RenderTexture.active = rt;
            readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); readback.Apply(false, false);
            float minSmooth = 1, maxSmooth = 0, minAO = 1, maxAO = 0;
            for (int y = 47; y < Size; y += 127) for (int x = 71; x < Size; x += 127)
            {
                var pixel = readback.GetPixel(x, y);
                Need(pixel.r < .005f && pixel.b < .005f, "Metallic channels remain zero.");
                minSmooth = Mathf.Min(minSmooth, pixel.a); maxSmooth = Mathf.Max(maxSmooth, pixel.a);
                minAO = Mathf.Min(minAO, pixel.g); maxAO = Mathf.Max(maxAO, pixel.g);
            }
            Need(maxAO > .5f && maxSmooth > minSmooth, "AO and smoothness contain valid data.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllBytes(output, readback.EncodeToPNG());
            report.AppendLine($"PASS {Path.GetFileName(output)} packed R=0, G=AO ({minAO:F3}..{maxAO:F3}), B=0, A=1-roughness ({minSmooth:F3}..{maxSmooth:F3}).");
        }
        finally
        {
            GL.sRGBWrite = previousSRGB; RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(readback); Object.DestroyImmediate(packer);
        }
        AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceSynchronousImport);
    }

    static void Capture(Transform root)
    {
        float s = root.Find(MicroLairRevisionB.MarkerName).localScale.x;
        var camera = MicroLairRevisionC.NewCamera(root);
        try
        {
            camera.transform.position = new Vector3(-1, 2.5f, -10.8f) * s;
            camera.transform.LookAt(new Vector3(0, 2.7f, 4) * s); camera.fieldOfView = 70;
            MicroLairRevisionC.Render(camera, "structure-materials-interior.png");
            camera.transform.position = new Vector3(4.4f, 2.85f, -1.5f) * s;
            camera.transform.LookAt(new Vector3(7.8f, 3.1f, 10) * s); camera.fieldOfView = 65;
            MicroLairRevisionC.Render(camera, "structure-materials-corridor.png");
        }
        finally { Object.DestroyImmediate(camera.gameObject); }
    }
    static Texture2D Load(string path)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Need(texture, "Texture loaded: " + path); return texture;
    }
    static void Need(bool condition, string message) { if (!condition) throw new InvalidOperationException("Structure materials: " + message); }
    static void Progress(string message) { File.WriteAllText(Reports + "structure-materials-progress.txt", DateTime.Now.ToString("s") + " " + message); }
}
