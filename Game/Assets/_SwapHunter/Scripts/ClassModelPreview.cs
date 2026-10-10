using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SwapHunter
{
    public sealed class ClassModelPreview : MonoBehaviour
    {
        const int Layer=30, Mask=1<<Layer;
        static readonly Vector3 Origin=new Vector3(1060,-256,-1024);
        GameObject stage,model;Camera cameraView;Light key,fill;RenderTexture texture;
        int selected,built=-1;bool visible,dirty;float yaw,nextRender;Bounds bounds;
        public RenderTexture Texture=>visible?texture:null;
        public string Error{get;private set;}="";
        public void Select(int id){if(selected!=id){selected=id;dirty=true;}}
        public void Rotate(float degrees){yaw=Mathf.Repeat(yaw+degrees,360);dirty=true;}
        public void ResetView(){yaw=0;dirty=true;}
        public void SetVisible(bool value){visible=value;}
        void Update()
        {
            if(!visible){Release();return;}
            if(!string.IsNullOrEmpty(Error))return;
            try
            {
                EnsureStage();
                if(built!=selected)
                {
                    if(model){model.SetActive(false);Destroy(model);}
                    model=ClassVisualFactory.CreateClassModel(selected,stage.transform);
                    foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;
                    var renderers=model.GetComponentsInChildren<Renderer>();
                    if(renderers.Length==0)throw new System.InvalidOperationException("职业模型没有可见网格");
                    bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                    model.transform.position-=bounds.center-Origin;
                    bounds=new Bounds(Vector3.zero,bounds.size);built=selected;yaw=0;dirty=true;
                }
                if(dirty&&Time.unscaledTime>=nextRender)
                {
                    model.transform.localRotation=Quaternion.Euler(0,yaw,0);
                    cameraView.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.magnitude*.65f)*1.2f;
                    cameraView.transform.position=Origin+new Vector3(3,.8f,5);cameraView.transform.LookAt(Origin);
                    var request=new RenderPipeline.StandardRequest{destination=texture};
                    if(!RenderPipeline.SupportsRenderRequest(cameraView,request))throw new System.InvalidOperationException("当前管线不支持角色预览");
                    try{model.SetActive(true);key.enabled=fill.enabled=true;RenderPipeline.SubmitRenderRequest(cameraView,request);}
                    finally{model.SetActive(false);key.enabled=fill.enabled=false;}
                    dirty=false;nextRender=Time.unscaledTime+.08f;
                }
            }
            catch(System.Exception error){Error=error.Message;Debug.LogWarning("角色预览："+Error);Release(false);}
        }
        void EnsureStage()
        {
            if(stage)return;
            stage=new GameObject("Class visual preview stage"){hideFlags=HideFlags.DontSave};stage.transform.position=Origin;
            var cameraObject=new GameObject("Class preview camera");cameraObject.transform.SetParent(stage.transform,false);
            cameraView=cameraObject.AddComponent<Camera>();cameraView.enabled=false;cameraView.orthographic=true;cameraView.cullingMask=Mask;
            cameraView.nearClipPlane=.01f;cameraView.farClipPlane=20;cameraView.clearFlags=CameraClearFlags.SolidColor;
            cameraView.backgroundColor=new Color(.025f,.045f,.07f);cameraView.allowHDR=false;cameraView.allowMSAA=false;
            var data=cameraView.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.renderShadows=false;
            key=MakeLight("Class key",new Color(1,.93f,.8f),3.1f,new Vector3(25,-35,0));
            fill=MakeLight("Class fill",new Color(.45f,.7f,1),1.2f,new Vector3(20,140,0));
            texture=new RenderTexture(384,512,24,RenderTextureFormat.ARGB32){name="Class portrait",hideFlags=HideFlags.DontSave};texture.Create();
        }
        Light MakeLight(string name,Color color,float strength,Vector3 angle)
        {
            var go=new GameObject(name);go.transform.SetParent(stage.transform,false);go.transform.localRotation=Quaternion.Euler(angle);
            var light=go.AddComponent<Light>();light.type=LightType.Directional;light.color=color;light.intensity=strength;light.cullingMask=Mask;light.shadows=LightShadows.None;light.enabled=false;return light;
        }
        void Release(bool resetError=true)
        {
            if(stage){stage.SetActive(false);Destroy(stage);}stage=null;model=null;built=-1;
            if(texture){texture.Release();Destroy(texture);}texture=null;
            if(resetError)Error="";
        }
        void OnDisable(){Release();}
        void OnDestroy(){Release();}
    }
    public sealed partial class DemoGame
    {
        ClassModelPreview classPreview;
        void UpdateClassPresentation()
        {
            bool visible=TacticalExpedition&&expeditionBoard&&state==RunState.Menu&&expeditionTab==2;
            if(visible&&!classPreview)classPreview=gameObject.AddComponent<ClassModelPreview>();
            if(classPreview){classPreview.SetVisible(visible);if(visible)classPreview.Select(expeditionStore.Profile.activeClass);}
        }
    }
}
