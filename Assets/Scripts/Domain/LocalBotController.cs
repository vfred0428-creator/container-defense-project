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
                    for (int offset = 0; offset < match.Houses.Count; offset++)
                    {
                        HouseState h = match.Houses[(p.Id * 2 + offset) % match.Houses.Count];
                        if (h.OwnerId < 0) { target = h; break; }
                    }
                    if (target == null) continue;
                    match.Navigate(p.Id,target.Entry,dt);
                    match.TryClaim(p.Id, target.Id);
                }
                if (p.HouseId < 0) continue;
                if (!p.Sleeping) match.TryToggleSleep(p.Id);
                thinkTimers[p.Id] -= dt;
                if (thinkTimers[p.Id] > 0) continue;
                thinkTimers[p.Id] = 0.8f + p.Id * 0.12f;
                HouseState home = match.Houses[p.HouseId];
                if (home.Health < home.MaxHealth * .45f && match.TryRepair(p.Id,home.Id)) continue;
                if (home.Weapons[0] == null) { match.TryPlaceWeapon(p.Id,home.Id,0,WeaponKind.Gatling); continue; }
                if (home.BedLevel >= 1 && home.Weapons[1] == null) { match.TryPlaceWeapon(p.Id,home.Id,1,(WeaponKind)(p.Id % 4)); continue; }
                if (home.BedLevel >= 2 && home.Weapons[2] == null) { match.TryPlaceWeapon(p.Id,home.Id,2,WeaponKind.Rocket); continue; }
                UpgradeKind choice;
                if (home.Health < home.MaxHealth * 0.65f && match.UpgradeCost(home.Id, UpgradeKind.Door) >= 0)
                    choice = UpgradeKind.Door;
                else if (home.BedLevel < 2) choice = UpgradeKind.Bed;
                else if (home.Weapons[0].Level < 3) { match.TryUpgradeWeapon(p.Id,home.Id,0); continue; }
                else if (match.UpgradeCost(home.Id, UpgradeKind.Door) >= 0) choice = UpgradeKind.Door;
                else choice = UpgradeKind.Bed;
                match.TryUpgrade(p.Id, home.Id, choice);
            }
        }
    }
}
