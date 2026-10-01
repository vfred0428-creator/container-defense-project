using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContainerDefense.Domain;

// Headless playtest sweep over the real simulation and bots: many seeds, every character in every slot
// over the run, default match rules. Logs match length, placements by character, boss route kinds,
// exceptions and stuck states. Build and run with Tools/Run-PlaytestSweep.ps1. Changes no balance values.
public static class PlaytestSweep
{
    private const float Step = 1f / 30;
    private const float MaxSeconds = 40 * 60;
    public static int Main(string[] args)
    {
        int seeds = args.Length > 0 ? int.Parse(args[0]) : 70; string output = args.Length > 1 ? args[1] : "Playtest";
        // Optional third argument: boss health override, for tuning match length without editing MatchRules.
        float bossHealth = args.Length > 2 ? float.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture) : new MatchRules().BossHealth;
        Directory.CreateDirectory(output);
        var rows = new List<string> { "seed,winner_reason,length_s,combat_s,human_character,placements(character:place),route_kinds,exception" };
        var placeSum = new Dictionary<CharacterId,List<int>>(); var wins = new Dictionary<CharacterId,int>(); var lengths = new List<float>();
        var routeKinds = new Dictionary<BossRouteKind,int>(); var reasons = new Dictionary<MatchEndReason,int>(); int stuck = 0, crashes = 0;
        var catalog = CharacterCatalog.Defaults();
        for (int seed = 1; seed <= seeds; seed++) {
            var roster = new CharacterDefinition[6];
            for (int i = 0; i < 6; i++) roster[i] = catalog[(seed + i) % 7];
            string error = ""; MatchSimulation m = null; var kinds = new HashSet<BossRouteKind>();
            try {
                m = new MatchSimulation(new MatchRules { BossHealth = bossHealth },seed,roster);
                var bots = new LocalBotController(m); var me = m.CommandsFor(0); int target = (seed * 5) % 12; float think = 0;
                m.Changed += e => { if (e.Kind == MatchEventKind.RoutePlanned) kinds.Add(m.Boss.RouteKind); };
                for (int i = 0; i < MaxSeconds / Step && !m.Finished; i++) {
                    // The human slot plays like a simple bot: claim a free house, sleep, build and upgrade.
                    var p = m.Players[0];
                    if (p.HouseId < 0 && !p.Eliminated && m.Phase == MatchPhase.Preparation) {
                        if (m.Houses[target].OwnerId >= 0) target = Enumerable.Range(0,12).FirstOrDefault(h => m.Houses[h].OwnerId < 0);
                        me.Navigate(m.Houses[target].Entry,Step); if (me.Claim(target)) me.ToggleSleep();
                    } else if (!p.Eliminated && p.HouseId >= 0 && (think -= Step) <= 0) {
                        think = 1; var h = m.Houses[p.HouseId];
                        if (h.Health < h.MaxHealth * .45f && me.Repair()) { }
                        else if (h.Weapons[0] == null) me.Place(0,WeaponKind.Gatling);
                        else if (h.BedLevel < 2) me.UpgradeHouse(UpgradeKind.Bed);
                        else if (h.Weapons[1] == null) me.Place(1,WeaponKind.Cannon);
                        else if (h.Weapons[0].Level < 3) me.Upgrade(0);
                        else if (h.Weapons[2] == null) me.Place(2,WeaponKind.Rocket);
                        else me.UpgradeHouse(UpgradeKind.Door);
                    }
                    bots.Tick(Step); m.Tick(Step);
                }
            } catch (Exception e) { error = e.GetType().Name + ": " + e.Message.Replace(',',';'); crashes++; }
            if (m == null) { rows.Add(seed + ",crash,,,,,," + error); continue; }
            if (!m.Finished && error == "") { stuck++; error = "stuck: not finished after " + MaxSeconds + " s"; }
            foreach (var k in kinds) Bump(routeKinds,k);
            if (m.Finished) {
                Bump(reasons,m.EndReason); lengths.Add(m.Elapsed);
                foreach (var p in m.Players) {
                    if (!placeSum.ContainsKey(p.Character.Id)) placeSum[p.Character.Id] = new List<int>();
                    placeSum[p.Character.Id].Add(p.Placement);
                    if (p.Placement == 1) Bump(wins,p.Character.Id);
                }
            }
            rows.Add(string.Join(",",seed,m.EndReason,m.Elapsed.ToString("0"),m.CombatSeconds.ToString("0"),m.Players[0].Character.Id,
                string.Join(" ",m.Players.Select(p => p.Character.Id + ":" + p.Placement)),string.Join(" ",kinds),error));
        }
        File.WriteAllLines(Path.Combine(output,"matches.csv"),rows);
        var summary = new List<string> {
            "Playtest sweep: " + seeds + " seeds, default MatchRules with boss health " + bossHealth + ", five LocalBotController bots plus a simple scripted human slot.",
            "Crashes: " + crashes + "   Stuck (not finished within " + MaxSeconds / 60 + " min): " + stuck,
            "Match length (s): min " + (lengths.Count > 0 ? lengths.Min() : 0).ToString("0") + "  median " + Median(lengths).ToString("0") + "  max " + (lengths.Count > 0 ? lengths.Max() : 0).ToString("0") + "  (target about 300 s)",
            "End reasons: " + string.Join(", ",reasons.Select(kv => kv.Key + " " + kv.Value)),
            "Route kinds seen (matches using each): " + string.Join(", ",routeKinds.OrderBy(kv => kv.Key).Select(kv => kv.Key + " " + kv.Value)),
            "By character: matches, average placement (1 = best), first places"
        };
        foreach (CharacterId c in Enum.GetValues(typeof(CharacterId))) {
            List<int> list; if (!placeSum.TryGetValue(c,out list)) continue;
            summary.Add("  " + c + ": " + list.Count + " matches, avg place " + list.Average().ToString("0.00") + ", first " + (wins.ContainsKey(c) ? wins[c] : 0));
        }
        File.WriteAllLines(Path.Combine(output,"summary.txt"),summary);
        foreach (var line in summary) Console.WriteLine(line);
        return crashes + stuck == 0 ? 0 : 1;
    }
    private static void Bump<T>(Dictionary<T,int> d,T k) { int v; d.TryGetValue(k,out v); d[k] = v + 1; }
    private static float Median(List<float> v) { if (v.Count == 0) return 0; var s = v.OrderBy(x => x).ToList(); return s[s.Count / 2]; }
}
