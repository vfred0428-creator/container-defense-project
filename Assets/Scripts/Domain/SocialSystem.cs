using System;
using System.Collections.Generic;
using System.IO;

namespace ContainerDefense.Domain
{
    [Serializable]
    public sealed class ProfileData
    {
        public string PlayerId;
        public string Username = "PlayerMochi";
        public long Popularity, Matches, Wins;
    }
    [Serializable]
    public sealed class LocalRecipient
    {
        public string PlayerId, Username;
        public CollectionData Collection;
        public long Popularity;
    }
    [Serializable]
    public sealed class GiftTransaction
    {
        public string TransactionId, SenderId, ReceiverId, StickerId;
        public long Quantity, Timestamp, PopularityValue;
    }
    [Serializable]
    public sealed class SocialData
    {
        public ProfileData Profile;
        public LocalRecipient[] Recipients;
        public GiftTransaction[] History;
        public int ReadThrough;
    }
    public sealed class GiftRequest
    {
        public string TransactionId, ReceiverId, StickerId;
        public long Quantity;
    }
    public sealed class GiftResult
    {
        public bool Success, AlreadyDelivered;
        public string Message;
        public static GiftResult Fail(string message) { return new GiftResult { Message = message }; }
    }
    public interface IGiftService { GiftResult Send(GiftRequest request); }

