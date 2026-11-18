using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class MicroLairGameplaySetup
{
    const string SourcePath = "Assets/Scenes/SampleScene.unity";
    public const string RigName = "Gameplay_Punisher";

    [MenuItem("Tools/Micro Lair/Copy Sample Scene Gameplay")]
    public static void CopyGameplay()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before copying gameplay.");
        var destination = SceneManager.GetSceneByPath(MicroLairGrayboxBuilder.ScenePath);
        if (!destination.IsValid() || !destination.isLoaded)
            destination = EditorSceneManager.OpenScene(MicroLairGrayboxBuilder.ScenePath, OpenSceneMode.Additive);
        if (destination.GetRootGameObjects().Any(g => g.name == RigName))
        { Validate(); Debug.Log("Micro lair gameplay already exists; kept existing rig."); return; }
        var environment = destination.GetRootGameObjects().First(g => g.name == "MicroLair_Graybox").transform;
        float scale = environment.Find(MicroLairRevisionB.MarkerName).localScale.x;
        Directory.CreateDirectory("Diagnostics/MicroLair");
        string backup = "Diagnostics/MicroLair/MicroLair_Graybox_before_gameplay.unity";
        if (!File.Exists(backup) && !EditorSceneManager.SaveScene(destination, backup, true))
            throw new IOException("Could not save the pre-gameplay scene backup.");
        var source = SceneManager.GetSceneByPath(SourcePath);
        bool openedSource = !source.IsValid() || !source.isLoaded;
        if (openedSource) source = EditorSceneManager.OpenScene(SourcePath, OpenSceneMode.Additive);
        GameObject container = null;
        try
        {
            var roots = source.GetRootGameObjects();
            var originals = new[] { roots.First(g => g.name == "rigger"), roots.First(g => g.name == "Main Camera"), roots.First(g => g.name == "thirdper") };
            SceneManager.SetActiveScene(destination);
            container = new GameObject(RigName);
            var map = new Dictionary<Object, Object>();
            var clones = new List<GameObject>();
            foreach (var original in originals)
            {
                var clone = Object.Instantiate(original, container.transform);
                clone.name = original.name;
                clones.Add(clone);
                MapHierarchy(original.transform, clone.transform, map);
            }
            // Instantiate preserves references within a root. Rebind references between
            // the player, physical camera and virtual camera without touching asset refs.
            foreach (var component in container.GetComponentsInChildren<Component>(true))
            {
                if (component == null) throw new InvalidOperationException("Source gameplay has a missing script.");
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue != null && map.TryGetValue(property.objectReferenceValue, out var replacement))
                        property.objectReferenceValue = replacement;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var player = clones[0];
            Vector3 shift = new Vector3(4.4f * scale, 0.025f, -6.35f * scale) - player.transform.position;
            foreach (var clone in clones) clone.transform.position += shift;
            player.transform.rotation = Quaternion.identity;
            player.tag = "Player";
            foreach (var collider in player.GetComponentsInChildren<Collider>(true)) collider.gameObject.tag = "Player";
            var camera = clones[1].GetComponent<Camera>();
            var virtualCamera = clones[2].GetComponent<CinemachineCamera>();
            var follow = clones[2].GetComponent<CinemachineThirdPersonFollow>();
            var movement = player.GetComponent<PunisherMovement>();
            var controls = player.GetComponentInChildren<ThirdPersonCameraController>(true);
            controls.transform.rotation = Quaternion.identity;
            controls.characterRoot = player.transform;
            controls.virtualCamera = virtualCamera;
            controls.shoulderFollow = follow;
            movement.cameraTransform = camera.transform;
            var target = virtualCamera.Target;
            target.TrackingTarget = controls.transform;
            virtualCamera.Target = target;
            var lens = virtualCamera.Lens;
            lens.NearClipPlane = 0.04f;
            virtualCamera.Lens = lens;
            camera.nearClipPlane = 0.04f;
            var obstacles = follow.AvoidObstacles;
            obstacles.Enabled = true;
            obstacles.IgnoreTag = "Player";
            obstacles.CameraRadius = 0.08f;
            follow.AvoidObstacles = obstacles;
            foreach (var ik in player.GetComponentsInChildren<RifleHandIK>(true)) ik.previewAimInSceneView = false;
            var shooter = player.GetComponent<PunisherShooter>() ?? player.AddComponent<PunisherShooter>();
            shooter.animator = movement.animator;
            shooter.aimingCamera = camera;
            shooter.cameraController = controls;
            shooter.muzzle = player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Muzzle");
            var inspection = environment.Find("09_InspectionCameraAndLights");
            foreach (var oldCamera in inspection.GetComponentsInChildren<Camera>(true))
            {
                oldCamera.enabled = false;
                oldCamera.tag = "Untagged";
                if (oldCamera.TryGetComponent<AudioListener>(out var listener)) listener.enabled = false;
            }
            environment.Find("10_Optional_PunisherScaleReference")?.gameObject.SetActive(false);
            Undo.RegisterCreatedObjectUndo(container, "Copy Punisher gameplay to Micro lair");
            EditorSceneManager.MarkSceneDirty(destination);
            Validate();
            if (!EditorSceneManager.SaveScene(destination)) throw new IOException("Could not save Micro lair gameplay scene.");
            Selection.activeGameObject = player;
            Debug.Log("MICRO_GAMEPLAY_SETUP_COMPLETE: Full SampleScene character, weapon, input and camera rig copied and saved.");
        }
        finally
        {
            if (openedSource && source.IsValid() && source.isLoaded) EditorSceneManager.CloseScene(source, true);
            SceneManager.SetActiveScene(destination);
        }
    }

    static void MapHierarchy(Transform source, Transform clone, Dictionary<Object, Object> map)
    {
        map[source.gameObject] = clone.gameObject;
        var a = source.GetComponents<Component>();
        var b = clone.GetComponents<Component>();
        if (a.Length != b.Length || source.childCount != clone.childCount) throw new InvalidOperationException("Clone hierarchy mismatch.");
        for (int i = 0; i < a.Length; i++) if (a[i] != null) map[a[i]] = b[i];
        for (int i = 0; i < source.childCount; i++) MapHierarchy(source.GetChild(i), clone.GetChild(i), map);
    }

    [MenuItem("Tools/Micro Lair/Validate Gameplay References")]
    public static void Validate()
    {
        var scene = SceneManager.GetSceneByPath(MicroLairGrayboxBuilder.ScenePath);
        var root = scene.GetRootGameObjects().First(g => g.name == RigName);
        var report = new StringBuilder();
        void Require(bool condition, string description)
        { report.AppendLine((condition ? "PASS " : "FAIL ") + description); if (!condition) throw new InvalidOperationException(report.ToString()); }
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            Require(component != null, "Component present");
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                var value = property.objectReferenceValue;
                GameObject go = value is GameObject obj ? obj : value is Component comp ? comp.gameObject : null;
                if (go != null && go.scene.IsValid() && go.scene != scene)
                    throw new InvalidOperationException("External scene reference: " + component.name + "." + property.propertyPath);
            }
        }
        report.Clear();
        report.AppendLine("PASS All copied components exist; all scene-object references stay in MicroLair_Graybox.");
        var movement = root.GetComponentInChildren<PunisherMovement>();
        var controls = root.GetComponentInChildren<ThirdPersonCameraController>();
        var shooter = root.GetComponentInChildren<PunisherShooter>();
        var camera = root.GetComponentInChildren<Camera>();
        var virtualCamera = root.GetComponentInChildren<CinemachineCamera>();
        Require(movement.controller != null && movement.animator != null && movement.animator.runtimeAnimatorController != null, "CharacterController, humanoid Animator and animation controller assigned.");
        Require(movement.cameraTransform == camera.transform && controls.virtualCamera == virtualCamera && virtualCamera.Target.TrackingTarget == controls.transform, "Movement and Cinemachine target linked to copied rig.");
        Require(shooter.aimingCamera == camera && shooter.cameraController == controls && shooter.animator == movement.animator, "Shooting references assigned to copied rig.");
        Require(root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "ar15"), "Complete AR15 hierarchy included.");
        Require(root.GetComponentsInChildren<RifleHandIK>().All(ik => !ik.previewAimInSceneView), "Editor aim preview off so normal gameplay input works.");
        Require(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>()).Count(c => c.enabled && c.CompareTag("MainCamera")) == 1, "One active MainCamera in destination.");
        Require(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AudioListener>()).Count(l => l.enabled) == 1, "One active AudioListener in destination.");
        Require(controls.shoulderFollow.AvoidObstacles.Enabled, "Indoor camera obstacle avoidance enabled.");
        report.AppendLine($"Player spawn: {movement.transform.position:F5}; scale: {movement.transform.localScale}; capsule height: {movement.controller.height:F5}.");
        Directory.CreateDirectory("Documentation/MicroLair");
        File.WriteAllText("Documentation/MicroLair/gameplay-validation.txt", report.ToString());
    }
}
