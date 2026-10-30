using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AimFitCapture
{
    static AimFitCapture() { EditorApplication.delayCall += Capture; }
    static void Capture()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists("Diagnostics/Shooting/aim-fit-live.txt")) return;
        try
        {
            var movement = UnityEngine.Object.FindAnyObjectByType<PunisherMovement>();
            if (movement == null) return;
            PrefabUtility.SaveAsPrefabAsset(movement.gameObject, "Assets/Editor/AimFitFixture.prefab");
            var sb = new StringBuilder(DateTime.Now.ToString("O") + "\n");
            foreach (var t in movement.GetComponentsInChildren<Transform>(true))
                if (t.name == "WeaponMount" || t.name == "ar15" || t.name == "LeftHandGrip" || t.name == "Muzzle")
                    sb.AppendLine(t.name + " parent=" + t.parent?.name + " localPos=" + t.localPosition.ToString("F6") + " localRot=" + t.localRotation.ToString("F6") + " scale=" + t.localScale.ToString("F6"));
            var ik = movement.GetComponentInChildren<RifleHandIK>();
            sb.AppendLine("Grip IK: " + JsonUtility.ToJson(ik, true));
            sb.AppendLine("AR15 clips: " + string.Join(", ", AssetDatabase.LoadAllAssetsAtPath("Assets/punisher/ar15/source/ar15.fbx").OfType<AnimationClip>().Select(c => c.name + " " + c.length)));
            File.WriteAllText("Diagnostics/Shooting/aim-fit-live.txt", sb.ToString());
        }
        catch (Exception e) { File.WriteAllText("Diagnostics/Shooting/aim-fit-capture-error.txt", e.ToString()); }
    }
}
