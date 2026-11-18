using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class MicroLairConcreteFloor
{
    public const string MaterialPath = "Assets/Materials/concrete floor.mat";
    const string Source = "Assets/Materials/concrete_floor_damaged_01_8k/";
    const string Tex = Source + "textures/concrete_floor_damaged_01_";
    const string Packed = Source + "Generated/concrete_floor_damaged_01_URP_PNG_8k.png";
    const float TileRate = 1f / 6f;
    const string Reports = "Documentation/MicroLair/";
    static readonly string[] Groups = { "01_LowerFloor", "02_RaisedPlatforms", "03_Stairs", "08_ProvisionalRearConnection" };

    [MenuItem("Tools/Micro Lair/Apply Concrete Floor Material")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        Directory.CreateDirectory(Reports);
        Progress("Importing the 8K color and normal maps.");
        Configure(Tex + "diff_8k.png", true, false, TextureImporterFormat.BC7);
        Configure(Tex + "nor_gl_8k.png", false, true, TextureImporterFormat.BC5);
        Configure(Tex + "disp_8k.png", false, false, TextureImporterFormat.BC7);

        if (!File.Exists(Packed))
        {
            Progress("Preparing linear roughness and occlusion data.");
            Configure(Tex + "ao_8k.png", false, false, TextureImporterFormat.R8);
            Configure(Tex + "rough_8k.png", false, false, TextureImporterFormat.R8);
            Progress("Packing metallic, occlusion and smoothness channels.");
            Pack();
        }
        Configure(Packed, false, false, TextureImporterFormat.BC7);
        Progress("Creating the concrete floor material and assigning the scene.");

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        Need(shader && shader.isSupported, "URP Lit shader is available.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (!material)
        {
            material = new Material(shader) { name = "concrete floor" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        Undo.RecordObject(material, "Configure concrete floor material");
        material.shader = shader;
        material.name = "concrete floor";
        material.SetFloat("_WorkflowMode", 1);
        material.SetFloat("_Surface", 0);
        material.SetFloat("_AlphaClip", 0);
        material.SetFloat("_Metallic", 0);
        material.SetFloat("_Smoothness", 1);
        material.SetFloat("_SmoothnessTextureChannel", 0);
        material.SetFloat("_BumpScale", 1.15f);
        material.SetFloat("_Parallax", .012f);
        material.SetFloat("_OcclusionStrength", 1);
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_Color", Color.white);
        material.SetTexture("_BaseMap", Load(Tex + "diff_8k.png"));
        material.SetTexture("_MainTex", Load(Tex + "diff_8k.png"));
        material.SetTexture("_BumpMap", Load(Tex + "nor_gl_8k.png"));
        material.SetTexture("_MetallicGlossMap", Load(Packed));
        material.SetTexture("_OcclusionMap", Load(Packed));
        material.SetTexture("_ParallaxMap", Load(Tex + "disp_8k.png"));
        foreach (string property in new[] { "_BaseMap", "_MainTex", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_ParallaxMap" })
        {
            // Three times fewer repeats per axis than the initial material.
            material.SetTextureScale(property, Vector2.one * TileRate);
            material.SetTextureOffset(property, Vector2.zero);
        }
        foreach (string keyword in new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP", "_PARALLAXMAP" }) material.EnableKeyword(keyword);
        foreach (string keyword in new[] { "_SPECULAR_SETUP", "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", "_SURFACE_TYPE_TRANSPARENT", "_ALPHATEST_ON" }) material.DisableKeyword(keyword);
        material.renderQueue = -1;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        var scene = SceneManager.GetSceneByPath(MicroLairGrayboxBuilder.ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(MicroLairGrayboxBuilder.ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var root = scene.GetRootGameObjects().First(g => g.name == "MicroLair_Graybox").transform;
        const string backup = "Diagnostics/MicroLair/MicroLair_Graybox_before_png_concrete_floor.unity";
        Directory.CreateDirectory(Path.GetDirectoryName(backup));
        if (!File.Exists(backup)) Need(EditorSceneManager.SaveScene(scene, backup, true), "Scene backup saved.");
        var targets = Groups.SelectMany(group => root.Find(group).GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        Need(targets.Length == 19, "Expected 19 floor/platform/stair renderers.");
        AlignFloorUVs(root, targets);
        var other = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true))
            .Where(r => !targets.Contains(r as MeshRenderer)).ToDictionary(r => r, r => r.sharedMaterials);
        var meshes = targets.ToDictionary(r => r, r => r.GetComponent<MeshFilter>().sharedMesh);
        var colliders = targets.ToDictionary(r => r, r => EditorJsonUtility.ToJson(r.GetComponent<Collider>()));
        int slots = 0;
        foreach (var renderer in targets)
        {
            Undo.RecordObject(renderer, "Apply concrete floor material");
            slots += renderer.sharedMaterials.Length;
            renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            EditorUtility.SetDirty(renderer);
        }
        foreach (var renderer in targets)
        {
            Need(renderer.sharedMaterials.All(m => m == material), "All material slots assigned: " + renderer.name);
            Need(renderer.GetComponent<MeshFilter>().sharedMesh == meshes[renderer], "Mesh unchanged: " + renderer.name);
            Need(EditorJsonUtility.ToJson(renderer.GetComponent<Collider>()) == colliders[renderer], "Collider unchanged: " + renderer.name);
        }
        Need(other.All(pair => pair.Key.sharedMaterials.SequenceEqual(pair.Value)), "All other scene materials unchanged.");
        Need(material.GetTexture("_BaseMap").width == 8192 && material.GetTexture("_BumpMap").width == 8192 && Load(Packed).width == 8192, "Runtime maps imported at 8192 pixels.");
        Need(!((TextureImporter)AssetImporter.GetAtPath(Packed)).sRGBTexture, "Packed data uses linear color space.");
        Need(((TextureImporter)AssetImporter.GetAtPath(Tex + "nor_gl_8k.png")).textureType == TextureImporterType.NormalMap, "Normal map import configured.");
        Need(material.IsKeywordEnabled("_PARALLAXMAP") && !((TextureImporter)AssetImporter.GetAtPath(Tex + "disp_8k.png")).sRGBTexture, "Linear height map and parallax shading enabled.");
        EditorSceneManager.MarkSceneDirty(scene);
        Need(EditorSceneManager.SaveScene(scene), "Textured scene saved.");
        var report = new StringBuilder();
        report.AppendLine("PASS URP Lit material: " + MaterialPath);
        report.AppendLine("PASS New PNG-only archive: 8192px color (sRGB), OpenGL normal (normal-map import), height and packed linear data.");
        report.AppendLine("Packed data: R = 0 metallic, G = AO, B = 0, A = 1 - roughness. Smoothness multiplier = 1.");
        report.AppendLine("Tiling = (1/6, 1/6) on nominal-meter UVs; one repeat per six nominal meters. Three times fewer repeats per axis.");
        report.AppendLine("Normal strength 1.15; parallax 0.012. Height affects shading only, with no displaced geometry/collision.");
        report.AppendLine("PASS Horizontal UVs share world coordinates, rotated 13 degrees; stair risers share world X/Y coordinates. Steps no longer restart the same texture patch.");
        report.AppendLine($"PASS {targets.Length} floor/platform/stair renderers, {slots} material slots.");
        foreach (string group in Groups) report.AppendLine("  " + group + ": " + root.Find(group).GetComponentsInChildren<MeshRenderer>(true).Length);
        report.AppendLine("PASS Mesh positions/normals/triangles and collider shape unchanged; UVs/tangents stored as separate mesh assets. All other scene renderer materials unchanged.");
        report.AppendLine("PASS Scene saved; source SampleScene was not opened or changed by this operation.");
        File.WriteAllText(Reports + "concrete-floor-png-validation.txt", report.ToString());
        Progress("Rendering floor and stair previews.");
        Capture(root);
        Need(EditorSceneManager.SaveScene(scene), "Scene saved after preview cleanup.");
        Selection.activeObject = material;
        Progress("COMPLETE");
        Debug.Log("MICRO_LAIR_CONCRETE_FLOOR_COMPLETE: concrete floor applied to " + targets.Length + " renderers and " + slots + " slots.");
    }

    static void AlignFloorUVs(Transform root, MeshRenderer[] targets)
    {
        const string folder = "Assets/Environment/MicroLair/Meshes/ConcreteUVs";
        Directory.CreateDirectory(folder);
        float s = root.Find(MicroLairRevisionB.MarkerName).localScale.x;
        float angle = 13 * Mathf.Deg2Rad, c = Mathf.Cos(angle), sn = Mathf.Sin(angle);
        for (int index = 0; index < targets.Length; index++)
        {
            var renderer = targets[index];
            var filter = renderer.GetComponent<MeshFilter>();
            var original = filter.sharedMesh;
            string path = folder + "/" + index.ToString("00") + "_" + renderer.name + ".asset";
            if (AssetDatabase.GetAssetPath(original) == path) continue;
            var mesh = Object.Instantiate(original);
            var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv;
            bool stair = renderer.transform.IsChildOf(root.Find("03_Stairs"));
            for (int i = 0; i < vertices.Length; i++)
            {
                var world = filter.transform.TransformPoint(vertices[i]) / s;
                var normal = filter.transform.TransformDirection(normals[i]);
                if (Mathf.Abs(normal.y) > .9f)
                    uv[i] = new Vector2(c * world.x - sn * world.z, sn * world.x + c * world.z);
                else if (stair)
                    uv[i] = Mathf.Abs(normal.z) > .5f ? new Vector2(world.x, world.y) : new Vector2(world.z, world.y);
            }
            mesh.uv = uv;
            mesh.RecalculateTangents();
            Need(mesh.vertices.SequenceEqual(original.vertices) && mesh.normals.SequenceEqual(original.normals)
                && mesh.triangles.SequenceEqual(original.triangles) && mesh.uv2.SequenceEqual(original.uv2), "Only UV0/tangents changed: " + renderer.name);
            mesh.name = renderer.name + "_ContinuousConcreteUVs";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
            else AssetDatabase.CreateAsset(mesh, path);
            Undo.RecordObject(filter, "Align concrete floor texture coordinates");
            filter.sharedMesh = mesh;
            if (renderer.TryGetComponent<MeshCollider>(out var collider))
            {
                Undo.RecordObject(collider, "Preserve collider mesh with aligned floor UVs");
                collider.sharedMesh = mesh;
            }
            EditorUtility.SetDirty(filter);
        }
        AssetDatabase.SaveAssets();
    }

    static void Configure(string path, bool srgb, bool normal, TextureImporterFormat format)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); importer = AssetImporter.GetAtPath(path) as TextureImporter; }
        Need(importer != null, "Texture importer: " + path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = srgb;
        importer.maxTextureSize = 8192;
        importer.mipmapEnabled = true;
        importer.isReadable = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        if (normal) { importer.convertToNormalmap = false; importer.flipGreenChannel = false; }
        importer.textureCompression = format == TextureImporterFormat.R8 ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
        var platform = importer.GetPlatformTextureSettings("Standalone");
        platform.name = "Standalone";
        platform.overridden = true;
        platform.maxTextureSize = 8192;
        platform.format = format;
        platform.compressionQuality = 100;
        platform.crunchedCompression = false;
        importer.SetPlatformTextureSettings(platform);
        importer.SaveAndReimport();
    }

    static void Pack()
    {
        var shader = Shader.Find("Hidden/MicroLair/PackConcreteData");
        Need(shader && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "Packing shader compiled.");
        var packer = new Material(shader);
        packer.SetTexture("_OcclusionTex", Load(Tex + "ao_8k.png"));
        var rt = RenderTexture.GetTemporary(8192, 8192, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var readback = new Texture2D(8192, 8192, TextureFormat.RGBA32, false, true);
        var previous = RenderTexture.active;
        bool previousSrgb = GL.sRGBWrite;
        try
        {
            GL.sRGBWrite = false;
            Graphics.Blit(Load(Tex + "rough_8k.png"), rt, packer);
            RenderTexture.active = rt;
            readback.ReadPixels(new Rect(0, 0, 8192, 8192), 0, 0);
            readback.Apply(false, false);
            float minSmooth = 1, maxSmooth = 0, minAO = 1, maxAO = 0;
            for (int y = 53; y < 8192; y += 257)
            for (int x = 71; x < 8192; x += 257)
            {
                var sample = readback.GetPixel(x, y);
                Need(sample.r < .005f && sample.b < .005f, "Nonmetallic packed channels.");
                minSmooth = Mathf.Min(minSmooth, sample.a); maxSmooth = Mathf.Max(maxSmooth, sample.a);
                minAO = Mathf.Min(minAO, sample.g); maxAO = Mathf.Max(maxAO, sample.g);
            }
            Need(maxSmooth - minSmooth > .05f && maxAO > .5f, "Packed roughness/AO contain texture data.");
            Directory.CreateDirectory(Path.GetDirectoryName(Packed));
            File.WriteAllBytes(Packed, readback.EncodeToPNG());
            File.WriteAllText(Reports + "concrete-floor-png-packing.txt", $"PASS sampled packed channels from matching PNG sources. Smoothness {minSmooth:F4}..{maxSmooth:F4}; AO {minAO:F4}..{maxAO:F4}. R/B are zero.\n");
        }
        finally
        {
            GL.sRGBWrite = previousSrgb;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(readback);
            Object.DestroyImmediate(packer);
        }
        AssetDatabase.ImportAsset(Packed, ImportAssetOptions.ForceSynchronousImport);
    }

    static void Capture(Transform root)
    {
        float s = root.Find(MicroLairRevisionB.MarkerName).localScale.x;
        var camera = MicroLairRevisionC.NewCamera(root);
        try
        {
            camera.transform.position = new Vector3(-1, 2.5f, -10.8f) * s;
            camera.transform.LookAt(new Vector3(0, 2.7f, 4) * s);
            camera.fieldOfView = 70;
            MicroLairRevisionC.Render(camera, "concrete-floor-png-interior.png");
            camera.transform.position = new Vector3(-5.8f, 3.2f, -7.3f) * s;
            camera.transform.LookAt(new Vector3(-3.5f, .65f, -2.4f) * s);
            camera.fieldOfView = 65;
            MicroLairRevisionC.Render(camera, "concrete-floor-png-stairs.png");
            camera.transform.position = new Vector3(-6.6f, 1.45f, -8.3f) * s;
            camera.transform.LookAt(new Vector3(-4.9f, 0, -5.5f) * s);
            camera.fieldOfView = 55;
            MicroLairRevisionC.Render(camera, "concrete-floor-png-detail.png");
        }
        finally { Object.DestroyImmediate(camera.gameObject); }
    }

    static Texture2D Load(string path)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Need(texture, "Texture loaded: " + path);
        return texture;
    }
    static void Need(bool condition, string message) { if (!condition) throw new InvalidOperationException("Concrete floor: " + message); }
    static void Progress(string message) { File.WriteAllText(Reports + "concrete-floor-progress.txt", DateTime.Now.ToString("s") + " " + message); }
}
