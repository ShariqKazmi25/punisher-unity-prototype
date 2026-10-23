// Temporary, read-only Play Mode diagnostics. This file is excluded from builds.
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class WalkingRuntimeTrace
{
    const string Folder = "Diagnostics/Animation";
    static readonly FieldInfo Speed = typeof(PunisherMovement).GetField("currentSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly StringBuilder Rows = new StringBuilder();
    static PunisherMovement movement;
    static Animator animator;
    static Transform hips;
    static Transform[] bones;
    static Quaternion[] previousRotations;
    static Vector3[] previousPositions;
    static int lastFrame = -1;
    static float startedAt = -1f;
    static bool finished;

    static WalkingRuntimeTrace()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += Sample;
        EditorApplication.delayCall += WriteSnapshot;
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            movement = null;
            animator = null;
            hips = null;
            bones = null;
            lastFrame = -1;
            startedAt = -1f;
            finished = false;
            Rows.Clear();
            Rows.AppendLine("frame,time,dt,focused,requested,iswalking,state,next,transition,normalizedTime,transitionTime,speed,controllerVx,controllerVy,controllerVz,grounded,collisionFlags,rootX,rootY,rootZ,rootYaw,modelLocalX,modelLocalY,modelLocalZ,modelLocalYaw,hipsLocalX,hipsLocalY,hipsLocalZ,hipsX,hipsY,hipsZ,cameraX,cameraY,cameraZ,cameraYaw,hipsScreenX,hipsScreenY,applyRootMotion,animatorSpeed,poseAngleRms,poseDistanceRms");
            WriteSnapshot();
        }
        else if (state == PlayModeStateChange.ExitingPlayMode && startedAt >= 0f && !finished)
            Save();
    }

    static PunisherMovement FindMovement()
    {
        foreach (var candidate in Resources.FindObjectsOfTypeAll<PunisherMovement>())
            if (candidate.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(candidate))
                return candidate;
        return null;
    }

    static void WriteSnapshot()
    {
        try
        {
            var report = new StringBuilder();
            report.AppendLine("Captured " + DateTime.Now.ToString("s") + "; Play Mode=" + EditorApplication.isPlaying);
            var found = FindMovement();
            if (found == null) { report.AppendLine("No scene PunisherMovement found."); }
            else
            {
                report.AppendLine(EditorJsonUtility.ToJson(found, true));
                var a = found.animator != null ? found.animator : found.GetComponentInChildren<Animator>();
                if (a != null)
                {
                    report.AppendLine("Animator=" + a.name + "; controller=" + AssetDatabase.GetAssetPath(a.runtimeAnimatorController) +
                        "; rootMotion=" + a.applyRootMotion + "; updateMode=" + a.updateMode + "; speed=" + a.speed +
                        "; avatar=" + AssetDatabase.GetAssetPath(a.avatar));
                    foreach (var clip in a.runtimeAnimatorController.animationClips)
                    {
                        var settings = AnimationUtility.GetAnimationClipSettings(clip);
                        report.AppendLine("Clip=" + clip.name + "; path=" + AssetDatabase.GetAssetPath(clip) + "; length=" + clip.length +
                            "; looping=" + clip.isLooping + "; loopPose=" + settings.loopBlend +
                            "; bakeXZ=" + settings.loopBlendPositionXZ + "; bakeRotation=" + settings.loopBlendOrientation);
                    }
                }
            }
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "walking-live-setup.txt"), report.ToString());
        }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    static void Sample()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused || finished || Time.frameCount == lastFrame)
            return;
        lastFrame = Time.frameCount;
        if (movement == null)
        {
            movement = FindMovement();
            if (movement == null) return;
            animator = movement.animator;
            if (animator != null && animator.isHuman) hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        }
        if (animator == null || !animator.isInitialized) return;
        if (startedAt < 0f)
        {
            if (movement.RequestedState != PunisherMovement.MovementState.Walking) return;
            startedAt = Time.time;
        }
        var root = movement.controller != null ? movement.controller.transform : movement.transform;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        bool blending = animator.IsInTransition(0);
        var next = blending ? animator.GetNextAnimatorStateInfo(0) : default;
        var blend = blending ? animator.GetAnimatorTransitionInfo(0) : default;
        var velocity = movement.controller != null ? movement.controller.velocity : Vector3.zero;
        var hp = hips != null ? root.InverseTransformPoint(hips.position) : Vector3.zero;
        var hw = hips != null ? hips.position : Vector3.zero;
        float angleSquared = 0f, distanceSquared = 0f;
        if (bones == null && animator.isHuman)
        {
            var ids = new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot };
            bones = Array.ConvertAll(ids, animator.GetBoneTransform);
            previousRotations = Array.ConvertAll(bones, b => b.localRotation);
            previousPositions = Array.ConvertAll(bones, b => root.InverseTransformPoint(b.position));
        }
        if (bones != null)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                float angle = Quaternion.Angle(previousRotations[i], bones[i].localRotation);
                var position = root.InverseTransformPoint(bones[i].position);
                angleSquared += angle * angle;
                distanceSquared += (position - previousPositions[i]).sqrMagnitude;
                previousRotations[i] = bones[i].localRotation;
                previousPositions[i] = position;
            }
        }
        var camera = movement.cameraTransform;
        var screen = Camera.main != null ? Camera.main.WorldToViewportPoint(hw) : Vector3.zero;
        object[] values = { Time.frameCount, Time.time, Time.deltaTime, Application.isFocused, movement.RequestedState,
            animator.GetBool("iswalking"), Name(state), Name(next), blending, state.normalizedTime, blend.normalizedTime,
            Speed.GetValue(movement), velocity.x, velocity.y, velocity.z,
            movement.controller != null && movement.controller.isGrounded,
            movement.controller != null ? (int)movement.controller.collisionFlags : 0,
            root.position.x, root.position.y, root.position.z, root.eulerAngles.y,
            animator.transform.localPosition.x, animator.transform.localPosition.y, animator.transform.localPosition.z,
            animator.transform.localEulerAngles.y, hp.x, hp.y, hp.z, hw.x, hw.y, hw.z,
            camera != null ? camera.position.x : 0f, camera != null ? camera.position.y : 0f,
            camera != null ? camera.position.z : 0f, camera != null ? camera.eulerAngles.y : 0f,
            screen.x, screen.y, animator.applyRootMotion, animator.speed,
            bones != null ? Mathf.Sqrt(angleSquared / bones.Length) : 0f,
            bones != null ? Mathf.Sqrt(distanceSquared / bones.Length) : 0f };
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) Rows.Append(',');
            Rows.Append(Convert.ToString(values[i], CultureInfo.InvariantCulture));
        }
        Rows.AppendLine();
        if (Time.time - startedAt >= 12f) Save();
    }

    static string Name(AnimatorStateInfo state)
    {
        foreach (string name in new[] { "Rifle Idle", "Start Walking", "Rifle Walk", "Rifle Walk To Stop" })
            if (state.shortNameHash == Animator.StringToHash(name)) return name;
        return state.shortNameHash.ToString();
    }

    static void Save()
    {
        finished = true;
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "walking-runtime-trace.csv"), Rows.ToString());
        Debug.Log("Walking trace saved to Diagnostics/Animation/walking-runtime-trace.csv. No movement or animation settings were changed.");
    }
}
