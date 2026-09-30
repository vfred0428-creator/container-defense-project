using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    public sealed partial class MatchHud
    {
        private bool collectionOpen, stickerTab;
        private CharacterId collectionCharacter;
        private string previewSkin, previewSticker = "bunny";
        private StickerIcons stickerIcons;
        public void OpenCollection(CharacterId character)
        {
            rankedOpen = false; socialOpen = false; collectionOpen = true; stickerTab = false; collectionCharacter = character;
            previewSkin = session.Inventory.Equipped(character);
        }
        public void ShowStickers() { rankedOpen = false; socialOpen = false; collectionOpen = true; stickerTab = true; }
        public void CloseCollection() { collectionOpen = false; }
        private void CollectionScreen()
        {
            MenuBackground();
            var inventory = session.Inventory; var area = Cut.Inset(safe,M);
            Header(ref area,"COLLECTION","Cosmetics only. Passives never change.",HudTheme.Number(inventory.Charisma) + " Charisma",inventory.SkinsOwned + " skins  ·  " + inventory.StickerTypesOwned + " sticker types");
            var tabs = Cut.Top(ref area,Touch,G);
            if (HudTheme.Button(Cut.Left(ref tabs,200,G),"BACK",ButtonKind.Secondary,true,false,HudTheme.Body)) collectionOpen = false;
            if (HudTheme.Button(Cut.Left(ref tabs,220,G),"SKINS",ButtonKind.Secondary,true,!stickerTab,HudTheme.Body)) stickerTab = false;
            if (HudTheme.Button(Cut.Left(ref tabs,220,G),"STICKERS",ButtonKind.Secondary,true,stickerTab,HudTheme.Body)) stickerTab = true;
            // Footer: starter pack and save state.
            var footer = Cut.Bottom(ref area,Touch + 32,G); HudTheme.Panel(footer); var f = Cut.Inset(footer,16);
            var claim = Cut.Right(ref f,340,G);
            if (session.SaveDirty) { if (HudTheme.Button(claim,"RETRY SAVE",ButtonKind.Play,true,false,HudTheme.Body)) session.PersistAccount(); }
            else if (HudTheme.Button(claim,inventory.StarterClaimed ? "COLLECTED" : "CLAIM STARTER PACK",ButtonKind.Primary,!inventory.StarterClaimed)) session.ClaimStarterCollection();
            HudTheme.Text(Cut.Top(ref f,f.height / 2),inventory.StarterClaimed ? "Starter collection claimed" : "Free starter sticker pack",HudTheme.Body,HudTheme.Gold,true);
            HudTheme.Text(f,session.SaveDirty ? session.SaveStatus : inventory.StarterClaimed ? "Charisma: " + inventory.SkinCharisma + " from skins + " + inventory.StickerCharisma + " from sticker types" : "Once per account.",HudTheme.Label,session.SaveDirty ? HudTheme.Bad : HudTheme.Muted);
            if (stickerTab) StickerCollection(area); else SkinCollection(area);
        }
        private void SkinCollection(Rect area)
        {
            // Character selector row: portrait tabs, one per resident.
            var selector = Cut.Top(ref area,Touch,G);
            var tabs = Cut.Row(Cut.Left(ref selector,Mathf.Min(selector.width,7 * Touch + 6 * G)),7,G);
            for (int i = 0; i < 7; i++) {
                var d = session.Characters.Get((CharacterId)i); var r = tabs[i];
                HudTheme.Card(r); var old = GUI.color; if (!session.Account.IsUnlocked(d.Id)) GUI.color = new Color(.45f,.47f,.55f,1);
                Portrait(Cut.Inset(r,4),session.Collections.DefaultSkin(d.Id),true); GUI.color = old;
                if (collectionCharacter == d.Id) HudTheme.Ring(r);
                if (Hit(r,"Collection " + d.Name)) { collectionCharacter = d.Id; previewSkin = session.Inventory.Equipped(d.Id); }
            }
            var character = session.Characters.Get(collectionCharacter);
            var skin = session.Collections.Skin(previewSkin ?? session.Collections.DefaultSkin(collectionCharacter));
            if (skin == null) return;
            var details = Cut.Right(ref area,Mathf.Min(560,area.width * .4f),G);
            HudTheme.Panel(area);
            float size = Mathf.Min(area.width,area.height) - 48;
            Portrait(Cut.Center(area,size,size),skin.SkinId,false);
            HudTheme.Panel(details); var inner = Cut.Inset(details,28);
            bool unlocked = session.Account.IsUnlocked(collectionCharacter);
            if (HudTheme.Button(Cut.Bottom(ref inner,Touch,G),unlocked ? "SELECT CHARACTER" : "UNLOCKS AT LEVEL " + character.UnlockLevel,ButtonKind.Play,unlocked,false,HudTheme.Body))
            { session.SelectCharacter(collectionCharacter); collectionOpen = false; }
            HudTheme.Text(Cut.Top(ref inner,68,4),character.Name,HudTheme.Title,HudTheme.Ink,true);
            HudTheme.Text(Cut.Top(ref inner,84,G),character.Description,HudTheme.Body,HudTheme.Gold,true,TextAnchor.UpperLeft,true);
            HudTheme.Text(Cut.Top(ref inner,72,G),unlocked ? "Unlocked permanently." : "Reach account level " + character.UnlockLevel + " to unlock.",HudTheme.Body,HudTheme.Ink,false,TextAnchor.UpperLeft,true);
            HudTheme.Text(Cut.Top(ref inner,64),"Default skin  ·  Common\nCosmetics never change passives.",HudTheme.Label,HudTheme.Muted,false,TextAnchor.UpperLeft,true);
        }
        private void StickerCollection(Rect area)
        {
            if (stickerIcons == null) stickerIcons = new StickerIcons();
            var definitions = session.Collections.Stickers;
            var detail = Cut.Bottom(ref area,Mathf.Min(200,area.height * .4f),G);
            int columns = Mathf.Max(1,Mathf.Min(definitions.Length,5));
            var grid = Cut.Row(Cut.Top(ref area,Mathf.Min(area.height,320)),columns,G);
            for (int i = 0; i < definitions.Length && i < columns; i++) {
                var d = definitions[i]; long quantity = session.Inventory.Quantity(d.StickerId); var card = grid[i];
                HudTheme.Panel(card,false); if (previewSticker == d.StickerId) HudTheme.Ring(card);
                var inner = Cut.Inset(card,16);
                var old = GUI.color; if (quantity == 0) GUI.color = new Color(.5f,.5f,.6f);
                float iconSize = Mathf.Min(inner.width,inner.height - 100);
                GUI.DrawTexture(Cut.Center(Cut.Top(ref inner,iconSize,8),iconSize,iconSize),stickerIcons.Get(d.Icon),ScaleMode.ScaleToFit,true); GUI.color = old;
                HudTheme.Text(Cut.Top(ref inner,40),d.Name,HudTheme.Body,HudTheme.Ink,true,TextAnchor.MiddleCenter);
                HudTheme.Text(inner,"x" + HudTheme.Number(quantity),HudTheme.CardTitle,HudTheme.Gold,true,TextAnchor.MiddleCenter);
                if (Hit(card,"Sticker " + d.Name)) previewSticker = d.StickerId;
            }
            var selected = session.Collections.Sticker(previewSticker);
            if (selected == null) return;
            HudTheme.Panel(detail); var r = Cut.Inset(detail,20);
            if (HudTheme.Button(Cut.Center(Cut.Right(ref r,300,G),300,Touch),"GIFT STICKERS",ButtonKind.Primary,true,false,HudTheme.Body)) { giftSticker = selected.StickerId; OpenSocial(1); }
            float icon = Mathf.Min(r.height,150);
            GUI.DrawTexture(Cut.Center(Cut.Left(ref r,icon,24),icon,icon),stickerIcons.Get(selected.Icon),ScaleMode.ScaleToFit,true);
            HudTheme.Text(Cut.Top(ref r,44),selected.Name + "  ·  " + selected.Rarity,HudTheme.CardTitle,HudTheme.Gold,true);
            HudTheme.Text(Cut.Top(ref r,36),"Owned x" + HudTheme.Number(session.Inventory.Quantity(selected.StickerId)) + "  ·  worth " + selected.CharismaValue + " Charisma",HudTheme.Body,HudTheme.Ink);
            HudTheme.Text(r,(selected.Animated ? "Animated" : "Static") + "  ·  " + (selected.Limited ? "Limited" : "Standard") + "  ·  each type counts once toward Charisma",HudTheme.Label,HudTheme.Muted,false,TextAnchor.UpperLeft,true);
        }
    }
}
