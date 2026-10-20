using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System;
using System.IO;
public static class AnimatorProbe
{
    static System.Text.StringBuilder report = new System.Text.StringBuilder();
    public static void Run()
    {
        try
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Punisher.controller");
            RunCase(controller, "direct walk controller");
            File.WriteAllText("probe-results.txt", report.ToString());
            EditorApplication.Exit(0);
        }
        catch(Exception ex) { File.WriteAllText("probe-results.txt",report + ex.ToString()); EditorApplication.Exit(1); }
    }
    static void RunCase(AnimatorController controller, string label)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/punisher_vest@Rifle Idle.fbx");
        var go = UnityEngine.Object.Instantiate(prefab);
        var animator = go.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.Update(0);
        report.AppendLine(label + " avatar=" + animator.avatar.isValid);
        foreach(var clip in controller.animationClips)
            report.AppendLine("clip " + clip.name + " length=" + clip.length);
        Step(animator, false, 120, "idle");
        Step(animator, true, 360, "first hold");
        if (label != "original" && (!animator.GetCurrentAnimatorStateInfo(0).IsName("Rifle Walk") || animator.IsInTransition(0)))
            throw new Exception("Failed to reach looping walk.");
        Step(animator, false, 360, "release");
        Step(animator, true, 10, "quick press");
        Step(animator, false, 360, "quick release");
        Step(animator, true, 360, "second hold");
        Step(animator, false, 10, "brief stop");
        Step(animator, true, 360, "restart during stop");
        if (label != "original" && (!animator.GetCurrentAnimatorStateInfo(0).IsName("Rifle Walk") || animator.IsInTransition(0)))
            throw new Exception("Failed to resume walking during stop.");
        Step(animator, false, 360, "final idle");
        if (label != "original" && (!animator.GetCurrentAnimatorStateInfo(0).IsName("Rifle Idle") || animator.IsInTransition(0)))
            throw new Exception("Failed to return to Idle.");
        UnityEngine.Object.DestroyImmediate(go);
    }
    static void Step(Animator a, bool input, int frames, string label)
    {
        a.SetBool("iswalking",input);
        var bone = a.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        Quaternion previous=bone.localRotation;
        float motion=0;
        for(int i=0;i<frames;i++) { a.Update(1f/60f); motion+=Quaternion.Angle(previous,bone.localRotation);previous=bone.localRotation; }
        var state=a.GetCurrentAnimatorStateInfo(0);
        string name="unknown";
        foreach(var n in new[]{"Rifle Idle","Rifle Walk","Rifle Walk To Stop"})
            if(state.IsName(n)) name=n;
        report.AppendLine(label+": "+name+" time="+state.normalizedTime+" transition="+a.IsInTransition(0)+" leg motion="+motion);
    }
}



