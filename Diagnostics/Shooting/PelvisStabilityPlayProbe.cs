using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PelvisStabilityPlayProbe
{
    const string Key="PelvisStabilityPlayProbe.Running";
    static PelvisStabilityPlayProbe(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Finish;}
    static void Finish()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        EditorApplication.update-=Finish; SessionState.SetBool(Key,false);
        EditorApplication.Exit(System.IO.File.ReadAllText("pelvis-stability-play-results.txt").Contains("PASS")?0:1);
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.transform.position=new Vector3(.3f,.65f,-.7f);camera.transform.LookAt(new Vector3(0,.62f,10));
        var driver=new GameObject("Play posture driver").AddComponent<PelvisStabilityDriver>();driver.camera=camera;
        driver.actors=new GameObject[4];
        for(int i=0;i<4;i++)
        {
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UserStandingFixture.prefab"));
            root.name="Posture actor "+i; root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var movement=root.GetComponent<PunisherMovement>();movement.enabled=false;
            movement.animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Production.controller");
            movement.animator.applyRootMotion=false;movement.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            root.GetComponentInChildren<RifleHandIK>().stabilizeMovingAim=i%2==1;
            foreach(var c in root.GetComponentsInChildren<ThirdPersonCameraController>())c.enabled=false;
            foreach(var c in root.GetComponentsInChildren<Collider>())c.enabled=false;
            var shooter=root.GetComponent<PunisherShooter>();if(shooter==null)shooter=root.AddComponent<PunisherShooter>();
            shooter.animator=movement.animator;shooter.aimingCamera=camera;shooter.showShotEffects=false;shooter.enabled=false;
            driver.actors[i]=root;
        }
        SessionState.SetBool(Key,true); EditorApplication.update+=Finish; EditorApplication.isPlaying=true;
    }
}
