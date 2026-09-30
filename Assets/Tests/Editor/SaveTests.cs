using System;
using System.IO;
using ContainerDefense;
using ContainerDefense.Domain;

public static class SaveTests
{
    public static string Run()
    {
        string directory = Path.Combine(Path.GetTempPath(),"ContainerDefense-SaveTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); int count = 0;
        try
        {
            string path = Path.Combine(directory,"account.json");
            var store = new LocalSaveService(path);
            var initial = store.Load(); Assert(initial.TotalXp == 0 && initial.SelectedCharacter == "milo"); count++;
            var account = new AccountData { TotalXp = 1000, Level = 5, SelectedCharacter = "kiko", UnlockedCharacters = new[] { "milo","lumi","kiko" }, MatchesStarted = 12, LastRewardedSequence = 11 };
            store.Save(account);
            var loaded = new LocalSaveService(path).Load();
            Assert(loaded.TotalXp == 1000 && loaded.SelectedCharacter == "kiko" && loaded.UnlockedCharacters.Length == 3 && loaded.MatchesStarted == 12 && loaded.LastRewardedSequence == 11); count++;
            account.TotalXp = 1250; store.Save(account);
            Assert(new LocalSaveService(path).Load().TotalXp == 1250 && File.Exists(path + ".bak") && !File.Exists(path + ".tmp")); count++;
            File.WriteAllText(path,"corrupt primary");
            var recovered = new LocalSaveService(path); Assert(recovered.Load().TotalXp == 1000 && recovered.CanWrite); count++;
            account.TotalXp = 1500; recovered.Save(account);
            Assert(new LocalSaveService(path).Load().TotalXp == 1500);
            Assert(new LocalSaveService(path + ".bak").Load().TotalXp == 1000); count++;
            File.Delete(path);
            Assert(new LocalSaveService(path).Load().TotalXp == 1000); count++;
            File.WriteAllText(path,"corrupt primary"); File.WriteAllText(path + ".bak","corrupt backup");
            var broken = new LocalSaveService(path); broken.Load();
            Assert(!broken.CanWrite); bool denied = false; try { broken.Save(account); } catch (IOException) { denied = true; }
            Assert(denied && File.ReadAllText(path) == "corrupt primary" && File.ReadAllText(path + ".bak") == "corrupt backup"); count++;
            File.WriteAllText(path,"{\"Format\":99,\"Payload\":\"future version\"}");
            var future = new LocalSaveService(path); future.Load(); Assert(!future.CanWrite && future.Status.Contains("newer")); count++;
            var impossible = new LocalSaveService(directory);
            bool failed = false; try { impossible.Save(account); } catch (IOException) { failed = true; } catch (UnauthorizedAccessException) { failed = true; }
            Assert(failed); count++;
            string collectionPath = Path.Combine(directory,"collection.json");
            var collectionStore = new LocalSaveService(collectionPath);
            collectionStore.Save(new AccountData { Version = 1, TotalXp = 1000, SelectedCharacter = "kiko", UnlockedCharacters = new[] { "milo","lumi","kiko" } });
            var migrated = new AccountProgression(collectionStore.Load(),new CharacterCatalog(CharacterCatalog.Defaults()),new ProgressionRules());
            Assert(migrated.TotalXp == 1000 && migrated.Selected == CharacterId.Kiko && migrated.Inventory.SkinsOwned == 7); count++;
            migrated.Inventory.ClaimStarter(); migrated.Inventory.TryEquip(CharacterId.Kiko,"kiko_default");
            migrated.Inventory.TryAddSticker("bunny",3000000000L); collectionStore.Save(migrated.Snapshot());
            var collectionReload = new AccountProgression(new LocalSaveService(collectionPath).Load(),new CharacterCatalog(CharacterCatalog.Defaults()),new ProgressionRules());
            Assert(collectionReload.Inventory.Quantity("bunny") == 3000000052L && collectionReload.Inventory.Equipped(CharacterId.Kiko) == "kiko_default" &&
                collectionReload.Inventory.Charisma == 100 && !collectionReload.Inventory.ClaimStarter() && collectionReload.TotalXp == 1000); count++;
            collectionStore.Save(collectionReload.Snapshot()); File.WriteAllText(collectionPath,"interrupted write");
            var collectionRecovery = new AccountProgression(new LocalSaveService(collectionPath).Load(),new CharacterCatalog(CharacterCatalog.Defaults()),new ProgressionRules());
            Assert(collectionRecovery.Inventory.Quantity("bunny") == 3000000052L && collectionRecovery.Inventory.Equipped(CharacterId.Kiko) == "kiko_default" && collectionRecovery.Inventory.Charisma == 100); count++;
            string socialPath = Path.Combine(directory,"social.json");
            var socialStore = new LocalSaveService(socialPath);
            var socialAccount = new AccountProgression(null,new CharacterCatalog(CharacterCatalog.Defaults()),new ProgressionRules());
            socialAccount.Inventory.ClaimStarter(); socialStore.Save(socialAccount.Snapshot());
            var gifts = new LocalGiftService(socialAccount,CollectionCatalog.CreateDefault(),socialStore,socialAccount.Social.Profile.PlayerId,() => 1790683200L);
            var request = new GiftRequest { TransactionId = Guid.NewGuid().ToString("N"),ReceiverId = "local_a",StickerId = "bunny",Quantity = 10 };
            Assert(gifts.Send(request).Success);
            var socialReload = new AccountProgression(new LocalSaveService(socialPath).Load(),new CharacterCatalog(CharacterCatalog.Defaults()),new ProgressionRules());
            Assert(socialReload.Inventory.Quantity("bunny") == 42 && socialReload.Social.Recipients[0].Popularity == 10 && socialReload.Social.History.Length == 1); count++;
            Assert(new LocalGiftService(socialReload,CollectionCatalog.CreateDefault(),socialStore,socialReload.Social.Profile.PlayerId,() => 1790683200L).Send(request).AlreadyDelivered); count++;
            socialStore.Save(socialReload.Snapshot()); File.WriteAllText(socialPath,"interrupted social write");
            var socialRecovery = new AccountProgression(new LocalSaveService(socialPath).Load(),new CharacterCatalog(CharacterCatalog.Defaults()),new ProgressionRules());
            Assert(socialRecovery.Inventory.Quantity("bunny") == 42 && socialRecovery.Social.Recipients[0].Collection.Stickers[0].QuantityOwned == 10 && socialRecovery.Social.History.Length == 1); count++;
            var oversized = socialRecovery.Snapshot(); oversized.Collection.OwnedSkins = new string[1]; oversized.Collection.OwnedSkins[0] = new string('x',1100000);
            bool tooLarge = false; try { socialStore.Save(oversized); } catch (IOException) { tooLarge = true; }
            Assert(tooLarge && File.ReadAllText(socialPath) == "interrupted social write"); count++;
            var rankedSave = socialRecovery.Snapshot(); rankedSave.Rank = new RankData { CurrentRank = RankTier.Champion,HighestRank = RankTier.Celestial,Stars = 3,RankPoints = 210,Season = 2,Wins = 7,MatchesPlayed = 12 };
            socialStore.Save(rankedSave);
            var rankedReload = new AccountProgression(new LocalSaveService(socialPath).Load(),new CharacterCatalog(CharacterCatalog.Defaults()),new ProgressionRules());
            Assert(rankedReload.Rank.CurrentRank == RankTier.Champion && rankedReload.Rank.HighestRank == RankTier.Celestial && rankedReload.Rank.Stars == 3 &&
                rankedReload.Rank.RankPoints == 210 && rankedReload.Rank.Season == 2 && rankedReload.Rank.Wins == 7 && rankedReload.Rank.MatchesPlayed == 12 && rankedReload.Social.History.Length == 1); count++;
            Console.WriteLine("PASS " + count + " persistence scenarios: roundtrip, replacement, backup, corruption, future versions, write failure, atomic gifts and size limits.");
            return count + " persistence scenarios passed.";
        }
        finally
        {
            string resolved = Path.GetFullPath(directory);
            if (resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("ContainerDefense-SaveTests-"))
                Directory.Delete(resolved,true);
            if (File.Exists(directory + ".tmp")) File.Delete(directory + ".tmp");
        }
    }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Persistence assertion failed."); }
}
