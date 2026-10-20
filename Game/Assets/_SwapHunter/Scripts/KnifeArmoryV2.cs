using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace SwapHunter
{
    public sealed class KnifeArmoryPreviewV2:MonoBehaviour
    {
        public readonly Texture2D[] Icons=new Texture2D[6];
        GameObject stage;Camera cameraView;RenderTexture target,selectedTarget;KnifeV2Visual selectedModel;
        int selected,built=-1;bool ready,visible;float yaw,nextRender,lastRotation;
        public bool AutoRotate=true;
        public string Error{get;private set;}
        public Texture SelectedTexture=>visible&&built==selected?selectedTarget:null;
        public void Select(int id){if(selected==id)return;selected=id;built=-1;yaw=0;}
        public void SetVisible(bool value){visible=value;if(!value&&stage)stage.SetActive(false);}
        IEnumerator Start()
        {
            stage=new GameObject("Knife armory render stage");stage.transform.position=new Vector3(3200,-200,0);
            cameraView=new GameObject("Knife catalogue camera").AddComponent<Camera>();cameraView.transform.SetParent(stage.transform,false);
            cameraView.enabled=false;cameraView.orthographic=true;cameraView.clearFlags=CameraClearFlags.SolidColor;
            cameraView.backgroundColor=new Color(.045f,.080f,.098f);cameraView.cullingMask=1<<29;cameraView.nearClipPlane=.01f;cameraView.farClipPlane=10;
            cameraView.GetUniversalAdditionalCameraData().renderShadows=false;
            MakeLight("Knife key",new Color(1,.9f,.76f),3,new Vector3(35,-35,0));
            MakeLight("Knife fill",new Color(.66f,.8f,.82f),1.4f,new Vector3(20,135,0));
            target=new RenderTexture(480,240,24,RenderTextureFormat.ARGB32);target.Create();
            selectedTarget=new RenderTexture(768,448,24,RenderTextureFormat.ARGB32);selectedTarget.Create();
            for(int i=0;i<6;i++)
            {
                stage.SetActive(true);
                var model=KnifeV2Visual.Create(i,stage.transform,29);model.SetFold(1);Frame(model,2);
                RenderPipeline.SubmitRenderRequest(cameraView,new RenderPipeline.StandardRequest{destination=target});
                var old=RenderTexture.active;RenderTexture.active=target;
                Icons[i]=new Texture2D(480,240,TextureFormat.RGB24,false);Icons[i].ReadPixels(new Rect(0,0,480,240),0,0);Icons[i].Apply();RenderTexture.active=old;
                model.gameObject.SetActive(false);Destroy(model.gameObject);stage.SetActive(false);yield return null;
            }
            ready=true;stage.SetActive(false);
        }
        void MakeLight(string label,Color color,float strength,Vector3 rotation)
        {
            var light=new GameObject(label).AddComponent<Light>();light.transform.SetParent(stage.transform,false);
            light.type=LightType.Directional;light.color=color;light.intensity=strength;light.cullingMask=1<<29;light.transform.rotation=Quaternion.Euler(rotation);
        }
        void Frame(KnifeV2Visual model,float aspect)
        {
            var renderers=model.GetComponentsInChildren<Renderer>();Bounds bounds=renderers[0].bounds;
            foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            model.transform.position-=bounds.center-stage.transform.position;
            cameraView.transform.position=stage.transform.position+new Vector3(.65f,.55f,.35f);cameraView.transform.LookAt(stage.transform.position);
            cameraView.aspect=aspect;cameraView.orthographicSize=Mathf.Max(.13f,bounds.extents.magnitude*.83f);
        }
        void LateUpdate()
        {
            if(!ready||!visible||!string.IsNullOrEmpty(Error)){lastRotation=Time.unscaledTime;return;}
            if(Time.unscaledTime<nextRender)return;
            try
            {
                stage.SetActive(true);
                if(built!=selected)
                {
                    if(selectedModel){selectedModel.gameObject.SetActive(false);Destroy(selectedModel.gameObject);}
                    selectedModel=KnifeV2Visual.Create(selected,stage.transform,29);selectedModel.SetFold(1);built=selected;
                }
                if(AutoRotate)yaw+=20f*Mathf.Min(.1f,Time.unscaledTime-lastRotation);
                lastRotation=Time.unscaledTime;
                selectedModel.transform.localPosition=Vector3.zero;selectedModel.transform.localRotation=Quaternion.Euler(0,yaw,0);
                Frame(selectedModel,768f/448);
                RenderPipeline.SubmitRenderRequest(cameraView,new RenderPipeline.StandardRequest{destination=selectedTarget});
                nextRender=Time.unscaledTime+.05f;
            }
            catch(System.Exception e){Error=e.Message;Debug.LogWarning("刀具预览："+Error);}
            finally{if(stage)stage.SetActive(false);}
        }
        void OnDestroy()
        {
            foreach(var icon in Icons)if(icon)Destroy(icon);
            if(target){target.Release();Destroy(target);}if(selectedTarget){selectedTarget.Release();Destroy(selectedTarget);}if(stage)Destroy(stage);
        }
    }
    public sealed partial class DemoGame
    {
        KnifeArmoryPreviewV2 knifePreview;
        void DrawKnifeArmoryV2(){expeditionWeaponSlot=2;DrawWeaponLibrary();}
        void DrawUnifiedKnifeArmory(float listWidth,Rect panel)
        {
            if(!knifePreview)knifePreview=gameObject.AddComponent<KnifeArmoryPreviewV2>();
            var p=expeditionStore.Profile;int current=p.classLoadouts[p.activeClass].knifeId;
            float cardWidth=(listWidth-8)/2,cardHeight=(422-16)/3;
            for(int i=0;i<6;i++)
            {
                Rect card=new Rect(48+i%2*(cardWidth+8),397+i/2*(cardHeight+8),cardWidth,cardHeight);
                if(NavigationButton(KnifeV2Visual.Names[i],card,selectedArmoryKnife==i,false))selectedArmoryKnife=i;
                WeaponText(current==i?"已装备":p.meleeKnives[i]?"可装备":"未拥有",new Rect(card.x+8,card.y+5,card.width-16,20),12,Muted,TextAnchor.MiddleRight);
                WeaponTexture(new Rect(card.x+8,card.y+26,card.width-16,card.height-57),knifePreview.Icons[i],!p.meleeKnives[i]);
                WeaponText(KnifeV2Visual.Names[i],new Rect(card.x+8,card.yMax-29,card.width-16,26),17,Paper,TextAnchor.MiddleCenter);
            }
            knifePreview.Select(selectedArmoryKnife);knifePreview.AutoRotate=armoryAutoRotate;
            float x=panel.x+20,y=panel.y+17,width=panel.width-40;
            Text(KnifeV2Visual.Names[selectedArmoryKnife],new Rect(x,y,width*.65f,40),29,Paper);
            Text(p.meleeKnives[selectedArmoryKnife]?"已拥有":"带回后解锁",new Rect(x+width*.65f,y,width*.35f,40),14,Muted,TextAnchor.MiddleRight);
            Text("近战武器 · 3 号槽",new Rect(x,y+48,width,30),15,Muted);
            DrawArmoryPreview(new Rect(x,y+93,width*.55f,190),knifePreview.SelectedTexture);
            float sx=x+width*.55f+20,sw=width*.45f-20;
            Text("轻击  72\n\n重击  144\n\n背击  ×3",new Rect(sx,y+96,sw,180),19,Paper);
            string[] details={"直刃短握，适合稳定切入。","长刃与护手，观察刀脊和刃面。","环柄反握，转环与回握衔接。","双柄翻转，开合后回到锁定握姿。","绳环牵引，翻面后重新收握。","锯齿刀背，侧旋检视锯齿轮廓。"};
            Text(details[selectedArmoryKnife],new Rect(x,y+294,width,43),17,Muted);
            Text(KeyName(25)+" 切刀 · "+KeyName(12)+" 轻击 · "+KeyName(13)+" 重击 · "+KeyName(28)+" 检视",new Rect(x,panel.yMax-93,width,43),16,Muted);
            float controlY=panel.yMax-40,rotateWidth=Mathf.Min(185,width*.37f);
            if(Button(armoryAutoRotate?"自动旋转预览 · 开":"自动旋转预览 · 关",new Rect(x,controlY,rotateWidth,32)))armoryAutoRotate=!armoryAutoRotate;
            bool enabled=GUI.enabled;GUI.enabled=enabled&&p.meleeKnives[selectedArmoryKnife]&&p.deployment==null&&current!=selectedArmoryKnife;
            if(Button(current==selectedArmoryKnife?"已装备":p.meleeKnives[selectedArmoryKnife]?"装备至 3 号槽":"拾取并撤离后解锁",new Rect(x+rotateWidth+12,controlY,width-rotateWidth-12,32),true))ExpeditionAction(()=>expeditionStore.EquipMeleeKnife(selectedArmoryKnife));
            GUI.enabled=enabled;
        }
    }
}
