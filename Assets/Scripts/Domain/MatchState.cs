using System;

namespace ContainerDefense.Domain
{
    public enum MatchPhase { Preparation, Combat, Victory, Defeat }
    public enum BossPhase { Waiting, Selecting, Travelling, Attacking, Dead }
    public enum UpgradeKind { Bed, Door, Weapon }
    public enum MatchEventKind { Claimed, Sleeping, Upgraded, Shot, DoorHit, Eliminated, CombatStarted, Finished, UpgradeStarted }

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
        internal PlayerState(int id, bool bot, float gold, CharacterDefinition character)
        {
            Id = id; IsBot = bot; Name = id == 0 ? "You" : character.Name;
            Character = new CharacterPassive(character);
            Gold = gold; HouseId = -1;
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
        internal float ShotTimer;
        internal HouseState(int id, float health)
        {
            Id = id; OwnerId = -1; Health = MaxHealth = health;
            Entry = new Point2((id - 2.5f) * 4.8f, 2.1f);
        }
    }

    public sealed class BossState
    {
        public BossPhase Phase { get; internal set; }
        public Point2 Position { get; internal set; }
        public float Health { get; internal set; }
        public float MaxHealth { get; private set; }
        public int TargetHouseId { get; internal set; }
        internal float AttackTimer;
        internal BossState(float health)
        {
            Health = MaxHealth = health; Position = new Point2(0, -4);
            TargetHouseId = -1; Phase = BossPhase.Waiting;
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
