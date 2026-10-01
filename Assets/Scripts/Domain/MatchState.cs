using System;

namespace ContainerDefense.Domain
{
    public enum MatchPhase { Preparation, Combat, Victory, Defeat }
    public enum MatchEndReason { None, BossDefeated, LastStanding, AllFallen }
    public enum BossPhase { Waiting, Selecting, Telegraphing, Travelling, Attacking, Recovery, Dead }
    public enum UpgradeKind { Bed, Door, Weapon }
    public enum MatchEventKind { Claimed, Sleeping, Upgraded, Shot, DoorHit, Eliminated, CombatStarted, Finished, UpgradeStarted, Placed, Moved, Sold, Repaired, RoutePlanned }

    public struct Point2
    {
        public readonly float X;
        public readonly float Z;
        public Point2(float x, float z) { X = x; Z = z; }
        public float Distance(Point2 other)
        {
            float x = other.X - X, z = other.Z - Z;
            return (float)Math.Sqrt(x * x + z * z);
        }
        public Point2 Towards(Point2 target, float distance)
        {
            float length = Distance(target);
            if (length <= distance || length < 0.0001f) return target;
            return new Point2(X + (target.X - X) / length * distance, Z + (target.Z - Z) / length * distance);
        }
    }

    public sealed class PlayerState
    {
        public int Id { get; private set; }
        public string Name { get; private set; }
        public bool IsBot { get; private set; }
        public CharacterPassive Character { get; private set; }
        public Point2 Position { get; internal set; }
        public int HouseId { get; internal set; }
        public bool Eliminated { get; internal set; }
        public bool Sleeping { get; internal set; }
        public double Gold { get; internal set; }
        public double GoldEarned { get; internal set; }
        public float DamageDealt { get; internal set; }
        public float SurvivalSeconds { get; internal set; }
        public int UpgradesPurchased { get; internal set; }
        // Match time of elimination (-1 while alive) and final placement (1 = best, 0 until the match ends).
        public float EliminatedAt { get; internal set; }
        // Match time the house was claimed (-1 if never), the last tie-break between boss-kill survivors.
        public float ClaimedAt { get; internal set; }
        public int Placement { get; internal set; }
        internal PlayerState(int id, bool bot, float gold, CharacterDefinition character)
        {
            Id = id; IsBot = bot; Name = id == 0 ? "You" : character.Name;
            Character = new CharacterPassive(character);
            Gold = gold; HouseId = -1; EliminatedAt = -1; ClaimedAt = -1;
            Position = new Point2((id - 2.5f) * 0.6f, -7);
        }
    }

    public sealed class HouseState
    {
        public int Id { get; private set; }
        public int OwnerId { get; internal set; }
        public Point2 Entry { get; private set; }
        public float Health { get; internal set; }
        public float MaxHealth { get; internal set; }
        public int BedLevel { get; internal set; }
        public int DoorLevel { get; internal set; }
        public int WeaponLevel { get; internal set; }
        public bool IsBuilding { get; internal set; }
        public UpgradeKind BuildingKind { get; internal set; }
        public float BuildRemaining { get; internal set; }
        public float BuildDuration { get; internal set; }
        public double LastUpgradePaid { get; internal set; }
        public bool LastUpgradeDiscounted { get; internal set; }
        public bool Destroyed { get { return OwnerId >= 0 && Health <= 0; } }
        // A living, claimed house whose owner is still in the match. Only these are ever attacked.
        public bool Occupied { get { return OwnerId >= 0 && !Destroyed && !Vacated; } }
        public bool Vacated { get; internal set; }
        public int VisitsReceived { get; internal set; }
        public WeaponPlacement[] Weapons { get; private set; }
        public Point2 Center { get; private set; }
        public float ProtectedUntil { get; internal set; }
        public int AttacksReceived { get; internal set; }
        internal HouseState(HouseSpawnPoint spawn, float health)
        {
            Id = spawn.Id; OwnerId = -1; Health = MaxHealth = health;
            Entry = spawn.Entry; Center = spawn.Center; Weapons = new WeaponPlacement[3];
        }
    }

    public sealed class BossState
    {
        public BossPhase Phase { get; internal set; }
        public Point2 Position { get; internal set; }
        public float Health { get; internal set; }
        public float MaxHealth { get; private set; }
        public int TargetHouseId { get; internal set; }
        public int EntryNode { get; internal set; }
        public string RouteId { get; internal set; }
        public BossRouteKind RouteKind { get; internal set; }
        public int[] RouteHouses { get; internal set; }
        public Point2[] RoutePath { get; internal set; }
        public float TelegraphRemaining { get; internal set; }
        public float RecoveryRemaining { get; internal set; }
        public float SlowRemaining { get; internal set; }
        public int Wave { get; internal set; }
        internal float AttackTimer;
        internal BossState(float health)
        {
            Health = MaxHealth = health; Position = new Point2(0, -4);
            TargetHouseId = -1; Phase = BossPhase.Waiting; RouteHouses = new int[0]; RoutePath = new Point2[0]; Wave = 1;
        }
    }

    public struct MatchEvent
    {
        public readonly MatchEventKind Kind;
        public readonly int PlayerId;
        public readonly int HouseId;
        public readonly float Amount;
        public MatchEvent(MatchEventKind kind, int playerId = -1, int houseId = -1, float amount = 0)
        { Kind = kind; PlayerId = playerId; HouseId = houseId; Amount = amount; }
    }
}
