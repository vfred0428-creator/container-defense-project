using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    public sealed partial class MatchHud
    {
        private CharacterId inspected = CharacterId.Milo;
        private CharacterId lastSelected;
        private bool inspectedInitialized;
        private static readonly string[] passiveNames = { "Pocket Gold", "Strong Doors", "More Firepower", "Dream Income", "Quick Steps", "Lucky Upgrades", "Fast Builder" };
        private void TitleScreen()
        {
            var account = session.Account;
            if (!inspectedInitialized || lastSelected != account.Selected)
            { inspected = account.Selected; lastSelected = account.Selected; inspectedInitialized = true; }
            MenuArtwork.Background(new Rect(0,0,width,height));
            Box(new Rect(24,22,329,112),panel);
            Label(new Rect(45,32,287,47),"CONTAINER",brand,cream);
            Label(new Rect(45,76,287,47),"DEFENSE",brand,gold);
            Box(new Rect(371,22,width - 745,98),panel);
            Portrait(new Rect(385,34,72,72),session.Inventory.Equipped(account.Selected),true);
            Label(new Rect(474,36,390,30),"Lv. " + account.Level + "   /   " + account.Selected,heading,cream);
            Label(new Rect(475,72,width - 880,24),account.XpNeeded == 0 ? "Maximum account level" : account.XpInLevel + " / " + account.XpNeeded + " XP",small,muted);
            Bar(new Rect(475,99,width - 881,7),account.XpNeeded == 0 ? 1 : (float)account.XpInLevel / account.XpNeeded,new Color(.35f,.7f,1));
            Box(new Rect(width - 354,22,330,98),panel);
            Label(new Rect(width - 330,38,286,32),session.Inventory.Charisma.ToString("N0") + "  CHARISMA",heading,gold);
            Label(new Rect(width - 330,78,286,23),session.Inventory.SkinsOwned + " skins  /  " + session.Inventory.StickerTypesOwned + " sticker types",small,muted);
            Label(new Rect(29,150,780,34),"CHOOSE YOUR RESIDENT",heading,cream);
            Label(new Rect(width - 460,156,434,28),"Seven little builders. One storm to survive.",body,cream);
            float cardWidth = (width - 48) / 7;
            float rowY = 197, cardHeight = height - 483;
            for (int i = 0; i < 7; i++)
            {
                var d = session.Characters.Get((CharacterId)i);
                bool owned = account.IsUnlocked(d.Id), equipped = account.Selected == d.Id;
                Rect card = new Rect(24 + i * cardWidth,rowY,cardWidth - 10,cardHeight);
                if (inspected == d.Id) Box(new Rect(card.x - 3,card.y - 3,card.width + 6,card.height + 6),gold);
                Box(card,panel);
                float pictureHeight = cardHeight - 155;
                Portrait(new Rect(card.x + 5,card.y + 5,card.width - 10,pictureHeight),session.Inventory.Equipped(d.Id),false);
                Box(new Rect(card.x + 1,card.y + pictureHeight - 4,card.width - 2,cardHeight - pictureHeight + 3),panel);
                Label(new Rect(card.x + 12,card.y + pictureHeight + 7,card.width - 24,33),d.Name,heading,cream);
                Label(new Rect(card.x + 12,card.y + pictureHeight + 43,card.width - 24,24),passiveNames[i],small,ResidentFactory.Palette[i] * 1.15f);
                Label(new Rect(card.x + 12,card.y + pictureHeight + 70,card.width - 24,39),d.Description,small,muted);
                string status = equipped ? "SELECTED" : owned ? "SELECT" : "LV. " + d.UnlockLevel + " / LOCKED";
                if (Button(new Rect(card.x + 10,card.yMax - 43,card.width - 20,33),status,equipped ? gold : owned ? new Color(.65f,.77f,.95f) : new Color(.5f,.55f,.67f)))
                { inspected = d.Id; if (owned) session.SelectCharacter(d.Id); }
            }
            float detailsY = height - 257;
            Box(new Rect(24,detailsY,width - 48,128),panel);
            var selected = session.Characters.Get(inspected);
            Portrait(new Rect(36,detailsY + 12,94,104),session.Inventory.Equipped(inspected),true);
            Label(new Rect(148,detailsY + 15,510,34),selected.Name + "  /  " + passiveNames[(int)inspected],heading,cream);
            Label(new Rect(148,detailsY + 55,510,30),selected.Description,body,muted);
            Label(new Rect(148,detailsY + 89,510,25),account.IsUnlocked(inspected) ? "Equipped: " + session.Collections.Skin(session.Inventory.Equipped(inspected)).Name :
                "Unlock by reaching account level " + selected.UnlockLevel + ".",small,gold);
            float skinX = width - 730;
            foreach (var skin in session.Collections.Skins)
            {
                if (skin.CharacterId != inspected) continue;
                bool worn = session.Inventory.Equipped(inspected) == skin.SkinId;
                Box(new Rect(skinX,detailsY + 12,94,104),worn ? gold : new Color(.27f,.3f,.43f));
                Portrait(new Rect(skinX + 3,detailsY + 15,88,75),skin.SkinId,true);
                Label(new Rect(skinX + 5,detailsY + 91,86,22),skin.Name,small,worn ? new Color(.08f,.1f,.17f) : cream);
                if (GUI.Button(new Rect(skinX,detailsY + 12,94,104),GUIContent.none,GUIStyle.none)) OpenCollection(inspected);
                skinX += 104;
            }
            if (Button(new Rect(width - 311,detailsY + 37,258,55),"WARDROBE  >",new Color(.68f,.73f,.95f))) OpenCollection(inspected);
            Box(new Rect(12,height - 115,width - 24,102),panel);
            if (Button(new Rect(28,height - 101,303,72),"COLLECTION  >",new Color(.78f,.53f,.91f))) OpenCollection(inspected);
            if (Button(new Rect(345,height - 101,276,72),"STICKER INVENTORY  >",new Color(.98f,.52f,.73f))) ShowStickers();
            Label(new Rect(645,height - 94,width - 1080,59),"WASD  Move    E  Claim / sleep\n1 / 2 / 3  Upgrade",small,muted);
            if (Button(new Rect(width - 388,height - 104,360,78),"PLAY AS " + account.Selected.ToString().ToUpperInvariant() + "  >",gold)) session.Play();
            if (session.SaveDirty)
            {
                Label(new Rect(32,117,width - 265,27),session.SaveStatus,small,red);
                if (Button(new Rect(width - 210,120,180,30),"RETRY SAVE",gold)) session.PersistAccount();
            }
        }
        private void Portrait(Rect rect,string skinId,bool face)
        {
            var texture = session.Arena.Portraits.Get(skinId);
            if (texture == null) return;
            if (face && MenuArtwork.Get(skinId) != null)
                GUI.DrawTextureWithTexCoords(rect,texture,new Rect(.13f,.42f,.74f,.49f));
            else GUI.DrawTexture(rect,texture,ScaleMode.ScaleToFit,true);
        }
    }
}
