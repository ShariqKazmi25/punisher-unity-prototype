// Isolated-project only: searches matching gait phases without changing production imports.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class WalkRangeAudit
{
    static readonly HumanBodyBones[] Ids = { HumanBodyBones.Hips, HumanBodyBones.Spine,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm };
    class Pose { public Quaternion[] rotations; public Vector3[] positions; }
    class Match { public int start, end; public float angle, distance, velocity; }

    public static void Run()
    {
        try
        {
            const string path = "Assets/punisher_vest@Rifle Walk.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var settings = importer.clipAnimations;
            settings[0].firstFrame = 0;
            settings[0].lastFrame = 92;
            settings[0].loopTime = false;
            settings[0].loopPose = false;
            settings[0].lockRootPositionXZ = true; // Match the captured live imported artifact.
            settings[0].lockRootRotation = false;
            importer.clipAnimations = settings;
            importer.SaveAndReimport();
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => c.name == "Rifle Walk");
            var controller = AnimatorController.CreateAnimatorControllerAtPath("Assets/WalkRangeAudit.controller");
            var state = controller.layers[0].stateMachine.AddState("Walk");
            state.motion = clip;
            controller.layers[0].stateMachine.defaultState = state;
            var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/punisher_vest@Rifle Idle.fbx"));
            var animator = actor.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            var bones = Ids.Select(animator.GetBoneTransform).ToArray();
            var poses = new Pose[93];
            for (int frame = 0; frame <= 92; frame++)
            {
                animator.Play("Walk", 0, frame / 92f);
                animator.Update(0f);
                poses[frame] = new Pose { rotations = bones.Select(b => b.localRotation).ToArray(),
                    positions = bones.Select(b => actor.transform.InverseTransformPoint(b.position)).ToArray() };
            }
            var matches = new List<Match>();
            for (int start = 0; start < 63; start++)
                for (int end = start + 30; end <= 92; end++)
                    matches.Add(Compare(poses, start, end));
            var report = new StringBuilder();
            report.AppendLine("Live-equivalent import: Loop Pose OFF, XZ Bake ON, Rotation Bake OFF. 60fps, frames 0..92.");
            report.AppendLine("Metric: endpoint bone rotation RMS (degrees), root-relative position RMS (meters), endpoint velocity mismatch RMS (m/s).");
            report.AppendLine("Original: " + Format(Compare(poses, 0, 92)));
            report.AppendLine("Best endpoint matches (>=30-frame duration):");
            foreach (var match in matches.OrderBy(m => m.angle + 100f * m.distance + m.velocity).Take(20))
                report.AppendLine(Format(match));
            report.AppendLine("Best matches retaining frame 0:");
            foreach (var match in matches.Where(m => m.start == 0).OrderBy(m => m.angle + 100f * m.distance + m.velocity).Take(8))
                report.AppendLine(Format(match));
            report.AppendLine("Best matches retaining frame 92:");
            foreach (var match in matches.Where(m => m.end == 92).OrderBy(m => m.angle + 100f * m.distance + m.velocity).Take(8))
                report.AppendLine(Format(match));
            UnityEngine.Object.DestroyImmediate(actor);
            report.AppendLine("Loop playback verification, 20 seconds at 60 Hz, no Loop Pose:");
            VerifyLoop(importer, controller, state, 92, report);
            VerifyLoop(importer, controller, state, 61, report);
            File.WriteAllText("walk-range-audit.txt", report.ToString());
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { File.WriteAllText("walk-range-audit.txt", ex.ToString()); EditorApplication.Exit(1); }
    }

    static Match Compare(Pose[] poses, int start, int end)
    {
        float angle = 0f, distance = 0f, velocity = 0f;
        for (int i = 0; i < Ids.Length; i++)
        {
            float difference = Quaternion.Angle(poses[start].rotations[i], poses[end].rotations[i]);
            angle += difference * difference;
            distance += (poses[start].positions[i] - poses[end].positions[i]).sqrMagnitude;
            var initialVelocity = (poses[start + 1].positions[i] - poses[start].positions[i]) * 60f;
            var finalVelocity = (poses[end].positions[i] - poses[end - 1].positions[i]) * 60f;
            velocity += (initialVelocity - finalVelocity).sqrMagnitude;
        }
        return new Match { start = start, end = end, angle = Mathf.Sqrt(angle / Ids.Length),
            distance = Mathf.Sqrt(distance / Ids.Length), velocity = Mathf.Sqrt(velocity / Ids.Length) };
    }

    static string Format(Match m) => m.start + ".." + m.end + ", duration=" + ((m.end - m.start) / 60f).ToString("F4") +
        ", angle=" + m.angle.ToString("F4") + ", position=" + m.distance.ToString("F5") + ", velocity=" + m.velocity.ToString("F4");

    static void VerifyLoop(ModelImporter importer, AnimatorController controller, AnimatorState state, int end, StringBuilder report)
    {
        var settings = importer.clipAnimations;
        settings[0].loopTime = true;
        settings[0].loopPose = false;
        settings[0].lastFrame = end;
        importer.clipAnimations = settings;
        importer.SaveAndReimport();
        state.motion = AssetDatabase.LoadAllAssetsAtPath(importer.assetPath).OfType<AnimationClip>().First(c => c.name == "Rifle Walk");
        var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/punisher_vest@Rifle Idle.fbx"));
        var animator = actor.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.Update(0f);
        var bones = Ids.Select(animator.GetBoneTransform).ToArray();
        var rotations = bones.Select(b => b.localRotation).ToArray();
        var positions = bones.Select(b => actor.transform.InverseTransformPoint(b.position)).ToArray();
        var seams = new List<float>();
        var ordinary = new List<float>();
        var seamPositions = new List<float>();
        float oldTime = 0f;
        for (int frame = 0; frame < 1200; frame++)
        {
            animator.Update(1f / 60f);
            float time = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            float angleSquared = 0f, distanceSquared = 0f;
            for (int i = 0; i < bones.Length; i++)
            {
                float angle = Quaternion.Angle(rotations[i], bones[i].localRotation);
                var position = actor.transform.InverseTransformPoint(bones[i].position);
                angleSquared += angle * angle;
                distanceSquared += (position - positions[i]).sqrMagnitude;
                rotations[i] = bones[i].localRotation;
                positions[i] = position;
            }
            bool boundary = Mathf.FloorToInt(time) > Mathf.FloorToInt(oldTime);
            if (frame > 120)
            {
                (boundary ? seams : ordinary).Add(Mathf.Sqrt(angleSquared / bones.Length));
                if (boundary) seamPositions.Add(Mathf.Sqrt(distanceSquared / bones.Length));
            }
            oldTime = time;
        }
        seams.Sort(); ordinary.Sort(); seamPositions.Sort();
        report.AppendLine("0.." + end + ": loop boundary angle RMS median=" + seams[seams.Count / 2].ToString("F4") +
            ", ordinary=" + ordinary[ordinary.Count / 2].ToString("F4") + "; boundary position RMS=" +
            seamPositions[seamPositions.Count / 2].ToString("F5") + "; boundaries=" + seams.Count);
        if (end == 61 && seams.Max() > 5f) throw new Exception("Trimmed loop still has an excessive angular jump.");
        UnityEngine.Object.DestroyImmediate(actor);
    }
}
