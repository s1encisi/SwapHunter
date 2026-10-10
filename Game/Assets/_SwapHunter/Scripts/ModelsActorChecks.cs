using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SwapHunter
{
    // New-model acceptance only. Controlled actors/motion and offscreen pixels are not a human playtest.
    public static class ModelsActorChecks
    {
        public static IEnumerator Run(Action<string,bool,string> add,string output)
        {
            var g=DemoGame.I;var p=g.player;var origin=new Vector3(100,0,0);
            var actors=new List<EnemyActor>();var cameras=new Dictionary<Camera,int>();var ownedMaterials=new List<Material>();
            GameObject fixture=null,stage=null;bool oldSuppress=g.qaSuppressAI;Vector3 oldFeet=p.transform.position;float oldYaw=p.yaw,oldPitch=p.pitch;
            void Check(string name,bool pass,string detail="")=>add("models_actor_"+name,pass,detail);
            EnemyActor Spawn(EnemyKind kind,Vector3 point)
            {
                var actor=EnemyActor.Spawn(kind,point,0);if(!actor)throw new InvalidOperationException("New actor fixture spawn rejected: "+point);
                actor.suppressAI=true;actors.Add(actor);return actor;
            }
            void Aim(Vector3 point){Vector3 d=point-p.Eye.position;p.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);}
            try
            {
                if(!RunStorage.IsIsolated||!g.IsPlaying){Check("fixture_precondition",false,"Requires caller's isolated playing state");yield break;}
                g.qaSuppressAI=true;
                fixture=new GameObject("New actor native fixture");
                Shapes.Box("Actor fixture floor",fixture.transform,origin+Vector3.down*.5f,new Vector3(26,1,24),1,true,Layers.World);
                var nav=fixture.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;nav.layerMask=Layers.WorldMask;nav.useGeometry=UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
                Physics.SyncTransforms();RuntimeNavigationOwner.BuildFresh(nav);
                p.ResetAt(origin+new Vector3(0,.04f,-7));p.SetLook(0,0);
                var assault=Spawn(EnemyKind.Assault,origin+new Vector3(-8,0,0));
                var sniper=Spawn(EnemyKind.Sniper,origin+new Vector3(-3,0,0));
                var guard=Spawn(EnemyKind.Shield,origin+new Vector3(3,0,0));
                var commander=Spawn(EnemyKind.Elite,origin+new Vector3(8,0,0));var boss=commander.EnableBoss();
                yield return new WaitForSeconds(.15f);Physics.SyncTransforms();
                bool sources=true,aligned=true;
                foreach(var actor in actors)
                {
                    var rig=actor.VisualRig;sources&=rig&&rig.SourceKey.StartsWith("VisualV2/Actors/",StringComparison.Ordinal)&&rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length>0&&rig.AnimationAvailable&&rig.RootMotionDisabled;
                    aligned&=rig&&actor.HeadHitbox&&actor.TorsoHitbox&&Vector3.Distance(actor.HeadHitbox.bounds.center,rig.HeadAimMarker)<.025f&&Vector3.Distance(actor.TorsoHitbox.bounds.center,rig.TorsoAimMarker)<.025f;
                }
                Check("new_sources_skins_animation",sources,"Three new humanoid variants and George; no old-body fallback");
                Check("head_torso_centers",aligned,"Native trigger centers follow measured new-model Head/Chest markers; trigger sizes unchanged");
                Check("george_height_and_capsule",commander.VisualRig.SourceKey.EndsWith("boss_george",StringComparison.Ordinal)&&Mathf.Abs(commander.VisualRig.ModelHeight-2.1f)<.25f&&commander.Radius==.45f&&commander.Height==2.1f,
                    "visualHeight="+commander.VisualRig.ModelHeight+";capsule="+commander.Radius+"/"+commander.Height+";clips="+commander.VisualRig.AnimationClipCount);

                // Existing cameras cannot see the temporary lineup, including while its poses are constructed.
                foreach(var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None)){cameras[camera]=camera.cullingMask;camera.cullingMask&=~(1<<30);}
                stage=new GameObject("Native new actor lineup");stage.layer=30;stage.transform.position=new Vector3(800,-300,-800);
                bool classes=true;
                string[] titles={"突击兵","重装兵","侦查兵","远程兵","突击敌人","狙击敌人","盾卫","George"};
                for(int i=0;i<8;i++)
                {
                    GameObject model;
                    if(i<4)
                    {
                        model=ClassVisualFactory.CreateClassModel(i,stage.transform);var rig=model.GetComponent<ActorVisualRig>();
                        classes&=rig&&rig.SourceKey.EndsWith("class_"+i.ToString("00"),StringComparison.Ordinal)&&rig.AnimationAvailable;
                    }
                    else
                    {
                        var source=actors[i-4];model=CloneRenderPose(source.VisualRig.gameObject,stage.transform);
                        var proxy=source.transform.Find("Shield");
                        if(proxy&&source.HasPhysicalShield)
                        {
                            var shieldCopy=CloneRenderPose(proxy.gameObject,model.transform);
                            shieldCopy.transform.localPosition=proxy.localPosition;shieldCopy.transform.localRotation=proxy.localRotation;
                        }
                    }
                    model.transform.localPosition=new Vector3((3.5f-i)*1.7f,0,0);model.transform.localRotation=Quaternion.identity;ImportedModels.SetLayer(model.transform,30);
                    var label=new GameObject("Lineup caption");label.layer=30;label.transform.SetParent(stage.transform,false);label.transform.localPosition=new Vector3((3.5f-i)*1.7f,-.22f,.2f);label.transform.localRotation=Quaternion.Euler(0,180,0);
                    var text=label.AddComponent<TextMesh>();text.text=titles[i];text.font=g.font;text.fontSize=64;text.characterSize=.08f;text.anchor=TextAnchor.MiddleCenter;text.color=Color.white;
                    g.font.RequestCharactersInTexture(text.text,64);var material=new Material(g.config.worldTextMaterial);material.mainTexture=g.font.material.mainTexture;ownedMaterials.Add(material);label.GetComponent<Renderer>().sharedMaterial=material;
                }
                Check("four_new_class_bodies",classes,"All class portraits use their new CC0 skinned source and an evaluated holding pose");
                RenderLineup(stage,output);stage.SetActive(false);Check("lineup_real_render",File.Exists(Path.Combine(output,"actor-lineup.png")),"Native URP offscreen render; no screen-space GUI or human approval");

                float time=assault.VisualRig.AnimationSeconds;yield return new WaitForSeconds(.16f);
                Check("idle_animation_advances",assault.VisualRig.AnimationAvailable&&Mathf.Abs(assault.VisualRig.AnimationSeconds-time)>.01f,assault.VisualRig.CurrentAnimation);
                Vector3 start=assault.transform.position;float stop=Time.time+.45f;
                while(Time.time<stop){assault.motion.Push(Vector3.right*2*Time.deltaTime);yield return null;}
                Check("walk_from_real_controller_motion",Vector3.Distance(start,assault.transform.position)>.5f&&assault.VisualRig.CurrentAnimation.IndexOf("Walk",StringComparison.OrdinalIgnoreCase)>=0,
                    "meters="+Vector3.Distance(start,assault.transform.position)+";clip="+assault.VisualRig.CurrentAnimation);
                yield return new WaitForSeconds(.15f);int shots=assault.shotsFired;assault.ShootAt(p.Eye.position,1,20);yield return new WaitForSeconds(.045f);
                Check("actual_shot_animation_muzzle",assault.shotsFired==shots+1&&assault.VisualRig.CurrentAnimation.IndexOf("Shoot",StringComparison.OrdinalIgnoreCase)>=0&&assault.VisualRig.Muzzle&&assault.VisualRig.Muzzle.IsChildOf(assault.VisualRig.WeaponMount),assault.VisualRig.CurrentAnimation);
                yield return new WaitForSeconds(.22f);float hp=assault.health;assault.Damage(1);yield return new WaitForSeconds(.045f);
                Check("actual_hit_animation",assault.health==hp-1&&assault.VisualRig.CurrentAnimation.IndexOf("Hit",StringComparison.OrdinalIgnoreCase)>=0,assault.VisualRig.CurrentAnimation);

                p.CancelActions();p.SetFeet(origin+new Vector3(0,.04f,-5));p.cooldown=0;Aim(guard.AimPoint);Physics.SyncTransforms();
                var shield=guard.transform.Find("Shield").GetComponent<BoxCollider>();Vector3 shieldSize=shield.size;Quaternion facing=guard.transform.rotation;int swaps=p.swapCount;
                bool swap=p.RequestSwap(guard);yield return new WaitForSeconds(.16f);Physics.SyncTransforms();
                var r=guard.VisualRig;
                Check("q_new_body_and_physical_shield",swap&&p.swapCount==swaps+1&&r.VisualRoot.parent==guard.transform&&r.VisualRoot.localPosition.sqrMagnitude<.00001f&&shield.enabled&&shield.size==shieldSize&&Quaternion.Angle(facing,guard.transform.rotation)<.1f&&Vector3.Distance(guard.HeadHitbox.bounds.center,r.HeadAimMarker)<.025f&&r.BladeVisible&&r.BladeGripError<.025f,
                    "swap="+swap+";grip="+r.BladeGripError+";head="+Vector3.Distance(guard.HeadHitbox.bounds.center,r.HeadAimMarker));
                assault.Damage(100000);yield return new WaitForSeconds(.12f);
                bool disabled=true;foreach(var c in assault.GetComponentsInChildren<Collider>(true))disabled&=!c.enabled;
                Check("death_animation_no_live_collision",!assault.Alive&&disabled&&assault.VisualRig.CurrentAnimation.IndexOf("Death",StringComparison.OrdinalIgnoreCase)>=0&&assault.VisualRig.AnimationSeconds>0,assault.VisualRig.CurrentAnimation);
            }
            finally
            {
                if(stage){stage.SetActive(false);UnityEngine.Object.Destroy(stage);}
                foreach(var material in ownedMaterials)if(material)UnityEngine.Object.Destroy(material);
                foreach(var camera in cameras)if(camera.Key)camera.Key.cullingMask=camera.Value;
                foreach(var actor in actors)if(actor){g.enemies.Remove(actor);actor.gameObject.SetActive(false);UnityEngine.Object.Destroy(actor.gameObject);}
                if(fixture){fixture.SetActive(false);UnityEngine.Object.Destroy(fixture);}
                p.ResetAt(oldFeet);p.SetLook(oldYaw,oldPitch);g.qaSuppressAI=oldSuppress;
            }
        }
        static GameObject CloneRenderPose(GameObject source,Transform parent)
        {
            var copy=UnityEngine.Object.Instantiate(source,parent,false);copy.name="Native posed copy / "+source.name;
            foreach(var c in copy.GetComponentsInChildren<Collider>(true)){c.enabled=false;UnityEngine.Object.Destroy(c);}
            foreach(var b in copy.GetComponentsInChildren<Behaviour>(true)){b.enabled=false;UnityEngine.Object.Destroy(b);}
            foreach(var rb in copy.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.Destroy(rb);
            return copy;
        }
        static void RenderLineup(GameObject stage,string output)
        {
            const int width=2560,height=850,mask=1<<30;
            var cameraObject=new GameObject("Lineup capture camera");cameraObject.transform.SetParent(stage.transform,false);
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=2.5f;camera.cullingMask=mask;
            camera.nearClipPlane=.01f;camera.farClipPlane=30;camera.allowHDR=false;camera.allowMSAA=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.05f,.07f);
            camera.transform.position=stage.transform.position+new Vector3(2.8f,2.0f,11);camera.transform.LookAt(stage.transform.position+Vector3.up*.8f);
            var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.renderShadows=false;
            var lights=new Dictionary<Light,int>();foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)){lights[light]=light.cullingMask;light.cullingMask&=~mask;}
            Light NewLight(Vector3 angle,float intensity,Color color)
            {var go=new GameObject("Lineup light");go.transform.SetParent(stage.transform,false);go.transform.localRotation=Quaternion.Euler(angle);var light=go.AddComponent<Light>();light.type=LightType.Directional;light.intensity=intensity;light.color=color;light.cullingMask=mask;light.shadows=LightShadows.None;return light;}
            NewLight(new Vector3(28,-25,0),2.2f,new Color(1,.95f,.88f));NewLight(new Vector3(18,140,0),1.0f,new Color(.55f,.74f,1));
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);Texture2D pixels=null;var previous=RenderTexture.active;
            try
            {
                target.Create();var request=new RenderPipeline.StandardRequest{destination=target};
                if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("Lineup requires supported native URP render requests");
                RenderPipeline.SubmitRenderRequest(camera,request);RenderTexture.active=target;pixels=new Texture2D(width,height,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,"actor-lineup.png"),pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous;target.Release();UnityEngine.Object.Destroy(target);if(pixels)UnityEngine.Object.Destroy(pixels);
                foreach(var light in lights)if(light.Key)light.Key.cullingMask=light.Value;
            }
        }
    }
}