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
    public sealed partial class MatchSimulation
    {
        public Point2 WeaponPoint(int houseId,int slot)
        { var h = houses[houseId]; return new Point2(h.Center.X + (slot - 1) * 1.9f,h.Center.Z + .65f); }
        public bool TryPlaceWeapon(int playerId,int houseId,int slot,WeaponKind kind)
        {
            PlayerState p; HouseState h; var d = WeaponCatalog.Get(kind);
            if (!OwnedHome(playerId,houseId,out p,out h) || slot < 0 || slot >= 3 || d == null || h.Weapons[slot] != null || h.IsBuilding || p.Gold < d.Cost) return false;
            p.Gold -= d.Cost; h.Weapons[slot] = new WeaponPlacement { Kind = kind,Level = 1,Invested = d.Cost,BuildRemaining = rules.UpgradeSeconds / p.Character.BuildMultiplier };
            Emit(MatchEventKind.Placed,playerId,houseId,slot); return true;
        }
        public int WeaponUpgradeCost(int houseId,int slot)
        {
            if (!ValidHouse(houseId) || slot < 0 || slot >= 3) return -1;
            var w = houses[houseId].Weapons[slot]; if (w == null || w.Level >= WeaponCatalog.Get(w.Kind).MaxLevel) return -1;
            return WeaponCatalog.Get(w.Kind).Cost * (w.Level + 1);
        }
        public bool TryUpgradeWeapon(int playerId,int houseId,int slot)
        {
            PlayerState p; HouseState h; int cost = WeaponUpgradeCost(houseId,slot);
            if (!OwnedHome(playerId,houseId,out p,out h) || cost < 0 || h.IsBuilding || h.Weapons[slot].Building || p.Gold < cost) return false;
            var w = h.Weapons[slot]; bool lucky = p.Character.DiscountChance > 0 && purchaseRandom[playerId].NextDouble() < p.Character.DiscountChance;
            double paid = lucky ? cost * .5 : cost; p.Gold -= paid; p.UpgradesPurchased++;
            w.Level++; w.Invested += paid; w.BuildRemaining = rules.UpgradeSeconds / p.Character.BuildMultiplier;
            h.WeaponLevel = Math.Max(h.WeaponLevel,w.Level - 1);
            h.LastUpgradePaid = paid; h.LastUpgradeDiscounted = lucky;
            Emit(MatchEventKind.Upgraded,playerId,houseId,(float)paid); return true;
        }
        public bool TryMoveWeapon(int playerId,int houseId,int from,int to)
        {
            PlayerState p; HouseState h;
            if (!OwnedHome(playerId,houseId,out p,out h) || from < 0 || from >= 3 || to < 0 || to >= 3 || from == to || h.Weapons[from] == null || h.Weapons[to] != null || h.Weapons[from].Building) return false;
            h.Weapons[to] = h.Weapons[from]; h.Weapons[from] = null; Emit(MatchEventKind.Moved,playerId,houseId,to); return true;
        }
        public bool TrySellWeapon(int playerId,int houseId,int slot)
        {
            PlayerState p; HouseState h;
            if (!OwnedHome(playerId,houseId,out p,out h) || slot < 0 || slot >= 3 || h.Weapons[slot] == null || h.Weapons[slot].Building) return false;
            p.Gold += Math.Floor(h.Weapons[slot].Invested * .5); h.Weapons[slot] = null; Emit(MatchEventKind.Sold,playerId,houseId,slot); return true;
        }
        public const int RepairCost = 40;
        public bool TryRepair(int playerId,int houseId)
        {
            PlayerState p; HouseState h;
            if (!OwnedHome(playerId,houseId,out p,out h) || h.Health >= h.MaxHealth || h.IsBuilding || p.Gold < RepairCost) return false;
            p.Gold -= RepairCost; h.Health = Math.Min(h.MaxHealth,h.Health + 140); Emit(MatchEventKind.Repaired,playerId,houseId,140); return true;
        }
        private bool OwnedHome(int playerId,int houseId,out PlayerState p,out HouseState h)
        {
            h = ValidHouse(houseId) ? houses[houseId] : null;
            return ActivePlayer(playerId,out p) && h != null && h.OwnerId == playerId && p.HouseId == houseId && !h.Destroyed;
        }
        private void TickWeapons(float dt)
        {
            foreach (var h in houses) {
                if (h.OwnerId < 0 || h.Destroyed) continue;
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
                    if (Boss.Health <= 0) { Boss.Phase = BossPhase.Dead; Finish(true); return; }
                }
            }
        }
    }
}
