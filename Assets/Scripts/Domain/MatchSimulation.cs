using System;
using System.Collections.Generic;

namespace ContainerDefense.Domain
{
    // Local authority. Future transports submit commands here, never mutate view objects.
    public sealed class MatchSimulation
    {
        private readonly MatchRules rules;
        private readonly PlayerState[] players = new PlayerState[6];
        private readonly HouseState[] houses = new HouseState[6];
        private readonly Random random;
        private readonly Random[] purchaseRandom = new Random[6];
        public IReadOnlyList<PlayerState> Players { get; private set; }
        public IReadOnlyList<HouseState> Houses { get; private set; }
        public BossState Boss { get; private set; }
        public MatchPhase Phase { get; private set; }
        public float Elapsed { get; private set; }
        public float CombatSeconds { get { return Math.Max(0, Elapsed - rules.PreparationSeconds); } }
        public float PreparationRemaining { get { return Math.Max(0, rules.PreparationSeconds - Elapsed); } }
        public bool Finished { get { return Phase == MatchPhase.Victory || Phase == MatchPhase.Defeat; } }
        public event Action<MatchEvent> Changed;

        public MatchSimulation(MatchRules settings, int seed = 731, CharacterDefinition[] roster = null)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            rules = settings.Snapshot(); random = new Random(seed);
            if (roster != null && roster.Length != 6) throw new ArgumentException("A match requires six character selections.");
            var defaults = CharacterCatalog.Defaults();
            for (int i = 0; i < 6; i++)
            {
                var definition = roster == null ? defaults[0] : roster[i];
                if (definition == null || !CharacterCatalog.Valid(definition.Id) || (int)definition.Passive != (int)definition.Id ||
                    !MatchRules.Finite(definition.Bonus) || definition.Bonus < 0 || definition.Bonus > 1)
                    throw new ArgumentException("Invalid match character.");
                players[i] = new PlayerState(i, i != 0, rules.StartingGold, definition);
                houses[i] = new HouseState(i, rules.DoorHealth[0]);
                purchaseRandom[i] = new Random(unchecked(seed * 397 + i * 31 + 17));
            }
            Players = Array.AsReadOnly(players); Houses = Array.AsReadOnly(houses);
            Boss = new BossState(rules.BossHealth); Phase = MatchPhase.Preparation;
        }

        public void Move(int playerId, float x, float z, float seconds)
        {
            PlayerState p;
            if (!ActivePlayer(playerId, out p) || p.Sleeping || !ValidStep(seconds) ||
                !MatchRules.Finite(x) || !MatchRules.Finite(z)) return;
            float length = (float)Math.Sqrt(x * x + z * z);
            if (!MatchRules.Finite(length) || length < 0.001f) return;
            float scale = rules.MoveSpeed * p.Character.MoveMultiplier * seconds / Math.Max(1, length);
            p.Position = new Point2(Clamp(p.Position.X + x * scale, -15, 15),
                Clamp(p.Position.Z + z * scale, -8, 2.6f));
        }

        public bool TryClaim(int playerId, int houseId)
        {
            PlayerState p;
            if (!ActivePlayer(playerId, out p) || Phase != MatchPhase.Preparation || !ValidHouse(houseId) || p.HouseId >= 0)
                return false;
            HouseState h = houses[houseId];
            if (h.OwnerId >= 0 || p.Position.Distance(h.Entry) > rules.ClaimRadius) return false;
            h.OwnerId = playerId; p.HouseId = houseId;
            h.Health = h.MaxHealth = rules.DoorHealth[0] * p.Character.DoorMultiplier;
            Emit(MatchEventKind.Claimed, playerId, houseId); return true;
        }

        public bool TryToggleSleep(int playerId)
        {
            PlayerState p;
            if (!ActivePlayer(playerId, out p) || p.HouseId < 0) return false;
            HouseState h = houses[p.HouseId];
            if (!p.Sleeping && p.Position.Distance(h.Entry) > rules.ClaimRadius) return false;
            p.Sleeping = !p.Sleeping;
            p.Position = p.Sleeping ? new Point2(h.Entry.X - 0.85f, 5.1f) : h.Entry;
            Emit(MatchEventKind.Sleeping, playerId, h.Id); return true;
        }

