using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace SwapHunter
{
    // Physical route pass: virtual W drives the real PlayerMotor. No pose relocation is used.
    // Enemies are explicitly cleared to isolate routes; this is not a combat/human playtest.
    public static class ExpeditionRouteQA
    {
        sealed class Context
        {
            public DemoGame g; public Keyboard keys; public Mouse mouse; public Action<string,bool,string> check;
            public bool ok; public string failure; public int transitions; public float walked;
            public PlayerMotor P=>g.player;
        }
        sealed class Plan
        {
            public PhaseAnchor anchor; public Vector3 approach; public NavMeshPath path;
            public float score; public bool reachesGoal;
        }
        public static IEnumerator Run(DemoGame g,Action<string,bool,string> check)
        {
            if(!g.qaMode||!RunStorage.IsValidation)
            {check("route_validation_guard",false,"Requires an isolated validation profile.");yield break;}
            if(g.expeditionActive&&g.expeditionReceipt==null)
            {check("route_idle_guard",false,"Call after finishing the previous contract, not during a live run.");yield break;}
            var oldOptions=g.options;var oldKeys=g.qaKeyboard;var oldMouse=g.qaMouse;
            bool oldInput=g.qaInputEnabled,oldSuppress=g.qaSuppressAI;
            var keys=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
            var context=new Context{g=g,keys=keys,mouse=mouse,check=check};
            try
            {
                g.options=new GameOptions();g.SyncLegacyBindings();g.ApplySettings(false);
                g.qaKeyboard=keys;g.qaMouse=mouse;g.qaInputEnabled=true;g.qaSuppressAI=true;
                for(int level=0;level<12;level++)
                {
                    string prefix="physical_route_"+(level+1).ToString("00");
                    if(level>g.expeditionStore.Profile.highestUnlocked)
                    {check(prefix+"_unlocked",false,"Earlier route failed and this level is still locked.");break;}
                    g.OpenExpeditionBoard();
                    if(!g.BeginExpedition(level)){check(prefix+"_deploy",false,g.expeditionNotice);continue;}
                    yield return new WaitForSeconds(.25f);ClearEnemies(g);
                    context.transitions=0;context.walked=0;context.ok=true;
                    check(prefix+"_start",Vector3.Distance(g.player.transform.position,g.expeditionMap.spawn)<.4f,"Normal BeginExpedition spawn; no test teleport.");
                    if(g.expeditionJammer)
                    {
                        var jammer=g.expeditionMap.nodes.Find(n=>n.kind==ExpeditionNodeKind.Jammer);
                        yield return Visit(context,jammer,prefix+"_jammer");
                    }
                    foreach(var node in g.expeditionMap.nodes)
                    {
                        if(!context.ok)break;
                        if(node.kind==ExpeditionNodeKind.Objective)yield return Visit(context,node,prefix+"_objective_"+node.label);
                    }
                    if(context.ok)
                    {
                        float until=Time.time+25;
                        while(!g.ExpeditionMainComplete&&Time.time<until)
                        {ClearEnemies(g);yield return new WaitForSeconds(.2f);}
                        if(!g.ExpeditionMainComplete){context.ok=false;context.failure="Waves/main goal did not complete after route.";}
                    }
                    if(context.ok)yield return Visit(context,g.expeditionMap.nodes.Find(n=>n.kind==ExpeditionNodeKind.Extraction),prefix+"_return_extract");
                    bool complete=context.ok&&g.expeditionReceipt!=null&&g.expeditionReceipt.success;
                    check(prefix+"_completed",complete,"Physical walking "+context.walked.ToString("F1")+"m; real swaps="+context.transitions+"; "+(complete?"all objectives and return extraction":context.failure));
                    Release(context);
                    if(!complete&&g.expeditionActive&&g.expeditionReceipt==null)g.FinishExpedition(false);
                }
            }
            finally
            {
                InputSystem.QueueStateEvent(keys,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());
                InputSystem.RemoveDevice(keys);InputSystem.RemoveDevice(mouse);
                g.qaKeyboard=oldKeys;g.qaMouse=oldMouse;g.qaInputEnabled=oldInput;g.qaSuppressAI=oldSuppress;
                g.options=oldOptions;g.SyncLegacyBindings();g.ApplySettings(false);
            }
        }
        static IEnumerator Visit(Context c,ExpeditionNode node,string label)
        {
            if(!node){Fail(c,label,"Missing route node.");yield break;}
            var goals=InteractionPoints(c,node);
            if(goals.Count==0){Fail(c,label,"No standing interaction point on this node's floor.");yield break;}
            yield return Reach(c,goals,label);
            if(!c.ok)yield break;
            Release(c);Aim(c.P,node.Point);yield return new WaitForSeconds(.18f);
            if(c.g.FindExpeditionNode()!=node)
            {Fail(c,label,"Arrived by walking, but actual node query cannot interact. Position="+c.P.transform.position);yield break;}
            bool used=c.g.Interact();c.check(label+"_interaction",used,"Actual Interact from physically reached position "+c.P.transform.position);
            if(!used){c.ok=false;c.failure="Interaction rejected: "+node.label;}
        }
        static List<Vector3> InteractionPoints(Context c,ExpeditionNode node)
        {
            var result=new List<Vector3>();
            for(int radius=0;radius<2;radius++)for(int i=0;i<16;i++)
            {
                float angle=i*Mathf.PI*2/16,range=radius==0?1.75f:2.15f;
                Vector3 p=node.transform.position+new Vector3(Mathf.Sin(angle)*range,.02f,Mathf.Cos(angle)*range);
                if(!NavMesh.SamplePosition(p,out var nav,.35f,NavMesh.AllAreas)||Mathf.Abs(nav.position.y-node.transform.position.y)>.3f)continue;
                p=nav.position+Vector3.up*.035f;if(!StandingRoom(c,p,null))continue;
                if(Physics.Linecast(p+Vector3.up*1.62f,node.Point,out var hit,Layers.WorldMask)&&hit.collider.GetComponentInParent<ExpeditionNode>()!=node)continue;
                result.Add(p);
            }
            return result;
        }
        static IEnumerator Reach(Context c,List<Vector3> goals,string label)
        {
            float until=Time.time+150;int transitions=0;
            while(Time.time<until&&transitions<9)
            {
                ClearEnemies(c.g);
                var direct=BestPath(c.P.transform.position,goals);
                if(direct!=null)
                {
                    yield return Walk(c,direct,label);
                    yield break;
                }
                if(c.g.expeditionJammer){Fail(c,label,"Route disconnected while jammer remains active.");yield break;}
                Plan plan=PlanTransition(c,goals);
                if(plan==null){Fail(c,label,"No complete walk path or visible, reachable phase-anchor approach; player="+c.P.transform.position);yield break;}
                c.check(label+"_anchor_plan_"+transitions,true,"Walk from y="+c.P.transform.position.y.ToString("F2")+" to "+plan.approach+", swap to "+plan.anchor.transform.position+", goal island="+plan.reachesGoal+"; actual nav corners="+string.Join(" -> ",Array.ConvertAll(plan.path.corners,v=>v.ToString("F2"))));
                yield return Walk(c,plan.path,label+"_approach");
                if(!c.ok)yield break;
                Release(c);
                float cooldownUntil=Time.time+9;
                while(c.P.cooldown>.001f&&Time.time<cooldownUntil){ClearEnemies(c.g);yield return new WaitForSeconds(.1f);}
                Aim(c.P,plan.anchor.AimPoint);yield return new WaitForSeconds(.16f);
                Vector3 before=c.P.transform.position,destination=plan.anchor.transform.position;
                string reason=c.P.ValidateSwap(plan.anchor);
                if(reason!="可换位"||!c.P.RequestSwap(plan.anchor)){Fail(c,label,"Actual swap rejected at planned approach: "+reason);yield break;}
                yield return new WaitForSeconds(.22f);
                bool swapped=Vector3.Distance(c.P.transform.position,destination)<.2f&&Vector3.Distance(plan.anchor.transform.position,before)<.2f;
                c.check(label+"_actual_swap_"+transitions,swapped,"before="+before.ToString("F3")+"; destination="+destination.ToString("F3")+"; actualPlayer="+c.P.transform.position.ToString("F3")+"; actualAnchor="+plan.anchor.transform.position.ToString("F3")+"; playerError="+Vector3.Distance(c.P.transform.position,destination).ToString("F4")+"; anchorError="+Vector3.Distance(plan.anchor.transform.position,before).ToString("F4"));
                if(!swapped){c.ok=false;c.failure="Swap transaction did not reach both expected endpoints.";yield break;}
                c.transitions++;transitions++;
            }
            Fail(c,label,"Route planning limit reached without physically reaching goal.");
        }
        static NavMeshPath BestPath(Vector3 start,List<Vector3> goals)
        {
            NavMeshPath best=null;float shortest=float.MaxValue;
            foreach(var goal in goals)
            {
                var path=Path(start,goal);if(path==null)continue;float length=Length(path);
                if(length<shortest){shortest=length;best=path;}
            }
            return best;
        }
        static NavMeshPath Path(Vector3 start,Vector3 target)
        {
            if(!NavMesh.SamplePosition(start,out var from,.6f,NavMesh.AllAreas)||!NavMesh.SamplePosition(target,out var to,.45f,NavMesh.AllAreas))return null;
            if(Mathf.Abs(to.position.y-target.y)>.35f)return null;
            var path=new NavMeshPath();
            return NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete?path:null;
        }
        static float Length(NavMeshPath path){float result=0;for(int i=1;i<path.corners.Length;i++)result+=Vector3.Distance(path.corners[i-1],path.corners[i]);return result;}
        static Plan PlanTransition(Context c,List<Vector3> goals)
        {
            Plan best=null;float bestScore=float.MaxValue;
            // An approach can be on any authored floor reachable by a COMPLETE walking path.
            // Restricting probes to the player's current altitude misses legitimate stair descent.
            var heights=new List<float>();
            AddHeight(heights,c.P.transform.position.y);
            AddHeight(heights,c.g.expeditionMap.spawn.y);
            foreach(var node in c.g.expeditionMap.nodes)if(node)AddHeight(heights,node.transform.position.y);
            foreach(var beacon in c.g.expeditionMap.anchors)if(beacon)AddHeight(heights,beacon.transform.position.y);
            foreach(var anchor in c.g.expeditionMap.anchors)
            {
                if(!anchor||!anchor.Alive||!StandingRoom(c,anchor.transform.position,anchor))continue;
                // Same walkable island gives no topological progress; do not shuttle anchors pointlessly.
                if(Path(c.P.transform.position,anchor.transform.position)!=null)continue;
                bool goalIsland=BestPath(anchor.transform.position,goals)!=null;
                float remaining=float.MaxValue;foreach(var goal in goals)remaining=Mathf.Min(remaining,Vector3.Distance(anchor.transform.position,goal));
                var candidates=new List<Vector3>{c.P.transform.position};
                foreach(float height in heights)
                    foreach(float radius in new[]{10f,16f,20f})
                        for(int i=0;i<24;i++)
                        {
                            float a=i*Mathf.PI*2/24;
                            candidates.Add(new Vector3(anchor.transform.position.x+Mathf.Sin(a)*radius,height,anchor.transform.position.z+Mathf.Cos(a)*radius));
                        }
                foreach(var sample in candidates)
                {
                    // Match the selected real floor; do not project through several storeys.
                    if(!NavMesh.SamplePosition(sample,out var nav,1,NavMesh.AllAreas)||Mathf.Abs(nav.position.y-sample.y)>.65f)continue;
                    Vector3 approach=nav.position+Vector3.up*.035f,eye=approach+Vector3.up*1.62f;
                    if(Vector3.Distance(eye,anchor.AimPoint)>c.g.config.swapRange-.25f||!StandingRoom(c,approach,anchor))continue;
                    if(Physics.Linecast(eye,anchor.AimPoint,Layers.WorldMask))continue;
                    var path=Path(c.P.transform.position,approach);if(path==null)continue;
                    float score=Length(path)+remaining*1.5f-(goalIsland?1000:0);
                    if(score>=bestScore)continue;bestScore=score;best=new Plan{anchor=anchor,approach=approach,path=path,score=score,reachesGoal=goalIsland};
                }
            }
            return best;
        }
        static void AddHeight(List<float> heights,float height)
        {
            foreach(float existing in heights)if(Mathf.Abs(existing-height)<.2f)return;
            heights.Add(height);
        }
        static IEnumerator Walk(Context c,NavMeshPath path,string label)
        {
            var corners=path.corners;
            if(corners.Length==0){Fail(c,label,"Complete path has no corners.");yield break;}
            Vector3 last=c.P.transform.position;
            foreach(var corner in corners)
            {
                float until=Time.time+Mathf.Max(6,Vector3.Distance(c.P.transform.position,corner)/2+5);
                float progressAt=Time.time;Vector3 progressPoint=c.P.transform.position;
                while(FlatDistance(c.P.transform.position,corner)>.32f||Mathf.Abs(c.P.transform.position.y-corner.y)>.4f)
                {
                    if(!c.g.IsPlaying){Fail(c,label,"Game stopped during physical navigation.");Release(c);yield break;}
                    if(Time.time>until){Fail(c,label,"Timed out walking to nav corner "+corner+"; actual="+c.P.transform.position);Release(c);yield break;}
                    Vector3 delta=corner-c.P.transform.position;
                    if(new Vector2(delta.x,delta.z).sqrMagnitude>.01f)c.P.SetLook(Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,0);
                    InputSystem.QueueStateEvent(c.keys,new KeyboardState(Key.W));
                    ClearEnemies(c.g);yield return null;
                    Vector3 now=c.P.transform.position;c.walked+=Vector3.Distance(now,last);last=now;
                    if(Time.time-progressAt>1.25f)
                    {
                        if(Vector3.Distance(now,progressPoint)<.12f){Fail(c,label,"Controller stuck despite complete NavMesh path, corner="+corner+"; actual="+now);Release(c);yield break;}
                        progressAt=Time.time;progressPoint=now;
                    }
                }
                Release(c);yield return new WaitForSeconds(.12f);
            }
            c.check(label+"_walk",true,"Real W movement reached path endpoint "+c.P.transform.position+"; cumulative "+c.walked.ToString("F1")+"m");
        }
        static bool StandingRoom(Context c,Vector3 feet,PhaseAnchor ignored)
        {
            if(!Physics.Raycast(feet+Vector3.up*.18f,Vector3.down,.45f,Layers.WorldMask))return false;
            foreach(var collider in Physics.OverlapCapsule(feet+Vector3.up*.37f,feet+Vector3.up*1.46f,.285f,Layers.CombatMask,QueryTriggerInteraction.Ignore))
            {
                if(collider.transform==c.P.transform||collider.transform.IsChildOf(c.P.transform))continue;
                if(ignored&&(collider.transform==ignored.transform||collider.transform.IsChildOf(ignored.transform)))continue;
                return false;
            }
            return true;
        }
        static float FlatDistance(Vector3 a,Vector3 b)=>new Vector2(a.x-b.x,a.z-b.z).magnitude;
        static void Aim(PlayerMotor p,Vector3 point){Vector3 d=(point-p.Eye.position).normalized;p.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Asin(d.y)*Mathf.Rad2Deg);}
        static void Release(Context c){InputSystem.QueueStateEvent(c.keys,new KeyboardState());InputSystem.QueueStateEvent(c.mouse,new MouseState());}
        static void ClearEnemies(DemoGame g){foreach(var enemy in g.enemies.ToArray())if(enemy&&enemy.Alive)enemy.Damage(100000);}
        static void Fail(Context c,string label,string detail){c.ok=false;c.failure=detail;c.check(label+"_route_failure",false,detail);}
    }
}
