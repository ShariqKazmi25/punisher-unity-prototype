using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class MicroLairRevisionE
{
    public const string MarkerName = "RevisionE_UpperFloorMeetsSideWalls";

    [MenuItem("Tools/Micro Lair/Fill Upper Side Floor Gaps")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        var scene = SceneManager.GetSceneByPath(MicroLairGrayboxBuilder.ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(MicroLairGrayboxBuilder.ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var root = scene.GetRootGameObjects().First(g => g.name == "MicroLair_Graybox").transform;
        if (!root.Find(MicroLairRevisionD.MarkerName)) throw new InvalidOperationException("The wider revision D layout is required.");
        if (!root.Find(MarkerName))
        {
            string backup = "Diagnostics/MicroLair/MicroLair_Graybox_before_revision_e.unity";
            if (!File.Exists(backup) && !EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Could not back up the scene.");
            const string folder = "Assets/Environment/MicroLair/Meshes/RevisionE";
            Directory.CreateDirectory(folder);
            float s = root.Find(MicroLairRevisionB.MarkerName).localScale.x;
            string[] paths = { "02_RaisedPlatforms/LivingBay_RoundedInnerCorner", "02_RaisedPlatforms/ControlBay_RoundedInnerCorner", "08_ProvisionalRearConnection/RearDeck_Connection_Assumed" };
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Fill upper floor side gaps");
            foreach (string path in paths)
            {
                var part = root.Find(path);
                var filter = part.GetComponent<MeshFilter>();
                var mesh = Object.Instantiate(filter.sharedMesh);
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var world = part.TransformPoint(vertices[i]);
                    float x = world.x / s;
                    if (Mathf.Abs(x) >= 8.69f) world.x += Mathf.Sign(x) * 2 * s;
                    vertices[i] = part.InverseTransformPoint(world);
                }
                mesh.vertices = vertices;
                MicroLairRevisionC.RetilePrism(mesh, s);
                mesh.RecalculateBounds(); mesh.RecalculateTangents(); Unwrapping.GenerateSecondaryUVSet(mesh);
                mesh.name = part.name + "_WallToWall";
                string asset = folder + "/" + mesh.name + ".asset";
                if (File.Exists(asset)) throw new IOException("Floor-fill asset already exists without scene marker: " + asset);
                AssetDatabase.CreateAsset(mesh, asset);
                filter.sharedMesh = mesh;
                if (part.TryGetComponent<MeshCollider>(out var mc)) { mc.sharedMesh = null; mc.sharedMesh = mesh; }
                if (part.TryGetComponent<BoxCollider>(out var bc)) { bc.center = mesh.bounds.center; bc.size = mesh.bounds.size; }
            }
            new GameObject(MarkerName).transform.SetParent(root, false);
            AssetDatabase.SaveAssets();
        }
        MicroLairRevisionD.Validate();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the filled upper floor.");
        MicroLairRevisionD.CapturePreviews();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("MICRO_LAIR_REVISION_E_COMPLETE: Both side trenches filled flush with the upper deck, including the rear connection.");
    }
}
