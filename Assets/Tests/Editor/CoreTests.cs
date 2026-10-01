using System;
using System.Collections.Generic;
using ContainerDefense.Domain;

public static class CoreTests
{
    private const float Step = 1f / 30;
    private static int passed;
    public static int Main()
    {
        try { Console.WriteLine(Run()); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    public static string Run()
    {
        passed = 0;
        Check("Twelve houses and six independent starting wallets", () => {
            var m = New(); Equal(12, m.Houses.Count); Equal(6, m.Players.Count);
            foreach (var h in m.Houses) Equal(-1, h.OwnerId);
            Equal(65, m.Players[0].Gold);
        });
        Check("Movement normalizes diagonals and enforces yard bounds", () => {
            var m = New(); var start = m.Players[0].Position;
            m.Move(0, 1, 1, Step); Near(5 * Step, start.Distance(m.Players[0].Position));
            for (int i = 0; i < 1000; i++) m.Move(0, 1, 1, Step);
            True(Math.Abs(m.Players[0].Position.X) <= m.Map.MaxX && Math.Abs(m.Players[0].Position.Z) <= m.Map.MaxZ); True(m.Map.Walkable(m.Players[0].Position));
        });
        Check("Claim requires proximity and is first-wins", () => {
            var m = New(); False(m.TryClaim(0, 0)); Walk(m, 0, 0); Walk(m, 1, 0);
            True(m.TryClaim(0, 0)); False(m.TryClaim(1, 0)); False(m.TryClaim(0, 1));
            Equal(0, m.Houses[0].OwnerId); Equal(-1, m.Players[1].HouseId);
        });
        Check("Unclaimed and awake players do not earn gold", () => {
            var m = New(); Advance(m, 2); Equal(65, m.Players[0].Gold);
            Claim(m, 0, 0); Advance(m, 2); Equal(65, m.Players[0].Gold);
        });
        Check("Sleeping income is personal; waking stops it", () => {
            var m = New(); Claim(m, 0, 0); True(m.TryToggleSleep(0));
            var pos = m.Players[0].Position; m.Move(0, 1, 0, Step); Near(0, pos.Distance(m.Players[0].Position));
            Advance(m, 3); Near(77.96, m.Players[0].Gold); Equal(65, m.Players[1].Gold);
            True(m.TryToggleSleep(0)); double gold = m.Players[0].Gold; Advance(m, 2); Near(gold, m.Players[0].Gold);
        });
        Check("Bed upgrade debits exact cost and increases income", () => {
            var m = New(); Claim(m, 0, 0); True(m.TryUpgrade(0, 0, UpgradeKind.Bed));
            Equal(20, m.Players[0].Gold); m.TryToggleSleep(0); Advance(m, 2);
            Near(35.12, m.Players[0].Gold); Equal(1, m.Players[0].UpgradesPurchased);
        });
        Check("Purchases reject wrong owner, insufficient funds, invalid ids and kinds", () => {
            var m = New(); Claim(m, 0, 0);
            False(m.TryUpgrade(1, 0, UpgradeKind.Door)); False(m.TryUpgrade(0, 5, UpgradeKind.Door));
            False(m.TryUpgrade(-1, 0, UpgradeKind.Door)); False(m.TryUpgrade(0, 0, (UpgradeKind)99));
            True(m.TryUpgrade(0, 0, UpgradeKind.Door)); False(m.TryUpgrade(0, 0, UpgradeKind.Door));
            Equal(15, m.Players[0].Gold); Near(350, m.Houses[0].Health);
        });
        Check("Door upgrade preserves existing damage", () => {
            var r = Rules(); r.PreparationSeconds = 0.1f; r.StartingGold = 200;
            var m = new MatchSimulation(r); Claim(m, 0, 0);
            while (m.Houses[0].Health == 200) m.Tick(Step);
            float missing = 200 - m.Houses[0].Health;
            True(m.TryUpgrade(0, 0, UpgradeKind.Door)); Near(missing, 350 - m.Houses[0].Health);
        });
        Check("Maximum upgrade levels reject without charging", () => {
            var r = Rules(); r.StartingGold = 10000; var m = new MatchSimulation(r); Claim(m, 0, 0);
            foreach (UpgradeKind kind in new[] { UpgradeKind.Bed, UpgradeKind.Door }) {
                for (int i = 0; i < 4; i++) True(m.TryUpgrade(0, 0, kind));
                double gold = m.Players[0].Gold; False(m.TryUpgrade(0, 0, kind));
                Equal(gold, m.Players[0].Gold); Equal(-1, m.UpgradeCost(0, kind));
            }
        });
        Check("Unclaimed players eliminated at deadline and late claims rejected", () => {
            var r = Rules(); r.PreparationSeconds = 1; var m = new MatchSimulation(r);
            Claim(m, 0, 0); Advance(m, 1.1f); True(m.Players[1].Eliminated);
            False(m.Players[0].Eliminated); False(m.TryClaim(1, 1)); Equal(MatchPhase.Combat, m.Phase);
        });
        Check("Nobody claiming yields defeat", () => {
            var r = Rules(); r.PreparationSeconds = 1; var m = new MatchSimulation(r); Advance(m, 2);
            Equal(MatchPhase.Defeat, m.Phase);
        });
        Check("Boss travels, telegraphs, attacks, eliminates, retargets and the last house standing wins", () => {
            var r = Rules(); r.PreparationSeconds = 0.1f; r.BossDamage = 500;
            r.BossHealth = 100000; var m = new MatchSimulation(r); Claim(m, 0, 0); Claim(m, 1, 1); Claim(m, 2, 2);
            int hits = 0; m.Changed += e => { if (e.Kind == MatchEventKind.DoorHit) hits++; };
            m.Tick(Step); m.Tick(Step); m.Tick(Step); m.Tick(Step);
            Equal(BossPhase.Telegraphing, m.Boss.Phase); Equal(0, hits);
            for (int i = 0; i < 1800 && !m.Finished; i++) m.Tick(Step);
            Equal(2, hits); Equal(MatchPhase.Victory, m.Phase); Equal(MatchEndReason.LastStanding, m.EndReason);
            int standing = 0; for (int p = 0; p < 3; p++) if (!m.Players[p].Eliminated) { standing++; Equal(p, m.WinnerId); Equal(1, m.Players[p].Placement); }
            Equal(1, standing); Equal(BossPhase.Waiting, m.Boss.Phase);
        });
        Check("Elimination blocks earnings, movement, upgrades and sleep", () => {
            var r = Rules(); r.PreparationSeconds = 0.1f; r.BossDamage = 1000; r.BossHealth = 100000;
            var m = new MatchSimulation(r); Claim(m, 0, 0); Claim(m, 1, 1); m.TryToggleSleep(0); m.TryToggleSleep(1);
            while (!m.Players[0].Eliminated && !m.Players[1].Eliminated) m.Tick(Step);
            int id = m.Players[0].Eliminated ? 0 : 1; var p = m.Players[id];
            double gold = p.Gold; var position = p.Position;
            Advance(m, 0.5f); m.Move(id, 1, 1, Step);
            Near(gold, p.Gold); Near(0, position.Distance(p.Position));
            False(m.TryUpgrade(id, p.HouseId, UpgradeKind.Bed)); False(m.TryToggleSleep(id));
        });
        Check("Boss death ends combat; terminal simulation is frozen", () => {
            var r = Rules(); r.PreparationSeconds = 0.1f; r.BossHealth = 1;
            var m = new MatchSimulation(r); Claim(m, 0, 0); m.TryToggleSleep(0); True(m.TryPlaceWeapon(0,0,0,WeaponKind.Gatling)); Advance(m, 40);
            Equal(MatchPhase.Victory, m.Phase); Equal(BossPhase.Dead, m.Boss.Phase);
            Near(0, m.Boss.Health); Near(1, m.Players[0].DamageDealt); False(m.Players[0].Eliminated);
            double gold = m.Players[0].Gold; float elapsed = m.Elapsed;
            Advance(m, 5); Near(gold, m.Players[0].Gold); Near(elapsed, m.Elapsed);
            False(m.TryUpgrade(0, 0, UpgradeKind.Bed));
        });
        Check("Weapon upgrades increase attributed damage", () => {
            var r = Rules(); r.PreparationSeconds = 0.1f; r.StartingGold = 1000;
            var m = new MatchSimulation(r); Claim(m, 0, 0); True(m.TryPlaceWeapon(0,0,0,WeaponKind.Gatling));
            float before = m.Damage(0); True(m.TryUpgradeWeapon(0,0,0)); True(m.Damage(0) > before);
            Advance(m,40); True(m.Players[0].DamageDealt > 0); True(Math.Abs(new MatchRules().BossHealth - m.Players[0].DamageDealt - m.Boss.Health) < 1); // float health at boss scale
        });
        Check("Invalid inputs do not poison the simulation", () => {
            var m = New(); var p = m.Players[0].Position;
            m.Move(0, float.NaN, 1, Step); m.Move(0, float.MaxValue, float.MaxValue, Step);
            m.Tick(float.NaN); m.Tick(-1); m.Tick(100); Near(0, m.Elapsed); Near(0, p.Distance(m.Players[0].Position));
        });
        Check("Configuration is validated and snapshotted", () => {
            var r = Rules(); var m = new MatchSimulation(r); r.BedIncome[0] = 9999;
            Near(4, m.Income(0)); r.BossHealth = float.NaN;
            bool threw = false; try { new MatchSimulation(r); } catch (ArgumentException) { threw = true; } True(threw);
        });
        Check("Bots reroute when human claims their preferred house", () => {
            var m = New(); Claim(m, 0, 1); m.TryToggleSleep(0); var bots = new LocalBotController(m);
            for (int i = 0; i < 720; i++) { bots.Tick(Step); m.Tick(Step); }
            var owners = new HashSet<int>();
            foreach (var h in m.Houses) if (h.OwnerId >= 0) True(owners.Add(h.OwnerId)); Equal(6,owners.Count);
            Equal(0, m.Houses[1].OwnerId);
        });
        Check("Default six-player loop reaches a result across 30 seeds", () => {
            int wins = 0, losses = 0;
            for (int seed = 0; seed < 30; seed++) {
                var m = new MatchSimulation(Rules(), seed); var bots = new LocalBotController(m);
                Claim(m, 0, 0); m.TryToggleSleep(0);
                for (int i = 0; i < 18000 && !m.Finished; i++) {
                    bots.Tick(Step);
                    if (i % 30 == 0) {
                        var h = m.Houses[0];
                        var choice = h.Health < h.MaxHealth * 0.65f ? UpgradeKind.Door :
                            h.BedLevel < 2 ? UpgradeKind.Bed : h.WeaponLevel < 4 ? UpgradeKind.Weapon : UpgradeKind.Door;
                        m.TryUpgrade(0, 0, choice);
                    }
                    m.Tick(Step);
                }
                True(m.Finished); foreach (var p in m.Players) True(p.Gold >= 0);
                if (m.Phase == MatchPhase.Victory) wins++; else losses++;
            }
            Console.WriteLine("Default balance sample: " + wins + " boss defeats / " + losses + " full eliminations.");
            True(wins > 0);
        });
        return passed + " core regression scenarios passed. " + MilestoneTwoTests.Run() + " " + CollectionTests.Run() + " " + SocialTests.Run() + " " + RankingTests.Run() + " " + NeighborhoodTests.Run() + " " + MatchContractTests.Run() + " " + MatchViewTests.Run() + " " + BoardTests.Run() + " " + InteriorTests.Run();
    }
    private static MatchRules Rules() { return new MatchRules { UpgradeSeconds = 0 }; }
    private static MatchSimulation New() { return new MatchSimulation(Rules()); }
    private static void Advance(MatchSimulation m, float seconds) { for (int i = 0; i < (int)Math.Round(seconds / Step); i++) m.Tick(Step); }
    private static void Walk(MatchSimulation m, int player, int house)
    {
        for (int i = 0; i < 600; i++) {
            var p = m.Players[player]; var target = m.Houses[house].Entry;
            if (p.Position.Distance(target) < 0.1f) return;
            m.Navigate(player,target,Step);
        }
        throw new Exception("Walk did not reach the house.");
    }
    private static void Claim(MatchSimulation m, int player, int house) { Walk(m, player, house); True(m.TryClaim(player, house)); }
    private static void Check(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    private static void True(bool value) { if (!value) throw new Exception("Expected true"); }
    private static void False(bool value) { True(!value); }
    private static void Equal<T>(T expected, T actual) { if (!Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual); }
    private static void Near(double expected, double actual) { if (Math.Abs(expected - actual) > 0.02) throw new Exception("Expected ~" + expected + ", got " + actual); }
}

