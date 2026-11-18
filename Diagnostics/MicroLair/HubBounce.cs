var root=UnityEngine.GameObject.Find("MicroLair_Graybox").transform;var group=root.Find("12_SurfacePolish");float s=root.Find(MicroLairRevisionB.MarkerName).localScale.x;
foreach(float x in new[]{0f,-8f,8f})foreach(float z in new[]{2f,8f}){
 var go=new UnityEngine.GameObject("Hub_ReflectedFill");go.transform.SetParent(group,false);go.transform.position=new UnityEngine.Vector3(x,3.8f,z)*s;
 var l=go.AddComponent<UnityEngine.Light>();l.type=UnityEngine.LightType.Point;l.intensity=x==0?1.1f:.85f;l.range=6*s;l.color=x==0?new UnityEngine.Color(.62f,.86f,1):new UnityEngine.Color(1,.86f,.66f);
}
var lens=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Environment/MicroLair/SurfacePolish/Pendant_Lens.mat");lens.SetColor("_BaseColor",new UnityEngine.Color(1,.78f,.38f,1));UnityEditor.EditorUtility.SetDirty(lens);
foreach(var p in group.GetComponentsInChildren<UnityEngine.ReflectionProbe>()){
 string path=UnityEditor.AssetDatabase.GetAssetPath(p.customBakedTexture);p.mode=UnityEngine.Rendering.ReflectionProbeMode.Baked;
 if(!UnityEditor.Lightmapping.BakeReflectionProbe(p,path))throw new System.Exception("Bake failed");UnityEditor.AssetDatabase.ImportAsset(path);p.mode=UnityEngine.Rendering.ReflectionProbeMode.Custom;p.customBakedTexture=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Cubemap>(path);
}
UnityEditor.AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(root.gameObject.scene);return "Soft hub bounce saved";
