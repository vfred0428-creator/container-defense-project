using System;

namespace ContainerDefense.Domain
{
    // What the resident hangs on the room walls: a keepsake for their passive.
    public enum Keepsake { CoinJar, Shield, Target, Pillow, Sneaker, PriceTag, Wrench }

    // One resident's touches over the shared painted room: their own poster, a keepsake for their passive and a
    // pennant in their colours. Purely visual; nothing here changes the rules.
    public sealed class RoomTheme
    {
        public CharacterId Owner { get; private set; }
        // Colours as 0xRRGGBB. Accent: poster frame and pennant. Paper: poster background. Ink: text on the accent.
        public int Accent { get; private set; }
        public int Paper { get; private set; }
        public int Ink { get; private set; }

        private static readonly RoomTheme[] themes = {
            new RoomTheme(CharacterId.Milo,0xC9873E,0xF6E3C4,0xFFFFFF),
            new RoomTheme(CharacterId.Lumi,0x5E9FE0,0xDCEEFF,0xFFFFFF),
            new RoomTheme(CharacterId.Kiko,0x2B3A6B,0xFBE7A1,0xFFD34A),
            new RoomTheme(CharacterId.Nori,0xE8649A,0xFFE0EC,0xFFFFFF),
            new RoomTheme(CharacterId.Pip,0x3E9E66,0xDDF3E2,0xFFFFFF),
            new RoomTheme(CharacterId.Mochi,0xD9A520,0xFFF3CF,0xFFFFFF),
            new RoomTheme(CharacterId.Yume,0x7A4CC8,0xEBDDFF,0xFFFFFF)
        };

        private RoomTheme(CharacterId owner,int accent,int paper,int ink)
        { Owner = owner; Accent = accent; Paper = paper; Ink = ink; }

        public static RoomTheme For(CharacterId id)
        {
            if (!CharacterCatalog.Valid(id)) throw new ArgumentOutOfRangeException("id");
            return themes[(int)id];
        }
        // Keepsakes are listed in PersonalPassive order: coin jar for gold, shield for the door, and so on.
        public static Keepsake KeepsakeFor(PersonalPassive passive) { return (Keepsake)(int)passive; }
    }
}
