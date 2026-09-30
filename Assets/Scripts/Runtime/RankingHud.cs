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
            MenuBackground();
            var rank = session.Account.Rank; var area = Cut.Inset(safe,M);
            Header(ref area,leaderboardOpen ? "LEADERBOARDS" : "PRACTICE RANKED","Season " + rank.Season + "  ·  bot matches and device-only records");
            var tabs = Cut.Top(ref area,Touch,G);
            if (HudTheme.Button(Cut.Left(ref tabs,200,G),"HOME",ButtonKind.Secondary,true,false,HudTheme.Body)) rankedOpen = false;
            if (HudTheme.Button(Cut.Left(ref tabs,260,G),"YOUR RANK",ButtonKind.Secondary,true,!leaderboardOpen,HudTheme.Body)) leaderboardOpen = false;
            if (HudTheme.Button(Cut.Left(ref tabs,300,G),"LEADERBOARDS",ButtonKind.Secondary,true,leaderboardOpen,HudTheme.Body)) leaderboardOpen = true;
            if (leaderboardOpen) LeaderboardPanel(area); else RankPanel(area,rank);
        }
        private void RankPanel(Rect area,RankData rank)
        {
            var play = Cut.Bottom(ref area,Touch,G);
            if (HudTheme.Button(Cut.Right(ref play,Mathf.Min(520,play.width * .45f),G),"PLAY PRACTICE RANKED",ButtonKind.Play,true,false,HudTheme.Body)) { rankedOpen = false; session.PlayRanked(); }
            HudTheme.Text(play,"Solo survival. Casual PLAY never changes your rank.",HudTheme.Body,HudTheme.Muted);
            var left = Cut.Left(ref area,Mathf.Min(460,area.width * .34f),G);
            HudTheme.Panel(left); var l = Cut.Inset(left,28);
            float art = Mathf.Min(l.width,l.height - 190);
            Portrait(Cut.Center(Cut.Top(ref l,art,G),art,art),session.Inventory.Equipped(session.Account.Selected),true);
            HudTheme.Text(Cut.Top(ref l,68),rank.CurrentRank.ToString(),HudTheme.Title,HudTheme.Gold,true);
            // Five star pips, filled to the current star count.
            var pips = Cut.Row(Cut.Top(ref l,28,G),RankProgression.StarsPerRank,10);
            for (int i = 0; i < pips.Length; i++) HudTheme.Fill(pips[i],i < rank.Stars ? HudTheme.Gold : HudTheme.BarBack,8);
            HudTheme.Text(l,"Best " + rank.HighestRank + "  ·  " + rank.Wins + " wins in " + rank.MatchesPlayed,HudTheme.Label,HudTheme.Muted);
            HudTheme.Panel(area); var r = Cut.Inset(area,28);
            HudTheme.Text(Cut.Top(ref r,44,8),"HOW STARS WORK",HudTheme.CardTitle,HudTheme.Ink,true);
            HudTheme.Text(Cut.Top(ref r,4 * 38,G),"Win the match in your own house: +1 star.\nEliminated or defeated: -1 star, never below zero.\nFive stars promote you; reached tiers are protected.\nSovereign holds up to five stars.",HudTheme.Body,HudTheme.Ink,false,TextAnchor.UpperLeft,true);
            HudTheme.Text(Cut.Top(ref r,44,8),"RANK JOURNEY",HudTheme.CardTitle,HudTheme.Gold,true);
            var grid = Cut.Top(ref r,Mathf.Min(r.height,2 * 72 + G));
            var rows = Cut.Column(grid,2,G);
            for (int i = 0; i < 8; i++) {
                var chip = Cut.Row(rows[i / 4],4,G)[i % 4];
                bool reached = i <= (int)rank.HighestRank, current = i == (int)rank.CurrentRank;
                HudTheme.Card(chip); if (current) HudTheme.Ring(chip);
                HudTheme.Text(Cut.Inset(chip,10),((RankTier)i).ToString(),HudTheme.Body,reached ? HudTheme.Ink : HudTheme.Muted,reached,TextAnchor.MiddleCenter);
            }
        }
        private void LeaderboardPanel(Rect area)
        {
            string[] labels = { "RANKED", "CHARISMA", "POPULARITY" };
            var tabs = Cut.Row(Cut.Top(ref area,Touch,G),3,G);
            for (int i = 0; i < 3; i++) if (HudTheme.Button(tabs[i],labels[i],ButtonKind.Secondary,true,boardKind == (LeaderboardKind)i,HudTheme.Body)) boardKind = (LeaderboardKind)i;
            HudTheme.Panel(area); var inner = Cut.Inset(area,24);
            HudTheme.Text(Cut.Bottom(ref inner,64,G),boardKind == LeaderboardKind.Ranked ?
                "Only your account has rank data on this device. Other players appear once online play exists." :
                "Scores come from saved collections and gift receipts on this device.",HudTheme.Label,HudTheme.Muted,false,TextAnchor.UpperLeft,true);
            var head = Cut.Top(ref inner,36,8); var h = Cut.Inset(head,0);
            HudTheme.Text(Cut.Left(ref h,80),"#",HudTheme.Label,HudTheme.Muted,true);
            HudTheme.Text(Cut.Right(ref h,220),boardKind == LeaderboardKind.Ranked ? "STARS" : labels[(int)boardKind],HudTheme.Label,HudTheme.Muted,true,TextAnchor.MiddleRight);
            HudTheme.Text(Cut.Right(ref h,300,G),boardKind == LeaderboardKind.Ranked ? "RANK" : "ITEMS",HudTheme.Label,HudTheme.Muted,true);
            HudTheme.Text(h,"PLAYER",HudTheme.Label,HudTheme.Muted,true);
            var rows = session.Leaderboards.Get(boardKind);
            for (int i = 0; i < rows.Length && inner.height >= 88; i++) {
                var row = rows[i]; var r = Cut.Top(ref inner,88,8);
                HudTheme.Card(r); if (row.IsYou) HudTheme.Ring(r); var c = Cut.Inset(r,12);
                HudTheme.Text(Cut.Left(ref c,68),(i + 1).ToString(),HudTheme.CardTitle,i < 3 ? HudTheme.Gold : HudTheme.Ink,true);
                var face = Cut.Left(ref c,64,G);
                if (row.IsYou) Portrait(face,session.Inventory.Equipped(session.Account.Selected),true); else HudTheme.Fill(face,HudTheme.SecondaryFill,12);
                HudTheme.Text(Cut.Right(ref c,220),boardKind == LeaderboardKind.Ranked ? row.Rank.Stars + " / 5" : HudTheme.Number(row.Score),HudTheme.CardTitle,HudTheme.Gold,true,TextAnchor.MiddleRight);
                HudTheme.Text(Cut.Right(ref c,300,G),boardKind == LeaderboardKind.Ranked ? row.Rank.CurrentRank.ToString() : row.CollectionCount + " items",HudTheme.Body,HudTheme.Ink);
                HudTheme.Text(c,row.Username + (row.IsYou ? "  (you)" : ""),HudTheme.Body,HudTheme.Ink,true);
            }
        }
    }
}
