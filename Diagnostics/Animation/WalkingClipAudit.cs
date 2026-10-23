// Run only in Temp/AnimatorProbe. Import settings below apply to test COPIES.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class WalkingClipAudit
{
    private static readonly StringBuilder Report = new StringBuilder();
    private static readonly string[] Names = { "Start Walking", "Rifle Walk", "Rifle Walk To Stop" };
    public static void Run()
    {
        try
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath("Assets/punisher_vest@Rifle Idle.fbx").OfType<Avatar>().First();
            foreach (string name in Names)
            {
                string path = "Assets/punisher_vest@" + name + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                Report.AppendLine("ORIGINAL " + name + ": rig=" + importer.animationType + ", avatar=" + importer.avatarSetup);
                DescribeClips(path);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
                var settings = importer.defaultClipAnimations;
                settings[0].name = name;
                settings[0].loopTime = name == "Rifle Walk";
                importer.clipAnimations = settings;
                importer.SaveAndReimport();
                Report.AppendLine("HUMANOID " + name);
                DescribeClips(path);
                Sample(path, name, "default rotation");
                settings = importer.clipAnimations;
                settings[0].keepOriginalOrientation = true;
                settings[0].lockRootRotation = false;
                settings[0].lockRootPositionXZ = false;
                importer.clipAnimations = settings;
                importer.SaveAndReimport();
                Report.AppendLine("ORIGINAL orientation, rotation/XZ NOT baked: " + name);
                Sample(path, name, "original rotation");
            }
            File.WriteAllText("walking-clip-audit.txt", Report.ToString());
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText("walking-clip-audit.txt", Report + ex.ToString());
            EditorApplication.Exit(1);
        }
    }
    private static void DescribeClips(string path)
    {
        foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")))
            Report.AppendLine(" clip=" + clip.name + " duration=" + clip.length + " fps=" + clip.frameRate +
                " humanoid=" + clip.isHumanMotion + " avgVelocity=" + clip.averageSpeed.ToString("F4"));
    }
    private static void Sample(string path, string name, string label)
    {
        var controller = AnimatorController.CreateAnimatorControllerAtPath("Assets/ClipAudit.controller");
        var machine = controller.layers[0].stateMachine;
        var state = machine.AddState("Sample");
        var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => c.name == name);
        state.motion = clip;
        machine.defaultState = state;
        var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/punisher_vest@Rifle Idle.fbx"));
        var a = go.GetComponent<Animator>();
        a.runtimeAnimatorController = controller;
        a.applyRootMotion = true;
        a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        a.Rebind();
        a.Update(0);
        Transform hips = a.GetBoneTransform(HumanBodyBones.Hips);
        Transform left = a.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform right = a.GetBoneTransform(HumanBodyBones.RightFoot);
        Vector3 rootTravel = Vector3.zero;
        Quaternion rootTurn = Quaternion.identity;
        float minBodyYaw = float.MaxValue, maxBodyYaw = float.MinValue;
        float minHipX = float.MaxValue, maxHipX = float.MinValue;
        float maxFootBackward = 0;
        Vector3 lp = left.position, rp = right.position;
        var footSamples = new List<Vector2>();
        Vector3 lastLeftLocal = go.transform.InverseTransformPoint(left.position);
        Vector3 lastRightLocal = go.transform.InverseTransformPoint(right.position);
        for (int frame = 0; frame < Mathf.RoundToInt(clip.length * 60); frame++)
        {
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            a.Update(1f / 60f);
            rootTravel += a.deltaPosition;
            rootTurn *= a.deltaRotation;
            Vector3 hipLocal = go.transform.InverseTransformPoint(hips.position);
            Vector3 bodyFwd = go.transform.InverseTransformDirection(a.bodyRotation * Vector3.forward);
            float yaw = Mathf.Atan2(bodyFwd.x, bodyFwd.z) * Mathf.Rad2Deg;
            minBodyYaw = Mathf.Min(minBodyYaw, yaw); maxBodyYaw = Mathf.Max(maxBodyYaw, yaw);
            minHipX = Mathf.Min(minHipX, hipLocal.x); maxHipX = Mathf.Max(maxHipX, hipLocal.x);
            Vector3 lv = (left.position - lp) * 60, rv = (right.position - rp) * 60;
            Vector3 leftLocal = go.transform.InverseTransformPoint(left.position);
            Vector3 rightLocal = go.transform.InverseTransformPoint(right.position);
            if (frame > 1)
            {
                footSamples.Add(new Vector2(leftLocal.y, -(leftLocal.z - lastLeftLocal.z) * 60f));
                footSamples.Add(new Vector2(rightLocal.y, -(rightLocal.z - lastRightLocal.z) * 60f));
            }
            lastLeftLocal = leftLocal; lastRightLocal = rightLocal;
            maxFootBackward = Mathf.Max(maxFootBackward, -lv.z, -rv.z);
            lp = left.position; rp = right.position;
        }
        Report.AppendLine(" " + label + ": rootTravel=" + rootTravel.ToString("F4") +
            " rootYaw=" + Mathf.DeltaAngle(0, rootTurn.eulerAngles.y) +
            " bodyYaw=[" + minBodyYaw.ToString("F2") + "," + maxBodyYaw.ToString("F2") + "]" +
            " hipX=[" + minHipX.ToString("F4") + "," + maxHipX.ToString("F4") + "]" +
            " maxFootBackward=" + maxFootBackward);
        float minHeight = footSamples.Min(p => p.x);
        var stanceVelocities = footSamples.Where(p => p.x < minHeight + .025f && p.y > .05f)
            .Select(p => p.y).OrderBy(v => v).ToArray();
        if (stanceVelocities.Length > 0)
            Report.AppendLine(" stance-foot estimate=" + stanceVelocities[stanceVelocities.Length / 2] +
                " m/s, samples=" + stanceVelocities.Length + ", humanScale=" + a.humanScale + ", rootScale=" + go.transform.lossyScale);
        UnityEngine.Object.DestroyImmediate(go);
    }
}
