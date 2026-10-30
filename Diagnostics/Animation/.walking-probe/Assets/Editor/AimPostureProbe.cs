using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AimPostureProbe
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    public static void Render()
    {
        var report = new StringBuilder();
        try
        {
            var camera = new GameObject("Aim camera").AddComponent<Camera>(); camera.transform.position = new Vector3(.3f, .65f, -.7f); camera.transform.LookAt(new Vector3(0f, .62f, 10f));
            foreach (string variant in new[] { "before", "025", "040" })
            {
                var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AimFitFixture.prefab"));
                root.transform.position = Vector3.zero; root.transform.rotation = Quaternion.identity;
                var a = root.GetComponentInChildren<PunisherMovement>().animator;
                a.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Production.controller");
                a.applyRootMotion = false; a.cullingMode = AnimatorCullingMode.AlwaysAnimate; a.Rebind(); a.Update(0f);
                var ik = a.GetComponent<RifleHandIK>(); Call(ik, "Awake");
                Component solver = ik;
                if (variant == "before")
                {
                    ik.enabled = false;
                    var old = a.gameObject.AddComponent<RifleHandIKBeforePostureFix>();
                    old.leftHandGrip = ik.leftHandGrip; old.positionWeight = ik.positionWeight; old.rotationWeight = ik.rotationWeight;
                    Call(old, "Awake"); solver = old;
                }
                if (variant == "040") ik.supportHandSlideBack = .04f;
                var shooter = root.AddComponent<PunisherShooter>(); shooter.animator = a; shooter.aimingCamera = camera; shooter.showShotEffects = false; Call(shooter, "Awake");
                for (int frame = 0; frame < 180; frame++)
                {
                    Call(shooter, "UpdateWeaponInput", frame >= 60, false, frame / 60f);
                    Call(shooter, "WriteAnimationParameters"); Call(shooter, "UpdateLayerWeight", 1f / 60f);
                    a.Update(1f / 60f); Call(shooter.weaponAnimation, "BindMarkers"); Call(solver, "LateUpdate");
                }
                report.AppendLine(variant + ": right angle=" + Angle(a, true) + "; left=" + Angle(a, false));
                if (variant != "before") report.AppendLine("stock gap=" + Vector3.Distance(ik.stockContact.position, (Vector3)typeof(RifleHandIK).GetMethod("ShoulderPosition", Private).Invoke(ik,null)));
                RenderPose(root, "posture-" + variant);
                UnityEngine.Object.DestroyImmediate(root);
            }
            File.WriteAllText("posture-render-results.txt", report.ToString()); UnityEngine.Object.DestroyImmediate(camera.gameObject); EditorApplication.Exit(0);
        }
        catch(Exception e) { File.WriteAllText("posture-render-results.txt", report + e.ToString()); EditorApplication.Exit(1); }
    }
    static void RenderPose(GameObject root, string name)
    {
        var gun = root.GetComponentsInChildren<Transform>().Single(t => t.name == "ar15");
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            var mat = new Material(Shader.Find("Standard")); mat.color = r.transform.IsChildOf(gun) ? new Color(.25f, .55f, .7f) : new Color(.45f, .4f, .35f);
            r.sharedMaterials = r.sharedMaterials.Select(_ => mat).ToArray();
        }
        var light = new GameObject("Render light").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(40f, -30f, 0f); RenderSettings.ambientLight = Color.white * .6f;
        var cam = new GameObject("Pose camera").AddComponent<Camera>(); cam.orthographic = true; cam.orthographicSize = .3f; cam.backgroundColor = new Color(.15f,.17f,.2f); cam.clearFlags = CameraClearFlags.SolidColor;
        foreach (bool side in new[] { false, true })
        {
            cam.transform.position = side ? new Vector3(.9f,.67f,.05f) : new Vector3(.65f,.82f,.8f); cam.transform.LookAt(new Vector3(0f,.57f,.13f));
            var rt = new RenderTexture(1000,1000,24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            var image = new Texture2D(1000,1000,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1000,1000),0,0); image.Apply(); File.WriteAllBytes(name + (side ? "-side.png" : ".png"),image.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image);
        }
        UnityEngine.Object.DestroyImmediate(cam.gameObject); UnityEngine.Object.DestroyImmediate(light.gameObject);
    }
    public static void Inspect()
    {
        var report = new StringBuilder();
        try
        {
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AimFitFixture.prefab"));
            root.transform.position = Vector3.zero; root.transform.rotation = Quaternion.identity;
            var a = root.GetComponentInChildren<PunisherMovement>().animator;
            a.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Production.controller");
            a.applyRootMotion = false; a.cullingMode = AnimatorCullingMode.AlwaysAnimate; a.Rebind(); a.Update(0f);
            var ik = a.GetComponent<RifleHandIK>(); Call(ik, "Awake");
            var camera = new GameObject("Aim target camera").AddComponent<Camera>(); camera.transform.position = new Vector3(.3f, .65f, -.7f); camera.transform.LookAt(new Vector3(0f, .62f, 10f));
            var shooter = root.AddComponent<PunisherShooter>(); shooter.animator = a; shooter.aimingCamera = camera; shooter.showShotEffects = false; Call(shooter, "Awake");
            for (int frame = 0; frame < 180; frame++)
            {
                Call(shooter, "UpdateWeaponInput", frame >= 60, false, frame / 60f);
                Call(shooter, "WriteAnimationParameters"); Call(shooter, "UpdateLayerWeight", 1f / 60f);
                a.Update(1f / 60f); Call(ik, "LateUpdate");
            }
            foreach (var bone in new[] { HumanBodyBones.Hips, HumanBodyBones.Chest, HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.Head })
                report.AppendLine(bone + " world=" + a.GetBoneTransform(bone).position.ToString("F6"));
            report.AppendLine("Model worldScale=" + a.transform.lossyScale + "; model localScale=" + a.transform.localScale);
            var gun = ik.weaponTransform;
            foreach (var renderer in gun.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh(); renderer.BakeMesh(mesh, true);
                var points = mesh.vertices.Select(v => gun.InverseTransformPoint(renderer.transform.TransformPoint(v))).ToArray();
                float back = points.Max(v => v.z);
                var pad = points.Where(v => v.z > back - .12f).ToArray();
                report.AppendLine("Stock z=" + back + " count=" + pad.Length + " centre=" + (pad.Aggregate(Vector3.zero, (s,v) => s + v) / pad.Length).ToString("F6"));
                report.AppendLine("Stock y=" + pad.Min(p => p.y) + ".." + pad.Max(p => p.y));
                foreach (float fraction in new[] { 0f, .5f, 1f })
                {
                    Vector3 local = new Vector3(0f, Mathf.Lerp(pad.Min(p=>p.y),pad.Max(p=>p.y), fraction), back);
                    report.AppendLine("Stock point " + fraction + ": local=" + local.ToString("F6") + " world=" + gun.TransformPoint(local).ToString("F6"));
                }
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            report.AppendLine("Right elbow interior angle=" + Angle(a, true) + "; left=" + Angle(a, false));
            File.WriteAllText("aim-posture-inspect.txt", report.ToString());
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(camera.gameObject); EditorApplication.Exit(0);
        }
        catch(Exception e) { File.WriteAllText("aim-posture-inspect.txt", report + e.ToString()); EditorApplication.Exit(1); }
    }
    static float Angle(Animator a, bool right)
    {
        Vector3 u = a.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm).position;
        Vector3 e = a.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm).position;
        Vector3 h = a.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand).position;
        return Vector3.Angle(u-e,h-e);
    }
}
