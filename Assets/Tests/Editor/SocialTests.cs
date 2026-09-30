using System;
using System.IO;
using System.Threading.Tasks;
using ContainerDefense.Domain;

public static class SocialTests
{
    private static int passed;
    private sealed class Store : ISaveService
    {
        public AccountData Data; public bool Fail; public int Writes;
        public AccountData Load() { return Data; }
        public void Save(AccountData data) { if (Fail) throw new IOException("Disk full"); Data = data; Writes++; }
        public string Status { get { return "Saved"; } }
        public bool CanWrite { get { return true; } }
    }
    private static readonly CollectionCatalog Catalog = CollectionCatalog.CreateDefault();
    private static AccountProgression New(AccountData data = null)
    { return new AccountProgression(data,new CharacterCatalog(CharacterCatalog.Defaults()),new ProgressionRules(),Catalog); }
    private static LocalGiftService Service(AccountProgression a,Store s,string sender = null)
    { return new LocalGiftService(a,Catalog,s,sender ?? a.Social.Profile.PlayerId,() => 1790683200L); }
    private static GiftRequest Request(long n = 10,string receiver = "local_a",string sticker = "bunny")
    { return new GiftRequest { TransactionId = Guid.NewGuid().ToString("N"),ReceiverId = receiver,StickerId = sticker,Quantity = n }; }
    public static string Run()
    {
        passed = 0;
        Check("Old accounts migrate without losing XP, permanent unlocks or unknown inventory",() => {
            var data = new AccountData { Version = 2, TotalXp = 1234, UnlockedCharacters = new[] { "milo","yume" },
                Collection = new CollectionData { Stickers = new[] { new StickerStack { StickerId = "retired", QuantityOwned = 72 } } } };
            var a = New(data); var copy = New(a.Snapshot());
            Assert(a.TotalXp == 1234 && a.IsUnlocked(CharacterId.Yume) && copy.Social.Profile.PlayerId == a.Social.Profile.PlayerId && copy.Inventory.Quantity("retired") == 72);
        });
        Check("Gift atomically moves ten copies and credits only receiver popularity",() => {
            var a = New(); a.Inventory.ClaimStarter(); var s = new Store(); var g = Service(a,s);
            Assert(g.Send(Request()).Success && s.Writes == 1 && a.Inventory.Quantity("bunny") == 42);
            Assert(a.Social.Recipients[0].Collection.Stickers[0].QuantityOwned == 10 && a.Social.Recipients[0].Popularity == 10);
            Assert(a.Social.Profile.Popularity == 0 && a.Inventory.Charisma == 100 && a.TotalXp == 0);
            Assert(New(s.Load()).Inventory.Quantity("bunny") == 42 && s.Data.Social.History.Length == 1);
        });
        Check("Duplicate requests and case variants are idempotent across restart",() => {
            var a = New(); a.Inventory.ClaimStarter(); var s = new Store(); var r = Request(52); var g = Service(a,s);
            Assert(g.Send(r).Success); r.TransactionId = r.TransactionId.ToUpperInvariant();
            Assert(g.Send(r).AlreadyDelivered && s.Writes == 1);
            var reload = New(s.Load()); Assert(Service(reload,s).Send(r).AlreadyDelivered && reload.Inventory.Quantity("bunny") == 0 && reload.Social.History.Length == 1);
        });
        Check("Reused transaction IDs with changed payload or authenticated sender reject",() => {
            var a = New(); a.Inventory.ClaimStarter(); var s = new Store(); var r = Request(); var g = Service(a,s); Assert(g.Send(r).Success);
            r.Quantity = 11; Assert(!g.Send(r).Success); r.Quantity = 10; r.ReceiverId = "local_b"; Assert(!g.Send(r).Success);
            r.ReceiverId = "local_a"; Assert(!Service(a,s,"local_b").Send(r).Success && s.Writes == 1);
        });
        Check("Invalid and unaffordable quantities never mutate state",() => {
            var a = New(); a.Inventory.ClaimStarter(); var s = new Store(); var g = Service(a,s);
            foreach (long n in new[] { long.MinValue,-1L,0L,53L,long.MaxValue }) Assert(!g.Send(Request(n)).Success);
            Assert(a.Inventory.Quantity("bunny") == 52 && s.Writes == 0 && a.Social.History.Length == 0);
        });
        Check("Unknown recipient, self gift, unknown sticker and unauthenticated sender reject",() => {
            var a = New(); a.Inventory.ClaimStarter(); var s = new Store(); var g = Service(a,s);
            Assert(!g.Send(Request(1,"unknown")).Success && !g.Send(Request(1,a.Social.Profile.PlayerId)).Success);
            Assert(!g.Send(Request(1,"local_a","missing")).Success && !Service(a,s,"unknown").Send(Request()).Success);
            var r = Request(); r.TransactionId = "bad"; Assert(!g.Send(r).Success && !g.Send(null).Success && s.Writes == 0);
        });
        Check("Persistence failure rolls back inventories, popularity, receipts and permits retry",() => {
            var a = New(); a.Inventory.ClaimStarter(); var s = new Store { Fail = true }; var r = Request(); var g = Service(a,s);
            Assert(!g.Send(r).Success && a.Inventory.Quantity("bunny") == 52 && a.Social.History.Length == 0 && a.Social.Recipients[0].Popularity == 0);
            s.Fail = false; Assert(g.Send(r).Success && a.Inventory.Quantity("bunny") == 42 && s.Writes == 1);
        });
        Check("Quantities above Int32 survive delivery and reload exactly",() => {
            var a = New(); a.Inventory.TryAddSticker("bunny",4000000000L); var s = new Store();
            Assert(Service(a,s).Send(Request(3000000000L)).Success && New(s.Data).Inventory.Quantity("bunny") == 1000000000L);
            Assert(New(s.Data).Social.Recipients[0].Popularity == 3000000000L);
        });
        Check("Popularity multiplication and addition overflow reject before committing",() => {
            var a = New(); a.Inventory.TryAddSticker("good",long.MaxValue); var s = new Store();
            Assert(!Service(a,s).Send(Request(long.MaxValue,"local_a","good")).Success);
            var data = a.Snapshot(); data.Social.Recipients[0].Popularity = long.MaxValue; a = New(data); a.Inventory.TryAddSticker("bunny",2);
            Assert(!Service(a,s).Send(Request(1)).Success && a.Inventory.Quantity("bunny") == 2 && s.Writes == 0);
        });
        Check("Receiver stack overflow preserves sender balance",() => {
            var a = New(); var data = a.Snapshot(); data.Social.Recipients[0].Collection.Stickers = new[] { new StickerStack { StickerId = "bunny", QuantityOwned = long.MaxValue } };
            a = New(data); a.Inventory.ClaimStarter(); var s = new Store();
            Assert(!Service(a,s).Send(Request()).Success && a.Inventory.Quantity("bunny") == 52 && s.Writes == 0);
        });
        Check("Incoming gifts produce a persisted unread receipt and separate Popularity",() => {
            var a = New(); a.Inventory.ClaimStarter(); var s = new Store(); Assert(Service(a,s).Send(Request()).Success);
            Assert(Service(a,s,"local_a").Send(Request(4,a.Social.Profile.PlayerId)).Success);
            Assert(a.Inventory.Quantity("bunny") == 46 && a.Social.Profile.Popularity == 4 && SocialState.Unread(a.Social) == 1);
            s.Fail = true; Assert(!Service(a,s).MarkRead().Success && SocialState.Unread(a.Social) == 1);
            s.Fail = false; Assert(Service(a,s).MarkRead().Success && SocialState.Unread(New(s.Data).Social) == 0);
        });
        Check("Profile edits validate and persist only on successful save",() => {
            var a = New(); var s = new Store(); var g = Service(a,s);
            Assert(!g.Rename("X").Success && !g.Rename("<b>Name</b>").Success && !g.Rename("Na\nme").Success);
            s.Fail = true; Assert(!g.Rename("Luna").Success && a.Social.Profile.Username == "PlayerMochi");
            s.Fail = false; Assert(g.Rename(" Luna ").Success && New(s.Data).Social.Profile.Username == "Luna");
        });
        Check("Snapshots cannot mutate live profiles, recipients, inventory or receipts",() => {
            var a = New(); a.Inventory.ClaimStarter(); var s = new Store(); Service(a,s).Send(Request()); var data = a.Snapshot();
            data.Social.Profile.Popularity = 999; data.Social.History[0].Quantity = 999; data.Social.Recipients[0].Collection.Stickers[0].QuantityOwned = 999;
            Assert(a.Social.Profile.Popularity == 0 && a.Social.History[0].Quantity == 10 && a.Social.Recipients[0].Collection.Stickers[0].QuantityOwned == 10);
        });
        Check("Competing service instances serialize stock validation and commit",() => {
            var a = New(); a.Inventory.ClaimStarter(); var s = new Store(); var g = Service(a,s); var h = Service(a,s);
            var first = Task.Factory.StartNew(() => g.Send(Request(40))); var second = Task.Factory.StartNew(() => h.Send(Request(40)));
            Task.WaitAll(first,second); Assert(first.Result.Success != second.Result.Success && a.Inventory.Quantity("bunny") == 12 && s.Writes == 1);
        });
        Check("Full receipt ledger rejects new gifts but still accepts committed retries",() => {
            var a = New(); a.Inventory.TryAddSticker("bunny",2000); var data = a.Snapshot(); data.Social.History = new GiftTransaction[SocialState.HistoryLimit];
            for (int i = 0; i < data.Social.History.Length; i++) data.Social.History[i] = new GiftTransaction { TransactionId = Guid.NewGuid().ToString("N"),
                SenderId = data.Social.Profile.PlayerId, ReceiverId = "local_a", StickerId = "bunny", Quantity = 1, PopularityValue = 1, Timestamp = 1 };
            a = New(data); var s = new Store(); var g = Service(a,s); Assert(!g.Send(Request()).Success);
            var r = Request(1); r.TransactionId = data.Social.History[0].TransactionId; Assert(g.Send(r).AlreadyDelivered && s.Writes == 0);
        });
        Check("Malformed duplicate receipts fail closed rather than silently losing idempotency",() => {
            var a = New(); a.Inventory.ClaimStarter(); var s = new Store(); Service(a,s).Send(Request()); var data = a.Snapshot();
            data.Social.History = new[] { data.Social.History[0],data.Social.History[0] }; bool rejected = false;
            try { New(data); } catch (ArgumentException) { rejected = true; } Assert(rejected);
        });
        return passed + " social scenarios passed.";
    }
    private static void Check(string label,Action test) { test(); passed++; Console.WriteLine("PASS " + label); }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Social assertion failed."); }
}
