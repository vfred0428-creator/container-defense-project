using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    public sealed partial class MatchHud
    {
        private bool collectionOpen, stickerTab;
        private CharacterId collectionCharacter;
        private string previewSkin, previewSticker = "bunny";
        private Vector2 stickerScroll;
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
            MenuArtwork.Background(new Rect(0,0,width,height)); Overlay();
            var inventory = session.Inventory;
            Box(new Rect(24,24,width - 48,108),panel);
            Label(new Rect(48,43,650,60),"YOUR COLLECTION",title,Color.white);
            Label(new Rect(width - 395,40,340,32),inventory.Charisma.ToString("N0") + " CHARISMA",heading,gold);
            Label(new Rect(width - 395,83,340,24),inventory.SkinsOwned + " skins  /  " + inventory.StickerTypesOwned + " sticker types",small,muted);
            if (Button(new Rect(24,150,220,44),"<  CHARACTERS",muted)) collectionOpen = false;
            if (Button(new Rect(264,150,180,44),"SKINS",!stickerTab ? gold : muted)) stickerTab = false;
            if (Button(new Rect(456,150,180,44),"STICKERS",stickerTab ? gold : muted)) stickerTab = true;
            Label(new Rect(666,159,width - 690,32),"Cosmetics only. Your character's passive stays the same.",body,muted);
            if (stickerTab) StickerCollection(); else SkinCollection();
            float bottom = height - 146;
            Box(new Rect(24,bottom,width - 48,92),panel);
            Label(new Rect(48,bottom + 15,width - 440,30),inventory.StarterClaimed ? "STARTER COLLECTION CLAIMED" : "A LITTLE SOMETHING TO GET STARTED",heading,gold);
            Label(new Rect(48,bottom + 51,width - 440,27),inventory.StarterClaimed ?
                "Charisma: " + inventory.SkinCharisma + " from skins + " + inventory.StickerCharisma + " from sticker types. Duplicates add no Charisma." :
                session.Collections.StarterStickers.Length + " sticker stacks. Free, once per account.",small,muted);
            if (Button(new Rect(width - 350,bottom + 20,296,50),inventory.StarterClaimed ? "COLLECTED" : "CLAIM STARTER PACK",gold,!inventory.StarterClaimed)) session.ClaimStarterCollection();
            Label(new Rect(32,height - 39,width - 230,30),session.SaveStatus,small,session.SaveDirty ? red : muted);
            if (session.SaveDirty && Button(new Rect(width - 190,height - 43,160,32),"RETRY SAVE",gold)) session.PersistAccount();
        }
        private void SkinCollection()
        {
            float areaHeight = height - 374;
            Box(new Rect(24,214,226,areaHeight),panel);
            for (int i = 0; i < 7; i++)
            {
                var d = session.Characters.Get((CharacterId)i);
                if (Button(new Rect(38,230 + i * 61,198,49),d.Name + (session.Account.IsUnlocked(d.Id) ? "" : " / Lv. " + d.UnlockLevel),collectionCharacter == d.Id ? gold : muted))
                { collectionCharacter = d.Id; previewSkin = session.Inventory.Equipped(d.Id); }
            }
            var character = session.Characters.Get(collectionCharacter);
            var skin = session.Collections.Skin(previewSkin ?? session.Collections.DefaultSkin(collectionCharacter));
            if (skin == null) return;
            float previewWidth = width - 720;
            Box(new Rect(268,214,previewWidth,areaHeight),panel);
            float size = Mathf.Min(previewWidth - 48,areaHeight - 88);
            Portrait(new Rect(292 + (previewWidth - 48 - size) / 2,234,size,size),skin.SkinId,false);
            Label(new Rect(296,214 + areaHeight - 58,previewWidth - 48,36),character.Name + " / Default",heading,cream);
            float x = width - 428;
            Box(new Rect(x,214,404,areaHeight),panel);
            Label(new Rect(x + 24,240,356,44),character.Name,title,cream);
            Label(new Rect(x + 24,304,356,74),character.Description,heading,gold);
            bool unlocked = session.Account.IsUnlocked(collectionCharacter);
            Label(new Rect(x + 24,410,356,70),unlocked ? "Character unlocked permanently.\nDefault appearance equipped." : "Reach Account Level " + character.UnlockLevel + " to unlock this resident.",body,cream);
            Label(new Rect(x + 24,510,356,74),"Default skin / Common\nCosmetics do not change your passive.",body,muted);
            if (Button(new Rect(x + 24,214 + areaHeight - 84,356,52),unlocked ? "SELECT CHARACTER" : "LOCKED / LEVEL " + character.UnlockLevel,gold,unlocked))
            { session.SelectCharacter(collectionCharacter); collectionOpen = false; }
        }
        private void StickerCollection()
        {
            if (stickerIcons == null) stickerIcons = new StickerIcons();
            var definitions = session.Collections.Stickers;
            float cardWidth = (width - 66) / 5, gridHeight = height - 562;
            stickerScroll = GUI.BeginScrollView(new Rect(24,214,width - 48,gridHeight),stickerScroll,
                new Rect(0,0,width - 66,Mathf.Max(gridHeight - 1,((definitions.Length + 4) / 5) * 320)));
            for (int i = 0; i < definitions.Length; i++)
            {
                var d = definitions[i]; long quantity = session.Inventory.Quantity(d.StickerId);
                Rect card = new Rect(i % 5 * cardWidth,i / 5 * 320,cardWidth - 12,304);
                Box(card,previewSticker == d.StickerId ? new Color(.28f,.17f,.3f) : panel);
                float size = Mathf.Min(150,card.width - 30);
                Color previous = GUI.color; if (quantity == 0) GUI.color = new Color(.5f,.5f,.6f);
                GUI.DrawTexture(new Rect(card.x + (card.width - size) / 2,card.y + 6,size,size),stickerIcons.Get(d.Icon),ScaleMode.ScaleToFit,true); GUI.color = previous;
                Label(new Rect(card.x + 16,card.y + 166,card.width - 32,33),d.Name,heading,Color.white);
                Label(new Rect(card.x + 16,card.y + 208,card.width - 32,30),"x" + quantity.ToString("N0"),quantity > 999999999999L ? small : quantity > 999999999 ? body : number,gold);
                if (Button(new Rect(card.x + 12,card.y + 256,card.width - 24,36),"VIEW",muted)) previewSticker = d.StickerId;
            }
            GUI.EndScrollView();
            var selected = session.Collections.Sticker(previewSticker);
            if (selected == null) return;
            float y = height - 332;
            Box(new Rect(24,y,width - 48,164),panel);
            GUI.DrawTexture(new Rect(45,y + 12,138,138),stickerIcons.Get(selected.Icon),ScaleMode.ScaleToFit,true);
            Label(new Rect(211,y + 17,500,34),selected.Name + "  /  " + selected.Rarity,heading,gold);
            Label(new Rect(211,y + 62,width - 260,27),"Owned: x" + session.Inventory.Quantity(selected.StickerId).ToString("N0") + "  /  Collection value: " + selected.CharismaValue + " Charisma",body,Color.white);
            Label(new Rect(211,y + 104,width - 260,46),(selected.Animated ? "Animated" : "Static") + "  /  " + (selected.Limited ? "Limited collection" : "Standard collection") +
                "\nEach owned sticker type counts once toward Charisma.",small,muted);
            if (Button(new Rect(width - 300,y + 16,252,42),"GIFT STICKERS",new Color(1,.52f,.71f)))
            { giftSticker = selected.StickerId; OpenSocial(1); }
        }
    }
}
