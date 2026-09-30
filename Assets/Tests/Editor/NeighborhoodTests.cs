using System;
using System.Collections.Generic;
using ContainerDefense.Domain;

public static class NeighborhoodTests
{
    private const float Step = 1f / 30;
    private static int passed;
    public static string Run()
    {
        passed = 0;
        Check("All twelve fixed houses are reachable without walking through walls", () => {
            for (int house = 0; house < 12; house++) {
                var m = New(); Claim(m,0,house); True(m.Houses[house].OwnerId == 0);
            }
        });
        Check("Seeds vary boss entrances without shuffling house identities", () => {
            var entries = new HashSet<int>(); var reference = MapDefinition.Default();
            for (int seed = 1; seed <= 40; seed++) {
                var m = new MatchSimulation(new MatchRules(),seed); entries.Add(m.Boss.EntryNode);
                for (int h = 0; h < 12; h++) Near(0,m.Houses[h].Center.Distance(reference.HouseSpawns[h].Center));
            }
            True(entries.Count > 3);
        });
        Check("Map input and returned snapshots cannot alter an active match", () => {
            var map = MapDefinition.Default(); var m = new MatchSimulation(new MatchRules(),1,null,map);
            map.HouseSpawns[0].X = 100; m.Map.HouseSpawns[1].X = 100; m.Map.Routes[0].NodeSequence[0] = 999;
            Near(-16.5,m.Houses[0].Center.X); Near(-5.5,m.Houses[1].Center.X); True(m.Map.Routes[0].NodeSequence[0] != 999);
        });
        Check("Invalid route timing and nonfinite coordinates are rejected", () => {
            var map = MapDefinition.Default(); map.Routes[0].TelegraphTime = float.NaN; Reject(map);
            map = MapDefinition.Default(); map.Nodes[0].X = float.PositiveInfinity; Reject(map);
            map = MapDefinition.Default(); map.Routes[0].MinimumWave = 4; map.Routes[0].MaximumWave = 1; Reject(map);
        });
        Check("Unbuilt houses have no invisible weapon damage", () => {
            var m = New(); Claim(m,0,0); Advance(m,20); Near(0,m.Players[0].DamageDealt); Near(100000,m.Boss.Health);
        });
        Check("Duplicate placement charges once and wrong-owner mutations fail", () => {
            var m = New(); Claim(m,0,0); Claim(m,1,1); double own = m.Players[0].Gold, other = m.Players[1].Gold;
            True(m.TryPlaceWeapon(0,0,0,WeaponKind.Gatling)); False(m.TryPlaceWeapon(0,0,0,WeaponKind.Cannon));
            False(m.TryPlaceWeapon(1,0,1,WeaponKind.Gatling)); False(m.TryUpgradeWeapon(1,0,0)); False(m.TrySellWeapon(1,0,0)); False(m.TryMoveWeapon(1,0,0,1)); False(m.TryRepair(1,0));
            Near(own - 35,m.Players[0].Gold); Near(other,m.Players[1].Gold);
        });
        Check("Invalid sockets and weapon ids do not mutate balances", () => {
            var m = New(); Claim(m,0,0); double gold = m.Players[0].Gold;
            False(m.TryPlaceWeapon(0,0,-1,WeaponKind.Cannon)); False(m.TryPlaceWeapon(0,0,3,WeaponKind.Cannon)); False(m.TryPlaceWeapon(0,0,0,(WeaponKind)99));
            False(m.TryUpgradeWeapon(0,0,99)); False(m.TrySellWeapon(0,0,99)); False(m.TryMoveWeapon(0,0,99,0)); Near(gold,m.Players[0].Gold);
        });
        Check("Building weapons cannot fire, move, sell or upgrade early", () => {
            var r = Rules(); r.UpgradeSeconds = 5; var m = new MatchSimulation(r); Claim(m,0,0);
            True(m.TryPlaceWeapon(0,0,0,WeaponKind.Rocket)); False(m.TryUpgradeWeapon(0,0,0)); False(m.TryMoveWeapon(0,0,0,1)); False(m.TrySellWeapon(0,0,0));
            Advance(m,4); Near(0,m.Players[0].DamageDealt); True(m.Houses[0].Weapons[0].Building);
        });
        Check("Movement preserves weapon investment, level and fire cooldown", () => {
            var m = New(); Claim(m,0,0); m.TryPlaceWeapon(0,0,0,WeaponKind.Cannon); m.TryUpgradeWeapon(0,0,0);
            var w = m.Houses[0].Weapons[0]; for (int i = 0; i < 3000 && w.ShotCooldown <= 0; i++) m.Tick(Step);
            True(w.ShotCooldown > 0); float cooldown = w.ShotCooldown; double gold = m.Players[0].Gold;
            True(m.TryMoveWeapon(0,0,0,2)); True(ReferenceEquals(w,m.Houses[0].Weapons[2])); True(m.Houses[0].Weapons[0] == null);
            Near(cooldown,w.ShotCooldown); Near(150,w.Invested); True(w.Level == 2); Near(gold,m.Players[0].Gold);
            False(m.TryMoveWeapon(0,0,2,2));
        });
        Check("Sell refund never exceeds actual paid investment and cannot replay", () => {
            var m = New(); Claim(m,0,0); double gold = m.Players[0].Gold;
            m.TryPlaceWeapon(0,0,0,WeaponKind.Gatling); m.TryUpgradeWeapon(0,0,0);
            True(m.TrySellWeapon(0,0,0)); False(m.TrySellWeapon(0,0,0)); Near(gold - 105 + 52,m.Players[0].Gold);
        });
        Check("Only physically in-range weapons deal attributed damage", () => {
            var m = New(); Claim(m,0,0); m.TryPlaceWeapon(0,0,0,WeaponKind.Gatling);
            int shots = 0; m.Changed += e => { if (e.Kind == MatchEventKind.Shot) { shots++; True(m.WeaponPoint(e.HouseId,(int)e.Amount).Distance(m.Boss.Position) <= 15.001f); } };
            for (int i = 0; i < 3000 && shots == 0; i++) m.Tick(Step);
            True(shots > 0); True(m.Players[0].DamageDealt > 0); Near(0,m.Players[1].DamageDealt);
        });
        Check("Weapon levels stop at three without extra debit", () => {
            var m = New(); Claim(m,0,0); m.TryPlaceWeapon(0,0,0,WeaponKind.Gatling);
            True(m.TryUpgradeWeapon(0,0,0)); True(m.TryUpgradeWeapon(0,0,0)); double gold = m.Players[0].Gold;
            False(m.TryUpgradeWeapon(0,0,0)); Near(gold,m.Players[0].Gold); True(m.WeaponUpgradeCost(0,0) == -1);
        });
        Check("Repair charges only for damaged owned houses and clamps to maximum", () => {
            var m = New(); Claim(m,0,0); double gold = m.Players[0].Gold;
            False(m.TryRepair(0,0)); for (int i = 0; i < 3000 && m.Houses[0].Health == m.Houses[0].MaxHealth; i++) m.Tick(Step);
            True(m.TryRepair(0,0)); Near(m.Houses[0].MaxHealth,m.Houses[0].Health); Near(gold - 40,m.Players[0].Gold);
        });
        Check("Boss warns for four seconds and moves within its speed budget", () => {
            var m = New(); Claim(m,0,0); Advance(m,.2f); True(m.Boss.Phase == BossPhase.Telegraphing);
            var start = m.Boss.Position; Advance(m,3); Near(0,start.Distance(m.Boss.Position)); True(m.Houses[0].AttacksReceived == 0);
            for (int i = 0; i < 2000 && m.Houses[0].AttacksReceived == 0; i++) {
                var old = m.Boss.Position; m.Tick(Step); True(old.Distance(m.Boss.Position) <= 3.2f * Step + .001f);
            }
            True(m.Houses[0].AttacksReceived > 0);
        });
        Check("Three-hit visits enforce recovery even for the last living house", () => {
            var r = Rules(); r.BossDamage = .1f; var m = new MatchSimulation(r); Claim(m,0,0);
            int hits = 0; float previousVisitEnd = -100; int revisits = 0;
            m.Changed += e => { if (e.Kind == MatchEventKind.DoorHit) { hits++; if (hits == 3) previousVisitEnd = m.Elapsed; if (hits > 3) throw new Exception("Unbounded boss visit"); } };
            BossPhase previous = m.Boss.Phase;
            for (int i = 0; i < 12000; i++) {
                m.Tick(Step);
                if (m.Boss.Phase == BossPhase.Telegraphing && previous != BossPhase.Telegraphing) { True(m.Elapsed >= previousVisitEnd + 18 - .001f); if (hits > 0) revisits++; hits = 0; }
                previous = m.Boss.Phase;
            }
            True(revisits > 1);
        });
        Check("Boss skips empty houses and rotates away from its last target", () => {
            var r = Rules(); r.BossDamage = .1f; var m = new MatchSimulation(r); Claim(m,0,0); Claim(m,1,11);
            int last = -1, visits = 0; BossPhase previous = m.Boss.Phase;
            for (int i = 0; i < 12000; i++) {
                m.Tick(Step);
                if (m.Boss.Phase == BossPhase.Telegraphing && previous != BossPhase.Telegraphing) { True(m.Boss.TargetHouseId == 0 || m.Boss.TargetHouseId == 11); True(m.Boss.TargetHouseId != last); last = m.Boss.TargetHouseId; visits++; }
                previous = m.Boss.Phase;
            }
            True(visits > 3);
        });
        Check("Route wave eligibility and authored telegraph duration are honored", () => {
            var map = MapDefinition.Default(); foreach (var r in map.Routes) r.MinimumWave = 2;
            map.Routes[2].MinimumWave = 1; map.Routes[2].TelegraphTime = 6;
            var m = new MatchSimulation(Rules(),4,null,map); Claim(m,0,0); Claim(m,1,1); Claim(m,2,2); Advance(m,.2f);
            True(m.Boss.RouteId == "zig_zag"); True(m.Boss.TelegraphRemaining > 5.8f);
        });
        Check("Destroyed houses cannot be repaired, rebuilt, sold or re-claimed", () => {
            var r = Rules(); r.BossDamage = 10000; var m = new MatchSimulation(r); Claim(m,0,0); m.TryPlaceWeapon(0,0,0,WeaponKind.Gatling);
            for (int i = 0; i < 3000 && !m.Finished; i++) m.Tick(Step);
            True(m.Players[0].Eliminated); False(m.TryRepair(0,0)); False(m.TrySellWeapon(0,0,0)); False(m.TryPlaceWeapon(0,0,1,WeaponKind.Cannon)); False(m.TryClaim(1,0));
            double gold = m.Players[0].Gold; float damage = m.Players[0].DamageDealt; Advance(m,10); Near(gold,m.Players[0].Gold); Near(damage,m.Players[0].DamageDealt);
        });
        return passed + " neighborhood scenarios passed.";
    }
    private static MatchRules Rules() { return new MatchRules { StartingGold = 10000,PreparationSeconds = .1f,UpgradeSeconds = 0,BossHealth = 100000,BossEnragePerSecond = 0 }; }
    private static MatchSimulation New() { return new MatchSimulation(Rules()); }
    private static void Claim(MatchSimulation m,int player,int house) {
        for (int i = 0; i < 900 && m.Players[player].Position.Distance(m.Houses[house].Entry) > .1f; i++) {
            m.Navigate(player,m.Houses[house].Entry,Step); True(m.Map.Walkable(m.Players[player].Position));
        }
        True(m.TryClaim(player,house));
    }
    private static void Advance(MatchSimulation m,float seconds) { for (int i = 0; i < (int)Math.Round(seconds / Step); i++) m.Tick(Step); }
    private static void Reject(MapDefinition map) { bool rejected = false; try { map.Validate(); } catch (ArgumentException) { rejected = true; } True(rejected); }
    private static void Check(string name,Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    private static void True(bool value) { if (!value) throw new Exception("Neighborhood assertion failed"); }
    private static void False(bool value) { True(!value); }
    private static void Near(double expected,double actual) { if (Math.Abs(expected - actual) > .025) throw new Exception("Expected " + expected + ", got " + actual); }
}
