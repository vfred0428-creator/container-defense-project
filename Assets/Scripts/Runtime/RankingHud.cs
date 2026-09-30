using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    public sealed partial class MatchHud
    {
        private bool rankedOpen, leaderboardOpen;
        private LeaderboardKind boardKind;
        public void OpenRanked() { rankedOpen = true; socialOpen = collectionOpen = leaderboardOpen = false; }
        public void OpenLeaderboards(LeaderboardKind kind) { OpenRanked(); leaderboardOpen = true; boardKind = kind; }
        public void CloseRanked() { rankedOpen = false; }
        private void RankedScreen()
        {
            MenuArtwork.Background(new Rect(0,0,width,height)); Overlay();
            var rank = session.Account.Rank;
            Box(new Rect(24,24,width - 48,108),panel);
            Label(new Rect(48,40,900,55),leaderboardOpen ? "LOCAL LEADERBOARDS" : "PRACTICE RANKED",title,cream);
            Label(new Rect(48,99,width - 100,24),"Season " + rank.Season + "  /  Local prototype. Bot matches and device-only records; online rankings are not connected.",small,muted);
            if (Button(new Rect(24,150,210,48),"<  HOME",muted)) rankedOpen = false;
            if (Button(new Rect(250,150,250,48),"YOUR RANK",!leaderboardOpen ? gold : muted)) leaderboardOpen = false;
            if (Button(new Rect(516,150,270,48),"LEADERBOARDS",leaderboardOpen ? gold : muted)) leaderboardOpen = true;
            if (leaderboardOpen) LeaderboardPanel(); else RankPanel(rank);
        }
        private void RankPanel(RankData rank)
        {
            Box(new Rect(24,222,424,height - 340),panel);
            Portrait(new Rect(110,244,252,252),session.Inventory.Equipped(session.Account.Selected),true);
            Label(new Rect(56,520,360,60),rank.CurrentRank.ToString(),title,gold);
            Label(new Rect(56,592,360,36),rank.Stars + " / " + RankProgression.StarsPerRank + " STARS",heading,cream);
            Bar(new Rect(56,644,360,14),(float)rank.Stars / RankProgression.StarsPerRank,gold);
            Label(new Rect(56,684,360,60),"Highest: " + rank.HighestRank + "\n" + rank.Wins + " wins / " + rank.MatchesPlayed + " practice matches",body,muted);
            Box(new Rect(468,222,width - 492,height - 340),panel);
            Label(new Rect(496,248,width - 548,40),"SURVIVE IN YOUR OWN HOUSE",heading,cream);
            Label(new Rect(496,310,width - 548,170),"Survive the boss defeat: +1 star.\nEliminated or defeated: −1 star, down to zero.\nFive stars promote you; reached tiers are protected.\nSovereign holds up to five stars.",body,cream);
            Label(new Rect(496,488,width - 548,40),"THE RANK JOURNEY",heading,gold);
            for (int i = 0; i < 8; i++)
            {
                float x = 496 + i % 4 * ((width - 568) / 4), y = 552 + i / 4 * 70;
                Label(new Rect(x,y,(width - 568) / 4 - 8,48),((RankTier)i).ToString(),body,i <= (int)rank.HighestRank ? cream : muted);
            }
            Label(new Rect(496,714,width - 548,52),"Casual PLAY leaves your rank unchanged. Account levels and cosmetics never reset with rank.",small,muted);
            if (Button(new Rect(width - 492,height - 94,468,64),"PLAY PRACTICE RANKED  >",gold)) { rankedOpen = false; session.PlayRanked(); }
            Label(new Rect(40,height - 83,width - 580,50),"Solo survival. Six independent residents. No shared houses or buffs.",small,cream);
        }
        private void LeaderboardPanel()
        {
            string[] labels = { "RANKED", "CHARISMA", "POPULARITY" };
            for (int i = 0; i < 3; i++) if (Button(new Rect(24 + i * 266,220,250,46),labels[i],boardKind == (LeaderboardKind)i ? gold : muted)) boardKind = (LeaderboardKind)i;
            Box(new Rect(24,286,width - 48,height - 354),panel);
            Label(new Rect(48,306,60,30),"#",body,muted);
            Label(new Rect(166,306,440,30),"PLAYER",body,muted);
            Label(new Rect(width - 570,306,260,30),boardKind == LeaderboardKind.Ranked ? "RANK" : "COLLECTION",body,muted);
            Label(new Rect(width - 272,306,220,30),boardKind == LeaderboardKind.Ranked ? "STARS" : labels[(int)boardKind],body,muted);
            var rows = session.Leaderboards.Get(boardKind);
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i]; float y = 350 + i * 84;
                Box(new Rect(40,y,width - 80,76),row.IsYou ? new Color(.19f,.23f,.32f) : new Color(.1f,.14f,.23f));
                Label(new Rect(56,y + 24,58,36),(i + 1).ToString(),heading,i < 3 ? gold : cream);
                if (row.IsYou) Portrait(new Rect(102,y + 6,66,64),session.Inventory.Equipped(session.Account.Selected),true);
                else { Box(new Rect(114,y + 18,42,42),new Color(.35f,.39f,.49f)); Label(new Rect(124,y + 24,30,32),"P",heading,cream); }
                Label(new Rect(186,y + 12,460,36),row.Username + (row.IsYou ? "  / YOU" : ""),heading,cream);
                Label(new Rect(186,y + 47,460,24),row.IsYou ? "Your device account" : "Local practice inbox",small,muted);
                Label(new Rect(width - 570,y + 24,260,42),boardKind == LeaderboardKind.Ranked ? row.Rank.CurrentRank.ToString() : row.CollectionCount + " items / types",body,cream);
                Label(new Rect(width - 272,y + 24,220,42),boardKind == LeaderboardKind.Ranked ? row.Rank.Stars + " / 5" : row.Score.ToString("N0"),row.Score > 999999999999L ? small : heading,gold);
            }
            Label(new Rect(48,height - 170,width - 96,82),boardKind == LeaderboardKind.Ranked ?
                "Only your account has recorded rank data on this device. Other players will appear when an online service is connected." :
                "Scores come from saved local collections and gift receipts. Charisma counts unique owned items; Popularity counts gift value received.",body,muted);
        }
    }
}
