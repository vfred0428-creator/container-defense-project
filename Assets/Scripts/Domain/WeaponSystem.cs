using System;

namespace ContainerDefense.Domain
{
    public enum WeaponKind { Gatling, Cannon, Slow, Rocket }
    [Serializable] public sealed class WeaponDefinition
    {
        public WeaponKind Id; public string Name; public int Cost,MaxLevel = 3; public float Damage,FireInterval,Range,SlowSeconds;
        public int SpriteIndex;
    }
    public sealed class WeaponPlacement
    {
        public WeaponKind Kind { get; internal set; }
        public int Level { get; internal set; }
        public double Invested { get; internal set; }
        public float BuildRemaining { get; internal set; }
        public float ShotCooldown { get; internal set; }
        // Build pad on your board (0..7). Display only: damage, range and targeting still use the slot.
        public int Spot { get; internal set; }
        public bool Building { get { return BuildRemaining > 0; } }
    }
    public static class WeaponCatalog
    {
        public static WeaponDefinition Get(WeaponKind kind)
        {
            switch (kind) {
                case WeaponKind.Gatling: return new WeaponDefinition { Id = kind,Name = "Gatling",Cost = 35,Damage = 11,FireInterval = .65f,Range = 15,SpriteIndex = 6 };
                case WeaponKind.Cannon: return new WeaponDefinition { Id = kind,Name = "Cannon",Cost = 50,Damage = 42,FireInterval = 2.1f,Range = 19,SpriteIndex = 7 };
                case WeaponKind.Slow: return new WeaponDefinition { Id = kind,Name = "Slow turret",Cost = 45,Damage = 7,FireInterval = 1.6f,Range = 16,SlowSeconds = .8f,SpriteIndex = 8 };
                case WeaponKind.Rocket: return new WeaponDefinition { Id = kind,Name = "Rocket",Cost = 70,Damage = 66,FireInterval = 3.2f,Range = 23,SpriteIndex = 9 };
                default: return null;
            }
        }
    }

    // Read-only scouting data: value copies only, no live state objects, and no exact gold for other players.
    public struct WeaponScout
    {
        public readonly bool Present; public readonly WeaponKind Kind; public readonly int Level; public readonly bool Building; public readonly int Spot;
        internal WeaponScout(WeaponPlacement w) { Present = w != null; Kind = w != null ? w.Kind : WeaponKind.Gatling; Level = w != null ? w.Level : 0; Building = w != null && w.Building; Spot = w != null ? w.Spot : -1; }
    }
    public enum WealthBand { Unknown, Low, Medium, High }
    public sealed class HouseScout
    {
        public int HouseId { get; internal set; }
        public int OwnerId { get; internal set; }
        public string OwnerName { get; internal set; }
        public bool Claimed { get; internal set; }
        public bool Occupied { get; internal set; }
        public bool Destroyed { get; internal set; }
        public bool OwnerSleeping { get; internal set; }
        public float Health { get; internal set; }
        public float MaxHealth { get; internal set; }
        public int BedLevel { get; internal set; }
        public int DoorLevel { get; internal set; }
        public bool Building { get; internal set; }
        public UpgradeKind BuildingKind { get; internal set; }
        public float BuildRemaining { get; internal set; }
        public WeaponScout[] Weapons { get; internal set; }
        public float ProtectedFor { get; internal set; }
        public bool OnBossRoute { get; internal set; }
        public bool BossTarget { get; internal set; }
        public bool IsViewer { get; internal set; }
        // Exact gold is present only on the viewer's own house (-1 elsewhere); others show a coarse band.
        public double ViewerGold { get; internal set; }
        public WealthBand Wealth { get; internal set; }
    }

    // A command handle bound by the simulation to one issuer. Callers never pass a player id.
    public sealed class PlayerCommands
    {
        private readonly MatchSimulation match;
        public int PlayerId { get; private set; }
        internal PlayerCommands(MatchSimulation simulation,int playerId) { match = simulation; PlayerId = playerId; }
        private int Home { get { return match.Players[PlayerId].HouseId; } }
        public bool Claim(int houseId) { return match.TryClaim(PlayerId,houseId); }
        public bool ToggleSleep() { return match.TryToggleSleep(PlayerId); }
        public void Move(float x,float z,float dt) { match.Move(PlayerId,x,z,dt); }
        public void Navigate(Point2 goal,float dt) { match.Navigate(PlayerId,goal,dt); }
        public bool UpgradeHouse(UpgradeKind kind) { return match.TryUpgrade(PlayerId,Home,kind); }
        public bool Place(int slot,WeaponKind kind,int spot = -1) { return match.TryPlaceWeapon(PlayerId,Home,slot,kind,spot); }
        public bool MoveToSpot(int slot,int spot) { return match.TryMoveWeaponSpot(PlayerId,Home,slot,spot); }
        public bool Upgrade(int slot) { return match.TryUpgradeWeapon(PlayerId,Home,slot); }
        public bool MoveWeapon(int from,int to) { return match.TryMoveWeapon(PlayerId,Home,from,to); }
        public bool Sell(int slot) { return match.TrySellWeapon(PlayerId,Home,slot); }
        public bool Repair() { return match.TryRepair(PlayerId,Home); }
        public bool Forfeit() { return match.TryForfeit(PlayerId); }
        public HouseScout[] Scout() { return match.Scout(PlayerId); }
    }

