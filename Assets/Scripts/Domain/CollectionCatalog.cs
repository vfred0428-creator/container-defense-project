using System;
using System.Collections.Generic;

namespace ContainerDefense.Domain
{
    public enum CosmeticRarity { Common, Rare, Epic, Legendary, Limited }
    [Serializable]
    public sealed class SkinDefinition
    {
        public string SkinId, Name;
        public CharacterId CharacterId;
        public CosmeticRarity Rarity;
        public int CharismaValue;
        public bool Default;
        public string CoatHex, AccentHex;
        public SkinDefinition Copy() { return (SkinDefinition)MemberwiseClone(); }
    }
    [Serializable]
    public sealed class StickerDefinition
    {
        public string StickerId, Name, Icon;
        public CosmeticRarity Rarity;
        public int CharismaValue, GiftValue;
        public bool Animated, Limited;
        public StickerDefinition Copy() { return (StickerDefinition)MemberwiseClone(); }
    }
    [Serializable]
    public sealed class StickerStack
    {
        public string StickerId;
        public long QuantityOwned;
    }
    public sealed class CollectionCatalog
    {
        private readonly Dictionary<string,SkinDefinition> skins = new Dictionary<string,SkinDefinition>();
        private readonly Dictionary<string,StickerDefinition> stickers = new Dictionary<string,StickerDefinition>();
        private readonly SkinDefinition[] orderedSkins;
        private readonly StickerDefinition[] orderedStickers;
        private readonly string[] defaults = new string[7];
        private readonly string[] starterSkinIds;
        private readonly StickerStack[] starterStacks;
        public string[] StarterSkins { get { return (string[])starterSkinIds.Clone(); } }
        public StickerStack[] StarterStickers { get { return Array.ConvertAll(starterStacks,s => new StickerStack { StickerId = s.StickerId, QuantityOwned = s.QuantityOwned }); } }
        public CollectionCatalog(SkinDefinition[] skinData, StickerDefinition[] stickerData,
            string[] starterSkins, StickerStack[] starterStickers)
        {
            if (skinData == null || stickerData == null || starterSkins == null || starterStickers == null)
                throw new ArgumentException("Missing collection data.");
            orderedSkins = new SkinDefinition[skinData.Length]; orderedStickers = new StickerDefinition[stickerData.Length];
            for (int i = 0; i < skinData.Length; i++)
            {
                var d = skinData[i];
                if (d == null || !Key(d.SkinId) || string.IsNullOrWhiteSpace(d.Name) || !CharacterCatalog.Valid(d.CharacterId) ||
                    !Enum.IsDefined(typeof(CosmeticRarity),d.Rarity) || d.CharismaValue < 0 || skins.ContainsKey(d.SkinId) ||
                    (!d.Default && (!Hex(d.CoatHex) || !Hex(d.AccentHex)))) throw new ArgumentException("Invalid skin definition.");
                if (d.Default)
                {
                    if (defaults[(int)d.CharacterId] != null || d.CharismaValue != 0) throw new ArgumentException("Default skins must be unique and have zero Charisma.");
                    defaults[(int)d.CharacterId] = d.SkinId;
                }
                orderedSkins[i] = d.Copy(); skins.Add(d.SkinId,orderedSkins[i]);
            }
            foreach (var id in defaults) if (id == null) throw new ArgumentException("Each character needs a default skin.");
            for (int i = 0; i < stickerData.Length; i++)
            {
                var d = stickerData[i];
                if (d == null || !Key(d.StickerId) || string.IsNullOrWhiteSpace(d.Name) || string.IsNullOrWhiteSpace(d.Icon) ||
                    !Enum.IsDefined(typeof(CosmeticRarity),d.Rarity) || d.CharismaValue < 0 || d.GiftValue < 0 || stickers.ContainsKey(d.StickerId))
                    throw new ArgumentException("Invalid sticker definition.");
                orderedStickers[i] = d.Copy(); stickers.Add(d.StickerId,orderedStickers[i]);
            }
            starterSkinIds = (string[])starterSkins.Clone(); starterStacks = new StickerStack[starterStickers.Length];
            var unique = new HashSet<string>();
            foreach (var id in StarterSkins) if (Skin(id) == null || !unique.Add(id)) throw new ArgumentException("Invalid starter skin.");
            unique.Clear();
            for (int i = 0; i < starterStickers.Length; i++)
            {
                var s = starterStickers[i];
                if (s == null || Sticker(s.StickerId) == null || s.QuantityOwned <= 0 || !unique.Add(s.StickerId)) throw new ArgumentException("Invalid starter sticker.");
                starterStacks[i] = new StickerStack { StickerId = s.StickerId, QuantityOwned = s.QuantityOwned };
            }
        }
        public SkinDefinition Skin(string id) { SkinDefinition d; return id != null && skins.TryGetValue(id,out d) ? d.Copy() : null; }
        public StickerDefinition Sticker(string id) { StickerDefinition d; return id != null && stickers.TryGetValue(id,out d) ? d.Copy() : null; }
        public SkinDefinition[] Skins { get { return Array.ConvertAll(orderedSkins,d => d.Copy()); } }
        public StickerDefinition[] Stickers { get { return Array.ConvertAll(orderedStickers,d => d.Copy()); } }
        public string DefaultSkin(CharacterId id) { return CharacterCatalog.Valid(id) ? defaults[(int)id] : null; }
        public static bool Key(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 80) return false;
            foreach (char c in value) if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_') return false;
            return true;
        }
        private static bool Hex(string value)
        {
            if (value == null || value.Length != 6) return false;
            foreach (char c in value) if (!Uri.IsHexDigit(c)) return false;
            return true;
        }
        public static SkinDefinition[] DefaultSkins()
        {
            var result = new List<SkinDefinition>();
            for (int i = 0; i < 7; i++) result.Add(new SkinDefinition { SkinId = CharacterCatalog.Key((CharacterId)i) + "_default",
                CharacterId = (CharacterId)i, Name = "Default", Default = true });
            result.Add(new SkinDefinition { SkinId = "milo_night", CharacterId = CharacterId.Milo, Name = "Night Shift", Rarity = CosmeticRarity.Rare, CharismaValue = 75, CoatHex = "334878", AccentHex = "85DADD" });
            result.Add(new SkinDefinition { SkinId = "lumi_cloudy", CharacterId = CharacterId.Lumi, Name = "Cloudy", Rarity = CosmeticRarity.Epic, CharismaValue = 200, CoatHex = "D8B5EE", AccentHex = "FFFFFF" });
            result.Add(new SkinDefinition { SkinId = "kiko_red", CharacterId = CharacterId.Kiko, Name = "Red Gear", Rarity = CosmeticRarity.Rare, CharismaValue = 75, CoatHex = "E05464", AccentHex = "FFE09C" });
            return result.ToArray();
        }
        public static StickerDefinition[] DefaultStickers()
        {
            string[] ids = { "bunny", "star", "cat", "good", "heart" };
            string[] names = { "Bunny", "Lucky Star", "Cozy Cat", "Good Job!", "Heart" };
            var result = new StickerDefinition[5];
            for (int i = 0; i < 5; i++) result[i] = new StickerDefinition { StickerId = ids[i], Name = names[i], Icon = ids[i],
                Rarity = i == 3 ? CosmeticRarity.Epic : i == 0 ? CosmeticRarity.Rare : CosmeticRarity.Common,
                CharismaValue = i == 3 ? 50 : i == 0 ? 20 : 10, GiftValue = i == 3 ? 10 : 1 };
            return result;
        }
        public static string[] DefaultStarterSkins() { return new[] { "milo_night", "lumi_cloudy", "kiko_red" }; }
        public static StickerStack[] DefaultStarterStickers()
        {
            var ids = new[] { "bunny", "star", "cat", "good", "heart" }; long[] counts = { 52,24,17,7,38 };
            var result = new StickerStack[5];
            for (int i = 0; i < 5; i++) result[i] = new StickerStack { StickerId = ids[i], QuantityOwned = counts[i] };
            return result;
        }
        public static CollectionCatalog CreateDefault() { return new CollectionCatalog(DefaultSkins(),DefaultStickers(),DefaultStarterSkins(),DefaultStarterStickers()); }
    }
}
