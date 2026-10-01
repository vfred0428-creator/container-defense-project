namespace ContainerDefense.Domain
{
    public enum ViewMode { Neighborhood, Base, FullMap }

    // What the local player is looking at, TFT style: the whole neighbourhood during the claim race,
    // then their own base; they can scout another living player's base (read-only) or open the full map.
    // Pure logic over read-only match state, so it can be tested without Unity.
    public sealed class MatchView
    {
        private readonly MatchSimulation match;
        private readonly int local;
        public ViewMode Mode { get; private set; }
        public int ViewedPlayer { get; private set; }
        public MatchView(MatchSimulation simulation,int localPlayer = 0)
        { match = simulation; local = localPlayer; Mode = ViewMode.Neighborhood; ViewedPlayer = localPlayer; }

        private PlayerState Local { get { return match.Players[local]; } }
        private bool Viewable(int id) { return id >= 0 && id < match.Players.Count && !match.Players[id].Eliminated && match.Players[id].HouseId >= 0; }
        public bool ViewingOwnBase { get { return Mode == ViewMode.Base && ViewedPlayer == local; } }
        public bool ViewingOther { get { return Mode == ViewMode.Base && ViewedPlayer != local; } }
        public bool Spectating { get { return Local.Eliminated; } }
        // Build and house commands are allowed only while looking at your own living base.
        public bool CanCommand { get { return ViewingOwnBase && !Local.Eliminated && !match.Finished; } }

        // Called every frame: claiming moves the camera to your base; losing a viewed player moves on.
        public void Refresh()
        {
            if (Mode == ViewMode.Neighborhood && Local.HouseId >= 0 && !Local.Eliminated) { Mode = ViewMode.Base; ViewedPlayer = local; }
            if (Mode == ViewMode.Neighborhood && match.Phase != MatchPhase.Preparation && Local.HouseId < 0) Spectate(0);
            if (Mode == ViewMode.Base && !Viewable(ViewedPlayer)) {
                if (!Local.Eliminated && Local.HouseId >= 0) ViewedPlayer = local; else Spectate(0);
            }
        }
        // Scouts a living player's base. Tapping yourself returns home.
        public bool View(int player)
        {
            if (player == local && !Local.Eliminated && Local.HouseId >= 0) { ReturnHome(); return true; }
            if (!Viewable(player)) return false;
            Mode = ViewMode.Base; ViewedPlayer = player; return true;
        }
        public void ReturnHome()
        {
            if (!Local.Eliminated && Local.HouseId >= 0) { Mode = ViewMode.Base; ViewedPlayer = local; return; }
            if (Local.HouseId < 0 && match.Phase == MatchPhase.Preparation && !Local.Eliminated) { Mode = ViewMode.Neighborhood; ViewedPlayer = local; return; }
            Spectate(0);
        }
        public void OpenFullMap() { Mode = ViewMode.FullMap; }
        public void CloseFullMap()
        {
            if (Mode != ViewMode.FullMap) return;
            Mode = ViewMode.Base; if (!Viewable(ViewedPlayer)) ReturnHome();
        }
        // Tapping a house on the full map jumps to that base when someone living owns it.
        public bool SelectHouse(int houseId)
        {
            if (houseId < 0 || houseId >= match.Houses.Count) return false;
            int owner = match.Houses[houseId].OwnerId;
            if (!Viewable(owner)) return false;
            Mode = ViewMode.Base; ViewedPlayer = owner; return true;
        }
        // Next or previous living base, in house order.
        public void Spectate(int direction)
        {
            int start = ViewedPlayer;
            for (int i = direction == 0 ? 0 : 1; i <= match.Players.Count; i++) {
                int id = ((start + (direction < 0 ? -i : i)) % match.Players.Count + match.Players.Count) % match.Players.Count;
                if (Viewable(id) && (id != local || !Local.Eliminated)) { Mode = ViewMode.Base; ViewedPlayer = id; return; }
            }
            Mode = ViewMode.Neighborhood;
        }
        // The boss is lining up the local player's house while they look elsewhere.
        public bool HomeUnderThreat
        {
            get {
                var p = Local; if (p.Eliminated || p.HouseId < 0 || ViewingOwnBase) return false;
                var b = match.Boss.Phase;
                return (b == BossPhase.Telegraphing || b == BossPhase.Travelling || b == BossPhase.Attacking) && match.Boss.TargetHouseId == p.HouseId;
            }
        }
    }
}
