using System.Collections.Generic;
using UnityEngine;

namespace SwapHunter
{
    public enum HomecomingMusicState { Menu, Explore, Combat, Boss, Ending }

    // The two Singularity versions share a timeline. Crossing between them keeps musical position.
    // Menu/Explore and Combat/Boss deliberately share themes rather than pretending to be five tracks.
    public sealed class AudioDirector : MonoBehaviour
    {
        const string Root = "Audio/N1/";
        readonly Dictionary<string, AudioClip> tracks = new Dictionary<string, AudioClip>();
        readonly AudioSource[] sources = new AudioSource[2];
        readonly float[] weights = new float[2];
        int destination;
        float combatUntil = -1, pollAt, radioUntil, warningUntil, mixLevel, duck = 1;
        bool initialized;
        public HomecomingMusicState CurrentState { get; private set; } = HomecomingMusicState.Menu;
        public int ImportedMusicTrackCount { get; private set; }
        public float DuckGain => duck;
        public float AudibleMusicGain => mixLevel * duck;
        public IEnumerable<KeyValuePair<string, AudioClip>> ReviewTracks => tracks;

        public void Initialize(AudioClip fallback)
        {
            if (initialized) return;
            foreach (string id in new[] { "music_explore", "music_combat", "music_ending" })
            {
                AudioClip clip = Resources.Load<AudioClip>(Root + id);
                if (clip) ImportedMusicTrackCount++;
                else { clip = fallback; Debug.LogWarning("N1 music resource missing; original synthesized score is used: " + Root + id); }
                tracks.Add(id, clip);
            }
            for (int i = 0; i < sources.Length; i++)
            {
                var child = new GameObject("Homecoming music " + i);
                child.transform.SetParent(transform, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = true; source.priority = 220;
                source.spatialBlend = 0; source.volume = 0; sources[i] = source;
            }
            initialized = true;
            SwitchTo(HomecomingMusicState.Menu);
        }

        public void RadioCue(float seconds = 2)
        { radioUntil = Mathf.Max(radioUntil, Time.unscaledTime + Mathf.Clamp(seconds, .2f, 15)); }

        public void NotifyAction(string id, Vector3? at)
        {
            var game = DemoGame.I;
            if (!game || !game.IsPlaying) return;
            if (id == "warning") warningUntil = Time.unscaledTime + .8f;
            bool combat = id == "rifle" || id == "shotgun" || id.StartsWith("weapon_") || id == "explosion" || id == "player_hit";
            if (!combat) return;
            if (at.HasValue && game.player && (at.Value - game.player.transform.position).sqrMagnitude > 40 * 40) return;
            combatUntil = Mathf.Max(combatUntil, Time.time + 6);
        }

        void Update()
        {
            var game = DemoGame.I;
            if (!initialized || !game) return;
            if (game.StoryEndingVisible) SwitchTo(HomecomingMusicState.Ending);
            else if (game.state == RunState.Menu || game.state == RunState.Loading)
            { combatUntil = -1; SwitchTo(HomecomingMusicState.Menu); }
            else if (game.state == RunState.Victory) SwitchTo(HomecomingMusicState.Ending);
            else if (game.IsPlaying && Time.unscaledTime >= pollAt)
            {
                pollAt = Time.unscaledTime + .3f;
                bool boss = false;
                if (game.player)
                {
                    Vector3 feet = game.player.transform.position;
                    foreach (var enemy in game.enemies)
                    {
                        if (!enemy || !enemy.Alive || enemy.kind == EnemyKind.Training) continue;
                        float distanceSquared = (enemy.transform.position - feet).sqrMagnitude;
                        if ((enemy.boss || enemy.miniBoss) && distanceSquared < 40 * 40) { boss = true; break; }
                        if (distanceSquared < 24 * 24 && !Physics.Linecast(enemy.AimPoint, game.player.Eye.position, Layers.WorldMask))
                            combatUntil = Mathf.Max(combatUntil, Time.time + 6);
                    }
                }
                SwitchTo(boss ? HomecomingMusicState.Boss : Time.time < combatUntil ? HomecomingMusicState.Combat : HomecomingMusicState.Explore);
            }
            // Pausing holds the current arrangement and transport, but reduces its loudness.
            // AudioListener.pause remains owned by the existing focus/mute setting.
            float gain = CurrentState == HomecomingMusicState.Menu ? .55f : CurrentState == HomecomingMusicState.Ending ? .50f
                : CurrentState == HomecomingMusicState.Boss ? .43f : CurrentState == HomecomingMusicState.Combat ? .38f : .43f;
            if (game.state == RunState.Paused || game.state == RunState.Dead) gain *= .42f;
            if (game.TrialSessionActive) gain = 0; // Only the isolated trial child is audible.
            float dt = Time.unscaledDeltaTime;
            mixLevel = Mathf.MoveTowards(mixLevel, gain * game.options.musicVolume, dt * .65f);
            float wantedDuck = game.StoryRadioActive || Time.unscaledTime < radioUntil ? .38f : Time.unscaledTime < warningUntil ? .62f : 1;
            duck = Mathf.MoveTowards(duck, wantedDuck, dt * (wantedDuck < duck ? 5 : 1.25f));
            for (int i = 0; i < sources.Length; i++)
            {
                weights[i] = Mathf.MoveTowards(weights[i], i == destination ? 1 : 0, dt / 1.6f);
                sources[i].volume = Mathf.Sqrt(weights[i]) * mixLevel * duck;
                if (i != destination && weights[i] <= 0 && sources[i].isPlaying) sources[i].Stop();
            }
        }

        void SwitchTo(HomecomingMusicState state)
        {
            CurrentState = state;
            string id = state == HomecomingMusicState.Combat || state == HomecomingMusicState.Boss ? "music_combat"
                : state == HomecomingMusicState.Ending ? "music_ending" : "music_explore";
            AudioClip next = tracks[id];
            if (sources[destination].clip == next) return;
            int incoming = 1 - destination;
            if (sources[incoming].clip != next || !sources[incoming].isPlaying)
            {
                var outgoing = sources[destination];
                sources[incoming].Stop(); sources[incoming].clip = next;
                // Only the matched calm/action pair shares phase. Ending starts at its own opening.
                bool paired = id != "music_ending" && outgoing.clip && outgoing.clip != tracks["music_ending"];
                if (paired && Mathf.Abs(outgoing.clip.length - next.length) < .1f)
                    sources[incoming].timeSamples = Mathf.Clamp(outgoing.timeSamples, 0, next.samples - 1);
                else sources[incoming].timeSamples = 0;
                weights[incoming] = 0;
            }
            if (!sources[incoming].isPlaying) sources[incoming].Play();
            destination = incoming;
        }
    }
}