        public int UpgradeCost(int houseId, UpgradeKind kind)
        {
            if (!ValidHouse(houseId) || !ValidKind(kind)) return -1;
            HouseState h = houses[houseId];
            int level = Level(h, kind); int[] costs = Costs(kind);
            return level < costs.Length ? costs[level] : -1;
        }
        public float Income(int houseId)
        {
            if (!ValidHouse(houseId)) return 0;
            var h = houses[houseId];
            return rules.BedIncome[h.BedLevel] * (h.OwnerId < 0 ? 1 : players[h.OwnerId].Character.IncomeMultiplier);
        }
        public float Damage(int houseId)
        {
            if (!ValidHouse(houseId)) return 0;
            var h = houses[houseId];
            return rules.WeaponDamage[h.WeaponLevel] * (h.OwnerId < 0 ? 1 : players[h.OwnerId].Character.DamageMultiplier);
        }

        public bool TryUpgrade(int playerId, int houseId, UpgradeKind kind)
        {
            PlayerState p;
            if (!ActivePlayer(playerId, out p) || !ValidHouse(houseId) || !ValidKind(kind)) return false;
            HouseState h = houses[houseId]; int cost = UpgradeCost(houseId, kind);
            if (p.HouseId != houseId || h.OwnerId != playerId || h.Destroyed || h.IsBuilding || cost < 0 || p.Gold < cost) return false;
            // Require the listed price first, so rejected requests cannot fish for discounts.
            bool discounted = p.Character.DiscountChance > 0 && purchaseRandom[playerId].NextDouble() < p.Character.DiscountChance;
            double paid = discounted ? cost * 0.5 : cost;
            p.Gold -= paid; p.UpgradesPurchased++;
            h.LastUpgradePaid = paid; h.LastUpgradeDiscounted = discounted;
            h.IsBuilding = true; h.BuildingKind = kind;
            h.BuildRemaining = h.BuildDuration = rules.UpgradeSeconds / p.Character.BuildMultiplier;
            Emit(MatchEventKind.UpgradeStarted, playerId, houseId, (float)paid);
            if (h.BuildRemaining <= 0) CompleteUpgrade(h);
            return true;
        }

        private void CompleteUpgrade(HouseState h)
        {
            switch (h.BuildingKind)
            {
                case UpgradeKind.Bed: h.BedLevel++; break;
                case UpgradeKind.Weapon: h.WeaponLevel++; break;
                case UpgradeKind.Door:
                    float oldMax = h.MaxHealth;
                    h.MaxHealth = rules.DoorHealth[++h.DoorLevel] * players[h.OwnerId].Character.DoorMultiplier;
                    h.Health += h.MaxHealth - oldMax;
                    break;
            }
            h.IsBuilding = false; h.BuildRemaining = 0;
            Emit(MatchEventKind.Upgraded, h.OwnerId, h.Id, (float)h.LastUpgradePaid);
        }

        public void Tick(float seconds)
        {
            if (Finished || !ValidStep(seconds)) return;
            Elapsed += seconds;
            if (Phase == MatchPhase.Preparation && PreparationRemaining <= 0)
            {
                Phase = MatchPhase.Combat; Boss.Phase = BossPhase.Selecting;
                foreach (PlayerState p in players) if (p.HouseId < 0) Eliminate(p);
                Emit(MatchEventKind.CombatStarted);
            }
            if (LivingHouses() == 0 && Phase == MatchPhase.Combat) { Finish(false); return; }
            foreach (var house in houses)
            {
                if (!house.IsBuilding || house.Destroyed) continue;
                house.BuildRemaining -= seconds;
                if (house.BuildRemaining <= 0.0001f) CompleteUpgrade(house);
            }
            foreach (PlayerState p in players)
            {
                if (p.Eliminated) continue;
                p.SurvivalSeconds = Elapsed;
                if (p.Sleeping && p.HouseId >= 0)
                {
                    double income = Income(p.HouseId) * seconds;
                    p.Gold += income; p.GoldEarned += income;
                }
            }
            if (Phase != MatchPhase.Combat) return;
            foreach (HouseState h in houses)
            {
                if (h.OwnerId < 0 || h.Destroyed) continue;
                h.ShotTimer += seconds;
                if (h.ShotTimer < rules.WeaponInterval) continue;
                h.ShotTimer -= rules.WeaponInterval;
                float damage = Math.Min(Boss.Health, Damage(h.Id));
                Boss.Health -= damage; players[h.OwnerId].DamageDealt += damage;
                Emit(MatchEventKind.Shot, h.OwnerId, h.Id, damage);
                if (Boss.Health <= 0) { Boss.Phase = BossPhase.Dead; Finish(true); return; }
            }
            TickBoss(seconds);
        }

