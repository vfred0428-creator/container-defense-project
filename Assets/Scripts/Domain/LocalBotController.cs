namespace ContainerDefense.Domain
{
    // Five independent competitors exercise the local six-player loop; no shared economy.
    // Bots act only through their bound PlayerCommands and see only the same scouting data as a player.
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
                var commands = match.CommandsFor(p.Id); var view = commands.Scout();
                if (p.HouseId < 0)
                {
                    HouseScout target = null;
                    for (int offset = 0; offset < view.Length; offset++)
                    {
                        var h = view[(p.Id * 2 + offset) % view.Length];
                        if (!h.Claimed) { target = h; break; }
                    }
                    if (target == null) continue;
                    var entry = match.Houses[target.HouseId].Entry;
                    commands.Navigate(entry,dt);
                    commands.Claim(target.HouseId);
                    if (p.HouseId < 0) continue;
                    view = commands.Scout();
                }
                if (!p.Sleeping) commands.ToggleSleep();
                thinkTimers[p.Id] -= dt;
                if (thinkTimers[p.Id] > 0) continue;
                thinkTimers[p.Id] = 0.8f + p.Id * 0.12f;
                var home = view[p.HouseId];
                if (home.Health < home.MaxHealth * .45f && commands.Repair()) continue;
                if (!home.Weapons[0].Present) { commands.Place(0,WeaponKind.Gatling); continue; }
                if (home.BedLevel >= 1 && !home.Weapons[1].Present) { commands.Place(1,(WeaponKind)(p.Id % 4)); continue; }
                if (home.BedLevel >= 2 && !home.Weapons[2].Present) { commands.Place(2,WeaponKind.Rocket); continue; }
                UpgradeKind choice;
                if (home.Health < home.MaxHealth * 0.65f && match.UpgradeCost(home.HouseId, UpgradeKind.Door) >= 0)
                    choice = UpgradeKind.Door;
                else if (home.BedLevel < 2) choice = UpgradeKind.Bed;
                else if (home.Weapons[0].Level < 3) { commands.Upgrade(0); continue; }
                else if (match.UpgradeCost(home.HouseId, UpgradeKind.Door) >= 0) choice = UpgradeKind.Door;
                else choice = UpgradeKind.Bed;
                commands.UpgradeHouse(choice);
            }
        }
    }
}
