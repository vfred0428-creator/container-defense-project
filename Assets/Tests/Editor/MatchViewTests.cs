using System;
using ContainerDefense.Domain;

// View-state rules for the TFT-style camera: neighbourhood while claiming, your base afterwards,
// read-only scouting of living players, the full map, spectating, and command gating.
public static class MatchViewTests
{
    private const float Step = 1f / 30;
    private static int passed;
    public static string Run()
    {
        passed = 0;
        Check("The claim race shows the neighbourhood; claiming settles on your own base", () => {
            var m = New(); var v = new MatchView(m);
            True(v.Mode == ViewMode.Neighborhood && !v.CanCommand);
            Claim(m,0,4); v.Refresh();
            True(v.Mode == ViewMode.Base && v.ViewingOwnBase && v.ViewedPlayer == 0 && v.CanCommand);
        });
        Check("Scouting another living base is read-only; your own portrait returns home", () => {
            var m = New(); var v = new MatchView(m); Claim(m,0,4); Claim(m,1,9); v.Refresh();
            True(v.View(1) && v.ViewingOther && v.ViewedPlayer == 1 && !v.CanCommand);
            True(v.View(0) && v.ViewingOwnBase && v.CanCommand);
            True(v.View(1)); v.ReturnHome(); True(v.ViewingOwnBase);
        });
        Check("Only living players with a house can be viewed", () => {
            var m = New(); var v = new MatchView(m); Claim(m,0,4); Claim(m,1,9); v.Refresh();
            False(v.View(2)); False(v.View(-1)); False(v.View(6)); True(v.ViewingOwnBase);
            True(m.CommandsFor(1).Forfeit()); False(v.View(1)); True(v.ViewingOwnBase);
        });
        Check("A scouted player who is eliminated sends the view back home", () => {
            var m = New(); var v = new MatchView(m); Claim(m,0,4); Claim(m,1,9); Claim(m,2,0); v.Refresh();
            True(v.View(1)); m.CommandsFor(1).Forfeit(); v.Refresh();
            True(v.ViewingOwnBase && v.CanCommand);
        });
        Check("The full map opens and closes; tapping an owned house scouts it, empty houses do nothing", () => {
            var m = New(); var v = new MatchView(m); Claim(m,0,4); Claim(m,1,9); v.Refresh();
            v.OpenFullMap(); True(v.Mode == ViewMode.FullMap && !v.CanCommand);
            False(v.SelectHouse(11)); False(v.SelectHouse(99)); True(v.Mode == ViewMode.FullMap);
            True(v.SelectHouse(9) && v.ViewingOther && v.ViewedPlayer == 1);
            v.OpenFullMap(); v.CloseFullMap(); True(v.ViewingOther && v.ViewedPlayer == 1);
            v.OpenFullMap(); True(v.SelectHouse(4) && v.ViewingOwnBase);
        });
        Check("An eliminated player spectates living bases and can never command", () => {
            var m = New(); var v = new MatchView(m); Claim(m,0,4); Claim(m,1,9); Claim(m,2,0); v.Refresh();
            True(m.CommandsFor(0).Forfeit()); v.Refresh();
            True(v.Spectating && v.Mode == ViewMode.Base && v.ViewedPlayer != 0 && !m.Players[v.ViewedPlayer].Eliminated && !v.CanCommand);
            False(v.View(0)); int first = v.ViewedPlayer; v.Spectate(1); True(v.ViewedPlayer != first && !m.Players[v.ViewedPlayer].Eliminated);
            v.ReturnHome(); True(v.ViewedPlayer != 0 && !v.CanCommand);
        });
        Check("An unclaimed player becomes a spectator when combat starts", () => {
            var m = New(); var v = new MatchView(m); Claim(m,1,9); Claim(m,2,0);
            while (m.Phase == MatchPhase.Preparation) m.Tick(Step);
            v.Refresh(); True(v.Mode == ViewMode.Base && v.ViewedPlayer != 0 && !v.CanCommand);
        });
        Check("A threat to your home is flagged only while you look elsewhere", () => {
            var r = Rules(); var m = new MatchSimulation(r,5); var v = new MatchView(m); Claim(m,0,4); Claim(m,1,9); v.Refresh();
            bool flagged = false, flaggedAtHome = false;
            for (int i = 0; i < 30 * 200 && !m.Finished; i++) {
                m.Tick(Step);
                if (m.Boss.TargetHouseId == 4) { v.View(1); flagged |= v.HomeUnderThreat; v.ReturnHome(); flaggedAtHome |= v.HomeUnderThreat; }
            }
            True(flagged && !flaggedAtHome);
        });
        Check("Viewing others blocks every command path the HUD uses", () => {
            var m = New(); var v = new MatchView(m); Claim(m,0,4); Claim(m,1,9); v.Refresh();
            foreach (var action in new Action[] { () => v.View(1),() => v.OpenFullMap() }) {
                v.ReturnHome(); action(); True(!v.CanCommand);
            }
            v.ReturnHome(); True(v.CanCommand);
        });
        return passed + " match view scenarios passed.";
    }
    private static MatchRules Rules() { return new MatchRules { StartingGold = 10000,PreparationSeconds = 30,UpgradeSeconds = 0,BossHealth = 100000,BossEnragePerSecond = 0,BossDamage = .01f }; }
    private static MatchSimulation New() { return new MatchSimulation(Rules(),3); }
    private static void Claim(MatchSimulation m,int player,int house)
    {
        for (int i = 0; i < 1500 && m.Players[player].Position.Distance(m.Houses[house].Entry) > .1f; i++) m.Navigate(player,m.Houses[house].Entry,Step);
        True(m.TryClaim(player,house));
    }
    private static void Check(string name,Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    private static void True(bool value) { if (!value) throw new Exception("Match view assertion failed"); }
    private static void False(bool value) { True(!value); }
}
