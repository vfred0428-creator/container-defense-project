using System;

namespace ContainerDefense.Domain
{
    // The three stations of the house panel inside your room.
    public enum Station { Bed, Door, Weapons }
    // What a station card shows: Ready (tap buys), TooExpensive (red price), Building (this station is under
    // construction), Waiting (the house is busy with another station; a tap queues it), Queued, Max (nothing left),
    // Full (weapons: all three slots used, the card opens the existing weapon flow instead of buying).
    public enum CardState { Ready, TooExpensive, Building, Waiting, Queued, Max, Full }

    // Pure read-outs over the match for the Bed / Door / Weapons cards. No rules live here: costs, levels and
    // effects come straight from MatchSimulation and WeaponCatalog.
    public static class HouseStations
    {
        public static int Level(MatchSimulation m,int house,Station s)
        {
            var h = m.Houses[house];
            switch (s) {
                case Station.Bed: return h.BedLevel + 1;
                case Station.Door: return h.DoorLevel + 1;
                default: return Owned(m,house);
            }
        }
        public static int Owned(MatchSimulation m,int house) { int n = 0; foreach (var w in m.Houses[house].Weapons) if (w != null) n++; return n; }
        // Price of the next purchase at this station, or -1 when there is none.
        public static int Cost(MatchSimulation m,int house,Station s)
        {
            if (s == Station.Bed) return m.UpgradeCost(house,UpgradeKind.Bed);
            if (s == Station.Door) return m.UpgradeCost(house,UpgradeKind.Door);
            if (Owned(m,house) >= 3) return -1;
            int cheapest = int.MaxValue;
            foreach (WeaponKind k in Enum.GetValues(typeof(WeaponKind))) cheapest = Math.Min(cheapest,WeaponCatalog.Get(k).Cost);
            return cheapest;
        }
        public static CardState State(MatchSimulation m,int player,Station s,UpgradeQueue queue)
        {
            var p = m.Players[player]; int house = p.HouseId; var h = m.Houses[house];
            int cost = Cost(m,house,s);
            if (s == Station.Weapons) {
                if (cost < 0) return CardState.Full;
                if (h.IsBuilding) return CardState.Waiting;
                return p.Gold >= cost ? CardState.Ready : CardState.TooExpensive;
            }
            var kind = s == Station.Bed ? UpgradeKind.Bed : UpgradeKind.Door;
            if (h.IsBuilding && h.BuildingKind == kind) return CardState.Building;
            if (cost < 0) return CardState.Max;
            if (queue != null && queue.Queued == s) return CardState.Queued;
            if (h.IsBuilding) return CardState.Waiting;
            return p.Gold >= cost ? CardState.Ready : CardState.TooExpensive;
        }
        // One line under the card title: what the next level adds.
        public static string Effect(MatchSimulation m,int house,Station s)
        {
            var h = m.Houses[house];
            switch (s) {
                case Station.Bed:
                    if (m.UpgradeCost(house,UpgradeKind.Bed) < 0) return "Income " + m.Income(house).ToString("0.#") + " gold/s";
                    return "Income +" + (m.IncomeAtLevel(house,h.BedLevel + 1) - m.IncomeAtLevel(house,h.BedLevel)).ToString("0.#") + " gold/s";
                case Station.Door:
                    if (m.UpgradeCost(house,UpgradeKind.Door) < 0) return "House HP " + Math.Round(h.MaxHealth);
                    return "House HP +" + Math.Round(m.DoorHealthAtLevel(house,h.DoorLevel + 1) - m.DoorHealthAtLevel(house,h.DoorLevel));
                default:
                    int n = Owned(m,house); return n >= 3 ? "3 of 3 built" : n + " of 3 built";
            }
        }
        // Shown while the card is held: the next level's totals.
        public static string Preview(MatchSimulation m,int house,Station s)
        {
            var h = m.Houses[house];
            switch (s) {
                case Station.Bed:
                    return m.UpgradeCost(house,UpgradeKind.Bed) < 0 ? "Bed is at max level" :
                        "Lv " + (h.BedLevel + 2) + ": " + m.IncomeAtLevel(house,h.BedLevel + 1).ToString("0.#") + " gold/s while asleep";
                case Station.Door:
                    return m.UpgradeCost(house,UpgradeKind.Door) < 0 ? "Door is at max level" :
                        "Lv " + (h.DoorLevel + 2) + ": " + Math.Round(m.DoorHealthAtLevel(house,h.DoorLevel + 1)) + " house HP";
                default:
                    return Owned(m,house) >= 3 ? "All slots used. Upgrade or move them in the yard." : "Pick a weapon, then a pad in your yard";
            }
        }
    }

