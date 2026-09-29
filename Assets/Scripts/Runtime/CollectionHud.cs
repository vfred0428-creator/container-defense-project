using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    public sealed partial class MatchHud
    {
        private bool collectionOpen, stickerTab;
        private CharacterId collectionCharacter;
        private string previewSkin, previewSticker = "bunny";
        private Vector2 skinScroll, stickerScroll;
        private StickerIcons stickerIcons;
        public void OpenCollection(CharacterId character)
        {
            collectionOpen = true; stickerTab = false; collectionCharacter = character;
            previewSkin = session.Inventory.Equipped(character); skinScroll = Vector2.zero;
        }
        public void ShowStickers() { collectionOpen = true; stickerTab = true; }
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
                session.Collections.StarterSkins.Length + " skin variants and " + session.Collections.StarterStickers.Length + " sticker stacks. Free, once per account.",small,muted);
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
                if (Button(new Rect(38,230 + i * 61,198,49),d.Name + (session.Account.IsUnlocked(d.Id) ? "" : "  /  LV. " + d.UnlockLevel),
                    collectionCharacter == d.Id ? gold : muted))
                { collectionCharacter = d.Id; previewSkin = session.Inventory.Equipped(d.Id); skinScroll = Vector2.zero; }
            }
            var choices = System.Array.FindAll(session.Collections.Skins,d => d.CharacterId == collectionCharacter);
            float available = width - 682, cardWidth = (available - 18) / 2;
            Rect viewport = new Rect(268,214,available,areaHeight);
            skinScroll = GUI.BeginScrollView(viewport,skinScroll,new Rect(0,0,available - 18,Mathf.Max(areaHeight - 1,((choices.Length + 1) / 2) * 367)));
            for (int i = 0; i < choices.Length; i++)
            {
                var d = choices[i]; bool owned = session.Inventory.OwnsSkin(d.SkinId);
                Rect card = new Rect((i % 2) * cardWidth,(i / 2) * 367,cardWidth - 12,352);
                Box(card,previewSkin == d.SkinId ? new Color(.24f,.22f,.32f) : panel);
                float size = Mathf.Min(card.width - 20,210);
                GUI.DrawTexture(new Rect(card.x + (card.width - size) / 2,card.y + 10,size,size),session.Arena.Portraits.Get(d.SkinId),ScaleMode.ScaleToFit,true);
                Label(new Rect(card.x + 16,card.y + 222,card.width - 32,33),d.Name,heading,Color.white);
                Label(new Rect(card.x + 16,card.y + 259,card.width - 32,25),d.Rarity + "  /  " + d.CharismaValue + " Charisma",small,muted);
                string state = session.Inventory.Equipped(collectionCharacter) == d.SkinId ? "EQUIPPED" : owned ? "OWNED" : "NOT OWNED";
                if (Button(new Rect(card.x + 12,card.y + 298,card.width - 24,40),state,previewSkin == d.SkinId ? gold : muted)) previewSkin = d.SkinId;
            }
            GUI.EndScrollView();
            var skin = session.Collections.Skin(previewSkin);
            if (skin == null) return;
            float x = width - 388;
            Box(new Rect(x,214,364,areaHeight),panel);
            GUI.DrawTexture(new Rect(x + 72,220,220,220),session.Arena.Portraits.Get(skin.SkinId),ScaleMode.ScaleToFit,true);
            Label(new Rect(x + 22,448,320,34),collectionCharacter + " / " + skin.Name,heading,gold);
            Label(new Rect(x + 22,490,320,35),skin.Rarity + "  /  +" + skin.CharismaValue + " Charisma when owned",small,muted);
            Label(new Rect(x + 22,532,320,62),session.Characters.Get(collectionCharacter).Description,body,Color.white);
            bool unlocked = session.Account.IsUnlocked(collectionCharacter), ownedPreview = session.Inventory.OwnsSkin(skin.SkinId);
            bool isEquipped = session.Inventory.Equipped(collectionCharacter) == skin.SkinId;
            string action = !unlocked ? "CHARACTER UNLOCKS AT LV. " + session.Characters.Get(collectionCharacter).UnlockLevel :
                !ownedPreview ? "CLAIM STARTER PACK FIRST" : isEquipped ? "EQUIPPED" : "EQUIP SKIN";
            if (Button(new Rect(x + 22,616,320,58),action,gold,unlocked && ownedPreview && !isEquipped)) session.EquipSkin(collectionCharacter,skin.SkinId);
            Label(new Rect(x + 22,690,320,35),"Appearance saved for this character.",small,muted);
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
        }
    }
}
