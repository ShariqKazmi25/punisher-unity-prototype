var root=UnityEngine.GameObject.Find("MicroLair_Graybox").transform;
var group=root.Find("12_SurfacePolish");float s=root.Find(MicroLairRevisionB.MarkerName).localScale.x;
if(UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Exit play mode first");
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(root.gameObject.scene,"Diagnostics/MicroLair/MicroLair_before_brighter_hubs.unity",true);
foreach(var l in group.GetComponentsInChildren<UnityEngine.Light>()){
 if(l.name=="Hub_Cool_Downlight")l.intensity=8;
 if(l.name=="Hub_Ceiling_Bounce")l.intensity=1.2f;
 if(l.name=="Aisle_Warm_Pool")l.intensity=1.2f;
}
foreach(float x in new[]{-8f,8f})foreach(float z in new[]{1f,8f}){
 string name="SideHub_Spill_"+x+"_"+z;
 var existing=group.Find(name);var go=existing?existing.gameObject:new UnityEngine.GameObject(name);go.transform.SetParent(group,false);
 go.transform.position=new UnityEngine.Vector3(x,5.65f,z)*s;go.transform.LookAt(new UnityEngine.Vector3(x*.72f,1,z+1)*s);
 var l=go.GetComponent<UnityEngine.Light>();if(!l)l=go.AddComponent<UnityEngine.Light>();l.type=UnityEngine.LightType.Spot;l.intensity=6.5f;
 l.color=new UnityEngine.Color(1,.86f,.66f);l.spotAngle=110;l.innerSpotAngle=65;l.range=9*s;l.shadows=UnityEngine.LightShadows.Soft;l.shadowBias=.025f;l.shadowNormalBias=.05f;
}
var profile=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Environment/MicroLair/SurfacePolish/LairGrade.asset");
profile.components.RemoveAll(c=>c==null);
if(!profile.TryGet<UnityEngine.Rendering.Universal.Tonemapping>(out var tone))tone=profile.Add<UnityEngine.Rendering.Universal.Tonemapping>();tone.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);
if(!profile.TryGet<UnityEngine.Rendering.Universal.ColorAdjustments>(out var color))color=profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>();color.postExposure.Override(.2f);color.contrast.Override(6);color.saturation.Override(-5);
if(!profile.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var bloom))bloom=profile.Add<UnityEngine.Rendering.Universal.Bloom>();bloom.intensity.Override(.15f);bloom.threshold.Override(1);bloom.scatter.Override(.55f);
foreach(var c in profile.components){if(!UnityEditor.AssetDatabase.Contains(c))UnityEditor.AssetDatabase.AddObjectToAsset(c,profile);UnityEditor.EditorUtility.SetDirty(c);}
UnityEditor.EditorUtility.SetDirty(profile);UnityEditor.AssetDatabase.SaveAssets();
foreach(var p in group.GetComponentsInChildren<UnityEngine.ReflectionProbe>()){
 string path=UnityEditor.AssetDatabase.GetAssetPath(p.bakedTexture);
 if(!UnityEditor.Lightmapping.BakeReflectionProbe(p,path))throw new System.Exception("Reflection bake failed");
 UnityEditor.AssetDatabase.ImportAsset(path);p.bakedTexture=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture>(path);
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(root.gameObject.scene);
return "Brighter central and side hubs saved; direct corridor lighting reduced.";
