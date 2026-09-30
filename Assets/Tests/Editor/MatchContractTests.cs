using System;
using System.Collections.Generic;
using System.Linq;
using ContainerDefense.Domain;

// Contract checks for the 12-house milestone: seeded route planning, boss timing, commands,
// scouting, elimination, match end and account regressions.
public static class MatchContractTests
{
    private const float Step = 1f / 30;
    private static int passed;
    public static string Run()
    {
        passed = 0;
        var map = MapDefinition.Default();
        Check("Planner is pure: same seed and inputs give the same plan without mutating inputs", () => {
            var input = new List<RouteCandidate>(); for (int i = 0; i < 12; i++) input.Add(new RouteCandidate(i,map.HouseSpawns[i].Entry,i % 3));
            var copy = new List<RouteCandidate>(input);
            var a = BossRoutePlanner.Plan(map,input,4,1,new Random(99)); var b = BossRoutePlanner.Plan(map,input,4,1,new Random(99));
            True(a.RouteId == b.RouteId && a.Houses.SequenceEqual(b.Houses)); True(input.SequenceEqual(copy));
            True(BossRoutePlanner.Plan(map,new List<RouteCandidate>(),-1,1,new Random(1)) == null);
        });
        Check("Planner properties hold over 5000 seeded scenarios", () => {
            var rng = new Random(20260930);
            for (int n = 0; n < 5000; n++) {
                var available = new List<RouteCandidate>();
                for (int h = 0; h < 12; h++) if (rng.Next(3) > 0) available.Add(new RouteCandidate(h,map.HouseSpawns[h].Entry,rng.Next(4)));
                int last = rng.Next(-1,12), wave = rng.Next(1,6);
                var plan = BossRoutePlanner.Plan(map,available,last,wave,new Random(n));
                if (available.Count == 0) { True(plan == null); continue; }
                var ids = new HashSet<int>(available.Select(c => c.HouseId));
                True(plan.Houses.Length >= 1 && plan.Houses.Length <= 3 && plan.Houses.Distinct().Count() == plan.Houses.Length);
                foreach (int h in plan.Houses) True(ids.Contains(h));
                True(plan.Telegraph >= 4);
                if (available.Count > 1) True(Array.IndexOf(plan.Houses,last) < 0);
                if (available.Count <= 2) True(plan.Kind == BossRouteKind.Straight);
                int pool = available.Count(c => available.Count == 1 || c.HouseId != last);
                True(plan.Houses.Length == Math.Min(3,pool));
                // Least visited first: nothing left out has fewer visits than something chosen.
                int maxChosen = plan.Houses.Max(h => available.First(c => c.HouseId == h).Visits);
                foreach (var c in available) if (Array.IndexOf(plan.Houses,c.HouseId) < 0 && (available.Count == 1 || c.HouseId != last)) True(c.Visits >= maxChosen);
            }
        });
        Check("Map load validates connectivity, route edges, house adjacency and telegraph floor", () => {
            var bad = MapDefinition.Default(); bad.Nodes[19].X = 30; bad.Nodes[19].Z = 30; Reject(bad);
            bad = MapDefinition.Default(); bad.Routes[0].NodeSequence = new[] { 0,6 }; Reject(bad);
            bad = MapDefinition.Default(); bad.HouseSpawns[4].X = 200; Reject(bad);
            bad = MapDefinition.Default(); bad.Routes[1].TelegraphTime = 3.9f; Reject(bad);
            foreach (int n in Enumerable.Range(0,20)) True(map.Neighbors(n).Length >= 2 && map.Neighbors(n).Length <= 4);
        });
        Check("Road paths are deterministic, orthogonal and shortest", () => {
            for (int a = 0; a < 20; a++) for (int b = 0; b < 20; b++) {
                var from = map.Nodes[a].Position; var to = map.Nodes[b].Position;
                var p1 = map.RoadPath(from,to); var p2 = map.Snapshot().RoadPath(from,to);
                True(p1.Length == p2.Length); for (int i = 0; i < p1.Length; i++) Near(0,p1[i].Distance(p2[i]));
                for (int i = 1; i < p1.Length; i++) True(Math.Abs(p1[i].X - p1[i - 1].X) < .001f || Math.Abs(p1[i].Z - p1[i - 1].Z) < .001f);
                True(p1.Length - 2 == Math.Abs(a % 5 - b % 5) + Math.Abs(a / 5 - b / 5));
            }
        });
        Check("Boss follows the road polyline without skipping nodes at the largest step", () => {
            var m = new MatchSimulation(Rules(),5); ClaimAll(m,new[] { 0,5,11 });
            int checkedSteps = 0; var start = m.Boss.Position;
            m.Changed += e => { if (e.Kind == MatchEventKind.RoutePlanned) start = m.Boss.Position; };
            for (int i = 0; i < 4000; i++) {
                m.Tick(.1f);
                if (m.Boss.Phase != BossPhase.Travelling) continue;
                var path = new[] { start }.Concat(m.Boss.RoutePath).ToArray(); float best = float.MaxValue;
                for (int s = 1; s < path.Length; s++) best = Math.Min(best,SegmentDistance(m.Boss.Position,path[s - 1],path[s]));
                True(best < .01f); checkedSteps++;
            }
            True(checkedSteps > 50);
        });
        Check("Simulated visits respect telegraph, three hits, protection, rotation and fairness", () => {
            for (int seed = 1; seed <= 12; seed++) {
                var r = Rules(); r.BossDamage = .01f; var m = new MatchSimulation(r,seed);
                var homes = new[] { 0,2,4,6,9,11 }; ClaimAll(m,homes);
                var log = new Log(m); float end = 0;
                for (int i = 0; i < 30 * 900; i++) m.Tick(Step);
                end = m.Elapsed; log.Verify(m,6,end);
            }
        });
        Check("Lethal matches never target empty, destroyed or vacated houses", () => {
            for (int seed = 1; seed <= 12; seed++) {
                var r = Rules(); r.BossDamage = 90; var m = new MatchSimulation(r,seed); ClaimAll(m,new[] { 1,3,5,7,8,10 });
                var log = new Log(m);
                for (int i = 0; i < 30 * 900 && !m.Finished; i++) m.Tick(Step);
                True(m.Phase == MatchPhase.Victory && m.EndReason == MatchEndReason.LastStanding && m.LivingHouses() == 1); log.Verify(m,6,m.Elapsed);
                foreach (var h in m.Houses) if (h.OwnerId < 0) True(h.AttacksReceived == 0);
            }
        });
        Check("Two living houses fall back to a Straight plan", () => {
            var m = new MatchSimulation(Rules(),3); ClaimAll(m,new[] { 0,11 }); int plans = 0;
            m.Changed += e => { if (e.Kind == MatchEventKind.RoutePlanned) { True(m.Boss.RouteKind == BossRouteKind.Straight); plans++; } };
            for (int i = 0; i < 30 * 200; i++) m.Tick(Step);
            True(plans > 3);
        });
        Check("A lone survivor waits out protection; the boss neither attacks nor ends the match", () => {
            var r = Rules(); r.BossDamage = .01f; var m = new MatchSimulation(r,8); ClaimAll(m,new[] { 6 });
            float lastHit = -1; int hits = 0;
            m.Changed += e => { if (e.Kind == MatchEventKind.DoorHit) { if (hits % 3 == 0 && lastHit >= 0) True(m.Elapsed - lastHit >= 18 + 4 - Step); lastHit = m.Elapsed; hits++; } };
            bool waited = false;
            for (int i = 0; i < 30 * 300; i++) { m.Tick(Step); if (m.Boss.Phase == BossPhase.Waiting) { waited = true; True(m.Boss.TargetHouseId < 0); } }
            True(waited && hits >= 6 && !m.Finished && m.Phase == MatchPhase.Combat);
        });
        Check("A target vacated during telegraph or travel is skipped without a visit or protection", () => {
            foreach (var phase in new[] { BossPhase.Telegraphing,BossPhase.Travelling }) {
                var m = new MatchSimulation(Rules(),11); ClaimAll(m,new[] { 0,4,11 });
                while (m.Boss.Phase != phase) m.Tick(Step);
                int target = m.Boss.TargetHouseId, owner = m.Houses[target].OwnerId; float protectedUntil = m.Houses[target].ProtectedUntil;
                True(m.CommandsFor(owner).Forfeit());
                int retarget = -1; float planned = 0;
                m.Changed += e => { if (e.Kind == MatchEventKind.RoutePlanned && retarget < 0) { retarget = e.HouseId; planned = m.Elapsed; } if (e.Kind == MatchEventKind.DoorHit) True(e.HouseId != target); };
                m.Tick(Step);
                True(retarget >= 0 && retarget != target && m.Boss.Phase == BossPhase.Telegraphing && m.Boss.TelegraphRemaining > 4 - 2 * Step);
                True(m.Houses[target].VisitsReceived == 0 && m.Houses[target].ProtectedUntil == protectedUntil && m.Houses[target].AttacksReceived == 0);
                for (int i = 0; i < 30 * 120; i++) m.Tick(Step);
                True(m.Houses[target].AttacksReceived == 0);
            }
        });
        Check("A house vacated mid-visit ends the visit and is never hit again", () => {
            var m = new MatchSimulation(Rules(),2); ClaimAll(m,new[] { 3,8,0 });
            int target = -1; m.Changed += e => { if (e.Kind == MatchEventKind.DoorHit && target < 0) target = e.HouseId; };
            while (target < 0) m.Tick(Step);
            int hits = m.Houses[target].AttacksReceived; True(m.CommandsFor(m.Houses[target].OwnerId).Forfeit());
            for (int i = 0; i < 30 * 120; i++) m.Tick(Step);
            True(m.Houses[target].AttacksReceived == hits && m.Houses[target].VisitsReceived == 1 && !m.Houses[target].Occupied);
        });
        Check("Boss timeline is identical at 30, 60 and 144 steps per second", () => {
            var runs = new[] { 30,60,144 }.Select(fps => Timeline(fps)).ToArray();
            for (int k = 1; k < runs.Length; k++) {
                True(runs[k].Events.Count == runs[0].Events.Count);
                for (int i = 0; i < runs[0].Events.Count; i++) {
                    True(runs[k].Events[i].Kind == runs[0].Events[i].Kind && runs[k].Events[i].House == runs[0].Events[i].House);
                    True(Math.Abs(runs[k].Events[i].Time - runs[0].Events[i].Time) <= 1f / 30 + .002f);
                }
                for (int h = 0; h < 12; h++) Near(runs[0].Health[h],runs[k].Health[h]);
            }
            True(runs[0].Events.Count(e => e.Kind == MatchEventKind.DoorHit) > 20);
        });
        Check("Command matrix rejects every invalid issuer, slot, state and wallet without mutation", () => {
            var r = Rules(); r.StartingGold = 200; r.UpgradeSeconds = 2; var m = new MatchSimulation(r,1); ClaimAll(m,new[] { 0,1 });
            var mine = m.CommandsFor(0); var theirs = m.CommandsFor(1); var idle = m.CommandsFor(2);
            Func<string> state = () => Fingerprint(m);
            Action<Func<bool>> reject = cmd => { string before = state(); False(cmd()); True(before == state()); };
            // Wrong issuer or no house.
            reject(() => m.TryPlaceWeapon(1,0,0,WeaponKind.Gatling)); reject(() => idle.Place(0,WeaponKind.Gatling)); reject(() => idle.Repair());
            reject(() => m.TryPlaceWeapon(-1,0,0,WeaponKind.Gatling)); reject(() => m.TryPlaceWeapon(6,0,0,WeaponKind.Gatling)); reject(() => m.TryPlaceWeapon(0,12,0,WeaponKind.Gatling));
            // Slots out of range and unknown weapon.
            foreach (int slot in new[] { -1,3,int.MaxValue,int.MinValue }) { reject(() => mine.Place(slot,WeaponKind.Cannon)); reject(() => mine.Upgrade(slot)); reject(() => mine.Sell(slot)); reject(() => mine.MoveWeapon(0,slot)); reject(() => mine.MoveWeapon(slot,0)); }
            reject(() => mine.Place(0,(WeaponKind)(-1)));
            // Empty-slot state.
            reject(() => mine.Upgrade(0)); reject(() => mine.Sell(0)); reject(() => mine.MoveWeapon(0,1)); reject(() => mine.Repair());
            // Double place into one slot: one weapon, one charge.
            double gold = m.Players[0].Gold; True(mine.Place(0,WeaponKind.Gatling)); reject(() => mine.Place(0,WeaponKind.Cannon)); Near(gold - 35,m.Players[0].Gold);
            // Building state blocks upgrade, sell and move.
            reject(() => mine.Upgrade(0)); reject(() => mine.Sell(0)); reject(() => mine.MoveWeapon(0,1));
            for (int i = 0; i < 90; i++) m.Tick(Step);
            // Occupied destination, same slot and cross-house move.
            True(mine.Place(1,WeaponKind.Slow)); for (int i = 0; i < 90; i++) m.Tick(Step);
            reject(() => mine.MoveWeapon(0,1)); reject(() => mine.MoveWeapon(0,0)); reject(() => m.TryMoveWeapon(0,1,0,2)); reject(() => m.TryMoveWeapon(1,0,0,2));
            // Insufficient personal wallet; the other player's gold never pays.
            var poor = Rules(); poor.StartingGold = 10; var p = new MatchSimulation(poor,1); ClaimAll(p,new[] { 0,1 });
            string pre = Fingerprint(p); False(p.CommandsFor(0).Place(0,WeaponKind.Gatling)); True(pre == Fingerprint(p));
            // Sell refund is floor(half of paid), never above cost; Mochi discounts lower it further.
            double beforeSell = m.Players[0].Gold; True(mine.Sell(1)); True(m.Players[0].Gold - beforeSell <= 45 && m.Players[0].Gold - beforeSell == 22);
            reject(() => mine.Sell(1));
        });
        Check("Move keeps cooldown and level; repair at full health is rejected", () => {
            var m = new MatchSimulation(Rules(),1); ClaimAll(m,new[] { 0 }); var mine = m.CommandsFor(0);
            True(mine.Place(0,WeaponKind.Cannon)); True(mine.Upgrade(0));
            var w = m.Houses[0].Weapons[0]; for (int i = 0; i < 4000 && w.ShotCooldown <= 0; i++) m.Tick(Step);
            float cd = w.ShotCooldown; True(cd > 0 && mine.MoveWeapon(0,2)); Near(cd,m.Houses[0].Weapons[2].ShotCooldown); True(m.Houses[0].Weapons[2].Level == 2);
            if (m.Houses[0].Health >= m.Houses[0].MaxHealth) False(mine.Repair());
        });
        Check("Wallets saturate instead of overflowing", () => {
            var r = Rules(); r.StartingGold = 5e12f; var m = new MatchSimulation(r,1); ClaimAll(m,new[] { 0 });
            Near(MatchSimulation.MaxWallet,m.Players[0].Gold); True(m.CommandsFor(0).ToggleSleep());
            for (int i = 0; i < 300; i++) m.Tick(Step);
            True(m.Players[0].Gold <= MatchSimulation.MaxWallet && !double.IsInfinity(m.Players[0].Gold));
        });
        Check("Scouting is read-only, hides other players' gold and holds no live references", () => {
            var m = new MatchSimulation(Rules(),1); ClaimAll(m,new[] { 0,1 }); m.CommandsFor(0).Place(0,WeaponKind.Gatling);
            string before = Fingerprint(m); var view = m.Scout(1); True(before == Fingerprint(m));
            True(view[0].ViewerGold == -1 && view[0].Wealth != WealthBand.Unknown && !view[0].IsViewer);
            True(view[1].IsViewer && view[1].ViewerGold == m.Players[1].Gold);
            True(view[0].Weapons[0].Present && view[0].Weapons[0].Kind == WeaponKind.Gatling && !view[2].Claimed);
            view[0].Weapons[0] = default(WeaponScout); True(m.Houses[0].Weapons[0] != null && m.Scout(1)[0].Weapons[0].Present);
            var allowed = new[] { typeof(int),typeof(float),typeof(double),typeof(bool),typeof(string),typeof(WealthBand),typeof(WeaponKind),typeof(UpgradeKind),typeof(WeaponScout[]) };
            foreach (var prop in typeof(HouseScout).GetProperties()) True(allowed.Contains(prop.PropertyType));
            foreach (var field in typeof(WeaponScout).GetFields()) True(allowed.Contains(field.FieldType));
            True(typeof(HouseScout).GetProperties().All(pr => pr.Name.IndexOf("Gold",StringComparison.Ordinal) < 0 || pr.Name == "ViewerGold"));
        });
        Check("Bots act only through their own bound commands", () => {
            var m = new MatchSimulation(new MatchRules { UpgradeSeconds = 0 },3); var bots = new LocalBotController(m); int placed = 0;
            m.Changed += e => { if (e.Kind == MatchEventKind.Placed || e.Kind == MatchEventKind.Upgraded || e.Kind == MatchEventKind.Repaired) { True(m.Houses[e.HouseId].OwnerId == e.PlayerId); if (e.Kind == MatchEventKind.Placed) placed++; } };
            for (int i = 0; i < 30 * 90; i++) { bots.Tick(Step); m.Tick(Step); }
            True(placed >= 5); True(m.Houses.Count(h => h.OwnerId >= 1) == 5);
            True(m.Houses.Where(h => h.OwnerId < 0).All(h => h.Weapons.All(w => w == null)));
        });
        Check("Eliminated players lose commands and firing; their house is never attacked", () => {
            var m = new MatchSimulation(Rules(),6); ClaimAll(m,new[] { 0,4,11 }); var quitter = m.CommandsFor(1);
            True(quitter.Place(0,WeaponKind.Rocket)); True(quitter.Forfeit()); int home = m.Players[1].HouseId;
            m.Changed += e => { if (e.HouseId == home && (e.Kind == MatchEventKind.Shot || e.Kind == MatchEventKind.DoorHit || e.Kind == MatchEventKind.RoutePlanned)) throw new Exception("Vacated house acted or was targeted."); };
            string before = Fingerprint(m);
            False(quitter.Place(1,WeaponKind.Gatling)); False(quitter.Upgrade(0)); False(quitter.Sell(0)); False(quitter.MoveWeapon(0,1)); False(quitter.Repair()); False(quitter.ToggleSleep()); False(quitter.Forfeit());
            True(before == Fingerprint(m)); True(!m.Houses[home].Occupied && m.LivingHouses() == 2);
            for (int i = 0; i < 30 * 300; i++) m.Tick(Step);
        });
        Check("Match end cancels pending attacks, freezes timers, rejects commands and emits once", () => {
            for (int seed = 1; seed <= 8; seed++) {
                var r = Rules(); r.BossHealth = 400; var m = new MatchSimulation(r,seed); ClaimAll(m,new[] { 0,4,8 });
                foreach (int p in new[] { 0,1,2 }) { m.CommandsFor(p).Place(0,WeaponKind.Rocket); m.CommandsFor(p).Place(1,WeaponKind.Cannon); }
                int finished = 0; bool after = false;
                m.Changed += e => { if (after) throw new Exception("Event after finish: " + e.Kind); if (e.Kind == MatchEventKind.Finished) { finished++; after = true; } };
                for (int i = 0; i < 30 * 400 && !m.Finished; i++) m.Tick(Step);
                True(m.Phase == MatchPhase.Victory && finished == 1 && m.Boss.TargetHouseId < 0 && m.Boss.RouteHouses.Length == 0 && m.Boss.Phase == BossPhase.Dead);
                float t = m.Elapsed; double g = m.Players[0].Gold; for (int i = 0; i < 90; i++) m.Tick(Step);
                True(m.Elapsed == t && m.Players[0].Gold == g);
                False(m.CommandsFor(0).Place(2,WeaponKind.Gatling)); False(m.CommandsFor(0).Repair()); False(m.CommandsFor(0).Forfeit());
            }
        });
        Check("Same-step endings resolve deterministically", () => {
            for (int seed = 1; seed <= 6; seed++) {
                var a = LethalRace(seed); var b = LethalRace(seed);
                True(a.Phase == b.Phase && Math.Abs(a.Elapsed - b.Elapsed) < 1e-6 && Math.Abs(a.Boss.Health - b.Boss.Health) < 1e-4);
            }
        });
        Check("A full match leaves passives, skins, gifts and casual rank unchanged; XP only grows once", () => {
            var catalog = CollectionCatalog.CreateDefault();
            var account = new AccountProgression(new AccountData { TotalXp = 777 },new CharacterCatalog(CharacterCatalog.Defaults()),new ProgressionRules(),catalog);
            account.Inventory.ClaimStarter(); var store = new Store();
            var gift = new LocalGiftService(account,catalog,store,account.Social.Profile.PlayerId,() => 1790683200L);
            True(gift.Send(new GiftRequest { TransactionId = "5e0c1a2b3c4d4e5f8a9b0c1d2e3f4a5b",ReceiverId = "local_a",StickerId = "bunny",Quantity = 3 }).Success);
            var before = account.Snapshot(); string passives = Passives(); long xp = account.TotalXp;
            var m = new MatchSimulation(new MatchRules { UpgradeSeconds = 0 },42); long ticket = account.BeginMatch(m,false);
            var bots = new LocalBotController(m); ClaimAll(m,new[] { 5 }); m.CommandsFor(0).ToggleSleep();
            for (int i = 0; i < 30 * 1200 && !m.Finished; i++) { bots.Tick(Step); if (i % 30 == 0) { m.CommandsFor(0).Place(i / 30 % 3,WeaponKind.Gatling); m.CommandsFor(0).Upgrade(0); } m.Tick(Step); }
            True(m.Finished); MatchReward reward, again; True(account.TryAward(m,ticket,out reward)); False(account.TryAward(m,ticket,out again));
            var after = account.Snapshot();
            True(Passives() == passives && passives == "0.08,0.15,0.1,0.12,0.12,0.08,0.1");
            True(reward.Xp > 0 && after.TotalXp == xp + reward.Xp);
            True(string.Join(",",after.Collection.OwnedSkins) == string.Join(",",before.Collection.OwnedSkins));
            True(string.Join(",",after.Collection.EquippedSkins.Select(e => e.CharacterId + ":" + e.SkinId)) == string.Join(",",before.Collection.EquippedSkins.Select(e => e.CharacterId + ":" + e.SkinId)));
            True(string.Join(",",after.Collection.Stickers.Select(s => s.StickerId + s.QuantityOwned)) == string.Join(",",before.Collection.Stickers.Select(s => s.StickerId + s.QuantityOwned)));
            True(after.Social.History.Length == before.Social.History.Length && after.Social.History[0].TransactionId == "5e0c1a2b3c4d4e5f8a9b0c1d2e3f4a5b");
            True(after.Social.Profile.Popularity == before.Social.Profile.Popularity && after.Social.Recipients.Sum(x => x.Popularity) == before.Social.Recipients.Sum(x => x.Popularity));
            True(after.Rank.CurrentRank == before.Rank.CurrentRank && after.Rank.Stars == before.Rank.Stars && after.Rank.MatchesPlayed == before.Rank.MatchesPlayed);
            True(after.Collection.OwnedSkins.All(s => s.EndsWith("_default")));
        });
        Check("Last one wins: the match ends on the fifth elimination and the survivor places first", () => {
            for (int seed = 1; seed <= 6; seed++) {
                var r = Rules(); r.BossDamage = 90; var m = new MatchSimulation(r,seed); ClaimAll(m,new[] { 0,2,4,6,9,11 });
                int eliminated = 0, eliminatedAtFinish = -1, finishes = 0; bool after = false;
                m.Changed += e => {
                    if (after) throw new Exception("Event after finish: " + e.Kind);
                    if (e.Kind == MatchEventKind.Eliminated) { eliminated++; True(!m.Finished); }
                    if (e.Kind == MatchEventKind.Finished) { finishes++; eliminatedAtFinish = eliminated; after = true; }
                };
                for (int i = 0; i < 30 * 1200 && !m.Finished; i++) m.Tick(Step);
                True(finishes == 1 && eliminatedAtFinish == 5 && m.Phase == MatchPhase.Victory && m.EndReason == MatchEndReason.LastStanding);
                var winner = m.Players[m.WinnerId]; True(!winner.Eliminated && winner.Placement == 1 && m.Houses[winner.HouseId].Occupied);
                var byPlace = m.Players.OrderBy(p => p.Placement).ToArray();
                for (int i = 0; i < 6; i++) True(byPlace[i].Placement == i + 1);
                for (int i = 2; i < 6; i++) True(byPlace[i - 1].EliminatedAt >= byPlace[i].EliminatedAt);
                True(m.Boss.TargetHouseId < 0 && m.Boss.RouteHouses.Length == 0 && m.Boss.Phase == BossPhase.Waiting && m.Boss.Health > 0);
                float t = m.Elapsed; for (int i = 0; i < 60; i++) m.Tick(Step); True(m.Elapsed == t);
                False(m.CommandsFor(winner.Id).Place(2,WeaponKind.Gatling)); False(m.CommandsFor(winner.Id).Forfeit()); False(m.TryToggleSleep(winner.Id));
            }
        });
        Check("A forfeit that leaves one house standing ends the match at once", () => {
            var m = new MatchSimulation(Rules(),4); ClaimAll(m,new[] { 1,7,10 });
            True(m.CommandsFor(2).Forfeit()); True(!m.Finished);
            while (m.Phase == MatchPhase.Preparation) m.Tick(Step);
            True(!m.Finished && m.LivingHouses() == 2);
            True(m.CommandsFor(0).Forfeit());
            True(m.Finished && m.EndReason == MatchEndReason.LastStanding && m.WinnerId == 1 && m.Players[1].Placement == 1 && m.Players[0].Placement == 2 && m.Players[2].Placement == 6);
            False(m.CommandsFor(1).Repair());
        });
        Check("A solo house still fights the boss; last one wins needs two contestants", () => {
            var r = Rules(); r.BossDamage = 500; var m = new MatchSimulation(r,4); ClaimAll(m,new[] { 5 });
            for (int i = 0; i < 30 * 30 && m.Phase == MatchPhase.Preparation; i++) m.Tick(Step);
            m.Tick(Step); True(!m.Finished);
            for (int i = 0; i < 30 * 300 && !m.Finished; i++) m.Tick(Step);
            True(m.Phase == MatchPhase.Defeat && m.EndReason == MatchEndReason.AllFallen && m.WinnerId == -1 && m.Players[0].Placement == 1);
        });
        Check("Simultaneous eliminations resolve by house number, then player id", () => {
            var m = new MatchSimulation(Rules(),9); ClaimAll(m,new[] { 8,3 });
            for (int i = 0; i < 30 * 31 && !m.Finished; i++) m.Tick(Step);
            // Players 2..5 never claimed and were all eliminated at the same instant.
            for (int p = 2; p < 6; p++) Near(30,m.Players[p].EliminatedAt);
            True(m.CommandsFor(1).Forfeit());
            True(m.Players[0].Placement == 1 && m.Players[1].Placement == 2);
            for (int p = 2; p < 6; p++) True(m.Players[p].Placement == p + 1);
        });
        Check("Boss death in the step the second-to-last house would fall is a boss victory", () => {
            bool found = false;
            for (int seed = 1; seed <= 400 && !found; seed++) {
                float shotDamageThroughFinal; int finalStep;
                if (!FinalStepHasShot(seed,float.MaxValue,out finalStep,out shotDamageThroughFinal)) continue;
                found = true;
                var m = TwoHouseRace(seed,shotDamageThroughFinal - .01f);
                for (int i = 0; i <= finalStep && !m.Finished; i++) m.Tick(RaceStep);
                True(m.Finished && m.EndReason == MatchEndReason.BossDefeated && m.Phase == MatchPhase.Victory && m.LivingHouses() == 2 && m.WinnerId == -1);
            }
            True(found);
        });
        Check("A last-one-wins result applies to the account exactly once", () => {
            var account = new AccountProgression(new AccountData { TotalXp = 500 },new CharacterCatalog(CharacterCatalog.Defaults()),new ProgressionRules(),CollectionCatalog.CreateDefault());
            var m = new MatchSimulation(Rules(),12); long ticket = account.BeginMatch(m,true); ClaimAll(m,new[] { 2,9 });
            while (m.Phase == MatchPhase.Preparation) m.Tick(Step);
            True(m.CommandsFor(1).Forfeit() && m.Finished && m.WinnerId == 0);
            MatchReward reward, again; True(account.TryAward(m,ticket,out reward)); False(account.TryAward(m,ticket,out again));
            True(account.Rank.Wins == 1 && account.Rank.MatchesPlayed == 1 && account.Social.Profile.Wins == 1 && account.TotalXp == 500 + reward.Xp);
        });
        return passed + " match contract scenarios passed.";
    }

