// Only run in Diagnostics/Animation/.walking-probe. All imports/controllers here are copies.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Unity.Cinemachine;

public static class ShooterProbe
{
    static readonly StringBuilder Report = new StringBuilder();
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string[] Names = { "Weapon Lowered", "Rifle Aiming Idle", "Firing Rifle2" };
    static AnimatorController fixture, original;
    static AnimatorState[] states;

    public static void Run()
    {
        try
        {
            LoadSavedController();
            CheckShooting();
            foreach (int fps in new[] { 30, 60, 144 })
            {
                CheckGraph(fps);
                CheckCamera(fps);
            }
            Report.AppendLine("PASS. Actual saved production controller, mask, imports and source scripts tested on copies. Full-scene visual tuning remains with the user.");
            File.WriteAllText("shooter-results.txt", Report.ToString());
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText("shooter-results.txt", Report + ex.ToString());
            EditorApplication.Exit(1);
        }
    }

    static void LoadSavedController()
    {
        original = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Punisher.controller");
        fixture = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Production.controller");
        Assert(fixture != null && fixture.layers.Length == 2, "Expected saved two-layer production graph.");
        Assert(fixture.layers[0].stateMachine.states.Length == 3, "Calibrated base graph was changed.");
        var layer = fixture.layers[1];
        Assert(layer.name == "Weapon Actions" && layer.iKPass && layer.avatarMask != null && layer.defaultWeight == 0f,
            "Weapon layer settings missing.");
        Assert(layer.blendingMode == AnimatorLayerBlendingMode.Override && layer.syncedLayerIndex == -1, "Weapon layer blend/sync changed.");
        var machine = layer.stateMachine;
        states = Names.Select(n => machine.states.Single(s => s.state.name == n).state).ToArray();
        Assert(machine.states.Length == 3 && machine.anyStateTransitions.Length == 0, "Expected three shared upper-body states.");
        Assert(machine.defaultState == states[0], "Neutral default missing.");
        Assert(states.Sum(s => s.transitions.Length) == 6, "Expected every direct aim/fire route.");
        foreach (var state in states)
        {
            Assert(state.motion is AnimationClip c && c.humanMotion && c.isLooping, "Weapon motion must be looping Humanoid.");
            var clip = (AnimationClip)state.motion;
            Report.AppendLine(state.name + ": duration=" + clip.length.ToString("F6") + ", loop=" + clip.isLooping);
            foreach (var t in state.transitions)
                Assert(!t.hasExitTime && t.hasFixedDuration && t.offset == 0f && !t.canTransitionToSelf &&
                    t.interruptionSource == TransitionInterruptionSource.Destination && t.orderedInterruption,
                    "Incorrect direct transition settings: " + state.name);
            for (int bits = 0; bits < 8; bits++)
            {
                bool walk = (bits & 1) != 0, aim = (bits & 2) != 0, fire = (bits & 4) != 0;
                int destination = fire ? 2 : aim ? 1 : 0;
                var matches = state.transitions.Where(t => t.conditions.All(c =>
                {
                    bool value = c.parameter == "iswalking" ? walk : c.parameter == "isaiming" ? aim : fire;
                    return value == (c.mode == AnimatorConditionMode.If);
                })).ToArray();
                Assert(state == states[destination] ? matches.Length == 0 : matches.Length == 1 && matches[0].destinationState == states[destination],
                    "Missing/ambiguous input route from " + state.name + " bits=" + bits);
            }
        }
        foreach (var clip in fixture.animationClips)
            Assert(clip.name != "Rifle Down To Aim" && clip.name != "Rifle Aim To Down", "Removed rotation-error clip still used.");
        Report.AppendLine("Saved graph: three shared states, six routes; all input combinations exclusive and complete; walking does not change upper-body states.");
    }

