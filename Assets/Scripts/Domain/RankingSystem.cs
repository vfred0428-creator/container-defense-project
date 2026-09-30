using System;
using System.Collections.Generic;

namespace ContainerDefense.Domain
{
    public enum RankTier { Rookie, Scout, Defender, Vanguard, Champion, Ascendant, Celestial, Sovereign }
    [Serializable]
    public sealed class RankData
    {
        public RankTier CurrentRank, HighestRank;
        public int Stars;
        public long RankPoints;
        public int Season = 1;
        public long Wins, MatchesPlayed;
    }
    public static class RankProgression
    {
        public const int StarsPerRank = 5;
        public static RankData Normalize(RankData saved)
        {
            saved = saved ?? new RankData();
            var current = Valid(saved.CurrentRank) ? saved.CurrentRank : RankTier.Rookie;
            var highest = Valid(saved.HighestRank) && saved.HighestRank > current ? saved.HighestRank : current;
            return new RankData { CurrentRank = current, HighestRank = highest,
                Stars = Math.Max(0,Math.Min(current == RankTier.Sovereign ? StarsPerRank : StarsPerRank - 1,saved.Stars)),
                RankPoints = Math.Max(0,saved.RankPoints), Season = Math.Max(1,saved.Season),
                MatchesPlayed = Math.Max(0,saved.MatchesPlayed), Wins = Math.Max(0,Math.Min(saved.MatchesPlayed,saved.Wins)) };
        }
        // Intentionally small practice rules. Online placement/season policies belong to server authority.
        public static RankData ApplyResult(RankData saved,bool survivedVictory)
        {
            var next = Normalize(saved);
            next.MatchesPlayed = Add(next.MatchesPlayed,1);
            if (survivedVictory)
            {
                next.Wins = Add(next.Wins,1); next.RankPoints = Add(next.RankPoints,20);
                next.Stars = Math.Min(StarsPerRank,next.Stars + 1);
                if (next.Stars == StarsPerRank && next.CurrentRank < RankTier.Sovereign) { next.CurrentRank++; next.Stars = 0; }
            }
            else { next.Stars = Math.Max(0,next.Stars - 1); next.RankPoints = Math.Max(0,next.RankPoints - 5); }
            if (next.CurrentRank > next.HighestRank) next.HighestRank = next.CurrentRank;
            return next;
        }
        private static bool Valid(RankTier rank) { return rank >= RankTier.Rookie && rank <= RankTier.Sovereign; }
        private static long Add(long value,long increment) { return value > long.MaxValue - increment ? long.MaxValue : value + increment; }
    }
    public enum LeaderboardKind { Ranked, Charisma, Popularity }
    public sealed class LeaderboardEntry
    {
        public string PlayerId, Username;
        public bool IsYou;
        public RankData Rank;
        public long Score;
        public int CollectionCount;
    }
    public interface ILeaderboardService { LeaderboardEntry[] Get(LeaderboardKind kind); }
    public sealed class LocalLeaderboardService : ILeaderboardService
    {
        private readonly AccountProgression account;
        private readonly CollectionCatalog catalog;
        public LocalLeaderboardService(AccountProgression owner,CollectionCatalog definitions) { account = owner; catalog = definitions; }
        public LeaderboardEntry[] Get(LeaderboardKind kind)
        {
            if (!Enum.IsDefined(typeof(LeaderboardKind),kind)) throw new ArgumentException("Unknown leaderboard.");
            var snapshot = account.Snapshot(); var s = snapshot.Social; var rank = snapshot.Rank;
            var rows = new List<LeaderboardEntry>();
            rows.Add(new LeaderboardEntry { PlayerId = s.Profile.PlayerId, Username = s.Profile.Username, IsYou = true, Rank = rank,
                Score = kind == LeaderboardKind.Ranked ? rank.RankPoints : kind == LeaderboardKind.Charisma ? snapshot.Collection.Charisma : s.Profile.Popularity,
                CollectionCount = account.Inventory.SkinsOwned + account.Inventory.StickerTypesOwned });
            if (kind != LeaderboardKind.Ranked)
                foreach (var peer in s.Recipients)
                {
                    var inventory = new InventorySystem(peer.Collection,catalog);
                    rows.Add(new LeaderboardEntry { PlayerId = peer.PlayerId, Username = peer.Username,
                        Score = kind == LeaderboardKind.Charisma ? inventory.Charisma : peer.Popularity,
                        CollectionCount = inventory.SkinsOwned + inventory.StickerTypesOwned });
                }
            rows.Sort((a,b) => {
                int order;
                if (kind == LeaderboardKind.Ranked) {
                    order = b.Rank.CurrentRank.CompareTo(a.Rank.CurrentRank); if (order != 0) return order;
                    order = b.Rank.Stars.CompareTo(a.Rank.Stars); if (order != 0) return order;
                }
                order = b.Score.CompareTo(a.Score); return order != 0 ? order : StringComparer.Ordinal.Compare(a.PlayerId,b.PlayerId);
            });
            return rows.ToArray();
        }
    }
}
