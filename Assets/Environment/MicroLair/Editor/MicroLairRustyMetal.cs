using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MicroLairRustyMetal
{
    public const string MaterialPath = "Assets/Materials/rusty metal.mat";
    const string Root = "Assets/Materials/rusty_metal_05_4k/";
    const string Prefix = Root + "textures/rusty_metal_05_";
    const string Packed = Root + "Generated/rusty_metal_05_URP_4k.png";
    const int Size = 4096;

    public static Material Build()
    {
        Import(Prefix + "diff_4k.png", true, false, TextureImporterFormat.BC7);
        Import(Prefix + "nor_gl_4k.png", false, true, TextureImporterFormat.BC5);
        if (!File.Exists(Packed) || File.GetLastWriteTimeUtc(Prefix + "arm_4k.png") > File.GetLastWriteTimeUtc(Packed))
        {
            Import(Prefix + "arm_4k.png", false, false, TextureImporterFormat.RGBA32);
            Pack();
        }
        Import(Packed, false, false, TextureImporterFormat.BC7);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader || !shader.isSupported) throw new InvalidOperationException("URP Lit shader is unavailable.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
        Undo.RecordObject(material, "Configure rusty steel window material");
        material.name = "rusty metal"; material.shader = shader;
        material.SetColor("_BaseColor", new Color(.45f, .45f, .45f, 1));
        material.SetColor("_Color", new Color(.45f, .45f, .45f, 1));
        material.SetFloat("_WorkflowMode", 1); material.SetFloat("_Surface", 0); material.SetFloat("_AlphaClip", 0);
        material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1); material.SetFloat("_SmoothnessTextureChannel", 0);
        material.SetFloat("_BumpScale", .7f); material.SetFloat("_OcclusionStrength", 1);
        material.SetTexture("_BaseMap", Load(Prefix + "diff_4k.png"));
        material.SetTexture("_MainTex", Load(Prefix + "diff_4k.png"));
        material.SetTexture("_BumpMap", Load(Prefix + "nor_gl_4k.png"));
        material.SetTexture("_MetallicGlossMap", Load(Packed)); material.SetTexture("_OcclusionMap", Load(Packed));
        foreach (var property in new[] { "_BaseMap", "_MainTex", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
        {
            material.SetTextureScale(property, new Vector2(2, 2)); material.SetTextureOffset(property, Vector2.zero);
            if (material.GetTexture(property).width != Size) throw new InvalidOperationException("Rust map did not import at 4K.");
        }
        foreach (string key in new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP" }) material.EnableKeyword(key);
        foreach (string key in new[] { "_SPECULAR_SETUP", "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", "_SURFACE_TYPE_TRANSPARENT", "_ALPHATEST_ON", "_PARALLAXMAP" }) material.DisableKeyword(key);
        material.renderQueue = -1; EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
        return material;
    }
    static void Import(string path, bool srgb, bool normal, TextureImporterFormat format)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = srgb; importer.maxTextureSize = Size; importer.mipmapEnabled = true; importer.isReadable = false;
        importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 8;
        importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = false;
        if (normal) { importer.convertToNormalmap = false; importer.flipGreenChannel = false; }
        importer.textureCompression = format == TextureImporterFormat.RGBA32 ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
        var platform = importer.GetPlatformTextureSettings("Standalone");
        platform.name = "Standalone"; platform.overridden = true; platform.maxTextureSize = Size;
        platform.format = format; platform.compressionQuality = 100; platform.crunchedCompression = false;
        importer.SetPlatformTextureSettings(platform); importer.SaveAndReimport();
    }
    static void Pack()
    {
        var shader = Shader.Find("Hidden/MicroLair/PackRustyMetal");
        if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Rust packing shader error.");
        var mat = new Material(shader);
        var rt = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
        var previous = RenderTexture.active; bool previousSRGB = GL.sRGBWrite;
        try
        {
            GL.sRGBWrite = false; Graphics.Blit(Load(Prefix + "arm_4k.png"), rt, mat); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); tex.Apply(false, false);
            float metalMin = 1, metalMax = 0, smoothMin = 1, smoothMax = 0;
            for (int y = 37; y < Size; y += 131) for (int x = 61; x < Size; x += 131)
            {
                var p = tex.GetPixel(x, y); metalMin = Mathf.Min(metalMin, p.r); metalMax = Mathf.Max(metalMax, p.r);
                smoothMin = Mathf.Min(smoothMin, p.a); smoothMax = Mathf.Max(smoothMax, p.a);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Packed)); File.WriteAllBytes(Packed, tex.EncodeToPNG());
            File.WriteAllText("Documentation/MicroLair/rusty-metal-packing.txt", $"ARM to URP: R=metallic, G=AO, B=0, A=1-roughness. Sample metallic {metalMin:F3}..{metalMax:F3}; smoothness {smoothMin:F3}..{smoothMax:F3}. Metallic variation is preserved.\n");
        }
        finally { GL.sRGBWrite = previousSRGB; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(mat); }
        AssetDatabase.ImportAsset(Packed, ImportAssetOptions.ForceSynchronousImport);
    }
    static Texture2D Load(string path)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (!tex) throw new InvalidOperationException("Missing texture " + path); return tex;
    }
}
