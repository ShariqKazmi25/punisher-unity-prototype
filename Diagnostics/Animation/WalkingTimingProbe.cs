// Editor-only diagnostic for the isolated Temp/AnimatorProbe project.
// Loads a COPY of the saved five-node gameplay controller and the actual movement script.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class WalkingTimingProbe
{
    private static readonly StringBuilder Report = new StringBuilder();
    private static readonly string[] Names =
        { "Rifle Idle", "Rifle Walk", "Rifle Walk To Stop", "Rifle Run", "Running To Stop" };
    private static readonly MethodInfo SetParameters = typeof(PunisherMovement).GetMethod("SetAnimationParameters", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly MethodInfo SetSpeed = typeof(PunisherMovement).GetMethod("UpdateMovementSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo Speed = typeof(PunisherMovement).GetField("currentSpeed", BindingFlags.NonPublic | BindingFlags.Instance);

    public static void Run()
    {
        try
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Punisher.controller");
            Assert(controller.layers[0].stateMachine.states.Length == 5, "Expected five saved states");
            Report.AppendLine("Saved five-node controller copy + transition-synchronized, eased walking and running ramps.");
            Assert(PunisherMovement.ResolveMovementState(false, false) == PunisherMovement.MovementState.Idle, "No input");
            Assert(PunisherMovement.ResolveMovementState(false, true) == PunisherMovement.MovementState.Idle, "Shift only");
            Assert(PunisherMovement.ResolveMovementState(true, false) == PunisherMovement.MovementState.Walking, "W only");
            Assert(PunisherMovement.ResolveMovementState(true, true) == PunisherMovement.MovementState.Running, "W + Shift");

            foreach (int fps in new[] { 30, 60, 144 })
            {
                for (int source = 0; source < Names.Length; source++)
                for (int desired = 0; desired < 3; desired++)
                {
                    var actor = MakeActor(controller, out var animator, out var movement);
                    animator.Play(Names[source], 0, 0f);
                    animator.Update(0f);
                    Advance(animator, movement, desired, 6f, fps);
                    AssertSteady(animator, movement, desired);
                    UnityEngine.Object.DestroyImmediate(actor);
                }
                CheckRunningRamps(controller, fps);
                CheckWalkingRamps(controller, fps);
                var quickActor = MakeActor(controller, out var a, out var m);
                Advance(a, m, 1, 3f, fps);
                Advance(a, m, 2, m.walkToRunTime + .15f, fps);
                Assert(Mathf.Abs((float)Speed.GetValue(m) - m.runSpeed) < .001f, "Walk-to-run blend timing");
                Advance(a, m, 1, m.runToWalkTime + .15f, fps);
                Assert(Mathf.Abs((float)Speed.GetValue(m) - m.walkSpeed) < .001f, "Run-to-walk blend timing");
                Advance(a, m, 0, 3f, fps);
                Advance(a, m, 2, .05f, fps);  // Begin sprint.
                Advance(a, m, 0, .05f, fps);  // Release W during direct run blend.
                Advance(a, m, 2, 6f, fps);    // Restart while stopping.
                AssertSteady(a, m, 2);
                Advance(a, m, 1, 6f, fps);    // Release Shift, retain W.
                AssertSteady(a, m, 1);
                Advance(a, m, 0, .05f, fps);  // Begin walking stop.
                Advance(a, m, 2, 6f, fps);    // Sprint from walking stop.
                AssertSteady(a, m, 2);
                Advance(a, m, 0, 6f, fps);    // W released, even if Shift held.
                AssertSteady(a, m, 0);
                m.SendMessage("OnDisable");
                Assert(!a.GetBool("iswalking") && !a.GetBool("isrunning"), "Disable must clear both parameters");
                UnityEngine.Object.DestroyImmediate(quickActor);
                Report.AppendLine(fps + " fps: all 15 starting-state/input combinations, quick release/restart, run-to-walk, walk-stop-to-run, idle, disable reset, gradual run-start and run-to-walk, and full-clip braking passed.");
            }
            Report.AppendLine("PASS. Gameplay Animator unchanged; keyboard focus and full-scene visuals require Play Mode review.");
            File.WriteAllText("walking-timing-results.txt", Report.ToString());
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText("walking-timing-results.txt", Report + ex.ToString());
            EditorApplication.Exit(1);
        }
    }

    private static GameObject MakeActor(AnimatorController controller, out Animator animator, out PunisherMovement movement)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/punisher_vest@Rifle Idle.fbx");
        var actor = UnityEngine.Object.Instantiate(model);
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

    private static void Advance(Animator animator, PunisherMovement movement, int desired, float seconds, int fps)
    {
        var state = (PunisherMovement.MovementState)desired;
        SetParameters.Invoke(movement, new object[] { state });
        Assert(animator.GetBool("iswalking") == (desired != 0), "Forward bool");
        Assert(animator.GetBool("isrunning") == (desired == 2), "Sprint bool");
        float previous = (float)Speed.GetValue(movement);
        float expected = desired == 2 ? movement.runSpeed : desired == 1 ? movement.walkSpeed : 0f;
        for (int frame = 0; frame < Mathf.CeilToInt(seconds * fps); frame++)
        {
            SetSpeed.Invoke(movement, new object[] { state, 1f / fps });
            float current = (float)Speed.GetValue(movement);
            Assert(Mathf.Abs(current - expected) <= Mathf.Abs(previous - expected) + .0001f, "Speed must approach target without resetting or overshooting");
            previous = current;
            animator.Update(1f / fps);
        }
    }

    private static void AssertSteady(Animator animator, PunisherMovement movement, int desired)
    {
        string expected = desired == 2 ? "Rifle Run" : desired == 1 ? "Rifle Walk" : "Rifle Idle";
        Assert(animator.GetCurrentAnimatorStateInfo(0).IsName(expected) && !animator.IsInTransition(0), "Expected steady " + expected);
        float speed = desired == 2 ? movement.runSpeed : desired == 1 ? movement.walkSpeed : 0f;
        Assert(Mathf.Abs((float)Speed.GetValue(movement) - speed) < .001f, "Expected speed " + speed);
    }

    private static void CheckRunningRamps(AnimatorController controller, int fps)
    {
        var actor = MakeActor(controller, out var a, out var m);
        // Deliberately use the previous serialized values: active Animator
        // timings must take over rather than relying on changed field defaults.
        m.runAccelerationTime = .25f;
        m.walkToRunTime = .2f;
        m.runToWalkTime = .2f;
        m.runStoppingTime = .6f;
        Advance(a, m, 2, .15f, fps);
        float early = (float)Speed.GetValue(m);
        Assert(early > 0f && early < m.runSpeed * .35f, "Idle-to-run must begin gradually: " + early);
        Advance(a, m, 2, .65f, fps);
        Assert(Mathf.Abs((float)Speed.GetValue(m) - m.runSpeed) < .001f, "Idle-to-run must finish with its 0.601s blend");

        Advance(a, m, 1, .23f, fps);
        Assert((float)Speed.GetValue(m) > m.walkSpeed + (m.runSpeed - m.walkSpeed) * .55f,
            "Run-to-walk must not finish before its 0.927s blend");
        Advance(a, m, 1, 1.1f, fps);
        Assert(Mathf.Abs((float)Speed.GetValue(m) - m.walkSpeed) < .001f, "Run-to-walk completion");
        Advance(a, m, 2, .14f, fps);
        Assert((float)Speed.GetValue(m) < m.walkSpeed + (m.runSpeed - m.walkSpeed) * .4f,
            "Walk-to-run must accelerate over the 0.578s blend");
        Advance(a, m, 2, .7f, fps);
        Assert(Mathf.Abs((float)Speed.GetValue(m) - m.runSpeed) < .001f, "Walk-to-run completion");

        Advance(a, m, 0, .25f, fps);
        Assert((float)Speed.GetValue(m) > m.runSpeed * .75f,
            "Braking must span the stop clip, not just its 0.25s entry blend");
        Advance(a, m, 0, 1.7f, fps);
        Assert((float)Speed.GetValue(m) < .001f, "Must stop by Running To Stop exit point");
        Advance(a, m, 0, 3f, fps);
        m.matchRunAnimationTiming = false;
        Advance(a, m, 2, .4f, fps);
        Assert(Mathf.Abs((float)Speed.GetValue(m) - m.runSpeed) < .001f,
            "Manual elapsed-time fallback must remain usable");
        Report.AppendLine(fps + " fps: speed after first 0.15s of sprint=" + early.ToString("F3") +
            ", full speed=" + m.runSpeed + "; transition timing overrides stale Inspector values.");
        UnityEngine.Object.DestroyImmediate(actor);
    }

    private static void CheckWalkingRamps(AnimatorController controller, int fps)
    {
        var actor = MakeActor(controller, out var a, out var m);
        // Verify the active blend overrides serialized fallback settings.
        m.accelerationTime = .8f;
        m.stoppingTime = .5f;
        Advance(a, m, 1, .05f, fps);
        float early = (float)Speed.GetValue(m);
        Assert(early > 0f && early < m.walkSpeed * .2f, "Walking must ease into motion: " + early);
        Advance(a, m, 1, .35f, fps);
        Assert(Mathf.Abs((float)Speed.GetValue(m) - m.walkSpeed) < .001f,
            "Walking should reach full speed with its 0.25s blend, not the .8s fallback");

        Advance(a, m, 0, .4f, fps);
        Assert((float)Speed.GetValue(m) > m.walkSpeed * .8f,
            "Walking must brake through the stop clip beyond its entry blend");
        Advance(a, m, 0, 2.3f, fps);
        Assert((float)Speed.GetValue(m) < .001f,
            "Walking must stop by its offset-adjusted 2.584s exit point");
        Advance(a, m, 0, 1f, fps);
        Advance(a, m, 1, 1f, fps);
        Advance(a, m, 0, .3f, fps);
        float beforeRestart = (float)Speed.GetValue(m);
        m.accelerationTime = .28f;
        Advance(a, m, 1, .25f, fps);
        float restarted = (float)Speed.GetValue(m);
        Report.AppendLine(fps + " fps: interrupted walking-stop restart speed " + beforeRestart.ToString("F4") +
            " -> " + restarted.ToString("F4") + ", active blend duration=" + a.GetAnimatorTransitionInfo(0).duration);
        Assert(beforeRestart > 0f && restarted >= beforeRestart, "Restart must preserve remaining momentum");
        Assert(restarted < beforeRestart + (m.walkSpeed - beforeRestart) * .6f,
            "Restart must use the 0.985s animation blend rather than the short fallback");
        Advance(a, m, 1, 1f, fps);
        AssertSteady(a, m, 1);

        Advance(a, m, 0, .05f, fps);
        Advance(a, m, 1, .05f, fps);
        Advance(a, m, 0, .05f, fps);
        Advance(a, m, 1, 4f, fps);
        AssertSteady(a, m, 1);
        Advance(a, m, 0, 4f, fps);
        AssertSteady(a, m, 0);

        m.matchWalkingAnimationTiming = false;
        m.accelerationTime = .8f;
        Advance(a, m, 1, .2f, fps);
        Assert((float)Speed.GetValue(m) > 0f && (float)Speed.GetValue(m) < m.walkSpeed * .4f,
            "Walking manual timing fallback must remain eased and usable");
        Advance(a, m, 1, .8f, fps);
        AssertSteady(a, m, 1);
        Report.AppendLine(fps + " fps: walking ease-in, 0.25s start, 2.584s offset-adjusted stop, " +
            "0.985s restart without losing momentum, repeated W taps, and manual fallback passed.");
        UnityEngine.Object.DestroyImmediate(actor);
    }

    private static void Assert(bool valid, string message)
    {
        if (!valid) throw new Exception(message);
    }
}



