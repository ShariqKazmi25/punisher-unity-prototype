// Runs against copied assets in .walking-probe; production imports/controller are read-only.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class WalkLoopAudit
{
    static readonly StringBuilder Report = new StringBuilder();
    static readonly HumanBodyBones[] Bones = { HumanBodyBones.Hips, HumanBodyBones.Spine,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm };
    static readonly string Path = "Assets/punisher_vest@Rifle Walk.fbx";

    public static void Run()
    {
        try
        {
            var actual = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Punisher.controller");
            var actor = MakeActor(actual, out var animator);
            float previousTime = -1f;
            int resets = 0, exits = 0, wraps = 0, walkFrames = 0;
            bool reachedWalk = false;
            for (int frame = 0; frame < 1200; frame++)
            {
                animator.SetBool("iswalking", true);
                animator.Update(1f / 60f);
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.IsName("Rifle Walk"))
                {
                    walkFrames++;
                    if (reachedWalk && state.normalizedTime < previousTime - .001f) resets++;
                    if (reachedWalk && Mathf.FloorToInt(state.normalizedTime) > Mathf.FloorToInt(previousTime)) wraps++;
                    reachedWalk = true;
                    previousTime = state.normalizedTime;
                }
                else if (reachedWalk) exits++;
            }
            Report.AppendLine("Saved controller, continuous iswalking=true for 20 seconds: walk frames=" + walkFrames +
                ", time resets=" + resets + ", exits after reaching walk=" + exits + ", loop boundaries=" + wraps +
                ", final normalizedTime=" + previousTime);
            UnityEngine.Object.DestroyImmediate(actor);

            var importer = (ModelImporter)AssetImporter.GetAtPath(Path);
            var settings = importer.clipAnimations;
            Report.AppendLine("Imported loop range=" + settings[0].firstFrame + ".." + settings[0].lastFrame +
                ", loopTime=" + settings[0].loopTime + ", loopPose=" + settings[0].loopPose);
            MeasureLoop("saved settings");
            settings[0].loopPose = true;
            importer.clipAnimations = settings;
            importer.SaveAndReimport();
            MeasureLoop("Loop Pose ON (test copy)");
            File.WriteAllText("walk-loop-audit.txt", Report.ToString());
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText("walk-loop-audit.txt", Report + ex.ToString());
            EditorApplication.Exit(1);
        }
    }

    static GameObject MakeActor(AnimatorController controller, out Animator animator)
    {
        var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/punisher_vest@Rifle Idle.fbx"));
        animator = actor.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.Update(0f);
        return actor;
    }

    static void MeasureLoop(string label)
    {
        var controller = AnimatorController.CreateAnimatorControllerAtPath("Assets/WalkLoopAudit.controller");
        var state = controller.layers[0].stateMachine.AddState("Loop");
        state.motion = AssetDatabase.LoadAllAssetsAtPath(Path).OfType<AnimationClip>().First(c => c.name == "Rifle Walk");
        controller.layers[0].stateMachine.defaultState = state;
        var actor = MakeActor(controller, out var animator);
        var bones = Bones.Select(animator.GetBoneTransform).ToArray();
        var rotations = bones.Select(b => b.localRotation).ToArray();
        var positions = bones.Select(b => actor.transform.InverseTransformPoint(b.position)).ToArray();
        var seamAngles = new List<float>();
        var normalAngles = new List<float>();
        var seamDistances = new List<float>();
        var normalDistances = new List<float>();
        float oldTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        for (int frame = 0; frame < 1200; frame++)
        {
            animator.Update(1f / 60f);
            float time = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            bool boundary = Mathf.FloorToInt(time) > Mathf.FloorToInt(oldTime);
            float angleSquared = 0, distanceSquared = 0;
            for (int i = 0; i < bones.Length; i++)
            {
                var rotation = bones[i].localRotation;
                var position = actor.transform.InverseTransformPoint(bones[i].position);
                float angle = Quaternion.Angle(rotation, rotations[i]);
                angleSquared += angle * angle;
                distanceSquared += (position - positions[i]).sqrMagnitude;
                rotations[i] = rotation; positions[i] = position;
            }
            if (frame > 120)
            {
                (boundary ? seamAngles : normalAngles).Add(Mathf.Sqrt(angleSquared / bones.Length));
                (boundary ? seamDistances : normalDistances).Add(Mathf.Sqrt(distanceSquared / bones.Length));
            }
            oldTime = time;
        }
        Report.AppendLine(label + ": boundary bone-angle RMS median=" + Median(seamAngles).ToString("F4") +
            " deg/frame, ordinary median=" + Median(normalAngles).ToString("F4") +
            "; boundary bone-position RMS median=" + Median(seamDistances).ToString("F5") +
            "m/frame, ordinary median=" + Median(normalDistances).ToString("F5") + "; seam count=" + seamAngles.Count);
        UnityEngine.Object.DestroyImmediate(actor);
    }

    static float Median(List<float> values)
    {
        values.Sort();
        return values.Count > 0 ? values[values.Count / 2] : float.NaN;
    }
}
