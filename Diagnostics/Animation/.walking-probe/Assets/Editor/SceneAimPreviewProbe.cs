using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;

[InitializeOnLoad]
public static class SceneAimPreviewProbe
{
    const string Running = "Punisher.SceneAimPreviewTest";
    const string Finished = "Punisher.SceneAimPreviewTestFinished";
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly StringBuilder Report = new StringBuilder();
    static int phase, phaseFrame;
    static float phaseTime;
    static RifleHandIK ik;
    static PunisherShooter shooter;
    static ThirdPersonCameraController control;
    static Animator animator;
    static Vector3 elbowBefore;
    static Quaternion lookBefore;
    static float animationBefore;
    static int shots;

    static SceneAimPreviewProbe()
    {
        if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick;
    }
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }

    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.transform.position = new Vector3(.3f,.65f,-.7f); camera.transform.LookAt(new Vector3(0f,.62f,10f));
        var vc = new GameObject("Preview virtual camera").AddComponent<CinemachineCamera>();
        var follow = vc.gameObject.AddComponent<CinemachineThirdPersonFollow>(); follow.CameraDistance = .5f;
        var lens = vc.Lens; lens.FieldOfView = 60f; lens.NearClipPlane = .3f; vc.Lens = lens;
        var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AimFitFixture.prefab"));
        actor.name = "ScenePreviewActor"; actor.transform.position = Vector3.zero; actor.transform.rotation = Quaternion.identity;
        actor.GetComponent<PunisherMovement>().enabled = false;
        var a = actor.GetComponentInChildren<PunisherMovement>().animator;
        a.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Production.controller");
        a.applyRootMotion = false; a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var c = actor.GetComponentInChildren<ThirdPersonCameraController>();
        c.characterRoot = actor.transform; c.virtualCamera = vc; c.shoulderFollow = follow; c.captureCursorOnEnable = false;
        SessionState.SetBool(Running, true); SessionState.SetBool(Finished, false);
        SessionState.SetInt("Punisher.ScenePreviewExit", 1);
        SessionState.SetString("Punisher.ScenePreviewStarted", DateTime.UtcNow.Ticks.ToString());
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Running, false)) return;
        if (SessionState.GetBool(Finished, false))
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= Tick; SessionState.SetBool(Running, false);
            EditorApplication.Exit(SessionState.GetInt("Punisher.ScenePreviewExit", 1)); return;
        }
        try
        {
            long started = long.Parse(SessionState.GetString("Punisher.ScenePreviewStarted", DateTime.UtcNow.Ticks.ToString()));
            Assert((DateTime.UtcNow.Ticks - started) < TimeSpan.FromSeconds(90).Ticks, "Play Mode preview test timed out.");
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            if (shooter == null)
            {
                var root = GameObject.Find("ScenePreviewActor");
                shooter = root.GetComponent<PunisherShooter>();
                if (shooter == null) return;
                ik = root.GetComponentInChildren<RifleHandIK>(); animator = shooter.animator;
                control = root.GetComponentInChildren<ThirdPersonCameraController>();
                shooter.showShotEffects = false; shooter.ShotFired += _ => shots++;
                phaseFrame = Time.frameCount; phaseTime = Time.unscaledTime;
            }
            if (Time.frameCount - phaseFrame < 5 || Time.unscaledTime - phaseTime < .4f) return;
            if (phase == 0)
            {
                Assert(!shooter.IsAiming, "Normal unfocused input unexpectedly held aim.");
                ik.previewAimInSceneView = true;
                lookBefore = control.LookRotation; phase = 1; phaseFrame = Time.frameCount; phaseTime = Time.unscaledTime;
            }
            else if (phase == 1)
            {
                Assert(ik.IsEditorAimPreviewActive && shooter.IsEditorAimPreviewActive && shooter.IsAiming && !shooter.IsFiring, "Preview did not hold aim with no Game-view input.");
                Assert(animator.GetLayerWeight(shooter.WeaponLayerIndex) > .99f, "Preview did not activate the aim pose.");
                Assert(!control.GameplayInputAvailable && Cursor.lockState == CursorLockMode.None, "Preview did not free the mouse/block gameplay input.");
                Assert(Quaternion.Angle(lookBefore, control.LookRotation) < .01f, "Scene inspection changed gameplay aim direction.");
                Assert(Mathf.Abs(control.virtualCamera.Lens.FieldOfView - 45f) < .01f && Mathf.Abs(control.shoulderFollow.CameraDistance - .3f) < .001f, "Preview did not preserve the aiming camera lens.");
                Report.AppendLine("Real Play Mode: aim held with no Game-view input; cursor free; normal controls blocked; shoulder zoom and aim direction preserved.");
                Call(ik, "CreateAimPoseTargets");
                elbowBefore = animator.GetBoneTransform(HumanBodyBones.RightLowerArm).position;
                ik.rightElbowHint.position += ik.transform.right * .06f;
                animationBefore = animator.GetCurrentAnimatorStateInfo(shooter.WeaponLayerIndex).normalizedTime;
                Call(shooter, "ReadControls", true, true, true, true, Time.time);
                Assert(!shooter.IsFiring, "Scene click started a firing pose.");
                Call(shooter, "UpdateWeaponInput", true, true, Time.time);
                Assert(!(bool)Call(shooter, "FireIfReady", Time.time), "Preview accepted a shot.");
                phase = 2; phaseFrame = Time.frameCount; phaseTime = Time.unscaledTime;
            }
            else if (phase == 2)
            {
                float change = Vector3.Distance(elbowBefore, animator.GetBoneTransform(HumanBodyBones.RightLowerArm).position);
                Assert(change > .001f, "Live target edits did not update the elbow.");
                Assert(animator.GetCurrentAnimatorStateInfo(shooter.WeaponLayerIndex).normalizedTime > animationBefore, "Animation was paused during Scene preview.");
                Assert(shots == 0 && !shooter.IsFiring, "Preview fired while editing.");
                Report.AppendLine("Unpaused live tuning: elbow moved " + change + "m after target edit; animation continued; Scene clicks fired no shots.");
                ik.previewAimInSceneView = false; phase = 3; phaseFrame = Time.frameCount; phaseTime = Time.unscaledTime;
            }
            else
            {
                Assert(!shooter.IsAiming && !shooter.IsFiring && animator.GetLayerWeight(shooter.WeaponLayerIndex) < .01f, "Disabling preview did not release aim.");
                Assert(Mathf.Abs(control.virtualCamera.Lens.FieldOfView - 60f) < .01f && Mathf.Abs(control.shoulderFollow.CameraDistance - .5f) < .001f, "Disabling preview did not restore normal lens.");
                Call(shooter, "ReadControls", false, true, false, true, Time.time);
                Assert(!shooter.IsFiring, "Scene drag leaked a held click into normal firing.");
                Call(shooter, "ReadControls", false, true, false, false, Time.time);
                Call(shooter, "ReadControls", false, true, false, true, Time.time);
                Assert(shooter.IsFiring, "Normal fire failed to rearm after release.");
                Call(shooter, "ReadControls", false, false, false, false, Time.time);
                Report.AppendLine("Preview off: normal pose/lens restored; fire requires button release before rearming. PASS.");
                Complete(0);
            }
        }
        catch (Exception e) { Report.AppendLine(e.ToString()); Complete(1); }
    }

    static void Complete(int code)
    {
        File.WriteAllText("scene-aim-preview-results.txt", Report.ToString());
        SessionState.SetInt("Punisher.ScenePreviewExit", code); SessionState.SetBool(Finished, true);
        EditorApplication.isPlaying = false;
    }
}
