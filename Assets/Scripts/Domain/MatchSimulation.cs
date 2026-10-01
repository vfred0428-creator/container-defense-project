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
        // Match time is accumulated in double precision so boss timing matches across step sizes.
        private double clock;
        public float Elapsed { get { return (float)clock; } }
        public const double MaxWallet = 1e12;
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
                players[i].Gold = Math.Min(MaxWallet, players[i].Gold);
                players[i].Position = map.Spawn(i);
                purchaseRandom[i] = new Random(unchecked(seed * 397 + i * 31 + 17));
            }
            for (int i = 0; i < houses.Length; i++) houses[i] = new HouseState(map.HouseSpawns[i],rules.DoorHealth[0]);
            Players = Array.AsReadOnly(players); Houses = Array.AsReadOnly(houses);
            Boss = new BossState(rules.BossHealth); Phase = MatchPhase.Preparation; WinnerId = -1;
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
            h.OwnerId = playerId; p.HouseId = houseId; p.ClaimedAt = Elapsed;
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
            // Asleep means indoors in bed: the resident stays at the door, never on the roof (renderers hide sleepers).
            p.Position = h.Entry;
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
        // Read-only previews for the house panel: income and door health at a given level for this owner.
        public float IncomeAtLevel(int houseId,int bedLevel)
        {
            if (!ValidHouse(houseId) || bedLevel < 0 || bedLevel >= rules.BedIncome.Length) return 0;
            var h = houses[houseId]; return rules.BedIncome[bedLevel] * (h.OwnerId < 0 ? 1 : players[h.OwnerId].Character.IncomeMultiplier);
        }
        public float DoorHealthAtLevel(int houseId,int doorLevel)
        {
            if (!ValidHouse(houseId) || doorLevel < 0 || doorLevel >= rules.DoorHealth.Length) return 0;
            var h = houses[houseId]; return rules.DoorHealth[doorLevel] * (h.OwnerId < 0 ? 1 : players[h.OwnerId].Character.DoorMultiplier);
        }
        public float BossMoveSpeed { get { return rules.BossMoveSpeed; } }
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
            if (p.HouseId != houseId || h.OwnerId != playerId || !h.Occupied || h.IsBuilding || cost < 0 || !CanDebit(p,cost)) return false;
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
            clock += seconds;
            double combatStep = seconds;
            if (Phase == MatchPhase.Preparation && clock >= rules.PreparationSeconds)
            {
                // The boss clock starts exactly at the deadline, not at the first step boundary after it.
                combatStep = clock - rules.PreparationSeconds;
                Phase = MatchPhase.Combat; Boss.Phase = BossPhase.Selecting;
                foreach (PlayerState p in players) if (p.HouseId < 0) Eliminate(p,rules.PreparationSeconds);
                // Last one wins applies only when two or more houses entered combat; a solo house fights the boss alone.
                contestants = LivingHouses();
                Emit(MatchEventKind.CombatStarted);
            }
            if (Phase == MatchPhase.Combat && LivingHouses() == 0) { Finish(MatchEndReason.AllFallen); return; }
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
                    Credit(p,income); p.GoldEarned = Math.Min(MaxWallet,p.GoldEarned + income);
                }
            }
            // Same-step ordering is fixed: weapons resolve before boss attacks, so a boss killed
            // this step never lands a pending hit and a simultaneous finish is always a victory.
            TickWeapons(seconds);
            if (Finished) return;
            if (Phase != MatchPhase.Combat) return;
            TickBossRoute(combatStep);
            // Houses that fell during this step are judged together at its end.
            ResolveEnd();
        }

        private int contestants;
        public MatchEndReason EndReason { get; private set; }
        // The last player standing when the match ended that way, otherwise -1.
        public int WinnerId { get; private set; }
        private bool EndReached()
        {
            if (Phase != MatchPhase.Combat) return false;
            int living = LivingHouses();
            return living == 0 || (contestants >= 2 && living == 1);
        }
        private void ResolveEnd()
        {
            if (Finished || !EndReached()) return;
            Finish(LivingHouses() == 0 ? MatchEndReason.AllFallen : MatchEndReason.LastStanding);
        }

        // Wallet arithmetic saturates at MaxWallet and never accepts nonfinite or negative credits.
        private static bool CanCredit(PlayerState p,double amount)
        { return !double.IsNaN(amount) && !double.IsInfinity(amount) && amount >= 0 && p.Gold + amount <= MaxWallet; }
        private static void Credit(PlayerState p,double amount)
        { if (!double.IsNaN(amount) && !double.IsInfinity(amount) && amount > 0) p.Gold = Math.Min(MaxWallet,p.Gold + amount); }
        private static bool CanDebit(PlayerState p,double amount)
        { return !double.IsNaN(amount) && !double.IsInfinity(amount) && amount > 0 && p.Gold >= amount; }

        // Leaving eliminates the issuer; the house is vacated and never attacked again.
        public bool TryForfeit(int playerId)
        {
            PlayerState p; if (!ActivePlayer(playerId, out p)) return false;
            Eliminate(p,clock); ResolveEnd();
            return true;
        }
        public int LivingHouses()
        {
            int count = 0;
            foreach (HouseState h in houses) if (h.Occupied) count++;
            return count;
        }
        private void Eliminate(PlayerState p,double at)
        {
            if (p.Eliminated) return;
            p.Eliminated = true; p.Sleeping = false; p.SurvivalSeconds = Elapsed; p.EliminatedAt = (float)at; navigation[p.Id] = null;
            if (p.HouseId >= 0) {
                // The house is vacated: its weapons stop and the boss never targets it again.
                var h = houses[p.HouseId]; h.IsBuilding = false; h.BuildRemaining = 0; h.Vacated = true;
                foreach (var w in h.Weapons) if (w != null) w.BuildRemaining = 0;
            }
            Emit(MatchEventKind.Eliminated, p.Id, p.HouseId);
        }
        private void Finish(MatchEndReason reason)
        {
            if (Finished) return;
            EndReason = reason;
            Phase = reason == MatchEndReason.AllFallen ? MatchPhase.Defeat : MatchPhase.Victory;
            foreach (PlayerState p in players) p.Sleeping = false;
            if (reason == MatchEndReason.LastStanding) foreach (var h in houses) if (h.Occupied) WinnerId = h.OwnerId;
            AssignPlacements();
            // Cancel anything pending so no hit, telegraph or timer survives the result.
            Boss.Phase = reason == MatchEndReason.BossDefeated ? BossPhase.Dead : BossPhase.Waiting; Boss.TargetHouseId = -1; Boss.AttackTimer = 0;
            Boss.TelegraphRemaining = 0; Boss.RecoveryRemaining = 0; Boss.RouteHouses = new int[0]; Boss.RoutePath = new Point2[0];
            Emit(MatchEventKind.Finished);
        }
        // Standing players first, then later eliminations. When the boss is killed, survivors rank by
        // damage dealt to it, then house health left, then earliest claim. Other ties (houses falling
        // together) resolve by house number, and players who never claimed a house come last by player id.
        private void AssignPlacements()
        {
            var order = new List<PlayerState>(players);
            order.Sort((a,b) => {
                if (a.Eliminated != b.Eliminated) return a.Eliminated ? 1 : -1;
                if (a.Eliminated && a.EliminatedAt != b.EliminatedAt) return b.EliminatedAt.CompareTo(a.EliminatedAt);
                if (!a.Eliminated && EndReason == MatchEndReason.BossDefeated && a.HouseId >= 0 && b.HouseId >= 0) {
                    if (a.DamageDealt != b.DamageDealt) return b.DamageDealt.CompareTo(a.DamageDealt);
                    float hpA = houses[a.HouseId].Health, hpB = houses[b.HouseId].Health;
                    if (hpA != hpB) return hpB.CompareTo(hpA);
                    if (a.ClaimedAt != b.ClaimedAt) return a.ClaimedAt.CompareTo(b.ClaimedAt);
                }
                int ha = a.HouseId >= 0 ? a.HouseId : houses.Length + a.Id, hb = b.HouseId >= 0 ? b.HouseId : houses.Length + b.Id;
                return ha.CompareTo(hb);
            });
            for (int i = 0; i < order.Count; i++) order[i].Placement = i + 1;
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
