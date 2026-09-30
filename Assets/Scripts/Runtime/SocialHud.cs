using System;
using System.Globalization;
using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    public sealed partial class MatchHud
    {
        private bool socialOpen, socialSuccess;
        private int socialTab, historyPage;
        private string profileName, socialMessage = "", giftSticker = "bunny", giftRecipient = "local_a", giftQuantity = "1";
        private GiftRequest pendingGift;
        private GUIStyle entry;
        public void OpenSocial(int tab)
        { rankedOpen = false; socialOpen = true; collectionOpen = false; socialTab = tab; profileName = session.Account.Social.Profile.Username; socialMessage = ""; }
        public void CloseSocial() { socialOpen = false; }
        private void SocialScreen()
        {
            MenuArtwork.Background(new Rect(0,0,width,height)); Overlay();
            var state = session.Account.Social;
            if (entry == null) { entry = new GUIStyle(GUI.skin.textField) { font = body.font, fontSize = 22, padding = new RectOffset(14,14,9,9) }; }
            if (stickerIcons == null) stickerIcons = new StickerIcons();
            bool reviewing = pendingGift != null; GUI.enabled = !reviewing;
            Box(new Rect(24,24,width - 48,108),panel);
            Label(new Rect(48,40,650,55),"YOUR NEIGHBORHOOD",title,cream);
            Label(new Rect(48,99,width - 100,24),"Local social prototype. Practice inboxes are stored on this device; online gifting is not connected.",small,muted);
            if (Button(new Rect(24,150,210,48),"<  HOME",muted)) socialOpen = false;
            string[] tabs = { "PROFILE", "SEND A GIFT", "HISTORY" };
            for (int i = 0; i < 3; i++) if (Button(new Rect(250 + i * 222,150,210,48),tabs[i],i == socialTab ? gold : muted)) { socialTab = i; socialMessage = ""; }
            if (socialTab == 0) ProfilePanel(state); else if (socialTab == 1) GiftPanel(state); else HistoryPanel(state);
            Label(new Rect(40,height - 108,width - 80,42),socialMessage,body,socialSuccess ? green : red);
            Label(new Rect(40,height - 57,width - 264,38),session.SaveDirty ? session.SaveStatus : "Rank = survival  /  Charisma = collection  /  Popularity = gifts received",small,muted);
            if (session.SaveDirty && Button(new Rect(width - 210,height - 62,176,42),"RETRY SAVE",gold)) session.PersistAccount();
            GUI.enabled = true;
            if (pendingGift != null) GiftConfirmation(state);
        }
        private void ProfilePanel(SocialData state)
        {
            float top = 222, contentHeight = height - 348;
            Box(new Rect(24,top,380,contentHeight),panel);
            Portrait(new Rect(64,top + 16,300,300),session.Inventory.Equipped(session.Account.Selected),false);
            Label(new Rect(52,top + 330,328,38),session.Account.Selected + " / Default",heading,gold);
            Label(new Rect(52,top + 382,328,66),"Level " + session.Account.Level + "\n" + session.Account.XpInLevel + " / " + session.Account.XpNeeded + " XP",body,cream);
            Label(new Rect(52,top + 462,328,68),"Rank: " + session.Account.Rank.CurrentRank + "\nBest: " + session.Account.Rank.HighestRank,body,muted);
            float x = 424, w = width - 448;
            Box(new Rect(x,top,w,contentHeight),panel);
            Label(new Rect(x + 24,top + 24,w - 48,32),"PLAYER PROFILE",heading,cream);
            profileName = GUI.TextField(new Rect(x + 24,top + 76,w - 250,50),profileName ?? "",20,entry);
            if (Button(new Rect(width - 226,top + 76,178,50),"SAVE NAME",gold)) SocialFeedback(session.RenameProfile(profileName));
            Label(new Rect(x + 24,top + 144,w - 48,24),"ID  " + state.Profile.PlayerId,small,muted);
            float statWidth = (w - 64) / 2;
            ProfileStat(new Rect(x + 24,top + 196,statWidth,90),"CHARISMA",session.Inventory.Charisma.ToString("N0"),gold);
            ProfileStat(new Rect(x + 40 + statWidth,top + 196,statWidth,90),"POPULARITY",state.Profile.Popularity.ToString("N0"),new Color(1,.57f,.75f));
            ProfileStat(new Rect(x + 24,top + 300,statWidth,90),"DEFAULT SKINS",session.Inventory.SkinsOwned.ToString(),cream);
            ProfileStat(new Rect(x + 40 + statWidth,top + 300,statWidth,90),"STICKERS",StickerTotal(session.Inventory),cream);
            Label(new Rect(x + 24,top + 420,w - 48,70),state.Profile.Matches.ToString("N0") + " completed matches  /  " + state.Profile.Wins.ToString("N0") + " survival wins\nMatch statistics recorded from this update onward.",body,muted);
        }
        private void ProfileStat(Rect rect,string caption,string value,Color color)
        {
            Box(rect,new Color(.1f,.14f,.23f));
            Label(new Rect(rect.x + 16,rect.y + 10,rect.width - 32,24),caption,small,muted);
            Label(new Rect(rect.x + 16,rect.y + 38,rect.width - 32,42),value,value.Length > 16 ? body : heading,color);
        }
        private static string StickerTotal(InventorySystem inventory)
        {
            decimal total = 0; foreach (var stack in inventory.Snapshot().Stickers) total += stack.QuantityOwned;
            return total.ToString("N0");
        }
        private void GiftPanel(SocialData state)
        {
            float x = 24, top = 222, w = width - 48;
            Box(new Rect(x,top,w,height - 348),panel);
            Label(new Rect(48,top + 20,550,32),"CHOOSE A STICKER",heading,cream);
            var stickers = session.Collections.Stickers; float cardWidth = (w - 64) / 5;
            for (int i = 0; i < stickers.Length; i++)
            {
                var d = stickers[i]; Rect card = new Rect(48 + i * (cardWidth + 4),top + 66,cardWidth,172);
                Box(card,d.StickerId == giftSticker ? new Color(.29f,.17f,.28f) : new Color(.1f,.14f,.23f));
                GUI.DrawTexture(new Rect(card.x + 16,card.y + 8,76,76),stickerIcons.Get(d.Icon),ScaleMode.ScaleToFit,true);
                Label(new Rect(card.x + 100,card.y + 26,card.width - 116,46),d.Name,body,cream);
                Label(new Rect(card.x + 16,card.y + 92,card.width - 32,28),"x" + session.Inventory.Quantity(d.StickerId).ToString("N0"),small,gold);
                if (Button(new Rect(card.x + 12,card.y + 128,card.width - 24,32),d.StickerId == giftSticker ? "SELECTED" : "SELECT",muted))
                { giftSticker = d.StickerId; giftQuantity = "1"; pendingGift = null; }
            }
            var selected = session.Collections.Sticker(giftSticker); if (selected == null) return;
            Label(new Rect(48,top + 258,550,30),"TO A LOCAL PRACTICE INBOX",heading,cream);
            for (int i = 0; i < state.Recipients.Length && i < 2; i++)
                if (Button(new Rect(48 + i * 284,top + 302,272,48),state.Recipients[i].Username,giftRecipient == state.Recipients[i].PlayerId ? gold : muted))
                { giftRecipient = state.Recipients[i].PlayerId; pendingGift = null; }
            Label(new Rect(48,top + 370,560,76),"Owned: " + session.Inventory.Quantity(giftSticker).ToString("N0") + "\nEach copy gives " + selected.GiftValue + " Popularity to its recipient.",body,cream);
            float controlsX = Mathf.Max(670,width - 654);
            Label(new Rect(controlsX,top + 258,560,30),"GIFT QUANTITY",heading,cream);
            long quantity; bool valid = long.TryParse(giftQuantity,NumberStyles.None,CultureInfo.InvariantCulture,out quantity) && quantity > 0;
            if (Button(new Rect(controlsX,top + 302,50,50),"−",muted)) { giftQuantity = Math.Max(1,quantity - (quantity > 1 ? 1 : 0)).ToString(CultureInfo.InvariantCulture); pendingGift = null; }
            string edited = GUI.TextField(new Rect(controlsX + 62,top + 302,324,50),giftQuantity,19,entry);
            if (edited != giftQuantity) { giftQuantity = edited; pendingGift = null; }
            if (Button(new Rect(controlsX + 398,top + 302,50,50),"+",muted)) { giftQuantity = (quantity < long.MaxValue ? Math.Max(1,quantity + 1) : quantity).ToString(CultureInfo.InvariantCulture); pendingGift = null; }
            if (Button(new Rect(controlsX + 460,top + 302,120,50),"MAX",muted)) { giftQuantity = session.Inventory.Quantity(giftSticker).ToString(CultureInfo.InvariantCulture); pendingGift = null; }
            valid = long.TryParse(giftQuantity,NumberStyles.None,CultureInfo.InvariantCulture,out quantity) && quantity > 0 && quantity <= session.Inventory.Quantity(giftSticker);
            if (Button(new Rect(controlsX,top + 370,580,62),"REVIEW GIFT",new Color(1,.52f,.71f),valid && !session.SaveDirty))
            {
                if (pendingGift == null) pendingGift = new GiftRequest { TransactionId = Guid.NewGuid().ToString("N"),ReceiverId = giftRecipient,StickerId = giftSticker,Quantity = quantity };
                socialMessage = "";
            }
            Label(new Rect(controlsX,top + 448,580,40),valid ? "Stickers leave your inventory after delivery." : "Enter a whole number between 1 and your owned quantity.",small,muted);
        }
        private void GiftConfirmation(SocialData state)
        {
            Overlay(); float x = width / 2 - 340, y = height / 2 - 184;
            Box(new Rect(x,y,680,368),panel);
            Label(new Rect(x + 28,y + 24,624,44),"SEND THIS GIFT?",heading,cream);
            var sticker = session.Collections.Sticker(pendingGift.StickerId);
            Label(new Rect(x + 28,y + 82,624,110),pendingGift.Quantity.ToString("N0") + " × " + (sticker == null ? pendingGift.StickerId : sticker.Name) +
                "\nTo " + SocialState.Name(state,pendingGift.ReceiverId) + "\nThis local transfer removes copies from your inventory.",body,cream);
            Label(new Rect(x + 28,y + 200,624,64),socialMessage,small,red);
            if (Button(new Rect(x + 28,y + 288,228,52),"CANCEL",muted)) pendingGift = null;
            if (Button(new Rect(x + 280,y + 288,372,52),"CONFIRM GIFT",new Color(1,.52f,.71f),pendingGift != null))
            {
                var result = session.SendGift(pendingGift); SocialFeedback(result);
                if (result.Success) { pendingGift = null; giftQuantity = "1"; }
            }
        }
        private void HistoryPanel(SocialData state)
        {
            const int pageSize = 5;
            Box(new Rect(24,222,width - 48,height - 348),panel);
            Label(new Rect(48,244,700,34),"GIFT RECEIPTS  /  " + SocialState.Unread(state) + " unread",heading,cream);
            if (Button(new Rect(width - 276,238,228,44),"MARK ALL READ",muted,SocialState.Unread(state) > 0)) SocialFeedback(session.ReadGiftInbox());
            int pages = Math.Max(1,(state.History.Length + pageSize - 1) / pageSize); historyPage = Math.Min(historyPage,pages - 1);
            if (state.History.Length == 0) Label(new Rect(48,322,width - 96,80),"No gifts yet. Send stickers to a practice inbox to try the local transaction flow.",body,muted);
            for (int i = 0; i < pageSize; i++)
            {
                int index = state.History.Length - 1 - historyPage * pageSize - i; if (index < 0) break;
                var g = state.History[index]; float y = 304 + i * 72;
                Box(new Rect(48,y,width - 96,64),new Color(.1f,.14f,.23f));
                var sticker = session.Collections.Sticker(g.StickerId);
                bool incoming = g.ReceiverId == state.Profile.PlayerId;
                Label(new Rect(64,y + 8,530,30),(incoming ? "FROM " + SocialState.Name(state,g.SenderId) : "TO " + SocialState.Name(state,g.ReceiverId)),body,cream);
                Label(new Rect(64,y + 36,530,24),new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(g.Timestamp).ToLocalTime().ToString("g"),small,muted);
                Label(new Rect(620,y + 8,width - 960,46),(sticker == null ? g.StickerId : sticker.Name) + "  x" + g.Quantity.ToString("N0"),body,gold);
                Label(new Rect(width - 310,y + 12,244,40),"+" + g.PopularityValue.ToString("N0") + " Popularity",small,incoming ? green : muted);
            }
            float bottom = height - 198;
            if (Button(new Rect(48,bottom,132,44),"< PREV",muted,historyPage > 0)) historyPage--;
            Label(new Rect(206,bottom + 10,200,30),(historyPage + 1) + " / " + pages,body,cream);
            if (Button(new Rect(350,bottom,132,44),"NEXT >",muted,historyPage + 1 < pages)) historyPage++;
            Label(new Rect(530,bottom + 9,width - 600,38),state.History.Length + " / " + SocialState.HistoryLimit + " local receipts. Duplicate deliveries are prevented.",small,muted);
        }
        private void SocialFeedback(GiftResult result) { socialSuccess = result.Success; socialMessage = result.Message; }
    }
}
