// Isolated editor test; IK Pass and ExecuteAlways are enabled only on test copies.
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class HandIKProbe
{
    public static void Run()
    {
        var report = new StringBuilder();
        try
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Punisher.controller");
            var layers = controller.layers; layers[0].iKPass = true; controller.layers = layers;
            var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/punisher_vest@Rifle Idle.fbx"));
            var animator = actor.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind(); animator.Update(0f);
            var right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var mount = new GameObject("WeaponMount").transform;
            mount.SetParent(right, false);
            var grip = new GameObject("LeftHandGrip").transform;
            grip.SetParent(mount, false);
            grip.position = left.position + new Vector3(.025f, .01f, .015f);
            grip.rotation = left.rotation;
            var ik = actor.AddComponent<RifleHandIK>();
            ik.SendMessage("Awake");
            ik.leftHandGrip = grip;
            var spy = actor.AddComponent<HandIKProbeSpy>();
            ik.positionWeight = 0f;
            animator.Update(1f / 60f);
            if (spy.callbacks == 0) throw new Exception("Edit-mode Animator.Update did not execute IK callbacks; this probe cannot validate runtime grip accuracy.");
            report.AppendLine("Isolated editor callback check, ExecuteAlways on the helper test copy; callbacks=" + spy.callbacks + ".");
            grip.position = spy.leftGoal + new Vector3(.025f, .01f, .015f);
            grip.rotation = spy.leftRotation;
            ik.positionWeight = 1f;
            float minimum = float.MaxValue, maximum = 0f, total = 0f;
            for (int frame = 0; frame < 180; frame++)
            {
                animator.Update(1f / 60f);
                float error = Vector3.Distance(left.position, grip.position);
                if (frame > 5) { minimum = Mathf.Min(minimum, error); maximum = Mathf.Max(maximum, error); total += error; }
            }
            report.AppendLine("Idle, target under animated right hand: left wrist target error min=" + minimum.ToString("F5") +
                ", mean=" + (total / 174f).ToString("F5") + ", max=" + maximum.ToString("F5") + "m.");
            report.AppendLine("IK callback count=" + spy.callbacks + "; IK position weight=" + spy.positionWeight +
                "; last IK goal=" + spy.leftGoal + "; target=" + grip.position + "; wrist=" + left.position);
            if (maximum > .01f) throw new Exception("IK did not reach the nearby hand target.");
            animator.SetBool("iswalking", true);
            maximum = 0f;
            for (int frame = 0; frame < 300; frame++)
            {
                animator.Update(1f / 60f);
                maximum = Mathf.Max(maximum, Vector3.Distance(left.position, grip.position));
            }
            report.AppendLine("Idle -> Walk, right-hand-parented target: max wrist target error=" + maximum.ToString("F5") + "m.");
            if (maximum > .01f) throw new Exception("IK did not follow the hand target while walking.");
            ik.leftHandGrip = null;
            animator.Update(1f / 60f);
            report.AppendLine("Missing target: no exception. Helper compiled and IK callback executed on the copied Humanoid Animator.");
            UnityEngine.Object.DestroyImmediate(actor);
            File.WriteAllText("hand-ik-results.txt", report + "PASS. Visual rifle fitting remains a manual scene setup step.\n");
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { File.WriteAllText("hand-ik-results.txt", report + ex.ToString()); EditorApplication.Exit(1); }
    }
}

[ExecuteAlways, DefaultExecutionOrder(1000)]
public class HandIKProbeSpy : MonoBehaviour
{
    public int callbacks;
    public Vector3 leftGoal;
    public Quaternion leftRotation;
    public float positionWeight;
    void OnAnimatorIK(int layerIndex)
    {
        var a = GetComponent<Animator>();
        callbacks++;
        leftGoal = a.GetIKPosition(AvatarIKGoal.LeftHand);
        leftRotation = a.GetIKRotation(AvatarIKGoal.LeftHand);
        positionWeight = a.GetIKPositionWeight(AvatarIKGoal.LeftHand);
    }
}
