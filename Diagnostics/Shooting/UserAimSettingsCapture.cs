using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class UserAimSettingsCapture
{
    static UserAimSettingsCapture() { EditorApplication.delayCall += Capture; }
    static void Capture()
    {
        const string reportPath = "Diagnostics/Shooting/user-standing-live-settings.txt";
        if (File.Exists(reportPath)) return;
        try
        {
            var movement = UnityEngine.Object.FindAnyObjectByType<PunisherMovement>();
            if (movement == null) return;
            var ik = movement.GetComponentInChildren<RifleHandIK>();
            if (ik == null) return;
            var report = new StringBuilder("Captured " + DateTime.Now.ToString("O") + "; playing=" + Application.isPlaying + "; scene=" + movement.gameObject.scene.path + "\n");
            report.AppendLine(JsonUtility.ToJson(ik, true));
            foreach (var field in typeof(RifleHandIK).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.FieldType != typeof(Transform)) continue;
                var target = field.GetValue(ik) as Transform;
                if (target == null) continue;
                report.AppendLine(field.Name + "=" + PathOf(target, movement.transform) + "; parent=" + PathOf(target.parent, movement.transform) +
                    "; localPosition=" + target.localPosition.ToString("F7") + "; localRotation=" + target.localRotation.ToString("F7") + "; localScale=" + target.localScale.ToString("F7"));
            }
            foreach (var field in typeof(RifleHandIK).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                if (field.Name == "hasIdleGrip" || field.Name == "idleGripPosition" || field.Name == "idleGripRotation") report.AppendLine(field.Name + "=" + field.GetValue(ik));
            var a = movement.animator;
            report.AppendLine("Controller=" + AssetDatabase.GetAssetPath(a.runtimeAnimatorController));
            report.AppendLine("Scene dirty=" + movement.gameObject.scene.isDirty);
            PrefabUtility.SaveAsPrefabAsset(movement.gameObject, "Assets/Editor/UserStandingLiveFixture.prefab");
            File.WriteAllText(reportPath, report.ToString());
        }
        catch(Exception e) { File.WriteAllText("Diagnostics/Shooting/user-standing-capture-error.txt", e.ToString()); }
    }
    static string PathOf(Transform t, Transform root)
    {
        if (t == null) return "<null>";
        if (t == root) return ".";
        return (t.parent != null ? PathOf(t.parent, root) + "/" : "") + t.name;
    }
}
