using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Authoring tool only. The saved scene contains ordinary mesh objects, not a runtime generator.
/// Dimensions follow Documentation/MicroLair/structural-reconstruction.md (estimated revision A).
/// </summary>
public static class MicroLairGrayboxBuilder
{
    public const string ScenePath = "Assets/Scenes/MicroLair_Graybox.unity";
    private const string AssetRoot = "Assets/Environment/MicroLair";
    private const string ReportRoot = "Documentation/MicroLair";
    private const float DeckHeight = 1.02f;
    private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
    private static Material floor, wall, stairs, column, ceiling;

    [MenuItem("Tools/Micro Lair/Create Graybox Scene")]
    public static void CreateWithoutChangingOpenScenes()
    {
        var previous = SceneManager.GetActiveScene();
        Scene generated = default;
        try
        {
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("The graybox already exists. Use Open Graybox Scene; existing work will not be overwritten.");
            Build();
            generated = SceneManager.GetActiveScene();
            ValidateSavedScene();
            CapturePreviews();
            Debug.Log("MICRO_LAIR_BUILD_SUCCESS: " + ScenePath);
        }
        finally
        {
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            if (generated.IsValid() && generated.path == ScenePath)
                EditorSceneManager.CloseScene(generated, true);
        }
    }

    // Used by Unity -batchmode -executeMethod. Never replace an existing authored scene.
    public static void BuildBatch()
    {
        if (File.Exists(ScenePath))
            throw new InvalidOperationException("Scene already exists; refusing to overwrite authored work: " + ScenePath);
        Build();
        ValidateSavedScene();
        CapturePreviews();
        Debug.Log("MICRO_LAIR_BUILD_SUCCESS: " + ScenePath);
    }

