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

public static class MicroLairSteelWindows
{
    const string GroupName = "11_SteelMullionWindows";
    const string Reports = "Documentation/MicroLair/";

    [MenuItem("Tools/Micro Lair/Build Rusty Steel Window Frames")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        Directory.CreateDirectory(Reports);
        Progress("Using the rusty metal material for the larger window panes.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MicroLairRustyMetal.MaterialPath);
        if (!material) material = MicroLairRustyMetal.Build();
        var scene = SceneManager.GetSceneByPath(MicroLairGrayboxBuilder.ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(MicroLairGrayboxBuilder.ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var root = scene.GetRootGameObjects().First(g => g.name == "MicroLair_Graybox").transform;
        Need(root.lossyScale == Vector3.one, "Graybox root retains unit scale.");
        float scale = root.Find(MicroLairRevisionB.MarkerName).localScale.x;
        const string backup = "Diagnostics/MicroLair/MicroLair_Graybox_before_six_window_rows.unity";
        Directory.CreateDirectory(Path.GetDirectoryName(backup));
        if (!File.Exists(backup)) Need(EditorSceneManager.SaveScene(scene, backup, true), "Scene backup saved.");
        var old = root.Find(GroupName);
        var previousMeshes = root.GetComponentsInChildren<MeshFilter>(true).Where(f => !old || !f.transform.IsChildOf(old)).ToDictionary(f => f, f => f.sharedMesh);
        var previousMaterials = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true))
            .Where(r => !old || !r.transform.IsChildOf(old)).ToDictionary(r => r, r => r.sharedMaterials);
        var previousColliders = root.GetComponentsInChildren<Collider>(true).Where(c => !old || !c.transform.IsChildOf(old)).ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
        Progress("Tracing platform contours and placing four clear room entrances.");
        MeshFilter Floor(string part) => root.Find("02_RaisedPlatforms/" + part).GetComponent<MeshFilter>();
        var hub = MicroLairWindowGeometry.Outline(Floor("Hub_ContinuousCurve_6p4m_CharacterScale"), "CentreHub", false, scale);
        var left = MicroLairWindowGeometry.Outline(Floor("ControlBay_RoundedInnerCorner"), "LeftControlBay", true, scale);
        var right = MicroLairWindowGeometry.Outline(Floor("LivingBay_RoundedInnerCorner"), "RightLivingBay", true, scale);
        // Determine physical left/right from the current meshes, not the original descriptive names.
        var sides = new[] { left, right }.OrderBy(p => p.Points.Average(v => v.x)).ToArray();
        left = sides[0]; right = sides[1]; left.Name = "LeftBay"; right.Name = "RightBay";
        float doorZ = root.Find("07_ReservedSpaces_NoGeometry/Right_HubEntry_Open").position.z / scale;
        MicroLairWindowGeometry.AddDoor(hub, "Hub_LeftEntrance", new Vector2(hub.Points.Min(p => p.x), doorZ));
        MicroLairWindowGeometry.AddDoor(hub, "Hub_RightEntrance", new Vector2(hub.Points.Max(p => p.x), doorZ));
        MicroLairWindowGeometry.AddDoor(left, "LeftBay_Entrance", new Vector2(left.Point(left.Length).x, doorZ));
        MicroLairWindowGeometry.AddDoor(right, "RightBay_Entrance", new Vector2(right.Point(right.Length).x, doorZ));
        var paths = new[] { hub, left, right };
        float top = root.Find("05_Ceilings/Hub_MatchingCurvedUpperFascia").GetComponent<MeshRenderer>().bounds.min.y / scale;
        Need(top - hub.Deck > 3, "Window top follows the existing raised upper fascia.");
        bool wasActive = old && old.gameObject.activeSelf;
        var staging = new GameObject(GroupName + "_Building").transform; staging.SetParent(root, false);
        if (old) old.gameObject.SetActive(false);
        try
        {
            MicroLairWindowGeometry.MemberCount = 0;
            foreach (var path in paths)
            {
                Progress("Building " + path.Name + " mullions, curved rails and open doorway bands.");
                MicroLairWindowGeometry.Build(path, staging, material, top, scale);
            }
            AssetDatabase.SaveAssets(); Physics.SyncTransforms();
            Progress("Checking player clearance, doorway dimensions, pane openings and bar collisions.");
            Validate(root, staging, paths, top, scale, material);
            Need(previousMeshes.All(p => p.Key.sharedMesh == p.Value), "Existing structural meshes unchanged.");
            Need(previousMaterials.All(p => p.Key.sharedMaterials.SequenceEqual(p.Value)), "Existing concrete and gameplay materials unchanged.");
            Need(previousColliders.All(p => EditorJsonUtility.ToJson(p.Key) == p.Value), "Existing colliders unchanged.");
            if (old) Undo.DestroyObjectImmediate(old.gameObject);
            staging.name = GroupName; Undo.RegisterCreatedObjectUndo(staging.gameObject, "Create rusty steel window frames");
            EditorSceneManager.MarkSceneDirty(scene);
            Need(EditorSceneManager.SaveScene(scene), "Window scene saved.");
            Progress("Rendering front, aisle and layout previews.");
            Capture(root, scale);
            Need(EditorSceneManager.SaveScene(scene), "Scene saved after preview cleanup.");
            Selection.activeGameObject = staging.gameObject; SceneView.RepaintAll();
            Progress("COMPLETE");
            Debug.Log("MICRO_LAIR_STEEL_WINDOWS_COMPLETE: Three curved window runs, four clear doors, rusty metal material and open-pane collision saved.");
        }
        catch
        {
            if (staging && staging.name.EndsWith("_Building")) Object.DestroyImmediate(staging.gameObject);
            if (old) old.gameObject.SetActive(wasActive);
            throw;
        }
    }

    static void Validate(Transform root, Transform frames, MicroWindowPath[] paths, float top, float scale, Material material)
    {
        var report = new StringBuilder();
        var filters = frames.GetComponentsInChildren<MeshFilter>();
        int triangles = 0;
        foreach (var filter in filters)
        {
            var mesh = filter.sharedMesh;
            Need(mesh && AssetDatabase.Contains(mesh), "Persistent bar mesh: " + filter.name);
            Need(mesh.uv.Length == mesh.vertexCount && mesh.uv2.Length == mesh.vertexCount && mesh.tangents.Length == mesh.vertexCount, "UVs and tangent frames: " + filter.name);
            Need(filter.transform.lossyScale == Vector3.one, "Unit bar scale.");
            Need(filter.GetComponent<MeshRenderer>().sharedMaterial == material, "Rust material assigned.");
            var collider = filter.GetComponent<MeshCollider>();
            Need(collider && collider.sharedMesh == mesh && !collider.convex && !collider.isTrigger, "Exact open-bar mesh collision.");
            triangles += mesh.triangles.Length / 3;
        }
        var controller = root.gameObject.scene.GetRootGameObjects().First(g => g.name == MicroLairGameplaySetup.RigName).GetComponentInChildren<CharacterController>();
        float radius = controller.radius * controller.transform.lossyScale.x;
        float height = controller.height * controller.transform.lossyScale.y;
        bool New(Collider c) => c.transform.IsChildOf(frames);
        bool Env(Collider c) => c.transform.IsChildOf(root) && c.enabled && c.gameObject.activeInHierarchy;
        Collider[] Capsule(Vector3 feet) => Physics.OverlapCapsule(feet + Vector3.up * (radius + .02f), feet + Vector3.up * (height - radius + .02f), radius, ~0, QueryTriggerInteraction.Ignore);
        int doorwaySamples = 0, laneSamples = 0;
        foreach (var path in paths)
        {
            var levels = MicroLairWindowGeometry.RowLevels(top - path.Deck);
            report.AppendLine($"{path.Name}: path {path.Length:F2} design m; {path.Posts.Count} vertical members; {levels.Count - 1} pane rows; raised floor {path.Deck:F2}, header top {top:F2} design m.");
            for (int i = 1; i < path.Posts.Count; i++) Need(path.Posts[i] - path.Posts[i - 1] <= MicroLairWindowGeometry.BayWidth + .002f, "Maximum pane width.");
            foreach (var door in path.Doors)
            {
                float clear = Vector2.Distance(path.Point(door.Start), path.Point(door.End)) - MicroLairWindowGeometry.JambWidth;
                Need(Mathf.Abs(clear - MicroLairWindowGeometry.DoorClearWidth) < .002f, "Door clear width: " + door.Name);
                Need(clear * scale > radius * 2 + .08f, "Character width clearance.");
                Need(MicroLairWindowGeometry.DoorClearHeight * scale > height + .08f, "Character head clearance.");
                var p = path.Point(door.Center); var t = path.Tangent(door.Center); var n = new Vector2(-t.y, t.x);
                float lateral = Mathf.Max(0, clear * scale * .5f - radius - .035f);
                foreach (float side in new[] { -lateral, 0, lateral })
                for (int step = -4; step <= 4; step++)
                {
                    var feet = new Vector3(p.x, path.Deck, p.y) * scale + new Vector3(t.x, 0, t.y) * side + new Vector3(n.x, 0, n.y) * (step * .16f * scale);
                    Need(!Capsule(feet).Any(Env), "Walkable doorway: " + door.Name + " sample " + step); doorwaySamples++;
                }
                report.AppendLine($"  {door.Name}: clear {clear:F2} x {MicroLairWindowGeometry.DoorClearHeight:F2} design m ({clear * scale:F3} x {MicroLairWindowGeometry.DoorClearHeight * scale:F3} Unity units), centre X/Z {p.x:F3}/{p.y:F3}; no sill in opening.");
            }
            float front = path.Closest(new Vector2(path.Points.Average(p => p.x), path.Points.Min(p => p.y)));
            var intervals = Enumerable.Range(1, path.Posts.Count - 1)
                .Select(i => (a: path.Posts[i - 1], b: path.Posts[i]))
                .Where(p => !path.Doors.Any(d => d.Inside((p.a + p.b) * .5f)))
                .OrderBy(p => Mathf.Abs((p.a + p.b) * .5f - front)).ToArray();
            var pane = intervals.First();
            float shotY = path.Deck + (levels[2] + levels[3]) * .5f;
            RaycastHit[] Shoot(float station)
            {
                var p = path.Point(station); var t = path.Tangent(station); var normal = new Vector3(-t.y, 0, t.x);
                var center = new Vector3(p.x, shotY, p.y) * scale;
                return Physics.RaycastAll(center + normal * (.2f * scale), -normal, .4f * scale, ~0, QueryTriggerInteraction.Ignore).Where(h => New(h.collider)).ToArray();
            }
            Need(Shoot((pane.a + pane.b) * .5f).Length == 0, "Bullets pass through empty pane: " + path.Name);
            Need(Shoot(pane.a).Length > 0, "Bullets hit actual steel post: " + path.Name);
            var paneCenter = path.Point((pane.a + pane.b) * .5f);
            Need(Capsule(new Vector3(paneCenter.x, path.Deck, paneCenter.y) * scale).Any(New), "Crossbars block standing passage through regular panes: " + path.Name);
        }
        foreach (float x in new[] { -4.4f, 4.4f })
        {
            for (int step = 1; step <= 6; step++)
            {
                var feet = new Vector3(x, step * .17f, -4.8f + (step - .5f) * .3f) * scale;
                Need(!Capsule(feet).Any(New), "Clear existing staircase."); laneSamples++;
            }
            float end = paths.Max(p => p.Points.Max(v => v.y)) + 1;
            for (float z = -2.9f; z < end; z += .3f)
            {
                Need(!Capsule(new Vector3(x, paths[0].Deck, z) * scale).Any(New), "Clear existing long aisle."); laneSamples++;
            }
        }
        report.AppendLine($"PASS {filters.Length} editable bar/rail mesh objects; {MicroLairWindowGeometry.MemberCount} rectangular steel members; {triangles} triangles. All have saved UV0/UV1, tangents, rusty metal and exact bar-only MeshColliders.");
        report.AppendLine($"PASS {doorwaySamples} actual-player capsule samples through four doors against the full environment, and {laneSamples} stair/aisle samples against the new frames.");
        report.AppendLine("PASS Ray tests through empty panes and into actual posts on all three enclosures. Rear ends remain open to the shared rear deck; existing building doorways are untouched.");
        report.AppendLine($"Player capsule {height:F3} high, {radius * 2:F3} wide in Unity units; design-to-world scale {scale:F8}.");
        report.AppendLine($"Larger-pane revision: 40mm mullions, 55mm main posts/jambs, 28mm intermediate crossbars; vertical spacing <={MicroLairWindowGeometry.BayWidth:F2} design m and {MicroLairWindowGeometry.PaneRows} pane rows in the current shell. Doors {MicroLairWindowGeometry.DoorClearWidth:F2} x {MicroLairWindowGeometry.DoorClearHeight:F2} design m. Standing-player probes hit real crossbars at regular panes.");
        File.WriteAllText(Reports + "steel-windows-validation.txt", report.ToString());
    }

    static void Capture(Transform root, float s)
    {
        var camera = MicroLairRevisionC.NewCamera(root);
        try
        {
            camera.transform.position = new Vector3(0, 2.9f, -9.5f) * s;
            camera.transform.LookAt(new Vector3(0, 3, 1.5f) * s); camera.fieldOfView = 60;
            MicroLairRevisionC.Render(camera, "steel-windows-front.png");
            camera.transform.position = new Vector3(4.4f, 2.75f, -6.6f) * s;
            camera.transform.LookAt(new Vector3(4.4f, 2.9f, 5) * s); camera.fieldOfView = 65;
            MicroLairRevisionC.Render(camera, "steel-windows-right-aisle.png");
            camera.transform.position = new Vector3(-4.4f, 2.75f, -6.6f) * s;
            camera.transform.LookAt(new Vector3(-4.4f, 2.9f, 5) * s);
            MicroLairRevisionC.Render(camera, "steel-windows-left-aisle.png");
        }
        finally { Object.DestroyImmediate(camera.gameObject); }
        var hidden = new[] { root.Find("05_Ceilings").gameObject, root.Find("06_OuterWalls/FrontWall_EntryOpening_Assumed").gameObject, root.Find("06_OuterWalls/EastWall").gameObject };
        var states = hidden.Select(g => g.activeSelf).ToArray(); camera = MicroLairRevisionC.NewCamera(root);
        try
        {
            foreach (var item in hidden) item.SetActive(false);
            camera.orthographic = true; camera.orthographicSize = 17.5f * s;
            camera.transform.position = new Vector3(22, 26, -30) * s;
            camera.transform.LookAt(new Vector3(0, 2.2f, 3) * s);
            MicroLairRevisionC.Render(camera, "steel-windows-cutaway.png");
        }
        finally { for (int i = 0; i < hidden.Length; i++) hidden[i].SetActive(states[i]); Object.DestroyImmediate(camera.gameObject); }
    }
    static void Need(bool condition, string message) { if (!condition) throw new InvalidOperationException("Steel windows: " + message); }
    static void Progress(string message) { File.WriteAllText(Reports + "steel-windows-progress.txt", DateTime.Now.ToString("s") + " " + message); }
}