        private void TickBoss(float seconds)
        {
            if (Boss.Phase == BossPhase.Selecting)
            {
                int pick = random.Next(LivingHouses());
                foreach (HouseState h in houses)
                {
                    if (h.OwnerId < 0 || h.Destroyed) continue;
                    if (pick-- == 0) { Boss.TargetHouseId = h.Id; break; }
                }
                Boss.Phase = BossPhase.Travelling;
            }
            HouseState target = houses[Boss.TargetHouseId];
            Point2 attackPoint = new Point2(target.Entry.X, target.Entry.Z - 1.5f);
            if (Boss.Phase == BossPhase.Travelling)
            {
                Boss.Position = Boss.Position.Towards(attackPoint, rules.BossMoveSpeed * seconds);
                if (Boss.Position.Distance(attackPoint) < 0.05f)
                { Boss.Phase = BossPhase.Attacking; Boss.AttackTimer = rules.BossAttackInterval; }
                return;
            }
            Boss.AttackTimer -= seconds;
            if (Boss.AttackTimer > 0) return;
            Boss.AttackTimer += rules.BossAttackInterval;
            float damage = rules.BossDamage * (1 + CombatSeconds * rules.BossEnragePerSecond);
            target.Health = Math.Max(0, target.Health - damage);
            Emit(MatchEventKind.DoorHit, target.OwnerId, target.Id, damage);
            if (!target.Destroyed) return;
            Eliminate(players[target.OwnerId]); Boss.TargetHouseId = -1; Boss.Phase = BossPhase.Selecting;
            if (LivingHouses() == 0) Finish(false);
        }

        public int LivingHouses()
        {
            int count = 0;
            foreach (HouseState h in houses) if (h.OwnerId >= 0 && !h.Destroyed) count++;
            return count;
        }
        private void Eliminate(PlayerState p)
        {
            p.Eliminated = true; p.Sleeping = false; p.SurvivalSeconds = Elapsed;
            if (p.HouseId >= 0) { houses[p.HouseId].IsBuilding = false; houses[p.HouseId].BuildRemaining = 0; }
            Emit(MatchEventKind.Eliminated, p.Id, p.HouseId);
        }
        private void Finish(bool victory)
        {
            Phase = victory ? MatchPhase.Victory : MatchPhase.Defeat;
            foreach (PlayerState p in players) p.Sleeping = false;
            Emit(MatchEventKind.Finished);
        }
        private bool ActivePlayer(int id, out PlayerState player)
        {
            player = id >= 0 && id < players.Length ? players[id] : null;
            return player != null && !player.Eliminated && !Finished;
        }
        private void Emit(MatchEventKind kind, int player = -1, int house = -1, float amount = 0)
        { if (Changed != null) Changed(new MatchEvent(kind, player, house, amount)); }
        private static bool ValidStep(float dt) { return MatchRules.Finite(dt) && dt > 0 && dt <= 0.1f; }
        private static float Clamp(float n, float min, float max) { return Math.Max(min, Math.Min(max, n)); }
        private static bool ValidKind(UpgradeKind kind) { return kind >= UpgradeKind.Bed && kind <= UpgradeKind.Weapon; }
        private bool ValidHouse(int id) { return id >= 0 && id < houses.Length; }
        private static int Level(HouseState h, UpgradeKind kind)
        { return kind == UpgradeKind.Bed ? h.BedLevel : kind == UpgradeKind.Door ? h.DoorLevel : h.WeaponLevel; }
        private int[] Costs(UpgradeKind kind)
        { return kind == UpgradeKind.Bed ? rules.BedCosts : kind == UpgradeKind.Door ? rules.DoorCosts : rules.WeaponCosts; }
    }
}
