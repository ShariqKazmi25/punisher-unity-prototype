using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SharedStandingAimProbe
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly StringBuilder Report = new StringBuilder();
    static readonly HumanBodyBones[] Upper = { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
        HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand };
    static readonly HumanBodyBones[] Legs = { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot };
    static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static AnimatorController oldGraph, shared;

    class Actor
    {
        public GameObject root;
        public Animator animator;
        public RifleHandIK ik;
        public PunisherShooter shooter;
        public Transform gun, chest;
        public void Step(bool walk, bool aim, bool fire, float time, float dt)
        {
            animator.SetBool("iswalking", walk);
            Call(shooter, "UpdateWeaponInput", aim, fire, time); Call(shooter, "WriteAnimationParameters"); Call(shooter, "UpdateLayerWeight", dt);
            animator.Update(dt); Call(shooter.weaponAnimation, "BindMarkers"); Call(shooter.weaponAnimation, "Advance", dt); Call(ik, "LateUpdate");
            Call(shooter, "FireIfReady", time);
        }
        public void Destroy() => UnityEngine.Object.DestroyImmediate(root);
    }

    public static void Run()
    {
        try
        {
            oldGraph = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/UserBeforeSharedAim.controller");
            shared = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Production.controller");
            Assert(shared.layers[1].stateMachine.states.Length == 3, "Expected common standing upper-body poses.");
            Assert(shared.layers[0].stateMachine.states.Length == oldGraph.layers[0].stateMachine.states.Length, "Locomotion graph changed.");
            var camera = new GameObject("Pose comparison camera").AddComponent<Camera>(); camera.transform.position = new Vector3(.3f,.65f,-.7f); camera.transform.LookAt(new Vector3(0f,.62f,10f));
            foreach (int fps in new[] { 30,60,144 })
            {
                float dt = 1f / fps, time = 0f;
                var oldStand = Make(oldGraph, camera); var newStand = Make(shared, camera);
                var oldWalk = Make(oldGraph, camera); var newWalk = Make(shared, camera);
                float standingPosition = 0f, standingAngle = 0f, legsPosition = 0f, legsAngle = 0f;
                float oldRelativePosition = 0f, newRelativePosition = 0f, oldRelativeAngle = 0f, newRelativeAngle = 0f;
                int walkStateChanges = 0; int previousHash = 0; bool previousFire = false;
                float previousNormalized = 0f;
                for (int frame = 0; frame < 12 * fps; frame++)
                {
                    time += dt;
                    bool aim = frame >= fps && frame < 11 * fps;
                    bool fire = frame >= 6 * fps && frame < 10 * fps;
                    bool walk = frame >= 3 * fps && frame < 5 * fps || frame >= 7 * fps && frame < 9 * fps;
                    oldStand.Step(false,aim,fire,time,dt); newStand.Step(false,aim,fire,time,dt);
                    oldWalk.Step(walk,aim,fire,time,dt); newWalk.Step(walk,aim,fire,time,dt);
                    foreach (var bone in Upper)
                    {
                        var a = oldStand.animator.GetBoneTransform(bone); var b = newStand.animator.GetBoneTransform(bone);
                        standingPosition = Mathf.Max(standingPosition,Vector3.Distance(a.position,b.position));
                        standingAngle = Mathf.Max(standingAngle,Quaternion.Angle(a.rotation,b.rotation));
                    }
                    standingPosition = Mathf.Max(standingPosition,Vector3.Distance(oldStand.gun.position,newStand.gun.position));
                    standingAngle = Mathf.Max(standingAngle,Quaternion.Angle(oldStand.gun.rotation,newStand.gun.rotation));
                    foreach(var bone in Legs)
                    {
                        var a = oldWalk.animator.GetBoneTransform(bone); var b = newWalk.animator.GetBoneTransform(bone);
                        legsPosition = Mathf.Max(legsPosition,Vector3.Distance(a.position,b.position));
                        legsAngle = Mathf.Max(legsAngle,Quaternion.Angle(a.rotation,b.rotation));
                    }
                    var state = newWalk.animator.GetCurrentAnimatorStateInfo(1);
                    if (aim && frame > 2 * fps && !newWalk.animator.IsInTransition(1))
                    {
                        bool activeFire = newWalk.shooter.IsFiring;
                        if (previousHash != 0 && previousFire == activeFire && previousHash != state.shortNameHash) walkStateChanges++;
                        if (previousHash == state.shortNameHash && previousFire == activeFire)
                            Assert(state.normalizedTime + .00001f >= previousNormalized, "W reset the common upper-body animation time.");
                        previousHash=state.shortNameHash; previousNormalized=state.normalizedTime; previousFire=activeFire;
                    }
                    if (walk && frame % (fps*4) > fps / 2 && !newWalk.animator.IsInTransition(1) && !oldWalk.animator.IsInTransition(1))
                    {
                        Vector3 oldDelta = oldWalk.chest.InverseTransformPoint(oldWalk.gun.position) - oldStand.chest.InverseTransformPoint(oldStand.gun.position);
                        Vector3 newDelta = newWalk.chest.InverseTransformPoint(newWalk.gun.position) - newStand.chest.InverseTransformPoint(newStand.gun.position);
                        oldRelativePosition=Mathf.Max(oldRelativePosition,oldDelta.magnitude); newRelativePosition=Mathf.Max(newRelativePosition,newDelta.magnitude);
                        oldRelativeAngle=Mathf.Max(oldRelativeAngle,Quaternion.Angle(Quaternion.Inverse(oldWalk.chest.rotation)*oldWalk.gun.rotation, Quaternion.Inverse(oldStand.chest.rotation)*oldStand.gun.rotation));
                        newRelativeAngle=Mathf.Max(newRelativeAngle,Quaternion.Angle(Quaternion.Inverse(newWalk.chest.rotation)*newWalk.gun.rotation, Quaternion.Inverse(newStand.chest.rotation)*newStand.gun.rotation));
                    }
                }
                Report.AppendLine(fps + " fps: standing change=" + standingPosition + "m / " + standingAngle + "deg; walking legs change=" + legsPosition + "m / " + legsAngle + "deg; W-induced upper-state changes=" + walkStateChanges);
                Report.AppendLine("  Gun relative to chest, walking vs standing: before="+oldRelativePosition+"m / "+oldRelativeAngle+"deg; after="+newRelativePosition+"m / "+newRelativeAngle+"deg.");
                Assert(standingPosition < .00001f && standingAngle < .06f, "The user's standing pose changed.");
                Assert(legsPosition < .00001f && legsAngle < .06f, "Walking legs changed.");
                Assert(walkStateChanges == 0, "W still selects a different upper-body pose.");
                Assert(newRelativePosition < .012f && newRelativeAngle < 4f, "Walking did not retain the user's standing weapon placement.");
                oldStand.Destroy(); newStand.Destroy(); oldWalk.Destroy(); newWalk.Destroy();
            }
            File.WriteAllText("shared-standing-aim-results.txt",Report+"PASS. User-tuned standing aim/fire unchanged; common upper-body states persist across walking input; walking gait preserved.\n");
            UnityEngine.Object.DestroyImmediate(camera.gameObject); EditorApplication.Exit(0);
        }
        catch(Exception e) { File.WriteAllText("shared-standing-aim-results.txt",Report+e.ToString()); EditorApplication.Exit(1); }
    }

    static Actor Make(AnimatorController graph, Camera camera)
    {
        var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UserStandingFixture.prefab")); root.transform.position=Vector3.zero; root.transform.rotation=Quaternion.identity;
        var movement=root.GetComponent<PunisherMovement>(); movement.enabled=false;
        var a=movement.animator; a.runtimeAnimatorController=graph; a.applyRootMotion=false; a.cullingMode=AnimatorCullingMode.AlwaysAnimate; a.Rebind(); a.Update(0f);
        var ik=a.GetComponent<RifleHandIK>(); Call(ik,"Awake");
        var s=root.GetComponent<PunisherShooter>(); if(s==null)s=root.AddComponent<PunisherShooter>(); s.animator=a; s.aimingCamera=camera; s.showShotEffects=false; Call(s,"Awake");
        foreach(var c in root.GetComponentsInChildren<Collider>())c.enabled=false;
        Assert(Mathf.Abs(ik.supportHandSlideBack-.0175f)<.000001f && Mathf.Abs(ik.aimingRotationWeight-.063f)<.000001f && ik.minimumLeftElbowBend==82f && ik.maxAimCorrection==25f,"Fixture did not use the user's saved settings.");
        return new Actor{root=root,animator=a,ik=ik,shooter=s,gun=ik.weaponTransform,chest=a.GetBoneTransform(HumanBodyBones.Chest)};
    }
}