    public static class SocialState
    {
        public const int HistoryLimit = 1000;
        public static bool ValidName(string name)
        {
            if (name == null || name.Length < 2 || name.Length > 20 || name != name.Trim()) return false;
            foreach (char c in name) if (char.IsControl(c) || c == '<' || c == '>') return false;
            return true;
        }
        public static SocialData Copy(SocialData source, CollectionCatalog catalog)
        {
            source = source ?? new SocialData();
            var p = source.Profile ?? new ProfileData();
            var result = new SocialData { Profile = new ProfileData {
                PlayerId = string.IsNullOrEmpty(p.PlayerId) ? Guid.NewGuid().ToString("N") : p.PlayerId,
                Username = ValidName(p.Username) ? p.Username : "PlayerMochi",
                Popularity = Math.Max(0,p.Popularity), Matches = Math.Max(0,p.Matches),
                Wins = Math.Max(0,Math.Min(p.Matches,p.Wins)) } };
            if (!CollectionCatalog.Key(result.Profile.PlayerId)) throw new ArgumentException("Invalid profile identity.");
            var peers = source.Recipients ?? new[] {
                new LocalRecipient { PlayerId = "local_a", Username = "Practice inbox A" },
                new LocalRecipient { PlayerId = "local_b", Username = "Practice inbox B" } };
            if (peers.Length > 16) throw new ArgumentException("Too many local recipients.");
            var ids = new HashSet<string>(StringComparer.Ordinal); ids.Add(result.Profile.PlayerId);
            result.Recipients = new LocalRecipient[peers.Length];
            for (int i = 0; i < peers.Length; i++)
            {
                var r = peers[i];
                if (r == null || !CollectionCatalog.Key(r.PlayerId) || !ids.Add(r.PlayerId)) throw new ArgumentException("Invalid recipient identity.");
                result.Recipients[i] = new LocalRecipient { PlayerId = r.PlayerId, Username = ValidName(r.Username) ? r.Username : "Local inbox",
                    Popularity = Math.Max(0,r.Popularity), Collection = new InventorySystem(r.Collection,catalog).Snapshot() };
            }
            var history = source.History ?? new GiftTransaction[0];
            if (history.Length > HistoryLimit) throw new ArgumentException("Gift history exceeds capacity.");
            result.History = new GiftTransaction[history.Length]; var transactions = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < history.Length; i++)
            {
                var g = history[i]; Guid transaction;
                if (g == null || !Guid.TryParseExact(g.TransactionId,"N",out transaction) || g.TransactionId != transaction.ToString("N") || !transactions.Add(g.TransactionId) ||
                    !ids.Contains(g.SenderId) || !ids.Contains(g.ReceiverId) || g.SenderId == g.ReceiverId ||
                    !CollectionCatalog.Key(g.StickerId) || g.Quantity <= 0 || g.PopularityValue < 0 || g.Timestamp < 0 || g.Timestamp > 253402300799L)
                    throw new ArgumentException("Invalid gift receipt. Preserve the save for recovery.");
                result.History[i] = new GiftTransaction { TransactionId = g.TransactionId, SenderId = g.SenderId, ReceiverId = g.ReceiverId,
                    StickerId = g.StickerId, Quantity = g.Quantity, Timestamp = g.Timestamp, PopularityValue = g.PopularityValue };
            }
            result.ReadThrough = Math.Max(0,Math.Min(history.Length,source.ReadThrough));
            return result;
        }
        public static int Unread(SocialData state)
        {
            int count = 0;
            for (int i = state.ReadThrough; i < state.History.Length; i++) if (state.History[i].ReceiverId == state.Profile.PlayerId) count++;
            return count;
        }
        public static string Name(SocialData state, string id)
        {
            if (state.Profile.PlayerId == id) return state.Profile.Username;
            foreach (var r in state.Recipients) if (r.PlayerId == id) return r.Username;
            return "Unknown profile";
        }
    }

    // This local authority stores BOTH sides in one account envelope. A cloud adapter must
    // authenticate the caller and commit inventories + receipt in one server transaction.
    public sealed class LocalGiftService : IGiftService
    {
        private readonly AccountProgression account;
        private readonly CollectionCatalog catalog;
        private readonly ISaveService saves;
        private readonly string senderId;
        private readonly Func<long> clock;
        public LocalGiftService(AccountProgression owner, CollectionCatalog definitions, ISaveService storage, string authenticatedSender, Func<long> utcSeconds)
        {
            if (owner == null || definitions == null || storage == null || utcSeconds == null) throw new ArgumentNullException("Gift dependencies");
            account = owner; catalog = definitions; saves = storage; senderId = authenticatedSender; clock = utcSeconds;
        }
        public GiftResult Send(GiftRequest request)
        {
            lock (account.SocialGate)
            {
                Guid id;
                if (request == null || !Guid.TryParseExact(request.TransactionId,"N",out id)) return GiftResult.Fail("Invalid transaction ID.");
                request = new GiftRequest { TransactionId = id.ToString("N"), ReceiverId = request.ReceiverId, StickerId = request.StickerId, Quantity = request.Quantity };
                var next = account.Snapshot(); var state = next.Social;
                // Replay is checked before stock/capacity: the first delivery already spent it.
                foreach (var receipt in state.History) if (receipt.TransactionId == request.TransactionId)
                {
                    bool same = receipt.SenderId == senderId && receipt.ReceiverId == request.ReceiverId && receipt.StickerId == request.StickerId && receipt.Quantity == request.Quantity;
                    return same ? new GiftResult { Success = true, AlreadyDelivered = true, Message = "Already delivered. No additional stickers spent." } : GiftResult.Fail("Transaction ID already used for another gift.");
                }
                var sticker = catalog.Sticker(request.StickerId);
                if (request.Quantity <= 0 || sticker == null || sticker.GiftValue <= 0) return GiftResult.Fail("Choose a valid sticker and a positive whole quantity.");
                if (senderId == request.ReceiverId) return GiftResult.Fail("Choose another recipient.");
                LocalRecipient sender = Find(state,senderId), receiver = Find(state,request.ReceiverId);
                bool fromOwner = senderId == state.Profile.PlayerId, toOwner = request.ReceiverId == state.Profile.PlayerId;
                if ((!fromOwner && sender == null) || (!toOwner && receiver == null)) return GiftResult.Fail("Unknown sender or recipient.");
                if (state.History.Length >= SocialState.HistoryLimit) return GiftResult.Fail("Local gift history is full. No stickers were spent.");
                var source = new InventorySystem(fromOwner ? next.Collection : sender.Collection,catalog);
                var target = new InventorySystem(toOwner ? next.Collection : receiver.Collection,catalog);
                long popularity = toOwner ? state.Profile.Popularity : receiver.Popularity, value;
                try { value = checked(request.Quantity * sticker.GiftValue); popularity = checked(popularity + value); }
                catch (OverflowException) { return GiftResult.Fail("Gift exceeds the recipient's Popularity limit."); }
                if (!source.TryRemoveSticker(request.StickerId,request.Quantity)) return GiftResult.Fail("Not enough stickers.");
                if (!target.TryAddSticker(request.StickerId,request.Quantity)) return GiftResult.Fail("Recipient sticker capacity exceeded.");
                if (fromOwner) next.Collection = source.Snapshot(); else sender.Collection = source.Snapshot();
                if (toOwner) { next.Collection = target.Snapshot(); state.Profile.Popularity = popularity; }
                else { receiver.Collection = target.Snapshot(); receiver.Popularity = popularity; }
                long timestamp = clock();
                if (timestamp < 0 || timestamp > 253402300799L) return GiftResult.Fail("Gift clock unavailable.");
                var list = new List<GiftTransaction>(state.History);
                list.Add(new GiftTransaction { TransactionId = request.TransactionId, SenderId = senderId, ReceiverId = request.ReceiverId,
                    StickerId = request.StickerId, Quantity = request.Quantity, Timestamp = timestamp, PopularityValue = value });
                state.History = list.ToArray();
                return Commit(next,"Gift delivered to " + SocialState.Name(state,request.ReceiverId) + ".");
            }
        }
        public GiftResult Rename(string username)
        {
            lock (account.SocialGate)
            {
                if (senderId != account.Social.Profile.PlayerId) return GiftResult.Fail("Profile access denied.");
                username = username == null ? null : username.Trim();
                if (!SocialState.ValidName(username)) return GiftResult.Fail("Use 2–20 characters, without control characters or angle brackets.");
                var next = account.Snapshot(); next.Social.Profile.Username = username;
                return Commit(next,"Profile saved.");
            }
        }
        public GiftResult MarkRead()
        {
            lock (account.SocialGate)
            {
                if (senderId != account.Social.Profile.PlayerId) return GiftResult.Fail("Inbox access denied.");
                var next = account.Snapshot(); next.Social.ReadThrough = next.Social.History.Length;
                return Commit(next,"Inbox read.");
            }
        }
        private GiftResult Commit(AccountData next, string message)
        {
            if (!saves.CanWrite) return GiftResult.Fail("Saving unavailable. No changes made.");
            try { saves.Save(next); }
            catch (IOException) { return GiftResult.Fail("Could not save. No changes made; retry this gift."); }
            catch (UnauthorizedAccessException) { return GiftResult.Fail("Could not save. No changes made; retry this gift."); }
            account.AdoptSocial(next);
            return new GiftResult { Success = true, Message = message };
        }
        private static LocalRecipient Find(SocialData state,string id)
        { foreach (var r in state.Recipients) if (r.PlayerId == id) return r; return null; }
    }
}
