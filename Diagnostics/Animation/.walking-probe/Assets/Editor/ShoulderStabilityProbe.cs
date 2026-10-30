using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ShoulderStabilityProbe
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly StringBuilder Report = new StringBuilder();
    static readonly HumanBodyBones[] Legs = { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
        HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
        HumanBodyBones.RightFoot, HumanBodyBones.RightToes };
    static readonly HumanBodyBones[] Upper = { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head,
        HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm };
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target,args);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    class Actor
    {
        public GameObject root;
        public Animator animator;
        public RifleHandIK ik;
        public PunisherShooter shooter;
        public void Step(bool walk,bool aim,bool fire,float now,float dt)
        {
            animator.SetBool("iswalking",walk);
            Call(shooter,"UpdateWeaponInput",aim,fire,now); Call(shooter,"WriteAnimationParameters"); Call(shooter,"UpdateLayerWeight",dt);
            animator.Update(dt);
            Call(shooter.weaponAnimation,"BindMarkers"); Call(shooter.weaponAnimation,"Advance",dt);
            Call(ik,"UpdateRig",dt); Call(shooter,"FireIfReady",now);
        }
        public void Dispose()
        {
            var reference=(RifleStandingAimPose)typeof(RifleHandIK).GetField("standingAimPose",Private).GetValue(ik);
            Call(ik,"OnDisable"); Check(reference==null,"Reference rig survived disable.");
            UnityEngine.Object.DestroyImmediate(shooter.weaponAnimation);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
    static Actor Make(Camera camera, bool stable)
    {
        var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UserStandingFixture.prefab"));
        root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        var m=root.GetComponent<PunisherMovement>(); m.enabled=false;
        var a=m.animator; a.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Production.controller");
        a.applyRootMotion=false; a.cullingMode=AnimatorCullingMode.AlwaysAnimate; a.Rebind(); a.Update(0f);
        var ik=a.GetComponent<RifleHandIK>(); ik.stabilizeMovingAim=stable; Call(ik,"Awake");
        var s=root.GetComponent<PunisherShooter>(); if(s==null)s=root.AddComponent<PunisherShooter>();
        s.animator=a; s.aimingCamera=camera; s.showShotEffects=false; Call(s,"Awake");
        foreach(var c in root.GetComponentsInChildren<Collider>())c.enabled=false;
        return new Actor{root=root,animator=a,ik=ik,shooter=s};
    }
    public static void Run()
    {
        try
        {
            var camera=new GameObject("Posture probe camera").AddComponent<Camera>();
            foreach(int fps in new[]{30,60,144})
            {
                var stand=Make(camera,false); var stableStand=Make(camera,true); var walk=Make(camera,false); var stableWalk=Make(camera,true);
                float dt=1f/fps, time=0f, standingPos=0,standingAngle=0,legsPos=0,legsAngle=0,oldChestPos=0,oldChestAngle=0,newChestPos=0,newChestAngle=0;
                float oldRollMin=1000,oldRollMax=-1000,newRollMin=1000,newRollMax=-1000,maxUpperJump=0;
                Vector3 previousChest=Vector3.zero; bool hadPrevious=false;
                for(int frame=0;frame<16*fps;frame++)
                {
                    time+=dt;
                    bool aim=frame>=fps && frame<14*fps;
                    bool fire=frame>=6*fps && frame<11*fps;
                    bool walking=frame>=3*fps && frame<5*fps || frame>=7*fps && frame<10*fps || frame>=12*fps;
                    camera.transform.position=new Vector3(.3f,.65f,-.7f);
                    Vector3 target=frame<11*fps?new Vector3(0,.62f,10):new Vector3(2*Mathf.Sin(time),.62f+2*Mathf.Sin(time*.8f),10);
                    camera.transform.LookAt(target);
                    stand.Step(false,aim,fire,time,dt);stableStand.Step(false,aim,fire,time,dt);
                    walk.Step(walking,aim,fire,time,dt);stableWalk.Step(walking,aim,fire,time,dt);
                    foreach(var bone in Upper)
                    {
                        var a=stand.animator.GetBoneTransform(bone);var b=stableStand.animator.GetBoneTransform(bone);
                        if(a==null || b==null)continue;
                        standingPos=Mathf.Max(standingPos,Vector3.Distance(a.position,b.position));standingAngle=Mathf.Max(standingAngle,Quaternion.Angle(a.rotation,b.rotation));
                    }
                    standingPos=Mathf.Max(standingPos,Vector3.Distance(stand.ik.weaponTransform.position,stableStand.ik.weaponTransform.position));
                    standingAngle=Mathf.Max(standingAngle,Quaternion.Angle(stand.ik.weaponTransform.rotation,stableStand.ik.weaponTransform.rotation));
                    foreach(var bone in Legs)
                    {
                        var a=walk.animator.GetBoneTransform(bone);var b=stableWalk.animator.GetBoneTransform(bone);
                        if(a==null || b==null)continue;
                        legsPos=Mathf.Max(legsPos,Vector3.Distance(a.position,b.position));legsAngle=Mathf.Max(legsAngle,Quaternion.Angle(a.rotation,b.rotation));
                    }
                    var chest=stableWalk.animator.GetBoneTransform(HumanBodyBones.Chest);
                    if(hadPrevious)maxUpperJump=Mathf.Max(maxUpperJump,Vector3.Distance(chest.position,previousChest));
                    previousChest=chest.position;hadPrevious=true;
                    if(walking && aim && !stableWalk.animator.IsInTransition(0) && frame<11*fps)
                    {
                        var a=stand.animator.GetBoneTransform(HumanBodyBones.Chest);var b=walk.animator.GetBoneTransform(HumanBodyBones.Chest);
                        oldChestPos=Mathf.Max(oldChestPos,Vector3.Distance(a.position,b.position));oldChestAngle=Mathf.Max(oldChestAngle,Quaternion.Angle(a.rotation,b.rotation));
                        newChestPos=Mathf.Max(newChestPos,Vector3.Distance(a.position,chest.position));newChestAngle=Mathf.Max(newChestAngle,Quaternion.Angle(a.rotation,chest.rotation));
                        float oldRoll=Mathf.Atan2(b.right.y,b.up.y)*Mathf.Rad2Deg,newRoll=Mathf.Atan2(chest.right.y,chest.up.y)*Mathf.Rad2Deg;
                        oldRollMin=Mathf.Min(oldRollMin,oldRoll);oldRollMax=Mathf.Max(oldRollMax,oldRoll);
                        newRollMin=Mathf.Min(newRollMin,newRoll);newRollMax=Mathf.Max(newRollMax,newRoll);
                    }
                }
                Report.AppendLine($"{fps}fps: standing delta {standingPos}m/{standingAngle}deg; hips+legs delta {legsPos}m/{legsAngle}deg.");
                Report.AppendLine($"  Walking chest vs standing before {oldChestPos}m/{oldChestAngle}deg; after {newChestPos}m/{newChestAngle}deg. Roll range {oldRollMax-oldRollMin} -> {newRollMax-newRollMin}deg. Max chest step {maxUpperJump}m.");
                File.WriteAllText("shoulder-stability-results.txt",Report.ToString());
                Check(standingPos<.00001f && standingAngle<.06f,"Standing pose changed.");
                Check(legsPos<.00001f && legsAngle<.06f,"Walking hips/legs changed.");
                Check(newChestAngle<1f && newChestPos<.012f,"Walking chest did not match standing.");
                Check(newRollMax-newRollMin<(oldRollMax-oldRollMin)*.35f,"Shoulder roll was not sufficiently reduced.");
                stand.Dispose();stableStand.Dispose();walk.Dispose();stableWalk.Dispose();
            }
            File.WriteAllText("shoulder-stability-results.txt",Report+"PASS. Standing pose, hips and legs preserved; moving shoulders stabilized; aim/fire, starts/stops, release and changing aim checked; reference rigs cleaned up.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText("shoulder-stability-results.txt",Report+e.ToString());EditorApplication.Exit(1);}
    }
}
