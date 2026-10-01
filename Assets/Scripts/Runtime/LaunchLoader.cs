using System.Collections;
using System.Collections.Generic;
using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    // Launch screen loader. Everything ships with the game (no download), so "preparing" means checking that the
    // bundled files the game needs really load: each file is requested asynchronously and the bar moves only when
    // one arrives. A missing required file stops on an error with RETRY; missing sound or music only warns.
    // Development builds accept --launch-fail <step> to fail that step once, so the error screen can be tested.
    public sealed class LaunchLoader : MonoBehaviour
    {
        public LaunchSequence Sequence { get; private set; }
        public bool Entered { get; private set; }
        public bool Running { get; private set; }
        private GameSession session;
        private readonly Dictionary<string,string[]> files = new Dictionary<string,string[]>();
        private readonly Dictionary<string,System.Type> kinds = new Dictionary<string,System.Type>();
        private string forcedFailure;

        public void Initialize(GameSession game)
        {
            session = game;
            var characters = new List<string>();
            foreach (var c in CharacterCatalog.Defaults()) characters.Add("Art2D/" + CharacterCatalog.Key(c.Id) + "_default");
            Group("Characters",true,typeof(Texture2D),characters.ToArray());
            Group("World",true,typeof(Texture2D),"Art2D/room","Art2D/houses","Art2D/yard","Art2D/topdown-atlas","Art2D/boss",
                G("house_blue"),G("house_pink"),G("house_purple"),G("house_red"),G("house_teal"),G("house_yellow"),G("house_blue_damaged"),
                G("asphalt"),G("ground_tile"),G("dock_strip"),G("edge_strip"),G("plaza"),G("road_cross"),G("road_straight"),G("minion"),G("boss_v2"),G("boss_entry_marker"));
            Group("Interface",true,typeof(Texture2D),G("logo"),G("menu_bg"),G("icon_bed"),G("icon_coin"),G("icon_door"),G("icon_gear"),G("icon_gem"),G("icon_heart"),
                G("icon_house"),G("icon_lock"),G("icon_repair"),G("icon_skull"),G("icon_timer"),G("icon_up"),G("weapon_cannon"),G("weapon_gatling"),G("weapon_rocket"),G("weapon_slow"),
                G("prop_barrel"),G("prop_cone"),G("prop_crate"),G("prop_lamp"),G("prop_pallets"),G("prop_plant"));
            Group("Sounds",false,typeof(AudioClip),"Sfx/build","Sfx/claim","Sfx/coin","Sfx/defeat","Sfx/hit","Sfx/house_destroyed","Sfx/house_hit","Sfx/match_start",
                "Sfx/repair","Sfx/shot_cannon","Sfx/shot_gatling","Sfx/shot_rocket","Sfx/shot_slow","Sfx/tap","Sfx/victory","Sfx/warning");
            Group("Music",false,typeof(AudioClip),"Music/cozy_puzzle_stage_select_bpm100");
            Sequence = new LaunchSequence().Add("Profile",true,1);
            foreach (var name in new[] { "Characters","World","Interface","Sounds","Music" }) Sequence.Add(name,name != "Sounds" && name != "Music",files[name].Length);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var args = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(args,"--launch-fail");
            if (i >= 0 && i + 1 < args.Length) forcedFailure = args[i + 1];
            if (forcedFailure != null) Debug.Log("[Launch] forcing a failure in " + forcedFailure);
#endif
            StartCoroutine(Run());
        }
        private static string G(string id) { return "Art2D/Generated/" + id; }
        private void Group(string name,bool required,System.Type kind,params string[] paths) { files[name] = paths; kinds[name] = kind; }

        public void Retry() { if (Running || !Sequence.Finished || Entered) return; Sequence.Retry(); StartCoroutine(Run()); }
        // Continue past optional warnings (no sound or music).
        public void Enter() { if (Sequence.CanEnter) Entered = true; }

        private IEnumerator Run()
        {
            Running = true;
            foreach (var step in Sequence.Steps) {
                if (step.State != LaunchStepState.Pending) continue;
                Sequence.Begin(step); yield return null;
                if (step.Name == "Profile") {
                    if (session.Account != null) Sequence.Complete(step); else Sequence.Fail(step,"Your profile could not be opened.");
                    continue;
                }
                bool forced = step.Name == forcedFailure && Sequence.Attempts == 0;
                var paths = files[step.Name]; int loaded = 0; string missing = null;
                foreach (var path in paths) {
                    var request = Resources.LoadAsync(path,kinds[step.Name]);
                    while (!request.isDone) yield return null;
                    if (request.asset == null || forced && loaded == paths.Length / 2) { missing = path.Substring(path.LastIndexOf('/') + 1); break; }
                    Sequence.Report(step,++loaded); yield return null;
                }
                if (missing == null) Sequence.Complete(step);
                else Sequence.Fail(step,"Missing " + missing + " (" + loaded + " of " + paths.Length + " files loaded).");
                Debug.Log("[Launch] " + step.Name + ": " + step.State + (forced ? " (forced failure)" : "") + " attempt " + Sequence.Attempts);
            }
            Running = false;
            // Straight in when everything loaded; warnings and errors wait for the player.
            if (Sequence.CanEnter && !Sequence.HasWarnings) { yield return new WaitForSecondsRealtime(.35f); Entered = true; }
        }
    }
}
