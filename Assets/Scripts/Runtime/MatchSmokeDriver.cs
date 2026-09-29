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
            if (Array.IndexOf(args,"--smoke-reload-only") >= 0)
            {
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
            session.Play();
            var match = session.Match;
            float deadline = Time.realtimeSinceStartup + 20;
            while (match.Players[0].Position.Distance(match.Houses[0].Entry) > 0.2f && Time.realtimeSinceStartup < deadline)
            {
                var position = match.Players[0].Position; var target = match.Houses[0].Entry;
                match.Move(0,target.X - position.X,target.Z - position.Z,1f / 30);
                yield return null;
            }
            session.Interact(); session.Interact();
            if (match.Players[0].HouseId != 0 || !match.Players[0].Sleeping)
            { Fail("Claim / sleep interaction failed."); yield break; }
            session.Buy(UpgradeKind.Bed);
            session.TogglePause(); float pausedAt = match.Elapsed;
            yield return new WaitForSecondsRealtime(0.25f);
            if (match.Elapsed != pausedAt) { Fail("Pause failed."); yield break; }
            session.TogglePause();
            session.GetComponent<MatchHud>().RoomOpen = true;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"02a-interior.png"));
            yield return new WaitForSecondsRealtime(.5f);
            session.GetComponent<MatchHud>().RoomOpen = false;
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"02-house.png"));
            yield return new WaitForSecondsRealtime(0.5f);
            Time.timeScale = 5;
            bool combatCaptured = false;
            deadline = Time.realtimeSinceStartup + 150;
            while (!match.Finished && Time.realtimeSinceStartup < deadline)
            {
                var home = match.Houses[0];
                UpgradeKind choice = home.Health < home.MaxHealth * 0.65f ? UpgradeKind.Door :
                    home.BedLevel < 2 ? UpgradeKind.Bed : home.WeaponLevel < 4 ? UpgradeKind.Weapon : UpgradeKind.Door;
                if (!home.IsBuilding && match.UpgradeCost(0,choice) >= 0 && match.Players[0].Gold >= match.UpgradeCost(0,choice)) session.Buy(choice);
                if (!combatCaptured && match.CombatSeconds > 10)
                { ScreenCapture.CaptureScreenshot(Path.Combine(output,"03-combat.png")); combatCaptured = true; }
                yield return null;
            }
            Time.timeScale = 1;
            if (!match.Finished) { Fail("Match did not resolve before timeout."); yield break; }
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"04-result.png"));
            yield return new WaitForSecondsRealtime(0.5f);
            if (session.LastReward == null || session.LastReward.Xp <= 0 || session.Account.TotalXp <= initialXp || session.SaveDirty)
            { Fail("Match XP was not awarded and saved."); yield break; }
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
            session.Play();
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
