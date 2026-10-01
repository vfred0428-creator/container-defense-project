using System;
using ContainerDefense.Domain;

// House interior rules: when the room is shown, the boss warning, the Bed / Door / Weapons card states,
// the upgrade queue and the double-buy guard. All prices and effects come from the simulation.
public static class InteriorTests
{
    private const float Step = 1f / 30;
    private static int passed;
    public static string Run()
    {
        passed = 0;
        Check("The room opens on request at your own living base and keeps the request while you scout", () => {
            var m = New(5000,0); var v = new InteriorView(m);
            v.Update(true,Step); False(v.Inside); False(v.Available);   // no house yet
            Claim(m,0,4); v.Update(true,Step); True(v.Available); False(v.Inside);
            v.Enter(); v.Update(true,Step); True(v.Inside);
            v.Update(false,Step); False(v.Inside); True(v.Requested);    // scouting someone else
            v.Update(true,Step); True(v.Inside);
            v.Toggle(); v.Update(true,Step); False(v.Inside);
        });
        Check("The boss attacking your house forces the board; the room returns 1 s after the attack ends", () => {
            var m = New(5000,0,.01f); Claim(m,0,4); Claim(m,1,9); var v = new InteriorView(m); v.Enter();
            bool forced = false, returned = false; float outFor = 0;
            for (int i = 0; i < 30 * 400 && !m.Finished && !returned; i++) {
                m.Tick(Step); v.Update(true,Step);
                bool attacking = m.Boss.Phase == BossPhase.Attacking && m.Boss.TargetHouseId == 4;
                if (attacking) { forced = true; False(v.Inside); outFor = 0; continue; }
                if (!forced) continue;
                outFor += Step;
                if (outFor < InteriorView.ReturnDelay - Step) False(v.Inside);
                if (outFor > InteriorView.ReturnDelay + Step) { True(v.Inside); returned = true; }
            }
            True(forced && returned);
        });
        Check("The warning banner shows within 5 s of the boss reaching your house, never for other houses", () => {
            var m = New(5000,0,.01f); Claim(m,0,4); Claim(m,1,9); var v = new InteriorView(m);
            bool bannerBeforeAttack = false, attacked = false;
            for (int i = 0; i < 30 * 400 && !m.Finished && !attacked; i++) {
                m.Tick(Step); v.Update(true,Step);
                if (v.BossIncoming) { True(m.Boss.TargetHouseId == 4 && v.BossEta <= InteriorView.BannerSeconds && v.BossInWindow); bannerBeforeAttack = true; }
                if (m.Boss.TargetHouseId != 4) False(v.BossIncoming || v.BossInWindow);
                if (m.Boss.Phase == BossPhase.Attacking && m.Boss.TargetHouseId == 4) attacked = true;
            }
            True(attacked && bannerBeforeAttack);
        });
        Check("Elimination or the match ending closes the room", () => {
            var m = New(5000,0); Claim(m,0,4); Claim(m,1,9); Claim(m,2,1); var v = new InteriorView(m); v.Enter(); v.Update(true,Step); True(v.Inside);
            True(m.CommandsFor(0).Forfeit()); v.Update(true,Step); False(v.Inside); False(v.Requested); False(v.Available);
        });
        Check("Card states: ready, too expensive, building, waiting, queued, max and full", () => {
            var m = New(60,1.5f); Claim(m,0,4); var q = new UpgradeQueue();
            True(HouseStations.State(m,0,Station.Bed,q) == CardState.Ready);       // 45 <= 60
            True(HouseStations.State(m,0,Station.Door,q) == CardState.Ready);      // 50 <= 60
            True(q.Request(m,0,Station.Bed));
            True(HouseStations.State(m,0,Station.Bed,q) == CardState.Building);
            True(HouseStations.State(m,0,Station.Door,q) == CardState.Waiting);
            False(q.Request(m,0,Station.Door)); True(HouseStations.State(m,0,Station.Door,q) == CardState.Queued);
            True(HouseStations.State(m,0,Station.Weapons,q) == CardState.Waiting);
            for (int i = 0; i < 60; i++) { m.Tick(Step); q.Tick(m,0); }
            // Door queued but 15 gold left is not enough: it stays queued, and shows the red price once the queue is cleared.
            True(m.Houses[4].BedLevel == 1 && !m.Houses[4].IsBuilding && HouseStations.State(m,0,Station.Door,q) == CardState.Queued);
            q.Clear(); True(HouseStations.State(m,0,Station.Door,q) == CardState.TooExpensive);
            var rich = New(100000,0); Claim(rich,0,4); var none = new UpgradeQueue();
            for (int i = 0; i < 4; i++) True(none.Request(rich,0,Station.Bed));
            True(HouseStations.State(rich,0,Station.Bed,none) == CardState.Max && HouseStations.Cost(rich,4,Station.Bed) == -1);
            False(none.Request(rich,0,Station.Bed)); True(none.Queued == null);
            for (int s = 0; s < 3; s++) True(rich.CommandsFor(0).Place(s,WeaponKind.Gatling));
            True(HouseStations.State(rich,0,Station.Weapons,none) == CardState.Full && HouseStations.Owned(rich,4) == 3);
        });
        Check("A queued upgrade is bought as soon as the house is free; tapping it again cancels", () => {
            var m = New(10000,1.5f); Claim(m,0,4); var q = new UpgradeQueue();
            True(q.Request(m,0,Station.Door)); False(q.Request(m,0,Station.Bed)); True(q.Queued == Station.Bed);
            False(q.Request(m,0,Station.Bed)); True(q.Queued == null);                    // cancelled
            False(q.Request(m,0,Station.Bed)); bool bought = false;
            for (int i = 0; i < 120 && !bought; i++) { m.Tick(Step); bought = q.Tick(m,0); }
            True(bought && m.Houses[4].IsBuilding && m.Houses[4].BuildingKind == UpgradeKind.Bed && m.Houses[4].DoorLevel == 1 && q.Queued == null);
        });
        Check("The purchase guard ignores taps within 0.3 s of an accepted one", () => {
            var g = new PurchaseGuard();
            True(g.TryPass(10f)); False(g.TryPass(10.05f)); False(g.TryPass(10.29f)); True(g.TryPass(10.31f)); False(g.TryPass(10.4f));
            var m = New(10000,0); Claim(m,0,4); var q = new UpgradeQueue(); var guard = new PurchaseGuard(); int bought = 0;
            foreach (float t in new[] { 1f,1.05f,1.1f }) if (guard.TryPass(t) && q.Request(m,0,Station.Bed)) bought++;
            True(bought == 1 && m.Houses[4].BedLevel == 1);
        });
        Check("Card effect lines and previews read the real rules", () => {
            var m = New(10000,0); Claim(m,0,4);
            string plus = (m.IncomeAtLevel(4,1) - m.IncomeAtLevel(4,0)).ToString("0.#"), next = m.IncomeAtLevel(4,1).ToString("0.#");
            True(HouseStations.Effect(m,4,Station.Bed) == "Income +" + plus + " gold/s" && HouseStations.Effect(m,4,Station.Door) == "House HP +150");
            True(HouseStations.Preview(m,4,Station.Bed) == "Lv 2: " + next + " gold/s while asleep" && next != plus && HouseStations.Preview(m,4,Station.Door) == "Lv 2: 350 house HP");
            True(HouseStations.Effect(m,4,Station.Weapons) == "0 of 3 built" && HouseStations.Cost(m,4,Station.Weapons) == 35);
            True(HouseStations.Level(m,4,Station.Bed) == 1 && HouseStations.Level(m,4,Station.Door) == 1);
        });
        return passed + " interior scenarios passed.";
    }
    private static MatchSimulation New(float gold,float build,float bossDamage = 32)
    {
        return new MatchSimulation(new MatchRules { StartingGold = gold,UpgradeSeconds = build,PreparationSeconds = 30,BossHealth = 1000000,BossDamage = bossDamage,BossEnragePerSecond = 0 },2);
    }
    private static void Claim(MatchSimulation m,int player,int house)
    {
        for (int i = 0; i < 1500 && m.Players[player].Position.Distance(m.Houses[house].Entry) > .1f; i++) m.Navigate(player,m.Houses[house].Entry,Step);
        True(m.TryClaim(player,house));
    }
    private static void Check(string name,Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    private static void True(bool value) { if (!value) throw new Exception("Interior assertion failed"); }
    private static void False(bool value) { True(!value); }
}
