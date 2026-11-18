using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class MicroLairSurfacePolish
{
 const string Folder="Assets/Environment/MicroLair/SurfacePolish";
 const string Reports="Documentation/MicroLair/";
 [MenuItem("Tools/Micro Lair/Polish Materials and Lighting")]
 public static void Apply()
 {
  Directory.CreateDirectory(Folder); Directory.CreateDirectory(Reports);
  var scene=SceneManager.GetSceneByPath(MicroLairGrayboxBuilder.ScenePath);
  if(!scene.isLoaded) scene=EditorSceneManager.OpenScene(MicroLairGrayboxBuilder.ScenePath);
  SceneManager.SetActiveScene(scene);
  var root=scene.GetRootGameObjects().First(g=>g.name=="MicroLair_Graybox").transform;
  float s=root.Find(MicroLairRevisionB.MarkerName).localScale.x;
  const string backup="Diagnostics/MicroLair/MicroLair_before_reference_lighting.unity";
  if(!File.Exists(backup)) EditorSceneManager.SaveScene(scene,backup,true);
  if(!File.Exists(Reports+"surface-before.png")) Capture(root,s,"surface-before.png");
  var geometry=root.GetComponentsInChildren<MeshFilter>(true).ToDictionary(x=>x,x=>x.sharedMesh);
  var collision=root.GetComponentsInChildren<Collider>(true).ToDictionary(x=>x,x=>EditorJsonUtility.ToJson(x));
  var map=new Dictionary<Material,Material>();
  string[] names={"concrete floor","concrete_wall_006","brushed concrete","rusty metal"};
  float[] lifts={.17f,.08f,.12f,.15f}, bumps={1.2f,1.1f,1.1f,.8f};
  for(int i=0;i<names.Length;i++) {
   var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/"+names[i]+".mat");
   if(!source) throw new Exception("Missing material "+names[i]);
   foreach(string prop in new[]{"_BaseMap","_BumpMap","_MetallicGlossMap","_OcclusionMap","_ParallaxMap"}) {
    var tex=source.GetTexture(prop);if(tex)FullResolution(AssetDatabase.GetAssetPath(tex),i==0?8192:4096);
   }
   string path=Folder+"/"+names[i]+" - Lair.mat";
   var m=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(!m) {m=new Material(source);AssetDatabase.CreateAsset(m,path);}
   else EditorUtility.CopySerialized(source,m);
   m.name=names[i]+" - Lair";
   var data=Pack(source.GetTexture("_MetallicGlossMap"),names[i],lifts[i],i==0?8192:4096);
   m.SetTexture("_MetallicGlossMap",data);m.SetTexture("_OcclusionMap",data);
   m.SetFloat("_Smoothness",1);m.SetFloat("_BumpScale",bumps[i]);m.SetFloat("_OcclusionStrength",.85f);
   m.SetFloat("_EnvironmentReflections",1);m.SetFloat("_SpecularHighlights",1);
   if(i==0) m.SetFloat("_Parallax",.006f);
   if(i==3) m.SetColor("_BaseColor",new Color(.58f,.58f,.58f,1));
   // URP samples these maps through BaseMap UVs; keep inspector scales consistent.
   foreach(var prop in new[]{"_MainTex","_BumpMap","_MetallicGlossMap","_OcclusionMap","_ParallaxMap"}) {
    m.SetTextureScale(prop,m.GetTextureScale("_BaseMap"));m.SetTextureOffset(prop,m.GetTextureOffset("_BaseMap"));
   }
   m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
   EditorUtility.SetDirty(m);map[source]=m;
  }
  int count=0;
  foreach(var r in root.GetComponentsInChildren<Renderer>(true)) {
   var a=r.sharedMaterials; bool changed=false;
   for(int i=0;i<a.Length;i++) {
    if(map.TryGetValue(a[i],out var m)) {a[i]=m;changed=true;}
    else if(a[i] && a[i].name.EndsWith(" - Lair")) {var same=map.Values.FirstOrDefault(v=>v.name==a[i].name);if(same){a[i]=same;changed=true;}}
   }
   if(changed){r.sharedMaterials=a;count++;}
  }
  foreach(var l in root.Find("09_InspectionCameraAndLights").GetComponentsInChildren<Light>(true)) l.enabled=false;
  var previous=root.Find("12_SurfacePolish");if(previous) Object.DestroyImmediate(previous.gameObject);
  var group=new GameObject("12_SurfacePolish").transform;group.SetParent(root,false);
  RenderSettings.ambientMode=AmbientMode.Trilight;
  RenderSettings.ambientSkyColor=new Color(.13f,.18f,.21f);
  RenderSettings.ambientEquatorColor=new Color(.075f,.10f,.12f);
  RenderSettings.ambientGroundColor=new Color(.035f,.045f,.05f);
  RenderSettings.reflectionIntensity=1;RenderSettings.sun=null;
  MicroLairReferenceLights.Build(group,s);
  var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder+"/LairGrade.asset");
  if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Folder+"/LairGrade.asset");}
  if(!profile.TryGet<Tonemapping>(out var tone))tone=profile.Add<Tonemapping>();tone.mode.Override(TonemappingMode.ACES);
  if(!profile.TryGet<ColorAdjustments>(out var color))color=profile.Add<ColorAdjustments>();
  color.postExposure.Override(.1f);color.contrast.Override(8);color.saturation.Override(-5);
  if(!profile.TryGet<Bloom>(out var bloom))bloom=profile.Add<Bloom>();
  bloom.threshold.Override(1);bloom.intensity.Override(.15f);bloom.scatter.Override(.55f);
  var vol=group.gameObject.AddComponent<Volume>();vol.isGlobal=true;vol.priority=20;vol.sharedProfile=profile;
  foreach(var camera in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true))) {
   camera.allowHDR=true;var cd=camera.GetUniversalAdditionalCameraData();cd.renderPostProcessing=true;cd.volumeLayerMask|=1;
  }
  AssetDatabase.SaveAssets();
  var gameplay=scene.GetRootGameObjects().FirstOrDefault(g=>g.name==MicroLairGameplaySetup.RigName);
  bool active=gameplay && gameplay.activeSelf;if(gameplay)gameplay.SetActive(false);
  try {foreach(float z in new[]{-5f,8f}) {
   var go=new GameObject("Local_Reflection_"+z);go.transform.SetParent(group,false);go.transform.position=new Vector3(0,3,z)*s;
   var p=go.AddComponent<ReflectionProbe>();p.mode=ReflectionProbeMode.Baked;p.resolution=512;p.hdr=true;
   p.boxProjection=true;p.size=new Vector3(22,7,20)*s;p.center=Vector3.zero;p.blendDistance=s;p.nearClipPlane=.03f;p.farClipPlane=35*s;p.intensity=1;
   string path=Folder+"/Reflection_"+z+".exr";
   if(!Lightmapping.BakeReflectionProbe(p,path))throw new Exception("Reflection bake failed");
   AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);p.bakedTexture=AssetDatabase.LoadAssetAtPath<Texture>(path);
   if(!p.bakedTexture)throw new Exception("Missing baked reflection");
  }}finally{if(gameplay)gameplay.SetActive(active);}
  if(!geometry.All(x=>x.Key.sharedMesh==x.Value)||!collision.All(x=>EditorJsonUtility.ToJson(x.Key)==x.Value))throw new Exception("Geometry or collision changed");
  EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  Capture(root,s,"surface-after.png");
  var report=new System.Text.StringBuilder($"PASS: {count} environment renderers use scene-specific material variants; original materials preserved.\nPASS: existing structural meshes and colliders unchanged.\nTwo 512px baked HDR box-projected reflection probes. Cool fluorescent hub, warm aisle pendants, muted foreground.\nACES and subtle bloom on graybox cameras. Six window rows, doors and gameplay scripts unchanged.\n");
  foreach(var m in map.Values)foreach(var prop in new[]{"_BaseMap","_BumpMap","_MetallicGlossMap","_ParallaxMap"}){
   var tex=m.GetTexture(prop);if(!tex)continue;int expected=m.name.StartsWith("concrete floor")?8192:4096;
   if(tex.width!=expected||tex.height!=expected)throw new Exception("Resolution mismatch: "+m.name+"/"+prop+" "+tex.width);
   report.AppendLine($"PASS {m.name}/{prop}: {tex.width}x{tex.height}");
  }
  File.WriteAllText(Reports+"surface-polish-validation.txt",report.ToString());
 }
 static void Spot(Transform parent,string name,Vector3 at,Vector3 target,Color color,float power,float s,bool shadows) {
  var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=at*s;go.transform.LookAt(target*s);
  var l=go.AddComponent<Light>();l.type=LightType.Spot;l.color=color;l.intensity=power;l.range=14*s;l.spotAngle=110;l.innerSpotAngle=65;
  l.shadows=shadows?LightShadows.Soft:LightShadows.None;l.shadowBias=.03f;l.shadowNormalBias=.08f;
 }
 static Texture2D Pack(Texture source,string name,float lift,int size) {
  var mat=new Material(Shader.Find("Hidden/MicroLair/PolishSurfaceData"));mat.SetFloat("_Lift",lift);mat.SetFloat("_Gain",.9f);mat.SetFloat("_Max",.55f);
  var rt=RenderTexture.GetTemporary(size,size,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
  var texture=new Texture2D(size,size,TextureFormat.RGBA32,false,true);var prev=RenderTexture.active;bool srgb=GL.sRGBWrite;
  string path=Folder+"/"+name+"_Surface.png";
  try{GL.sRGBWrite=false;Graphics.Blit(source,rt,mat);RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,size,size),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
  finally{GL.sRGBWrite=srgb;RenderTexture.active=prev;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(texture);Object.DestroyImmediate(mat);}
  AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var imp=(TextureImporter)AssetImporter.GetAtPath(path);
  imp.sRGBTexture=false;imp.alphaSource=TextureImporterAlphaSource.FromInput;imp.wrapMode=TextureWrapMode.Repeat;imp.anisoLevel=8;
  imp.textureCompression=TextureImporterCompression.CompressedHQ;imp.SaveAndReimport();FullResolution(path,size);return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
 }
 static void FullResolution(string path,int size){
  var imp=AssetImporter.GetAtPath(path) as TextureImporter;if(!imp)return;
  imp.maxTextureSize=size;imp.anisoLevel=16;imp.filterMode=FilterMode.Trilinear;imp.mipmapEnabled=true;imp.streamingMipmaps=false;
  imp.ignoreMipmapLimit=true;
  var pc=imp.GetPlatformTextureSettings("Standalone");pc.name="Standalone";pc.overridden=true;pc.maxTextureSize=size;
  pc.format=imp.textureType==TextureImporterType.NormalMap?TextureImporterFormat.BC5:TextureImporterFormat.BC7;
  pc.compressionQuality=100;pc.crunchedCompression=false;imp.SetPlatformTextureSettings(pc);imp.SaveAndReimport();
 }
 public static void Capture(Transform root,float s,string filename){
  var camera=MicroLairRevisionC.NewCamera(root);
  try{camera.allowHDR=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
   camera.transform.position=new Vector3(0,2.9f,-9.5f)*s;camera.transform.LookAt(new Vector3(0,3,1.5f)*s);camera.fieldOfView=60;
   MicroLairRevisionC.Render(camera,filename);
  }finally{Object.DestroyImmediate(camera.gameObject);}
 }
}
