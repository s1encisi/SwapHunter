using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwapHunter
{
    public static class Shapes
    {
        public static DemoConfig Config;
        public static int EffectBudget => !DemoGame.I ? 120 : DemoGame.I.quality == 0 ? 60 : DemoGame.I.quality == 1 ? 90 : 120;
        public static Material Mat(int id) => Config.materials[Mathf.Clamp(id, 0, Config.materials.Length - 1)];
        public static GameObject Make(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, int material, bool solid = false, int layer = 0)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name; go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mat(material);
            Collider collider = go.GetComponent<Collider>();
            if (!solid) { collider.enabled = false; UnityEngine.Object.Destroy(collider); }
            return go;
        }
        public static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, int mat, bool solid = false, int layer = 0)
            => Make(name, PrimitiveType.Cube, parent, position, scale, mat, solid, layer);
        public static LineRenderer Line(Transform parent, Vector3 a, Vector3 b, Color color, float width)
        {
            GameObject go = new GameObject("Light trace"); go.layer = Layers.Effects; go.transform.SetParent(parent);
            LineRenderer line = go.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.positionCount = 2;
            line.SetPosition(0, a); line.SetPosition(1, b); line.startWidth = width; line.endWidth = width * .35f;
            line.sharedMaterial = Mat(7); line.startColor = color; line.endColor = color;
            MaterialPropertyBlock block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", color); block.SetColor("_Color", color); line.SetPropertyBlock(block);
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }
        public static LineRenderer Ring(Transform parent, Vector3 center, float radius, Color color, float width)
        {
            LineRenderer line = Line(parent, center, center, color, width); line.positionCount = 49; line.loop = true;
            for (int i = 0; i < 49; i++) { float angle = i * Mathf.PI * 2 / 48; line.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius); }
            return line;
        }
        public static void Trace(Vector3 a, Vector3 b, Color color, float seconds = .09f, float width = .035f)
        {
            LineRenderer line = Line(DemoGame.I.effects, a, b, color, width);
            line.gameObject.AddComponent<TimedEffect>().life = seconds;
        }
        static readonly System.Random visualRandom = new System.Random(37013);
        static Vector3 VisualDirection()
        {
            Vector3 v = new Vector3((float)visualRandom.NextDouble() * 2 - 1, (float)visualRandom.NextDouble() * 2 - 1, (float)visualRandom.NextDouble() * 2 - 1);
            return v.sqrMagnitude > .001f ? v.normalized : Vector3.up;
        }
        public static void Burst(Vector3 point, Color color, float size = .5f)
        {
            for (int i = 0; i < 7 && ImpactSpark.ActiveCount < EffectBudget; i++)
            {
                Vector3 direction = VisualDirection();
                var line = Line(DemoGame.I.effects, point, point + direction * size * .35f, color, .018f);
                var spark = line.gameObject.AddComponent<ImpactSpark>();
                spark.Configure(point, direction * size * 11, color, .14f + i * .009f, .018f);
            }
        }
        public static void Impact(Vector3 point, Vector3 normal, bool shield = false)
        {
            normal = normal.sqrMagnitude > .01f ? normal.normalized : Vector3.up;
            Color color = shield ? new Color(1, .92f, .70f) : new Color(1, .68f, .32f);
            for (int i = 0; i < 5 && ImpactSpark.ActiveCount < EffectBudget; i++)
            {
                Vector3 random = VisualDirection();
                Vector3 direction = shield ? (normal * .35f + Vector3.ProjectOnPlane(random, normal)).normalized : (normal * 1.5f + random).normalized;
                var line = Line(DemoGame.I.effects, point, point + direction * .08f, color, .015f);
                line.gameObject.AddComponent<ImpactSpark>().Configure(point + normal * .008f, direction * (1.5f + i * .45f), color, .18f + i * .016f, .015f);
            }
            // Small contact flash is short-lived, so repeated impacts do not hide the target.
            Trace(point - Vector3.Cross(normal, Vector3.up).normalized * .045f,
                point + Vector3.Cross(normal, Vector3.up).normalized * .045f, color, .06f, .025f);
        }
        public static void Echo(Vector3 feet)
        {
            var echo = Make("Phase afterimage", PrimitiveType.Capsule, DemoGame.I.effects, feet + Vector3.up * .9f, new Vector3(.62f, .9f, .62f), 9);
            echo.AddComponent<TimedEffect>().life = .7f;
            Ring(DemoGame.I.effects, feet + Vector3.up * .07f, .75f, new Color(.25f, 1, .92f), .035f).gameObject.AddComponent<TimedEffect>().life = .7f;
        }
    }
    public sealed class TimedEffect : MonoBehaviour
    {
        public float life = .3f;
        public Vector3 velocity;
        float initialLife, width; LineRenderer line;
        void Start() { initialLife = Mathf.Max(.001f, life); line = GetComponent<LineRenderer>(); if (line) width = line.startWidth; }
        void Update()
        {
            life -= Time.deltaTime; transform.position += velocity * Time.deltaTime;
            if (line) { float fade = Mathf.Clamp01(life / initialLife); line.startWidth = width * fade; line.endWidth = width * .35f * fade; }
            if (life <= 0) Destroy(gameObject);
        }
    }
    public sealed class ImpactSpark : MonoBehaviour
    {
        public static int ActiveCount { get; private set; }
        LineRenderer line; Vector3 head, velocity; Color color; float age, duration, width;
        public void Configure(Vector3 point, Vector3 speed, Color tint, float seconds, float thickness)
        { line = GetComponent<LineRenderer>(); head = point; velocity = speed; color = tint; duration = seconds; width = thickness; }
        void OnEnable() { ActiveCount++; }
        void OnDisable() { ActiveCount = Mathf.Max(0, ActiveCount - 1); }
        void Update()
        {
            float dt = Time.deltaTime; age += dt; if (!line || age >= duration) { Destroy(gameObject); return; }
            velocity += Vector3.down * 5 * dt; head += velocity * dt;
            float fade = 1 - age / duration;
            line.SetPosition(0, head); line.SetPosition(1, head - velocity * .023f * fade);
            line.startWidth = width * fade; line.endWidth = width * .2f * fade;
            line.startColor = line.endColor = color * Mathf.Lerp(.25f, 1, fade);
        }
    }

    // Independent original synthesis. Fixed FNV/xorshift seeds are stable across processes.
    public sealed class SoundBank : MonoBehaviour
    {
        const int Rate = 48000, Limit = 24;
        readonly System.Collections.Generic.Dictionary<string, AudioClip> clips = new System.Collections.Generic.Dictionary<string, AudioClip>();
        readonly System.Collections.Generic.Dictionary<string, float> last = new System.Collections.Generic.Dictionary<string, float>();
        readonly AudioSource[] voices = new AudioSource[Limit];
        readonly string[] names = new string[Limit];
        readonly float[] starts = new float[Limit];
        AudioSource ambient, music; AudioClip ambientClip, musicClip; int variation; readonly float[] voiceGains = new float[Limit];
        public int VoiceLimit => Limit;
        public bool ReviewExportSucceeded { get; private set; }
        public int ActiveVoiceCount { get { int n = 0; foreach (var v in voices) if (v && v.isPlaying) n++; return n; } }
        void Awake()
        {
            foreach (string id in new[] { "rifle","shotgun","phase","impact","death","warning","explosion","confirm","shield","kill","reload","reload_out","reload_in","reload_bolt","empty","step","land","player_hit","surface" }) clips[id] = Render(id);
            foreach (string id in new[] { "head_hit", "limb_hit", "vault", "crouch", "knife_swipe", "knife_throw", "knife_recall", "silence" }) clips[id] = Render(id);
            for (int w = 2; w < 12; w++) clips["weapon_" + w.ToString("00")] = Render("weapon_" + w.ToString("00"));
            for (int i = 0; i < Limit; i++)
            {
                var go = new GameObject("Audio voice " + i); go.transform.SetParent(transform, false);
                var a = go.AddComponent<AudioSource>(); a.playOnAwake = false; a.dopplerLevel = 0;
                a.rolloffMode = AudioRolloffMode.Logarithmic; a.minDistance = 2.5f; a.maxDistance = 48; voices[i] = a;
            }
            float[] bed = new float[Rate * 6];
            for (int i = 0; i < bed.Length; i++) { float t = i / (float)Rate; bed[i] = .012f * Tone(50,t) + .006f * Tone(100,t) + .007f * Tone(157f/6,t) * (.6f+.4f*Tone(1f/6,t)); }
            ambientClip = AudioClip.Create("port_ambience",bed.Length,1,Rate,false); ambientClip.SetData(bed,0);
            ambient = gameObject.AddComponent<AudioSource>(); ambient.clip = ambientClip; ambient.loop = true; ambient.priority = 240; ambient.volume = 0; ambient.Play();
            musicClip = PhaseScore.Create(); music = gameObject.AddComponent<AudioSource>(); music.clip = musicClip; music.loop = true; music.priority = 245; music.volume = 0; music.Play();
            string[] args = Environment.GetCommandLineArgs(); int ix = Array.IndexOf(args,"-swapHunterAudioReview");
            if (ix >= 0) ExportReview(ix+1 < args.Length && !args[ix+1].StartsWith("-") ? args[ix+1] : System.IO.Path.Combine(RunStorage.Root,"AudioReview"));
        }
        void Update()
        {
            if(!DemoGame.I)return;var o=DemoGame.I.options;
            ambient.volume=Mathf.MoveTowards(ambient.volume,DemoGame.I.IsPlaying?.19f*o.ambientVolume:0,Time.unscaledDeltaTime*.22f);
            music.volume=Mathf.MoveTowards(music.volume,(DemoGame.I.IsPlaying?.38f:.62f)*o.musicVolume,Time.unscaledDeltaTime*.3f);
            for(int i=0;i<Limit;i++)if(voices[i]&&voices[i].isPlaying)voices[i].volume=voiceGains[i]*MixVolume(names[i]);
        }
        static float MixVolume(string key)
        {if(string.IsNullOrEmpty(key)||!DemoGame.I)return 0;var o=DemoGame.I.options;if(key.StartsWith("confirm_"))return o.uiVolume;return Feedback(key.Replace("_world","").Replace("_local",""))?o.feedbackVolume:o.effectsVolume;}
        static uint Seed(string text) { unchecked { uint s=2166136261; foreach(char c in text) { s^=c; s*=16777619; } return s; } }
        static float Noise(ref uint s) { unchecked { s^=s<<13; s^=s>>17; s^=s<<5; } return (s&0xFFFFFF)/8388607.5f-1; }
        static float Tone(float hz,float t) => Mathf.Sin(2*Mathf.PI*hz*t);
        static float E(float t,float speed) => Mathf.Exp(-t*speed);
        static float Tap(float t,float delay,float hz,float gain) => t<delay ? 0 : Tone(hz,t-delay)*E(t-delay,95)*gain;
        static readonly float[] WeaponFundamentals={112,63,142,74,185,49,105,43,89,54,265,410};
        static AudioClip Render(string id)
        {
            bool weaponEvent=id.StartsWith("weapon_"); int weaponId=weaponEvent?int.Parse(id.Substring(7)):0;
            float duration=weaponEvent?.6f:id=="shotgun"?.52f:id=="rifle"?.32f:id=="explosion"?.95f:id=="phase"?.46f:id=="death"?.62f:.26f;
            float[] data=new float[Mathf.CeilToInt(Rate*duration)]; uint seed=Seed(id); float low=0,mid=0,phase=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)Rate,n=Noise(ref seed); low+=(n-low)*.032f; mid+=(n-mid)*.23f; float high=n-mid,v;
                if(weaponEvent)
                {
                    float hz=WeaponFundamentals[weaponId];
                    float decay=weaponId==4?46:weaponId==5||weaponId==7?14:25;
                    v=high*E(t,180)*.9f+Tone(hz,t)*E(t,decay)*.7f+mid*E(t,decay*.8f)*.75f+low*E(t,9)*.35f;
                    if(weaponId>=10)v=(Tone(hz+1100*t,t)*.3f+Tone(hz*1.5f,t)*.14f+mid*.14f)*E(t,8);
                    if(weaponId==9)v=Tone(54,t)*E(t,13)*.6f+low*E(t,12)+Tap(t,.1f,460,.2f);
                    if(weaponId==7)v+=Tap(t,.27f,700,.22f);
                }
                else if(id=="knife_swipe"||id=="knife_throw")v=(mid*.7f+high*.16f)*Mathf.Sin(Mathf.PI*t/duration)*E(t,5)+Tap(t,.11f,2200,.08f);
                else if(id=="knife_recall")v=(Tone(700+1400*t,t)*.13f+mid*.11f)*Mathf.Sin(Mathf.PI*t/duration);
                else if(id=="silence")v=(Tone(430-900*t,t)*.16f+Tone(650-1000*t,t)*.09f)*E(t,10);
                else if(id=="head_hit")v=(Tone(1250,t)*.17f+Tone(1875,t)*.09f+high*.15f)*E(t,40);
                else if(id=="limb_hit")v=(Tone(260,t)*.15f+mid*.5f)*E(t,58);
                else if(id=="vault"||id=="crouch")v=(low*.6f+mid*.15f+Tone(85,t)*.06f)*E(t,id=="vault"?13:30);
                else if(id=="rifle"||id=="shotgun")
                {
                    bool sg=id=="shotgun";
                    v=high*E(t,sg?155:220)*1.1f + Tone(sg?63:112,t)*E(t,sg?23:42)*.64f
                        + mid*E(t,sg?17:30)*(sg?1.12f:.75f) + low*E(t,sg?8:13)*.58f
                        + Tap(t,sg?.17f:.055f,sg?710:1450,.18f)+Tap(t,sg?.245f:.085f,310,.1f);
                }
                else if(id=="phase") { phase+=2*Mathf.PI*(300+2400*t*t)/Rate; v=(Mathf.Sin(phase)*.18f+Mathf.Sin(phase*1.51f)*.08f+mid*.14f)*Mathf.Sin(Mathf.PI*t/duration)*E(t,2); }
                else if(id=="explosion") v=(low*2.8f+mid*.32f+Tone(37,t)*.45f)*E(t,5.5f)+high*E(t,110)*.7f;
                else if(id=="death") v=(Tone(95-65*t,t)*.2f+low*.75f)*E(t,6)+Tap(t,.14f,280,.16f)+Tap(t,.28f,170,.12f);
                else if(id=="shield") v=(Tone(1620,t)*.18f+Tone(2347,t)*.1f)*E(t,20)+high*E(t,125)*.36f;
                else if(id=="impact") v=(mid*.7f+Tone(580,t)*.18f)*E(t,65);
                else if(id=="surface") v=(high*.42f+Tone(2350,t)*.1f+Tone(3217,t)*.06f)*E(t,52);
                else if(id=="kill"||id=="confirm") { float hz=id=="kill"?820:620; v=Tone(hz,t)*E(t,32)*.12f+Tap(t,.065f,hz*1.5f,.15f); }
                else if(id=="warning") v=Tone(960,t)*.17f*Mathf.Pow(Mathf.Sin(Mathf.PI*t/duration),2)*(.7f+.3f*Tone(18,t));
                else if(id=="step"||id=="land") v=(low*.65f+Tone(id=="land"?68:112,t)*.1f+high*.07f)*E(t,id=="land"?24:45);
                else if(id=="player_hit") v=(mid*.7f+Tone(125,t)*.3f)*E(t,32);
                else { float hz=id=="reload_out"?330:id=="reload_in"?490:id=="reload_bolt"?820:id=="empty"?1150:410; v=(high*.24f+Tone(hz,t)*.17f)*E(t,95)+Tap(t,id=="reload_bolt"?.07f:.04f,hz*.61f,.12f); }
                // Per-clip soft limiting and onset/end ramps; playback reserves further mix headroom.
                data[i]=(float)Math.Tanh(v*1.1f)*.62f*Mathf.Min(1,t/.0013f)*Mathf.Clamp01((duration-t)/.016f);
            }
            AudioClip clip=AudioClip.Create(id,data.Length,1,Rate,false); clip.SetData(data,0); return clip;
        }
        static bool Feedback(string id) => id=="impact"||id=="kill"||id=="shield"||id=="confirm"||id=="head_hit"||id=="limb_hit";
        static int Priority(string id) => id=="warning"||id=="phase"||id=="player_hit"?32:id=="rifle"||id=="shotgun"||id=="kill"?64:id=="step"||id=="surface"?180:100;
        public void Play(string id,Vector3? at=null)
        {
            if(!DemoGame.I)return;
            if(!clips.TryGetValue(id,out var clip)){id="confirm";clip=clips[id];}
            string key=id+(at.HasValue?"_world":"_local"); float cooldown=id=="surface"?.075f:id=="impact"||id=="shield"?.045f:id=="step"?.08f:.008f;
            if(last.TryGetValue(key,out float old)&&Time.unscaledTime-old<cooldown)return; last[key]=Time.unscaledTime;
            float volume=MixVolume(key); if(volume<=0)return;
            int pick=-1,same=0; float oldest=float.MaxValue;
            for(int i=0;i<Limit;i++){if(!voices[i].isPlaying){if(pick<0)pick=i;continue;}if(names[i]==key)same++;}
            int max=at.HasValue&&id=="rifle"?8:id=="rifle"||id=="shotgun"?4:3;
            if(same>=max||pick<0)
            {
                pick=-1;
                for(int i=0;i<Limit;i++){if(same>=max?(!voices[i].isPlaying||names[i]!=key):voices[i].priority<Priority(id))continue;if(starts[i]<oldest){oldest=starts[i];pick=i;}}
            }
            if(pick<0)return;
            var source=voices[pick];source.Stop();source.transform.position=at??transform.position;source.spatialBlend=at.HasValue?1:0;
            float obstruction=at.HasValue&&DemoGame.I.player&&Physics.Linecast(at.Value,DemoGame.I.player.Eye.position,Layers.WorldMask)?.48f:1;
            voiceGains[pick]=.58f*obstruction;source.volume=volume*voiceGains[pick];source.priority=Priority(id);source.pitch=id=="rifle"||id=="step"||id=="surface"?1+((variation++%5)-2)*.012f:1;
            source.clip=clip;names[pick]=key;starts[pick]=Time.unscaledTime;source.Play();ShowcaseCapture.Sound(id,source.volume*AudioListener.volume,source.pitch,at);
        }
        [Serializable] public sealed class ClipMeasurement { public string id,sha256; public int sampleRate,sampleCount; public float seconds,peak,rms,dcOffset; public bool finite; }
        [Serializable] public sealed class ReviewManifest { public string note="Original synthesis; PCM16 48kHz. Measurements do not validate listening quality."; public int voiceLimit=Limit; public ClipMeasurement[] clips; }
        public void ExportReview(string directory)
        {
            ReviewExportSucceeded = false;
            System.IO.Directory.CreateDirectory(directory);var list=new System.Collections.Generic.List<ClipMeasurement>();
            foreach(var pair in clips)list.Add(WriteWave(directory,pair.Key,pair.Value));list.Add(WriteWave(directory,"port_ambience",ambientClip));list.Add(WriteWave(directory,"phase_score",musicClip));
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"audio-review.json"),JsonUtility.ToJson(new ReviewManifest{clips=list.ToArray()},true));
            Debug.Log("Audio review exported: "+directory);
            ReviewExportSucceeded = true;
        }
        static ClipMeasurement WriteWave(string directory,string id,AudioClip clip)
        {
            float[] samples=new float[clip.samples];clip.GetData(samples,0);
            var result=new ClipMeasurement{id=id,sampleRate=clip.frequency,sampleCount=samples.Length,seconds=clip.length,finite=true};double sum=0,power=0;byte[] bytes;
            using(var memory=new System.IO.MemoryStream())
            {
                using(var writer=new System.IO.BinaryWriter(memory,System.Text.Encoding.ASCII,true))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples.Length*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);
                    writer.Write(clip.frequency);writer.Write(clip.frequency*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(samples.Length*2);
                    foreach(float sample in samples){bool finite=!float.IsNaN(sample)&&!float.IsInfinity(sample);result.finite&=finite;float v=finite?sample:0;sum+=v;power+=v*v;result.peak=Mathf.Max(result.peak,Mathf.Abs(v));writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(v,-1,1)*32767));}
                }
                bytes=memory.ToArray();
            }
            result.rms=(float)Math.Sqrt(power/samples.Length);result.dcOffset=(float)(sum/samples.Length);
            using(var sha=System.Security.Cryptography.SHA256.Create())result.sha256=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,id+".wav"),bytes);return result;
        }
        void OnDestroy(){foreach(var pair in clips)if(pair.Value)Destroy(pair.Value);if(ambientClip)Destroy(ambientClip);if(musicClip)Destroy(musicClip);}
    }
    public sealed class Bolt : MonoBehaviour
    {
        public Vector3 velocity; public float damage, life = 3;
        public EnemyActor owner;
        public static Bolt Spawn(EnemyActor source, Vector3 origin, Vector3 direction, float speed, float damage)
        {
            GameObject go = new GameObject("Enemy projectile"); go.layer = Layers.Effects;
            go.transform.SetParent(DemoGame.I.effects, false); go.transform.localPosition = origin;
            Bolt bolt = go.AddComponent<Bolt>(); bolt.owner = source; bolt.velocity = direction.normalized * speed; bolt.damage = damage;
            EnemyProjectileVisual.Attach(bolt);
            return bolt;
        }
        void Update()
        {
            if (!DemoGame.I.IsPlaying) return;
            Vector3 old = transform.position; Vector3 travel = velocity * Time.deltaTime;
            var tactics=DemoGame.I.player?DemoGame.I.player.tactics:null;
            if(tactics&&tactics.Intercept(old,old+travel,out var barrierHit)){transform.position=barrierHit;Destroy(gameObject);return;}
            if (CombatRay.Cast(old, travel.normalized, travel.magnitude, owner ? owner.transform : null, out RaycastHit hit))
            {
                PlayerMotor player = hit.collider.GetComponentInParent<PlayerMotor>();
                EnemyActor enemy = hit.collider.GetComponentInParent<EnemyActor>();
                if (player) player.Hurt(damage, old);
                else if (enemy && enemy != owner) enemy.Hit(damage, hit.point, hit.collider, old);
                Shapes.Impact(hit.point, hit.normal); if (!player && !enemy) DemoGame.I.sound.Play("surface", hit.point); Destroy(gameObject); return;
            }
            transform.position += travel; life -= Time.deltaTime;
            if (life <= 0) Destroy(gameObject);
        }
    }
    public static class CombatRay
    {
        public static bool Cast(Vector3 origin, Vector3 direction, float range, Transform ignore, out RaycastHit nearest)
        {
            nearest = default; float distance = float.MaxValue; bool found = false;
            foreach (var hit in Physics.RaycastAll(origin, direction, range, Layers.CombatMask, QueryTriggerInteraction.Ignore))
            {
                if (ignore && (hit.collider.transform == ignore || hit.collider.transform.IsChildOf(ignore))) continue;
                if (hit.distance < distance) { distance = hit.distance; nearest = hit; found = true; }
            }
            return found;
        }
    }
    public sealed class GrenadeActor : MonoBehaviour
    {
        public float remaining;
        LineRenderer ring; Rigidbody body;
        public static GrenadeActor Spawn(Vector3 origin, Vector3 destination, float fuse = -1)
        {
            GameObject go = Shapes.Make("LIVE GRENADE", PrimitiveType.Sphere, DemoGame.I.effects, origin, Vector3.one * .22f, 6, true, Layers.Grenade);
            Rigidbody rb = go.AddComponent<Rigidbody>(); rb.mass = .35f; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Vector3 delta = destination - origin; float horizontal = new Vector2(delta.x, delta.z).magnitude;
            float flight = Mathf.Clamp(horizontal / 8 + .4f, .9f, 1.8f);
            rb.linearVelocity = horizontal < 1 ? Vector3.zero : delta / flight - Physics.gravity * flight * .5f;
            GrenadeActor grenade = go.AddComponent<GrenadeActor>(); grenade.body = rb;
            grenade.remaining = fuse > 0 ? fuse : DemoGame.I.config.grenadeFuse;
            grenade.ring = Shapes.Ring(DemoGame.I.effects, destination + Vector3.up * .05f, DemoGame.I.config.grenadeRadius, new Color(1, .23f, .12f), .045f);
            DemoGame.I.sound.Play("warning", origin); return grenade;
        }
        void Update()
        {
            if (!DemoGame.I.IsPlaying) return;
            remaining -= Time.deltaTime;
            Vector3 floor = transform.position;
            if (Physics.Raycast(floor + Vector3.up, Vector3.down, out var hit, 20, Layers.WorldMask)) floor = hit.point + Vector3.up * .06f;
            for (int i = 0; i < ring.positionCount; i++) { float a = i * Mathf.PI * 2 / 48; ring.SetPosition(i, floor + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * DemoGame.I.config.grenadeRadius); }
            ring.startWidth = ring.endWidth = .035f + Mathf.Sin(Time.time * 18) * .012f;
            if (remaining <= 0) Explode();
        }
        public void Explode()
        {
            var game = DemoGame.I; Vector3 center = transform.position + Vector3.up * .15f;
            float radius = game.config.grenadeRadius;
            if (game.player) Blast(game.player.transform.position, game.player.Eye.position, d => game.player.Hurt(d, center));
            foreach (EnemyActor enemy in game.enemies.ToArray())
                if (enemy && enemy.Alive) Blast(enemy.transform.position, enemy.AimPoint, d => enemy.Damage(d));
            Shapes.Burst(center, new Color(1, .6f, .12f), radius);
            Shapes.Ring(game.effects, center, radius, new Color(1, .7f, .2f), .1f).gameObject.AddComponent<TimedEffect>().life = .25f;
            game.sound.Play("explosion", center); game.Record("grenade_explode", center.ToString());
            if (ring) Destroy(ring.gameObject); Destroy(gameObject);
            void Blast(Vector3 feet, Vector3 point, Action<float> apply)
            {
                float distance = Vector3.Distance(center, feet + Vector3.up * .4f);
                if (distance >= radius || Physics.Linecast(center, point, Layers.WorldMask)) return;
                apply(game.config.grenadeDamage * Mathf.Lerp(1, .2f, distance / radius));
            }
        }
        void OnDestroy() { if (ring) Destroy(ring.gameObject); }
        void OnCollisionEnter(Collision collision)
        {
            if (!body || collision.contactCount == 0) return;
            Vector3 normal = collision.GetContact(0).normal;
            if (normal.y > .5f) { body.linearVelocity = Vector3.ProjectOnPlane(body.linearVelocity, normal) * .18f; body.linearDamping = 2.8f; }
        }
    }
}





