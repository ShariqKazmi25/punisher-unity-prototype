// Isolated-project test: real three-state controller and copied clips.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class ThreeStateMovementProbe
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly StringBuilder Report = new StringBuilder();
    static AnimatorController controller;

    class Actor
    {
        public GameObject root;
        public Animator animator;
        public Component movement;
        public Type type;
        public MethodInfo input, advance;
        public FieldInfo speed;
        public Transform[] feet;
        public Vector3[] local, world;
        public float CurrentSpeed => (float)speed.GetValue(movement);
        public void Step(bool held, float dt)
        {
            object state = Enum.Parse(type.GetNestedType("MovementState"), held ? "Walking" : "Idle");
            input.Invoke(movement, new[] { state });
            advance.Invoke(movement, new object[] { state, dt });
            root.transform.position += Vector3.forward * CurrentSpeed * dt;
            animator.Update(dt);
        }
        public void ResetFeet()
        {
            local = feet.Select(f => root.transform.InverseTransformPoint(f.position)).ToArray();
            world = feet.Select(f => f.position).ToArray();
        }
        public void MeasureFeet(float dt, List<float> slip)
        {
            for (int i = 0; i < feet.Length; i++)
            {
                var p = root.transform.InverseTransformPoint(feet[i].position);
                float backward = -(p.z - local[i].z) / dt;
                if (p.y < .072f && backward > .05f)
                    slip.Add(Mathf.Abs(feet[i].position.z - world[i].z) / dt);
                local[i] = p; world[i] = feet[i].position;
            }
        }
        public void Destroy() => UnityEngine.Object.DestroyImmediate(root);
    }

    public static void Run()
    {
        try
        {
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Punisher.controller");
            Assert(controller.layers[0].stateMachine.states.Length == 3, "Expected the current three-state graph.");
            foreach (int fps in new[] { 30, 60, 144 })
            {
                float dt = 1f / fps;
                var oldSlip = new List<float>(); var newSlip = new List<float>();
                var newStopTimes = new List<float>(); var newDistances = new List<float>();
                float maxStartDifference = 0f;
                for (int phase = 0; phase < 8; phase++)
                {
                    var before = Make(typeof(PunisherMovementBeforeStopSync));
                    var after = Make(typeof(PunisherMovement));
                    int frames = Mathf.CeilToInt((4f + phase * 1.016667f / 8f) * fps);
                    for (int frame = 0; frame < frames; frame++)
                    {
                        before.Step(true, dt); after.Step(true, dt);
                        maxStartDifference = Mathf.Max(maxStartDifference, Mathf.Abs(before.CurrentSpeed - after.CurrentSpeed));
                    }
                    Assert(after.animator.GetCurrentAnimatorStateInfo(0).IsName("Rifle Walk"), "Direct start did not reach Walk.");
                    Assert(Mathf.Abs(after.CurrentSpeed - .6222f) < .00001f, "Calibrated walking speed changed.");
                    before.ResetFeet(); after.ResetFeet();
                    float previous = after.CurrentSpeed, stoppedAt = -1f;
                    float startZ = after.root.transform.position.z;
                    for (int frame = 0; frame < Mathf.CeilToInt(3.5f * fps); frame++)
                    {
                        before.Step(false, dt); after.Step(false, dt);
                        before.MeasureFeet(dt, oldSlip); after.MeasureFeet(dt, newSlip);
                        Assert(after.CurrentSpeed >= 0f && after.CurrentSpeed <= previous + .000001f, "Release accelerates or reverses movement.");
                        previous = after.CurrentSpeed;
                        if (after.CurrentSpeed <= .00001f && stoppedAt < 0f) stoppedAt = (frame + 1) * dt;
                        if (frame == Mathf.CeilToInt(.2f * fps)) Assert(after.CurrentSpeed > .4f, "Body stopped while final steps were still underway.");
                    }
                    Assert(stoppedAt > .8f && stoppedAt < 1.5f, "Stop did not match measured clip phase: " + stoppedAt);
                    Assert(after.animator.GetCurrentAnimatorStateInfo(0).IsName("Rifle Idle"), "Did not finish in Idle.");
                    Assert(after.CurrentSpeed == 0f, "Settling/Idle continues to move.");
                    newStopTimes.Add(stoppedAt); newDistances.Add(after.root.transform.position.z - startZ);
                    before.Destroy(); after.Destroy();
                }
                Assert(maxStartDifference < .000001f, "Direct-start/steady speed changed.");
                float oldMean = oldSlip.Average(), newMean = newSlip.Average();
                Report.AppendLine(fps + " Hz, 8 release phases: start/steady max speed difference=" + maxStartDifference +
                    "; planted-foot mean drift=" + oldMean.ToString("F5") + " -> " + newMean.ToString("F5") +
                    " m/s; stop=" + newStopTimes.Min().ToString("F4") + ".." + newStopTimes.Max().ToString("F4") +
                    "s; travel=" + newDistances.Min().ToString("F4") + ".." + newDistances.Max().ToString("F4") + "m.");
                Assert(newMean < oldMean * .5f, "Stop profile failed to substantially reduce planted-foot drift.");

                foreach (float releaseDuration in new[] { dt, .25f, .75f, 1.4f })
                {
                    var a = Make(typeof(PunisherMovement));
                    for (int i = 0; i < 4 * fps; i++) a.Step(true, dt);
                    for (int i = 0; i < Mathf.CeilToInt(releaseDuration * fps); i++) a.Step(false, dt);
                    for (int i = 0; i < 3 * fps; i++) a.Step(true, dt);
                    Assert(a.animator.GetCurrentAnimatorStateInfo(0).IsName("Rifle Walk") && Mathf.Abs(a.CurrentSpeed - .6222f) < .00001f,
                        "Repress during Stop failed to resume walking.");
                    a.Destroy();
                }
                foreach (float hold in new[] { .1f, .5f })
                {
                    var a = Make(typeof(PunisherMovement));
                    for (int i = 0; i < Mathf.CeilToInt(hold * fps); i++) a.Step(true, dt);
                    for (int i = 0; i < 4 * fps; i++) a.Step(false, dt);
                    Assert(a.CurrentSpeed == 0f && a.animator.GetCurrentAnimatorStateInfo(0).IsName("Rifle Idle"), "Early release failed.");
                    a.Destroy();
                }
            }
            Report.AppendLine("PASS. Pose/translation sampled together with copied three-state graph. No gameplay Animator/imports modified. Full-scene visual confirmation still required.");
            File.WriteAllText("three-state-movement-results.txt", Report.ToString());
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText("three-state-movement-results.txt", Report + ex.ToString());
            EditorApplication.Exit(1);
        }
    }

    static Actor Make(Type type)
    {
        var root = new GameObject("Movement probe");
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/punisher_vest@Rifle Idle.fbx"), root.transform);
        var a = model.GetComponent<Animator>(); a.runtimeAnimatorController = controller;
        root.AddComponent<CharacterController>();
        var m = root.AddComponent(type);
        type.GetField("animator").SetValue(m, a);
        type.GetField("stoppingTime").SetValue(m, .5f); // Existing scene's serialized fallback.
        type.GetMethod("Awake", Private).Invoke(m, null);
        type.GetMethod("OnEnable", Private).Invoke(m, null);
        a.Rebind(); a.Update(0f);
        return new Actor { root = root, animator = a, movement = m, type = type,
            input = type.GetMethod("SetAnimationParameters", Private), advance = type.GetMethod("UpdateMovementSpeed", Private),
            speed = type.GetField("currentSpeed", Private),
            feet = new[] { a.GetBoneTransform(HumanBodyBones.LeftFoot), a.GetBoneTransform(HumanBodyBones.RightFoot) } };
    }
    static void Assert(bool valid, string message) { if (!valid) throw new Exception(message); }
}
