using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Editor authoring only: all resulting geometry is saved as ordinary mesh assets.
public static class MicroLairRevisionC
{
    public const string MarkerName = "RevisionC_TallerLongerShell";
    const string MeshFolder = "Assets/Environment/MicroLair/Meshes/RevisionC";
    const string Reports = "Documentation/MicroLair/";
    const float Raise = 2.3f;
    public static float MapZ(float z) => z < -4.8f ? -4.8f + (z + 4.8f) * 1.625f
        : z <= -1.2f ? z : -1.2f + (z + 1.2f) * 1.9f;

    [MenuItem("Tools/Micro Lair/Apply Taller Longer Revision C")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        var scene = SceneManager.GetSceneByPath(MicroLairGrayboxBuilder.ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(MicroLairGrayboxBuilder.ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var root = scene.GetRootGameObjects().First(g => g.name == "MicroLair_Graybox").transform;
        float s = root.Find(MicroLairRevisionB.MarkerName).localScale.x;
        if (root.Find(MarkerName)) { Validate(); CapturePreviews(); EditorSceneManager.SaveScene(scene); return; }
        var reference = root.Find("10_Optional_PunisherScaleReference");
        var floors = root.Find("01_LowerFloor");
        var stairs = root.Find("03_Stairs");
        var structure = root.Find("04_PillarsAndBeams");
        var ceilings = root.Find("05_Ceilings");
        var walls = root.Find("06_OuterWalls");
        var filters = root.GetComponentsInChildren<MeshFilter>(true).Where(f => !f.transform.IsChildOf(reference)).ToArray();
        foreach (var filter in filters)
            if (Quaternion.Angle(filter.transform.rotation, Quaternion.identity) > 0.001f || (filter.transform.lossyScale - Vector3.one).sqrMagnitude > 0.000001f)
                throw new InvalidOperationException("Expected unit, unrotated structural mesh: " + filter.name);
        Directory.CreateDirectory("Diagnostics/MicroLair");
        string backup = "Diagnostics/MicroLair/MicroLair_Graybox_before_revision_c.unity";
        if (!File.Exists(backup) && !EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Could not back up the current scene.");
        CaptureInterior(root, s, "revision-c-before.png");
        Directory.CreateDirectory(MeshFolder);
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Taller and longer Micro lair");
        var changes = new List<(MeshFilter filter, Mesh mesh, Vector3 position)>();
        int serial = 0;
        foreach (var filter in filters)
        {
            if (filter.transform.IsChildOf(stairs)) continue;
            var mesh = Object.Instantiate(filter.sharedMesh);
            var vertices = mesh.vertices;
            var oldPosition = filter.transform.position / s;
            bool pillar = filter.name.Contains("Pillar");
            bool beam = filter.name.Contains("Beam");
            bool crossBeam = filter.name.Contains("CrossBeam");
            bool longitudinal = filter.name.Contains("LongitudinalBeam");
            bool overhead = filter.transform.IsChildOf(ceilings) || beam;
            bool wall = filter.transform.IsChildOf(walls);
            var oldMin = oldPosition + mesh.bounds.min / s;
            var oldMax = oldPosition + mesh.bounds.max / s;
            float WallY(float y) => y > 3.94f ? y + Raise : y;
            var pivot = new Vector3(oldPosition.x, oldPosition.y, MapZ(oldPosition.z));
            if (overhead) pivot.y += Raise;
            if (wall) pivot.y = (WallY(oldMin.y) + WallY(oldMax.y)) * 0.5f;
            float BeamZ(float z)
            {
                float a, b;
                if (filter.name.EndsWith("_1")) { a = MapZ(-7.7f); b = -4.425f; }
                else if (filter.name.EndsWith("_2")) { a = -3.975f; b = MapZ(4.8f) - 0.225f; }
                else { a = MapZ(4.8f) + 0.225f; b = MapZ(7.5f); }
                return Mathf.Lerp(a, b, Mathf.InverseLerp(oldMin.z, oldMax.z, z));
            }
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = oldPosition + vertices[i] / s;
                float z = pillar || crossBeam ? MapZ(oldPosition.z) + p.z - oldPosition.z : longitudinal ? BeamZ(p.z) : MapZ(p.z);
                float y = overhead ? p.y + Raise : wall ? WallY(p.y) : p.y;
                if (pillar) y = p.y + Mathf.InverseLerp(oldMin.y, oldMax.y, p.y) * Raise;
                vertices[i] = (new Vector3(p.x, y, z) - pivot) * s;
            }
            mesh.vertices = vertices;
            // Axial stretches preserve these face/radial normals, including the smooth hub curve.
            RetilePrism(mesh, s);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            Unwrapping.GenerateSecondaryUVSet(mesh);
            mesh.name = filter.name + "_RevisionC";
            string path = MeshFolder + "/" + (++serial).ToString("00") + "_" + filter.name + ".asset";
            if (File.Exists(path)) throw new IOException("Revision C asset already exists without its scene marker: " + path);
            AssetDatabase.CreateAsset(mesh, path);
            changes.Add((filter, mesh, pivot * s));
        }
        foreach (var change in changes)
        {
            change.filter.transform.position = change.position;
            change.filter.sharedMesh = change.mesh;
            if (change.filter.TryGetComponent<MeshCollider>(out var mc)) { mc.sharedMesh = null; mc.sharedMesh = change.mesh; }
            if (change.filter.TryGetComponent<BoxCollider>(out var bc)) { bc.center = change.mesh.bounds.center; bc.size = change.mesh.bounds.size; }
        }
        floors.GetChild(0).name = "LowerFloor_18x26p28m_CharacterScale";
        // Repeat one overhead span to keep a structural rhythm along the longer room.
        var middle = Object.Instantiate(structure.Find("Front_CrossBeam").gameObject, structure);
        middle.name = "Middle_CrossBeam_ExtendedBay";
        var middlePosition = middle.transform.position;
        middlePosition.z = (-4.2f + MapZ(4.8f)) * 0.5f * s;
        middle.transform.position = middlePosition;
        foreach (Transform marker in root.Find("07_ReservedSpaces_NoGeometry"))
        { var p = marker.position; p.z = MapZ(p.z / s) * s; marker.position = p; }
        foreach (var light in root.GetComponentsInChildren<Light>(true))
        {
            if (light.type == LightType.Directional) continue;
            var p = light.transform.position;
            light.transform.position = new Vector3(p.x, p.y + Raise * s, MapZ(p.z / s) * s);
            light.range *= 1.4f;
        }
        new GameObject(MarkerName).transform.SetParent(root, false);
        AssetDatabase.SaveAssets();
        Validate();
        MicroLairGameplaySetup.Validate();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the expanded graybox.");
        CapturePreviews();
        EditorSceneManager.SaveScene(scene);
        // Only close clean previously loaded scenes; never discard unsaved work.
        var others = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(x => x != scene && x.isLoaded && !x.isDirty).ToArray();
        foreach (var other in others) EditorSceneManager.CloseScene(other, true);
        Selection.activeGameObject = root.gameObject;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(new Vector3(0, 2.2f, 2.5f) * s, Quaternion.Euler(20, -20, 0), 14f * s);
        Debug.Log("MICRO_LAIR_REVISION_C_COMPLETE: Higher ceiling, longer Z corridors, original player scale and stairs preserved.");
    }

    internal static void RetilePrism(Mesh mesh, float s)
    {
        if (mesh.subMeshCount != 3) return; // The curved fascia only translates vertically.
        var vertices = mesh.vertices;
        var uv = mesh.uv;
        foreach (int submesh in new[] { 0, 2 })
            foreach (int index in mesh.GetTriangles(submesh).Distinct()) uv[index] = new Vector2(vertices[index].x, vertices[index].z) / s;
        var triangles = mesh.GetTriangles(1);
        float u = 0;
        for (int i = 0; i < triangles.Length; i += 6)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2], d = triangles[i + 5];
            float length = Vector3.Distance(vertices[a], vertices[d]) / s;
            float height = Vector3.Distance(vertices[a], vertices[b]) / s;
            uv[a] = new Vector2(u, 0); uv[b] = new Vector2(u, height);
            uv[c] = new Vector2(u + length, height); uv[d] = new Vector2(u + length, 0);
            u += length;
        }
        mesh.uv = uv;
    }