    // A short window after any accepted purchase tap in which further taps are ignored (no double buys).
    public sealed class PurchaseGuard
    {
        public const float Window = .3f;
        private float last = float.NegativeInfinity;
        public bool TryPass(float now)
        {
            if (now - last < Window) return false;
            last = now; return true;
        }
    }

    // One queued bed or door upgrade: tapping while the house is busy queues it, and it is bought as soon as
    // the house is free and the gold is there. Prices and rules are the simulation's.
    public sealed class UpgradeQueue
    {
        private Station queued = Station.Weapons;
        private bool has;
        public Station? Queued { get { if (has) return queued; return null; } }
        public void Clear() { has = false; }
        // Buys now when possible, otherwise queues. Returns true when it bought.
        public bool Request(MatchSimulation m,int player,Station s)
        {
            if (s == Station.Weapons) return false;
            var p = m.Players[player]; if (p.HouseId < 0) return false;
            var kind = s == Station.Bed ? UpgradeKind.Bed : UpgradeKind.Door;
            if (m.CommandsFor(player).UpgradeHouse(kind)) { if (has && queued == s) has = false; return true; }
            if (m.UpgradeCost(p.HouseId,kind) < 0) return false;
            if (has && queued == s) { has = false; return false; }   // tapping a queued card cancels it
            queued = s; has = true; return false;
        }
        // Call every frame: fires the queued upgrade once possible. Returns true on the frame it bought.
        public bool Tick(MatchSimulation m,int player)
        {
            if (!has) return false;
            var p = m.Players[player];
            if (p.Eliminated || p.HouseId < 0 || m.Finished) { has = false; return false; }
            var kind = queued == Station.Bed ? UpgradeKind.Bed : UpgradeKind.Door;
            if (m.UpgradeCost(p.HouseId,kind) < 0) { has = false; return false; }
            if (!m.CommandsFor(player).UpgradeHouse(kind)) return false;
            has = false; return true;
        }
    }

    // Inside your room or out on the board. You choose; the boss attacking your house forces the board view,
    // and the room comes back one second after the attack ends. Also drives the warning banner and the window.
    public sealed class InteriorView
    {
        public const float ReturnDelay = 1f, BannerSeconds = 5f, WindowSeconds = 8f;
        private readonly MatchSimulation match;
        private readonly int local;
        private float returnTimer;
        public bool Requested { get; private set; }
        public bool ForcedOut { get; private set; }
        public bool Available { get; private set; }
        public bool Inside { get { return Requested && Available && !ForcedOut; } }
        // Seconds until the boss reaches your house (-1 when it is not coming), the banner and the window boss.
        public float BossEta { get; private set; }
        public bool BossIncoming { get { return BossEta >= 0 && BossEta <= BannerSeconds; } }
        public bool BossInWindow { get { return BossEta >= 0 && BossEta <= WindowSeconds; } }
        public InteriorView(MatchSimulation simulation,int localPlayer = 0) { match = simulation; local = localPlayer; BossEta = -1; }
        public void Enter() { Requested = true; }
        public void Exit() { Requested = false; }
        public void Toggle() { Requested = !Requested; }
        public void Update(bool viewingOwnBase,float dt)
        {
            var p = match.Players[local]; int house = p.HouseId;
            bool alive = house >= 0 && !p.Eliminated && !match.Finished;
            if (!alive) Requested = false;
            Available = alive && viewingOwnBase;
            var b = match.Boss; bool mine = alive && b.TargetHouseId == house;
            if (mine && b.Phase == BossPhase.Attacking) { ForcedOut = true; returnTimer = ReturnDelay; }
            else if (ForcedOut) { returnTimer -= dt; if (returnTimer <= 0) ForcedOut = false; }
            BossEta = -1;
            if (mine && (b.Phase == BossPhase.Telegraphing || b.Phase == BossPhase.Travelling)) {
                float travel = b.Position.Distance(match.Houses[house].Entry) / Math.Max(.01f,match.BossMoveSpeed);
                BossEta = (b.Phase == BossPhase.Telegraphing ? b.TelegraphRemaining : 0) + travel;
            }
        }
    }
}
