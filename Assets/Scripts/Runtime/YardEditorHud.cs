using System.Collections.Generic;
using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    // "My Yard": decorate the yard around your house on the board grid. Cosmetic only, free, saved in the
    // profile. Decorations can never cover the house, the door path or a build pad.
    public sealed partial class MatchHud
    {
        private bool yardOpen;
        private string yardTool = "prop_plant";
        private YardData yardDraft;
        public void CloseYard() { yardOpen = false; }
        public void OpenYard() { yardOpen = true; rankedOpen = socialOpen = collectionOpen = false; yardDraft = session.Account.Yard; }
        private static readonly string[] YardLabels = { "CRATE","BARREL","PLANT","LAMP","CONE","PALLETS" };
        private void YardScreen()
        {
            MenuBackground();
            var area = Cut.Inset(safe,M);
            Header(ref area,"MY YARD","Decorate the yard around your house. Free and cosmetic; everyone sees it on your board.");
            // Tools: one per decoration, plus remove; then back and reset.
            var tools = Cut.Bottom(ref area,Touch,G);
            if (HudTheme.Button(Cut.Right(ref tools,220,G),"DONE",ButtonKind.Primary,true,false,HudTheme.Body)) yardOpen = false;
            if (HudTheme.Button(Cut.Right(ref tools,220,G),"RESET",ButtonKind.Secondary,true,false,HudTheme.Body)) { yardDraft = YardLayout.Starter(); session.SaveYard(yardDraft); }
            var row = Cut.Row(tools,YardLayout.DecorationIds.Length + 1,G);
            for (int i = 0; i < YardLayout.DecorationIds.Length; i++) {
                var id = YardLayout.DecorationIds[i];
                if (HudTheme.Button(row[i],"",ButtonKind.Secondary,true,yardTool == id)) yardTool = id;
                var tex = MenuArtwork.Get("Generated/" + id); if (tex != null) GUI.DrawTexture(Cut.Inset(row[i],12),tex,ScaleMode.ScaleToFit,true);
            }
            if (HudTheme.Button(row[row.Length - 1],"REMOVE",ButtonKind.Danger,true,yardTool == "")) yardTool = "";
            // The board grid, kept at the board's 12 x 7 shape and centred.
            float cell = Mathf.Min(area.width / YardLayout.Width,area.height / YardLayout.Height);
            var board = Cut.Center(area,cell * YardLayout.Width,cell * YardLayout.Height);
            HudTheme.Panel(board,false);
            var items = new List<YardItem>(yardDraft != null && yardDraft.Items != null ? yardDraft.Items : new YardItem[0]);
            bool changed = false;
            for (int y = 0; y < YardLayout.Height; y++) for (int x = 0; x < YardLayout.Width; x++) {
                var r = new Rect(board.x + x * cell,board.y + y * cell,cell,cell); var inner = Cut.Inset(r,3);
                if (YardLayout.IsHouse(x,y)) continue;
                if (YardLayout.IsPath(x,y)) { HudTheme.Fill(inner,HudTheme.Hex(0x6E6458,.9f),6); continue; }
                if (YardLayout.PadAt(x,y) >= 0) { HudTheme.Fill(Cut.Center(inner,inner.width * .8f,inner.height * .5f),HudTheme.Hex(0x6E7283),inner.width * .3f); continue; }
                HudTheme.Fill(inner,HudTheme.Hex(0x2E3242,.9f),6);
                int at = items.FindIndex(it => it.X == x && it.Y == y);
                if (at >= 0) { var tex = MenuArtwork.Get("Generated/" + items[at].Prop); if (tex != null) GUI.DrawTexture(Cut.Inset(inner,4),tex,ScaleMode.ScaleToFit,true); }
                if (!Hit(Cut.Inset(r,1),"Yard cell " + x + "," + y)) continue;
                if (at >= 0) items.RemoveAt(at);
                // Tapping replaces whatever was there (or clears it with REMOVE).
                if (yardTool != "") items.Add(new YardItem { Prop = yardTool,X = x,Y = y });
                changed = true;
            }
            // The house block itself, drawn once across its cells, with the path below its door.
            var houseRect = new Rect(board.x + 3 * cell,board.y,6 * cell,3 * cell);
            var house = MenuArtwork.Get("Generated/house_blue"); if (house != null) GUI.DrawTexture(Cut.Inset(houseRect,4),house,ScaleMode.ScaleToFit,true);
            if (changed) { yardDraft = YardLayout.Normalize(new YardData { Items = items.ToArray() }); session.SaveYard(yardDraft); }
        }
    }
}
