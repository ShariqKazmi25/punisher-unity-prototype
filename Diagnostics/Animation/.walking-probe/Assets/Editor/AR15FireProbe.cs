using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Playables;

public static class AR15FireProbe
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Run()
    {
        var report = new StringBuilder();
        try
        {
            var settings = Resources.Load<RifleWeaponAnimationSettings>("Weapons/AR15Animation");
            var original = AssetDatabase.LoadAllAssetsAtPath("Assets/ar15.fbx").OfType<AnimationClip>().Single(c => c.name == "Armature.003|shooting");
            Assert(settings != null && settings.shootingClip == original, "Profile must reference the original imported shooting clip.");
            report.AppendLine("Original FBX clip reference: " + original.name + ", length=" + original.length + ", fps=" + original.frameRate);
            foreach (int fps in new[] { 30, 60, 144 })
            {
                var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AimFitFixture.prefab"));
                root.transform.position = Vector3.zero; root.transform.rotation = Quaternion.identity;
                var a = root.GetComponentInChildren<PunisherMovement>().animator;
                a.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Production.controller");
                a.applyRootMotion = false; a.cullingMode = AnimatorCullingMode.AlwaysAnimate; a.Rebind(); a.Update(0f);
                var ik = a.GetComponent<RifleHandIK>(); Call(ik, "Awake");
                var camera = new GameObject("Firing camera").AddComponent<Camera>(); camera.transform.position = new Vector3(.3f, .65f, -.7f); camera.transform.LookAt(new Vector3(0f, .62f, 10f));
                var shooter = root.AddComponent<PunisherShooter>(); shooter.animator = a; shooter.aimingCamera = camera; shooter.showShotEffects = false; Call(shooter, "Awake");
                var player = shooter.weaponAnimation;
                Assert(player != null && player.settings == settings, "Gun animation was not configured automatically.");
                var gun = player.transform;
                var body = gun.Find(settings.bodyBonePath);
                var bolt = body.Find("Bone.001"); var casing = body.Find("Bone.003");
                Vector3 boltRest = bolt.localPosition, caseRest = casing.localPosition, gunPosition = gun.localPosition, gunScale = gun.localScale;
                Quaternion gunRotation = gun.localRotation;
                float dt = 1f / fps, now = 0f, maxBolt = 0f, maxCase = 0f, maxGrip = 0f, maxAngle = 0f, maxFeet = 0f;
                int rounds = 0; float previousShot = -100f;
                shooter.ShotFired += _ =>
                {
                    Assert(now - previousShot >= .2f - .0001f, "Animation changed firing cadence.");
                    previousShot = now; rounds++;
                };
                Action<bool, bool> step = (walk, fire) =>
                {
                    now += dt; a.SetBool("iswalking", walk);
                    Call(shooter, "UpdateWeaponInput", fire, fire, now); Call(shooter, "WriteAnimationParameters"); Call(shooter, "UpdateLayerWeight", dt);
                    a.Update(dt);
                    var leftFoot = a.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                    var rightFoot = a.GetBoneTransform(HumanBodyBones.RightFoot).position;
                    Call(player, "BindMarkers"); Call(player, "Advance", dt); Call(ik, "LateUpdate");
                    maxFeet = Mathf.Max(maxFeet, Vector3.Distance(leftFoot, a.GetBoneTransform(HumanBodyBones.LeftFoot).position), Vector3.Distance(rightFoot, a.GetBoneTransform(HumanBodyBones.RightFoot).position));
                    maxBolt = Mathf.Max(maxBolt, Vector3.Distance(boltRest, bolt.localPosition));
                    maxCase = Mathf.Max(maxCase, Vector3.Distance(caseRest, casing.localPosition));
                    if (fire && a.GetLayerWeight(1) > .999f)
                    {
                        Vector3 offset = (Vector3)typeof(RifleHandIK).GetField("idleGripPosition", Private).GetValue(ik);
                        Vector3 targetGrip = ik.leftHandGrip.TransformPoint(offset) - ik.barrelTip.forward * ik.supportHandSlideBack;
                        maxGrip = Mathf.Max(maxGrip, Vector3.Distance(a.GetBoneTransform(HumanBodyBones.LeftHand).position, targetGrip));
                        maxAngle = Mathf.Max(maxAngle, Vector3.Angle(ik.barrelTip.forward, shooter.GetAimLookPoint() - ik.barrelTip.position));
                    }
                    Call(shooter, "FireIfReady", now);
                    Assert(Vector3.Distance(gun.localPosition, gunPosition) < .000001f && Quaternion.Angle(gun.localRotation, gunRotation) < .001f && Vector3.Distance(gun.localScale, gunScale) < .000001f, "Clip changed the fitted gun mount.");
                };
                for (int i = 0; i < fps; i++) step(false, false);
                Assert(ik.leftHandGrip.parent == body && shooter.muzzle.parent == body, "Grip and muzzle must follow the animated rifle body.");
                for (int i = 0; i < 2 * fps; i++) step(i > fps, true);
                Assert(rounds >= 9 && rounds <= 11, "Unexpected accepted shot count.");
                Assert(maxBolt > .005f && maxCase > .01f, "The imported bolt/casing animation did not play.");
                Assert(maxGrip < .002f && maxAngle < .15f && maxFeet < .00001f, "Animated recoil broke grip, crosshair alignment or feet.");
                int stoppedRounds = rounds;
                for (int i = 0; i < fps / 2; i++) step(false, false);
                Assert(rounds == stoppedRounds && !player.IsPlaying, "Release started another round/animation.");
                Assert(Vector3.Distance(boltRest, bolt.localPosition) < .000001f && Vector3.Distance(caseRest, casing.localPosition) < .000001f, "Completed shot did not restore the weapon's rest pose.");
                player.PlayShot(.1f); Call(player, "Advance", .04f);
                Assert(player.IsPlaying && Mathf.Abs(player.NormalizedTime - .4f) < .001f, "Clip did not fit a faster shot interval.");
                Call(player, "Advance", .07f); Assert(!player.IsPlaying, "Fast cycle failed to finish.");
                player.PlayShot(1f); Call(player, "Advance", .1f);
                Assert(player.IsPlaying && player.NormalizedTime < .4f, "Sparse shots were slowed/stretched.");
                Call(shooter, "OnDisable"); Assert(!player.IsPlaying && Vector3.Distance(boltRest, bolt.localPosition) < .000001f, "Disable left a weapon pose active.");
                report.AppendLine(fps + " fps: shots=" + rounds + ", bolt motion=" + maxBolt + ", casing motion=" + maxCase + ", max grip error=" + maxGrip + "m, barrel error=" + maxAngle + "deg, foot displacement=" + maxFeet + "; release/fast cycle/disable passed.");
                var ownedGraph = (PlayableGraph)typeof(RifleWeaponAnimation).GetField("graph", Private).GetValue(player);
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(camera.gameObject);
                Assert(!ownedGraph.IsValid(), "Destroying the weapon leaked its animation graph.");
            }
            File.WriteAllText("ar15-fire-results.txt", report + "PASS. Original AR15 clip drives actual weapon bones once per accepted shot; fitted gun root unchanged.\n");
            ShooterProbe.Run();
        }
        catch (Exception e) { File.WriteAllText("ar15-fire-results.txt", report + e.ToString()); EditorApplication.Exit(1); }
    }
}
