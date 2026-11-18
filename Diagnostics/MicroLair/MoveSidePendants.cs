var root=UnityEngine.GameObject.Find("MicroLair_Graybox").transform;var group=root.Find("12_SurfacePolish");float s=root.Find(MicroLairRevisionB.MarkerName).localScale.x;
if(UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Exit Play mode first");
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(root.gameObject.scene,"Diagnostics/MicroLair/MicroLair_before_side_hub_pendants.unity",true);
var children=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Cast<UnityEngine.Transform>(group));
foreach(var t in children){
 bool fixture=t.name=="Pendant_Stem"||t.name=="Pendant_Shade"||t.name=="Warm_Pendant_Lens"||t.name=="Aisle_Warm_Pool";
 if(fixture){
  var p=t.position/s;
  if(UnityEngine.Mathf.Abs(p.z-4.3f)>.1f){UnityEditor.Undo.DestroyObjectImmediate(t.gameObject);continue;}
  UnityEditor.Undo.RecordObject(t,"Move pendant inside side hub");p.x=UnityEngine.Mathf.Sign(p.x)*8;t.position=p*s;
  if(t.TryGetComponent<UnityEngine.Light>(out var light)){
   UnityEditor.Undo.RecordObject(light,"Consolidate side hub lighting");t.name=p.x<0?"LeftHub_Warm_Pendant":"RightHub_Warm_Pendant";
   light.intensity=10;light.range=12*s;light.spotAngle=115;light.innerSpotAngle=65;light.shadows=UnityEngine.LightShadows.Soft;
   light.shadowBias=.025f;light.shadowNormalBias=.05f;t.LookAt(new UnityEngine.Vector3(p.x*.8f,1,5)*s);
  }
 }
 if(t.name.StartsWith("SideHub_Spill_"))UnityEditor.Undo.DestroyObjectImmediate(t.gameObject);
}
if(System.Linq.Enumerable.Count(group.GetComponentsInChildren<UnityEngine.Transform>(),t=>t.name=="Warm_Pendant_Lens")!=2)throw new System.Exception("Expected two pendants");
foreach(var p in group.GetComponentsInChildren<UnityEngine.ReflectionProbe>()){
 var path=UnityEditor.AssetDatabase.GetAssetPath(p.customBakedTexture);p.mode=UnityEngine.Rendering.ReflectionProbeMode.Baked;
 if(!UnityEditor.Lightmapping.BakeReflectionProbe(p,path))throw new System.Exception("Bake failed");UnityEditor.AssetDatabase.ImportAsset(path);p.mode=UnityEngine.Rendering.ReflectionProbeMode.Custom;p.customBakedTexture=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Cubemap>(path);
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(root.gameObject.scene);
return "Saved: one warm pendant inside each side hub, zero corridor pendants; redundant side-hub spot sources consolidated.";
