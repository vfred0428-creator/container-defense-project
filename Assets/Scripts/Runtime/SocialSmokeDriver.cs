#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    public sealed class SocialSmokeDriver : MonoBehaviour
    {
        private GameSession session;
        private string output;
        private int errors;
        public void Initialize(GameSession game) { session = game; }
        private void OnEnable() { Application.logMessageReceived += Log; }
        private void OnDisable() { Application.logMessageReceived -= Log; }
        private void Log(string message,string trace,LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private IEnumerator Start()
        {
            output = Path.GetDirectoryName(session.AccountPath); Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(2);
            var hud = session.GetComponent<MatchHud>();
            bool reload = Array.IndexOf(Environment.GetCommandLineArgs(),"--social-reload-only") >= 0;
            if (!reload)
            {
                if (session.Inventory.StarterClaimed) { Fail("Use a fresh isolated test account."); yield break; }
                session.ClaimStarterCollection();
                if (!session.RenameProfile("LunaBuilder").Success) { Fail("Profile rename failed."); yield break; }
                hud.OpenSocial(0); yield return Capture("01-profile.png");
                hud.OpenSocial(1); yield return Capture("02-gift-selector.png");
                var r = new GiftRequest { TransactionId = Guid.NewGuid().ToString("N"), ReceiverId = "local_a", StickerId = "bunny", Quantity = 10 };
                if (!session.SendGift(r).Success || !session.SendGift(r).AlreadyDelivered || session.Inventory.Quantity("bunny") != 42)
                { Fail("Transfer or duplicate prevention failed."); yield break; }
                // Simulate a trusted incoming transaction only in an isolated development account.
                var incoming = new LocalGiftService(session.Account,session.Collections,new LocalSaveService(session.AccountPath),"local_a",() => 1790683200L);
                if (!incoming.Send(new GiftRequest { TransactionId = Guid.NewGuid().ToString("N"),ReceiverId = session.Account.Social.Profile.PlayerId,StickerId = "bunny",Quantity = 4 }).Success)
                { Fail("Incoming test delivery failed."); yield break; }
                if (SocialState.Unread(session.Account.Social) != 1) { Fail("Incoming notification missing."); yield break; }
                hud.CloseSocial(); yield return Capture("03-inbox-notification.png");
                hud.OpenSocial(2); yield return Capture("04-history.png");
                if (!session.ReadGiftInbox().Success) { Fail("Read receipt failed."); yield break; }
                hud.OpenSocial(0); yield return Capture("05-popularity.png");
            }
            var state = session.Account.Social;
            if (state.Profile.Username != "LunaBuilder" || state.Profile.Popularity != 4 || state.History.Length != 2 || SocialState.Unread(state) != 0 ||
                session.Inventory.Quantity("bunny") != 46 || state.Recipients[0].Popularity != 10 || session.Inventory.Charisma != 100 || session.SaveDirty)
            { Fail("Persisted profile, inventories, Popularity or history mismatch."); yield break; }
            if (reload)
            {
                var g = state.History[0];
                if (!session.SendGift(new GiftRequest { TransactionId = g.TransactionId,ReceiverId = g.ReceiverId,StickerId = g.StickerId,Quantity = g.Quantity }).AlreadyDelivered)
                { Fail("Replay after process restart failed."); yield break; }
                hud.OpenSocial(2); yield return Capture("06-reloaded-history.png");
            }
            hud.CloseSocial(); session.Play();
            if (session.SendGift(new GiftRequest { TransactionId = Guid.NewGuid().ToString("N"), ReceiverId = "local_a", StickerId = "bunny", Quantity = 1 }).Success)
            { Fail("Gifting allowed during match."); yield break; }
            yield return new WaitForSecondsRealtime(.5f);
            File.WriteAllText(Path.Combine(output,reload ? "reload-result.txt" : "result.txt"),errors == 0 ?
                "PASS: profile, atomic gifting, duplicate replay, incoming notification, Popularity, history, restart and in-match gate. No runtime errors." : "FAIL: runtime errors = " + errors);
            Application.Quit(errors == 0 ? 0 : 1);
        }
        private IEnumerator Capture(string name)
        { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output,name)); yield return new WaitForSecondsRealtime(.6f); }
        private void Fail(string text) { File.WriteAllText(Path.Combine(output,"result.txt"),"FAIL: " + text); Debug.LogError(text); Application.Quit(1); }
    }
}
#endif
