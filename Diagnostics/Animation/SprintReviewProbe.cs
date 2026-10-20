// Editor-only diagnostic for the isolated Temp/AnimatorProbe project.
// Builds the PROPOSED graph from copied clips; never edits the gameplay controller.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SprintReviewProbe
{
    private static readonly StringBuilder Report = new StringBuilder();
    private static readonly string[] Names =
        { "Rifle Idle", "Rifle Walk", "Idle to Run", "Walk to Stop", "Rifle Run", "Run to Stop" };
    private static readonly string[] Clips =
        { "Rifle Idle", "Rifle Walk", "Idle To Running", "Rifle Walk To Stop", "Rifle Run", "Running To Stop" };
    private static readonly MethodInfo SetParameters = typeof(PunisherMovement).GetMethod("SetAnimationParameters", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly MethodInfo SetSpeed = typeof(PunisherMovement).GetMethod("UpdateMovementSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo Speed = typeof(PunisherMovement).GetField("currentSpeed", BindingFlags.NonPublic | BindingFlags.Instance);

    public static void Run()
    {
        try
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath("Assets/SprintReview.controller");
            controller.AddParameter("iswalking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("isrunning", AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            var states = Names.Select(n => machine.AddState(n)).ToArray();
            machine.defaultState = states[0];
            for (int i = 0; i < states.Length; i++)
            {
                string clipName = Clips[i];
                states[i].motion = AssetDatabase.FindAssets("t:AnimationClip")
                    .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                    .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<AnimationClip>()
                    .First(c => c.name == clipName);
                states[i].speed = 1f;
            }

            Add(states, 0, 1, true, false, -1f, .25f);
            Add(states, 0, 2, true, true, -1f, .15f);
            Add(states, 1, 3, false, null, -1f, .37804615f, .33817723f);
            Add(states, 1, 2, true, true, -1f, .15f);
            Add(states, 2, 4, true, true, .85f, .15f);
            Add(states, 2, 5, null, false, -1f, .15f);
            Add(states, 3, 0, false, false, .9318182f, .05f);
            Add(states, 3, 1, true, false, -1f, .25f);
            Add(states, 4, 5, null, false, -1f, .20f);
            Add(states, 5, 0, false, false, .90f, .10f);
            Add(states, 5, 1, true, false, -1f, .15f);
            Add(states, 5, 4, true, true, -1f, .15f);
            Add(states, 3, 2, true, true, -1f, .15f);

            Report.AppendLine("Proposed 6-state, 13-transition graph + actual updated movement script.");
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
                var quickActor = MakeActor(controller, out var a, out var m);
                Advance(a, m, 2, .05f, fps);  // Begin sprint.
                Advance(a, m, 0, .05f, fps);  // Release W during run-start.
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
                Report.AppendLine(fps + " fps: all 18 starting-state/input combinations, quick release/restart, run-to-walk, walk-stop-to-run, idle, and disable reset passed.");
            }
            Report.AppendLine("PASS. Gameplay Animator unchanged; keyboard focus and full-scene visuals require Play Mode review.");
            File.WriteAllText("sprint-review-results.txt", Report.ToString());
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText("sprint-review-results.txt", Report + ex.ToString());
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

    private static void Add(AnimatorState[] states, int from, int to, bool? walking, bool? running, float exit, float duration, float offset = 0f)
    {
        var t = states[from].AddTransition(states[to]);
        t.hasExitTime = exit >= 0f;
        t.exitTime = Mathf.Max(0f, exit);
        t.hasFixedDuration = true;
        t.duration = duration;
        t.offset = offset;
        t.interruptionSource = TransitionInterruptionSource.Destination;
        t.orderedInterruption = true;
        if (walking.HasValue) t.AddCondition(walking.Value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "iswalking");
        if (running.HasValue) t.AddCondition(running.Value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "isrunning");
    }

    private static void Assert(bool valid, string message)
    {
        if (!valid) throw new Exception(message);
    }
}
