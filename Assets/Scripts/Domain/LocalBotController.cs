namespace ContainerDefense.Domain
{
    // Five independent competitors exercise the local six-player loop; no shared economy.
    public sealed class LocalBotController
    {
        private readonly MatchSimulation match;
        private readonly float[] thinkTimers = new float[6];
        public LocalBotController(MatchSimulation simulation) { match = simulation; }
        public void Tick(float dt)
        {
            if (match.Finished) return;
            foreach (PlayerState p in match.Players)
            {
                if (!p.IsBot || p.Eliminated || match.Elapsed < 2 + p.Id * 1.25f) continue;
                if (p.HouseId < 0)
                {
                    HouseState target = null;
                    for (int offset = 0; offset < 6; offset++)
                    {
                        HouseState h = match.Houses[(p.Id + offset) % 6];
                        if (h.OwnerId < 0) { target = h; break; }
                    }
                    if (target == null) continue;
                    float x = target.Entry.X - p.Position.X, z = target.Entry.Z - p.Position.Z;
                    match.Move(p.Id, x, z, dt);
                    match.TryClaim(p.Id, target.Id);
                }
                if (p.HouseId < 0) continue;
                if (!p.Sleeping) match.TryToggleSleep(p.Id);
                thinkTimers[p.Id] -= dt;
                if (thinkTimers[p.Id] > 0) continue;
                thinkTimers[p.Id] = 0.8f + p.Id * 0.12f;
                HouseState home = match.Houses[p.HouseId];
                UpgradeKind choice;
                if (home.Health < home.MaxHealth * 0.65f && match.UpgradeCost(home.Id, UpgradeKind.Door) >= 0)
                    choice = UpgradeKind.Door;
                else if (home.BedLevel < 2) choice = UpgradeKind.Bed;
                else if (home.WeaponLevel < 4) choice = UpgradeKind.Weapon;
                else if (match.UpgradeCost(home.Id, UpgradeKind.Door) >= 0) choice = UpgradeKind.Door;
                else choice = UpgradeKind.Bed;
                match.TryUpgrade(p.Id, home.Id, choice);
            }
        }
    }
}