    [MenuItem("Tools/Micro Lair/Validate Revision C")]
    public static void Validate()
    {
        var scene = SceneManager.GetSceneByPath(MicroLairGrayboxBuilder.ScenePath);
        var root = scene.GetRootGameObjects().First(g => g.name == "MicroLair_Graybox").transform;
        float s = root.Find(MicroLairRevisionB.MarkerName).localScale.x;
        var report = new StringBuilder();
        void Need(bool ok, string message) { if (!ok) throw new InvalidOperationException("Revision C: " + message); }
        var reference = root.Find("10_Optional_PunisherScaleReference");
        var filters = root.GetComponentsInChildren<MeshFilter>(true).Where(f => !f.transform.IsChildOf(reference)).ToArray();
        foreach (var filter in filters)
        {
            var mesh = filter.sharedMesh;
            Need(mesh && AssetDatabase.Contains(mesh), "saved mesh " + filter.name);
            Need(mesh.uv.Length == mesh.vertexCount && mesh.uv2.Length == mesh.vertexCount, "UV sets " + filter.name);
            Need((filter.transform.lossyScale - Vector3.one).sqrMagnitude < 0.000001f, "unit scale " + filter.name);
            Need(filter.GetComponent<MeshRenderer>().sharedMaterials.Length == mesh.subMeshCount, "material slots " + filter.name);
            var col = filter.GetComponent<Collider>();
            Need(col && col.enabled, "collider " + filter.name);
            if (col is MeshCollider mc) Need(mc.sharedMesh == mesh, "mesh collider match " + filter.name);
            if (col is BoxCollider bc) Need((bc.center - mesh.bounds.center).sqrMagnitude < 0.000001f && (bc.size - mesh.bounds.size).sqrMagnitude < 0.000001f, "box collider bounds " + filter.name);
        }
        Physics.SyncTransforms();
        bool Own(Collider c) => c.transform.IsChildOf(root);
        RaycastHit[] Hits(Vector3 p, Vector3 direction, float length) => Physics.RaycastAll(p, direction, length).Where(h => Own(h.collider)).OrderBy(h => h.distance).ToArray();
        void Floor(float x, float z, float y)
        {
            var hits = Hits(new Vector3(x, 2.4f, z) * s, Vector3.down, 3 * s);
            Need(hits.Length > 0 && Mathf.Abs(hits[0].point.y - y * s) < 0.002f, "floor at " + x + "," + z);
        }
        var controller = scene.GetRootGameObjects().First(g => g.name == MicroLairGameplaySetup.RigName).GetComponentInChildren<CharacterController>();
        int samples = 0;
        foreach (float x in new[] { -4.4f, 4.4f })
        {
            for (int i = 1; i <= 6; i++) Floor(x, -4.8f + (i - .5f) * .3f, i * .17f);
            for (float z = -2.9f; z < MapZ(7.5f) - 0.5f; z += 0.35f)
            {
                Floor(x, z, 1.02f);
                float r = controller.radius, h = controller.height, deck = 1.02f * s;
                var low = new Vector3(x * s, deck + r + 0.015f, z * s);
                var high = low + Vector3.up * (h - 2 * r);
                Need(!Physics.OverlapCapsule(low, high, r).Any(Own), "character clearance in extended aisle " + x + "," + z);
                var roof = Hits(new Vector3(x * s, deck + h, z * s), Vector3.up, 8 * s);
                Need(roof.Length > 0 && roof[0].point.y >= 6.24f * s, "beam/ceiling clearance " + x + "," + z);
                samples++;
            }
            var line = new Vector3(x * s, 1.02f * s + 0.55f, -2.8f * s);
            Need(Hits(line, Vector3.forward, (MapZ(7.5f) - 0.7f + 2.8f) * s).Length == 0, "clear long shooting lane " + x);
        }
        Floor(0, -9.1f, 0); Floor(0, -2.7f, 1.02f); Floor(0, 10, 1.02f); Floor(7.9f, 9, 1.02f); Floor(-7.9f, 9, 1.02f);
        Need(Hits(new Vector3(4.4f, 2.4f, MapZ(7.5f) - .5f) * s, Vector3.forward, 1.5f * s).Length == 0, "rear doorway");
        Need(Hits(new Vector3(0, 1.4f, MapZ(-7.7f) + .5f) * s, Vector3.back, 1.5f * s).Length == 0, "front doorway");
        Need(Hits(new Vector3(7.9f, 2.4f, 5) * s, Vector3.right, 2 * s).Length > 0, "side wall stops projectile ray");
        var floorBounds = root.Find("01_LowerFloor").GetComponentInChildren<MeshRenderer>().bounds;
        Need(Mathf.Abs(floorBounds.size.z / s - 26.28f) < .02f, "room Z length");
        foreach (var pillar in filters.Where(f => f.name.Contains("Pillar")))
        { var b = pillar.GetComponent<MeshRenderer>().bounds; Need(Mathf.Abs(b.size.x - b.size.z) < .001f, "round pillar " + pillar.name); Need(Mathf.Abs(b.max.y / s - 6.25f) < .01f, "pillar reaches beam " + pillar.name); }
        Need(Mathf.Abs(controller.height - .77f) < .0001f && controller.transform.localScale == Vector3.one, "original player scale");
        report.AppendLine("PASS Saved structural mesh assets, UV0/UV1, material slots, colliders and unit scales.");
        report.AppendLine("PASS All 12 unchanged stair treads meet the upper deck.");
        report.AppendLine("PASS " + samples + " floor, actual-player capsule and overhead-clearance samples along both extended aisles.");
        report.AppendLine("PASS Unobstructed long shooting lanes, open front/rear doorways and side-wall projectile collision.");
        report.AppendLine("PASS Pillars remain round and reach the raised beams; player scale remains unchanged.");
        report.AppendLine($"World units per nominal meter: {s:F6}. Floor: {floorBounds.size.x:F3} x {floorBounds.size.z:F3} units (18 x 26.28 nominal m).");
        report.AppendLine($"Ceiling underside: {6.8f*s:F3}; beam underside: {6.25f*s:F3}; raised floor: {1.02f*s:F3} units.");
        report.AppendLine($"Upper-floor ceiling clearance: {5.78f*s:F3}; under-beam clearance: {5.23f*s:F3} units; character height: {controller.height:F3}.");
        report.AppendLine("Validation uses edit-time physics probes; existing gameplay input/shooting scripts were not changed.");
        File.WriteAllText(Reports + "revision-c-validation.txt", report.ToString());
    }

