using System;
using System.Collections.Generic;

namespace ContainerDefense.Domain
{
    // Your own board, TFT style: a 12 x 7 grid around your house. The house sits top-centre, a path runs
    // from the door to the bottom edge, and eight build pads ring the house. Decorations are cosmetic only.
    public static class YardLayout
    {
        public const int Width = 12, Height = 7, PadCount = 8;
        public static readonly string[] DecorationIds = { "prop_crate","prop_barrel","prop_plant","prop_lamp","prop_cone","prop_pallets" };
        // Pad cells around the house: two either side of it, four along the front.
        private static readonly int[,] Pads = { { 1,1 },{ 10,1 },{ 1,3 },{ 10,3 },{ 2,5 },{ 4,5 },{ 7,5 },{ 9,5 } };
        public static int PadX(int pad) { return Pads[pad,0]; }
        public static int PadY(int pad) { return Pads[pad,1]; }
        public static bool IsHouse(int x,int y) { return x >= 3 && x <= 8 && y >= 0 && y <= 2; }
        public static bool IsPath(int x,int y) { return (x == 5 || x == 6) && y >= 3; }
        public static int PadAt(int x,int y) { for (int i = 0; i < PadCount; i++) if (Pads[i,0] == x && Pads[i,1] == y) return i; return -1; }
        // Decorations may never cover the house, the door path or a build pad.
        public static bool CanDecorate(int x,int y)
        { return x >= 0 && y >= 0 && x < Width && y < Height && !IsHouse(x,y) && !IsPath(x,y) && PadAt(x,y) < 0; }
        public static bool KnownDecoration(string id) { return Array.IndexOf(DecorationIds,id) >= 0; }
        // Keeps only valid, non-overlapping items (at most one per cell, capped at 24).
        public static YardData Normalize(YardData data)
        {
            var result = new List<YardItem>(); var used = new HashSet<int>();
            if (data != null && data.Items != null)
                foreach (var item in data.Items) {
                    if (item == null || !KnownDecoration(item.Prop) || !CanDecorate(item.X,item.Y) || !used.Add(item.Y * Width + item.X)) continue;
                    result.Add(new YardItem { Prop = item.Prop,X = item.X,Y = item.Y });
                    if (result.Count >= 24) break;
                }
            return new YardData { Items = result.ToArray() };
        }
        public static YardData Starter()
        {
            return Normalize(new YardData { Items = new[] {
                new YardItem { Prop = "prop_plant",X = 2,Y = 0 },new YardItem { Prop = "prop_plant",X = 9,Y = 0 },
                new YardItem { Prop = "prop_lamp",X = 4,Y = 3 },new YardItem { Prop = "prop_lamp",X = 7,Y = 3 },
                new YardItem { Prop = "prop_crate",X = 0,Y = 6 },new YardItem { Prop = "prop_barrel",X = 11,Y = 6 } } });
        }
        // Deterministic random preset for bots.
        public static YardData RandomPreset(int seed)
        {
            var rng = new Random(seed); var items = new List<YardItem>();
            for (int tries = 0; tries < 40 && items.Count < 7; tries++) {
                int x = rng.Next(Width), y = rng.Next(Height);
                if (CanDecorate(x,y)) items.Add(new YardItem { Prop = DecorationIds[rng.Next(DecorationIds.Length)],X = x,Y = y });
            }
            return Normalize(new YardData { Items = items.ToArray() });
        }
    }
    [Serializable] public sealed class YardItem { public string Prop; public int X, Y; }
    [Serializable] public sealed class YardData
    {
        public YardItem[] Items = new YardItem[0];
        public YardData Copy() { return YardLayout.Normalize(this); }
    }
}
