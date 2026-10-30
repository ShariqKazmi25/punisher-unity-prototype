using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ApplySharedStandingAim
{
    const string Request = "Diagnostics/Shooting/apply-shared-standing-aim.request";
    static ApplySharedStandingAim() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        File.Delete(Request);
        try
        {
            var movement = UnityEngine.Object.FindAnyObjectByType<PunisherMovement>();
            if (movement == null) throw new Exception("No live Punisher actor found.");
            string before = Fingerprint(movement);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/punisher/animations/Punisher.controller");
            AssetDatabase.SaveAssetIfDirty(controller);
            File.Copy(AssetDatabase.GetAssetPath(controller), "Diagnostics/Shooting/Punisher-live-before-shared-aim.controller", true);
            SharedStandingAimGraph.Configure(controller);
            string after = Fingerprint(movement);
            if (before != after) throw new Exception("Standing settings changed unexpectedly; inspect the captured scene before saving.");
            bool savedScene = false;
            if (!EditorApplication.isPlayingOrWillChangePlaymode && movement.gameObject.scene.isDirty)
                savedScene = EditorSceneManager.SaveScene(movement.gameObject.scene);
            File.WriteAllText("Diagnostics/Shooting/user-standing-conserved-settings.txt", after);
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Copy(movement.gameObject.scene.path, "Diagnostics/Shooting/UserStanding-conserved.unity", true);
                PrefabUtility.SaveAsPrefabAsset(movement.gameObject, "Assets/Editor/UserStandingConservedFixture.prefab");
            }
            File.WriteAllText("Diagnostics/Shooting/shared-standing-aim-applied.txt",
                "Applied common standing aim/fire states to Weapon Actions. All standing IK fields, grip/weapon/target transforms matched before and after exactly. Scene saved=" + savedScene + "; playing=" + Application.isPlaying + ".\n");
            Debug.Log("Punisher: your standing tuning is conserved; walking uses the same upper-body aim/fire states.");
        }
        catch(Exception e) { File.WriteAllText("Diagnostics/Shooting/shared-standing-aim-apply-error.txt",e.ToString()); Debug.LogException(e); }
    }
    static string Fingerprint(PunisherMovement movement)
    {
        var ik = movement.GetComponentInChildren<RifleHandIK>();
        var report = new StringBuilder(JsonUtility.ToJson(ik,true) + "\n");
        foreach(var field in typeof(RifleHandIK).GetFields(BindingFlags.Public|BindingFlags.Instance))
        {
            if(field.FieldType != typeof(Transform))continue;
            var t=field.GetValue(ik) as Transform; if(t==null)continue;
            report.AppendLine(field.Name+": "+t.name+" parent="+t.parent?.name+" position="+t.localPosition.ToString("F9")+" rotation="+t.localRotation.ToString("F9")+" scale="+t.localScale.ToString("F9"));
        }
        return report.ToString();
    }
}