    private sealed class Store : ISaveService
    {
        public AccountData Data;
        public AccountData Load() { return Data; }
        public void Save(AccountData data) { Data = data; }
        public string Status { get { return "Saved"; } }
        public bool CanWrite { get { return true; } }
    }
    private struct Entry { public MatchEventKind Kind; public int House; public float Time; }
    private sealed class Trace { public List<Entry> Events = new List<Entry>(); public float[] Health = new float[12]; }
    private static Trace Timeline(int fps)
    {
        var r = Rules(); r.BossDamage = 30; r.BossEnragePerSecond = .004f; var m = new MatchSimulation(r,77);
        ClaimAll(m,new[] { 0,2,4,7,9,11 }); var run = new Trace(); float step = 1f / fps;
        m.Changed += e => { if (e.Kind == MatchEventKind.RoutePlanned || e.Kind == MatchEventKind.DoorHit || e.Kind == MatchEventKind.Eliminated || e.Kind == MatchEventKind.Finished) run.Events.Add(new Entry { Kind = e.Kind,House = e.HouseId,Time = m.Elapsed }); };
        for (int i = 0; i < fps * 420 && !m.Finished; i++) m.Tick(step);
        for (int h = 0; h < 12; h++) run.Health[h] = m.Houses[h].Health;
        return run;
    }
    // The largest legal step, so a weapon shot and the final door hit can share one step.
    private const float RaceStep = .1f;
    private static MatchSimulation TwoHouseRace(int seed,float bossHealth)
    {
        var r = Rules(); r.BossHealth = bossHealth; r.BossDamage = 60; var m = new MatchSimulation(r,seed); ClaimAll(m,new[] { 1,4 });
        m.CommandsFor(0).Place(1,WeaponKind.Gatling); m.CommandsFor(1).Place(1,(WeaponKind)(seed % 4));
        return m;
    }
    // Finds whether the step that ends a two-house race by elimination also contains a weapon shot.
    private static bool FinalStepHasShot(int seed,float bossHealth,out int finalStep,out float damageThroughFinal)
    {
        var m = TwoHouseRace(seed,bossHealth); int step = 0; bool shotThisStep = false; float damage = 0;
        m.Changed += e => { if (e.Kind == MatchEventKind.Shot) shotThisStep = true; };
        finalStep = -1; damageThroughFinal = 0;
        for (; step < 10 * 900 && !m.Finished; step++) {
            shotThisStep = false; m.Tick(RaceStep); damage = m.Players[0].DamageDealt + m.Players[1].DamageDealt;
        }
        if (!m.Finished || m.EndReason != MatchEndReason.LastStanding || !shotThisStep) return false;
        finalStep = step - 1; damageThroughFinal = damage; return true;
    }
    private static MatchSimulation LethalRace(int seed)
    {
        var r = Rules(); r.BossHealth = 2500; r.BossDamage = 120; var m = new MatchSimulation(r,seed); ClaimAll(m,new[] { 0,6,10 });
        foreach (int p in new[] { 0,1,2 }) m.CommandsFor(p).Place(0,WeaponKind.Rocket);
        for (int i = 0; i < 30 * 600 && !m.Finished; i++) m.Tick(Step);
        return m;
    }
    // Records boss behaviour and checks the route contract after the run.
    private sealed class Log
    {
        private readonly Dictionary<int,float> plannedAt = new Dictionary<int,float>();
        private readonly Dictionary<int,float> visitEnd = new Dictionary<int,float>();
        private int currentHouse = -1, currentHits, lastVisited = -1;
        public Log(MatchSimulation m)
        {
            m.Changed += e => {
                if (e.Kind == MatchEventKind.RoutePlanned) {
                    var h = m.Houses[e.HouseId]; True(h.Occupied);
                    float end; if (visitEnd.TryGetValue(e.HouseId,out end)) True(m.Elapsed >= end + 18 - Step - .002f);
                    // One other target before a repeat when another house is attackable.
                    if (e.HouseId == lastVisited) True(m.Houses.Count(x => x.Occupied && x.Id != e.HouseId && m.Elapsed - Step >= x.ProtectedUntil) == 0);
                    plannedAt[e.HouseId] = m.Elapsed; currentHouse = e.HouseId; currentHits = 0;
                }
                if (e.Kind == MatchEventKind.DoorHit) {
                    True(e.HouseId == currentHouse); True(e.PlayerId >= 0);
                    if (currentHits == 0) True(m.Elapsed - plannedAt[e.HouseId] >= 4 - Step);
                    currentHits++; True(currentHits <= 3);
                    if (currentHits == 3 || m.Houses[e.HouseId].Destroyed) { visitEnd[e.HouseId] = m.Elapsed; lastVisited = e.HouseId; }
                }
            };
        }
        public void Verify(MatchSimulation m,int occupied,float end)
        {
            var visits = m.Houses.Where(h => h.OwnerId >= 0 && h.Health > 0).Select(h => h.VisitsReceived).ToArray();
            if (visits.Length > 1) True(visits.Max() - visits.Min() <= 2);
            foreach (var h in m.Houses) if (h.OwnerId < 0) True(h.VisitsReceived == 0 && h.AttacksReceived == 0);
        }
    }
    private static string Passives()
    { return string.Join(",",CharacterCatalog.Defaults().Select(c => c.Bonus.ToString(System.Globalization.CultureInfo.InvariantCulture))); }
    private static string Fingerprint(MatchSimulation m)
    {
        var parts = new List<string>();
        foreach (var p in m.Players) parts.Add(p.Id + ":" + p.Gold.ToString("R") + ":" + p.HouseId + ":" + p.Eliminated + ":" + p.UpgradesPurchased);
        foreach (var h in m.Houses) {
            parts.Add(h.Id + ":" + h.OwnerId + ":" + h.Health.ToString("R") + ":" + h.IsBuilding);
            foreach (var w in h.Weapons) parts.Add(w == null ? "-" : w.Kind + "/" + w.Level + "/" + w.Invested + "/" + w.BuildRemaining.ToString("R"));
        }
        return string.Join("|",parts);
    }
    private static float SegmentDistance(Point2 p,Point2 a,Point2 b)
    {
        float dx = b.X - a.X, dz = b.Z - a.Z, len = dx * dx + dz * dz;
        float t = len < 1e-8f ? 0 : Math.Max(0,Math.Min(1,((p.X - a.X) * dx + (p.Z - a.Z) * dz) / len));
        return p.Distance(new Point2(a.X + dx * t,a.Z + dz * t));
    }
    private static MatchRules Rules() { return new MatchRules { StartingGold = 10000,PreparationSeconds = 30,UpgradeSeconds = 0,BossHealth = 100000,BossEnragePerSecond = 0 }; }
    // Players 0..n-1 walk to and claim the given houses during preparation, then preparation elapses.
    private static void ClaimAll(MatchSimulation m,int[] houses)
    {
        for (int p = 0; p < houses.Length; p++) {
            for (int i = 0; i < 1500 && m.Players[p].Position.Distance(m.Houses[houses[p]].Entry) > .1f; i++) m.Navigate(p,m.Houses[houses[p]].Entry,Step);
            True(m.TryClaim(p,houses[p]));
        }
    }
    private static void Reject(MapDefinition map) { bool rejected = false; try { map.Validate(); } catch (ArgumentException) { rejected = true; } True(rejected); }
    private static void Check(string name,Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    private static void True(bool value) { if (!value) throw new Exception("Match contract assertion failed"); }
    private static void False(bool value) { True(!value); }
    private static void Near(double expected,double actual) { if (Math.Abs(expected - actual) > .025) throw new Exception("Expected " + expected + ", got " + actual); }
}
