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

public static class MicroLairRevisionD
{
    public const string MarkerName = "RevisionD_WiderDeeperLowerLevel";
    const string MeshFolder = "Assets/Environment/MicroLair/Meshes/RevisionD";
    const string Reports = "Documentation/MicroLair/";
    public static float MapZ(float z) => z < -4.8f ? -4.8f + (z + 4.8f) * (8.2f / 5.2f)
        : z <= -1.2f ? z : -1.2f + (z + 1.2f) * 1.12f;
    static float ShellX(float x) => Mathf.Abs(x) >= 8.69f ? x + Mathf.Sign(x) * 2 : x;
    static Transform Root() => SceneManager.GetSceneByPath(MicroLairGrayboxBuilder.ScenePath).GetRootGameObjects().First(g => g.name == "MicroLair_Graybox").transform;

    [MenuItem("Tools/Micro Lair/Apply Wider Lower Level Revision D")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        var scene = SceneManager.GetSceneByPath(MicroLairGrayboxBuilder.ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(MicroLairGrayboxBuilder.ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var root = Root();
        if (!root.Find(MicroLairRevisionC.MarkerName)) throw new InvalidOperationException("Revision D requires the existing revision C scene.");
        if (root.Find(MarkerName)) { Validate(); CapturePreviews(); EditorSceneManager.SaveScene(scene); return; }
        float s = root.Find(MicroLairRevisionB.MarkerName).localScale.x;
        var reference = root.Find("10_Optional_PunisherScaleReference");
        var stairs = root.Find("03_Stairs");
        var floor = root.Find("01_LowerFloor");
        var ceilings = root.Find("05_Ceilings");
        var walls = root.Find("06_OuterWalls");
        var filters = root.GetComponentsInChildren<MeshFilter>(true).Where(f => !f.transform.IsChildOf(reference)).ToArray();
        foreach (var f in filters)
            if (Quaternion.Angle(f.transform.rotation, Quaternion.identity) > .001f || (f.transform.lossyScale - Vector3.one).sqrMagnitude > .000001f)
                throw new InvalidOperationException("Unexpected structural rotation/scale: " + f.name);
        Directory.CreateDirectory("Diagnostics/MicroLair");
        string backup = "Diagnostics/MicroLair/MicroLair_Graybox_before_revision_d.unity";
        if (!File.Exists(backup) && !EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Could not back up the scene.");
        Directory.CreateDirectory(MeshFolder);
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Wider and deeper lower floor");
        var changes = new List<(MeshFilter filter, Mesh mesh, Vector3 position)>();
        int serial = 0;
        foreach (var f in filters)
        {
            if (f.transform.IsChildOf(stairs)) continue;
            var mesh = Object.Instantiate(f.sharedMesh);
            var p = f.transform.position / s;
            var oldMin = p + mesh.bounds.min / s;
            var oldMax = p + mesh.bounds.max / s;
            bool pillar = f.name.Contains("Pillar"), cross = f.name.Contains("CrossBeam"), longitudinal = f.name.Contains("LongitudinalBeam");
            bool widen = f.transform.IsChildOf(floor) || f.transform.IsChildOf(ceilings) || f.transform.IsChildOf(walls) || cross;
            float pillarShift = f.name == "Right_FrontPillar" ? 2 : 0;
            var pivot = new Vector3((widen ? ShellX(p.x) : p.x) + pillarShift, p.y, MapZ(p.z));
            float BeamZ(float z)
            {
                float a, b, rear = MapZ(MicroLairRevisionC.MapZ(4.8f));
                if (f.name.EndsWith("_1")) { a = MapZ(MicroLairRevisionC.MapZ(-7.7f)); b = -4.425f; }
                else if (f.name.EndsWith("_2")) { a = -3.975f; b = rear - .225f; }
                else { a = rear + .225f; b = MapZ(MicroLairRevisionC.MapZ(7.5f)); }
                return Mathf.Lerp(a, b, Mathf.InverseLerp(oldMin.z, oldMax.z, z));
            }
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                var v = p + vertices[i] / s;
                float x = (widen ? ShellX(v.x) : v.x) + pillarShift;
                float z = pillar || cross ? MapZ(p.z) + v.z - p.z : longitudinal ? BeamZ(v.z) : MapZ(v.z);
                vertices[i] = (new Vector3(x, v.y, z) - pivot) * s;
            }
            mesh.vertices = vertices;
            MicroLairRevisionC.RetilePrism(mesh, s);
            mesh.RecalculateBounds(); mesh.RecalculateTangents(); Unwrapping.GenerateSecondaryUVSet(mesh);
            mesh.name = f.name + "_RevisionD";
            string path = MeshFolder + "/" + (++serial).ToString("00") + "_" + f.name + ".asset";
            if (File.Exists(path)) throw new IOException("Existing revision D asset without scene marker: " + path);
            AssetDatabase.CreateAsset(mesh, path);
            changes.Add((f, mesh, pivot * s));
        }
        foreach (var change in changes)
        {
            change.filter.transform.position = change.position;
            change.filter.sharedMesh = change.mesh;
            if (change.filter.TryGetComponent<MeshCollider>(out var mc)) { mc.sharedMesh = null; mc.sharedMesh = change.mesh; }
            if (change.filter.TryGetComponent<BoxCollider>(out var bc)) { bc.center = change.mesh.bounds.center; bc.size = change.mesh.bounds.size; }
        }
        floor.GetChild(0).name = "LowerFloor_22x31p38m_CharacterScale";
        foreach (Transform marker in root.Find("07_ReservedSpaces_NoGeometry"))
        { var p = marker.position; p.z = MapZ(p.z / s) * s; marker.position = p; }
        foreach (var light in root.GetComponentsInChildren<Light>(true).Where(l => l.type != LightType.Directional))
        { var p = light.transform.position; p.z = MapZ(p.z / s) * s; light.transform.position = p; }
        new GameObject(MarkerName).transform.SetParent(root, false);
        AssetDatabase.SaveAssets();
        Validate(); MicroLairGameplaySetup.Validate();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save revision D.");
        CapturePreviews(); EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("MICRO_LAIR_REVISION_D_COMPLETE: Wider lower level, deeper front apron and slightly longer upper corridors saved.");
    }

    [MenuItem("Tools/Micro Lair/Validate Revision D")]
    public static void Validate()
    {
        var root = Root(); float s = root.Find(MicroLairRevisionB.MarkerName).localScale.x;
        var scene = root.gameObject.scene;
        bool filledSides = root.Find(MicroLairRevisionE.MarkerName) != null;
        var reference = root.Find("10_Optional_PunisherScaleReference");
        void Need(bool ok, string message) { if (!ok) throw new InvalidOperationException("Revision D: " + message); }
        foreach (var f in root.GetComponentsInChildren<MeshFilter>(true).Where(f => !f.transform.IsChildOf(reference)))
        {
            var mesh = f.sharedMesh;
            Need(mesh && AssetDatabase.Contains(mesh), "saved mesh " + f.name);
            Need(mesh.uv.Length == mesh.vertexCount && mesh.uv2.Length == mesh.vertexCount, "UV sets " + f.name);
            Need((f.transform.lossyScale - Vector3.one).sqrMagnitude < .000001f, "unit scale " + f.name);
            var materials = f.GetComponent<MeshRenderer>().sharedMaterials;
            Need(materials.Length == mesh.subMeshCount && materials.All(m => m), "material slots " + f.name);
            var collider = f.GetComponent<Collider>(); Need(collider && collider.enabled, "collider " + f.name);
            if (collider is MeshCollider mc) Need(mc.sharedMesh == mesh, "mesh collider " + f.name);
            if (collider is BoxCollider bc) Need((bc.center - mesh.bounds.center).sqrMagnitude < .000001f && (bc.size - mesh.bounds.size).sqrMagnitude < .000001f, "box collider " + f.name);
        }
        Physics.SyncTransforms();
        bool Own(Collider c) => c.transform.IsChildOf(root);
        RaycastHit[] Hits(Vector3 p, Vector3 direction, float length) => Physics.RaycastAll(p, direction, length).Where(h => Own(h.collider)).OrderBy(h => h.distance).ToArray();
        var controller = scene.GetRootGameObjects().First(g => g.name == MicroLairGameplaySetup.RigName).GetComponentInChildren<CharacterController>();
        int lowerSamples = 0, upperSamples = 0;
        void Floor(float x, float z, float y)
        {
            var hits = Hits(new Vector3(x, 2.4f, z) * s, Vector3.down, 3 * s);
            Need(hits.Length > 0 && Mathf.Abs(hits[0].point.y - y * s) < .002f, "floor " + x + "," + z);
        }
        void Clearance(float x, float z, float y)
        {
            Floor(x, z, y);
            var low = new Vector3(x * s, y * s + controller.radius + .015f, z * s);
            var high = low + Vector3.up * (controller.height - 2 * controller.radius);
            Need(!Physics.OverlapCapsule(low, high, controller.radius).Any(Own), "player clearance " + x + "," + z);
            var roof = Hits(new Vector3(x * s, y * s + controller.height, z * s), Vector3.up, 8 * s);
            Need(roof.Length > 0 && roof[0].point.y >= 6.24f * s, "roof continuity/headroom " + x + "," + z);
        }
        float rear = MapZ(MicroLairRevisionC.MapZ(7.5f));
        foreach (float x in new[] { -4.4f, 4.4f })
        {
            for (int i = 1; i <= 6; i++) Floor(x, -4.8f + (i - .5f) * .3f, i * .17f);
            for (float z = -2.9f; z < rear - .5f; z += .35f) { Clearance(x, z, 1.02f); upperSamples++; }
            Need(Hits(new Vector3(x * s, 1.02f * s + .55f, -2.8f * s), Vector3.forward, (rear - .7f + 2.8f) * s).Length == 0, "upper shooting lane");
        }
        foreach (float x in new[] { -9.7f, 9.7f })
            for (float z = -2.5f; z < rear - .5f; z += .4f)
            { Clearance(x, z, filledSides ? 1.02f : 0); if (filledSides) upperSamples++; else lowerSamples++; }
        if (filledSides)
            foreach (float x in new[] { -10.6f, -8.75f, -8.65f, 8.65f, 8.75f, 10.6f })
                foreach (float z in new[] { -2f, 6f, 14f, rear - .1f }) Floor(x, z, 1.02f);
        foreach (float z in new[] { -11.4f, -8.5f, -5.8f })
            foreach (float x in new[] { -9.7f, -7f, -4f, 0f, 4f, 7f, 9.7f }) { Clearance(x, z, 0); lowerSamples++; }
        // With filled side strips, the lower approach ends at the platform front.
        foreach (float x in new[] { -10.2f, 10.2f })
            for (float z = -5.8f; z < (filledSides ? -3.5f : -2.5f); z += .2f) { Clearance(x, z, 0); lowerSamples++; }
        Need(Hits(new Vector3(4.4f, 2.4f, rear - .5f) * s, Vector3.forward, 2 * s).Length == 0, "rear door opening");
        float front = MapZ(MicroLairRevisionC.MapZ(-7.7f));
        Need(Hits(new Vector3(0, 1.4f, front + .5f) * s, Vector3.back, 2 * s).Length == 0, "front door opening");
        Need(Hits(new Vector3(9.7f, 1.4f, 5) * s, Vector3.right, 2 * s).Length > 0, "widened side wall stops shots");
        var bounds = root.Find("01_LowerFloor").GetComponentInChildren<MeshRenderer>().bounds;
        Need(Mathf.Abs(bounds.size.x / s - 22) < .01f && Mathf.Abs(bounds.size.z / s - 31.3776f) < .02f, "lower floor size");
        foreach (var pillar in root.Find("04_PillarsAndBeams").GetComponentsInChildren<MeshRenderer>().Where(r => r.name.Contains("Pillar")))
            Need(Mathf.Abs(pillar.bounds.size.x - pillar.bounds.size.z) < .001f && Mathf.Abs(pillar.bounds.max.y / s - 6.25f) < .01f, "round pillar/beam contact");
        Need(Mathf.Abs(root.Find("04_PillarsAndBeams/Right_FrontPillar").position.x / s - 9.3f) < .01f, "right pillar follows outer wall");
        Need(Mathf.Abs(root.Find("04_PillarsAndBeams/Left_FrontPillar").position.x / s + 2.65f) < .01f, "left pillar remains beside hub");
        Need(Mathf.Abs(controller.height - .77f) < .0001f && controller.transform.localScale == Vector3.one, "Punisher scale");
        var report = new StringBuilder();
        report.AppendLine("PASS Saved mesh assets, both UV sets, material slots, unit scales and matching colliders.");
        report.AppendLine($"PASS {lowerSamples} lower-floor and {upperSamples} upper-corridor floor, player capsule and roof/headroom samples.");
        report.AppendLine("PASS All 12 unchanged stair treads, clear doorways, long upper shooting lanes and widened side-wall shot collision.");
        report.AppendLine(filledSides ? "PASS Both former side trenches are flush with the upper deck, meet the walls and continue through the rear connection. Lower front apron remains intact." : "PASS Both new lower side strips connect to the front apron around the pillars.");
        report.AppendLine("PASS Round pillars, asymmetrical front placement, original player scale and existing ceiling height.");
        report.AppendLine($"Lower floor: {bounds.size.x:F3} x {bounds.size.z:F3} Unity units = 22 x 31.3776 nominal m.");
        report.AppendLine($"Front floor-edge to stair foot: 8.20 nominal m (was 5.20). Upper aisle run: {rear + 3:F4} nominal m (was 18.33).");
        report.AppendLine("Edit-time physics validation; gameplay input and shooting scripts unchanged.");
        File.WriteAllText(Reports + (filledSides ? "revision-e-validation.txt" : "revision-d-validation.txt"), report.ToString());
    }

    [MenuItem("Tools/Micro Lair/Capture Revision D Previews")]
    public static void CapturePreviews()
    {
        var root = Root(); float s = root.Find(MicroLairRevisionB.MarkerName).localScale.x;
        string prefix = root.Find(MicroLairRevisionE.MarkerName) ? "revision-e" : "revision-d";
        var camera = MicroLairRevisionC.NewCamera(root);
        var hidden = new[] { root.Find("05_Ceilings").gameObject, root.Find("06_OuterWalls/FrontWall_EntryOpening_Assumed").gameObject, root.Find("06_OuterWalls/EastWall").gameObject };
        var states = hidden.Select(g => g.activeSelf).ToArray();
        try
        {
            camera.transform.position = new Vector3(-1, 2.5f, -10.8f) * s;
            camera.transform.LookAt(new Vector3(0, 2.7f, 4) * s); camera.fieldOfView = 70;
            MicroLairRevisionC.Render(camera, prefix + "-interior.png");
            foreach (var item in hidden) item.SetActive(false);
            camera.orthographic = true; camera.orthographicSize = 17.5f * s;
            camera.transform.position = new Vector3(21, 29, -34) * s;
            camera.transform.LookAt(new Vector3(0, 2, 2.7f) * s);
            MicroLairRevisionC.Render(camera, prefix + "-cutaway.png");
        }
        finally
        {
            for (int i = 0; i < hidden.Length; i++) hidden[i].SetActive(states[i]);
            Object.DestroyImmediate(camera.gameObject);
        }
    }
}
