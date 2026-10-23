// Builds a four-state walking fixture only in the isolated test project.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class NewWalkingProbe
{
    static readonly string[] Names = { "Rifle Idle", "Start Walking", "Rifle Walk", "Rifle Walk To Stop" };
    static readonly StringBuilder Report = new StringBuilder();
    static readonly MethodInfo SetInput = typeof(PunisherMovement).GetMethod("SetAnimationParameters", BindingFlags.NonPublic | BindingFlags.Instance);
    static readonly MethodInfo SetSpeed = typeof(PunisherMovement).GetMethod("UpdateMovementSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
    static readonly FieldInfo Speed = typeof(PunisherMovement).GetField("currentSpeed", BindingFlags.NonPublic | BindingFlags.Instance);

    public static void Run()
    {
        try
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath("Assets/punisher_vest@Rifle Idle.fbx").OfType<Avatar>().First();
            var controller = AnimatorController.CreateAnimatorControllerAtPath("Assets/NewWalkingFixture.controller");
            controller.AddParameter("iswalking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("isrunning", AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            var states = Names.Select(n => machine.AddState(n)).ToArray();
            for (int i = 0; i < Names.Length; i++)
            {
                string path = "Assets/punisher_vest@" + Names[i] + ".fbx";
                if (i > 0)
                {
                    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    importer.sourceAvatar = avatar;
                    var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
                    clips[0].name = Names[i];
                    clips[0].loopTime = i == 2;
                    clips[0].loopPose = i == 2;
                    clips[0].keepOriginalOrientation = false;
                    clips[0].lockRootRotation = false;
                    clips[0].lockRootPositionXZ = false;
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();
                }
                states[i].motion = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => c.name == Names[i]);
            }
            machine.defaultState = states[0];
            Add(states, 0, 1, true, -1f, .08f);
            Add(states, 1, 2, true, .9f, .1f);
            Add(states, 1, 3, false, -1f, .1f);
            Add(states, 2, 3, false, -1f, .1f);
            Add(states, 3, 0, false, .9318182f, .1f);
            Add(states, 3, 1, true, -1f, .1f);

            Report.AppendLine("New walking-only script, corrected test-copy imports, four-state fixture. Gameplay Animator/imports were not edited.");
            foreach (int fps in new[] { 30, 60, 144 })
            {
                var actor = MakeActor(controller, out var a, out var m);
                Assert(m.walkingOnly && Mathf.Abs(m.EffectiveWalkSpeed - .6222f) < .001f, "Walking-only calibrated speed");
                Step(a, m, true, .2f, fps);
                Assert((float)Speed.GetValue(m) < m.EffectiveWalkSpeed * .15f, "Start animation must not immediately reach walking speed");
                Step(a, m, true, 4f, fps);
                Assert(a.GetCurrentAnimatorStateInfo(0).IsName("Rifle Walk"), "Must reach walk loop");
                Assert(Mathf.Abs((float)Speed.GetValue(m) - m.EffectiveWalkSpeed) < .001f, "Calibrated steady speed");
                Step(a, m, false, .2f, fps);
                float before = (float)Speed.GetValue(m);
                Step(a, m, true, .1f, fps);
                Assert((float)Speed.GetValue(m) >= before, "Quick restart preserves speed");
                Step(a, m, true, 4f, fps);
                Step(a, m, false, 4f, fps);
                Assert(a.GetCurrentAnimatorStateInfo(0).IsName("Rifle Idle") && (float)Speed.GetValue(m) < .001f, "Stop to idle");
                Report.AppendLine(fps + " fps: gradual start, walk loop, quick restart, and stopping passed.");
                UnityEngine.Object.DestroyImmediate(actor);
            }
            float oldSlip = MeasureSlip(controller, 1.5f);
            float calibratedSlip = MeasureSlip(controller, .6222f);
            Assert(calibratedSlip < oldSlip * .4f, "Calibrated speed should substantially reduce stance-foot drift");
            Report.AppendLine("Estimated median planted-foot Z drift: old 1.5 m/s=" + oldSlip.ToString("F4") +
                " m/s; calibrated .6222 m/s=" + calibratedSlip.ToString("F4") + " m/s.");
            Report.AppendLine("PASS. Full scene, camera turns, blending foot phase, and terrain still need visual review.");
            File.WriteAllText("new-walking-results.txt", Report.ToString());
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText("new-walking-results.txt", Report + ex.ToString());
            EditorApplication.Exit(1);
        }
    }
    static GameObject MakeActor(AnimatorController controller, out Animator animator, out PunisherMovement movement)
    {
        var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/punisher_vest@Rifle Idle.fbx"));
        animator = actor.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        actor.AddComponent<CharacterController>();
        movement = actor.AddComponent<PunisherMovement>();
        movement.controller = actor.GetComponent<CharacterController>();
        movement.animator = animator;
        movement.SendMessage("Awake");
        movement.SendMessage("OnEnable");
        animator.Rebind();
        animator.Update(0f);
        return actor;
    }
    static void Step(Animator a, PunisherMovement m, bool held, float seconds, int fps)
    {
        var input = held ? PunisherMovement.MovementState.Walking : PunisherMovement.MovementState.Idle;
        SetInput.Invoke(m, new object[] { input });
        for (int frame = 0; frame < Mathf.CeilToInt(seconds * fps); frame++)
        {
            SetSpeed.Invoke(m, new object[] { input, 1f / fps });
            a.Update(1f / fps);
            Assert(!a.GetBool("isrunning"), "Walking-only parameters");
            Assert(Mathf.Abs(a.bodyRotation.eulerAngles.y > 180 ? a.bodyRotation.eulerAngles.y - 360 : a.bodyRotation.eulerAngles.y) < 3f,
                "Unbaked Body Orientation should keep body facing forward");
        }
    }
    static float MeasureSlip(AnimatorController controller, float speed)
    {
        var actor = MakeActor(controller, out var a, out var m);
        SetInput.Invoke(m, new object[] { PunisherMovement.MovementState.Walking });
        a.Play("Rifle Walk", 0, 0f); a.Update(0f);
        Transform[] feet = { a.GetBoneTransform(HumanBodyBones.LeftFoot), a.GetBoneTransform(HumanBodyBones.RightFoot) };
        var samples = new List<Vector3>();
        var world = feet.Select(f => f.position).ToArray();
        var local = feet.Select(f => actor.transform.InverseTransformPoint(f.position)).ToArray();
        for (int frame = 0; frame < 180; frame++)
        {
            actor.transform.position += Vector3.forward * speed / 60f;
            a.Update(1f / 60f);
            for (int i = 0; i < 2; i++)
            {
                Vector3 nextLocal = actor.transform.InverseTransformPoint(feet[i].position);
                if (frame > 1) samples.Add(new Vector3(nextLocal.y, (nextLocal.z - local[i].z) * 60, (feet[i].position.z - world[i].z) * 60));
                local[i] = nextLocal; world[i] = feet[i].position;
            }
        }
        float floor = samples.Min(p => p.x);
        var slip = samples.Where(p => p.x < floor + .025f && p.y < -.05f).Select(p => Mathf.Abs(p.z)).OrderBy(v => v).ToArray();
        UnityEngine.Object.DestroyImmediate(actor);
        return slip[slip.Length / 2];
    }
    static void Add(AnimatorState[] states, int from, int to, bool held, float exit, float duration)
    {
        var t = states[from].AddTransition(states[to]);
        t.AddCondition(held ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "iswalking");
        t.hasFixedDuration = true; t.duration = duration;
        t.hasExitTime = exit >= 0; t.exitTime = Mathf.Max(0, exit);
        t.interruptionSource = TransitionInterruptionSource.Destination; t.orderedInterruption = true;
    }
    static void Assert(bool valid, string message) { if (!valid) throw new Exception(message); }
}
