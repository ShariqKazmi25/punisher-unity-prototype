using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PelvisStabilityProbe
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly StringBuilder Report = new StringBuilder();
    static readonly HumanBodyBones[] Legs = { HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
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
    static Actor Make(Camera camera, bool stable, float pelvis = 1f)
    {
        var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UserStandingFixture.prefab"));
        root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        var m=root.GetComponent<PunisherMovement>(); m.enabled=false;
        var a=m.animator; a.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Production.controller");
        a.applyRootMotion=false; a.cullingMode=AnimatorCullingMode.AlwaysAnimate; a.Rebind(); a.Update(0f);
        var ik=a.GetComponent<RifleHandIK>(); ik.stabilizeMovingAim=stable; ik.movingPelvisStability=pelvis; Call(ik,"Awake");
        var s=root.GetComponent<PunisherShooter>(); if(s==null)s=root.AddComponent<PunisherShooter>();
        s.animator=a; s.aimingCamera=camera; s.showShotEffects=false; Call(s,"Awake");
        foreach(var c in root.GetComponentsInChildren<Collider>())c.enabled=false;
        return new Actor{root=root,animator=a,ik=ik,shooter=s};
    }
    public static void Render()
    {
        try
        {
            var aimCamera=new GameObject("Aim target").AddComponent<Camera>();aimCamera.transform.position=new Vector3(.3f,.65f,-.7f);aimCamera.transform.LookAt(new Vector3(0,.62f,10));
            var before=Make(aimCamera,true,0);var after=Make(aimCamera,true,1);
            var light=new GameObject("Pose light").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(25,-30,0);RenderSettings.ambientLight=Color.white*.65f;
            foreach(var actor in new[]{before,after})
                foreach(var renderer in actor.root.GetComponentsInChildren<Renderer>())
                {
                    var material=new Material(Shader.Find("Standard"));material.color=renderer.transform.IsChildOf(actor.ik.weaponTransform)?new Color(.2f,.5f,.65f):new Color(.55f,.5f,.45f);
                    var materials=renderer.sharedMaterials;for(int m=0;m<materials.Length;m++)materials[m]=material;renderer.sharedMaterials=materials;
                }
            var camera=new GameObject("Render camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.42f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.14f,.17f);
            for(int frame=0;frame<=366;frame++)
            {
                bool walk=frame>=60,aim=frame>=30,fire=frame>=340;
                before.Step(walk,aim,fire,frame/60f,1f/60);after.Step(walk,aim,fire,frame/60f,1f/60);
                if(frame!=330 && frame!=342 && frame!=354 && frame!=366)continue;
                foreach(bool side in new[]{false,true})
                {
                    camera.transform.position=side?new Vector3(1.3f,.48f,.05f):new Vector3(0,.48f,-1.3f);camera.transform.LookAt(new Vector3(0,.4f,.025f));
                    foreach(bool fixedPose in new[]{false,true})
                    {
                        foreach(var renderer in before.root.GetComponentsInChildren<Renderer>())renderer.enabled=!fixedPose;
                        foreach(var renderer in after.root.GetComponentsInChildren<Renderer>())renderer.enabled=fixedPose;
                        var rt=new RenderTexture(480,720,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                        var image=new Texture2D(480,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,480,720),0,0);image.Apply();
                        File.WriteAllBytes("pelvis-"+(fixedPose?"after":"before")+"-"+frame+(side?"-side":"-back")+".png",image.EncodeToPNG());
                        RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
                    }
                }
            }
            before.Dispose();after.Dispose();EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText("pelvis-render-error.txt",e.ToString());EditorApplication.Exit(1);}
    }
    public static void Run()
    {
        try
        {
            var camera=new GameObject("Posture probe camera").AddComponent<Camera>();
            foreach(int fps in new[]{30,60,144})
            {
                var stand=Make(camera,false); var stableStand=Make(camera,true); var walk=Make(camera,true,0f); var stableWalk=Make(camera,true);
                float dt=1f/fps, time=0f, standingPos=0,standingAngle=0,legsPos=0,legsAngle=0,oldChestPos=0,oldChestAngle=0,newChestPos=0,newChestAngle=0;
                float oldRollMin=1000,oldRollMax=-1000,newRollMin=1000,newRollMax=-1000,maxUpperJump=0;
                float hipsAngle=0, hipsHorizontal=0, maxDrop=0, kneeChange=0, lengthError=0, waistAngle=0, waistLength=0;
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
                    foreach(bool right in new[]{false,true})
                    {
                        var thigh=right?HumanBodyBones.RightUpperLeg:HumanBodyBones.LeftUpperLeg;
                        var shin=right?HumanBodyBones.RightLowerLeg:HumanBodyBones.LeftLowerLeg;
                        var ankle=right?HumanBodyBones.RightFoot:HumanBodyBones.LeftFoot;
                        Transform a=walk.animator.GetBoneTransform(thigh),b=walk.animator.GetBoneTransform(shin),c=walk.animator.GetBoneTransform(ankle);
                        Transform x=stableWalk.animator.GetBoneTransform(thigh),y=stableWalk.animator.GetBoneTransform(shin),z=stableWalk.animator.GetBoneTransform(ankle);
                        lengthError=Mathf.Max(lengthError,Mathf.Abs(Vector3.Distance(a.position,b.position)-Vector3.Distance(x.position,y.position)),Mathf.Abs(Vector3.Distance(b.position,c.position)-Vector3.Distance(y.position,z.position)));
                        kneeChange=Mathf.Max(kneeChange,Vector3.Distance(b.position,y.position));
                    }
                    var chest=stableWalk.animator.GetBoneTransform(HumanBodyBones.Chest);
                    if(hadPrevious)maxUpperJump=Mathf.Max(maxUpperJump,Vector3.Distance(chest.position,previousChest));
                    previousChest=chest.position;hadPrevious=true;
                    if(walking && aim && !stableWalk.animator.IsInTransition(0) && frame<11*fps)
                    {
                        var a=stand.animator.GetBoneTransform(HumanBodyBones.Chest);var b=walk.animator.GetBoneTransform(HumanBodyBones.Chest);
                        oldChestPos=Mathf.Max(oldChestPos,Vector3.Distance(a.position,b.position));oldChestAngle=Mathf.Max(oldChestAngle,Quaternion.Angle(a.rotation,b.rotation));
                        newChestPos=Mathf.Max(newChestPos,Vector3.Distance(a.position,chest.position));newChestAngle=Mathf.Max(newChestAngle,Quaternion.Angle(a.rotation,chest.rotation));
                        var oldHips=walk.animator.GetBoneTransform(HumanBodyBones.Hips);var newHips=stableWalk.animator.GetBoneTransform(HumanBodyBones.Hips);var standingHips=stand.animator.GetBoneTransform(HumanBodyBones.Hips);
                        var spine=stableWalk.animator.GetBoneTransform(HumanBodyBones.Spine);var standingSpine=stand.animator.GetBoneTransform(HumanBodyBones.Spine);
                        hipsAngle=Mathf.Max(hipsAngle,Quaternion.Angle(newHips.rotation,standingHips.rotation));
                        hipsHorizontal=Mathf.Max(hipsHorizontal,Mathf.Abs(newHips.position.x-standingHips.position.x));
                        maxDrop=Mathf.Max(maxDrop,oldHips.position.y-newHips.position.y);
                        waistAngle=Mathf.Max(waistAngle,Quaternion.Angle(Quaternion.Inverse(newHips.rotation)*spine.rotation,Quaternion.Inverse(standingHips.rotation)*standingSpine.rotation));
                        waistLength=Mathf.Max(waistLength,Vector3.Distance(newHips.InverseTransformPoint(spine.position),standingHips.InverseTransformPoint(standingSpine.position)));
                        float oldRoll=Mathf.Atan2(oldHips.right.y,oldHips.up.y)*Mathf.Rad2Deg,newRoll=Mathf.Atan2(newHips.right.y,newHips.up.y)*Mathf.Rad2Deg;
                        oldRollMin=Mathf.Min(oldRollMin,oldRoll);oldRollMax=Mathf.Max(oldRollMax,oldRoll);
                        newRollMin=Mathf.Min(newRollMin,newRoll);newRollMax=Mathf.Max(newRollMax,newRoll);
                    }
                }
                Report.AppendLine($"{fps}fps: standing delta {standingPos}m/{standingAngle}deg; animated feet/toes delta {legsPos}m/{legsAngle}deg; leg length error {lengthError}m; knee adjustment {kneeChange}m.");
                Report.AppendLine($"  Pelvis roll range {oldRollMax-oldRollMin} -> {newRollMax-newRollMin}deg; pelvis vs standing rotation {hipsAngle}deg, lateral {hipsHorizontal}m; extra stance lowering {maxDrop}m. Waist rotation/length difference {waistAngle}deg/{waistLength}m; chest rotation {newChestAngle}deg. Max chest step {maxUpperJump}m.");
                File.WriteAllText("pelvis-stability-results.txt",Report.ToString());
                Check(standingPos<.00001f && standingAngle<.06f,"Standing pose changed.");
                Check(legsPos<.00015f && legsAngle<.06f,"Animated footfalls changed.");
                Check(lengthError<.00001f,"Leg bones stretched.");
                Check(hipsAngle<.1f && hipsHorizontal<.001f,"Pelvis still sways relative to standing.");
                Check(waistAngle<.1f && waistLength<.0001f,"Abdomen still bends/stretches against the pelvis.");
                Check(maxDrop<.05f && newChestAngle<1f,"Unnatural torso drop/rotation.");
                Check(newRollMax-newRollMin<(oldRollMax-oldRollMin)*.35f,"Pelvis roll was not sufficiently reduced.");
                stand.Dispose();stableStand.Dispose();walk.Dispose();stableWalk.Dispose();
            }
            File.WriteAllText("pelvis-stability-results.txt",Report+"PASS. Standing pose and animated footfalls retained; pelvis/waist stable; leg lengths unchanged; aim/fire, starts/stops, release and changing aim checked.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText("pelvis-stability-results.txt",Report+e.ToString());EditorApplication.Exit(1);}
    }
}
