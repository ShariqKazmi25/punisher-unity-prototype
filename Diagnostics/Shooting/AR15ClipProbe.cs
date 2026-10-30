using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class AR15ClipProbe
{
    public static void Inspect()
    {
        var report = new StringBuilder();
        try
        {
            const string path = "Assets/ar15.fbx";
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => c.name == "Armature.003|shooting");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long id);
            report.AppendLine("CLIP " + clip.name + " GUID=" + guid + " fileID=" + id + " length=" + clip.length + " fps=" + clip.frameRate + " legacy=" + clip.legacy + " loop=" + clip.isLooping + " human=" + clip.humanMotion + " root=" + clip.hasRootCurves + " motion=" + clip.hasMotionCurves);
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                report.AppendLine(binding.path + " | " + binding.type.Name + " | " + binding.propertyName + " | range=" + curve.keys.Min(k => k.value) + ".." + curve.keys.Max(k => k.value));
            }
            var gun = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var bones = gun.GetComponentsInChildren<Transform>();
            var positions = bones.Select(t => t.localPosition).ToArray();
            var rotations = bones.Select(t => t.localRotation).ToArray();
            var scales = bones.Select(t => t.localScale).ToArray();
            foreach (float time in new[] { 0f, clip.length * .25f, clip.length * .5f, clip.length * .75f, clip.length })
            {
                clip.SampleAnimation(gun, time);
                report.AppendLine("TIME " + time);
                for (int i = 0; i < bones.Length; i++)
                    report.AppendLine(bones[i].name + ": positionDelta=" + (bones[i].localPosition - positions[i]).ToString("F6") + " angle=" + Quaternion.Angle(bones[i].localRotation, rotations[i]) + " scale=" + bones[i].localScale.ToString("F6"));
            }
            UnityEngine.Object.DestroyImmediate(gun);
            File.WriteAllText("ar15-clip-inspect.txt", report.ToString()); EditorApplication.Exit(0);
        }
        catch (Exception e) { File.WriteAllText("ar15-clip-inspect.txt", report + e.ToString()); EditorApplication.Exit(1); }
    }
}
