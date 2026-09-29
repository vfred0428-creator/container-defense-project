using System;
using System.Collections.Generic;

namespace ContainerDefense.Domain
{
    [Serializable]
    public sealed class EquippedSkin
    {
        public CharacterId CharacterId;
        public string SkinId;
    }
    [Serializable]
    public sealed class CollectionData
    {
        public string[] OwnedSkins = new string[0];
        public EquippedSkin[] EquippedSkins = new EquippedSkin[0];
        public StickerStack[] Stickers = new StickerStack[0];
        public bool StarterClaimed;
        public long Charisma;
    }
    // Local inventory authority for the prototype. Rendering never changes combat data.
    public sealed class InventorySystem
    {
        private readonly CollectionCatalog catalog;
        private readonly HashSet<string> owned = new HashSet<string>();
        private readonly Dictionary<string,long> quantities = new Dictionary<string,long>();
        private readonly string[] equipped = new string[7];
        public bool StarterClaimed { get; private set; }
        public InventorySystem(CollectionData saved, CollectionCatalog definitions)
        {
            if (definitions == null) throw new ArgumentNullException("definitions");
            catalog = definitions;
            saved = saved ?? new CollectionData(); StarterClaimed = saved.StarterClaimed;
            foreach (var id in saved.OwnedSkins ?? new string[0]) if (CollectionCatalog.Key(id)) owned.Add(id);
            for (int i = 0; i < 7; i++) { equipped[i] = catalog.DefaultSkin((CharacterId)i); owned.Add(equipped[i]); }
            foreach (var s in saved.Stickers ?? new StickerStack[0])
                if (s != null && CollectionCatalog.Key(s.StickerId) && s.QuantityOwned > 0)
                { long old; quantities.TryGetValue(s.StickerId,out old); quantities[s.StickerId] = Math.Max(old,s.QuantityOwned); }
            foreach (var e in saved.EquippedSkins ?? new EquippedSkin[0])
            {
                if (e == null) continue; var skin = catalog.Skin(e.SkinId);
                if (skin != null && skin.CharacterId == e.CharacterId && OwnsSkin(e.SkinId)) equipped[(int)e.CharacterId] = e.SkinId;
            }
        }
        public bool OwnsSkin(string id) { return id != null && owned.Contains(id); }
        public string Equipped(CharacterId id) { return CharacterCatalog.Valid(id) ? equipped[(int)id] : null; }
        public long Quantity(string id) { long q; return id != null && quantities.TryGetValue(id,out q) ? q : 0; }
        public bool TryEquip(CharacterId character, string skinId)
        {
            var skin = catalog.Skin(skinId);
            if (skin == null || skin.CharacterId != character || !OwnsSkin(skinId)) return false;
            equipped[(int)character] = skinId; return true;
        }
        public bool TryGrantSkin(string id) { return catalog.Skin(id) != null && owned.Add(id); }
        public bool TryAddSticker(string id, long quantity)
        {
            if (catalog.Sticker(id) == null || quantity <= 0 || Quantity(id) > long.MaxValue - quantity) return false;
            quantities[id] = Quantity(id) + quantity; return true;
        }
        public bool TryRemoveSticker(string id, long quantity)
        {
            if (catalog.Sticker(id) == null || quantity <= 0 || Quantity(id) < quantity) return false;
            long remaining = Quantity(id) - quantity;
            if (remaining == 0) quantities.Remove(id); else quantities[id] = remaining;
            return true;
        }
        public bool ClaimStarter()
        {
            if (StarterClaimed) return false;
            foreach (var s in catalog.StarterStickers)
                if (Quantity(s.StickerId) > long.MaxValue - s.QuantityOwned) return false;
            foreach (var id in catalog.StarterSkins) TryGrantSkin(id);
            foreach (var s in catalog.StarterStickers) TryAddSticker(s.StickerId,s.QuantityOwned);
            StarterClaimed = true; return true;
        }
        public long SkinCharisma { get { long score = 0; foreach (var d in catalog.Skins) if (OwnsSkin(d.SkinId)) score += d.CharismaValue; return score; } }
        public long StickerCharisma { get { long score = 0; foreach (var d in catalog.Stickers) if (Quantity(d.StickerId) > 0) score += d.CharismaValue; return score; } }
        public long Charisma { get { return SkinCharisma + StickerCharisma; } }
        public int SkinsOwned { get { int n = 0; foreach (var d in catalog.Skins) if (OwnsSkin(d.SkinId)) n++; return n; } }
        public int StickerTypesOwned { get { int n = 0; foreach (var d in catalog.Stickers) if (Quantity(d.StickerId) > 0) n++; return n; } }
        public CollectionData Snapshot()
        {
            var skins = new List<string>(owned); skins.Sort(StringComparer.Ordinal);
            var stacks = new List<StickerStack>(); var keys = new List<string>(quantities.Keys); keys.Sort(StringComparer.Ordinal);
            foreach (var id in keys) stacks.Add(new StickerStack { StickerId = id, QuantityOwned = quantities[id] });
            var worn = new EquippedSkin[7];
            for (int i = 0; i < 7; i++) worn[i] = new EquippedSkin { CharacterId = (CharacterId)i, SkinId = equipped[i] };
            return new CollectionData { OwnedSkins = skins.ToArray(), EquippedSkins = worn, Stickers = stacks.ToArray(), StarterClaimed = StarterClaimed, Charisma = Charisma };
        }
    }
}