    static void Call(object target, string method, params object[] arguments)
        => target.GetType().GetMethod(method, Private).Invoke(target, arguments);
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }

    static void CheckShooting()
    {
        var root = new GameObject("Shooter test");
        root.AddComponent<CapsuleCollider>();
        var cameraObject = new GameObject("Test camera");
        var camera = cameraObject.AddComponent<Camera>(); camera.transform.position = new Vector3(0f, .6f, -1f);
        var muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(root.transform, false); muzzle.localPosition = new Vector3(.15f, .5f, .3f);
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube); target.transform.position = new Vector3(0f, .6f, 5f);
        var health = target.AddComponent<RifleDamageableTarget>(); health.deactivateOnDeath = false; Call(health, "OnEnable");
        var shooter = root.AddComponent<PunisherShooter>(); shooter.aimingCamera = camera; shooter.muzzle = muzzle; shooter.showShotEffects = false;
        Call(shooter, "Awake");
        int shots = 0; PunisherShooter.ShotInfo last = default; shooter.ShotFired += shot => { shots++; last = shot; };
        Physics.SyncTransforms();
        Call(shooter, "UpdateWeaponInput", false, true, 0f);
        Assert((bool)shooter.GetType().GetMethod("FireIfReady", Private).Invoke(shooter, new object[] { 0f }), "First click did not shoot.");
        Assert(last.collider == target.GetComponent<Collider>() && health.Health == 75f, "Crosshair shot missed or hit the player.");
        Call(shooter, "FireIfReady", .05f); Assert(shots == 1, "Fire cooldown ignored.");
        foreach (float time in new[] { .2f, .4f, .6f }) Call(shooter, "FireIfReady", time);
        Assert(shots == 4 && health.Health == 0f, "Damage/cadence failed.");
        Call(shooter, "UpdateWeaponInput", false, false, .65f);
        Assert(shooter.IsFiring && shooter.WeaponReady, "Tap recoil pose was cut off early.");
        Call(shooter, "UpdateWeaponInput", true, false, 1f);
        Assert(shooter.IsAiming && !shooter.IsFiring && shooter.WeaponReady, "Aim-only state failed.");

        var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cover.transform.position = new Vector3(.14f, .5f, .7f); cover.transform.localScale = new Vector3(.12f, .3f, .05f);
        Physics.SyncTransforms(); Call(shooter, "UpdateWeaponInput", false, true, 1f); Call(shooter, "FireIfReady", 1f);
        Assert(last.collider == cover.GetComponent<Collider>(), "Muzzle shot passed through cover missed by the camera ray.");
        cover.transform.position = new Vector3(.075f, .475f, .15f); cover.transform.localScale = new Vector3(.05f, .1f, .025f);
        Physics.SyncTransforms(); Call(shooter, "FireIfReady", 1.2f);
        Assert(last.collider == cover.GetComponent<Collider>(), "Barrel poking through cover bypassed the wall.");
        Call(shooter, "ClearInput"); Call(shooter, "FireIfReady", 2f); Assert(shots == 6, "Released/unfocused input still fired.");
        Report.AppendLine("Hitscan: centre ray, self-ignore, 5 rounds/sec cooldown, damage, recoil hold, aim-only, muzzle cover and barrel-through-cover passed.");
        UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(cameraObject);
        UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(cover);
    }

    static void CheckGraph(int fps)
    {
        var root = new GameObject("Shooter graph test");
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/punisher_vest@Rifle Idle.fbx"), root.transform);
        var a = model.GetComponent<Animator>(); a.runtimeAnimatorController = fixture; a.applyRootMotion = false;
        var shooter = root.AddComponent<PunisherShooter>(); shooter.animator = a; shooter.showShotEffects = false; Call(shooter, "Awake");
        a.Rebind(); a.Update(0f);
        float now = 0f, dt = 1f / fps;
        Action<bool, bool, bool, float> step = (walk, aim, fire, duration) =>
        {
            for (int i = 0; i < Mathf.CeilToInt(duration * fps); i++)
            {
                now += dt; a.SetBool("iswalking", walk);
                Call(shooter, "UpdateWeaponInput", aim, fire, now);
                Call(shooter, "WriteAnimationParameters"); Call(shooter, "UpdateLayerWeight", dt); a.Update(dt);
            }
        };
        Action<string> state = name => Assert(a.GetCurrentAnimatorStateInfo(1).IsName(name), "Expected upper state " + name + " at " + fps + " fps.");
        step(false, false, false, .2f); state("Weapon Lowered");
        step(false, true, false, 1.2f); state("Rifle Aiming Idle");
        step(true, true, false, 1.5f); state("Rifle Aiming Idle");
        step(true, false, true, .5f); state("Firing Rifle2");
        float oldTime = a.GetCurrentAnimatorStateInfo(1).normalizedTime;
        step(true, false, true, 2f); Assert(a.GetCurrentAnimatorStateInfo(1).normalizedTime > oldTime + 1f, "Held firing repeatedly restarted instead of looping.");
        step(false, false, true, .2f); state("Firing Rifle2");
        step(false, true, false, .3f); state("Rifle Aiming Idle");
        step(false, false, false, .2f); state("Weapon Lowered");
        Assert(a.GetLayerWeight(1) < .001f, "Direct aim release failed to fade the layer.");
        step(false, true, false, 1.1f); state("Rifle Aiming Idle");
        step(false, false, false, 1.6f); state("Weapon Lowered");
        Assert(a.GetLayerWeight(1) < .001f, "Weapon layer stayed active after release.");
        step(true, false, true, .15f); state("Firing Rifle2");
        step(false, false, false, 1.6f); state("Weapon Lowered");
        // Every state pair through the runtime parameter writer.
        bool[] walking = { false, false, false };
        bool[] aiming = { false, true, false };
        bool[] firing = { false, false, true };
        for (int from = 0; from < 3; from++)
            for (int to = 0; to < 3; to++)
            {
                step(walking[from], aiming[from], firing[from], .25f); state(Names[from]);
                step(walking[to], aiming[to], firing[to], .25f); state(Names[to]);
                step(true, aiming[to], firing[to], .25f); state(Names[to]);
            }
        step(false, false, false, .25f);
        for (int i = 0; i < 18; i++) step(walking[i % 3], aiming[i % 3], firing[i % 3], dt);
        step(true, true, false, .25f); state("Rifle Aiming Idle");
        step(false, false, false, .25f); state("Weapon Lowered");
        Assert(a.GetLayerWeight(1) < .001f, "Rapid interrupts left layer active.");
        // Both controllers get identical walking input; upper-body poses must not change leg muscles.
        var reference = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/punisher_vest@Rifle Idle.fbx"));
        var b = reference.GetComponent<Animator>(); b.runtimeAnimatorController = original; b.applyRootMotion = false;
        a.Rebind(); a.Update(0f); b.Rebind(); b.Update(0f);
        float maxLegDifference = 0f, maxFootDifference = 0f;
        var bones = new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg };
        for (int frame = 0; frame < 4 * fps; frame++)
        {
            bool fire = frame > fps; now += dt;
            a.SetBool("iswalking", true); b.SetBool("iswalking", true);
            Call(shooter, "UpdateWeaponInput", true, fire, now); Call(shooter, "WriteAnimationParameters"); Call(shooter, "UpdateLayerWeight", dt);
            a.Update(dt); b.Update(dt);
            foreach (var bone in bones) maxLegDifference = Mathf.Max(maxLegDifference,
                Quaternion.Angle(a.GetBoneTransform(bone).localRotation, b.GetBoneTransform(bone).localRotation));
            foreach (var foot in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
                maxFootDifference = Mathf.Max(maxFootDifference, Vector3.Distance(model.transform.InverseTransformPoint(a.GetBoneTransform(foot).position),
                    reference.transform.InverseTransformPoint(b.GetBoneTransform(foot).position)));
        }
        Report.AppendLine(fps + " fps: graph states/interrupts/held-fire loops passed; masked leg angle difference=" + maxLegDifference + ", foot position difference=" + maxFootDifference);
        Assert(maxLegDifference < .1f && maxFootDifference < .01f, "Upper-body layer altered calibrated walking feet.");
        UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(reference);
    }

    static void CheckCamera(int fps)
    {
        var root = new GameObject("Camera player");
        var pivot = new GameObject("CameraTarget"); pivot.transform.SetParent(root.transform, false);
        var virtualObject = new GameObject("Virtual camera");
        var virtualCamera = virtualObject.AddComponent<CinemachineCamera>();
        var follow = virtualObject.AddComponent<CinemachineThirdPersonFollow>(); follow.CameraDistance = .5f;
        var lens = virtualCamera.Lens; lens.FieldOfView = 60f; lens.NearClipPlane = .3f; virtualCamera.Lens = lens;
        var camera = pivot.AddComponent<ThirdPersonCameraController>(); camera.characterRoot = root.transform;
        camera.virtualCamera = virtualCamera; camera.shoulderFollow = follow; camera.captureCursorOnEnable = false;
        Call(camera, "OnEnable");
        for (int frame = 0; frame < fps; frame++) Call(camera, "ApplyLook", new Vector2(120f / fps, 0f), false, true, 1f / fps);
        Assert(Mathf.Abs(Mathf.DeltaAngle(0f, camera.LookRotation.eulerAngles.y) - 12f) < .001f, "Mouse sensitivity varies with fps.");
        for (int frame = 0; frame < fps; frame++) Call(camera, "ApplyLook", Vector2.zero, false, true, 1f / fps);
        Assert(Mathf.Abs(Mathf.DeltaAngle(root.transform.eulerAngles.y, camera.LookRotation.eulerAngles.y)) < .01f, "Idle character did not turn with camera.");
        Call(camera, "UpdateAimLens", true, .3f);
        Assert(Mathf.Abs(follow.CameraDistance - .3f) < .0001f && Mathf.Abs(virtualCamera.Lens.FieldOfView - 45f) < .0001f, "Shoulder aim zoom failed.");
        Assert(Mathf.Abs(virtualCamera.Lens.NearClipPlane - .05f) < .0001f, "Close shoulder aim retained the long near clip.");
        Call(camera, "UpdateAimLens", false, .3f);
        Assert(Mathf.Abs(follow.CameraDistance - .5f) < .0001f && Mathf.Abs(virtualCamera.Lens.FieldOfView - 60f) < .0001f, "Aim release did not restore lens.");
        Assert(Mathf.Abs(virtualCamera.Lens.NearClipPlane - .3f) < .0001f, "Aim release did not restore near clip.");
        Report.AppendLine(fps + " fps: camera-facing idle turn, mouse sensitivity, shoulder aim/restore passed.");
        UnityEngine.Object.DestroyImmediate(pivot); UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(virtualObject);
    }
}
