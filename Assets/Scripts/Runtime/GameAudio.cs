using System.Collections.Generic;
using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    // Small sound player: CC0 clips from Assets/Audio (see LICENSE.txt), pitch variation and a per-sound
    // cooldown so rapid events (gatling fire) never flood the mixer. Volumes come from the saved AudioPrefs.
    public sealed class GameAudio : MonoBehaviour
    {
        private static GameAudio instance;
        private readonly Dictionary<string,AudioClip> clips = new Dictionary<string,AudioClip>();
        private readonly Dictionary<string,float> lastPlayed = new Dictionary<string,float>();
        private AudioSource[] voices;
        private AudioSource jingle, music;
        // Background loop ("Cozy Puzzle Stage Select", CC0): quiet under the Music slider, ducked for warnings and jingles.
        private const float MusicGain = .4f, DuckGain = .55f, DuckSeconds = 3.5f;
        private float duckUntil;
        private int nextVoice;
        private AudioPrefs prefs = new AudioPrefs();
        private static readonly Dictionary<string,float> Cooldown = new Dictionary<string,float> {
            { "shot_gatling",.18f },{ "shot_cannon",.12f },{ "shot_slow",.2f },{ "shot_rocket",.15f },{ "hit",.08f },{ "house_hit",.1f },{ "coin",.06f },{ "tap",.04f },{ "warning",1.2f }
        };
        private static readonly Dictionary<string,float> Loudness = new Dictionary<string,float> {
            { "shot_gatling",.35f },{ "shot_cannon",.6f },{ "shot_slow",.45f },{ "shot_rocket",.55f },{ "hit",.4f },{ "house_hit",.7f },{ "house_destroyed",.9f },{ "warning",.55f },{ "tap",.5f },{ "coin",.5f }
        };
        public static void Ensure(GameObject host,AudioPrefs prefs)
        {
            if (instance == null) instance = host.AddComponent<GameAudio>();
            instance.prefs = AudioPrefs.Normalize(prefs);
        }
        public static void Apply(AudioPrefs prefs) { if (instance != null) { instance.prefs = AudioPrefs.Normalize(prefs); if (instance.jingle != null) instance.jingle.volume = instance.MusicLevel; } }
        private float SfxLevel { get { return prefs.Muted ? 0 : prefs.SfxVolume; } }
        private float MusicLevel { get { return prefs.Muted ? 0 : prefs.MusicVolume; } }
        private void Awake()
        {
            voices = new AudioSource[10];
            for (int i = 0; i < voices.Length; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false; voices[i].spatialBlend = 0; }
            jingle = gameObject.AddComponent<AudioSource>(); jingle.playOnAwake = false;
            music = gameObject.AddComponent<AudioSource>(); music.playOnAwake = false; music.loop = true; music.volume = 0;
            music.clip = Resources.Load<AudioClip>("Music/cozy_puzzle_stage_select_bpm100"); if (music.clip != null) music.Play();
            foreach (var c in Resources.LoadAll<AudioClip>("Sfx")) clips[c.name] = c;
        }
        private void Update()
        {
            if (music == null || music.clip == null) return;
            float target = MusicLevel * MusicGain * (Time.unscaledTime < duckUntil ? DuckGain : 1) * (jingle.isPlaying ? .5f : 1);
            music.volume = Mathf.MoveTowards(music.volume,target,Time.unscaledDeltaTime * .6f);
        }
        public static void Duck() { if (instance != null) instance.duckUntil = Time.unscaledTime + DuckSeconds; }
        // Plays a one-shot unless it played too recently; volume is scaled per sound and by the SFX setting.
        public static void Play(string id,float volume = 1)
        {
            var a = instance; AudioClip clip;
            if (a == null || a.SfxLevel <= 0 || !a.clips.TryGetValue(id,out clip)) return;
            float cd, last; if (Cooldown.TryGetValue(id,out cd) && a.lastPlayed.TryGetValue(id,out last) && Time.unscaledTime - last < cd) return;
            a.lastPlayed[id] = Time.unscaledTime;
            var v = a.voices[a.nextVoice++ % a.voices.Length];
            float loud; if (!Loudness.TryGetValue(id,out loud)) loud = .7f;
            v.pitch = Random.Range(.93f,1.07f); v.PlayOneShot(clip,loud * volume * a.SfxLevel);
        }
        public static void Jingle(string id)
        {
            var a = instance; AudioClip clip;
            if (a == null || !a.clips.TryGetValue(id,out clip)) return;
            a.jingle.Stop(); a.jingle.clip = clip; a.jingle.volume = a.MusicLevel; if (a.MusicLevel > 0) a.jingle.Play();
        }
        // Maps match events to sounds; only events near the local player are loud.
        public static void OnMatchEvent(MatchSimulation m,MatchEvent e)
        {
            switch (e.Kind) {
                case MatchEventKind.Shot:
                    if (e.HouseId < 0) break;
                    var w = m.Houses[e.HouseId].Weapons[Mathf.Clamp((int)e.Amount,0,2)]; if (w == null) break;
                    float near = m.Houses[e.HouseId].OwnerId == 0 ? 1 : .45f;
                    Play(w.Kind == WeaponKind.Gatling ? "shot_gatling" : w.Kind == WeaponKind.Cannon ? "shot_cannon" : w.Kind == WeaponKind.Slow ? "shot_slow" : "shot_rocket",near);
                    Play("hit",near * .8f); break;
                case MatchEventKind.DoorHit: Play("house_hit",e.PlayerId == 0 ? 1 : .5f); break;
                case MatchEventKind.Eliminated: if (e.HouseId >= 0 && m.Phase == MatchPhase.Combat) Play("house_destroyed",e.PlayerId == 0 ? 1 : .6f); break;
                case MatchEventKind.RoutePlanned: if (e.PlayerId == 0) { Play("warning"); Duck(); } break;
                case MatchEventKind.Claimed: if (e.PlayerId == 0) Play("claim"); break;
                case MatchEventKind.Placed: case MatchEventKind.UpgradeStarted: if (e.PlayerId == 0) Play("build"); break;
                case MatchEventKind.Repaired: if (e.PlayerId == 0) Play("repair"); break;
                case MatchEventKind.Sold: if (e.PlayerId == 0) Play("coin"); break;
                case MatchEventKind.CombatStarted: Jingle("match_start"); break;
                case MatchEventKind.Finished: Jingle(m.Phase == MatchPhase.Victory && !m.Players[0].Eliminated ? "victory" : "defeat"); break;
            }
        }
    }
}
