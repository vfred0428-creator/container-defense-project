using System;
using System.Collections.Generic;

namespace ContainerDefense.Domain
{
    // Local authority. Future transports submit commands here, never mutate view objects.
    public sealed partial class MatchSimulation
    {
        private readonly MatchRules rules;
        private readonly PlayerState[] players = new PlayerState[6];
        private readonly HouseState[] houses = new HouseState[12];
        private readonly MapDefinition map;
        public MapDefinition Map { get { return map.Snapshot(); } }
        private readonly Point2[][] navigation = new Point2[6][];
        private readonly int[] navigationIndex = new int[6];
        private readonly Point2[] navigationGoal = new Point2[6];
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

        public MatchSimulation(MatchRules settings, int seed = 731, CharacterDefinition[] roster = null,MapDefinition layout = null)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            rules = settings.Snapshot(); random = new Random(seed);
            map = (layout ?? MapDefinition.Default()).Snapshot();
            if (roster != null && roster.Length != 6) throw new ArgumentException("A match requires six character selections.");
            var defaults = CharacterCatalog.Defaults();
            for (int i = 0; i < 6; i++)
            {
                var definition = roster == null ? defaults[0] : roster[i];
                if (definition == null || !CharacterCatalog.Valid(definition.Id) || (int)definition.Passive != (int)definition.Id ||
                    !MatchRules.Finite(definition.Bonus) || definition.Bonus < 0 || definition.Bonus > 1)
                    throw new ArgumentException("Invalid match character.");
                players[i] = new PlayerState(i, i != 0, rules.StartingGold, definition);
                players[i].Position = map.Spawn(i);
                purchaseRandom[i] = new Random(unchecked(seed * 397 + i * 31 + 17));
            }
            for (int i = 0; i < houses.Length; i++) houses[i] = new HouseState(map.HouseSpawns[i],rules.DoorHealth[0]);
            Players = Array.AsReadOnly(players); Houses = Array.AsReadOnly(houses);
            Boss = new BossState(rules.BossHealth); Phase = MatchPhase.Preparation;
            Boss.EntryNode = map.EntryNodes[random.Next(map.EntryNodes.Length)]; Boss.Position = map.Nodes[Boss.EntryNode].Position;
        }

        public void Move(int playerId, float x, float z, float seconds)
        {
            PlayerState p;
            if (!ActivePlayer(playerId, out p) || p.Sleeping || !ValidStep(seconds) ||
                !MatchRules.Finite(x) || !MatchRules.Finite(z)) return;
            float length = (float)Math.Sqrt(x * x + z * z);
            if (!MatchRules.Finite(length) || length < 0.001f) return;
            float scale = rules.MoveSpeed * p.Character.MoveMultiplier * seconds / Math.Max(1, length);
            p.Position = map.ClampMove(p.Position,new Point2(p.Position.X + x * scale,p.Position.Z + z * scale));
        }
        public void Navigate(int playerId,Point2 goal,float dt)
        {
            PlayerState p; if (!ActivePlayer(playerId,out p) || p.Sleeping || !ValidStep(dt)) return;
            if (navigation[playerId] == null || navigationGoal[playerId].Distance(goal) > .05f) {
                navigation[playerId] = map.RoadPath(p.Position,goal); navigationIndex[playerId] = 0; navigationGoal[playerId] = goal;
            }
            var path = navigation[playerId]; int index = navigationIndex[playerId];
            while (index < path.Length && p.Position.Distance(path[index]) < (index == path.Length - 1 ? .05f : .25f)) index++;
            navigationIndex[playerId] = index; if (index >= path.Length) return;
            Move(playerId,path[index].X - p.Position.X,path[index].Z - p.Position.Z,dt);
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
            p.Position = p.Sleeping ? new Point2(h.Center.X - 2,h.Center.Z + .4f) : h.Entry;
            navigation[playerId] = null;
            Emit(MatchEventKind.Sleeping, playerId, h.Id); return true;
        }

        public int UpgradeCost(int houseId, UpgradeKind kind)
        {
            if (!ValidHouse(houseId) || !ValidKind(kind)) return -1;
            if (kind == UpgradeKind.Weapon) return WeaponUpgradeCost(houseId,0);
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
            var h = houses[houseId]; float result = 0;
            foreach (var weapon in h.Weapons) if (weapon != null) result += WeaponCatalog.Get(weapon.Kind).Damage * (1 + (weapon.Level - 1) * .7f);
            return result * (h.OwnerId < 0 ? 1 : players[h.OwnerId].Character.DamageMultiplier);
        }

        public bool TryUpgrade(int playerId, int houseId, UpgradeKind kind)
        {
            if (kind == UpgradeKind.Weapon) return TryUpgradeWeapon(playerId,houseId,0);
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
            TickWeapons(seconds);
            if (Finished) return;
            if (Phase != MatchPhase.Combat) return;
            TickBoss(seconds);
        }

        private void TickBoss(float seconds)
        {
            TickBossRoute(seconds);
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
            if (p.HouseId >= 0) {
                var h = houses[p.HouseId]; h.IsBuilding = false; h.BuildRemaining = 0;
                foreach (var w in h.Weapons) if (w != null) w.BuildRemaining = 0;
            }
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
