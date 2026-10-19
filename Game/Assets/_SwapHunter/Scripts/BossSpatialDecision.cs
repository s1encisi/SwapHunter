using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SwapHunter
{
    // One observation is shared by scoring, target selection and the accepted cast.
    public sealed class BossSpatialObservation
    {
        public readonly Vector3 position;
        public readonly float observedAt, age, ground, highSeconds, stationarySeconds, reactionWindow;
        public readonly bool currentSight;
        public bool High => position.y > ground + 1.8f;
        public bool Low => position.y < ground + 1.2f;
        public BossSpatialObservation(Vector3 position, float observedAt, float age, float ground,
            float highSeconds, float stationarySeconds, float reactionWindow, bool currentSight)
        {
            this.position=position;this.observedAt=observedAt;this.age=age;this.ground=ground;
            this.highSeconds=highSeconds;this.stationarySeconds=stationarySeconds;
            this.reactionWindow=reactionWindow;this.currentSight=currentSight;
        }
    }
    [Serializable] public sealed class BossSpatialCandidate
    {
        public BossSkill skill; public bool legal; public float score; public string reason;
        [NonSerialized] public SwapTarget target;
    }
    [Serializable] public sealed class BossSpatialDecision
    {
        public int sequence; public string observation; public Vector3 position; public float observationAge, highSeconds, stationarySeconds;
        public BossSkill selected; public List<BossSpatialCandidate> candidates=new List<BossSpatialCandidate>();
    }
    public sealed partial class BossEncounter
    {
        const float MaximumObservationAge=1, HighHold=2.5f, StayRadius=2;
        Vector3 lastObservedPosition, stationOrigin;
        float lastObservedAt=-100, observedHighSeconds, observedStaySeconds, observedGround, observedReactionWindow=1.5f;
        bool previousSight;
        int observationFrame=-1, decisionSequence;
        BossSkill lastAcceptedSkill=BossSkill.None, previousAcceptedSkill=BossSkill.None;
        public BossSpatialDecision LastSpatialDecision { get; private set; }
        public BossSpatialObservation CurrentSpatialObservation { get; private set; }

        internal void ObserveSpatialTarget(float dt)
        {
            if(observationFrame==Time.frameCount||!G||!G.player||!Owner||!Owner.Alive)return;
            observationFrame=Time.frameCount;
            Vector3 candidate=G.player.transform.position;
            bool visible=(candidate-transform.position).sqrMagnitude<=40*40
                &&!Physics.Linecast(Owner.AimPoint,G.player.Eye.position,Layers.WorldMask,QueryTriggerInteraction.Ignore);
            if(!visible)
            {
                // An occlusion interrupts continuous occupation; remembering a location is not continued sight.
                previousSight=false;observedHighSeconds=0;observedStaySeconds=0;
                return;
            }
            float ground=GroundThreatFloor(candidate);
            if(!previousSight||(candidate-stationOrigin).sqrMagnitude>StayRadius*StayRadius)
            {stationOrigin=candidate;observedStaySeconds=0;}
            else observedStaySeconds+=dt;
            observedHighSeconds=candidate.y>ground+1.8f?(previousSight?observedHighSeconds+dt:0):0;
            previousSight=true;lastObservedPosition=candidate;lastObservedAt=Time.time;observedGround=ground;
            // This existing minimum-reaction allowance is frozen with the observation, never refreshed through a wall.
            observedReactionWindow=Mathf.Max(1.5f,G.player.cooldown+.45f);
        }
        public bool TryGetSpatialObservation(out BossSpatialObservation observation)
        {
            ObserveSpatialTarget(0);
            float age=Time.time-lastObservedAt;
            if(age>MaximumObservationAge||!G.player){observation=null;CurrentSpatialObservation=null;return false;}
            observation=new BossSpatialObservation(lastObservedPosition,lastObservedAt,age,observedGround,
                observedHighSeconds,observedStaySeconds,observedReactionWindow,previousSight);
            CurrentSpatialObservation=observation;return true;
        }
        BossSpatialDecision BuildSpatialDecision(BossSpatialObservation observed)
        {
            var decision=new BossSpatialDecision{sequence=++decisionSequence,observation=observed.currentSight?"current_sight":"last_seen",
                position=observed.position,observationAge=observed.age,highSeconds=observed.highSeconds,stationarySeconds=observed.stationarySeconds};
            Add(BossSkill.SwapElbow,1,"nearby exchange target",ChooseTarget(true,observed.position));
            Add(BossSkill.AdvantageSwap,1+(observed.highSeconds>=HighHold?3:0),observed.High?"high position":"not on high ground",observed.High?ChooseTarget(false,observed.position):null,observed.High);
            Add(BossSkill.Attraction,1+(observed.stationarySeconds>=HighHold?2:0),"observed area; stationary="+observed.stationarySeconds.ToString("F2"));
            bool laserExit=observed.Low&&HasMovementCounter(observed,false);
            Add(BossSkill.SweepLaser,1+(observed.stationarySeconds>=HighHold?2:0),laserExit?"low ground with traversable exit":"no verified ground-band exit",null,laserExit);
            bool waveExit=observed.Low&&HasMovementCounter(observed,true);
            Add(BossSkill.GroundWave,3,waveExit?"low ground with reachable elevation":"no reachable elevation in warning window",null,waveExit);
            Add(BossSkill.OmniFire,1,"existing cover/A-shield counter; normal mixed-phase candidate");
            ApplySpatialHistory(decision.candidates,lastAcceptedSkill,previousAcceptedSkill);
            return decision;
            void Add(BossSkill skill,float score,string reason,SwapTarget target=null,bool condition=true)
            {
                bool needsTarget=skill==BossSkill.SwapElbow||skill==BossSkill.AdvantageSwap;
                bool legal=SkillAllowed(skill)&&condition&&(!needsTarget||target);
                decision.candidates.Add(new BossSpatialCandidate{skill=skill,legal=legal,score=score,reason=reason+(SkillAllowed(skill)?"":"; phase excludes"),target=target});
            }
        }
        // Pure policy seam used by the focused D2 fixtures as well as production scoring.
        internal static void ApplySpatialHistory(List<BossSpatialCandidate> candidates,BossSkill last,BossSkill previous)
        {
            int legalCount=0;foreach(var c in candidates)if(c.legal)legalCount++;
            foreach(var c in candidates)
            {
                if(c.skill==last){c.score-=3;c.reason+="; last -3";}
                if(c.skill==previous){c.score-=1.5f;c.reason+="; previous -1.5";}
                if(c.legal&&legalCount>1&&c.skill==last&&c.skill==previous){c.legal=false;c.reason+="; third repeat excluded";}
            }
            candidates.Sort((a,b)=>a.legal!=b.legal?(a.legal?-1:1):b.score.CompareTo(a.score)!=0?b.score.CompareTo(a.score):((int)a.skill).CompareTo((int)b.skill));
        }
        bool HasMovementCounter(BossSpatialObservation observed,bool elevation)
        {
            if(!NavMesh.SamplePosition(observed.position,out var start,.8f,NavMesh.AllAreas))return false;
            Vector3 forward=Vector3.ProjectOnPlane(observed.position-transform.position,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
            // Groundwave's chapter-12 warning is 2.5s. Use ordinary walking, no consumable/skill assumptions.
            float warning=elevation?(G.expeditionActive&&G.expeditionLevel==11?2.5f:1.5f):observed.reactionWindow;
            float arrival=elevation?Vector3.ProjectOnPlane(observed.position-transform.position,Vector3.up).magnitude/12f:.9f;
            float maxTravel=Mathf.Max(2,G.config.walkSpeed*(warning+arrival)*.85f);
            var path=new NavMeshPath();
            for(int ring=0;ring<3;ring++)for(int i=0;i<12;i++)
            {
                float radius=elevation?3+ring*3:8+ring*2;
                float angle=i*Mathf.PI/6;
                Vector3 requested=observed.position+new Vector3(Mathf.Cos(angle)*radius,elevation?2.4f:0,Mathf.Sin(angle)*radius);
                if(!NavMesh.SamplePosition(requested,out var target,elevation?3f:1f,NavMesh.AllAreas))continue;
                if(elevation?target.position.y<observed.ground+1.25f:
                    target.position.y<observed.ground+1.25f&&Mathf.Abs(Vector3.Dot(target.position-observed.position,forward))<7.2f)continue;
                if(!EnemyMotion.ClearVolume(target.position,.32f,1.8f,G.player.transform))continue;
                if(!NavMesh.CalculatePath(start.position,target.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                float length=0;for(int k=1;k<path.corners.Length;k++)length+=Vector3.Distance(path.corners[k-1],path.corners[k]);
                if(length<=maxTravel)return true;
            }
            return false;
        }
        void RememberSpatialCast(BossSkill accepted)
        {previousAcceptedSkill=lastAcceptedSkill;lastAcceptedSkill=accepted;}
    }
}
