#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    public sealed class CollectionSmokeDriver : MonoBehaviour
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
            output = Path.GetDirectoryName(session.AccountPath);
            Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(2);
            var hud = session.GetComponent<MatchHud>();
            bool reload = Array.IndexOf(Environment.GetCommandLineArgs(),"--collection-reload-only") >= 0;
            if (reload)
            {
                if (!VerifyCollection()) { Fail("Collection did not survive process restart."); yield break; }
                hud.OpenCollection(CharacterId.Milo);
                yield return Capture("07-reloaded-skin.png");
                hud.ShowStickers(); yield return Capture("08-reloaded-stickers.png");
                Finish("reload-result.txt","New process restored skin ownership, equipment, 64-bit sticker quantities, starter receipt and Charisma.");
                yield break;
            }
            if (session.Inventory.StarterClaimed) { Fail("Use a fresh isolated output directory for the collection test."); yield break; }
            long xp = session.Account.TotalXp;
            hud.OpenCollection(CharacterId.Milo); yield return Capture("01-unclaimed.png");
            if (session.EquipSkin(CharacterId.Milo,"milo_night")) { Fail("Unowned skin was equipped."); yield break; }
            if (!session.ClaimStarterCollection() || session.ClaimStarterCollection() || session.Inventory.Charisma != 450)
            { Fail("Starter receipt or Charisma failed."); yield break; }
            if (session.EquipSkin(CharacterId.Lumi,"lumi_cloudy")) { Fail("Locked character accepted equipment."); yield break; }
            if (!session.EquipSkin(CharacterId.Milo,"milo_night")) { Fail("Owned skin could not equip."); yield break; }
            hud.OpenCollection(CharacterId.Milo); yield return Capture("02-equipped-night.png");
            hud.ShowStickers(); yield return Capture("03-stickers.png");
            // Explicit test account only: exercise exact stack sizes beyond Int32.
            session.Inventory.TryAddSticker("bunny",3000000000L); session.PersistAccount();
            yield return Capture("04-large-stack.png");
            var loaded = new AccountProgression(new LocalSaveService(session.AccountPath).Load(),session.Characters,session.Progression,session.Collections);
            if (loaded.Inventory.Quantity("bunny") != 3000000052L || loaded.Inventory.Equipped(CharacterId.Milo) != "milo_night" || loaded.Inventory.Charisma != 450 || session.SaveDirty || session.Account.TotalXp != xp)
            { Fail("Collection save roundtrip or cosmetic isolation failed."); yield break; }
            hud.CloseCollection(); yield return Capture("05-equipped-title.png");
            session.Play(); yield return new WaitForSecondsRealtime(.5f);
            bool visible = false;
            foreach (var t in session.Arena.GetComponentsInChildren<Transform>())
                if (t.name == "Milo / milo_night" && t.gameObject.activeInHierarchy) visible = true;
            if (!visible || session.Match.Players[0].Character.Id != CharacterId.Milo ||
                Math.Abs(session.Match.Players[0].Character.IncomeMultiplier - 1.08f) > .001f || session.EquipSkin(CharacterId.Milo,"milo_default"))
            { Fail("Equipped appearance or in-match equipment gate failed."); yield break; }
            yield return Capture("06-in-match.png");
            Finish("result.txt","Starter pack, ownership gates, locked characters, skin preview/equip, in-match appearance, stacks above Int32, Charisma and saved collection. No gameplay bonuses added.");
        }
        private bool VerifyCollection()
        {
            return session.Inventory.StarterClaimed && session.Inventory.SkinsOwned == 10 &&
                session.Inventory.Equipped(CharacterId.Milo) == "milo_night" && session.Inventory.Quantity("bunny") == 3000000052L &&
                session.Inventory.Quantity("heart") == 38 && session.Inventory.Charisma == 450 && !session.ClaimStarterCollection() && !session.SaveDirty;
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,name));
            yield return new WaitForSecondsRealtime(.5f);
        }
        private void Finish(string filename,string message)
        {
            File.WriteAllText(Path.Combine(output,filename),errors == 0 ? "PASS: " + message + " No runtime errors." : "FAIL: runtime errors = " + errors);
            Application.Quit(errors == 0 ? 0 : 1);
        }
        private void Fail(string message) { File.WriteAllText(Path.Combine(output,"result.txt"),"FAIL: " + message); Debug.LogError(message); Application.Quit(1); }
    }
}
#endif
