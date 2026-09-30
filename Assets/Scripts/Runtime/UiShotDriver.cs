#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    // Explicit --ui-shots <dir> opt-in (with --smoke-test --manual-test for an isolated account), excluded from
    // release builds. Walks every screen at the current window size and saves captures; --notch <px> simulates
    // a phone notch on both sides so safe-area handling is visible.
    public sealed class UiShotDriver : MonoBehaviour
    {
        private GameSession session;
        private MatchHud hud;
        private string output;
        private int warnings;
        public void Initialize(GameSession game) { session = game; hud = game.GetComponent<MatchHud>(); }
        private void OnEnable() { Application.logMessageReceived += OnLog; }
        private void OnDisable() { Application.logMessageReceived -= OnLog; }
        private void OnLog(string condition,string trace,LogType type)
        { if (condition.StartsWith("[HUD audit]") || type == LogType.Error || type == LogType.Exception) { warnings++; File.AppendAllText(Path.Combine(output,"audit.txt"),condition + Environment.NewLine); } }
        private static string Arg(string name)
        { var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args,name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        private IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(.6f); yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,name + ".png"));
            yield return new WaitForSecondsRealtime(.3f);
        }
        private IEnumerator Start()
        {
            output = Path.GetFullPath(Arg("--ui-shots")); Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"audit.txt"),"");
            float notch; if (float.TryParse(Arg("--notch") ?? "0",out notch)) HudLayout.SimulatedInset = notch;
            yield return new WaitForSecondsRealtime(2);
            session.Inventory.ClaimStarter();
            yield return Shot("01-title");
            hud.OpenCollection(session.Account.Selected); yield return Shot("02-collection-skins");
            hud.ShowStickers(); yield return Shot("03-collection-stickers");
            hud.OpenSocial(0); yield return Shot("04-social-profile");
            hud.OpenSocial(1); yield return Shot("05-social-gift");
            hud.OpenSocial(2); yield return Shot("06-social-history");
            hud.OpenRanked(); yield return Shot("07-ranked");
            hud.OpenLeaderboards(LeaderboardKind.Charisma); yield return Shot("08-leaderboard");
            hud.CloseRanked(); session.Play(); session.Overview = true;
            yield return new WaitForSecondsRealtime(1.2f); yield return Shot("09-match-claim");
            var m = session.Match; var bots = new LocalBotController(m);
            for (int i = 0; i < 1500 && m.Players[0].Position.Distance(m.Houses[5].Entry) > .1f; i++) m.Navigate(0,m.Houses[5].Entry,1f / 30);
            m.TryClaim(0,5); m.TryToggleSleep(0);
            for (int i = 0; i < 30 * 12; i++) { bots.Tick(1f / 30); m.Tick(1f / 30); }
            m.TryPlaceWeapon(0,5,0,WeaponKind.Gatling); m.TryPlaceWeapon(0,5,2,WeaponKind.Cannon);
            session.ReturnToOwnHouse(); session.Overview = false; hud.ShowBuildBoard(false);
            yield return new WaitForSecondsRealtime(1f); yield return Shot("10-my-house");
            hud.ShowBuildBoard(true); yield return Shot("11-socket-selected");
            session.ReturnToOwnHouse(); session.Overview = true; hud.ShowBuildBoard(false);
            yield return new WaitForSecondsRealtime(1f); yield return Shot("12-overview-claimed");
            session.Scout(2); yield return new WaitForSecondsRealtime(1f); yield return Shot("13-scouting");
            session.ReturnToOwnHouse(); session.Overview = true;
            for (int i = 0; i < 30 * 60 && m.Boss.Phase != BossPhase.Telegraphing; i++) { bots.Tick(1f / 30); m.Tick(1f / 30); }
            session.Notify("The storm is here. Protect your own door!");
            yield return new WaitForSecondsRealtime(1f); yield return Shot("14-boss-telegraph");
            for (int i = 0; i < 30 * 60 && m.Boss.Phase != BossPhase.Attacking; i++) { bots.Tick(1f / 30); m.Tick(1f / 30); }
            yield return Shot("15-boss-attacking");
            hud.RoomOpen = true; session.Overview = false; yield return Shot("16-my-room"); hud.RoomOpen = false;
            session.TogglePause(); yield return Shot("17-pause"); session.TogglePause();
            for (int p = 1; p < 6; p++) m.TryForfeit(p);
            yield return new WaitForSecondsRealtime(1f); yield return Shot("18-results");
            File.WriteAllText(Path.Combine(output,"result.txt"),"Captured 18 screens at " + Screen.width + "x" + Screen.height + " (notch " + HudLayout.SimulatedInset + "). HUD audit/log warnings: " + warnings);
            Application.Quit();
        }
    }
}
#endif
