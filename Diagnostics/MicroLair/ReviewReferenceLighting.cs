var profile=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Environment/MicroLair/SurfacePolish/LairGrade.asset");
profile.components.RemoveAll(c=>c==null);
foreach(var component in profile.components){
 if(!UnityEditor.AssetDatabase.Contains(component))UnityEditor.AssetDatabase.AddObjectToAsset(component,profile);
 UnityEditor.EditorUtility.SetDirty(component);
}
UnityEditor.EditorUtility.SetDirty(profile);
UnityEditor.AssetDatabase.SaveAssets();
var root=UnityEngine.GameObject.Find("MicroLair_Graybox").transform;
float s=root.Find(MicroLairRevisionB.MarkerName).localScale.x;
MicroLairSurfacePolish.Capture(root,s,"reference-lighting-front.png");
var go=new UnityEngine.GameObject("Temporary_SurfaceReview");
var camera=go.AddComponent<UnityEngine.Camera>();camera.enabled=false;camera.allowHDR=true;camera.nearClipPlane=.03f;camera.farClipPlane=50;
var data=go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();data.renderPostProcessing=true;
try{
 camera.transform.position=new UnityEngine.Vector3(4.4f,2.75f,-6.6f)*s;camera.transform.LookAt(new UnityEngine.Vector3(4.4f,2.9f,5)*s);camera.fieldOfView=65;
 var render=typeof(MicroLairRevisionC).GetMethod("Render",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
 render.Invoke(null,new object[]{camera,"reference-lighting-aisle.png"});
 camera.transform.position=new UnityEngine.Vector3(.5f,2.6f,-4.7f)*s;camera.transform.LookAt(new UnityEngine.Vector3(0,1.1f,-1.2f)*s);camera.fieldOfView=55;
 render.Invoke(null,new object[]{camera,"reference-material-closeup.png"});
}finally{UnityEngine.Object.DestroyImmediate(go);}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(root.gameObject.scene);
return new { savedProfileComponents=profile.components.Count, allPersistent=System.Linq.Enumerable.All(profile.components,c=>UnityEditor.AssetDatabase.Contains(c)), lights=root.Find("12_SurfacePolish").GetComponentsInChildren<UnityEngine.Light>().Length, shadowedLights=System.Linq.Enumerable.Count(root.Find("12_SurfacePolish").GetComponentsInChildren<UnityEngine.Light>(),l=>l.shadows!=UnityEngine.LightShadows.None), hdr=UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset.supportsHDR };
