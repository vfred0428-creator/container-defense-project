using System;
using System.Collections.Generic;

namespace ContainerDefense.Domain
{
    [Serializable]
    public sealed class AccountData
    {
        public int Version = 2;
        public CollectionData Collection;
        public long TotalXp;
        public int Level = 1;
        public string SelectedCharacter = "milo";
        public string[] UnlockedCharacters = { "milo" };
        public long MatchesStarted;
        public long LastRewardedSequence;
    }

    public sealed class MatchReward
    {
        public int Xp { get; internal set; }
        public int PreviousLevel { get; internal set; }
        public int NewLevel { get; internal set; }
        public CharacterId[] Unlocked { get; internal set; }
    }

    public interface ISaveService
    {
        AccountData Load();
        void Save(AccountData account);
        string Status { get; }
        bool CanWrite { get; }
    }

    public sealed class AccountProgression
    {
        private readonly CharacterCatalog catalog;
        private readonly ProgressionRules rules;
        private readonly HashSet<CharacterId> unlocked = new HashSet<CharacterId>();
        private readonly AccountData data;
        private MatchSimulation activeMatch;
        public long TotalXp { get { return data.TotalXp; } }
        public int Level { get { return data.Level; } }
        public CharacterId Selected { get; private set; }
        public InventorySystem Inventory { get; private set; }
        public long XpInLevel { get { return TotalXp - rules.XpForLevel(Level); } }
        public int XpNeeded { get { return Level <= rules.XpToNextLevel.Length ? rules.XpToNextLevel[Level - 1] : 0; } }
        public bool IsUnlocked(CharacterId id) { return unlocked.Contains(id); }

        public AccountProgression(AccountData saved, CharacterCatalog characters, ProgressionRules settings, CollectionCatalog collections = null)
        {
            if (characters == null) throw new ArgumentNullException("characters");
            catalog = characters;
            rules = settings.Snapshot();
            saved = saved ?? new AccountData();
            if (saved.Version < 1 || saved.Version > 2) throw new ArgumentException("Unsupported save version.");
            Inventory = new InventorySystem(saved.Collection,collections ?? CollectionCatalog.CreateDefault());
            data = new AccountData {
                TotalXp = Math.Max(0,Math.Min(1000000000,saved.TotalXp)),
                MatchesStarted = Math.Max(0,Math.Min(long.MaxValue - 1,saved.MatchesStarted))
            };
            data.LastRewardedSequence = Math.Max(0,Math.Min(data.MatchesStarted,saved.LastRewardedSequence));
            data.Level = rules.Level(data.TotalXp);
            foreach (string key in saved.UnlockedCharacters ?? new string[0])
            { CharacterId id; if (CharacterCatalog.TryId(key,out id)) unlocked.Add(id); }
            UnlockForLevel();
            CharacterId selected;
            Selected = CharacterCatalog.TryId(saved.SelectedCharacter,out selected) && IsUnlocked(selected) ? selected : CharacterId.Milo;
        }
        public bool Select(CharacterId id)
        { if (!IsUnlocked(id)) return false; Selected = id; return true; }
        public long BeginMatch(MatchSimulation match)
        {
            if (match == null || match.Finished || match.Elapsed != 0 || ReferenceEquals(match,activeMatch))
                throw new ArgumentException("Start rewards with a new, unplayed match.");
            if (data.MatchesStarted == long.MaxValue) throw new InvalidOperationException("Match sequence exhausted.");
            activeMatch = match;
            return ++data.MatchesStarted;
        }
        public bool TryAward(MatchSimulation match, long sequence, out MatchReward reward)
        {
            reward = null;
            if (match == null || !ReferenceEquals(match,activeMatch) || !match.Finished || sequence <= data.LastRewardedSequence || sequence != data.MatchesStarted) return false;
            data.LastRewardedSequence = sequence;
            int xp = rules.Reward(match), before = Level;
            data.TotalXp = Math.Min(1000000000,data.TotalXp + xp); data.Level = rules.Level(data.TotalXp);
            reward = new MatchReward { Xp = xp, PreviousLevel = before, NewLevel = Level, Unlocked = UnlockForLevel() };
            return true;
        }
        private CharacterId[] UnlockForLevel()
        {
            var gained = new List<CharacterId>();
            for (int i = 0; i < 7; i++)
            {
                var d = catalog.Get((CharacterId)i);
                if (Level >= d.UnlockLevel && unlocked.Add(d.Id)) gained.Add(d.Id);
            }
            return gained.ToArray();
        }
        public AccountData Snapshot()
        {
            var keys = new List<string>();
            for (int i = 0; i < 7; i++) if (IsUnlocked((CharacterId)i)) keys.Add(CharacterCatalog.Key((CharacterId)i));
            return new AccountData { TotalXp = TotalXp, Level = Level, SelectedCharacter = CharacterCatalog.Key(Selected),
                UnlockedCharacters = keys.ToArray(), MatchesStarted = data.MatchesStarted, LastRewardedSequence = data.LastRewardedSequence,
                Collection = Inventory.Snapshot() };
        }
    }
}
