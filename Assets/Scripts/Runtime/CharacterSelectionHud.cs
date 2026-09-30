using ContainerDefense.Domain;
using UnityEngine;
namespace ContainerDefense
{
    public sealed partial class MatchHud
    {
        private CharacterId inspected = CharacterId.Milo;
        private static readonly string[] passiveNames = { "+8% gold", "+15% door HP", "+10% damage", "12% faster income", "+12% move speed", "8% half-cost chance", "+10% build speed" };
        private void TitleScreen()
        {
            var account = session.Account;
            Box(new Rect(24,24,300,102),panel);
            Label(new Rect(44,32,270,43),"CONTAINER",brand,cream);
            Label(new Rect(44,72,270,43),"DEFENSE",brand,gold);
            Box(new Rect(344,24,440,88),panel);
            Portrait(new Rect(352,30,76,72),session.Inventory.Equipped(account.Selected),true);
            Label(new Rect(444,36,310,30),"Lv. " + account.Level + "   " + account.Social.Profile.Username,body,cream);
            Label(new Rect(444,72,290,24),account.XpInLevel + " / " + account.XpNeeded + " XP",small,muted);
            Bar(new Rect(444,101,310,5),account.XpNeeded == 0 ? 1 : (float)account.XpInLevel / account.XpNeeded,new Color(.25f,.62f,1));
            Box(new Rect(width - 328,24,304,88),panel);
            Label(new Rect(width - 308,38,266,28),session.Inventory.Charisma.ToString("N0") + "  CHARISMA",heading,gold);
            Label(new Rect(width - 308,77,266,24),"Solo survival  /  six residents",small,muted);
            float step = (width - 48) / 7, y = height - 425;
            for (int i = 0; i < 7; i++)
            {
                var d = session.Characters.Get((CharacterId)i);
                bool unlocked = account.IsUnlocked(d.Id), selected = account.Selected == d.Id;
                Rect hit = new Rect(24 + i * step,y,step - 8,310);
                if (selected) Box(new Rect(hit.x + 18,y + 195,hit.width - 36,5),gold);
                Portrait(new Rect(hit.x - 3,y,hit.width + 6,214),session.Collections.DefaultSkin(d.Id),false);
                Box(new Rect(hit.x,y + 202,hit.width,108),panel);
                Label(new Rect(hit.x + 12,y + 208,hit.width - 24,30),d.Name,heading,selected ? gold : cream);
                Label(new Rect(hit.x + 12,y + 245,hit.width - 24,25),passiveNames[i],small,cream);
                Label(new Rect(hit.x + 12,y + 276,hit.width - 24,25),!unlocked ? "LOCKED / Level " + d.UnlockLevel : selected ? "SELECTED" : "SELECT",small,selected ? gold : muted);
                if (GUI.Button(hit,GUIContent.none,GUIStyle.none))
                { inspected = d.Id; if (unlocked) session.SelectCharacter(d.Id); }
            }
            if (!account.IsUnlocked(inspected))
            {
                var locked = session.Characters.Get(inspected);
                Box(new Rect(width / 2 - 310,136,620,48),panel);
                Label(new Rect(width / 2 - 290,148,580,26),locked.Name + ": reach Account Level " + locked.UnlockLevel + "  /  " + passiveNames[(int)inspected],body,cream);
            }
            float navY = height - 88;
            if (Button(new Rect(24,navY,230,64),"COLLECTION",new Color(.58f,.68f,.97f))) OpenCollection(account.Selected);
            if (Button(new Rect(266,navY,230,64),"STICKERS",new Color(1,.52f,.71f))) ShowStickers();
            int unread = SocialState.Unread(account.Social);
            if (Button(new Rect(508,navY,230,64),unread > 0 ? "INBOX (" + unread + ")" : "PROFILE & GIFTS",new Color(.7f,.64f,.96f))) OpenSocial(unread > 0 ? 2 : 0);
            Label(new Rect(758,navY + 6,width - 1140,54),"WASD: move\nE: claim / sleep",small,cream);
            if (Button(new Rect(width - 364,navY,340,64),"PLAY  >",gold)) session.Play();
            if (session.SaveDirty) Label(new Rect(24,131,width - 48,26),session.SaveStatus,small,red);
        }
        private void Portrait(Rect rect,string skinId,bool face)
        { session.Arena.Portraits.Draw(rect,skinId,face); }
    }
}