    public sealed partial class MatchSimulation
    {
        public const int RepairCost = 40, RepairAmount = 140;
        private PlayerCommands[] commands;
        public PlayerCommands CommandsFor(int playerId)
        {
            if (playerId < 0 || playerId >= players.Length) throw new ArgumentOutOfRangeException("playerId");
            if (commands == null) { commands = new PlayerCommands[players.Length]; for (int i = 0; i < commands.Length; i++) commands[i] = new PlayerCommands(this,i); }
            return commands[playerId];
        }
        public HouseScout[] Scout(int viewerId)
        {
            var result = new HouseScout[houses.Length];
            foreach (var h in houses) {
                var owner = h.OwnerId >= 0 ? players[h.OwnerId] : null; bool viewer = owner != null && owner.Id == viewerId;
                result[h.Id] = new HouseScout {
                    HouseId = h.Id,OwnerId = h.OwnerId,OwnerName = owner != null ? owner.Name : "",Claimed = owner != null,Occupied = h.Occupied,Destroyed = h.Destroyed,
                    OwnerSleeping = owner != null && owner.Sleeping,Health = h.Health,MaxHealth = h.MaxHealth,BedLevel = h.BedLevel,DoorLevel = h.DoorLevel,Building = h.IsBuilding,BuildingKind = h.BuildingKind,BuildRemaining = h.BuildRemaining,
                    Weapons = Array.ConvertAll(h.Weapons,w => new WeaponScout(w)),ProtectedFor = (float)Math.Max(0,h.ProtectedUntil - clock),
                    OnBossRoute = Array.IndexOf(Boss.RouteHouses,h.Id) >= 0,BossTarget = Boss.TargetHouseId == h.Id,IsViewer = viewer,
                    ViewerGold = viewer ? owner.Gold : -1,Wealth = owner == null ? WealthBand.Unknown : owner.Gold < 100 ? WealthBand.Low : owner.Gold < 400 ? WealthBand.Medium : WealthBand.High
                };
            }
            return result;
        }
        public Point2 WeaponPoint(int houseId,int slot)
        { var h = houses[houseId]; return new Point2(h.Center.X + (slot - 1) * 1.9f,h.Center.Z + .65f); }
        public bool TryPlaceWeapon(int playerId,int houseId,int slot,WeaponKind kind) { return TryPlaceWeapon(playerId,houseId,slot,kind,-1); }
        // spot -1 picks the first free pad; an explicit spot must be a free pad on your own board.
        public bool TryPlaceWeapon(int playerId,int houseId,int slot,WeaponKind kind,int spot)
        {
            PlayerState p; HouseState h; var d = WeaponCatalog.Get(kind);
            if (!OwnedHome(playerId,houseId,out p,out h) || !ValidSlot(slot) || d == null || h.Weapons[slot] != null || h.IsBuilding || !CanDebit(p,d.Cost)) return false;
            if (spot == -1) spot = FreeSpot(h); else if (!SpotFree(h,spot,-1)) return false;
            p.Gold -= d.Cost; h.Weapons[slot] = new WeaponPlacement { Kind = kind,Level = 1,Invested = d.Cost,Spot = spot,BuildRemaining = rules.UpgradeSeconds / p.Character.BuildMultiplier };
            Emit(MatchEventKind.Placed,playerId,houseId,slot); return true;
        }
        public int WeaponUpgradeCost(int houseId,int slot)
        {
            if (!ValidHouse(houseId) || !ValidSlot(slot)) return -1;
            var w = houses[houseId].Weapons[slot]; if (w == null || w.Level >= WeaponCatalog.Get(w.Kind).MaxLevel) return -1;
            return WeaponCatalog.Get(w.Kind).Cost * (w.Level + 1);
        }
        public bool TryUpgradeWeapon(int playerId,int houseId,int slot)
        {
            PlayerState p; HouseState h; int cost = WeaponUpgradeCost(houseId,slot);
            if (!OwnedHome(playerId,houseId,out p,out h) || cost < 0 || h.IsBuilding || h.Weapons[slot].Building || !CanDebit(p,cost)) return false;
            var w = h.Weapons[slot]; bool lucky = p.Character.DiscountChance > 0 && purchaseRandom[playerId].NextDouble() < p.Character.DiscountChance;
            double paid = lucky ? cost * .5 : cost; p.Gold -= paid; p.UpgradesPurchased++;
            w.Level++; w.Invested += paid; w.BuildRemaining = rules.UpgradeSeconds / p.Character.BuildMultiplier;
            h.WeaponLevel = Math.Max(h.WeaponLevel,w.Level - 1);
            h.LastUpgradePaid = paid; h.LastUpgradeDiscounted = lucky;
            Emit(MatchEventKind.Upgraded,playerId,houseId,(float)paid); return true;
        }
        public bool TryMoveWeaponSpot(int playerId,int houseId,int slot,int spot)
        {
            PlayerState p; HouseState h;
            if (!OwnedHome(playerId,houseId,out p,out h) || !ValidSlot(slot) || h.Weapons[slot] == null || h.Weapons[slot].Building || !SpotFree(h,spot,slot)) return false;
            h.Weapons[slot].Spot = spot; Emit(MatchEventKind.Moved,playerId,houseId,slot); return true;
        }
        private static bool SpotFree(HouseState h,int spot,int ignoreSlot)
        {
            if (spot < 0 || spot >= YardLayout.PadCount) return false;
            for (int s = 0; s < 3; s++) if (s != ignoreSlot && h.Weapons[s] != null && h.Weapons[s].Spot == spot) return false;
            return true;
        }
        private static int FreeSpot(HouseState h) { for (int i = 0; i < YardLayout.PadCount; i++) if (SpotFree(h,i,-1)) return i; return 0; }
        // Moves stay inside the issuer's own house and keep level, investment and cooldown.
        public bool TryMoveWeapon(int playerId,int houseId,int from,int to)
        {
            PlayerState p; HouseState h;
            if (!OwnedHome(playerId,houseId,out p,out h) || !ValidSlot(from) || !ValidSlot(to) || from == to || h.Weapons[from] == null || h.Weapons[to] != null || h.Weapons[from].Building) return false;
            h.Weapons[to] = h.Weapons[from]; h.Weapons[from] = null; Emit(MatchEventKind.Moved,playerId,houseId,to); return true;
        }
        public double SellValue(int houseId,int slot)
        { var w = ValidHouse(houseId) && ValidSlot(slot) ? houses[houseId].Weapons[slot] : null; return w == null ? -1 : Math.Floor(w.Invested * .5); }
        public bool TrySellWeapon(int playerId,int houseId,int slot)
        {
            PlayerState p; HouseState h;
            if (!OwnedHome(playerId,houseId,out p,out h) || !ValidSlot(slot) || h.Weapons[slot] == null || h.Weapons[slot].Building) return false;
            // Half of what was actually paid, so a refund never exceeds cost.
            double refund = SellValue(houseId,slot);
            if (!CanCredit(p,refund)) return false;
            p.Gold += refund; h.Weapons[slot] = null; Emit(MatchEventKind.Sold,playerId,houseId,slot); return true;
        }
        public bool TryRepair(int playerId,int houseId)
        {
            PlayerState p; HouseState h;
            if (!OwnedHome(playerId,houseId,out p,out h) || h.Health >= h.MaxHealth || h.IsBuilding || !CanDebit(p,RepairCost)) return false;
            p.Gold -= RepairCost; h.Health = Math.Min(h.MaxHealth,h.Health + RepairAmount); Emit(MatchEventKind.Repaired,playerId,houseId,RepairAmount); return true;
        }
        private static bool ValidSlot(int slot) { return slot >= 0 && slot < 3; }
        private bool OwnedHome(int playerId,int houseId,out PlayerState p,out HouseState h)
        {
            h = ValidHouse(houseId) ? houses[houseId] : null;
            return ActivePlayer(playerId,out p) && h != null && h.OwnerId == playerId && p.HouseId == houseId && h.Occupied;
        }
        private void TickWeapons(float dt)
        {
            foreach (var h in houses) {
                // Weapons of destroyed or vacated houses are inactive.
                if (!h.Occupied) continue;
                for (int slot = 0; slot < 3; slot++) {
                    var w = h.Weapons[slot]; if (w == null) continue;
                    if (w.Building) { w.BuildRemaining = Math.Max(0,w.BuildRemaining - dt); continue; }
                    w.ShotCooldown = Math.Max(0,w.ShotCooldown - dt);
                    if (Phase != MatchPhase.Combat || w.ShotCooldown > 0) continue;
                    var d = WeaponCatalog.Get(w.Kind); if (WeaponPoint(h.Id,slot).Distance(Boss.Position) > d.Range) continue;
                    w.ShotCooldown = d.FireInterval;
                    float damage = Math.Min(Boss.Health,d.Damage * (1 + (w.Level - 1) * .7f) * players[h.OwnerId].Character.DamageMultiplier);
                    Boss.Health -= damage; players[h.OwnerId].DamageDealt += damage;
                    if (d.SlowSeconds > 0) Boss.SlowRemaining = Math.Max(Boss.SlowRemaining,d.SlowSeconds);
                    Emit(MatchEventKind.Shot,h.OwnerId,h.Id,slot);
                    if (Boss.Health <= 0) { Finish(MatchEndReason.BossDefeated); return; }
                }
            }
        }
    }
}
