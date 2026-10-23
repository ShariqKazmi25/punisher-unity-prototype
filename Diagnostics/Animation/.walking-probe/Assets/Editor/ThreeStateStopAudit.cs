// Run only in the isolated probe project, against copied imports/controller.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class ThreeStateStopAudit
{
    public static void Run()
    {
        try
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/punisher_vest@Rifle Walk To Stop.fbx").OfType<AnimationClip>().First(c => c.name == "Rifle Walk To Stop");
            var controller = AnimatorController.CreateAnimatorControllerAtPath("Assets/ThreeStateStopAudit.controller");
            var state = controller.layers[0].stateMachine.AddState("Stop");
            state.motion = clip;
            controller.layers[0].stateMachine.defaultState = state;
            var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/punisher_vest@Rifle Idle.fbx"));
            var a = actor.GetComponent<Animator>();
            a.runtimeAnimatorController = controller;
            a.applyRootMotion = true;
            a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            a.Rebind(); a.Update(0f);
            var left = a.GetBoneTransform(HumanBodyBones.LeftFoot);
            var right = a.GetBoneTransform(HumanBodyBones.RightFoot);
            var lastLeft = actor.transform.InverseTransformPoint(left.position);
            var lastRight = actor.transform.InverseTransformPoint(right.position);
            var report = new StringBuilder("time,normalizedTime,rootSpeed,leftY,rightY,leftBackward,rightBackward\n");
            const float dt = 1f / 120f;
            for (int frame = 0; frame < Mathf.CeilToInt(clip.length / dt); frame++)
            {
                a.Update(dt);
                var lp = actor.transform.InverseTransformPoint(left.position);
                var rp = actor.transform.InverseTransformPoint(right.position);
                var delta = actor.transform.InverseTransformVector(a.deltaPosition);
                float[] values = { (frame + 1) * dt, a.GetCurrentAnimatorStateInfo(0).normalizedTime,
                    delta.z / dt, lp.y, rp.y, -(lp.z - lastLeft.z) / dt, -(rp.z - lastRight.z) / dt };
                report.AppendLine(string.Join(",", values.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
                lastLeft = lp; lastRight = rp;
            }
            File.WriteAllText("three-state-stop-motion.csv", report.ToString());
            UnityEngine.Object.DestroyImmediate(actor);
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { File.WriteAllText("three-state-stop-motion-error.txt", ex.ToString()); EditorApplication.Exit(1); }
    }
}
