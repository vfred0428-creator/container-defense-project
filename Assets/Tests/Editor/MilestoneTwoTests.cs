using System;
using ContainerDefense.Domain;

public static class MilestoneTwoTests
{
    private const float Step = 1f / 30;
    private static int passed;
    public static string Run()
    {
        passed = 0;
        Check("Exactly seven definitions with requested unlock levels", () => {
            var catalog = Catalog(); int[] levels = { 1,3,5,7,9,12,15 };
            for (int i = 0; i < 7; i++) { Equal(levels[i],catalog.Get((CharacterId)i).UnlockLevel); True(catalog.Get((CharacterId)i).Bonus > 0); }
            var invalid = CharacterCatalog.Defaults(); invalid[1].Id = CharacterId.Milo;
            bool threw = false; try { new CharacterCatalog(invalid); } catch (ArgumentException) { threw = true; } True(threw);
            var detached = catalog.Get(CharacterId.Milo); detached.Bonus = .99f; Near(.08,catalog.Get(CharacterId.Milo).Bonus);
        });
        Check("Milo and Nori increase only their own sleeping income", () => {
            foreach (var id in new[] { CharacterId.Milo, CharacterId.Nori }) {
                var m = Match(id,CharacterId.Kiko); Claim(m,0,0); Claim(m,1,1);
                m.TryToggleSleep(0); m.TryToggleSleep(1); Advance(m,1);
                Near(id == CharacterId.Milo ? 69.32 : 69.48,m.Players[0].Gold); Near(69,m.Players[1].Gold);
                m.TryToggleSleep(0); double gold = m.Players[0].Gold; Advance(m,1); Near(gold,m.Players[0].Gold);
            }
        });
        Check("Lumi gains personal door HP at claim and every tier", () => {
            var r = new MatchRules { StartingGold = 1000, UpgradeSeconds = .3f };
            var m = Match(CharacterId.Lumi,CharacterId.Milo,r); Claim(m,0,0); Claim(m,1,1);
            Near(230,m.Houses[0].MaxHealth); Near(200,m.Houses[1].MaxHealth);
            True(m.TryUpgrade(0,0,UpgradeKind.Door)); Near(230,m.Houses[0].MaxHealth);
            Advance(m,.3f); Near(402.5,m.Houses[0].MaxHealth); Near(402.5,m.Houses[0].Health); Near(200,m.Houses[1].Health);
        });
        Check("Kiko weapon bonus applies to actual shots and later tiers", () => {
            var r = new MatchRules { PreparationSeconds = .1f, UpgradeSeconds = .3f };
            var m = Match(CharacterId.Kiko,CharacterId.Milo,r); Claim(m,0,0); Claim(m,1,1);
            Advance(m,1.2f); Near(9.9,m.Players[0].DamageDealt); Near(9,m.Players[1].DamageDealt);
            True(m.TryUpgrade(0,0,UpgradeKind.Weapon)); Advance(m,.3f); Near(18.7,m.Damage(0)); Near(9,m.Damage(1));
        });
        Check("Pip moves 12 percent faster without changing another resident", () => {
            var m = Match(CharacterId.Pip,CharacterId.Milo);
            var a = m.Players[0].Position; var b = m.Players[1].Position;
            m.Move(0,1,1,.1f); m.Move(1,1,1,.1f);
            Near(.56,a.Distance(m.Players[0].Position)); Near(.5,b.Distance(m.Players[1].Position));
        });
        Check("Yume completes a queued upgrade sooner; other upgrades remain pending", () => {
            var m = Match(CharacterId.Yume,CharacterId.Milo); Claim(m,0,0); Claim(m,1,1);
            True(m.TryUpgrade(0,0,UpgradeKind.Bed)); True(m.TryUpgrade(1,1,UpgradeKind.Bed));
            Near(1.5 / 1.1,m.Houses[0].BuildDuration); Near(1.5,m.Houses[1].BuildDuration);
            False(m.TryUpgrade(0,0,UpgradeKind.Door)); Near(20,m.Players[0].Gold);
            Advance(m,1.4f); Equal(1,m.Houses[0].BedLevel); Equal(0,m.Houses[1].BedLevel);
            Advance(m,.1f); Equal(1,m.Houses[1].BedLevel); False(m.Houses[1].IsBuilding);
        });
        Check("Mochi rolls 8 percent half-price purchases, never another player's cost", () => {
            int discounts = 0;
            for (int seed = 0; seed < 1000; seed++) {
                var m = Match(CharacterId.Mochi,CharacterId.Milo,null,seed); Claim(m,0,0); Claim(m,1,1);
                True(m.TryUpgrade(0,0,UpgradeKind.Bed)); True(m.TryUpgrade(1,1,UpgradeKind.Bed));
                var h = m.Houses[0];
                if (h.LastUpgradeDiscounted) { discounts++; Near(22.5,h.LastUpgradePaid); Near(42.5,m.Players[0].Gold); }
                else { Near(45,h.LastUpgradePaid); Near(20,m.Players[0].Gold); }
                Near(45,m.Houses[1].LastUpgradePaid); Near(20,m.Players[1].Gold);
            }
            True(discounts > 50 && discounts < 110); Console.WriteLine("Mochi sample: " + discounts + "/1000 discounted purchases.");
        });
        Check("Rejected Mochi purchases cannot reroll discounts or spend gold", () => {
            var r = new MatchRules { StartingGold = 35 };
            var a = Match(CharacterId.Mochi,CharacterId.Milo,r); var b = Match(CharacterId.Mochi,CharacterId.Milo,r);
            Claim(a,0,0); Claim(b,0,0);
            for (int i = 0; i < 100; i++) False(a.TryUpgrade(0,0,UpgradeKind.Bed));
            Near(35,a.Players[0].Gold); a.TryToggleSleep(0); b.TryToggleSleep(0); Advance(a,3); Advance(b,3);
            True(a.TryUpgrade(0,0,UpgradeKind.Bed)); True(b.TryUpgrade(0,0,UpgradeKind.Bed));
            Near(a.Houses[0].LastUpgradePaid,b.Houses[0].LastUpgradePaid);
        });
        Check("Elimination cancels unfinished construction", () => {
            var r = new MatchRules { UpgradeSeconds = 100, PreparationSeconds = .1f, BossDamage = 1000, BossHealth = 100000 };
            var m = Match(CharacterId.Yume,CharacterId.Milo,r); Claim(m,0,0); m.TryUpgrade(0,0,UpgradeKind.Bed);
            Advance(m,20); True(m.Players[0].Eliminated); False(m.Houses[0].IsBuilding); Equal(0,m.Houses[0].BedLevel);
        });
        Check("Each unlock appears exactly at its XP level boundary", () => {
            var rules = new ProgressionRules(); var catalog = Catalog();
            var fresh = new AccountProgression(null,catalog,rules); True(fresh.IsUnlocked(CharacterId.Milo));
            for (int i = 1; i < 7; i++) {
                var id = (CharacterId)i; long threshold = rules.XpForLevel(catalog.Get(id).UnlockLevel);
                var before = new AccountProgression(new AccountData { TotalXp = threshold - 1 },catalog,rules);
                var after = new AccountProgression(new AccountData { TotalXp = threshold },catalog,rules);
                False(before.IsUnlocked(id)); True(after.IsUnlocked(id));
                False(before.Select(id)); True(after.Select(id)); Equal(id,after.Selected);
            }
            var all = new AccountProgression(new AccountData { TotalXp = rules.XpForLevel(15) },catalog,rules);
            for (int i = 0; i < 7; i++) True(all.IsUnlocked((CharacterId)i));
        });
        Check("Stored level is derived from XP and earned unlocks persist", () => {
            var a = new AccountProgression(new AccountData { Level = 99, SelectedCharacter = "yume", UnlockedCharacters = new[] { "milo","yume","unknown" } },Catalog(),new ProgressionRules());
            Equal(1,a.Level); True(a.IsUnlocked(CharacterId.Yume)); Equal(CharacterId.Yume,a.Selected);
            var b = new AccountProgression(new AccountData { SelectedCharacter = "yume" },Catalog(),new ProgressionRules());
            Equal(CharacterId.Milo,b.Selected); False(b.Select((CharacterId)999));
            var loaded = new AccountProgression(a.Snapshot(),Catalog(),new ProgressionRules()); True(loaded.IsUnlocked(CharacterId.Yume));
        });
        Check("Match XP awards once, levels up, unlocks Lumi and survives reload", () => {
            var rules = new ProgressionRules();
            var a = new AccountProgression(new AccountData { TotalXp = rules.XpForLevel(3) - 1 },Catalog(),rules);
            var m = Match(CharacterId.Milo,CharacterId.Kiko,new MatchRules { PreparationSeconds = .1f, BossHealth = 1 }); long ticket = a.BeginMatch(m);
            MatchReward receipt; False(a.TryAward(m,ticket,out receipt)); Claim(m,0,0); Advance(m,2);
            True(a.TryAward(m,ticket,out receipt)); True(receipt.Xp > 0); Equal(3,a.Level); True(a.IsUnlocked(CharacterId.Lumi));
            Equal(CharacterId.Lumi,receipt.Unlocked[0]); long xp = a.TotalXp;
            False(a.TryAward(m,ticket,out receipt)); Equal(xp,a.TotalXp);
            var reload = new AccountProgression(a.Snapshot(),Catalog(),rules);
            False(reload.TryAward(m,ticket,out receipt)); Equal(xp,reload.TotalXp);
            var next = Match(CharacterId.Milo,CharacterId.Kiko); long nextTicket = a.BeginMatch(next);
            False(a.TryAward(m,nextTicket,out receipt)); Equal(xp,a.TotalXp);
        });
        Check("Abandoned, stale, future and unclaimed matches cannot farm XP", () => {
            var a = new AccountProgression(null,Catalog(),new ProgressionRules()); long old = a.BeginMatch(Match(CharacterId.Milo,CharacterId.Kiko));
            var m = Match(CharacterId.Milo,CharacterId.Kiko,new MatchRules { PreparationSeconds = .1f });
            long current = a.BeginMatch(m);
            Advance(m,1); MatchReward reward;
            False(a.TryAward(m,old,out reward)); False(a.TryAward(m,current + 1,out reward));
            True(a.TryAward(m,current,out reward)); Equal(0,reward.Xp); Equal(0L,a.TotalXp);
        });
        Check("An eliminated claimant gets participation XP without a victory bonus", () => {
            var m = Match(CharacterId.Milo,CharacterId.Kiko,new MatchRules { PreparationSeconds = .1f, BossDamage = 1000, BossHealth = 100000 });
            Claim(m,0,0); Advance(m,20);
            var rules = new ProgressionRules(); int xp = rules.Reward(m); True(xp >= rules.CompletionXp && xp < rules.CompletionXp + rules.VictoryXp);
        });
        Check("XP cap and maximum level are stable", () => {
            var rules = new ProgressionRules(); var a = new AccountProgression(new AccountData { TotalXp = long.MaxValue },Catalog(),rules);
            Equal(1000000000L,a.TotalXp); Equal(100,a.Level); Equal(0,a.XpNeeded);
        });
        Check("Every character can finish the configured gameplay loop", () => {
            for (int id = 0; id < 7; id++) {
                var m = Match((CharacterId)id,CharacterId.Kiko); var bots = new LocalBotController(m); Claim(m,0,0); m.TryToggleSleep(0);
                for (int frame = 0; frame < 12000 && !m.Finished; frame++) {
                    bots.Tick(Step); var h = m.Houses[0];
                    if (!h.IsBuilding && frame % 15 == 0) m.TryUpgrade(0,0,h.Health < h.MaxHealth * .65f ? UpgradeKind.Door : h.BedLevel < 2 ? UpgradeKind.Bed : UpgradeKind.Weapon);
                    m.Tick(Step);
                }
                True(m.Finished); True(m.Players[0].Gold >= 0);
            }
        });
        return passed + " character/progression scenarios passed.";
    }
    private static CharacterCatalog Catalog() { return new CharacterCatalog(CharacterCatalog.Defaults()); }
    private static MatchSimulation Match(CharacterId first, CharacterId second, MatchRules rules = null, int seed = 731)
    {
        var catalog = Catalog(); var roster = new CharacterDefinition[6]; roster[0] = catalog.Get(first);
        for (int i = 1; i < 6; i++) roster[i] = catalog.Get(second);
        return new MatchSimulation(rules ?? new MatchRules(),seed,roster);
    }
    private static void Claim(MatchSimulation m, int player, int house)
    {
        for (int i = 0; i < 600; i++) {
            var p = m.Players[player]; var target = m.Houses[house].Entry;
            if (p.Position.Distance(target) < .1f) break;
            m.Move(player,target.X - p.Position.X,target.Z - p.Position.Z,Step);
        }
        True(m.TryClaim(player,house));
    }
    private static void Advance(MatchSimulation m, float seconds) { for (int i = 0; i < (int)Math.Round(seconds / Step); i++) m.Tick(Step); }
    private static void Check(string name, Action run) { run(); passed++; Console.WriteLine("PASS " + name); }
    private static void True(bool value) { if (!value) throw new Exception("Expected true"); }
    private static void False(bool value) { True(!value); }
    private static void Equal<T>(T expected,T actual) { if (!Equals(expected,actual)) throw new Exception("Expected " + expected + ", got " + actual); }
    private static void Near(double expected,double actual) { if (Math.Abs(expected - actual) > .02) throw new Exception("Expected " + expected + ", got " + actual); }
}
