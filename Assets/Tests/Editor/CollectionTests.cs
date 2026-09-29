using System;
using ContainerDefense.Domain;

public static class CollectionTests
{
    private static int passed;
    public static string Run()
    {
        passed = 0;
        Check("Collection catalog has seven defaults, three variants and five sticker definitions",() => {
            var c = CollectionCatalog.CreateDefault(); Assert(c.Skins.Length == 10 && c.Stickers.Length == 5);
            for (int i = 0; i < 7; i++) Assert(c.Skin(c.DefaultSkin((CharacterId)i)).Default);
            foreach (var s in c.Stickers) Assert(s.GiftValue > 0 && !string.IsNullOrEmpty(s.Icon));
        });
        Check("Catalog rejects duplicate IDs, invalid colors and missing defaults",() => {
            var skins = CollectionCatalog.DefaultSkins(); skins[9].SkinId = skins[8].SkinId; Reject(skins);
            skins = CollectionCatalog.DefaultSkins(); skins[7].CoatHex = "bad"; Reject(skins);
            skins = CollectionCatalog.DefaultSkins(); skins[0].Default = false; Reject(skins);
        });
        Check("Catalog snapshots definitions and starter quantities",() => {
            var c = CollectionCatalog.CreateDefault(); c.Skins[7].CharismaValue = 999; c.StarterStickers[0].QuantityOwned = 0; c.StarterSkins[0] = "bad";
            var inv = New(c); Assert(inv.ClaimStarter() && inv.Quantity("bunny") == 52 && inv.Charisma == 450);
        });
        Check("New collections own zero-value defaults and no stickers",() => {
            var inv = New(); Assert(inv.SkinsOwned == 7 && inv.StickerTypesOwned == 0 && inv.Charisma == 0 && !inv.StarterClaimed);
            Assert(inv.Equipped(CharacterId.Milo) == "milo_default");
        });
        Check("Starter pack grants stacks exactly once, including after reload",() => {
            var inv = New(); Assert(inv.ClaimStarter()); Assert(!inv.ClaimStarter());
            inv = new InventorySystem(inv.Snapshot(),CollectionCatalog.CreateDefault());
            Assert(!inv.ClaimStarter() && inv.Quantity("bunny") == 52 && inv.Quantity("heart") == 38 && inv.SkinsOwned == 10);
        });
        Check("Owned skins equip per character and reject unowned or mismatched skins",() => {
            var inv = New(); Assert(!inv.TryEquip(CharacterId.Milo,"milo_night")); Assert(inv.ClaimStarter());
            Assert(!inv.TryEquip(CharacterId.Milo,"lumi_cloudy") && !inv.TryEquip((CharacterId)99,"milo_night") && !inv.TryEquip(CharacterId.Milo,"missing"));
            Assert(inv.TryEquip(CharacterId.Milo,"milo_night") && inv.TryEquip(CharacterId.Lumi,"lumi_cloudy"));
            Assert(inv.Equipped(CharacterId.Milo) == "milo_night" && inv.Equipped(CharacterId.Lumi) == "lumi_cloudy");
        });
        Check("Sticker stacks handle values above 32-bit range and exact subtraction",() => {
            var inv = New(); Assert(inv.TryAddSticker("bunny",5000) && inv.TryAddSticker("bunny",3000000000L));
            Assert(inv.Quantity("bunny") == 3000005000L && inv.TryRemoveSticker("bunny",5000) && inv.Quantity("bunny") == 3000000000L);
        });
        Check("Invalid sticker changes and overflow leave inventory unchanged",() => {
            var inv = New(); Assert(!inv.TryAddSticker("missing",1) && !inv.TryAddSticker("bunny",0) && !inv.TryRemoveSticker("bunny",1));
            Assert(inv.TryAddSticker("bunny",long.MaxValue));
            Assert(!inv.TryAddSticker("bunny",1) && !inv.TryRemoveSticker("bunny",-1) && inv.Quantity("bunny") == long.MaxValue);
        });
        Check("Starter grant is atomic when a stack cannot fit",() => {
            var inv = New(); inv.TryAddSticker("heart",long.MaxValue);
            Assert(!inv.ClaimStarter() && !inv.StarterClaimed && inv.SkinsOwned == 7 && inv.Quantity("bunny") == 0);
        });
        Check("Charisma counts unique ownership, not equipped state or duplicate quantities",() => {
            var inv = New(); inv.ClaimStarter(); Assert(inv.SkinCharisma == 350 && inv.StickerCharisma == 100 && inv.Charisma == 450);
            inv.TryEquip(CharacterId.Milo,"milo_night"); inv.TryAddSticker("bunny",5000); Assert(!inv.TryGrantSkin("milo_night") && inv.Charisma == 450);
            Assert(inv.TryRemoveSticker("bunny",5052) && inv.Quantity("bunny") == 0 && inv.Charisma == 430);
        });
        Check("Collection snapshots are deep copies and cached Charisma is not trusted",() => {
            var inv = New(); inv.ClaimStarter(); var data = inv.Snapshot();
            data.Stickers[0].QuantityOwned = 999; data.EquippedSkins[0].SkinId = "bad"; data.OwnedSkins[0] = "bad"; data.Charisma = long.MaxValue;
            Assert(inv.Quantity("bunny") == 52 && inv.Equipped(CharacterId.Milo) == "milo_default");
            var restored = new InventorySystem(data,CollectionCatalog.CreateDefault()); Assert(restored.Charisma == 450 && restored.Equipped(CharacterId.Milo) == "milo_default");
        });
        Check("Malformed saved stacks normalize without duplicate inflation",() => {
            var data = new CollectionData { Stickers = new[] { new StickerStack { StickerId = "bunny", QuantityOwned = 10 }, new StickerStack { StickerId = "bunny", QuantityOwned = 20 }, new StickerStack { StickerId = "cat", QuantityOwned = -2 }, null },
                EquippedSkins = new[] { new EquippedSkin { CharacterId = CharacterId.Milo, SkinId = "lumi_cloudy" } } };
            var inv = new InventorySystem(data,CollectionCatalog.CreateDefault()); Assert(inv.Quantity("bunny") == 20 && inv.Quantity("cat") == 0 && inv.Equipped(CharacterId.Milo) == "milo_default");
        });
        Check("Temporarily unknown collection IDs survive save normalization",() => {
            var inv = new InventorySystem(new CollectionData { OwnedSkins = new[] { "future_skin" }, Stickers = new[] { new StickerStack { StickerId = "future_sticker", QuantityOwned = 5000 } } },CollectionCatalog.CreateDefault());
            var restored = new InventorySystem(inv.Snapshot(),CollectionCatalog.CreateDefault());
            Assert(restored.OwnsSkin("future_skin") && restored.Quantity("future_sticker") == 5000 && restored.Charisma == 0);
        });
        Check("Version-one accounts migrate without losing XP, unlocks or selection",() => {
            var a = new AccountProgression(new AccountData { Version = 1, TotalXp = 1000, SelectedCharacter = "kiko", UnlockedCharacters = new[] { "milo","lumi","kiko","yume" } },new CharacterCatalog(CharacterCatalog.Defaults()),new ProgressionRules());
            Assert(a.TotalXp == 1000 && a.Selected == CharacterId.Kiko && a.IsUnlocked(CharacterId.Yume) && a.Inventory.SkinsOwned == 7);
            Assert(a.Snapshot().Version == 2 && a.Snapshot().Collection != null);
        });
        Check("Collection actions leave gameplay progression and all personal passives untouched",() => {
            var a = new AccountProgression(null,new CharacterCatalog(CharacterCatalog.Defaults()),new ProgressionRules());
            var before = new MatchSimulation(new MatchRules());
            a.Inventory.ClaimStarter(); a.Inventory.TryEquip(CharacterId.Milo,"milo_night"); a.Inventory.TryAddSticker("bunny",5000);
            var after = new MatchSimulation(new MatchRules());
            Assert(a.TotalXp == 0 && a.Level == 1 && !a.IsUnlocked(CharacterId.Lumi));
            for (int i = 0; i < 6; i++) Assert(before.Players[i].Character.IncomeMultiplier == after.Players[i].Character.IncomeMultiplier && before.Players[i].Gold == after.Players[i].Gold);
        });
        return passed + " collection scenarios passed.";
    }
    private static InventorySystem New(CollectionCatalog c = null) { return new InventorySystem(null,c ?? CollectionCatalog.CreateDefault()); }
    private static void Reject(SkinDefinition[] skins)
    {
        bool rejected = false;
        try { new CollectionCatalog(skins,CollectionCatalog.DefaultStickers(),new string[0],new StickerStack[0]); } catch (ArgumentException) { rejected = true; }
        Assert(rejected);
    }
    private static void Check(string name,Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    private static void Assert(bool value) { if (!value) throw new Exception("Collection assertion failed."); }
}
