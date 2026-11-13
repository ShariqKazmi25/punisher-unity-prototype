using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class MicroLairRevisionB
{
    const string MeshFolder = "Assets/Environment/MicroLair/Meshes/RevisionB";
    public const string MarkerName = "RevisionB_WorldUnitsPerMeter";
    static float MapX(float x)
    {
        float a = Mathf.Abs(x);
        float b = a <= 4 ? a * 0.8f : a <= 6.4f ? a - 0.8f : a <= 8.7f ? 5.6f + (a - 6.4f) * 3.1f / 2.3f : a;
        return Mathf.Sign(x) * b;
    }

    [MenuItem("Tools/Micro Lair/Apply Reference Revision B")]
    public static void Apply()
    {
        var scene = SceneManager.GetSceneByPath(MicroLairGrayboxBuilder.ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(MicroLairGrayboxBuilder.ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var root = scene.GetRootGameObjects().First(g => g.name == "MicroLair_Graybox").transform;
        if (root.Find(MarkerName)) { Finish(scene, root); return; }
        float height = MeasureCharacter(out var character, out var source, out bool opened);
        SceneManager.SetActiveScene(scene);
        float s = height / 1.83f;
        Directory.CreateDirectory("Diagnostics/MicroLair");
        string backup = "Diagnostics/MicroLair/MicroLair_Graybox_before_revision_b.unity";
        if (!File.Exists(backup) && !EditorSceneManager.SaveScene(scene, backup, true)) throw new Exception("Could not save revision backup.");
        Directory.CreateDirectory(MeshFolder);
        AssetDatabase.Refresh();
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Micro lair reference revision B");
        var filters = root.GetComponentsInChildren<MeshFilter>(true);
        var all = root.GetComponentsInChildren<Transform>(true);
        foreach (var f in filters)
        {
            if (f.transform.localRotation != Quaternion.identity || f.transform.localScale != Vector3.one)
                throw new Exception("Unexpected custom mesh transform on " + f.name + "; revision stopped before editing geometry.");
        }
        // Remap positions separately from vertices so objects retain useful independent pivots.
        var oldPositions = all.ToDictionary(t => t, t => root.InverseTransformPoint(t.position));
        int serial = 0;
        foreach (var f in filters)
        {
            var old = f.sharedMesh;
            Mesh mesh;
            Vector3 p = oldPositions[f.transform];
            bool pillar = f.name.Contains("Pillar");
            bool beam = f.name.Contains("LongitudinalBeam");
            Vector3 target = new Vector3(MapX(p.x), p.y, p.z);
            if (pillar || beam) target.x = p.x;
            if (f.name == "Left_FrontPillar") target.x = -2.65f;
            if (f.name == "Right_FrontPillar") target.x = 7.3f;
            if (f.name.StartsWith("Hub_RoundedFront"))
            {
                var outline = Nose();
                mesh = MicroLairGrayboxBuilder.Prism(outline, 0, 1.02f);
                f.name = "Hub_ContinuousCurve_6p4m_CharacterScale";
                var v = mesh.vertices;
                for (int i=0;i<v.Length;i++) v[i] *= s;
                mesh.vertices = v;
                SmoothNose(mesh, s);
            }
            else
            {
                mesh = Object.Instantiate(old);
                var vertices = mesh.vertices;
                for (int i=0;i<vertices.Length;i++)
                {
                    var v = vertices[i];
                    vertices[i] = new Vector3(pillar || beam ? v.x : MapX(p.x + v.x) - target.x, v.y, v.z) * s;
                }
                mesh.vertices = vertices;
            }
            // Preserve UV0's nominal meter tiling so later textures have consistent apparent size.
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            string asset = MeshFolder + "/" + (++serial).ToString("00") + "_" + f.name + ".asset";
            mesh.name = f.name;
            AssetDatabase.CreateAsset(mesh, asset);
            f.sharedMesh = mesh;
            f.transform.localPosition = target * s;
            if (f.TryGetComponent<MeshCollider>(out var mc)) { mc.sharedMesh = null; mc.sharedMesh = mesh; }
            if (f.TryGetComponent<BoxCollider>(out var bc)) { bc.center = mesh.bounds.center; bc.size = mesh.bounds.size; }
        }
        foreach (var t in all)
        {
            if (t == root || t.GetComponent<MeshFilter>()) continue;
            var p = oldPositions[t];
            // Group origins stay at zero; lights, camera, and reservation markers follow the new layout.
            t.localPosition = new Vector3(MapX(p.x), p.y, p.z) * s;
            if (t.TryGetComponent<Light>(out var light) && light.type != LightType.Directional) light.range *= s;
            if (t.TryGetComponent<Camera>(out var cam)) { cam.nearClipPlane *= s; cam.farClipPlane *= s; }
        }
        AddUpperFascia(root, s);
        var reference = BakeReference(character, root, s);
        if (opened) EditorSceneManager.CloseScene(source, true);
        var marker = new GameObject(MarkerName).transform;
        marker.SetParent(root, false);
        marker.localScale = Vector3.one * s;
        AssetDatabase.SaveAssets();
        Finish(scene, root);
    }

    static void Finish(Scene scene, Transform root)
    {
        Validate();
        EditorSceneManager.SaveScene(scene);
        var reference = root.Find("10_Optional_PunisherScaleReference").gameObject;
        reference.SetActive(true);
        try { MicroLairGrayboxBuilder.CapturePreviews(); }
        finally { reference.SetActive(false); }
        EditorSceneManager.SaveScene(scene);
        Debug.Log("MICRO_LAIR_REVISION_B_COMPLETE");
    }

    static List<Vector2> Nose()
    {
        var p = new List<Vector2>();
        // Half ellipse: tangent to straight side runs; no central flat front chord.
        for(int i=0;i<=48;i++)
        {
            float a = (180 + i * 180f / 48) * Mathf.Deg2Rad;
            p.Add(new Vector2(3.2f * Mathf.Cos(a), -1.2f + 1.8f * Mathf.Sin(a)));
        }
        p.Add(new Vector2(3.2f,5)); p.Add(new Vector2(-3.2f,5));
        return p;
    }

    static void SmoothNose(Mesh mesh, float s)
    {
        var vertices = mesh.vertices; var normals = mesh.normals;
        for(int i=0;i<vertices.Length;i++)
        {
            var v=vertices[i]/s;
            if(Mathf.Abs(normals[i].y)<0.5f && v.z<=-1.199f)
                normals[i]=new Vector3(v.x/(3.2f*3.2f),0,(v.z+1.2f)/(1.8f*1.8f)).normalized;
        }
        mesh.normals=normals;
    }

    static void AddUpperFascia(Transform root,float s)
    {
        var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var triangles=new List<int>();
        float u=0;
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int n=vertices.Count; float length=Vector3.Distance(a,d);
            vertices.AddRange(new[]{a*s,b*s,c*s,d*s});
            uv.AddRange(new[]{new Vector2(u,0),new Vector2(u,Vector3.Distance(a,b)),new Vector2(u+length,Vector3.Distance(a,b)),new Vector2(u+length,0)});
            triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3}); u+=length;
        }
        for(int i=0;i<48;i++)
        {
            float a=(180+i*180f/48)*Mathf.Deg2Rad,b=(180+(i+1)*180f/48)*Mathf.Deg2Rad;
            Vector3 O(float t,float y)=>new Vector3(3.2f*Mathf.Cos(t),y,-1.2f+1.8f*Mathf.Sin(t));
            Vector3 I(float t,float y)=>new Vector3(2.95f*Mathf.Cos(t),y,-1.2f+1.55f*Mathf.Sin(t));
            Quad(O(a,3.95f),O(a,4.5f),O(b,4.5f),O(b,3.95f));
            Quad(I(b,3.95f),I(b,4.5f),I(a,4.5f),I(a,3.95f));
            Quad(O(a,4.5f),I(a,4.5f),I(b,4.5f),O(b,4.5f));
            Quad(O(b,3.95f),I(b,3.95f),I(a,3.95f),O(a,3.95f));
        }
        Quad(new Vector3(-3.2f,3.95f,-1.2f),new Vector3(-2.95f,3.95f,-1.2f),new Vector3(-2.95f,4.5f,-1.2f),new Vector3(-3.2f,4.5f,-1.2f));
        Quad(new Vector3(2.95f,3.95f,-1.2f),new Vector3(3.2f,3.95f,-1.2f),new Vector3(3.2f,4.5f,-1.2f),new Vector3(2.95f,4.5f,-1.2f));
        var mesh=new Mesh {name="Hub_MatchingCurvedUpperFascia"}; mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();Unwrapping.GenerateSecondaryUVSet(mesh);
        AssetDatabase.CreateAsset(mesh,MeshFolder+"/Hub_MatchingCurvedUpperFascia.asset");
        var go=new GameObject(mesh.name);go.transform.SetParent(root.Find("05_Ceilings"),false);
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/MicroLair/Materials/Gray_Ceilings.mat");
        go.AddComponent<MeshCollider>().sharedMesh=mesh;
    }

    static GameObject BakeReference(GameObject source, Transform root, float s)
    {
        var group=new GameObject("10_Optional_PunisherScaleReference");group.transform.SetParent(root,false);
        var skins=source.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.enabled && r.gameObject.activeInHierarchy && !AssetDatabase.GetAssetPath(r.sharedMesh).ToLowerInvariant().Contains("ar15")).ToArray();
        var meshes=new List<Mesh>();float min=float.MaxValue;
        foreach(var skin in skins)
        {
            var m=new Mesh();skin.BakeMesh(m);var v=m.vertices;
            for(int i=0;i<v.Length;i++){v[i]=skin.transform.TransformPoint(v[i])-source.transform.position;min=Mathf.Min(min,v[i].y);}
            m.vertices=v;meshes.Add(m);
        }
        for(int j=0;j<skins.Length;j++)
        {
            var m=meshes[j];var v=m.vertices;
            for(int i=0;i<v.Length;i++) v[i].y-=min;
            m.vertices=v;m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();m.name="Reference_"+skins[j].name;
            AssetDatabase.CreateAsset(m,MeshFolder+"/"+m.name+".asset");
            var g=new GameObject(m.name);g.transform.SetParent(group.transform,false);g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterials=skins[j].sharedMaterials;
        }
        group.transform.localPosition=new Vector3(0,1.02f,-1.6f)*s;
        group.SetActive(false);return group;
    }

    public static void Validate()
    {
        var scene=SceneManager.GetActiveScene();var root=scene.GetRootGameObjects().First(g=>g.name=="MicroLair_Graybox").transform;
        float s=root.Find(MarkerName).localScale.x;
        void Need(bool ok,string message){if(!ok)throw new Exception("Revision B validation: "+message);}
        foreach(var f in root.GetComponentsInChildren<MeshFilter>(true).Where(f=>!f.transform.IsChildOf(root.Find("10_Optional_PunisherScaleReference"))))
        {
            var m=f.sharedMesh;Need(m && AssetDatabase.Contains(m),"saved mesh "+f.name);Need(m.uv.Length==m.vertexCount && m.uv2.Length==m.vertexCount,"UV sets "+f.name);
            Need(f.GetComponent<Collider>()!=null,"collider "+f.name);Need(f.GetComponent<MeshRenderer>().sharedMaterials.All(x=>x!=null),"materials "+f.name);
        }
        Physics.SyncTransforms();
        void Floor(float x,float z,float y)
        {
            var hits=Physics.RaycastAll(new Vector3(x,2,z)*s,Vector3.down,3*s).Where(h=>h.collider.gameObject.scene==scene).OrderBy(h=>h.distance).ToArray();
            Need(hits.Length>0 && Mathf.Abs(hits[0].point.y-y*s)<0.002f,"floor "+x+","+z);
        }
        Floor(0,-6,0);Floor(0,-2.7f,1.02f);Floor(7,1,1.02f);Floor(-7,1,1.02f);
        foreach(float x in new[]{-4.4f,4.4f})
        {
            for(int i=1;i<=6;i++) Floor(x,-4.8f+(i-.5f)*.3f,i*.17f);
            // Keep the capsule center at least its radius away from the closed back wall.
            for(float z=-2.9f;z<7.0f;z+=.4f)
            {
                Floor(x,z,1.02f);
                // Actual gameplay controller radius/height, not an assumed standard adult capsule.
                var overlaps=Physics.OverlapCapsule(new Vector3(x*s,1.02f*s+.185f,z*s),new Vector3(x*s,1.02f*s+.595f,z*s),.18f);
                Need(!overlaps.Any(c=>c.gameObject.scene==scene),"gameplay capsule clearance "+x+","+z);
            }
        }
        foreach(var ray in new[]{new Ray(new Vector3(4.4f,2,7)*s,Vector3.forward),new Ray(new Vector3(0,1.4f,-7)*s,Vector3.back)})
            Need(!Physics.RaycastAll(ray,1.2f*s).Any(h=>h.collider.gameObject.scene==scene),"door gap");
        Need(Mathf.Abs(root.Find("04_PillarsAndBeams/Left_FrontPillar").localPosition.x+2.65f*s)<.001f,"left pillar");
        Need(Mathf.Abs(root.Find("04_PillarsAndBeams/Right_FrontPillar").localPosition.x-7.3f*s)<.001f,"right pillar");
        File.WriteAllText("Documentation/MicroLair/revision-b-validation.txt","PASS\nSaved mesh assets, UVs, materials and colliders checked.\nBoth six-step flights meet the upper floor. Actual 0.77 x 0.36 unit controller clears both upper aisles. Door gaps clear.\nContinuous elliptical hub front and matching upper fascia; asymmetrical front pillar coordinates verified.\nWorld units per nominal meter: "+s.ToString("F6")+"\nNominal hub width: 6.4 m; side bays: 3.1 m each; aisle width: 2.4 m.\nGameplay controller movement has not been play-tested in this scene.\n");
    }
    public static float MeasureCharacter(out GameObject character, out Scene source, out bool opened)
    {
        source = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        opened = !source.IsValid() || !source.isLoaded;
        if (opened) source = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
        character = source.GetRootGameObjects().First(g => g.name == "rigger");
        var report = new StringBuilder();
        report.AppendLine("Gameplay model measurement (world-space baked skin vertices)");
        Bounds all = default;
        bool first = true;
        foreach (var skin in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skin.sharedMesh == null || !skin.enabled || !skin.gameObject.activeInHierarchy) continue;
            if (AssetDatabase.GetAssetPath(skin.sharedMesh).IndexOf("ar15", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            var mesh = new Mesh();
            skin.BakeMesh(mesh);
            Bounds b = default;
            bool start = true;
            foreach (var vertex in mesh.vertices)
            {
                var world = skin.transform.TransformPoint(vertex);
                if (start) { b = new Bounds(world, Vector3.zero); start = false; }
                else b.Encapsulate(world);
            }
            Object.DestroyImmediate(mesh);
            if (start) continue;
            report.AppendLine(skin.name + " size=" + b.size.ToString("F4") + " min=" + b.min.ToString("F4") + " max=" + b.max.ToString("F4"));
            if (first) { all = b; first = false; } else all.Encapsulate(b);
        }
        if (first || all.size.y < 0.2f || all.size.y > 5) throw new Exception("Unable to establish a plausible model height.");
        var controller = character.GetComponent<CharacterController>();
        report.AppendLine("Measured character height=" + all.size.y.ToString("F5") + " Unity units");
        report.AppendLine("Controller height=" + controller.height + "; radius=" + controller.radius + "; stepOffset=" + controller.stepOffset);
        report.AppendLine("World units per nominal meter (1.83 m adult)=" + (all.size.y / 1.83f).ToString("F6"));
        File.WriteAllText("Documentation/MicroLair/revision-b-scale.txt", report.ToString());
        return all.size.y;
    }

    public static void Inspect()
    {
        var previous = SceneManager.GetActiveScene();
        MeasureCharacter(out _, out var scene, out bool opened);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        SceneManager.SetActiveScene(previous);
        Debug.Log("MICRO_LAIR_SCALE_MEASURED");
    }
}
