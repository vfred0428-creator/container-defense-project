using System;
using System.Collections.Generic;

namespace ContainerDefense.Domain
{
    [Serializable]
    public sealed class AccountData
    {
        public int Version = 4;
        public CollectionData Collection;
        public SocialData Social;
        public RankData Rank;
        public long TotalXp;
        public int Level = 1;
        public string SelectedCharacter = "milo";
        public string[] UnlockedCharacters = { "milo" };
        public long MatchesStarted;
        public long LastRewardedSequence;
        // Added after v4 without a version bump: older saves load with null and get defaults.
        public AudioPrefs Audio;
    }
    [Serializable]
    public sealed class AudioPrefs
    {
        public float MusicVolume = .6f, SfxVolume = .8f;
        public bool Muted;
        public static AudioPrefs Normalize(AudioPrefs p)
        {
            if (p == null) return new AudioPrefs();
            return new AudioPrefs { MusicVolume = Clamp(p.MusicVolume),SfxVolume = Clamp(p.SfxVolume),Muted = p.Muted };
        }
        private static float Clamp(float v) { return float.IsNaN(v) || float.IsInfinity(v) ? .7f : Math.Max(0,Math.Min(1,v)); }
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
        private readonly CollectionCatalog collections;
        internal readonly object SocialGate = new object();
        private MatchSimulation activeMatch;
        private bool activeRanked;
        public long TotalXp { get { return data.TotalXp; } }
        public int Level { get { return data.Level; } }
        public CharacterId Selected { get; private set; }
        public InventorySystem Inventory { get; private set; }
        public SocialData Social { get { return SocialState.Copy(data.Social,collections); } }
        public RankData Rank { get { return RankProgression.Normalize(data.Rank); } }
        public AudioPrefs Audio { get { return AudioPrefs.Normalize(data.Audio); } }
        public void SetAudio(AudioPrefs prefs) { data.Audio = AudioPrefs.Normalize(prefs); }
        public long XpInLevel { get { return TotalXp - rules.XpForLevel(Level); } }
        public int XpNeeded { get { return Level <= rules.XpToNextLevel.Length ? rules.XpToNextLevel[Level - 1] : 0; } }
        public bool IsUnlocked(CharacterId id) { return unlocked.Contains(id); }

        public AccountProgression(AccountData saved, CharacterCatalog characters, ProgressionRules settings, CollectionCatalog collections = null)
        {
            if (characters == null) throw new ArgumentNullException("characters");
            catalog = characters;
            rules = settings.Snapshot();
            saved = saved ?? new AccountData();
            if (saved.Version < 1 || saved.Version > 4) throw new ArgumentException("Unsupported save version.");
            this.collections = collections ?? CollectionCatalog.CreateDefault();
            Inventory = new InventorySystem(saved.Collection,this.collections);
            data = new AccountData {
                Social = SocialState.Copy(saved.Social,this.collections),
                Rank = RankProgression.Normalize(saved.Rank),
                TotalXp = Math.Max(0,Math.Min(1000000000,saved.TotalXp)),
                MatchesStarted = Math.Max(0,Math.Min(long.MaxValue - 1,saved.MatchesStarted)),
                Audio = AudioPrefs.Normalize(saved.Audio)
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
        public long BeginMatch(MatchSimulation match,bool practiceRanked = false)
        {
            if (match == null || match.Finished || match.Elapsed != 0 || ReferenceEquals(match,activeMatch))
                throw new ArgumentException("Start rewards with a new, unplayed match.");
            if (data.MatchesStarted == long.MaxValue) throw new InvalidOperationException("Match sequence exhausted.");
            activeMatch = match; activeRanked = practiceRanked;
            return ++data.MatchesStarted;
        }
        public bool TryAward(MatchSimulation match, long sequence, out MatchReward reward)
        {
            reward = null;
            if (match == null || !ReferenceEquals(match,activeMatch) || !match.Finished || sequence <= data.LastRewardedSequence || sequence != data.MatchesStarted) return false;
            data.LastRewardedSequence = sequence;
            data.Social.Profile.Matches = data.Social.Profile.Matches == long.MaxValue ? long.MaxValue : data.Social.Profile.Matches + 1;
            if (match.Phase == MatchPhase.Victory && !match.Players[0].Eliminated && data.Social.Profile.Wins < long.MaxValue) data.Social.Profile.Wins++;
            if (activeRanked) data.Rank = RankProgression.ApplyResult(data.Rank,match.Phase == MatchPhase.Victory && !match.Players[0].Eliminated);
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
                Collection = Inventory.Snapshot(), Social = Social, Rank = Rank, Audio = Audio };
        }
        internal void AdoptSocial(AccountData saved)
        { Inventory = new InventorySystem(saved.Collection,collections); data.Social = SocialState.Copy(saved.Social,collections); }
        public void SaveTo(ISaveService storage)
        { lock (SocialGate) { storage.Save(Snapshot()); } }
    }
}
