using System;
using System.Collections.Generic;
using System.IO;
using ContainerDefense.Domain;
using UnityEditor;
using UnityEngine;

namespace ContainerDefense.Editor
{
    // Editor-only screenshot pass: enters play mode, walks every screen at several Game view
    // resolutions and saves captures. Launch the Editor (not batchmode) with
    //   -executeMethod ContainerDefense.Editor.UiScreenshots.Run --smoke-test --manual-test --smoke-output <dir> --ui-shots <dir>
    // --smoke-test/--smoke-output isolate the account file; --manual-test keeps smoke drivers off.
    [InitializeOnLoad]
    public static class UiScreenshots
    {
        private const string ActiveKey = "ContainerDefense.UiShots.Active";
        private static readonly Vector2Int[] Resolutions = { new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(2340,1080) };
        private static IEnumerator<float> steps;
        private static double waitUntil;
        static UiScreenshots()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey,false)) { steps = Sequence(); waitUntil = 0; EditorApplication.update += Pump; }
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ActiveKey,false)) { SessionState.SetBool(ActiveKey,false); EditorApplication.Exit(0); }
            };
        }
        public static void Run()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            SessionState.SetBool(ActiveKey,true);
            EditorApplication.EnterPlaymode();
        }
        private static void Pump()
        {
            if (EditorApplication.timeSinceStartup < waitUntil) return;
            bool more;
            try { more = steps.MoveNext(); }
            catch (Exception e) { Debug.LogException(e); more = false; }
            if (!more) { EditorApplication.update -= Pump; EditorApplication.ExitPlaymode(); return; }
            waitUntil = EditorApplication.timeSinceStartup + steps.Current;
        }
        private static string Output()
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args,"--ui-shots");
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.GetFullPath("TestResults/UI-Pass/latest");
        }
        private static IEnumerator<float> Sequence()
        {
            string output = Output(); Directory.CreateDirectory(output);
            var session = UnityEngine.Object.FindFirstObjectByType<GameSession>();
            var hud = session.GetComponent<MatchHud>();
            foreach (var size in Resolutions)
            {
                PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
                PlayModeWindow.SetCustomRenderingResolution((uint)size.x,(uint)size.y,"UI pass " + size.x + "x" + size.y);
                // Wide phones get a simulated notch inset so safe-area handling is visible.
                HudLayout.SimulatedInset = size.x * 1f / size.y > 2 ? 132 : 0;
                yield return 1.2f;
                string tag = size.x + "x" + size.y;
                Func<string,string> file = name => Path.Combine(output,tag + "-" + name + ".png");
                session.ReturnToTitle(); hud.CloseRanked(); hud.CloseSocial(); hud.CloseCollection(); yield return .6f;
                Shot(file("01-title")); yield return .4f;
                hud.OpenCollection(session.Account.Selected); yield return .4f; Shot(file("02-collection-skins")); yield return .4f;
                hud.ShowStickers(); yield return .4f; Shot(file("03-collection-stickers")); yield return .4f;
                hud.OpenSocial(0); yield return .4f; Shot(file("04-social-profile")); yield return .4f;
                hud.OpenSocial(1); yield return .4f; Shot(file("05-social-gift")); yield return .4f;
                hud.OpenSocial(2); yield return .4f; Shot(file("06-social-history")); yield return .4f;
                hud.OpenRanked(); yield return .4f; Shot(file("07-ranked")); yield return .4f;
                hud.OpenLeaderboards(LeaderboardKind.Ranked); yield return .4f; Shot(file("08-leaderboard")); yield return .4f;
                hud.CloseRanked(); session.Play(); session.Overview = true; yield return 1f; Shot(file("09-match-claim")); yield return .4f;
                var m = session.Match; var bots = new LocalBotController(m);
                for (int i = 0; i < 900 && m.Players[0].Position.Distance(m.Houses[4].Entry) > .1f; i++) m.Navigate(0,m.Houses[4].Entry,1f / 30);
                m.TryClaim(0,4); m.TryToggleSleep(0);
                for (int i = 0; i < 30 * 12; i++) { bots.Tick(1f / 30); m.Tick(1f / 30); }
                m.TryPlaceWeapon(0,4,0,WeaponKind.Gatling); m.TryPlaceWeapon(0,4,2,WeaponKind.Cannon);
                session.ReturnToOwnHouse(); session.Overview = false; hud.ShowBuildBoard(false); yield return 1.2f; Shot(file("10-my-house")); yield return .4f;
                hud.ShowBuildBoard(true); yield return .6f; Shot(file("11-build-sockets")); yield return .4f;
                hud.ShowBuildBoard(false); session.Scout(2); yield return 1.2f; Shot(file("12-scouting")); yield return .4f;
                session.ReturnToOwnHouse(); session.Overview = true;
                for (int i = 0; i < 30 * 40 && m.Boss.Phase != BossPhase.Telegraphing; i++) { bots.Tick(1f / 30); m.Tick(1f / 30); }
                session.Notify("The storm is here. Protect your own door!"); yield return 1.2f; Shot(file("13-combat-overview")); yield return .4f;
                session.Overview = false; yield return 1.2f; Shot(file("14-combat-house")); yield return .4f;
                session.TogglePause(); yield return .5f; Shot(file("15-pause")); yield return .3f; session.TogglePause();
                for (int p = 1; p < 6; p++) m.TryForfeit(p);
                yield return 1f; Shot(file("16-results")); yield return .4f;
            }
            HudLayout.SimulatedInset = 0;
            File.WriteAllText(Path.Combine(output,"done.txt"),"UI screenshot pass complete: " + DateTime.Now.ToString("s"));
        }
        private static void Shot(string path) { ScreenCapture.CaptureScreenshot(path); }
    }
}
