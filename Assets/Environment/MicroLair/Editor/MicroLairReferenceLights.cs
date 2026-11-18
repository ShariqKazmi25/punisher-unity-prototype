using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class MicroLairReferenceLights
{
 const string Folder="Assets/Environment/MicroLair/SurfacePolish/";
 public static void Build(Transform parent,float s){
  Color cool=new Color(.62f,.86f,1f),warm=new Color(1f,.78f,.43f);
  var steel=AssetDatabase.LoadAssetAtPath<Material>(Folder+"rusty metal - Lair.mat");
  var white=Emission("Hub_Fluorescent",new Color(.69f,.88f,1),3);
  var amber=Emission("Pendant_Lens",new Color(1,.8f,.46f),4);
  // Repeated strips run inside the central cage, below the structural beam soffit.
  foreach(float z in new[]{-.2f,3.4f,7f,10.6f}) {
   foreach(float x in new[]{-1.9f,1.9f}) {
    Box(parent,"Fluorescent_Housing",new Vector3(x,6.02f,z),new Vector3(.22f,.1f,1.8f),steel,s);
    Box(parent,"Cool_Fluorescent_Diffuser",new Vector3(x,5.955f,z),new Vector3(.13f,.035f,1.65f),white,s);
   }
   Light(parent,"Hub_Cool_Downlight",new Vector3(0,5.8f,z),new Vector3(0,1,z+.4f),cool,5.5f,9,s,true,115);
   // Soft reflected spill reveals the overhead concrete and beam structure.
   Light(parent,"Hub_Ceiling_Bounce",new Vector3(0,5.5f,z),new Vector3(0,6.8f,z),cool,.9f,4,s,false,150);
  }
  foreach(float x in new[]{-4.4f,4.4f})foreach(float z in new[]{-2.8f,4.3f,11.4f}){
   var p=new Vector3(x,5.15f,z);
   Box(parent,"Pendant_Stem",p+Vector3.up*.55f,new Vector3(.035f,1.05f,.035f),steel,s);
   Disc(parent,"Pendant_Shade",p,new Vector3(.64f,.09f,.64f),steel,s);
   Disc(parent,"Warm_Pendant_Lens",p-Vector3.up*.101f,new Vector3(.47f,.008f,.47f),amber,s);
   Light(parent,"Aisle_Warm_Pool",p-Vector3.up*.15f,p+Vector3.down*4,warm,3.2f,8,s,false,95);
  }
  foreach(float x in new[]{-8f,8f}){
   var p=new Vector3(x,4.8f,14);
   Disc(parent,"Rear_Utility_Lamp",p,new Vector3(.4f,.04f,.4f),amber,s);
   Light(parent,"Rear_Warm_Utility",p-Vector3.up*.1f,p+new Vector3(0,-3,1),warm,2f,7,s,false,100);
  }
  // Low indirect light preserves navigation in the expanded foreground.
  Light(parent,"Lower_Cool_Indirect",new Vector3(0,4,-8),new Vector3(0,0,-5),cool,.55f,13,s,false,130);
 }
 static Material Emission(string name,Color color,float power){
  string path=Folder+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
  m.SetColor("_BaseColor",color);m.SetColor("_EmissionColor",color*power);m.EnableKeyword("_EMISSION");EditorUtility.SetDirty(m);return m;
 }
 static void Box(Transform p,string name,Vector3 pos,Vector3 size,Material mat,float s){Shape(p,name,pos,size,mat,s,PrimitiveType.Cube);}
 static void Disc(Transform p,string name,Vector3 pos,Vector3 size,Material mat,float s){Shape(p,name,pos,size,mat,s,PrimitiveType.Cylinder);}
 static void Shape(Transform p,string name,Vector3 pos,Vector3 size,Material mat,float s,PrimitiveType type){
  var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(p,false);go.transform.position=pos*s;go.transform.localScale=size*s;
  Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;
 }
 static void Light(Transform p,string name,Vector3 pos,Vector3 target,Color color,float power,float range,float s,bool shadows,float cone){
  var go=new GameObject(name);go.transform.SetParent(p,false);go.transform.position=pos*s;go.transform.LookAt(target*s);
  var l=go.AddComponent<UnityEngine.Light>();l.type=LightType.Spot;l.color=color;l.intensity=power;l.range=range*s;l.spotAngle=cone;l.innerSpotAngle=cone*.6f;
  l.shadows=shadows?LightShadows.Soft:LightShadows.None;l.shadowBias=.025f;l.shadowNormalBias=.05f;
 }
}
