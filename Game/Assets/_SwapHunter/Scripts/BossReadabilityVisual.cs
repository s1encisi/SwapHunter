using UnityEngine;
using UnityEngine.Rendering;
namespace SwapHunter
{
    // Reads actual skill state; never chooses targets, moves actors, changes timing or applies damage.
    public sealed class BossReadabilityVisual : MonoBehaviour
    {
        BossEncounter boss;
        GameObject badge;
        Transform glyph;
        TextMesh caption;
        LineRenderer progress;
        Material glyphMaterial;
        MaterialPropertyBlock lineTint;
        BossSkill lastSkill=BossSkill.None;BossSkillStep lastStep=BossSkillStep.Ready;
        public bool Visible=>badge&&badge.activeSelf;
        public BossSkill DisplayedSkill=>Visible?boss.Skill:BossSkill.None;
        public static void Attach(BossEncounter source)
        {
            var visual=source.GetComponent<BossReadabilityVisual>();
            if(!visual)visual=source.gameObject.AddComponent<BossReadabilityVisual>();
            visual.Initialize(source);
        }
        void Initialize(BossEncounter source)
        {
            if(badge)return;boss=source;lineTint=new MaterialPropertyBlock();
            badge=new GameObject("Boss state-driven visual cue");badge.transform.SetParent(transform,false);
            glyph=Shapes.Make("Kenney CC0 state glyph",PrimitiveType.Quad,badge.transform,Vector3.zero,new Vector3(.46f,.46f,1),9,false,Layers.Effects).transform;
            glyphMaterial=new Material(Shapes.Mat(9)){name="Owned Boss CC0 glyph"};glyphMaterial.SetFloat("_Cull",0);
            glyph.GetComponent<Renderer>().sharedMaterial=glyphMaterial;
            var label=new GameObject("Boss action caption");label.transform.SetParent(badge.transform,false);label.transform.localPosition=new Vector3(.33f,.06f,0);
            caption=label.AddComponent<TextMesh>();caption.font=DemoGame.I.config.uiFont;caption.fontSize=40;caption.characterSize=.052f;
            caption.anchor=TextAnchor.MiddleLeft;caption.alignment=TextAlignment.Left;
            var fontMaterial=new Material(DemoGame.I.config.worldTextMaterial);fontMaterial.mainTexture=caption.font.material.mainTexture;
            label.GetComponent<MeshRenderer>().sharedMaterial=fontMaterial;label.AddComponent<WorldFontAtlas>().Initialize(caption.font,fontMaterial);
            progress=Shapes.Line(badge.transform,Vector3.zero,Vector3.zero,Color.white,.023f);progress.name="Cast progress, not danger radius";
            progress.useWorldSpace=false;progress.positionCount=33;progress.loop=false;progress.startWidth=progress.endWidth=.023f;
            ImportedModels.SetLayer(badge.transform,Layers.Effects);badge.SetActive(false);
        }
        void LateUpdate()
        {
            bool active=boss&&boss.Owner&&boss.Owner.Alive&&(boss.SkillStep==BossSkillStep.Telegraph||boss.SkillStep==BossSkillStep.Execute);
            if(!badge)return;badge.SetActive(active);if(!active)return;
            var camera=DemoGame.I&&DemoGame.I.player?DemoGame.I.player.cameraView:null;
            badge.transform.position=boss.Owner.AimPoint+Vector3.up*.85f;
            if(camera)badge.transform.rotation=camera.transform.rotation;
            Color tint=Tint(boss.Skill);bool warning=boss.SkillStep==BossSkillStep.Telegraph;
            if(lastSkill!=boss.Skill||lastStep!=boss.SkillStep)
            {
                lastSkill=boss.Skill;lastStep=boss.SkillStep;
                caption.text=(warning?"预备 · ":"生效 · ")+Name(boss.Skill);
                string texture=boss.Skill==BossSkill.Attraction||boss.Skill==BossSkill.GroundWave?"circle_01":
                    boss.Skill==BossSkill.AdvantageSwap||boss.Skill==BossSkill.SwapElbow?"flare_01":"spark_01";
                var asset=Resources.Load<Texture2D>("VisualD11/Particles/"+texture);
                if(!asset)throw new System.InvalidOperationException("Missing licensed Boss texture: "+texture);
                glyphMaterial.SetTexture("_BaseMap",asset);glyphMaterial.mainTexture=asset;
            }
            float pulse=warning?.72f+.22f*Mathf.Sin(Time.time*9):1;
            tint.a=pulse;glyphMaterial.SetColor("_BaseColor",tint);caption.color=new Color(tint.r,tint.g,tint.b,1);
            // Original warning geometry remains the authoritative visible boundary; recolour only.
            var boundary=boss.DangerOutline;
            if(boundary){boundary.GetPropertyBlock(lineTint);lineTint.SetColor("_BaseColor",new Color(tint.r,tint.g,tint.b,1));lineTint.SetColor("_Color",new Color(tint.r,tint.g,tint.b,1));boundary.SetPropertyBlock(lineTint);}
            float fraction=warning?Mathf.Clamp01(1-boss.SkillAgeSeconds/Mathf.Max(.001f,boss.SkillSeconds)):1;
            for(int i=0;i<33;i++){float a=(i/32f)*Mathf.PI*2*fraction;progress.SetPosition(i,new Vector3(Mathf.Sin(a)*.275f,Mathf.Cos(a)*.275f,-.01f));}
            progress.enabled=warning;
        }
        static string Name(BossSkill skill)
        {
            switch(skill){case BossSkill.SweepLaser:return "扫地激光";case BossSkill.Attraction:return "牵引场";
                case BossSkill.AdvantageSwap:return "优势换位";case BossSkill.GroundWave:return "撼地波";
                case BossSkill.SwapElbow:return "换位肘击";case BossSkill.OmniFire:return "环射橙短 / 点射紫长";default:return "";}
        }
        static Color Tint(BossSkill skill)
        {
            switch(skill){case BossSkill.SweepLaser:return new Color(1,.24f,.15f);case BossSkill.Attraction:return new Color(.88f,.40f,.98f);
                case BossSkill.AdvantageSwap:return new Color(.22f,.85f,1);case BossSkill.GroundWave:return new Color(1,.70f,.18f);
                case BossSkill.SwapElbow:return new Color(1,.40f,.12f);default:return new Color(1,.40f,.72f);}
        }
        void OnDisable(){if(badge)badge.SetActive(false);}
        void OnDestroy(){if(glyphMaterial)Destroy(glyphMaterial);if(badge)Destroy(badge);}
    }
}