    static void CaptureInterior(Transform root, float s, string file)
    {
        var camera = NewCamera(root);
        try
        {
            camera.transform.position = new Vector3(4.4f, 1.85f, -6.8f) * s;
            camera.transform.LookAt(new Vector3(1.1f, 3.05f, 3f) * s);
            camera.fieldOfView = 70;
            Render(camera, file);
        }
        finally { Object.DestroyImmediate(camera.gameObject); }
    }
    internal static Camera NewCamera(Transform root)
    {
        var go = new GameObject("Temporary_RevisionC_Preview");
        SceneManager.MoveGameObjectToScene(go, root.gameObject.scene);
        var camera = go.AddComponent<Camera>();
        camera.enabled = false; camera.nearClipPlane = 0.03f; camera.farClipPlane = 100;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f, .17f, .20f);
        camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(root.gameObject.scene);
        return camera;
    }
    [MenuItem("Tools/Micro Lair/Capture Revision C Previews")]
    public static void CapturePreviews()
    {
        var scene = SceneManager.GetSceneByPath(MicroLairGrayboxBuilder.ScenePath);
        var root = scene.GetRootGameObjects().First(g => g.name == "MicroLair_Graybox").transform;
        float s = root.Find(MicroLairRevisionB.MarkerName).localScale.x;
        CaptureInterior(root, s, "revision-c-interior.png");
        var hidden = new[] { root.Find("05_Ceilings").gameObject, root.Find("06_OuterWalls/FrontWall_EntryOpening_Assumed").gameObject, root.Find("06_OuterWalls/EastWall").gameObject };
        var states = hidden.Select(g => g.activeSelf).ToArray();
        var camera = NewCamera(root);
        try
        {
            foreach (var item in hidden) item.SetActive(false);
            camera.orthographic = true; camera.orthographicSize = 14.5f * s;
            camera.transform.position = new Vector3(18, 24, -26) * s;
            camera.transform.LookAt(new Vector3(0, 2.2f, 3) * s);
            Render(camera, "revision-c-cutaway.png");
            camera.transform.position = new Vector3(0, 30, 3.14f) * s;
            camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            Render(camera, "revision-c-plan.png");
        }
        finally
        {
            for (int i = 0; i < hidden.Length; i++) hidden[i].SetActive(states[i]);
            Object.DestroyImmediate(camera.gameObject);
        }
    }
    internal static void Render(Camera camera, string file)
    {
        var previous = RenderTexture.active;
        var target = new RenderTexture(1600, 1000, 24);
        var texture = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); texture.Apply();
            File.WriteAllBytes(Reports + file, texture.EncodeToPNG());
        }
        finally { camera.targetTexture = null; RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(texture); }
    }
}
