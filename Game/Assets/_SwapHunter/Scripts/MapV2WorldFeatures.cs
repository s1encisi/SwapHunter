using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace SwapHunter
{
    // Authored world semantics and floor rectangles are exported with each build.
    // A rectangle is a construction footprint, not a claim of reachable NavMesh area.
    public sealed class MapV2Footprint : MonoBehaviour
    {
        public string zone;
        public Vector2 center, size;
        public float height;
    }
    public sealed class MapV2WorldFeature : MonoBehaviour
    {
        public string featureId, purpose, spatialRole, interaction, adjacency;
        public void Describe(string id,string use,string role,string rule,string neighbours)
        { featureId=id; purpose=use; spatialRole=role; interaction=rule; adjacency=neighbours; }
    }
    public sealed class MapV2TimedGate : MonoBehaviour
    {
        public GameObject barrier;
        float remaining;
        public bool Closed => barrier && barrier.activeSelf;
        public void Close(float seconds)
        {
            if (!barrier) return;
            var box=barrier.GetComponent<BoxCollider>();
            if(box && Physics.CheckBox(box.transform.TransformPoint(box.center),Vector3.Scale(box.size*.5f,box.transform.lossyScale),box.transform.rotation,(1 << Layers.Actor),QueryTriggerInteraction.Ignore))return;
            barrier.SetActive(true); remaining=seconds; GetComponentInParent<MissionLayout>().MarkNavigationDirty();
        }
        public void Open(){ if(!barrier)return;barrier.SetActive(false); remaining=0;GetComponentInParent<MissionLayout>().MarkNavigationDirty(); }
        void Update(){if(remaining>0 && DemoGame.I && DemoGame.I.IsPlaying){remaining-=Time.deltaTime;if(remaining<=0)Open();}}
    }
    public sealed class MapV2RailCart : MonoBehaviour
    {
        public Vector3 a,b;
        public bool AtB { get; private set; }
        public bool Moving { get; private set; }
        Vector3 destination;
        public bool Toggle()
        {
            if(Moving)return false;
            destination=AtB?a:b;Moving=true;
            DemoGame.I.Record("world_cart_started",name+";destination="+destination);return true;
        }
        void Update()
        {
            if(!Moving || !DemoGame.I || !DemoGame.I.IsPlaying)return;
            Vector3 next=Vector3.MoveTowards(transform.position,destination,4*Time.deltaTime);
            Vector3 center=next+Vector3.up*1.4f;
            if(Physics.CheckBox(center,new Vector3(2.7f,1.35f,1.7f),Quaternion.identity,(1 << Layers.Actor),QueryTriggerInteraction.Ignore))return;
            transform.position=next;
            if(Vector3.Distance(transform.position,destination)<.02f){Moving=false;AtB=!AtB;GetComponentInParent<MissionLayout>().MarkNavigationDirty();DemoGame.I.Record("world_cart_docked",name+";B="+AtB);}
        }
    }
    public sealed class MapV2SteamValve : MonoBehaviour
    {
        static readonly System.Collections.Generic.List<MapV2SteamValve> active=new System.Collections.Generic.List<MapV2SteamValve>();
        public static MapV2SteamValve[] ActiveSnapshot()=>active.ToArray();
        public Vector3 DangerCenter=>diverted?outletB:outletA;
        public bool Live=>isActiveAndEnabled;
        public float DamagePerSecond=>80;
        float warnUntil;
        void OnEnable(){active.Add(this);}
        void OnDisable(){active.Remove(this);}
        public Vector3 outletA,outletB;
        public bool diverted;
        LineRenderer ring;
        float tick;
        public void Initialize(){warnUntil=Time.time+1.2f;ring=Shapes.Ring(transform,outletA+Vector3.up*.08f,3,new Color(1,.35f,.08f),.12f);}
        public bool Toggle(){diverted=!diverted;warnUntil=Time.time+1.2f;if(ring)UnityEngine.Object.Destroy(ring.gameObject);ring=Shapes.Ring(transform,(diverted?outletB:outletA)+Vector3.up*.08f,3,new Color(1,.35f,.08f),.12f);DemoGame.I.Record("world_steam_diverted",name+";B="+diverted);return true;}
        void Update()
        {
            if(!DemoGame.I || !DemoGame.I.IsPlaying)return;
            if(Time.time<warnUntil)return;tick-=Time.deltaTime;if(tick>0)return;tick=.25f;
            var p=diverted?outletB:outletA; var game=DemoGame.I;
            if(ClassSkillArea.Contains(p,game.player.transform.position,game.player.Eye.position) && !Physics.Linecast(p+Vector3.up*.3f,game.player.Eye.position,Layers.WorldMask))game.player.Hurt(2,p,false);
            foreach(var enemy in game.enemies)if(enemy && enemy.Alive && ClassSkillArea.Contains(p,enemy.transform.position,enemy.AimPoint) && !Physics.Linecast(p+Vector3.up*.3f,enemy.AimPoint,Layers.WorldMask))enemy.Damage((enemy.IsBoss?32:80)*.25f);
        }
    }
    public sealed class MapV2Track : MonoBehaviour
    {
        public Vector3[] routes;
        public int selected;
        public void Switch(){selected=1-selected;DemoGame.I.Record("world_track_changed",name+";route="+selected);}
        public Vector3 Destination => routes!=null && routes.Length>selected?routes[selected]:transform.position;
    }
    public static class MapV2WorldActions
    {
        public static bool Try(int chapter,string actionKey,Vector3 origin,Vector3 direction)
        {
            if(actionKey=="breach") { bool broken=false;foreach(var hit in Physics.OverlapSphere(origin,8,Layers.WorldMask,QueryTriggerInteraction.Ignore)){var feature=hit.GetComponent<MapV2WorldFeature>();var damage=hit.GetComponent<DamageableObjective>();if(!feature || !damage || !feature.interaction.Contains("冲撞"))continue;Vector3 delta=hit.bounds.center-origin;if(Vector3.Dot(delta.normalized,direction.normalized)<.5f)continue;damage.ApplyDamage(10000,origin);broken=true;}return broken;}
            var game=DemoGame.I;if(!game || game.expeditionMap==null)return false;var root=game.expeditionMap.root;
            if(chapter==1){var gates=root.GetComponentsInChildren<MapV2TimedGate>(true);if(gates.Length>0){gates[0].Close(4);game.Record("world_boss_gate","one_gate_4s_other_route_open");return true;}}
            if(chapter==7){var carts=root.GetComponentsInChildren<MapV2RailCart>(true);if(carts.Length>0)return carts[0].Toggle();}
            if(chapter==9){var track=root.GetComponentInChildren<MapV2Track>();if(track){game.Record("world_boss_track","route="+track.selected+";target="+track.Destination);return true;}}
            return false;
        }
        public static Vector3 TrackDestination(Vector3 fallback)
        {var game=DemoGame.I;if(!game || game.expeditionMap==null)return fallback;var track=game.expeditionMap.root.GetComponentInChildren<MapV2Track>();return track?track.Destination:fallback;}
    }
}
