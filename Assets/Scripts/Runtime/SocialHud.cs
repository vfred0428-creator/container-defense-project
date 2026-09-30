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
            MenuBackground();
            var state = session.Account.Social;
            if (entry == null) {
                entry = new GUIStyle(GUI.skin.textField) { font = HudTheme.TextStyle(HudTheme.Body,false).font,fontSize = HudTheme.Body,padding = new RectOffset(18,18,10,10),alignment = TextAnchor.MiddleLeft };
                entry.normal.textColor = entry.focused.textColor = entry.hover.textColor = Color.white;
            }
            if (stickerIcons == null) stickerIcons = new StickerIcons();
            bool reviewing = pendingGift != null; GUI.enabled = !reviewing;
            var area = Cut.Inset(safe,M);
            Header(ref area,"YOUR NEIGHBORHOOD","Local practice inboxes on this device. Online gifting is not connected.");
            var tabs = Cut.Top(ref area,Touch,G);
            if (HudTheme.Button(Cut.Left(ref tabs,200,G),"HOME",ButtonKind.Secondary,!reviewing,false,HudTheme.Body)) socialOpen = false;
            string[] names = { "PROFILE", "SEND A GIFT", "HISTORY" };
            for (int i = 0; i < 3; i++) if (HudTheme.Button(Cut.Left(ref tabs,260,G),names[i],ButtonKind.Secondary,!reviewing,i == socialTab,HudTheme.Body)) { socialTab = i; socialMessage = ""; }
            var footer = Cut.Bottom(ref area,48,G);
            if (session.SaveDirty && HudTheme.Button(Cut.Right(ref footer,260,G),"RETRY SAVE",ButtonKind.Play,!reviewing)) session.PersistAccount();
            HudTheme.Text(footer,!string.IsNullOrEmpty(socialMessage) ? socialMessage : session.SaveDirty ? session.SaveStatus : "Rank = survival  ·  Charisma = collection  ·  Popularity = gifts received",HudTheme.Label,!string.IsNullOrEmpty(socialMessage) ? (socialSuccess ? HudTheme.Good : HudTheme.Bad) : session.SaveDirty ? HudTheme.Bad : HudTheme.Muted,true);
            if (socialTab == 0) ProfilePanel(area,state); else if (socialTab == 1) GiftPanel(area,state); else HistoryPanel(area,state);
            GUI.enabled = true;
            if (pendingGift != null) GiftConfirmation(state);
        }
        private void ProfilePanel(Rect area,SocialData state)
        {
            var left = Cut.Left(ref area,Mathf.Min(440,area.width * .32f),G);
            HudTheme.Panel(left); var l = Cut.Inset(left,24);
            float art = Mathf.Min(l.width,l.height - 170);
            Portrait(Cut.Center(Cut.Top(ref l,art,G),art,art),session.Inventory.Equipped(session.Account.Selected),false);
            HudTheme.Text(Cut.Top(ref l,44),session.Account.Selected.ToString(),HudTheme.CardTitle,HudTheme.Gold,true);
            HudTheme.Text(Cut.Top(ref l,36),"Level " + session.Account.Level + "  ·  " + HudTheme.Number(session.Account.XpInLevel) + " / " + HudTheme.Number(session.Account.XpNeeded) + " XP",HudTheme.Label,HudTheme.Ink);
            HudTheme.Text(l,"Rank " + session.Account.Rank.CurrentRank + "  ·  best " + session.Account.Rank.HighestRank,HudTheme.Label,HudTheme.Muted);
            HudTheme.Panel(area); var r = Cut.Inset(area,28);
            HudTheme.Text(Cut.Top(ref r,44,8),"PLAYER PROFILE",HudTheme.CardTitle,HudTheme.Ink,true);
            var nameRow = Cut.Top(ref r,Touch,8);
            if (HudTheme.Button(Cut.Right(ref nameRow,240,G),"SAVE NAME",ButtonKind.Primary,true,false,HudTheme.Body)) SocialFeedback(session.RenameProfile(profileName));
            HudAudit.Interactive(nameRow,"Profile name");
            profileName = GUI.TextField(nameRow,profileName ?? "",20,entry);
            HudTheme.Text(Cut.Top(ref r,32,G),"ID  " + state.Profile.PlayerId,HudTheme.Label,HudTheme.Muted);
            var stats = Cut.Top(ref r,Mathf.Min(r.height - 48,2 * 112 + G),G);
            var rows = Cut.Column(stats,2,G);
            var top = Cut.Row(rows[0],2,G); var bottom = Cut.Row(rows[1],2,G);
            ProfileStat(top[0],"CHARISMA",HudTheme.Number(session.Inventory.Charisma),HudTheme.Gold);
            ProfileStat(top[1],"POPULARITY",HudTheme.Number(state.Profile.Popularity),HudTheme.Hex(0xFF8FB8));
            ProfileStat(bottom[0],"DEFAULT SKINS",session.Inventory.SkinsOwned.ToString(),HudTheme.Ink);
            ProfileStat(bottom[1],"STICKERS",StickerTotal(session.Inventory),HudTheme.Ink);
            HudTheme.Text(r,HudTheme.Number(state.Profile.Matches) + " completed matches  ·  " + HudTheme.Number(state.Profile.Wins) + " survival wins",HudTheme.Body,HudTheme.Muted);
        }
        private void ProfileStat(Rect rect,string caption,string value,Color color)
        {
            HudTheme.Card(rect); var inner = Cut.Inset(rect,16);
            HudTheme.Text(Cut.Top(ref inner,28),caption,HudTheme.Label,HudTheme.Muted,true);
            HudTheme.Text(inner,value,HudTheme.CardTitle,color,true);
        }
        private static string StickerTotal(InventorySystem inventory)
        {
            decimal total = 0; foreach (var stack in inventory.Snapshot().Stickers) total += stack.QuantityOwned;
            return total.ToString("N0",CultureInfo.InvariantCulture);
        }
        private void GiftPanel(Rect area,SocialData state)
        {
            HudTheme.Panel(area); var inner = Cut.Inset(area,28);
            HudTheme.Text(Cut.Top(ref inner,40,8),"CHOOSE A STICKER",HudTheme.Body,HudTheme.Ink,true);
            var stickers = session.Collections.Stickers;
            var cards = Cut.Row(Cut.Top(ref inner,120,24),Mathf.Max(1,stickers.Length),G);
            for (int i = 0; i < stickers.Length; i++) {
                var d = stickers[i]; var card = cards[i];
                HudTheme.Card(card); if (d.StickerId == giftSticker) HudTheme.Ring(card);
                var c = Cut.Inset(card,12);
                GUI.DrawTexture(Cut.Left(ref c,c.height,12),stickerIcons.Get(d.Icon),ScaleMode.ScaleToFit,true);
                HudTheme.Text(Cut.Top(ref c,c.height / 2),d.Name,HudTheme.Label,HudTheme.Ink,true);
                HudTheme.Text(c,"x" + HudTheme.Number(session.Inventory.Quantity(d.StickerId)),HudTheme.Label,HudTheme.Gold,true);
                if (Hit(card,"Gift sticker " + d.Name)) { giftSticker = d.StickerId; giftQuantity = "1"; pendingGift = null; }
            }
            var selected = session.Collections.Sticker(giftSticker); if (selected == null) return;
            var right = Cut.Right(ref inner,Mathf.Min(620,inner.width * .5f),32);
            HudTheme.Text(Cut.Top(ref inner,40,8),"TO A PRACTICE INBOX",HudTheme.Body,HudTheme.Ink,true);
            var recipients = Cut.Row(Cut.Top(ref inner,Touch,G),2,G);
            for (int i = 0; i < state.Recipients.Length && i < 2; i++)
                if (HudTheme.Button(recipients[i],state.Recipients[i].Username,ButtonKind.Secondary,true,giftRecipient == state.Recipients[i].PlayerId))
                { giftRecipient = state.Recipients[i].PlayerId; pendingGift = null; }
            HudTheme.Text(inner,"You own " + HudTheme.Number(session.Inventory.Quantity(giftSticker)) + ". Each copy gives " + selected.GiftValue + " Popularity to its recipient.",HudTheme.Body,HudTheme.Muted,false,TextAnchor.UpperLeft,true);
            HudTheme.Text(Cut.Top(ref right,40,8),"QUANTITY",HudTheme.Body,HudTheme.Ink,true);
            var qty = Cut.Top(ref right,Touch,G);
            long quantity; long.TryParse(giftQuantity,NumberStyles.None,CultureInfo.InvariantCulture,out quantity);
            if (HudTheme.Button(Cut.Left(ref qty,Touch,G),"-",ButtonKind.Secondary,true,false,HudTheme.CardTitle)) { giftQuantity = Math.Max(1,quantity - 1).ToString(CultureInfo.InvariantCulture); pendingGift = null; }
            if (HudTheme.Button(Cut.Right(ref qty,120,G),"MAX",ButtonKind.Secondary,true,false,HudTheme.Body)) { giftQuantity = session.Inventory.Quantity(giftSticker).ToString(CultureInfo.InvariantCulture); pendingGift = null; }
            if (HudTheme.Button(Cut.Right(ref qty,Touch,G),"+",ButtonKind.Secondary,true,false,HudTheme.CardTitle)) { giftQuantity = (quantity < long.MaxValue ? Math.Max(1,quantity + 1) : quantity).ToString(CultureInfo.InvariantCulture); pendingGift = null; }
            HudAudit.Interactive(qty,"Gift quantity");
            string edited = GUI.TextField(qty,giftQuantity,19,entry);
            if (edited != giftQuantity) { giftQuantity = edited; pendingGift = null; }
            bool valid = long.TryParse(giftQuantity,NumberStyles.None,CultureInfo.InvariantCulture,out quantity) && quantity > 0 && quantity <= session.Inventory.Quantity(giftSticker);
            if (HudTheme.Button(Cut.Top(ref right,Touch,8),"REVIEW GIFT",ButtonKind.Primary,valid && !session.SaveDirty,false,HudTheme.Body)) {
                // The transaction ID is fixed when the confirmation sheet opens.
                if (pendingGift == null) pendingGift = new GiftRequest { TransactionId = Guid.NewGuid().ToString("N"),ReceiverId = giftRecipient,StickerId = giftSticker,Quantity = quantity };
                socialMessage = "";
            }
            HudTheme.Text(right,valid ? "Stickers leave your inventory after delivery." : "Enter a whole number from 1 to what you own.",HudTheme.Label,HudTheme.Muted,false,TextAnchor.UpperLeft,true);
        }
        private void GiftConfirmation(SocialData state)
        {
            Overlay();
            var panel = Cut.Center(safe,Mathf.Min(760,safe.width - M * 2),Mathf.Min(440,safe.height - M * 2));
            HudTheme.Panel(panel); var inner = Cut.Inset(panel,36);
            HudTheme.Text(Cut.Top(ref inner,64,8),"SEND THIS GIFT?",HudTheme.Title,HudTheme.Ink,true);
            var sticker = session.Collections.Sticker(pendingGift.StickerId);
            var buttons = Cut.Row(Cut.Bottom(ref inner,Touch,G),2,G);
            HudTheme.Text(Cut.Top(ref inner,40),HudTheme.Number(pendingGift.Quantity) + " x " + (sticker == null ? pendingGift.StickerId : sticker.Name) + " to " + SocialState.Name(state,pendingGift.ReceiverId),HudTheme.Body,HudTheme.Ink,true);
            HudTheme.Text(Cut.Top(ref inner,36),"These copies leave your inventory.",HudTheme.Label,HudTheme.Muted);
            HudTheme.Text(inner,socialMessage,HudTheme.Label,HudTheme.Bad,false,TextAnchor.UpperLeft,true);
            if (HudTheme.Button(buttons[0],"CANCEL",ButtonKind.Secondary,true,false,HudTheme.Body)) pendingGift = null;
            if (HudTheme.Button(buttons[1],"CONFIRM GIFT",ButtonKind.Primary,pendingGift != null,false,HudTheme.Body))
            {
                var result = session.SendGift(pendingGift); SocialFeedback(result);
                if (result.Success) { pendingGift = null; giftQuantity = "1"; }
            }
        }
        private void HistoryPanel(Rect area,SocialData state)
        {
            HudTheme.Panel(area); var inner = Cut.Inset(area,28);
            var head = Cut.Top(ref inner,Touch,G);
            if (HudTheme.Button(Cut.Right(ref head,300,G),"MARK ALL READ",ButtonKind.Secondary,SocialState.Unread(state) > 0)) SocialFeedback(session.ReadGiftInbox());
            HudTheme.Text(head,"GIFT RECEIPTS  ·  " + SocialState.Unread(state) + " unread",HudTheme.Body,HudTheme.Ink,true);
            var pager = Cut.Bottom(ref inner,Touch,G);
            int pageSize = Mathf.Max(1,Mathf.FloorToInt((inner.height + 8) / 80));
            int pages = Math.Max(1,(state.History.Length + pageSize - 1) / pageSize); historyPage = Math.Min(historyPage,pages - 1);
            if (HudTheme.Button(Cut.Left(ref pager,Touch,G),"<",ButtonKind.Secondary,historyPage > 0,false,HudTheme.Body)) historyPage--;
            HudTheme.Text(Cut.Left(ref pager,140,G),(historyPage + 1) + " / " + pages,HudTheme.Body,HudTheme.Ink,true,TextAnchor.MiddleCenter);
            if (HudTheme.Button(Cut.Left(ref pager,Touch,24),">",ButtonKind.Secondary,historyPage + 1 < pages,false,HudTheme.Body)) historyPage++;
            HudTheme.Text(pager,state.History.Length + " / " + SocialState.HistoryLimit + " local receipts. Duplicate deliveries are prevented.",HudTheme.Label,HudTheme.Muted);
            if (state.History.Length == 0) { HudTheme.Text(Cut.Top(ref inner,80),"No gifts yet. Send stickers to a practice inbox to try it out.",HudTheme.Body,HudTheme.Muted,false,TextAnchor.UpperLeft,true); return; }
            for (int i = 0; i < pageSize; i++)
            {
                int index = state.History.Length - 1 - historyPage * pageSize - i; if (index < 0) break;
                var g = state.History[index]; var row = Cut.Top(ref inner,72,8);
                HudTheme.Card(row); var r = Cut.Inset(row,14);
                var sticker = session.Collections.Sticker(g.StickerId);
                bool incoming = g.ReceiverId == state.Profile.PlayerId;
                HudTheme.Text(Cut.Right(ref r,260,G),"+" + HudTheme.Number(g.PopularityValue) + " Popularity",HudTheme.Label,incoming ? HudTheme.Good : HudTheme.Muted,true,TextAnchor.MiddleRight);
                HudTheme.Text(Cut.Right(ref r,300,G),(sticker == null ? g.StickerId : sticker.Name) + "  x" + HudTheme.Number(g.Quantity),HudTheme.Body,HudTheme.Gold,true);
                HudTheme.Text(Cut.Left(ref r,Mathf.Min(460,r.width * .55f),G),incoming ? "From " + SocialState.Name(state,g.SenderId) : "To " + SocialState.Name(state,g.ReceiverId),HudTheme.Body,HudTheme.Ink,true);
                HudTheme.Text(r,new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(g.Timestamp).ToLocalTime().ToString("g"),HudTheme.Label,HudTheme.Muted);
            }
        }
        private void SocialFeedback(GiftResult result) { socialSuccess = result.Success; socialMessage = result.Message; }
    }
}