    [MenuItem("Tools/Micro Lair/Open Graybox Scene")]
    public static void OpenScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath);
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
            sceneView.LookAt(new Vector3(0, 1, 0), Quaternion.Euler(35, -30, 0), 19);
    }

    [MenuItem("Tools/Micro Lair/Toggle Ceiling Visibility (Scene View)")]
    public static void ToggleCeilingVisibility()
    {
        var root = FindRoot();
        var group = root.transform.Find("05_Ceilings").gameObject;
        if (SceneVisibilityManager.instance.IsHidden(group))
            SceneVisibilityManager.instance.Show(group, true);
        else
            SceneVisibilityManager.instance.Hide(group, true);
        SceneView.RepaintAll();
    }

    private static void Build()
    {
        Directory.CreateDirectory(AssetRoot + "/Materials");
        Directory.CreateDirectory(AssetRoot + "/Meshes");
        Directory.CreateDirectory(ReportRoot);
        AssetDatabase.Refresh();
        Meshes.Clear();

        floor = GrayMaterial("Gray_Floors", 0.57f);
        wall = GrayMaterial("Gray_Walls", 0.51f);
        stairs = GrayMaterial("Gray_Stairs", 0.61f);
        column = GrayMaterial("Gray_Columns", 0.55f);
        ceiling = GrayMaterial("Gray_Ceilings", 0.60f);

        // A fresh scene is created only when explicitly invoking this builder.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var root = new GameObject("MicroLair_Graybox").transform;
        var lower = Group(root, "01_LowerFloor");
        var decks = Group(root, "02_RaisedPlatforms");
        var steps = Group(root, "03_Stairs");
        var structure = Group(root, "04_PillarsAndBeams");
        var roofs = Group(root, "05_Ceilings");
        var walls = Group(root, "06_OuterWalls");
        var reserved = Group(root, "07_ReservedSpaces_NoGeometry");
        var provisional = Group(root, "08_ProvisionalRearConnection");
        var preview = Group(root, "09_InspectionCameraAndLights");

        Box(lower, "LowerFloor_18x16m", new Vector3(0, -0.125f, 0), new Vector3(18, 0.25f, 16), floor);
        Platform(decks, "Hub_RoundedFront_8x8m", HubOutline(), floor);
        Platform(decks, "LivingBay_RoundedInnerCorner", BayOutline(true), floor);
        Platform(decks, "ControlBay_RoundedInnerCorner", BayOutline(false), floor);
        foreach (int side in new[] { -1, 1 })
        {
            string label = side < 0 ? "Left" : "Right";
            Box(decks, label + "_UpperAisle_2p4m", new Vector3(side * 5.2f, DeckHeight / 2, 1),
                new Vector3(2.4f, DeckHeight, 8), floor, wall);
            var flight = Group(steps, label + "_Flight_6Risers");
            for (int i = 1; i <= 6; i++)
            {
                float height = i * 0.17f;
                Box(flight, "Step_" + i.ToString("00"),
                    new Vector3(side * 5.2f, height / 2, -4.8f + (i - 0.5f) * 0.3f),
                    new Vector3(2.4f, height, 0.3f), stairs);
            }
        }

        // Explicitly labeled as hypothetical; this is not evidence of the original set's back layout.
        Box(provisional, "RearDeck_Connection_Assumed", new Vector3(0, DeckHeight / 2, 6.25f),
            new Vector3(17.4f, DeckHeight, 2.5f), floor, wall);

        BuildStructure(structure);

        // Outer shell. Front access is a blockout convenience, not a recovered production doorway.
        var front = Group(walls, "FrontWall_EntryOpening_Assumed");
        Box(front, "Front_Left", new Vector3(-5.1f, 2.25f, -7.85f), new Vector3(7.2f, 4.5f, 0.3f), wall);
        Box(front, "Front_Right", new Vector3(5.1f, 2.25f, -7.85f), new Vector3(7.2f, 4.5f, 0.3f), wall);
        Box(front, "Entry_Lintel", new Vector3(0, 3.65f, -7.85f), new Vector3(3, 1.7f, 0.3f), wall);
        Box(walls, "WestWall", new Vector3(-8.85f, 2.25f, -0.15f), new Vector3(0.3f, 4.5f, 15.4f), wall);
        Box(walls, "EastWall", new Vector3(8.85f, 2.25f, -0.15f), new Vector3(0.3f, 4.5f, 15.4f), wall);
        var back = Group(walls, "RearWall_GlazedDoorGap");
        Box(back, "Rear_Main", new Vector3(-2.35f, 2.25f, 7.65f), new Vector3(12.7f, 4.5f, 0.3f), wall);
        Box(back, "Rear_RightReturn", new Vector3(7.55f, 2.25f, 7.65f), new Vector3(2.3f, 4.5f, 0.3f), wall);
        Box(back, "Door_Lintel", new Vector3(5.2f, 4.11f, 7.65f), new Vector3(2.4f, 0.78f, 0.3f), wall);
        Box(back, "Door_BaseBelowDeck", new Vector3(5.2f, DeckHeight / 2, 7.65f), new Vector3(2.4f, DeckHeight, 0.3f), wall);

        // Simple ceiling slabs and two large recesses only. No grill, panes or drop-ceiling lattice.
        Box(roofs, "Soffit_Front", new Vector3(0, 4.65f, -4.5f), new Vector3(18, 0.3f, 7), ceiling);
        Box(roofs, "Soffit_Rear", new Vector3(0, 4.65f, 5.5f), new Vector3(18, 0.3f, 5), ceiling);
        Box(roofs, "Soffit_Left", new Vector3(-6, 4.65f, 1), new Vector3(6, 0.3f, 4), ceiling);
        Box(roofs, "Soffit_Right", new Vector3(6, 4.65f, 1), new Vector3(6, 0.3f, 4), ceiling);
        Box(roofs, "Well_CentralDivider", new Vector3(0, 4.95f, 1), new Vector3(0.6f, 0.9f, 4), ceiling);
        foreach (int side in new[] { -1, 1 })
        {
            var well = Group(roofs, (side < 0 ? "Left" : "Right") + "_SimpleCeilingRecess");
            Box(well, "PlainCap_NoGlass", new Vector3(side * 1.65f, 5.55f, 1), new Vector3(2.7f, 0.3f, 4), ceiling);
            Box(well, "OuterReveal", new Vector3(side * 3.1f, 5.1f, 1), new Vector3(0.2f, 0.6f, 4), ceiling);
            Box(well, "FrontReveal", new Vector3(side * 1.65f, 5.1f, -1.1f), new Vector3(2.7f, 0.6f, 0.2f), ceiling);
            Box(well, "RearReveal", new Vector3(side * 1.65f, 5.1f, 3.1f), new Vector3(2.7f, 0.6f, 0.2f), ceiling);
        }

        Marker(reserved, "Hub_Equipment_Area", new Vector3(0, DeckHeight, 1));
        Marker(reserved, "Living_Equipment_Area", new Vector3(7.5f, DeckHeight, 1));
        Marker(reserved, "Control_Equipment_Area", new Vector3(-7.5f, DeckHeight, 1));
        Marker(reserved, "Right_HubEntry_Open", new Vector3(4, DeckHeight, -0.6f));
        Marker(reserved, "Left_HubEntry_Open", new Vector3(-4, DeckHeight, -0.6f));
        Marker(reserved, "Rear_DoorOpening_2p4x2p7m", new Vector3(5.2f, DeckHeight, 7.5f));
        Marker(reserved, "Front_DoorOpening_3x2p8m_Assumed", new Vector3(0, 0, -7.7f));

        var cameraObject = new GameObject("Main Camera");
        cameraObject.transform.SetParent(preview, false);
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        camera.transform.position = new Vector3(5.0f, 1.65f, -7.15f);
        camera.transform.LookAt(new Vector3(2.8f, 2.0f, 1.5f));
        camera.fieldOfView = 65;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 100;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.16f, 0.16f, 0.16f);
        camera.allowHDR = false;
        RenderSettings.skybox = null;
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.65f, 0.65f, 0.65f);
        RenderSettings.ambientIntensity = 1;
        RenderSettings.reflectionIntensity = 0;
        var sun = new GameObject("Neutral_Inspection_Key");
        sun.transform.SetParent(preview, false);
        sun.transform.rotation = Quaternion.Euler(55, -35, 0);
        var key = sun.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = Color.white;
        key.intensity = 1;
        key.shadows = LightShadows.Soft;
        RenderSettings.sun = key;
        foreach (var position in new[] { new Vector3(0, 3.8f, -5.5f), new Vector3(0, 4.0f, 1),
                     new Vector3(-5.2f, 3.6f, 2), new Vector3(5.2f, 3.6f, 2), new Vector3(0, 3.8f, 6) })
        {
            var lamp = new GameObject("Neutral_Inspection_Fill");
            lamp.transform.SetParent(preview, false);
            lamp.transform.position = position;
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Color.white;
            light.intensity = 2;
            light.range = 11;
            light.shadows = LightShadows.None;
        }
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new IOException("Unity failed to save " + ScenePath);
    }

    private static Transform Group(Transform parent, string name)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        return obj.transform;
    }

    private static void BuildStructure(Transform structure)
    {
        // Columns end at beam undersides. Beam spans meet at edges, avoiding coplanar overlaps.
        foreach (int side in new[] { -1, 1 })
        {
            string label = side < 0 ? "Left" : "Right";
            Pillar(structure, label + "_FrontPillar", new Vector3(side * 7, 0, -4.2f), 3.95f);
            Pillar(structure, label + "_RearPillar_Assumed", new Vector3(side * 7, DeckHeight, 4.8f), 3.95f - DeckHeight);
            var spans = new[] { new Vector2(-7.7f, -4.425f), new Vector2(-3.975f, 4.575f), new Vector2(5.025f, 7.5f) };
            for (int i = 0; i < spans.Length; i++)
                Box(structure, label + "_LongitudinalBeam_" + (i + 1),
                    new Vector3(side * 7, 4.225f, (spans[i].x + spans[i].y) / 2),
                    new Vector3(0.45f, 0.55f, spans[i].y - spans[i].x), ceiling);
        }
        foreach (float z in new[] { -4.2f, 4.8f })
            Box(structure, z < 0 ? "Front_CrossBeam" : "Rear_CrossBeam", new Vector3(0, 4.225f, z),
                new Vector3(17.4f, 0.55f, 0.45f), ceiling);
    }

    private static void Marker(Transform parent, string name, Vector3 position)
    {
        var marker = Group(parent, name);
        marker.localPosition = position;
    }

    private static Material GrayMaterial(string name, float shade)
    {
        string path = AssetRoot + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
        material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", new Color(shade, shade, shade));
        material.SetFloat("_Smoothness", 0.12f);
        material.SetFloat("_Metallic", 0);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void Box(Transform parent, string name, Vector3 center, Vector3 size, Material top, Material sides = null)
    {
        string key = FormattableString.Invariant($"Box_{size.x:0.###}_{size.y:0.###}_{size.z:0.###}");
        if (!Meshes.TryGetValue(key, out var mesh))
        {
            mesh = Prism(new List<Vector2> { new Vector2(-size.x / 2, -size.z / 2),
                new Vector2(size.x / 2, -size.z / 2), new Vector2(size.x / 2, size.z / 2),
                new Vector2(-size.x / 2, size.z / 2) }, -size.y / 2, size.y / 2);
            mesh = SaveMesh(key, mesh);
        }
        var obj = MeshObject(parent, name, mesh, top, sides ?? top);
        obj.transform.localPosition = center;
        obj.AddComponent<BoxCollider>().size = size;
    }

    private static void Platform(Transform parent, string name, List<Vector2> outline, Material top)
    {
        var mesh = SaveMesh(name, Prism(outline, 0, DeckHeight));
        var obj = MeshObject(parent, name, mesh, top, wall);
        obj.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private static void Pillar(Transform parent, string name, Vector3 foot, float height)
    {
        var outline = new List<Vector2>();
        for (int i = 0; i < 32; i++)
        {
            float a = i * Mathf.PI * 2 / 32;
            outline.Add(new Vector2(Mathf.Cos(a) * 0.4f, Mathf.Sin(a) * 0.4f));
        }
        string key = FormattableString.Invariant($"Pillar_D0p8_H{height:0.###}");
        if (!Meshes.TryGetValue(key, out var mesh)) mesh = SaveMesh(key, Prism(outline, 0, height, true));
        var obj = MeshObject(parent, name, mesh, column, column);
        obj.transform.localPosition = foot;
        obj.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private static List<Vector2> HubOutline()
    {
        var points = new List<Vector2>();
        AddArc(points, new Vector2(-2.4f, -1.4f), 1.6f, 180, 270);
        AddArc(points, new Vector2(2.4f, -1.4f), 1.6f, 270, 360);
        points.Add(new Vector2(4, 5));
        points.Add(new Vector2(-4, 5));
        return points;
    }

    private static List<Vector2> BayOutline(bool right)
    {
        var points = new List<Vector2>();
        AddArc(points, new Vector2(7.2f, -2.2f), 0.8f, 180, 270);
        points.Add(new Vector2(8.7f, -3));
        points.Add(new Vector2(8.7f, 5));
        points.Add(new Vector2(6.4f, 5));
        if (!right)
        {
            for (int i = 0; i < points.Count; i++) points[i] = new Vector2(-points[i].x, points[i].y);
            points.Reverse();
        }
        return points;
    }

    private static void AddArc(List<Vector2> points, Vector2 center, float radius, float start, float end)
    {
        for (int i = 0; i <= 12; i++)
        {
            float a = Mathf.Lerp(start, end, i / 12f) * Mathf.Deg2Rad;
            points.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
        }
    }

    // Convex CCW footprints in XZ. Top, sides, underside are separate material slots.
    // UV0 uses one repeat per meter. UV1 is a separate non-overlapping lightmap unwrap.
    public static Mesh Prism(List<Vector2> points, float bottom, float top, bool smoothSides = false)
    {
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
        Vector2 center = Vector2.zero;
        foreach (var p in points) center += p;
        center /= points.Count;
        for (int face = 0; face < 2; face++)
        {
            float y = face == 0 ? top : bottom;
            int offset = vertices.Count;
            vertices.Add(new Vector3(center.x, y, center.y));
            uv.Add(center);
            foreach (var p in points) { vertices.Add(new Vector3(p.x, y, p.y)); uv.Add(p); }
            var indices = triangles[face == 0 ? 0 : 2];
            for (int i = 0; i < points.Count; i++)
            {
                int a = offset + 1 + i, b = offset + 1 + (i + 1) % points.Count;
                indices.Add(offset); indices.Add(face == 0 ? b : a); indices.Add(face == 0 ? a : b);
            }
        }
        int sideStart = vertices.Count;
        float distance = 0;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i], b = points[(i + 1) % points.Count];
            int offset = vertices.Count;
            float length = Vector2.Distance(a, b);
            vertices.AddRange(new[] { new Vector3(a.x, bottom, a.y), new Vector3(a.x, top, a.y),
                new Vector3(b.x, top, b.y), new Vector3(b.x, bottom, b.y) });
            uv.AddRange(new[] { new Vector2(distance, 0), new Vector2(distance, top - bottom),
                new Vector2(distance + length, top - bottom), new Vector2(distance + length, 0) });
            triangles[1].AddRange(new[] { offset, offset + 1, offset + 2, offset, offset + 2, offset + 3 });
            distance += length;
        }
        var mesh = new Mesh { subMeshCount = 3 };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        for (int i = 0; i < 3; i++) mesh.SetTriangles(triangles[i], i);
        mesh.RecalculateNormals();
        if (smoothSides)
        {
            var normals = mesh.normals;
            for (int i = sideStart; i < vertices.Count; i++)
                normals[i] = new Vector3(vertices[i].x - center.x, 0, vertices[i].z - center.y).normalized;
            mesh.normals = normals;
        }
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        Unwrapping.GenerateSecondaryUVSet(mesh);
        return mesh;
    }

    private static Mesh SaveMesh(string name, Mesh mesh)
    {
        mesh.name = name;
        string path = AssetRoot + "/Meshes/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) AssetDatabase.CreateAsset(mesh, path);
        else { Object.DestroyImmediate(mesh); mesh = existing; }
        Meshes[name] = mesh;
        return mesh;
    }

    private static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material top, Material side)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = new[] { top, side, side };
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return obj;
    }

    private static GameObject FindRoot()
    {
        var root = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "MicroLair_Graybox");
        if (root == null) throw new InvalidOperationException("Open the MicroLair_Graybox scene first.");
        return root;
    }

    [MenuItem("Tools/Micro Lair/Validate Saved Graybox")]
    public static void ValidateSavedScene()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Open the graybox scene before validating.");
        var root = FindRoot();
        if (root.transform.Find(MicroLairRevisionD.MarkerName)) { MicroLairRevisionD.Validate(); return; }
        if (root.transform.Find(MicroLairRevisionC.MarkerName)) { MicroLairRevisionC.Validate(); return; }
        if (root.transform.Find(MicroLairRevisionB.MarkerName)) { MicroLairRevisionB.Validate(); return; }
        var filters = root.GetComponentsInChildren<MeshFilter>(true);
        var checks = new List<string>();
        foreach (var filter in filters)
        {
            var mesh = filter.sharedMesh;
            Require(mesh != null && AssetDatabase.Contains(mesh), "Persistent mesh: " + filter.name);
            Require(mesh.uv.Length == mesh.vertexCount && mesh.uv2.Length == mesh.vertexCount, "Both UV sets: " + filter.name);
            Require(mesh.normals.Length == mesh.vertexCount && mesh.tangents.Length == mesh.vertexCount, "Normals/tangents: " + filter.name);
            Require(filter.GetComponent<Collider>() != null, "Collider: " + filter.name);
            Require(filter.transform.lossyScale == Vector3.one, "Unscaled texture-ready mesh: " + filter.name);
            var mats = filter.GetComponent<MeshRenderer>().sharedMaterials;
            Require(mats.Length == mesh.subMeshCount && mats.All(m => m != null && m.shader != null), "Materials: " + filter.name);
            var vertices = mesh.vertices;
            var indices = mesh.GetTriangles(0);
            for (int i = 0; i < indices.Length; i += 3)
                Require(Vector3.Cross(vertices[indices[i + 1]] - vertices[indices[i]], vertices[indices[i + 2]] - vertices[indices[i]]).y > 0,
                    "Top triangles face up: " + filter.name);
        }
        checks.Add(filters.Length + " structural mesh objects: saved assets, UV0/UV1, normals/tangents, materials, colliders, unit transforms and upward top faces checked.");
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "No missing scripts: " + t.name);
        Physics.SyncTransforms();
        CheckFloor(new Vector3(0, 2, -6), 0);
        CheckFloor(new Vector3(0, 2, 1), DeckHeight);
        CheckFloor(new Vector3(7.5f, 2, 1), DeckHeight);
        CheckFloor(new Vector3(-7.5f, 2, 1), DeckHeight);
        CheckFloor(new Vector3(0, 2, 6), DeckHeight);
        foreach (float x in new[] { -5.2f, 5.2f })
        {
            for (int i = 1; i <= 6; i++) CheckFloor(new Vector3(x, 2, -4.8f + (i - 0.5f) * 0.3f), i * 0.17f);
            for (float z = -2.9f; z <= 7.4f; z += 0.5f)
            {
                CheckFloor(new Vector3(x, 2, z), DeckHeight);
                Require(CapsuleClear(new Vector3(x, 1.37f, z), new Vector3(x, 2.47f, z)), "Clear upper aisle: " + x + ", " + z);
            }
        }
        Require(!RayHitsOwnScene(new Vector3(5.2f, 2, 7), Vector3.forward, 1.2f).Any(), "Rear doorway stays open");
        Require(!RayHitsOwnScene(new Vector3(0, 1.4f, -7), Vector3.back, 1.2f).Any(), "Front doorway stays open");
        foreach (float x in new[] { -4.0f, 4.0f, -6.4f, 6.4f })
            Require(CapsuleClear(new Vector3(x, 1.37f, -0.6f), new Vector3(x, 2.47f, -0.6f)), "Open room access: " + x);
        checks.Add("Physics raycasts confirm the lower floor, hub, both bays, rear connection and all 12 stair tread heights.");
        checks.Add("Capsule clearance confirms both upper aisles and access to equipment/living/control zones; both door apertures are unobstructed.");
        checks.Add("No runtime generator, missing scripts, fine frames, railings, equipment or glass.");
        File.WriteAllText(ReportRoot + "/graybox-validation.txt", "PASS\n" + string.Join("\n", checks) + "\n");
        Debug.Log("MICRO_LAIR_VALIDATION_PASS\n" + string.Join("\n", checks));
    }

    private static void CheckFloor(Vector3 point, float expected)
    {
        var hits = RayHitsOwnScene(point, Vector3.down, 3).OrderBy(h => h.distance).ToArray();
        Require(hits.Length > 0, "Walkable surface at " + point);
        var hit = hits[0];
        Require(Mathf.Abs(hit.point.y - expected) < 0.005f, "Floor elevation at " + point + ": " + hit.point.y + " expected " + expected);
    }

    private static IEnumerable<RaycastHit> RayHitsOwnScene(Vector3 point, Vector3 direction, float distance)
    {
        var ownScene = SceneManager.GetActiveScene();
        return Physics.RaycastAll(point, direction, distance).Where(h => h.collider.gameObject.scene == ownScene);
    }

    private static bool CapsuleClear(Vector3 bottom, Vector3 top)
    {
        var ownScene = SceneManager.GetActiveScene();
        return !Physics.OverlapCapsule(bottom, top, 0.3f).Any(c => c.gameObject.scene == ownScene);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Graybox validation failed: " + message);
    }

    [MenuItem("Tools/Micro Lair/Capture Graybox Previews")]
    public static void CapturePreviews()
    {
        var root = FindRoot();
        if (root.transform.Find(MicroLairRevisionD.MarkerName)) { MicroLairRevisionD.CapturePreviews(); return; }
        if (root.transform.Find(MicroLairRevisionC.MarkerName)) { MicroLairRevisionC.CapturePreviews(); return; }
        var camera = root.GetComponentInChildren<Camera>();
        var revisionScale = root.transform.Find(MicroLairRevisionB.MarkerName);
        float s = revisionScale ? revisionScale.localScale.x : 1f;
        var position = camera.transform.position;
        var rotation = camera.transform.rotation;
        bool wasOrthographic = camera.orthographic;
        float size = camera.orthographicSize;
        var roofs = root.transform.Find("05_Ceilings").gameObject;
        var front = root.transform.Find("06_OuterWalls/FrontWall_EntryOpening_Assumed").gameObject;
        var east = root.transform.Find("06_OuterWalls/EastWall").gameObject;
        bool roofActive = roofs.activeSelf, frontActive = front.activeSelf, eastActive = east.activeSelf;
        ulong previousSceneMask = camera.overrideSceneCullingMask;
        try
        {
            // Isolate this scene without changing layers cached by Unity's GPU resident drawer.
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(root.scene);
            RenderPreview(camera, "graybox-interior.png");
            roofs.SetActive(false);
            front.SetActive(false);
            east.SetActive(false);
            camera.transform.position = new Vector3(14, 17, -21) * s;
            camera.transform.LookAt(new Vector3(0, 1, 0) * s);
            camera.orthographic = true;
            camera.orthographicSize = 11.5f * s;
            RenderPreview(camera, "graybox-cutaway.png");
            camera.transform.position = new Vector3(0, 24, 0) * s;
            camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            camera.orthographicSize = 10 * s;
            RenderPreview(camera, "graybox-top.png");
        }
        finally
        {
            camera.overrideSceneCullingMask = previousSceneMask;
            roofs.SetActive(roofActive);
            front.SetActive(frontActive);
            east.SetActive(eastActive);
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.orthographic = wasOrthographic;
            camera.orthographicSize = size;
        }
    }

    private static void RenderPreview(Camera camera, string filename)
    {
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var target = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
            texture.Apply();
            File.WriteAllBytes(ReportRoot + "/" + filename, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(texture);
        }
    }
}
