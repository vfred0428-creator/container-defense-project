using System;

namespace ContainerDefense.Domain
{
    [Serializable]
    public sealed class ProgressionRules
    {
        public int[] XpToNextLevel = DefaultCurve();
        public int CompletionXp = 30;
        public int SurvivalXpPerSecond = 1;
        public int MaximumSurvivalXp = 180;
        public int DamagePerXp = 30;
        public int MaximumDamageXp = 100;
        public int VictoryXp = 75;
        public static int[] DefaultCurve()
        {
            var curve = new int[99];
            for (int i = 0; i < curve.Length; i++) curve[i] = 120 + i * 40;
            return curve;
        }
        public ProgressionRules Snapshot()
        {
            if (XpToNextLevel == null || XpToNextLevel.Length < 14 || XpToNextLevel.Length > 999 ||
                CompletionXp < 0 || SurvivalXpPerSecond < 0 || MaximumSurvivalXp < 0 || DamagePerXp <= 0 ||
                MaximumDamageXp < 0 || VictoryXp < 0)
                throw new ArgumentException("Invalid account progression settings.");
            long total = 0;
            foreach (int xp in XpToNextLevel) { if (xp <= 0) throw new ArgumentException("XP requirements must be positive."); total += xp; }
            if (total > 1000000000) throw new ArgumentException("XP curve exceeds supported range.");
            var result = (ProgressionRules)MemberwiseClone(); result.XpToNextLevel = (int[])XpToNextLevel.Clone(); return result;
        }
        public long XpForLevel(int level)
        {
            long total = 0;
            for (int i = 0; i < Math.Min(level - 1, XpToNextLevel.Length); i++) total += XpToNextLevel[i];
            return total;
        }
        public int Level(long xp)
        {
            int level = 1;
            foreach (int required in XpToNextLevel) { if (xp < required) break; xp -= required; level++; }
            return level;
        }
        public int Reward(MatchSimulation match)
        {
            if (match == null || !match.Finished) return 0;
            var p = match.Players[0];
            if (p.HouseId < 0) return 0;
            float combatSurvival = Math.Max(0, p.SurvivalSeconds - (match.Elapsed - match.CombatSeconds));
            long survival = (long)Math.Min(MaximumSurvivalXp, (double)combatSurvival * SurvivalXpPerSecond);
            long damage = (long)Math.Min(MaximumDamageXp, p.DamageDealt / DamagePerXp);
            long reward = CompletionXp + survival + damage + (match.Phase == MatchPhase.Victory && !p.Eliminated ? VictoryXp : 0);
            return (int)Math.Min(1000000, reward);
        }
    }
}
