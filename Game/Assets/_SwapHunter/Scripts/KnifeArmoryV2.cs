using System.Collections;using UnityEngine;using UnityEngine.Rendering;using UnityEngine.Rendering.Universal;
namespace SwapHunter{
 public sealed class KnifeArmoryPreviewV2:MonoBehaviour{
  public readonly Texture2D[] Icons=new Texture2D[6];GameObject stage;Camera cameraView;RenderTexture target;
  IEnumerator Start(){
   stage=new GameObject("Knife armory render stage");stage.transform.position=new Vector3(3200,-200,0);
   cameraView=new GameObject("Knife catalogue camera").AddComponent<Camera>();cameraView.transform.SetParent(stage.transform,false);cameraView.enabled=false;cameraView.orthographic=true;cameraView.clearFlags=CameraClearFlags.SolidColor;cameraView.backgroundColor=new Color(.035f,.055f,.075f);cameraView.cullingMask=1<<29;cameraView.nearClipPlane=.01f;cameraView.farClipPlane=10;
   cameraView.GetUniversalAdditionalCameraData().renderShadows=false;
   var light=new GameObject("Knife preview key").AddComponent<Light>();light.transform.SetParent(stage.transform,false);light.type=LightType.Directional;light.intensity=3;light.cullingMask=1<<29;light.transform.rotation=Quaternion.Euler(35,-35,0);
   target=new RenderTexture(480,240,24,RenderTextureFormat.ARGB32);target.Create();
   for(int i=0;i<6;i++){
    var model=KnifeV2Visual.Create(i,stage.transform,29);model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.Euler(0,20,0);model.SetFold(1);
    var renderers=model.GetComponentsInChildren<Renderer>();Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
    cameraView.transform.position=bounds.center+new Vector3(.65f,.55f,.35f);cameraView.transform.LookAt(bounds.center);
    cameraView.aspect=2;cameraView.orthographicSize=Mathf.Max(.11f,bounds.extents.magnitude*.72f);
    var request=new RenderPipeline.StandardRequest{destination=target};RenderPipeline.SubmitRenderRequest(cameraView,request);
    var previous=RenderTexture.active;RenderTexture.active=target;Icons[i]=new Texture2D(480,240,TextureFormat.RGB24,false);Icons[i].ReadPixels(new Rect(0,0,480,240),0,0);Icons[i].Apply();RenderTexture.active=previous;
    model.gameObject.SetActive(false);Destroy(model.gameObject);yield return null;
   }
   stage.SetActive(false);
  }
  void OnDestroy(){foreach(var icon in Icons)if(icon)Destroy(icon);if(target){target.Release();Destroy(target);}if(stage)Destroy(stage);}
 }
 public sealed partial class DemoGame{
  KnifeArmoryPreviewV2 knifePreview;
  void DrawKnifeArmoryV2(){
   if(!knifePreview)knifePreview=gameObject.AddComponent<KnifeArmoryPreviewV2>();
   float w=(viewWidth-128)/3;var p=expeditionStore.Profile;int current=p.classLoadouts[p.activeClass].knifeId;
   for(int i=0;i<6;i++){
    int id=i;float x=48+(i%3)*(w+16),y=269+(i/3)*268;Fill(new Rect(x,y,w,251),PanelBlue);
    if(knifePreview.Icons[i])GUI.DrawTexture(new Rect(x+12,y+9,w-24,115),knifePreview.Icons[i],ScaleMode.ScaleToFit);
    Text(KnifeV2Visual.Names[i],new Rect(x+18,y+128,w-36,29),22,Paper);
    Text("轻击72 / 重击144 · 背击×3",new Rect(x+18,y+165,w-36,25),16,Muted);
    Fill(new Rect(x+18,y+192,w-36,4),new Color(.13f,.22f,.29f));Fill(new Rect(x+18,y+192,(w-36)*.5f,4),Cyan);
    bool enabled=GUI.enabled;GUI.enabled=enabled&&p.meleeKnives[i];
    if(Button(current==i?"已装备 / 槽3":p.meleeKnives[i]?"装备":"未拥有 · 场景拾取并成功撤离",new Rect(x+12,y+210,w-24,34),current==i))ExpeditionAction(()=>expeditionStore.EquipMeleeKnife(id));
    GUI.enabled=enabled;
   }
   Text("六刀统一战斗性能 · 地面拾取替换本次选择，成功撤离后永久拥有 · 3切刀 / 左键轻击 / 右键重击 / H检视",new Rect(50,820,viewWidth-100,40),17,Muted);
  }
 }
}

