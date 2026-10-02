using System;
using ContainerDefense.Domain;

public static class RankingTests
{
    private static int passed;
    private static AccountProgression New(AccountData data = null) { return new AccountProgression(data,new CharacterCatalog(CharacterCatalog.Defaults()),new ProgressionRules()); }
    public static string Run()
    {
        passed = 0;
        Check("Eight solo tiers start at Rookie without touching account XP",() => {
            var a = New(new AccountData { Version = 3,TotalXp = 1234 });
            Assert(Enum.GetValues(typeof(RankTier)).Length == 8 && a.Rank.CurrentRank == RankTier.Rookie && a.TotalXp == 1234 && a.Rank.Season == 1);
        });
        Check("Five wins promote to the next tier and preserve the original snapshot",() => {
            var old = new RankData(); var next = old;
            for (int i = 0; i < 5; i++) next = RankProgression.ApplyResult(next,true);
            Assert(next.CurrentRank == RankTier.Scout && next.HighestRank == RankTier.Scout && next.Stars == 0 && next.RankPoints == 100 && next.Wins == 5 && next.MatchesPlayed == 5 && old.Wins == 0);
        });
        Check("Losses remove one star with a tier floor and never erase highest rank",() => {
            var next = RankProgression.ApplyResult(new RankData { CurrentRank = RankTier.Champion,HighestRank = RankTier.Celestial,Stars = 1,RankPoints = 2 },false);
            Assert(next.CurrentRank == RankTier.Champion && next.HighestRank == RankTier.Celestial && next.Stars == 0 && next.RankPoints == 0);
            next = RankProgression.ApplyResult(next,false); Assert(next.Stars == 0 && next.CurrentRank == RankTier.Champion);
        });
        Check("Sovereign and Int64 counters saturate without overflow",() => {
            var r = RankProgression.ApplyResult(new RankData { CurrentRank = RankTier.Sovereign,Stars = 5,RankPoints = long.MaxValue - 1,Wins = long.MaxValue,MatchesPlayed = long.MaxValue },true);
            Assert(r.CurrentRank == RankTier.Sovereign && r.Stars == 5 && r.RankPoints == long.MaxValue && r.Wins == long.MaxValue && r.MatchesPlayed == long.MaxValue);
        });
        Check("Malformed rank data clamps safely without granting promotions",() => {
            var r = RankProgression.Normalize(new RankData { CurrentRank = (RankTier)900,HighestRank = (RankTier)(-1),Stars = 999,RankPoints = -1,Wins = 9,MatchesPlayed = 2,Season = -1 });
            Assert(r.CurrentRank == RankTier.Rookie && r.HighestRank == RankTier.Rookie && r.Stars == 4 && r.RankPoints == 0 && r.Wins == 2 && r.Season == 1);
        });
        Check("Season metadata survives rewards without resetting other progression",() => {
            var r = RankProgression.ApplyResult(new RankData { Season = 12 },true); Assert(r.Season == 12);
            var a = New(new AccountData { TotalXp = 1200,Rank = r }); a.Inventory.ClaimStarter(); var restored = New(a.Snapshot());
            Assert(restored.TotalXp == 1200 && restored.Rank.Season == 12 && restored.Inventory.Charisma == 100);
        });
        Check("Casual completed matches update profile statistics but never rank",() => {
            var a = New(); var m = FinishedMatch(a,false); MatchReward reward;
            Assert(a.TryAward(m,1,out reward) && a.Rank.MatchesPlayed == 0 && a.Social.Profile.Matches == 1);
        });
        Check("Practice result applies only once alongside account reward receipt",() => {
            var a = New(); var m = FinishedMatch(a,true); MatchReward reward;
            Assert(a.TryAward(m,1,out reward) && a.Rank.MatchesPlayed == 1);
            Assert(!a.TryAward(m,1,out reward) && a.Rank.MatchesPlayed == 1 && a.Social.Profile.Matches == 1);
            var restored = New(a.Snapshot()); Assert(!restored.TryAward(m,1,out reward) && restored.Rank.MatchesPlayed == 1);
        });
        Check("Unfinished and stale matches cannot be rewarded; a replaced ranked match is one loss",() => {
            var a = New(); var m = new MatchSimulation(new MatchRules()); var seq = a.BeginMatch(m,true); MatchReward reward;
            Assert(!a.TryAward(m,seq,out reward) && a.Rank.MatchesPlayed == 0);
            var newer = new MatchSimulation(new MatchRules()); a.BeginMatch(newer,true); for (int i = 0; i < 1000 && !m.Finished; i++) m.Tick(.1f);
            Assert(!a.TryAward(m,seq,out reward) && a.Rank.MatchesPlayed == 1 && a.Rank.Wins == 0 && a.TotalXp == 0);
        });
        Check("Leaving a ranked match records one loss with no XP; leaving a casual one changes nothing",() => {
            var a = New(new AccountData { Rank = new RankData { CurrentRank = RankTier.Scout,Stars = 3,RankPoints = 40 } });
            var m = new MatchSimulation(new MatchRules()); long seq = a.BeginMatch(m,true);
            Assert(a.AbandonMatch() && !a.AbandonMatch());
            MatchReward reward; for (int i = 0; i < 2000 && !m.Finished; i++) m.Tick(.1f);
            Assert(!a.TryAward(m,seq,out reward));
            Assert(a.Rank.Stars == 2 && a.Rank.RankPoints == 35 && a.Rank.MatchesPlayed == 1 && a.Social.Profile.Matches == 1 && a.TotalXp == 0);
            var casual = new MatchSimulation(new MatchRules()); a.BeginMatch(casual,false);
            Assert(!a.AbandonMatch() && a.Rank.MatchesPlayed == 1 && a.Social.Profile.Matches == 1);
            var restored = New(a.Snapshot()); Assert(restored.Rank.Stars == 2 && restored.Rank.MatchesPlayed == 1);
        });
        Check("A ranked match cut off by a closed or crashed game is settled as a loss on the next load, once",() => {
            var a = New(new AccountData { Rank = new RankData { Stars = 2 } });
            a.BeginMatch(new MatchSimulation(new MatchRules()),true); var saved = a.Snapshot();   // saved when the match began
            var reloaded = New(saved); Assert(reloaded.Rank.Stars == 1 && reloaded.Rank.MatchesPlayed == 1 && reloaded.Social.Profile.Matches == 1);
            var again = New(reloaded.Snapshot()); Assert(again.Rank.Stars == 1 && again.Rank.MatchesPlayed == 1);
            var won = FinishedMatch(again,true); MatchReward reward; Assert(again.TryAward(won,again.Snapshot().MatchesStarted,out reward));
            var later = New(again.Snapshot()); Assert(later.Rank.MatchesPlayed == 2);
            var bogus = New(new AccountData { MatchesStarted = 3,LastRewardedSequence = 3,RankedSequence = 99 }); Assert(bogus.Rank.MatchesPlayed == 0);
        });
        Check("Actual surviving victory promotes once and persists profile wins",() => {
            var a = New(new AccountData { Rank = new RankData { Stars = 4 } });
            var m = new MatchSimulation(new MatchRules { PreparationSeconds = .1f,BossHealth = 1 }); long sequence = a.BeginMatch(m,true);
            for (int i = 0; i < 600; i++) m.Navigate(0,m.Houses[0].Entry,1f / 30);
            Assert(m.TryClaim(0,0)); Assert(m.TryPlaceWeapon(0,0,0,WeaponKind.Gatling)); for (int i = 0; i < 600 && !m.Finished; i++) m.Tick(.1f);
            MatchReward reward; Assert(m.Phase == MatchPhase.Victory && a.TryAward(m,sequence,out reward));
            Assert(a.Rank.CurrentRank == RankTier.Scout && a.Rank.Wins == 1 && a.Social.Profile.Wins == 1 && !a.TryAward(m,sequence,out reward));
            var restored = New(a.Snapshot()); Assert(restored.Rank.CurrentRank == RankTier.Scout && restored.Rank.Wins == 1 && restored.Social.Profile.Wins == 1);
        });
        Check("Ranked leaderboard contains only real recorded account data",() => {
            var a = New(); var service = new LocalLeaderboardService(a,CollectionCatalog.CreateDefault()); var rows = service.Get(LeaderboardKind.Ranked);
            Assert(rows.Length == 1 && rows[0].IsYou && rows[0].Rank.CurrentRank == RankTier.Rookie && rows[0].Score == 0);
            rows[0].Rank.Stars = 4; Assert(a.Rank.Stars == 0);
        });
        Check("Charisma and Popularity leaderboards sort independently with stable ties",() => {
            var a = New(); a.Inventory.ClaimStarter(); var data = a.Snapshot(); data.Social.Recipients[1].Popularity = 99; a = New(data);
            var service = new LocalLeaderboardService(a,CollectionCatalog.CreateDefault()); var charm = service.Get(LeaderboardKind.Charisma); var pop = service.Get(LeaderboardKind.Popularity);
            Assert(charm.Length == 3 && charm[0].IsYou && charm[0].Score == 100 && pop[0].PlayerId == "local_b" && pop[0].Score == 99 && a.Rank.Stars == 0);
            Assert(charm[1].PlayerId == "local_a" && charm[2].PlayerId == "local_b");
        });
        Check("Audio settings default for old saves, persist, and clamp without touching progression",() => {
            var a = New(new AccountData { Version = 4,TotalXp = 900 }); var p = a.Audio;
            Assert(p.MusicVolume > 0 && p.SfxVolume > 0 && !p.Muted);
            a.SetAudio(new AudioPrefs { MusicVolume = 2,SfxVolume = float.NaN,Muted = true });
            var restored = New(a.Snapshot());
            Assert(restored.Audio.MusicVolume == 1 && restored.Audio.SfxVolume >= 0 && restored.Audio.SfxVolume <= 1 && restored.Audio.Muted && restored.TotalXp == 900 && restored.Rank.CurrentRank == RankTier.Rookie);
        });
        return passed + " ranking scenarios passed.";
    }
    private static MatchSimulation FinishedMatch(AccountProgression a,bool ranked)
    {
        var rules = new MatchRules { PreparationSeconds = .1f }; var m = new MatchSimulation(rules); a.BeginMatch(m,ranked);
        for (int i = 0; i < 100 && !m.Finished; i++) m.Tick(.1f);
        Assert(m.Finished); return m;
    }
    private static void Check(string name,Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Ranking assertion failed."); }
}
