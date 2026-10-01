#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    // Explicit --smoke-test opt-in, excluded from release builds.
    public sealed class MatchSmokeDriver : MonoBehaviour
    {
        private GameSession session;
        private string output;
        private int errors;
        public void Initialize(GameSession game) { session = game; }
        private void OnEnable() { Application.logMessageReceived += OnLog; }
        private void OnDisable() { Application.logMessageReceived -= OnLog; }
        private void OnLog(string condition, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert ||
                (type == LogType.Warning && condition.Contains("Look rotation"))) errors++;
        }

        private IEnumerator Start()
        {
            output = Path.GetFullPath(Path.Combine(Application.dataPath,"../../TestResults/Playtest"));
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args,"--smoke-output");
            if (index >= 0 && index + 1 < args.Length) output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(2);
            bool ranked = Array.IndexOf(args,"--ranked-smoke-test") >= 0;
            if (Array.IndexOf(args,"--smoke-reload-only") >= 0)
            {
                if (ranked && (session.Account.Rank.MatchesPlayed != 2 || session.Account.Social.Profile.Matches != 2)) { Fail("Rank or profile statistics did not survive restart."); yield break; }
                if (session.Account.Selected != CharacterId.Lumi || session.Account.Level < 3 ||
                    !session.Account.IsUnlocked(CharacterId.Lumi) || session.Account.TotalXp <= 0 || session.SaveDirty)
                { Fail("Account did not survive a process restart."); yield break; }
                ScreenCapture.CaptureScreenshot(Path.Combine(output,"08-reloaded-account.png"));
                yield return new WaitForSecondsRealtime(.5f);
                File.WriteAllText(Path.Combine(output,"reload-result.txt"),errors == 0 ?
                    "PASS: a new process restored account XP, level, permanent Lumi unlock and selected character. No runtime errors." : "FAIL: runtime errors = " + errors);
                Application.Quit(errors == 0 ? 0 : 1);
                yield break;
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"01-title.png"));
            yield return new WaitForSecondsRealtime(0.5f);
            long initialXp = session.Account.TotalXp;
            if (session.SelectCharacter(CharacterId.Yume) && !session.Account.IsUnlocked(CharacterId.Yume))
            { Fail("Locked character selected."); yield break; }
            if (ranked)
            {
                var hud = session.GetComponent<MatchHud>(); hud.OpenRanked(); yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output,"00-ranked.png")); yield return new WaitForSecondsRealtime(.5f);
                foreach (LeaderboardKind board in Enum.GetValues(typeof(LeaderboardKind)))
                {
                    hud.OpenLeaderboards(board); yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(output,"00-board-" + board + ".png")); yield return new WaitForSecondsRealtime(.5f);
                }
                hud.CloseRanked(); session.PlayRanked();
            }
            else session.Play();
            var match = session.Match;
            var matchHud = session.GetComponent<MatchHud>();
            yield return new WaitForSecondsRealtime(.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"01a-twelve-house-overview.png"));
            session.WalkToHouse(0);
            float deadline = Time.realtimeSinceStartup + 20;
            while (match.Players[0].HouseId < 0 && Time.realtimeSinceStartup < deadline) yield return null;
            if (match.Players[0].HouseId != 0 || !match.Players[0].Sleeping)
            { Fail("Claim / sleep interaction failed."); yield break; }
            if (!session.PlaceWeapon(0,WeaponKind.Gatling)) { Fail("Initial weapon placement failed."); yield break; }
            yield return new WaitForSecondsRealtime(2);
            if (!session.MoveWeapon(0,2) || !session.MoveWeapon(2,0)) { Fail("Weapon move failed."); yield break; }
            // A fixed bot may still be running to a house. Enter a real scouted base before testing its guard.
            deadline = Time.realtimeSinceStartup + 20;
            while (!session.Scouting && Time.realtimeSinceStartup < deadline) {
                for (int target = 1; target < match.Players.Count && !session.Scouting; target++) session.Scout(target);
                yield return null;
            }
            if (!session.Scouting) { Fail("No claimed base became available to scout."); yield break; }
            if (session.PlaceWeapon(1,WeaponKind.Gatling) || session.UpgradeWeapon(0) || session.MoveWeapon(0,2) || session.SellWeapon(0) || session.Repair() || session.BuyStation(Station.Bed)) { Fail("Scouting allowed mutation."); yield break; }
            matchHud.ShowBuildBoard(true);
            yield return new WaitForSecondsRealtime(.8f);
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"02a-readonly-scout.png"));
            yield return new WaitForSecondsRealtime(.5f);
            session.ReturnToOwnHouse();
            matchHud.RoomOpen = true;
            deadline = Time.realtimeSinceStartup + 15;
            while ((!matchHud.RoomOpen || match.Houses[0].IsBuilding || match.Players[0].Gold < match.UpgradeCost(0,UpgradeKind.Bed)) && Time.realtimeSinceStartup < deadline) yield return null;
            if (!matchHud.RoomOpen || !session.BuyStation(Station.Bed) || session.BuyStation(Station.Bed)) { Fail("Room entry / station purchase guard failed."); yield break; }
            yield return new WaitForSecondsRealtime(.35f);
            session.BuyStation(Station.Door);
            if (session.Queue.Queued != Station.Door) { Fail("Station queue failed."); yield break; }
            double queuedGold = match.Players[0].Gold;
            yield return new WaitForSecondsRealtime(.35f);
            session.BuyStation(Station.Door);
            if (session.Queue.Queued != null || match.Players[0].Gold < queuedGold) { Fail("Station cancellation spent gold."); yield break; }
            matchHud.RoomOpen = false;
            session.TogglePause(); float pausedAt = match.Elapsed;
            yield return new WaitForSecondsRealtime(0.25f);
            if (match.Elapsed != pausedAt) { Fail("Pause failed."); yield break; }
            session.TogglePause();
            yield return new WaitForSecondsRealtime(.8f);
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"02-house.png"));
            yield return new WaitForSecondsRealtime(0.5f);
            Time.timeScale = 5;
            bool combatCaptured = false;
            deadline = Time.realtimeSinceStartup + 150;
            while (!match.Finished && Time.realtimeSinceStartup < deadline)
            {
                var home = match.Houses[0];
                if (!home.IsBuilding && !match.Players[0].Eliminated) {
                    if (home.Health < home.MaxHealth * .6f) session.Repair();
                    for (int slot = 0; slot < 3; slot++) {
                        if (home.Weapons[slot] == null) session.PlaceWeapon(slot,slot == 2 ? WeaponKind.Rocket : WeaponKind.Gatling);
                        else if (home.BedLevel >= 2) session.UpgradeWeapon(slot);
                    }
                }
                UpgradeKind choice = home.Health < home.MaxHealth * 0.65f ? UpgradeKind.Door :
                    home.BedLevel < 2 ? UpgradeKind.Bed : UpgradeKind.Door;
                if (!home.IsBuilding && match.UpgradeCost(0,choice) >= 0 && match.Players[0].Gold >= match.UpgradeCost(0,choice)) session.Buy(choice);
                if (!combatCaptured && match.CombatSeconds > 10)
                { session.Overview = true; matchHud.ShowBuildBoard(false); yield return new WaitForSecondsRealtime(.8f); ScreenCapture.CaptureScreenshot(Path.Combine(output,"03-combat.png")); combatCaptured = true; }
                yield return null;
            }
            Time.timeScale = 1;
            if (!match.Finished) { Fail("Match did not resolve before timeout."); yield break; }
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"04-result.png"));
            yield return new WaitForSecondsRealtime(0.5f);
            if (session.LastReward == null || session.LastReward.Xp <= 0 || session.Account.TotalXp <= initialXp || session.SaveDirty)
            { Fail("Match XP was not awarded and saved."); yield break; }
            if (ranked && (session.Account.Rank.MatchesPlayed != 1 || session.Account.Social.Profile.Matches != 1)) { Fail("Ranked match receipt failed."); yield break; }
            long rewardedXp = session.Account.TotalXp;
            session.ReturnToTitle();
            if (session.Account.IsUnlocked(CharacterId.Lumi))
            {
                if (!session.SelectCharacter(CharacterId.Lumi)) { Fail("Newly unlocked Lumi cannot be selected."); yield break; }
                var loaded = new AccountProgression(new LocalSaveService(session.AccountPath).Load(),session.Characters,session.Progression);
                if (loaded.Selected != CharacterId.Lumi || loaded.TotalXp != rewardedXp || !loaded.IsUnlocked(CharacterId.Lumi))
                { Fail("Selection / XP / unlock save roundtrip failed."); yield break; }
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"05-character-unlock.png"));
            yield return new WaitForSecondsRealtime(.5f);
            if (ranked) session.PlayRanked(); else session.Play();
            if (session.Match.Elapsed != 0 || session.Match.Players[0].HouseId != -1)
            { Fail("Restart did not reset the match."); yield break; }
            // A second match deliberately misses the claim deadline to verify spectator UI.
            match = session.Match; Time.timeScale = 5;
            deadline = Time.realtimeSinceStartup + 100;
            while (!match.Players[0].Eliminated && Time.realtimeSinceStartup < deadline) yield return null;
            if (!match.Players[0].Eliminated || match.Finished)
            { Fail("Unclaimed-player elimination failed."); yield break; }
            session.CycleSpectator();
            if (session.SpectatedPlayer == 0 || match.Players[session.SpectatedPlayer].Eliminated)
            { Fail("Spectator did not select a living resident."); yield break; }
            session.Buy(UpgradeKind.Bed);
            if (match.Players[0].UpgradesPurchased != 0) { Fail("Eliminated player purchased an upgrade."); yield break; }
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"06-spectator.png"));
            while (!match.Finished && Time.realtimeSinceStartup < deadline) yield return null;
            Time.timeScale = 1;
            if (!match.Finished) { Fail("Spectated match timed out."); yield break; }
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"07-eliminated.png"));
            yield return new WaitForSecondsRealtime(0.5f);
            if (session.Account.TotalXp != rewardedXp) { Fail("Unclaimed spectator received XP."); yield break; }
            if (ranked && (session.Account.Rank.MatchesPlayed != 2 || session.Account.Social.Profile.Matches != 2)) { Fail("Ranked elimination result failed."); yield break; }
            File.WriteAllText(Path.Combine(output,"result.txt"),errors == 0 ? "PASS: character selection, claim, sleep, timed upgrade, pause, combat, XP reward, saved XP/unlock/selection, restart, elimination, spectating. No runtime errors." : "FAIL: runtime errors = " + errors);
            Application.Quit(errors == 0 ? 0 : 1);
        }
        private void Fail(string reason)
        {
            Time.timeScale = 1; File.WriteAllText(Path.Combine(output,"result.txt"),"FAIL: " + reason);
            Debug.LogError(reason); Application.Quit(1);
        }
    }
}
#endif
