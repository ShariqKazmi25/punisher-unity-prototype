using System;
using System.IO;
using System.Reflection;
using UnityEngine;

[DefaultExecutionOrder(300)]
public sealed class ShoulderStabilityDriver : MonoBehaviour
{
    public GameObject[] actors;
    public Camera camera;
    Animator[] animators;
    RifleHandIK[] rigs;
    PunisherShooter[] shooters;
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private).Invoke(target,args);
    int frame, shots;
    bool completed;
    float standingPosition,standingAngle,legsPosition,legsAngle,chestPosition,chestAngle,gunPosition,gunAngle;
    static readonly HumanBodyBones[] Legs={HumanBodyBones.Hips,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot};
    static readonly HumanBodyBones[] Upper={HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.LeftShoulder,HumanBodyBones.RightShoulder,HumanBodyBones.Head,HumanBodyBones.LeftHand,HumanBodyBones.RightHand};
    void Start()
    {
        Time.captureDeltaTime=1f/60;animators=new Animator[4];rigs=new RifleHandIK[4];shooters=new PunisherShooter[4];
        for(int i=0;i<4;i++)
        {
            rigs[i]=actors[i].GetComponentInChildren<RifleHandIK>();animators[i]=rigs[i].GetComponent<Animator>();
            shooters[i]=actors[i].GetComponent<PunisherShooter>();shooters[i].ShotFired+=_=>shots++;
        }
    }
    void Update()
    {
        if(completed||animators==null)return;
        frame++;
        bool walk=frame>=60 && frame<300 || frame>=330 && frame<400 || frame>=405;
        bool aim=frame>=120 && frame<480;
        bool fire=frame>=180 && frame<280 || frame>=360 && frame<420;
        Vector3 position=frame<300?Vector3.zero:new Vector3((frame-300)*.001f,0,(frame-300)*.002f);
        Quaternion rotation=Quaternion.Euler(0,frame<300?0:(frame-300)*.2f,0);
        for(int i=0;i<4;i++)
        {
            actors[i].transform.SetPositionAndRotation(position,rotation);
            animators[i].SetBool("iswalking",i>=2 && walk);
            Call(shooters[i],"UpdateWeaponInput",aim,fire,Time.time);Call(shooters[i],"WriteAnimationParameters");Call(shooters[i],"UpdateLayerWeight",Time.deltaTime);
        }
        camera.transform.position=position+rotation*new Vector3(.3f,.65f,-.7f);
        camera.transform.LookAt(position+rotation*new Vector3(0,.62f+(frame<300?0:Mathf.Sin(frame*.02f)*2),10));
    }
    void LateUpdate()
    {
        if(completed||animators==null)return;
        try
        {
            for(int i=0;i<4;i++)Call(shooters[i],"FireIfReady",Time.time);
            foreach(var bone in Upper)
            {
                var a=animators[0].GetBoneTransform(bone);var b=animators[1].GetBoneTransform(bone);if(a==null||b==null)continue;
                standingPosition=Mathf.Max(standingPosition,Vector3.Distance(a.position,b.position));standingAngle=Mathf.Max(standingAngle,Quaternion.Angle(a.rotation,b.rotation));
            }
            foreach(var bone in Legs)
            {
                var a=animators[2].GetBoneTransform(bone);var b=animators[3].GetBoneTransform(bone);if(a==null||b==null)continue;
                legsPosition=Mathf.Max(legsPosition,Vector3.Distance(a.position,b.position));legsAngle=Mathf.Max(legsAngle,Quaternion.Angle(a.rotation,b.rotation));
            }
            if(frame>140 && frame<300 && !animators[3].IsInTransition(0) && animators[3].GetLayerWeight(1)>.999f)
            {
                var a=animators[0].GetBoneTransform(HumanBodyBones.Chest);var b=animators[3].GetBoneTransform(HumanBodyBones.Chest);
                chestPosition=Mathf.Max(chestPosition,Vector3.Distance(a.position,b.position));chestAngle=Mathf.Max(chestAngle,Quaternion.Angle(a.rotation,b.rotation));
                gunPosition=Mathf.Max(gunPosition,Vector3.Distance(rigs[0].weaponTransform.position,rigs[3].weaponTransform.position));gunAngle=Mathf.Max(gunAngle,Quaternion.Angle(rigs[0].weaponTransform.rotation,rigs[3].weaponTransform.rotation));
            }
            if(frame==440)rigs[3].enabled=false;
            if(frame==445)rigs[3].enabled=true;
            if(frame<540)return;
            string result=$"Real Play Mode, 540 frames: standing delta {standingPosition}m/{standingAngle}deg; hips/legs delta {legsPosition}m/{legsAngle}deg.\nWalking chest vs standing {chestPosition}m/{chestAngle}deg; rifle {gunPosition}m/{gunAngle}deg; accepted rounds across actors {shots}.\n";
            if(standingPosition>.00001f || standingAngle>.06f || legsPosition>.00001f || legsAngle>.06f || chestPosition>.012f || chestAngle>1f || gunPosition>.02f || gunAngle>4f || shots<20)
                throw new Exception(result+"Play Mode posture assertions failed.");
            File.WriteAllText("shoulder-stability-play-results.txt",result+"PASS. Production callbacks: walking before aiming, standing/moving aim/fire, rapid movement restart, changing camera pitch and character heading/position, release, disable/re-enable.\n");
            completed=true;UnityEditor.EditorApplication.isPlaying=false;
        }
        catch(Exception e){File.WriteAllText("shoulder-stability-play-results.txt",e.ToString());completed=true;UnityEditor.EditorApplication.isPlaying=false;}
    }
}
