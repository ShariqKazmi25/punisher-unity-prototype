using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AimFitProbe
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static float InteriorAngle(Animator a, bool right)
    {
        Vector3 upper = a.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm).position;
        Vector3 elbow = a.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm).position;
        Vector3 hand = a.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand).position;
        return Vector3.Angle(upper - elbow, hand - elbow);
    }
    public static void Run()
    {
        var report = new StringBuilder();
        try
        {
            foreach (int fps in new[] { 30, 60, 144 })
            {
                var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AimFitFixture.prefab"));
                root.transform.position = Vector3.zero; root.transform.rotation = Quaternion.identity;
                var a = root.GetComponentInChildren<PunisherMovement>().animator;
                a.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Production.controller");
                a.cullingMode = AnimatorCullingMode.AlwaysAnimate; a.applyRootMotion = false; a.Rebind(); a.Update(0f);
                var ik = a.GetComponent<RifleHandIK>(); Call(ik, "Awake");
                var reference = UnityEngine.Object.Instantiate(root);
                var b = reference.GetComponentInChildren<Animator>(); b.Rebind(); b.Update(0f);
                var unused = b.GetComponent<RifleHandIK>(); unused.enabled = false;
                var before = b.gameObject.AddComponent<RifleHandIKBeforeAimFix>();
                before.leftHandGrip = unused.leftHandGrip; before.positionWeight = ik.positionWeight;
                before.rotationWeight = ik.rotationWeight; Call(before, "Awake");
                var camera = new GameObject("Aim camera").AddComponent<Camera>();
                camera.transform.position = new Vector3(.35f, .65f, -.7f);
                camera.transform.LookAt(new Vector3(0f, .62f, 10f));
                var shooter = root.AddComponent<PunisherShooter>(); shooter.aimingCamera = camera; shooter.animator = a; shooter.showShotEffects = false; Call(shooter, "Awake");
                var oldShooter = reference.AddComponent<PunisherShooter>(); oldShooter.aimingCamera = camera; oldShooter.animator = b; oldShooter.showShotEffects = false; Call(oldShooter, "Awake");
                foreach (var c in root.GetComponentsInChildren<Collider>().Concat(reference.GetComponentsInChildren<Collider>())) c.enabled = false;
                float dt = 1f / fps, now = 0f, idleDifference = 0f;
                Action<bool, bool, bool> step = (walk, aim, fire) =>
                {
                    now += dt; a.SetBool("iswalking", walk); b.SetBool("iswalking", walk);
                    foreach (var s in new[] { shooter, oldShooter })
                    {
                        Call(s, "UpdateWeaponInput", aim, fire, now); Call(s, "WriteAnimationParameters"); Call(s, "UpdateLayerWeight", dt);
                    }
                    a.Update(dt); b.Update(dt); Call(ik, "LateUpdate");
                };
                for (int i = 0; i < fps; i++)
                {
                    step(false, false, false);
                    idleDifference = Mathf.Max(idleDifference, Vector3.Distance(a.GetBoneTransform(HumanBodyBones.LeftHand).position, b.GetBoneTransform(HumanBodyBones.LeftHand).position));
                }
                report.AppendLine(fps + " fps idle wrist difference=" + idleDifference);
                Assert(idleDifference < .00001f, "Idle fit changed.");
                var gun = ik.weaponTransform;
                if (fps == 30)
                {
                    foreach (var t in gun.GetComponentsInChildren<Transform>()) report.AppendLine("Gun transform " + t.name + " localScale=" + t.localScale + " lossy=" + t.lossyScale);
                    foreach (var renderer in gun.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        report.AppendLine("renderer worldbounds=" + renderer.bounds + " meshbounds=" + renderer.sharedMesh.bounds);
                        foreach (bool scale in new[] { false, true })
                        {
                            var mesh = new Mesh(); renderer.BakeMesh(mesh, scale);
                            var points = mesh.vertices.Select(v => gun.InverseTransformPoint(renderer.transform.TransformPoint(v))).ToArray();
                            float z = points.Min(v => v.z); var front = points.Where(v => v.z < z + .01f).ToArray();
                            report.AppendLine("BAKE scale=" + scale + " muzzle=" + (front.Aggregate(Vector3.zero, (sum, v) => sum + v) / front.Length).ToString("F8"));
                            UnityEngine.Object.DestroyImmediate(mesh);
                        }
                    }
                }
                float maxGrip = 0f, maxBarrel = 0f, maxHead = 0f, oldGrip = 0f, oldBarrel = 0f, footChange = 0f;
                float leftAngleMax = 0f, rightAngleMin = 180f, stockGapMax = 0f, faceClearanceMin = 100f;
                var feet = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
                for (int frame = 0; frame < 7 * fps; frame++)
                {
                    bool walking = frame >= 2 * fps && frame < 6 * fps;
                    bool fire = frame >= 4 * fps;
                    step(walking, true, fire);
                    if (frame < fps / 2) continue;
                    Vector3 offset = (Vector3)typeof(RifleHandIK).GetField("idleGripPosition", Private).GetValue(ik);
                    var goal = ik.leftHandGrip.TransformPoint(offset) - ik.barrelTip.forward * ik.supportHandSlideBack;
                    maxGrip = Mathf.Max(maxGrip, Vector3.Distance(a.GetBoneTransform(HumanBodyBones.LeftHand).position, goal));
                    Vector3 target = shooter.GetAimLookPoint();
                    maxBarrel = Mathf.Max(maxBarrel, Vector3.Angle(ik.barrelTip.forward, target - ik.barrelTip.position));
                    maxHead = Mathf.Max(maxHead, Vector3.Angle(a.GetBoneTransform(HumanBodyBones.Head).TransformDirection(ik.localHeadForward), target - a.GetBoneTransform(HumanBodyBones.Head).position));
                    if (!walking)
                    {
                        leftAngleMax = Mathf.Max(leftAngleMax, InteriorAngle(a, false));
                        rightAngleMin = Mathf.Min(rightAngleMin, InteriorAngle(a, true));
                        Vector3 pocket = ik.shoulderAnchor != null ? ik.shoulderAnchor.position : a.GetBoneTransform(HumanBodyBones.RightUpperArm).position + ik.transform.TransformVector(ik.shoulderOffset);
                        stockGapMax = Mathf.Max(stockGapMax, Vector3.Distance(ik.stockContact.position, pocket));
                        foreach (float edge in new[] { -.50909f, .50909f })
                            faceClearanceMin = Mathf.Min(faceClearanceMin, Vector3.Distance(a.GetBoneTransform(HumanBodyBones.Head).position,
                                ik.stockContact.position + gun.TransformVector(Vector3.up * edge)));
                    }
                    oldGrip = Mathf.Max(oldGrip, Vector3.Distance(b.GetBoneTransform(HumanBodyBones.LeftHand).position, before.leftHandGrip.TransformPoint(offset)));
                    oldBarrel = Mathf.Max(oldBarrel, Vector3.Angle(before.leftHandGrip.parent.TransformDirection(Vector3.back), target - before.leftHandGrip.parent.position));
                    var footPositions = feet.Select(f => a.GetBoneTransform(f).position).ToArray();
                    Call(ik, "LateUpdate");
                    for (int f = 0; f < 2; f++) footChange = Mathf.Max(footChange, Vector3.Distance(footPositions[f], a.GetBoneTransform(feet[f]).position));
                    if (fps == 60 && frame == 3 * fps && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    {
                        RenderPose(root, reference, "aim-after.png");
                        RenderPose(reference, root, "aim-before.png");
                    }
                }
                report.AppendLine(fps + " fps aiming: max grip error=" + maxGrip + "m; barrel angle=" + maxBarrel + "; head angle=" + maxHead + "; old grip=" + oldGrip + "m; old barrel=" + oldBarrel + "; late foot change=" + footChange);
                Assert(maxGrip < .002f, "Support hand could not reach the preserved idle grip.");
                Assert(maxBarrel < .1f && maxHead < .1f, "Barrel/face failed to align to the crosshair.");
                Assert(footChange < .00001f, "Aim correction moved the feet.");
                report.AppendLine(fps + " fps posture: left elbow max=" + leftAngleMax + ", right min=" + rightAngleMin + ", stock gap max=" + stockGapMax + "m, head-to-stock edge clearance min=" + faceClearanceMin + "m");
                Assert(leftAngleMax < 148f && rightAngleMin > 87f, "Aiming elbows remained locked/tightly folded.");
                Assert(stockGapMax < .025f && faceClearanceMin > .045f, "Stock failed to stay at the shoulder and away from the face.");
                foreach (float pitch in new[] { -70f, -30f, 30f, 70f })
                {
                    camera.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
                    for (int frame = 0; frame < fps / 2; frame++) step(false, true, false);
                    Vector3 point = shooter.GetAimLookPoint();
                    Vector3 offset = (Vector3)typeof(RifleHandIK).GetField("idleGripPosition", Private).GetValue(ik);
                    float error = Vector3.Distance(a.GetBoneTransform(HumanBodyBones.LeftHand).position, ik.leftHandGrip.TransformPoint(offset) - ik.barrelTip.forward * ik.supportHandSlideBack);
                    float angle = Vector3.Angle(ik.barrelTip.forward, point - ik.barrelTip.position);
                    report.AppendLine(fps + " fps pitch=" + pitch + ": grip=" + error + "m, barrel=" + angle);
                    Assert(error < .002f && angle < .15f, "Vertical aim failed.");
                }
                var nearSurface = GameObject.CreatePrimitive(PrimitiveType.Cube); nearSurface.transform.position = new Vector3(0f, .62f, 1f); nearSurface.transform.localScale = Vector3.one * .2f;
                camera.transform.LookAt(nearSurface.transform.position); Physics.SyncTransforms();
                for (int frame = 0; frame < fps / 2; frame++) step(false, true, false);
                Vector3 near = shooter.GetAimLookPoint();
                float nearAngle = Vector3.Angle(ik.barrelTip.forward, near - ik.barrelTip.position);
                report.AppendLine(fps + " fps near surface target=" + near.ToString("F4") + ": barrel=" + nearAngle);
                Assert(near.z < 1f && nearAngle < .15f, "Close target parallax failed.");
                UnityEngine.Object.DestroyImmediate(nearSurface);
                Call(ik, "CreateAimPoseTargets");
                Assert(ik.shoulderAnchor != null && ik.leftAimElbowHint != null && ik.rightElbowHint != null, "Manual pose targets were not created.");
                Assert(ik.leftElbowHint == null && !ik.rightElbowHint.IsChildOf(a.GetBoneTransform(HumanBodyBones.RightUpperArm)) &&
                    !ik.leftAimElbowHint.IsChildOf(a.GetBoneTransform(HumanBodyBones.LeftUpperArm)), "Manual aiming targets changed idle IK or were parented into an arm.");
                for (int frame = 0; frame < fps / 2; frame++) step(false, true, false);
                Vector3 previousElbow = a.GetBoneTransform(HumanBodyBones.RightLowerArm).position;
                ik.rightElbowHint.position += ik.transform.right * .06f;
                for (int frame = 0; frame < fps / 2; frame++) step(false, true, false);
                float manualElbowChange = Vector3.Distance(previousElbow, a.GetBoneTransform(HumanBodyBones.RightLowerArm).position);
                report.AppendLine(fps + " fps manual right elbow target: elbow moved=" + manualElbowChange + "m; aiming-only target creation passed.");
                Assert(manualElbowChange > .001f, "Moving the manual elbow target did not change the elbow pose.");
                for (int frame = 0; frame < fps / 2; frame++) step(false, false, false);
                Assert(a.GetLayerWeight(1) < .00001f, "Release did not restore the base pose.");
                ik.leftHandGrip = null; ik.weaponTransform = null; step(false, true, false);
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(reference); UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
            File.WriteAllText("aim-fit-results.txt", report + "PASS. Actual live-fitted character and AR15 copies; standing/moving aim, both firing poses, release and missing-target checks.\n");
            ShooterProbe.Run();
        }
        catch (Exception e) { File.WriteAllText("aim-fit-results.txt", report + e.ToString()); EditorApplication.Exit(1); }
    }
    static void RenderPose(GameObject visible, GameObject hidden, string file)
    {
        foreach (var r in hidden.GetComponentsInChildren<Renderer>()) r.enabled = false;
        var gun = visible.GetComponentsInChildren<Transform>().Single(t => t.name == "ar15");
        foreach (var r in visible.GetComponentsInChildren<Renderer>())
        {
            r.enabled = true;
            var mat = new Material(Shader.Find("Standard")); mat.color = r.transform.IsChildOf(gun) ? new Color(.25f, .55f, .7f) : new Color(.45f, .4f, .35f);
            r.sharedMaterials = r.sharedMaterials.Select(_ => mat).ToArray();
        }
        var light = new GameObject("Render light").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
        RenderSettings.ambientLight = Color.white * .6f;
        var cam = new GameObject("Pose camera").AddComponent<Camera>();
        cam.transform.position = new Vector3(.65f, .82f, .8f); cam.transform.LookAt(new Vector3(0f, .55f, .13f));
        cam.orthographic = true; cam.orthographicSize = .38f; cam.backgroundColor = new Color(.15f, .17f, .2f); cam.clearFlags = CameraClearFlags.SolidColor;
        var rt = new RenderTexture(1000, 1000, 24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var image = new Texture2D(1000, 1000, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0); image.Apply(); File.WriteAllBytes(file, image.EncodeToPNG());
        cam.targetTexture = null; RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(cam.gameObject); UnityEngine.Object.DestroyImmediate(light.gameObject);
        foreach (var r in hidden.GetComponentsInChildren<Renderer>()) r.enabled = true;
    }
    public static void RenderAsset()
    {
        try
        {
            var gun = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ar15.fbx"));
            gun.transform.localScale = Vector3.one * .03f;
            var material = new Material(Shader.Find("Standard")); material.color = new Color(.5f, .6f, .65f);
            foreach (var renderer in gun.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
            var camera = new GameObject("Inspect camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(1f, .2f, 0f); camera.transform.LookAt(new Vector3(0f, 0f, 0f), Vector3.up);
            camera.orthographic = true; camera.orthographicSize = .38f;
            camera.backgroundColor = Color.gray; camera.clearFlags = CameraClearFlags.SolidColor;
            var light = new GameObject("Inspect light").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(40f, -50f, 0f);
            RenderSettings.ambientLight = Color.white * .65f;
            var rt = new RenderTexture(1200, 700, 24); camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(1200, 700, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1200, 700), 0, 0); image.Apply();
            File.WriteAllBytes("ar15-side.png", image.EncodeToPNG());
            EditorApplication.Exit(0);
        }
        catch (Exception e) { File.WriteAllText("aim-render-error.txt", e.ToString()); EditorApplication.Exit(1); }
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
            a.applyRootMotion = false; a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var gun = root.GetComponentsInChildren<Transform>().Single(t => t.name == "ar15");
            var ik = a.GetComponent<RifleHandIK>(); ik.SendMessage("Awake");
            report.AppendLine("GUN ANIMATORS: " + string.Join(", ", gun.GetComponentsInChildren<Animator>().Select(x => x.name + ":" + x.runtimeAnimatorController)));
            foreach (var mesh in gun.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked = new Mesh(); mesh.BakeMesh(baked);
                var vertices = baked.vertices.Select(v => gun.InverseTransformPoint(mesh.transform.TransformPoint(v))).ToArray();
                report.AppendLine("Mesh " + mesh.name + " verts=" + vertices.Length);
                for (int axis = 0; axis < 3; axis++)
                {
                    float min = vertices.Min(v => v[axis]), max = vertices.Max(v => v[axis]);
                    report.AppendLine("Axis " + axis + ": " + min + " .. " + max);
                    for (int bin = 0; bin < 8; bin++)
                    {
                        var slice = vertices.Where(v => v[axis] >= Mathf.Lerp(min, max, bin / 8f) && v[axis] <= Mathf.Lerp(min, max, (bin + 1) / 8f)).ToArray();
                        report.AppendLine("  slice" + bin + " n=" + slice.Length + " centre=" + (slice.Aggregate(Vector3.zero, (sum, v) => sum + v) / Mathf.Max(1, slice.Length)).ToString("F4"));
                    }
                }
                UnityEngine.Object.DestroyImmediate(baked);
            }
            foreach (string state in new[] { "Weapon Lowered", "Rifle Aiming Idle", "Walking", "Firing Rifle", "Firing Rifle2" })
            {
                a.Rebind(); a.Update(0f); a.SetLayerWeight(1, state == "Weapon Lowered" ? 0f : 1f);
                a.Play(state, 1, .2f); a.Update(0f);
                var left = a.GetBoneTransform(HumanBodyBones.LeftHand);
                report.AppendLine(state + ": left=" + left.position.ToString("F4") + " grip=" + ik.leftHandGrip.position.ToString("F4") + " wristOffsetInGrip=" + ik.leftHandGrip.InverseTransformPoint(left.position).ToString("F6"));
                foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
                    report.AppendLine("  gun local " + axis + " world=" + gun.TransformDirection(axis).ToString("F4"));
                report.AppendLine("  head worldRot=" + a.GetBoneTransform(HumanBodyBones.Head).rotation.ToString("F6"));
            }
            File.WriteAllText("aim-fit-inspect.txt", report.ToString());
            UnityEngine.Object.DestroyImmediate(root); EditorApplication.Exit(0);
        }
        catch (Exception e) { File.WriteAllText("aim-fit-inspect.txt", report + e.ToString()); EditorApplication.Exit(1); }
    }
}
