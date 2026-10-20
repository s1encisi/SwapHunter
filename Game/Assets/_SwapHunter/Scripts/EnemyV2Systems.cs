using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace SwapHunter
{
 public static class EnemyV2Definition
 {
  // This rebalance applies once to creature health at initialization. Devices and
  // the final boss's separate shield energy do not use this multiplier.
  public const float HealthScale=.5f;
  public static float ScaleHealth(float previousBase)=>previousBase*HealthScale;
  static readonly string[] labels={"校准训练机","突击兵","哨戒狙击手","实体盾卫","战术精英","屏障射手","跃袭追猎者","破障冲击兵","曲射掷弹兵","维修工兵","标记侦察机","反相干扰员","牵引控制者","架设压制机"};
  static readonly float[] addedHealth={960,720,1440,840,600,600,840,960,1200};
  public static string Name(EnemyKind k)=>labels[Mathf.Clamp((int)k,0,labels.Length-1)];
  public static float Health(EnemyKind k,DemoConfig config)
  {int n=(int)k;float previousBase=n>=5?addedHealth[n-5]:10*(k==EnemyKind.Sniper?config.sniperHealth:k==EnemyKind.Shield?config.shieldHealth:k==EnemyKind.Elite?config.eliteHealth:config.assaultHealth);return ScaleHealth(previousBase);}
 }
 public enum EnemyAbilityPhase { Ready, Telegraph, Execute, Recover }
 public static class EnemySkillReservations
 {
  sealed class Slot {public EnemyActor owner;public string channel;public float until;public Vector3 position;}
  static readonly List<Slot> slots=new List<Slot>();
  public static bool TryClaim(EnemyActor owner,string channel,float seconds,int maximum=1)
  {
   slots.RemoveAll(s=>!s.owner||!s.owner.Alive||Time.time>=s.until);int count=0;
   foreach(var s in slots)if(s.owner!=owner&&s.channel==channel&&Vector3.Distance(s.position,owner.transform.position)<45)count++;
   if(count>=maximum)return false;Release(owner);slots.Add(new Slot{owner=owner,channel=channel,until=Time.time+seconds,position=owner.transform.position});return true;
  }
  public static void Release(EnemyActor owner){slots.RemoveAll(s=>s.owner==owner);}
  public static void Hold(EnemyActor owner,float seconds){foreach(var s in slots)if(s.owner==owner)s.until=Mathf.Max(s.until,Time.time+seconds);}
 }
 // A separately targetable visible component. It never heals a body or rewinds a mission objective.
 public sealed class EnemyV2Device:MonoBehaviour
 {
  static readonly List<EnemyV2Device> active=new List<EnemyV2Device>();
  public float Health{get;private set;}public float MaximumHealth{get;private set;}
  public EnemyActor Carrier{get;private set;}public bool Repairable{get;private set;}public bool Repaired{get;private set;}
  public bool Alive=>Health>0&&isActiveAndEnabled;public Vector3 AimPoint=>transform.position;
  Action broken;Collider shape;Renderer shell;float repairBudget;
  public static EnemyV2Device[] ActiveSnapshot()=>active.ToArray();
  public static EnemyV2Device Create(Transform parent,Vector3 position,string label,float hp,bool repairable,EnemyActor carrier,Action onBroken)
  {
   var go=new GameObject(label);go.transform.SetParent(parent,true);go.transform.position=position;go.layer=Layers.Actor;
   var d=go.AddComponent<EnemyV2Device>();d.MaximumHealth=d.Health=hp;d.repairBudget=hp*.12f;d.Carrier=carrier;d.Repairable=repairable;d.broken=onBroken;
   d.shell=DeviceHousingV2.Create(go.transform,carrier,out var housingSize);
   var c=go.AddComponent<BoxCollider>();c.size=housingSize;d.shape=c;
   if(carrier)foreach(var other in carrier.GetComponentsInChildren<Collider>())if(other!=c)Physics.IgnoreCollision(c,other,true);
   D2WorldText.Create(go.transform,label+" "+hp+"HP",position+Vector3.up*.4f,.17f);return d;
  }
  void OnEnable(){active.Add(this);}void OnDisable(){active.Remove(this);}
  public bool ApplyDamage(float amount,Vector3 source)
  {
   if(!Alive||amount<=0)return false;Health=Mathf.Max(0,Health-amount);DemoGame.I.Record("enemy_device_damage",name+";remaining="+Health);DemoGame.I.sound.Play("impact",AimPoint);
   if(Health<=0){if(shape)shape.enabled=false;if(shell)shell.sharedMaterial=Shapes.Mat(1);broken?.Invoke();broken=null;DemoGame.I.Record("enemy_device_destroyed",name);}return true;
  }
  public bool Repair(float amount){if(!Alive||!Repairable||Repaired)return false;float restored=Mathf.Min(Mathf.Max(0,amount),repairBudget,MaximumHealth-Health);Health+=restored;repairBudget-=restored;if(repairBudget<=.001f)Repaired=true;return restored>0;}
  public void FinishRepair(){Repaired=true;}
 }
 public sealed class EnemyV2DynamicField:MonoBehaviour
 {
  static readonly List<EnemyV2DynamicField> active=new List<EnemyV2DynamicField>();
  float remaining;PhaseInterferenceField field;public EnemyV2Device Device{get;private set;}
  public static bool BlocksQ(Vector3 point){foreach(var f in active)if(f&&f.field&&f.field.ContainsPoint(point))return true;return false;}
  public static bool CanDeploy(Vector3 position)
  {foreach(var f in active)if(f&&Vector3.Distance(f.transform.position,position)<45)return false;return true;}
  public static EnemyV2DynamicField Spawn(Vector3 position,float duration=4)
  {
   if(!CanDeploy(position))return null;var go=new GameObject("动态反相信标 / 144HP");go.transform.SetParent(DemoGame.I.effects);go.transform.position=position;
   var f=go.AddComponent<EnemyV2DynamicField>();f.remaining=duration;f.field=go.AddComponent<PhaseInterferenceField>();f.field.Configure("dynamic_"+go.GetInstanceID(),6);
   f.Device=EnemyV2Device.Create(go.transform,position+Vector3.up*.35f,"干扰信标",144,false,null,()=>{if(f)Destroy(f.gameObject);});return f;
  }
  void OnEnable(){active.Add(this);}void OnDisable(){active.Remove(this);}
  void Update(){if(DemoGame.I.IsPlaying&&(remaining-=Time.deltaTime)<=0)Destroy(gameObject);}
 }
 // Flying cord is a real straight projectile. The two-second slow starts only on a body hit.
 public sealed class EnemyV2Tether:MonoBehaviour
 {
  static readonly List<EnemyV2Tether> active=new List<EnemyV2Tether>();EnemyActor owner;Vector3 velocity;LineRenderer line;float remaining=2,life=1.5f,checkAt;bool attached;
  public static float PlayerMoveMultiplier{get{foreach(var t in active)if(t&&t.attached)return .75f;return 1;}}
  public static void Fire(EnemyActor owner,Vector3 aim)
  {var go=new GameObject("牵引弹索");go.transform.SetParent(DemoGame.I.effects);go.transform.position=owner.MuzzlePosition;var t=go.AddComponent<EnemyV2Tether>();t.owner=owner;t.velocity=(aim-go.transform.position).normalized*26;t.line=Shapes.Line(go.transform,go.transform.position,go.transform.position,new Color(1,.65f,.12f),.035f);}
  public static void BreakOwner(EnemyActor actor){foreach(var t in active.ToArray())if(t&&t.owner==actor)Destroy(t.gameObject);}
  public static void RecheckAll(){foreach(var t in active.ToArray())if(t&&t.attached){var p=DemoGame.I.player.transform.position+Vector3.up*.8f;if(!t.owner||Vector3.Distance(t.owner.AimPoint,p)>25||Physics.Linecast(t.owner.AimPoint,p,Layers.WorldMask))Destroy(t.gameObject);}}
  void OnEnable(){active.Add(this);}void OnDisable(){active.Remove(this);}
  void Update()
  {
   var g=DemoGame.I;if(!g||!g.IsPlaying)return;if(!owner||!owner.Alive||owner.Silenced){Destroy(gameObject);return;}
   if(!attached){Vector3 step=velocity*Time.deltaTime;if(CombatRay.Cast(transform.position,step.normalized,step.magnitude,owner.transform,out var h)){if(h.collider.GetComponentInParent<PlayerMotor>()){attached=true;g.Toast("牵引减速 · 绕柱断索 / {key:6}拉开 / 打断控制者",2);}else{Destroy(gameObject);return;}}else transform.position+=step;if((life-=Time.deltaTime)<=0){Destroy(gameObject);return;}}
   else
   {
    transform.position=g.player.transform.position+Vector3.up*.8f;if((remaining-=Time.deltaTime)<=0){Destroy(gameObject);return;}
    if(Time.time>=checkAt){checkAt=Time.time+.5f;if(Vector3.Distance(owner.AimPoint,transform.position)>25||Physics.Linecast(owner.AimPoint,transform.position,Layers.WorldMask)){Destroy(gameObject);return;}}
   }
   line.SetPosition(0,owner.MuzzlePosition);line.SetPosition(1,transform.position);
  }
 }
 public sealed class EnemyV2Danger:MonoBehaviour
 {
  static readonly List<EnemyV2Danger> active=new List<EnemyV2Danger>();
  public static EnemyV2Danger[] ActiveSnapshot()=>active.ToArray();
  public bool Live=>duration>0&&isActiveAndEnabled;public Vector3 Center=>center;public float Radius=>radius;public float DamagePerSecond=>damage;public float SecondsLeft=>duration;
  void OnEnable(){active.Add(this);}void OnDisable(){active.Remove(this);}
  Vector3 center,forward;float radius,damage,duration,tick,interval;LineRenderer ring;EnemyActor source;bool rectangle,shieldExplosion;float width,length;
  public static void Area(EnemyActor owner,Vector3 center,float radius,float damage,float seconds=.1f,float interval=.8f,bool shieldExplosion=false)
  {Create(owner,center,radius,damage,seconds,interval,false,Vector3.forward,0,0,shieldExplosion);}
  public static void Rectangle(EnemyActor owner,Vector3 center,Vector3 forward,float width,float length,float damage)
  {Create(owner,center,0,damage,.12f,.8f,true,forward,width,length,false);}
  static void Create(EnemyActor owner,Vector3 center,float radius,float damage,float seconds,float interval,bool rect,Vector3 direction,float width,float length,bool shieldExplosion)
  {
   var go=new GameObject("敌方危险 / 世界锁点");go.transform.SetParent(DemoGame.I.effects);var x=go.AddComponent<EnemyV2Danger>();x.source=owner;x.center=center;x.radius=radius;x.damage=damage;x.duration=seconds;x.interval=interval;x.rectangle=rect;x.forward=direction;x.width=width;x.length=length;x.shieldExplosion=shieldExplosion;
   x.ring=Shapes.Ring(go.transform,center+Vector3.up*.04f,rect?length*.5f:radius,new Color(1,.25f,.08f),.09f);Shapes.Burst(center,new Color(1,.4f,.1f),rect?1:radius);DemoGame.I.sound.Play("explosion",center);
  }
  bool Contains(Vector3 p){Vector3 d=p-center;if(Mathf.Abs(d.y)>1.8f)return false;if(!rectangle)return Vector3.ProjectOnPlane(d,Vector3.up).sqrMagnitude<radius*radius;var right=Vector3.Cross(Vector3.up,forward);return Mathf.Abs(Vector3.Dot(d,right))<width*.5f&&Mathf.Abs(Vector3.Dot(d,forward))<length*.5f;}
  void Update()
  {
   var g=DemoGame.I;if(!g.IsPlaying)return;duration-=Time.deltaTime;tick-=Time.deltaTime;
   if(tick<=0){tick=interval;Vector3 origin=center+Vector3.up*.35f;float value=damage*(interval<.5f?interval:1);
    Vector3 playerPart=g.player.controller?g.player.controller.bounds.center:g.player.transform.position+Vector3.up*.8f;
    if(Contains(g.player.transform.position)&&!Physics.Linecast(origin,playerPart,Layers.WorldMask))g.player.Hurt(value*(shieldExplosion?DirectionalBarrier.ExplosionMultiplier(origin,playerPart):1),origin,false);
    var batch=new DamageBatch(DamageSource.EnemyGrenade,source?source.transform:null,origin);long id=batch.NewEvent();
    foreach(var e in g.enemies)if(e&&e.Alive&&Contains(e.transform.position)&&!Physics.Linecast(origin,e.AimPoint,Layers.WorldMask))batch.Enemy(e,value*(e.IsBoss?4:10)*(shieldExplosion?DirectionalBarrier.ExplosionMultiplier(origin,e.AimPoint):1),id);batch.Apply();
   }if(duration<=0)Destroy(gameObject);
  }
 }
 public sealed class EnemyV2MortarShell:MonoBehaviour
 {
  EnemyActor owner;Vector3 start,end;float elapsed,flight=.8f,fuse=.7f;bool landed;LineRenderer ring;
  public static bool ArcClear(Vector3 start,Vector3 end)
  {Vector3 prev=start;for(int i=1;i<=12;i++){float t=i/12f;Vector3 p=Vector3.Lerp(start,end,t)+Vector3.up*(4*Mathf.Sin(t*Mathf.PI));if(Physics.Linecast(prev,p,Layers.WorldMask))return false;prev=p;}return true;}
  public static void Spawn(EnemyActor owner,Vector3 end)
  {var go=Shapes.Make("曲射实体弹",PrimitiveType.Sphere,DemoGame.I.effects,owner.AimPoint+Vector3.up*.4f,Vector3.one*.22f,6,false,Layers.Effects);var s=go.AddComponent<EnemyV2MortarShell>();s.owner=owner;s.start=go.transform.position;s.end=end;s.ring=Shapes.Ring(DemoGame.I.effects,end+Vector3.up*.06f,3,new Color(1,.3f,.1f),.07f);}
  void Update()
  {
   if(!DemoGame.I.IsPlaying)return;if(!landed){elapsed+=Time.deltaTime;float t=Mathf.Clamp01(elapsed/flight);Vector3 p=Vector3.Lerp(start,end,t)+Vector3.up*4*Mathf.Sin(t*Mathf.PI);if(Physics.Linecast(transform.position,p,out var hit,Layers.WorldMask)){transform.position=hit.point;end=hit.point;landed=true;}else transform.position=p;if(t>=1)landed=true;}
   else if((fuse-=Time.deltaTime)<=0){EnemyV2Danger.Area(owner,end,3,24,shieldExplosion:true);Destroy(gameObject);}
  }
  void OnDestroy(){if(ring)Destroy(ring.gameObject);}
 }
}
