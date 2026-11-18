var group=UnityEngine.GameObject.Find("MicroLair_Graybox/12_SurfacePolish").transform;
foreach(string name in new[]{"Hub_Fluorescent","Pendant_Lens"}){
 var m=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Environment/MicroLair/SurfacePolish/"+name+".mat");
 m.shader=UnityEngine.Shader.Find("Universal Render Pipeline/Unlit");m.SetColor("_BaseColor",name=="Pendant_Lens"?new UnityEngine.Color(3.5f,2.7f,1.4f,1):new UnityEngine.Color(2.1f,2.8f,3.2f,1));UnityEditor.EditorUtility.SetDirty(m);
}
foreach(var p in group.GetComponentsInChildren<UnityEngine.ReflectionProbe>()){
 string path="Assets/Environment/MicroLair/SurfacePolish/"+p.name.Replace("Local_Reflection_","Reflection_")+".exr";
 p.mode=UnityEngine.Rendering.ReflectionProbeMode.Baked;
 if(!UnityEditor.Lightmapping.BakeReflectionProbe(p,path))throw new System.Exception("Reflection bake failed: "+path);
 UnityEditor.AssetDatabase.ImportAsset(path,UnityEditor.ImportAssetOptions.ForceSynchronousImport);
 p.mode=UnityEngine.Rendering.ReflectionProbeMode.Custom;p.customBakedTexture=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Cubemap>(path);
 if(!p.customBakedTexture)throw new System.Exception("Missing cubemap");
}
UnityEditor.AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(group.gameObject.scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(group.gameObject.scene);
return "Reflection cubemaps persisted; fixture lenses illuminated.";
