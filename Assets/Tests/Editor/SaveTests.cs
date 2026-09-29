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
            migrated.Inventory.ClaimStarter(); migrated.Inventory.TryEquip(CharacterId.Kiko,"kiko_red");
            migrated.Inventory.TryAddSticker("bunny",3000000000L); collectionStore.Save(migrated.Snapshot());
            var collectionReload = new AccountProgression(new LocalSaveService(collectionPath).Load(),new CharacterCatalog(CharacterCatalog.Defaults()),new ProgressionRules());
            Assert(collectionReload.Inventory.Quantity("bunny") == 3000000052L && collectionReload.Inventory.Equipped(CharacterId.Kiko) == "kiko_red" &&
                collectionReload.Inventory.Charisma == 450 && !collectionReload.Inventory.ClaimStarter() && collectionReload.TotalXp == 1000); count++;
            collectionStore.Save(collectionReload.Snapshot()); File.WriteAllText(collectionPath,"interrupted write");
            var collectionRecovery = new AccountProgression(new LocalSaveService(collectionPath).Load(),new CharacterCatalog(CharacterCatalog.Defaults()),new ProgressionRules());
            Assert(collectionRecovery.Inventory.Quantity("bunny") == 3000000052L && collectionRecovery.Inventory.Equipped(CharacterId.Kiko) == "kiko_red" && collectionRecovery.Inventory.Charisma == 450); count++;
            Console.WriteLine("PASS " + count + " persistence scenarios: roundtrip, replacement, backup, corruption, future versions, write failure.");
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
