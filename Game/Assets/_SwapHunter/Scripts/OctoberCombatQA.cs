using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace SwapHunter
{
    // Opt-in native observation fixture. It uses the shipped chapter geometry,
    // NavMesh, unsuppressed ordinary AI and actual projectiles. It never writes
    // an enemy's health or completes an objective/kill on the player's behalf.
    public sealed class OctoberCombatQA : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name,detail; public bool passed; }
        [Serializable] public sealed class Frame
        {
            public string phase,state;public float time,distance,playerHealth,enemyHealth,muzzleHeight;
            public Vector3 playerFeet,enemyFeet,lastKnownFeet,destination;
            public int shots,hits,heardShots,pursuitDecisions,hazardDecisions,muzzleBlockedShots;
            public bool seesPlayer,hasMemory,completePath,avoidingDanger,shieldBlocks;
        }
        [Serializable] public sealed class Report
        {
            public string kind="October native combat: actual chapter 12 map, normal AI, real projectile collision, no forced enemy damage or mission completion";
            public string fixture="One ordinary Assault enemy per isolated observation. Other mission actors remain alive but have suppressAI=true to attribute shots/hits. Player is moved only between cases using clear NavMesh feet; one real RequestSwap is used. Player fixture HP=100000 prevents early termination; health loss is observed, never fabricated. TimeScale=1.";
            public string unity,startedUtc;public bool passed;public int chapter=12,navCandidates,plannedBudget;
            public List<Check> checks=new List<Check>();public List<Frame> frames=new List<Frame>();
        }
        readonly Report report=new Report();readonly List<Vector3> candidates=new List<Vector3>();
        DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
        EnemyActor actor;DirectionalBarrier shield;PhaseAnchor anchor;
        string output;bool done;float began;Vector3 farA,farB,nearA,nearB,hiddenA,hiddenB;

        void Awake()
        {
            began=Time.realtimeSinceStartup;var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-qaOutput");
            output=index>=0&&index+1<args.Length?args[index+1]:System.IO.Path.Combine(RunStorage.Root,"october-combat");
            Directory.CreateDirectory(output);report.unity=Application.unityVersion;report.startedUtc=DateTime.UtcNow.ToString("O");Application.logMessageReceived+=Log;
        }
        void Log(string message,string stack,LogType type)
        {if(!done&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))Add("runtime_error",false,message+"\n"+stack);}
        void Update(){if(!done&&Time.realtimeSinceStartup-began>170){Add("watchdog",false,"170 seconds without completion");Finish();}}
        void Add(string name,bool passed,string detail)
        {report.checks.Add(new Check{name=name,passed=passed,detail=detail});Debug.Log("OCTOBER_COMBAT_QA "+name+" "+passed+" "+detail);}
        void Look(Vector3 point)
        {Vector3 d=point-P.Eye.position;P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);}
        void HoldOtherActors()
        {if(G.StoryModal)G.CloseStoryModal();foreach(var enemy in G.enemies)if(enemy&&enemy!=actor)enemy.suppressAI=true;}
        void RecordFrame(string phase)
        {
            if(!actor||!actor.tactical)return;var ai=actor.tactical;var hazard=actor.GetComponent<EnemyHazardAwareness>();
            report.frames.Add(new Frame{phase=phase,time=Time.realtimeSinceStartup-began,state=ai.State.ToString(),distance=Vector3.Distance(actor.transform.position,P.transform.position),playerFeet=P.transform.position,enemyFeet=actor.transform.position,lastKnownFeet=ai.LastKnownFeet,destination=actor.agent&&actor.agent.enabled&&actor.agent.isOnNavMesh?actor.agent.destination:actor.transform.position,playerHealth=P.health,enemyHealth=actor.health,muzzleHeight=actor.MuzzlePosition.y-actor.transform.position.y,muzzleBlockedShots=actor.MuzzleBlockedShots,shots=actor.shotsFired,hits=actor.ProjectileHits,heardShots=ai.HeardShots,pursuitDecisions=ai.PursuitDecisions,hazardDecisions=hazard?hazard.Decisions:0,seesPlayer=ai.SeesPlayer,hasMemory=ai.HasPlayerMemory,completePath=actor.agent&&actor.agent.enabled&&actor.agent.isOnNavMesh&&actor.agent.pathStatus==NavMeshPathStatus.PathComplete,avoidingDanger=ai.EvadingKnownDanger,shieldBlocks=shield&&DirectionalBarrier.BlocksSegment(actor.MuzzlePosition,P.controller.bounds.center)});
        }
        IEnumerator Observe(string phase,float seconds)
        {
            float until=Time.realtimeSinceStartup+seconds,next=0;
            while(!done&&Time.realtimeSinceStartup<until)
            {
                HoldOtherActors();if(actor)Look(actor.AimPoint);
                if(Time.realtimeSinceStartup>=next){next=Time.realtimeSinceStartup+.2f;RecordFrame(phase);}yield return null;
            }
        }
        IEnumerator Capture(string name)
        {yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(output,name+".png"));yield return null;}
        bool Feet(Vector3 value,out Vector3 result)
        {
            result=value;if(!NavMesh.SamplePosition(value,out var n,.8f,NavMesh.AllAreas)||Mathf.Abs(n.position.y-value.y)>.8f)return false;
            if(!EnemyMotion.GroundAt(n.position,out var ground)||!EnemyMotion.ClearVolume(ground+Vector3.up*.035f,.38f,1.8f,P.transform))return false;
            result=ground+Vector3.up*.035f;return true;
        }
        static bool Path(Vector3 a,Vector3 b,out float length)
        {
            length=0;var path=new NavMeshPath();if(!NavMesh.CalculatePath(a,b,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
            for(int i=1;i<path.corners.Length;i++)length+=Vector3.Distance(path.corners[i-1],path.corners[i]);return true;
        }
        static bool ClearSight(Vector3 a,Vector3 b)=>!Physics.Linecast(a+Vector3.up*1.12f,b+Vector3.up*.95f,Layers.WorldMask)&&!Physics.Linecast(a+Vector3.up*1.12f,b+Vector3.up*1.62f,Layers.WorldMask);
        void FindCandidates()
        {
            var tri=NavMesh.CalculateTriangulation();Vector3 spawn=P.transform.position;
            for(int i=0;i+2<tri.indices.Length&&candidates.Count<220;i+=3)
            {
                Vector3 value=(tri.vertices[tri.indices[i]]+tri.vertices[tri.indices[i+1]]+tri.vertices[tri.indices[i+2]])/3;
                if(Vector3.Distance(value,spawn)>85||Mathf.Abs(value.y-spawn.y)>1.5f||!Feet(value,out var feet)||!Path(spawn,feet,out _))continue;
                bool crowded=false;foreach(var existing in candidates)if(Vector3.Distance(existing,feet)<1.5f){crowded=true;break;}if(!crowded)candidates.Add(feet);
            }
            report.navCandidates=candidates.Count;
        }
        bool FindPair(float min,float max,bool clear,bool detour,out Vector3 a,out Vector3 b)
        {
            a=b=Vector3.zero;float best=float.NegativeInfinity;
            for(int i=0;i<candidates.Count;i++)for(int j=i+1;j<candidates.Count;j++)
            {
                float distance=Vector3.Distance(candidates[i],candidates[j]);if(distance<min||distance>max||ClearSight(candidates[j],candidates[i])!=clear)continue;
                if(!Path(candidates[j],candidates[i],out float length)||length>distance+24||detour&&length<distance+2)continue;
                float score=detour?length-distance:distance;if(score<=best)continue;best=score;a=candidates[i];b=candidates[j];
            }
            return !float.IsNegativeInfinity(best);
        }
        void ClearActor()
        {
            if(actor){G.enemies.Remove(actor);actor.gameObject.SetActive(false);Destroy(actor.gameObject);actor=null;}
            if(shield){Destroy(shield.gameObject);shield=null;}
            if(anchor){G.anchors.Remove(anchor);Destroy(anchor.gameObject);anchor=null;}
            foreach(var bolt in FindObjectsByType<Bolt>(FindObjectsSortMode.None))if(bolt&&!bolt.IsRetired)bolt.Retire();
            foreach(var danger in EnemyV2Danger.ActiveSnapshot())if(danger)Destroy(danger.gameObject);
        }
        IEnumerator Fresh(Vector3 player,Vector3 enemy,bool facing=true)
        {
            ClearActor();yield return null;P.SetFeet(player);P.verticalSpeed=0;P.health=100000;P.UnequipV2Handheld();
            actor=EnemyActor.Spawn(EnemyKind.Assault,enemy,0);
            if(!actor){Add("ordinary_spawn",false,"Rejected clear NavMesh candidate "+enemy);yield break;}
            actor.suppressAI=false;Vector3 direction=Vector3.ProjectOnPlane(player-actor.transform.position,Vector3.up);actor.transform.rotation=Quaternion.LookRotation(facing?direction:-direction,Vector3.up);
            HoldOtherActors();Physics.SyncTransforms();Look(actor.AimPoint);yield return null;
            Add("spawn_clear",actor.agent.enabled&&actor.agent.isOnNavMesh&&EnemyMotion.ClearVolume(actor.transform.position+Vector3.up*.035f,actor.Radius,actor.Height,actor.transform),"feet="+actor.transform.position+";player="+P.transform.position+";ordinary assault, unsuppressed");
        }
        IEnumerator Start()
        {
            while(!G||!G.player||!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            if(!RunStorage.IsValidation){Add("isolated_storage",false,"Validation flag must select isolated storage before fixture runs");Finish();yield break;}
            Time.timeScale=1;G.qaSuppressAI=false;G.qaInputEnabled=false;P.enabled=true;P.SetViewActive(true);
            if(G.expeditionActive&&G.expeditionReceipt==null)G.AbandonExpedition();G.OpenExpeditionBoard();
            string profile=System.IO.Path.Combine(RunStorage.Root,"october-combat-profile");Directory.CreateDirectory(profile);File.WriteAllText(System.IO.Path.Combine(profile,"expedition-v6.json"),JsonUtility.ToJson(TacticalDemoProfile.Create(0,true)));G.expeditionStore=new ExpeditionStore(profile,true);
            bool beganRun=G.BeginExpedition(11);Add("native_chapter_12_started",beganRun,"notice="+G.expeditionNotice);if(!beganRun){Finish();yield break;}
            HoldOtherActors();yield return new WaitForSeconds(.6f);HoldOtherActors();report.plannedBudget=G.mission.Definition.TotalBudget;
            bool campaign=true;for(int level=0;level<12;level++)
            {
                var definition=MissionCatalog.Get(level);int mini=0;foreach(var group in definition.groups)if(!string.IsNullOrEmpty(group.miniBossId))mini++;
                bool valid=mini==1&&definition.TotalBudget==MissionCatalog.Defaults[level].TotalBudget&&definition.liveLimit==MissionCatalog.Defaults[level].liveLimit;campaign&=valid;
                Add("chapter_"+(level+1)+"_miniboss_budget",valid,"miniBossGroups="+mini+";budget="+definition.TotalBudget+";liveLimit="+definition.liveLimit);
            }
            FindCandidates();bool far=FindPair(22,35,true,false,out farA,out farB),near=FindPair(9,13,true,false,out nearA,out nearB),hidden=FindPair(8,17,false,true,out hiddenA,out hiddenB);
            Add("native_navmesh_fixtures",far&&near&&hidden,"candidates="+candidates.Count+";far="+far+";near="+near+";occluded_detour="+hidden);
            if(!far||!near||!hidden){Finish();yield break;}

            yield return Fresh(hiddenA,hiddenB,false);if(!actor){Finish();yield break;}
            P.SetLook(0,-78);int playerShots=P.shots;bool fired=P.Fire();int enemyShots=actor.shotsFired;float beforeHealth=P.health;
            yield return Observe("hearing_and_occluded_fire_gate",1.15f);
            Add("real_player_shot_alerts_occluded_enemy",fired&&P.shots>playerShots&&actor.tactical.HeardShots>0&&actor.tactical.HasPlayerMemory,"playerShot="+fired+";heard="+actor.tactical.HeardShots+";memory="+actor.tactical.HasPlayerMemory+";LOS="+actor.tactical.SeesPlayer);
            bool stayedHidden=true;foreach(var frame in report.frames)if(frame.phase=="hearing_and_occluded_fire_gate"&&frame.seesPlayer)stayedHidden=false;
            Add("no_shooting_or_damage_through_wall",stayedHidden&&actor.shotsFired==enemyShots&&P.health==beforeHealth,"occludedWholeWindow="+stayedHidden+";enemyShotsDelta="+(actor.shotsFired-enemyShots)+";healthDelta="+(beforeHealth-P.health));
            Vector3 beforeFeet=actor.transform.position;float hiddenStart=Vector3.Distance(beforeFeet,P.transform.position);
            yield return Observe("path_around_real_cover",5.5f);
            Add("investigates_via_real_nav_path",actor.tactical.PursuitDecisions>0&&Vector3.Distance(beforeFeet,actor.transform.position)>1.5f,"startDistance="+hiddenStart+";endDistance="+Vector3.Distance(actor.transform.position,P.transform.position)+";travelDisplacement="+Vector3.Distance(beforeFeet,actor.transform.position)+";pursuitDecisions="+actor.tactical.PursuitDecisions+";state="+actor.tactical.State);
            yield return Capture("01-investigation-around-cover");

            yield return Fresh(farA,farB);if(!actor){Finish();yield break;}
            float farStart=Vector3.Distance(actor.transform.position,P.transform.position);Vector3 farStartFeet=actor.transform.position;
            yield return Observe("distant_pursuit",7);
            float closest=farStart;foreach(var frame in report.frames)if(frame.phase=="distant_pursuit")closest=Mathf.Min(closest,frame.distance);
            Add("distant_enemy_advances",closest<farStart-3&&Vector3.Distance(farStartFeet,actor.transform.position)>3,"start="+farStart+";closest="+closest+";end="+Vector3.Distance(actor.transform.position,P.transform.position)+";state="+actor.tactical.State);
            yield return Capture("02-distant-pursuit");

            yield return Fresh(nearA,nearB);if(!actor){Finish();yield break;}
            float health=P.health;int shots=actor.shotsFired,hits=actor.ProjectileHits;
            yield return Observe("physical_enemy_projectiles",6);
            Add("real_projectiles_damage_player",actor.shotsFired>shots&&actor.ProjectileHits>hits&&P.health<health,"shots="+(actor.shotsFired-shots)+";reportedPhysicalHits="+(actor.ProjectileHits-hits)+";playerHealthBefore="+health+";after="+P.health);
            yield return Capture("03-real-projectile-combat");

            Vector3 oldPlayer=P.transform.position,swapFeet=Vector3.zero;bool swapCandidate=false;
            foreach(var point in candidates)if(Vector3.Distance(point,oldPlayer)>5&&Vector3.Distance(point,oldPlayer)<14&&ClearSight(oldPlayer,point)&&EnemyMotion.ClearVolume(point,.4f,1.8f,P.transform))
            {swapFeet=point;swapCandidate=true;break;}
            bool requested=false;int pursuits=actor.tactical.PursuitDecisions;
            if(swapCandidate)
            {
                var go=new GameObject("October QA actual swap anchor");go.layer=Layers.Actor;anchor=go.AddComponent<PhaseAnchor>();var capsule=go.AddComponent<CapsuleCollider>();capsule.radius=.32f;capsule.height=1.4f;capsule.center=Vector3.up*.7f;anchor.MoveTo(swapFeet);G.anchors.Add(anchor);Look(anchor.AimPoint);requested=P.RequestSwap(anchor);
            }
            yield return Observe("after_real_swap",4);
            Add("swap_triggers_new_search_or_firing_position",requested&&Vector3.Distance(P.transform.position,oldPlayer)>3&&(actor.tactical.PursuitDecisions>pursuits||Vector3.Distance(actor.tactical.LastKnownFeet,P.transform.position)<3),"candidate="+swapCandidate+";accepted="+requested+";playerDisplacement="+Vector3.Distance(P.transform.position,oldPlayer)+";pursuitBefore="+pursuits+";after="+actor.tactical.PursuitDecisions+";state="+actor.tactical.State+";lastKnown="+actor.tactical.LastKnownFeet);
            yield return Capture("04-search-after-swap");

            yield return Fresh(nearA,nearB);if(!actor){Finish();yield break;}
            Vector3 directionToEnemy=(actor.transform.position-P.transform.position).normalized;Vector3 center=Vector3.Lerp(P.transform.position,actor.transform.position,.35f)+Vector3.up*1.2f;
            shield=DirectionalBarrier.Spawn(center,directionToEnemy,12,P.transform);bool initiallyBlocked=DirectionalBarrier.BlocksSegment(actor.MuzzlePosition,P.controller.bounds.center);Vector3 shieldStart=actor.transform.position;
            yield return Observe("directional_shield_flank",5);
            float lateral=Vector3.ProjectOnPlane(actor.transform.position-shieldStart,directionToEnemy).magnitude;
            bool finallyBlocked=DirectionalBarrier.BlocksSegment(actor.MuzzlePosition,P.controller.bounds.center);
            Add("enemy_repositions_around_directional_shield",initiallyBlocked&&lateral>1&&!finallyBlocked,"initialBlock="+initiallyBlocked+";finalBlock="+finallyBlocked+";lateralMovement="+lateral+";shieldBlocks="+shield.Blocks+";state="+actor.tactical.State);
            yield return Capture("05-directional-shield-flank");

            yield return Fresh(nearA,nearB);if(!actor){Finish();yield break;}
            yield return Observe("hazard_ready",.7f);Vector3 dangerCenter=actor.transform.position;var hazard=actor.GetComponent<EnemyHazardAwareness>();int decisions=hazard?hazard.Decisions:0;
            EnemyV2Danger.Area(null,dangerCenter,3,1,6,.2f);yield return Observe("known_live_danger_escape",3.8f);
            float escaped=Vector3.ProjectOnPlane(actor.transform.position-dangerCenter,Vector3.up).magnitude;
            Add("enemy_leaves_known_danger",actor.Alive&&hazard&&hazard.Decisions>decisions&&escaped>2.7f,"known="+(hazard?hazard.KnownCount:0)+";decisionsBefore="+decisions+";after="+(hazard?hazard.Decisions:0)+";distanceFromDanger="+escaped+";enemyHealth="+actor.health);
            yield return Capture("06-danger-avoidance");
            int accounted=G.mission.UntriggeredBudget+G.mission.WarningBudget+G.mission.DeployedBudget+G.mission.CancelledBudget;
            Add("mission_budget_still_conserved",campaign&&accounted==report.plannedBudget,"planned="+report.plannedBudget+";accounted="+accounted+";no objectives or enemy health edited by fixture");
            Finish();
        }
        void Finish()
        {
            if(done)return;done=true;report.passed=report.checks.Count>0&&report.checks.TrueForAll(check=>check.passed);
            File.WriteAllText(System.IO.Path.Combine(output,"october-combat-report.json"),JsonUtility.ToJson(report,true));Debug.Log("OCTOBER_COMBAT_QA_COMPLETE passed="+report.passed);Application.Quit(report.passed?0:1);
        }
        void OnDestroy(){Application.logMessageReceived-=Log;}
    }
}
