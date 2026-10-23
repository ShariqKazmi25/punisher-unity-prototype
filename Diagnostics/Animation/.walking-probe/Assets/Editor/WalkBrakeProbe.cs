// Isolated Unity project only. Uses copied gameplay controller/imports and source.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class WalkBrakeProbe
{
    static readonly StringBuilder Report = new StringBuilder();
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static AnimatorController controller;
    class Actor
    {
        public GameObject root;
        public Animator animator;
        public Component movement;
        public Type type;
        public MethodInfo speed, input;
        public FieldInfo currentSpeed;
        public float Speed => (float)currentSpeed.GetValue(movement);
        public void Step(bool held, float dt)
        {
            var stateType = type.GetNestedType("MovementState");
            object state = Enum.Parse(stateType, held ? "Walking" : "Idle");
            input.Invoke(movement, new[] { state });
            speed.Invoke(movement, new object[] { state, dt });
            animator.Update(dt);
        }
    }

    public static void Run()
    {
        try
        {
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Punisher.controller");
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states)
            {
                var clip = child.state.motion as AnimationClip;
                Report.AppendLine("State " + child.state.name + ", speed=" + child.state.speed + ", length=" + (clip != null ? clip.length.ToString("F6") : "none"));
                foreach (var transition in child.state.transitions)
                    Report.AppendLine(" " + child.state.name + " -> " + transition.destinationState.name + ": duration=" + transition.duration +
                        ", fixed=" + transition.hasFixedDuration + ", offset=" + transition.offset + ", exit=" + transition.hasExitTime);
            }
            AuditStopMotion();
            foreach (int fps in new[] { 30, 60, 144 })
            {
                float dt = 1f / fps;
                var before = Make(typeof(PunisherMovementBeforeWalkBrake));
                var after = Make(typeof(PunisherMovement));
                float largestStartDifference = 0f;
                for (int frame = 0; frame < 6 * fps; frame++)
                {
                    before.Step(true, dt); after.Step(true, dt);
                    largestStartDifference = Mathf.Max(largestStartDifference, Mathf.Abs(before.Speed - after.Speed));
                }
                Assert(largestStartDifference < .000001f, "Start/walk acceleration changed: " + largestStartDifference);
                Assert(Mathf.Abs(after.Speed - .6222f) < .00001f, "Steady calibrated speed changed.");
                float beforeDistance = 0f, afterDistance = 0f, oldStopAt = -1f, newStopAt = -1f;
                for (int frame = 0; frame < 4 * fps; frame++)
                {
                    before.Step(false, dt); after.Step(false, dt);
                    beforeDistance += before.Speed * dt; afterDistance += after.Speed * dt;
                    if (before.Speed <= .00001f && oldStopAt < 0f) oldStopAt = (frame + 1) * dt;
                    if (after.Speed <= .00001f && newStopAt < 0f) newStopAt = (frame + 1) * dt;
                }
                Assert(newStopAt > 0f && newStopAt <= .08f + dt + .0001f, "Walk stop exceeded short brake window.");
                Assert(afterDistance < .03f, "Walk stop travels more than 3 cm.");
                Assert(after.animator.GetCurrentAnimatorStateInfo(0).IsName("Rifle Idle"), "Animator did not finish stop to Idle.");
                Report.AppendLine(fps + " Hz: start/steady max speed difference=" + largestStartDifference +
                    "; old stop=" + oldStopAt.ToString("F4") + "s/" + beforeDistance.ToString("F5") + "m; new=" +
                    newStopAt.ToString("F4") + "s/" + afterDistance.ToString("F5") + "m.");
                UnityEngine.Object.DestroyImmediate(before.root); UnityEngine.Object.DestroyImmediate(after.root);

                // Release at different gait phases and repress W while the stop blend is active.
                for (int phase = 0; phase < 8; phase++)
                {
                    var actor = Make(typeof(PunisherMovement));
                    for (int frame = 0; frame < Mathf.CeilToInt((4f + phase * .1f) * fps); frame++) actor.Step(true, dt);
                    for (int frame = 0; frame < Mathf.CeilToInt(.12f * fps); frame++) actor.Step(false, dt);
                    Assert(actor.Speed == 0f, "Brake extended by active transition.");
                    for (int frame = 0; frame < 4 * fps; frame++)
                    {
                        actor.Step(true, dt);
                        Assert(actor.Speed >= 0f && actor.Speed <= .62221f, "Restart speed outside walking limits.");
                    }
                    Assert(actor.animator.GetCurrentAnimatorStateInfo(0).IsName("Rifle Walk"), "Restart never reached Walk.");
                    Assert(Mathf.Abs(actor.Speed - .6222f) < .00001f, "Restart never reached walking speed.");
                    UnityEngine.Object.DestroyImmediate(actor.root);
                }
                foreach (float releaseAt in new[] { .1f, .6f })
                {
                    var actor = Make(typeof(PunisherMovement));
                    for (int frame = 0; frame < Mathf.CeilToInt(releaseAt * fps); frame++) actor.Step(true, dt);
                    for (int frame = 0; frame < Mathf.CeilToInt(.12f * fps); frame++) actor.Step(false, dt);
                    Assert(actor.Speed == 0f, "Release during startup keeps drifting.");
                    UnityEngine.Object.DestroyImmediate(actor.root);
                }
                var quick = Make(typeof(PunisherMovement));
                for (int frame = 0; frame < 5 * fps; frame++) quick.Step(true, dt);
                quick.Step(false, dt);
                for (int frame = 0; frame < 4 * fps; frame++) quick.Step(true, dt);
                Assert(quick.Speed > .6221f, "One-frame release/repress failed.");
                UnityEngine.Object.DestroyImmediate(quick.root);
            }
            Report.AppendLine("PASS. Actual gameplay controller copy; original start/steady speed curves preserved. Visual full-scene confirmation remains necessary.");
            File.WriteAllText("walk-braking-results.txt", Report.ToString());
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText("walk-braking-results.txt", Report + ex.ToString());
            EditorApplication.Exit(1);
        }
    }

    static Actor Make(Type type)
    {
        var root = new GameObject("Brake probe");
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/punisher_vest@Rifle Idle.fbx"), root.transform);
        var a = model.GetComponent<Animator>();
        a.runtimeAnimatorController = controller;
        root.AddComponent<CharacterController>();
        var m = root.AddComponent(type);
        type.GetField("animator").SetValue(m, a);
        type.GetMethod("Awake", Private).Invoke(m, null);
        type.GetMethod("OnEnable", Private).Invoke(m, null);
        a.Rebind(); a.Update(0f);
        return new Actor { root = root, animator = a, movement = m, type = type,
            speed = type.GetMethod("UpdateMovementSpeed", Private), input = type.GetMethod("SetAnimationParameters", Private),
            currentSpeed = type.GetField("currentSpeed", Private) };
    }

    static void AuditStopMotion()
    {
        var walk = controller.layers[0].stateMachine.states.First(s => s.state.name == "Rifle Walk").state;
        var transition = walk.transitions.First(t => t.destinationState.name == "Rifle Walk To Stop");
        var stop = (AnimationClip)transition.destinationState.motion;
        Report.AppendLine("Walk stop entry time=" + (stop.length * transition.offset).ToString("F5") + "s in " + stop.length.ToString("F5") + "s clip.");
        var single = AnimatorController.CreateAnimatorControllerAtPath("Assets/StopMotionAudit.controller");
        var state = single.layers[0].stateMachine.AddState("Stop");
        state.motion = stop; single.layers[0].stateMachine.defaultState = state;
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/punisher_vest@Rifle Idle.fbx"));
        var a = model.GetComponent<Animator>(); a.runtimeAnimatorController = single;
        a.applyRootMotion = true; a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        a.Rebind(); a.Update(0f); a.Play("Stop", 0, transition.offset); a.Update(0f);
        Vector3 displacement = Vector3.zero;
        for (int frame = 0; frame < 120; frame++)
        {
            a.Update(1f / 120f); displacement += a.deltaPosition;
            if ((frame + 1) % 12 == 0)
            {
                Report.AppendLine(" Stop motion @" + ((frame + 1) / 120f).ToString("F2") + "s after entry: current extracted root speed=" +
                    (a.deltaPosition.magnitude * 120f).ToString("F5") + ", cumulative travel=" + displacement.magnitude.ToString("F5"));
            }
        }
        UnityEngine.Object.DestroyImmediate(model);
    }
    static void Assert(bool valid, string message) { if (!valid) throw new Exception(message); }
}
