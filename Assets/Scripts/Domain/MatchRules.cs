using System;

namespace ContainerDefense.Domain
{
    [Serializable]
    public sealed class MatchRules
    {
        public float PreparationSeconds = 25;
        public float MoveSpeed = 5;
        public float ClaimRadius = 1.65f;
        public float StartingGold = 65;
        public float BossHealth = 77500;
        public float BossDamage = 32;
        public float BossAttackInterval = 1.7f;
        public float BossMoveSpeed = 3.2f;
        public float BossEnragePerSecond = 0.006f;
        public float WeaponInterval = 1;
        public float UpgradeSeconds = 1.5f;
        public float[] BedIncome = { 4, 7, 11, 16, 23 };
        public float[] DoorHealth = { 200, 350, 560, 840, 1200 };
        public float[] WeaponDamage = { 9, 17, 29, 46, 70 };
        public int[] BedCosts = { 45, 90, 165, 280 };
        public int[] DoorCosts = { 50, 95, 175, 300 };
        public int[] WeaponCosts = { 50, 100, 185, 320 };

        public void Validate()
        {
            Positive(PreparationSeconds, "PreparationSeconds");
            Positive(MoveSpeed, "MoveSpeed"); Positive(ClaimRadius, "ClaimRadius");
            Positive(BossHealth, "BossHealth"); Positive(BossDamage, "BossDamage");
            Positive(BossAttackInterval, "BossAttackInterval");
            Positive(BossMoveSpeed, "BossMoveSpeed"); Positive(WeaponInterval, "WeaponInterval");
            if (!Finite(UpgradeSeconds) || UpgradeSeconds < 0) throw new ArgumentException("Invalid upgrade duration.");
            if (!Finite(StartingGold) || StartingGold < 0 || !Finite(BossEnragePerSecond) || BossEnragePerSecond < 0)
                throw new ArgumentException("Gold and enrage must be finite and nonnegative.");
            Levels(BedIncome, BedCosts); Levels(DoorHealth, DoorCosts); Levels(WeaponDamage, WeaponCosts);
        }

        public MatchRules Snapshot()
        {
            Validate();
            var copy = (MatchRules)MemberwiseClone();
            copy.BedIncome = (float[])BedIncome.Clone(); copy.BedCosts = (int[])BedCosts.Clone();
            copy.DoorHealth = (float[])DoorHealth.Clone(); copy.DoorCosts = (int[])DoorCosts.Clone();
            copy.WeaponDamage = (float[])WeaponDamage.Clone(); copy.WeaponCosts = (int[])WeaponCosts.Clone();
            return copy;
        }

        internal static bool Finite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }
        private static void Positive(float n, string name)
        {
            if (!Finite(n) || n <= 0) throw new ArgumentException(name + " must be finite and positive.");
        }
        private static void Levels(float[] values, int[] costs)
        {
            if (values == null || costs == null || values.Length == 0 || values.Length != costs.Length + 1)
                throw new ArgumentException("Each upgrade table needs one more value than costs.");
            for (int i = 0; i < values.Length; i++)
            {
                Positive(values[i], "Upgrade value");
                if (i > 0 && values[i] <= values[i - 1]) throw new ArgumentException("Upgrade values must increase.");
            }
            foreach (int cost in costs) if (cost <= 0) throw new ArgumentException("Upgrade costs must be positive.");
        }
    }
}
