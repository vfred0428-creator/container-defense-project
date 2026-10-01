using ContainerDefense.Domain;
using UnityEngine;
namespace ContainerDefense
{
    public sealed partial class MatchHud
    {
        private CharacterId inspected = CharacterId.Milo;
        private bool settingsOpen;
        private static readonly string[] passiveNames = { "+8% gold", "+15% door HP", "+10% damage", "12% faster income", "+12% move speed", "8% half-cost chance", "+10% build speed" };
        private void TitleScreen()
        {
            MenuBackground();
            // Settings replace the title while open, so nothing behind it can be tapped.
            if (settingsOpen) { var panel = Cut.Center(safe,Mathf.Min(620,safe.width - M * 2),Mathf.Min(560,safe.height - M * 2));
                HudTheme.Panel(panel); var inner = Cut.Inset(panel,32);
                HudTheme.Text(Cut.Top(ref inner,64,8),"SETTINGS",HudTheme.Title,HudTheme.Ink,true,TextAnchor.MiddleCenter);
                SoundSettings(ref inner);
                if (HudTheme.Button(Cut.Bottom(ref inner,Touch),"DONE",ButtonKind.Primary,true,false,HudTheme.Body)) settingsOpen = false;
                return;
            }
            var account = session.Account; var area = Cut.Inset(safe,M);
            // Top row: brand, profile, rank, charisma. Cards are fixed widths; spare width is margin.
            var top = Cut.Top(ref area,120,24);
            var charisma = Cut.Right(ref top,300,G); var rank = Cut.Right(ref top,280,G); var profile = Cut.Right(ref top,Mathf.Min(480,top.width * .55f),G);
            // Title as live text in the display font: two stacked lines, dark outline and drop shadow drawn in code.
            HudTheme.Logo(new Rect(top.x,top.y - 6,top.width,top.height + 12));
            HudTheme.Panel(profile); var p = Cut.Inset(profile,12);
            Portrait(Cut.Left(ref p,96,G),session.Inventory.Equipped(account.Selected),true);
            HudTheme.Text(Cut.Top(ref p,40),account.Social.Profile.Username,HudTheme.Body,HudTheme.Ink,true);
            HudTheme.Text(Cut.Top(ref p,30,6),"Level " + account.Level + "  ·  " + HudTheme.Number(account.XpInLevel) + " / " + HudTheme.Number(account.XpNeeded) + " XP",HudTheme.Label,HudTheme.Muted);
            HudTheme.Bar(Cut.Top(ref p,10),account.XpNeeded == 0 ? 1 : (float)account.XpInLevel / account.XpNeeded,HudTheme.Info);
            if (Hit(profile,"Profile")) OpenSocial(0);
            HudTheme.Panel(rank); var rr = Cut.Inset(rank,18);
            HudTheme.Text(Cut.Top(ref rr,44),account.Rank.CurrentRank.ToString(),HudTheme.CardTitle,HudTheme.Ink,true);
            HudTheme.Text(rr,account.Rank.Stars + " / 5 stars",HudTheme.Label,HudTheme.Gold,true);
            if (Hit(rank,"Rank")) OpenRanked();
            HudTheme.Panel(charisma); var cr = Cut.Inset(charisma,18);
            HudTheme.Text(Cut.Top(ref cr,44),HudTheme.Number(session.Inventory.Charisma) + " Charisma",HudTheme.CardTitle,HudTheme.Gold,true);
            int unread = SocialState.Unread(account.Social);
            HudTheme.Text(cr,unread > 0 ? unread + " new gifts" : "Profile and gifts",HudTheme.Label,unread > 0 ? HudTheme.Good : HudTheme.Muted,true);
            if (Hit(charisma,"Gifts")) OpenSocial(unread > 0 ? 2 : 0);
            // Bottom navigation.
            var nav = Cut.Bottom(ref area,Touch,24);
            if (HudTheme.Button(Cut.Right(ref nav,Mathf.Min(440,nav.width * .3f),24),"PLAY",ButtonKind.Play,true,false,HudTheme.CardTitle)) session.Play();
            if (HudTheme.Button(Cut.Right(ref nav,Touch,24),"",ButtonKind.Secondary,"icon_gear")) settingsOpen = true;
            var navButtons = Cut.Row(Cut.Left(ref nav,Mathf.Min(nav.width,5 * 250 + 4 * G)),5,G);
            if (HudTheme.Button(navButtons[0],"RANKED",ButtonKind.Secondary,"icon_skull",true,false,HudTheme.Body)) OpenRanked();
            if (HudTheme.Button(navButtons[1],"LEADERBOARD",ButtonKind.Secondary,"icon_up"))  OpenLeaderboards(LeaderboardKind.Ranked);
            if (HudTheme.Button(navButtons[2],"STICKERS",ButtonKind.Secondary,"icon_heart",true,false,HudTheme.Body)) ShowStickers();
            if (HudTheme.Button(navButtons[3],"COLLECTION",ButtonKind.Secondary,"icon_house")) OpenCollection(account.Selected);
            if (HudTheme.Button(navButtons[4],"MY YARD",ButtonKind.Secondary,"prop_plant")) OpenYard();
            // Notice slot above the navigation: locked-character info or a save problem.
            var notice = Cut.Bottom(ref area,56,G);
            string message = session.SaveDirty ? session.SaveStatus : !account.IsUnlocked(inspected) ? session.Characters.Get(inspected).Name + " unlocks at level " + session.Characters.Get(inspected).UnlockLevel + "  ·  " + passiveNames[(int)inspected] : null;
            if (message != null) {
                var size = HudTheme.TextStyle(HudTheme.Label,true,TextAnchor.MiddleCenter,false).CalcSize(new GUIContent(message));
                var pill = Cut.Center(notice,Mathf.Min(notice.width,size.x + 64),56);
                HudTheme.Panel(pill,false); HudTheme.Text(Cut.Inset(pill,12),message,HudTheme.Label,session.SaveDirty ? HudTheme.Bad : HudTheme.Gold,true,TextAnchor.MiddleCenter);
            }
            // Roster: seven cards, portraits kept inside their cards.
            float cardWidth = Mathf.Min(260,Mathf.Min((area.width - 6 * G) / 7,area.height - 150)), cardHeight = Mathf.Min(area.height,cardWidth + 150);
            var row = Cut.Center(area,cardWidth * 7 + 6 * G,cardHeight);
            var cards = Cut.Row(row,7,G);
            for (int i = 0; i < 7; i++) {
                var d = session.Characters.Get((CharacterId)i); var card = cards[i];
                bool unlocked = account.IsUnlocked(d.Id), selected = account.Selected == d.Id;
                HudTheme.Panel(card,false); if (selected) HudTheme.Ring(card);
                var inner = Cut.Inset(card,12);
                var art = Cut.Top(ref inner,inner.width,8);
                HudTheme.Fill(art,HudTheme.Hex(0x141A2C),10);
                var old = GUI.color; if (!unlocked) GUI.color = new Color(.32f,.34f,.42f,1);
                Portrait(art,session.Collections.DefaultSkin(d.Id),false); GUI.color = old;
                // Locked: greyed art, padlock badge and the level it unlocks at.
                if (!unlocked) { var lockRect = Cut.Center(art,Mathf.Min(88,art.width * .45f),Mathf.Min(88,art.width * .45f)); HudTheme.Fill(lockRect,HudTheme.Hex(0x111627,.85f),lockRect.width / 2); HudIcons.Draw(Cut.Inset(lockRect,12),"icon_lock"); }
                HudTheme.Text(Cut.Top(ref inner,42),d.Name,HudTheme.CardTitle,selected ? HudTheme.Gold : HudTheme.Ink,true);
                HudTheme.Text(Cut.Top(ref inner,30),passiveNames[i],HudTheme.Label,HudTheme.Ink);
                HudTheme.Text(inner,!unlocked ? "Unlocks at Lv " + d.UnlockLevel : selected ? "Selected" : "Tap to select",HudTheme.Label,!unlocked ? HudTheme.Bad : selected ? HudTheme.Gold : HudTheme.Muted,true);
                if (Hit(card,"Character " + d.Name)) { inspected = d.Id; if (unlocked) session.SelectCharacter(d.Id); }
            }
        }
    }
}
