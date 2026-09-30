using System.Collections.Generic;
using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    // Runtime IMGUI HUD. Layout follows the UI spec: 1920x1080 reference scaled with match 0.5,
    // everything interactive inside the safe area, regions cut from their parents so they never overlap.
    public sealed partial class MatchHud : MonoBehaviour
    {
        private GameSession session;
        private float width, height, scale;
        private Rect safe;
        private const float M = HudTheme.Margin, G = HudTheme.Gap, Touch = HudTheme.Touch;
        public void Initialize(GameSession game) { session = game; }

        private void OnGUI()
        {
            DrawHud(); HudLayout.DrawSimulatedNotch();
        }
        private void DrawHud()
        {
            if (session == null || session.Match == null) return;
            scale = HudTheme.Scale; width = Screen.width / scale; height = Screen.height / scale;
            var s = HudLayout.SafeAreaGui; safe = new Rect(s.x / scale,s.y / scale,s.width / scale,s.height / scale);
            if (Event.current.type == EventType.Repaint) HudAudit.Begin(safe);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero,Quaternion.identity,Vector3.one * scale);
            if (!session.Started) {
                HudLayout.Clear();
                if (rankedOpen) RankedScreen(); else if (socialOpen) SocialScreen(); else if (collectionOpen) CollectionScreen(); else TitleScreen();
                return;
            }
            var layout = PlanMatch();
            session.Arena.DrawLabels(); HouseInterior();
            MapControls(layout); TopBar(layout); LeftColumn(layout); MiniMap(layout.MiniMap); HouseBoard(layout.Board); Toasts(layout.Toasts);
            if (session.Paused) PauseScreen();
            else if (session.Match.Finished) Results();
        }

        // ---------- In-match layout ----------
        private struct MatchLayout { public Rect Strip, Boss, Gold, Timer, Gear, MiniMap, Left, Board, Toasts; }
        private MatchLayout PlanMatch()
        {
            var l = new MatchLayout(); var area = Cut.Inset(safe,M);
            const float band = 136;
            var top = Cut.Top(ref area,band,G);
            l.Strip = Cut.Left(ref top,12 * 2 + 6 * 96 + 5 * 12,G);
            l.Gear = Cut.Right(ref top,Touch,G); l.Gear = new Rect(l.Gear.x,l.Gear.y + (band - Touch) / 2,Touch,Touch);
            l.Timer = Cut.Right(ref top,196,G); l.Timer = new Rect(l.Timer.x,l.Timer.y + (band - 64) / 2,196,64);
            l.Gold = Cut.Right(ref top,236,G); l.Gold = new Rect(l.Gold.x,l.Gold.y + (band - 64) / 2,236,64);
            // The boss bar sits at screen centre when it fits between the side groups, else in the gap.
            float bossWidth = Mathf.Min(620,top.width);
            float centred = width / 2 - bossWidth / 2;
            l.Boss = new Rect(Mathf.Clamp(centred,top.x,top.xMax - bossWidth),top.y,bossWidth,band);
            if (top.width < 380) { l.Boss = new Rect(width / 2 - 310,area.y,620,band); Cut.Top(ref area,band,G); }
            l.Board = Cut.Bottom(ref area,260,G);
            if (l.Board.width > 1872) l.Board = new Rect(l.Board.center.x - 936,l.Board.y,1872,l.Board.height);
            l.Toasts = Cut.Bottom(ref area,120,0); l.Toasts = Cut.Center(l.Toasts,Mathf.Min(880,l.Toasts.width),120);
            l.MiniMap = new Rect(area.xMax - 300,area.y,300,196);
            float leftRows = session.Scouting || session.Match.Players[0].Eliminated ? 3 : CanOpenRoom ? 2 : 1;
            l.Left = new Rect(area.x,area.y,232,leftRows * Touch + (leftRows - 1) * G);
            // Publish reserved space so the camera and world labels stay clear of the HUD.
            HudLayout.Clear();
            HudLayout.ReservedTop = (l.Strip.yMax + G) / height;
            HudLayout.ReservedBottom = (height - l.Board.y + G) / height;
            HudLayout.ReservedLeft = (l.Left.xMax + G) / width;
            HudLayout.ReservedRight = (width - l.MiniMap.x + G) / width;
            foreach (var r in new[] { l.Strip,l.Boss,l.Gold,l.Timer,l.Gear,l.MiniMap,l.Left,l.Board })
                HudLayout.Block(new Rect(r.x * scale,r.y * scale,r.width * scale,r.height * scale));
            if (!string.IsNullOrEmpty(session.CurrentNotice) || RouteWarning()) HudLayout.Block(new Rect(l.Toasts.x * scale,l.Toasts.y * scale,l.Toasts.width * scale,l.Toasts.height * scale));
            return l;
        }
        private bool RouteWarning()
        {
            var m = session.Match; var p = m.Players[0];
            return m.Boss.Phase == BossPhase.Telegraphing && p.HouseId >= 0 && !p.Eliminated && System.Array.IndexOf(m.Boss.RouteHouses,p.HouseId) >= 0;
        }
        // Players ordered by house number; residents without a house come last.
        private List<PlayerState> HouseOrder()
        {
            var list = new List<PlayerState>(session.Match.Players);
            list.Sort((a,b) => {
                int ha = a.HouseId >= 0 ? a.HouseId : 100 + a.Id, hb = b.HouseId >= 0 ? b.HouseId : 100 + b.Id;
                return ha.CompareTo(hb);
            });
            return list;
        }
        private void TopBar(MatchLayout l)
        {
            var m = session.Match;
            // Player strip.
            HudTheme.Panel(l.Strip);
            var cells = Cut.Row(Cut.Inset(l.Strip,12),6,12); var order = HouseOrder();
            for (int i = 0; i < 6; i++) {
                var p = order[i]; var cell = cells[i]; var face = new Rect(cell.x,cell.y,96,96);
                HudTheme.Card(face);
                var old = GUI.color; if (p.Eliminated) GUI.color = new Color(.45f,.45f,.5f,1);
                Portrait(Cut.Inset(face,4),p.Id == 0 ? session.Inventory.Equipped(p.Character.Id) : session.Collections.DefaultSkin(p.Character.Id),true);
                GUI.color = old;
                if (session.ViewedPlayer == p.Id) HudTheme.Ring(face);
                var badge = new Rect(face.xMax - 58,face.yMax - 30,58,28);
                HudTheme.Fill(badge,p.Eliminated ? HudTheme.DangerFill : p.Id == 0 ? HudTheme.PrimaryFill : HudTheme.SecondaryFill,10);
                HudTheme.OutlinedText(badge,p.Eliminated ? "OUT" : p.HouseId >= 0 ? (p.HouseId + 1).ToString("00") : "--",HudTheme.Label,Color.white,TextAnchor.MiddleCenter);
                float hp = p.HouseId < 0 || p.Eliminated ? 0 : m.Houses[p.HouseId].Health / m.Houses[p.HouseId].MaxHealth;
                HudTheme.Bar(new Rect(cell.x,face.yMax + 8,96,8),hp,p.Id == 0 ? HudTheme.Good : HudTheme.Info);
                if (Hit(new Rect(cell.x,cell.y,96,112),"Resident " + p.Name)) session.Scout(p.Id);
            }
            // Boss bar.
            HudTheme.Panel(l.Boss);
            var inner = Cut.Inset(l.Boss,14);
            var row = Cut.Top(ref inner,36,6);
            bool prep = m.Phase == MatchPhase.Preparation;
            HudTheme.Text(Cut.Left(ref row,200),prep ? "PREPARE" : "WAVE " + m.Boss.Wave,HudTheme.Body,prep ? HudTheme.Gold : HudTheme.Ink,true);
            HudTheme.Text(row,BossStatus(),HudTheme.Label,HudTheme.Muted,false,TextAnchor.MiddleRight);
            HudTheme.Bar(Cut.Top(ref inner,30,6),m.Boss.Health / m.Boss.MaxHealth,HudTheme.Bad,HudTheme.Number(Mathf.Ceil(m.Boss.Health)) + " / " + HudTheme.Number(m.Boss.MaxHealth));
            HudTheme.Text(inner,BossDetail(),HudTheme.Label,HudTheme.Muted,false,TextAnchor.MiddleCenter);
            // Gold and timer pills, settings gear.
            HudTheme.Pill(l.Gold); HudIcons.Draw(new Rect(l.Gold.x + 12,l.Gold.y + 10,44,44),"icon_coin");
            HudTheme.Text(new Rect(l.Gold.x + 64,l.Gold.y,l.Gold.width - 76,l.Gold.height),HudTheme.Number(m.Players[0].Gold),HudTheme.Body,HudTheme.Gold,true);
            HudTheme.Pill(l.Timer); HudIcons.Draw(new Rect(l.Timer.x + 12,l.Timer.y + 10,44,44),"icon_timer");
            HudTheme.Text(new Rect(l.Timer.x + 64,l.Timer.y,l.Timer.width - 76,l.Timer.height),HudTheme.Clock(prep ? m.PreparationRemaining + .99f : m.CombatSeconds),HudTheme.Body,prep ? HudTheme.Gold : HudTheme.Ink,true);
            if (HudTheme.Button(l.Gear,"",ButtonKind.Secondary,!m.Finished)) session.TogglePause();
            HudIcons.Draw(Cut.Inset(l.Gear,20),"icon_gear");
        }
        private string BossStatus()
        {
            var m = session.Match;
            if (m.Phase == MatchPhase.Preparation) return "Storm arrives soon";
            switch (m.Boss.Phase) {
                case BossPhase.Telegraphing: return "Aiming at house " + (m.Boss.TargetHouseId + 1).ToString("00");
                case BossPhase.Travelling: return "Moving to house " + (m.Boss.TargetHouseId + 1).ToString("00");
                case BossPhase.Attacking: return "Attacking house " + (m.Boss.TargetHouseId + 1).ToString("00");
                case BossPhase.Recovery: return "Recovering";
                case BossPhase.Waiting: return m.Finished ? "Match over" : "Waiting";
                case BossPhase.Dead: return "Defeated";
                default: return "Choosing a route";
            }
        }
        private string BossDetail()
        {
            var m = session.Match;
            if (m.Phase == MatchPhase.Preparation) return m.LivingHouses() + " of 12 houses claimed";
            if (m.Boss.Phase == BossPhase.Telegraphing)
                return "Route " + string.Join(" > ",System.Array.ConvertAll(m.Boss.RouteHouses,id => (id + 1).ToString("00"))) + "  ·  " + Mathf.CeilToInt(m.Boss.TelegraphRemaining) + "s";
            return m.LivingHouses() + " houses standing";
        }
        private void LeftColumn(MatchLayout l)
        {
            var m = session.Match; bool finished = m.Finished || session.Paused;
            var area = l.Left;
            if (HudTheme.Button(Cut.Top(ref area,Touch,G),session.Overview ? "FOCUS HOUSE" : "VIEW MAP",ButtonKind.Secondary,!finished && m.Players[session.ViewedPlayer].HouseId >= 0)) session.Overview = !session.Overview;
            if (!session.Scouting && !m.Players[0].Eliminated) {
                if (CanOpenRoom && HudTheme.Button(Cut.Top(ref area,Touch,G),RoomOpen ? "VIEW YARD" : "MY ROOM",ButtonKind.Secondary,!finished)) RoomOpen = !RoomOpen;
                return;
            }
            if (!m.Players[0].Eliminated) {
                if (HudTheme.Button(Cut.Top(ref area,Touch,G),"MY HOUSE",ButtonKind.Primary,!finished)) { session.ReturnToOwnHouse(); ShowBuildBoard(false); }
            }
            else Cut.Top(ref area,Touch,G);
            var arrows = Cut.Row(Cut.Top(ref area,Touch),2,G);
            if (HudTheme.Button(new Rect(arrows[0].x,arrows[0].y,Touch,Touch),"<",ButtonKind.Secondary,!finished,false,HudTheme.Body)) Step(-1);
            if (HudTheme.Button(new Rect(arrows[1].xMax - Touch,arrows[1].y,Touch,Touch),">",ButtonKind.Secondary,!finished,false,HudTheme.Body)) Step(1);
        }
        private void Step(int direction)
        {
            var order = HouseOrder(); int index = order.FindIndex(p => p.Id == session.ViewedPlayer);
            for (int i = 1; i <= 6; i++) {
                var next = order[((index + direction * i) % 6 + 6) % 6];
                if (!session.Match.Players[0].Eliminated || !next.Eliminated) { session.Scout(next.Id); return; }
            }
        }
        private void MiniMap(Rect r)
        {
            var m = session.Match; HudTheme.Panel(r);
            var inner = Cut.Inset(r,20);
            const float cellW = 52, cellH = 36;
            var arena = session.Arena;
            if (m.Boss.Phase == BossPhase.Telegraphing || m.Boss.Phase == BossPhase.Travelling) {
                var previous = arena.MapFraction(m.Boss.Position);
                foreach (var point in m.Boss.RoutePath) {
                    var next = arena.MapFraction(point);
                    for (int i = 0; i <= 10; i++) {
                        var f = Vector2.Lerp(previous,next,i / 10f);
                        HudTheme.Fill(new Rect(inner.x + f.x * inner.width - 2,inner.y + f.y * inner.height - 2,4,4),HudTheme.Hex(0xE86A4A),2);
                    }
                    previous = next;
                }
            }
            foreach (var h in m.Houses) {
                var f = arena.MapFraction(h.Center);
                var cell = new Rect(inner.x + f.x * inner.width - cellW / 2,inner.y + f.y * inner.height - cellH / 2,cellW,cellH);
                cell.x = Mathf.Clamp(cell.x,inner.x - 8,inner.xMax - cellW + 8); cell.y = Mathf.Clamp(cell.y,inner.y - 8,inner.yMax - cellH + 8);
                Color c = h.Destroyed || h.Vacated ? HudTheme.Hex(0x2E3550) : h.OwnerId == 0 ? HudTheme.PrimaryFill : h.OwnerId < 0 ? HudTheme.Hex(0x3A4670) : HudTheme.SecondaryFill;
                HudTheme.Fill(cell,c,8);
                if (h.Id == m.Boss.TargetHouseId) HudTheme.Ring(cell);
                HudTheme.OutlinedText(cell,(h.Id + 1).ToString("00"),HudTheme.Label,h.OwnerId < 0 ? HudTheme.Muted : Color.white,TextAnchor.MiddleCenter);
            }
            var b = arena.MapFraction(m.Boss.Position);
            if (m.Boss.Phase != BossPhase.Dead) HudTheme.Fill(new Rect(inner.x + b.x * inner.width - 8,inner.y + b.y * inner.height - 8,16,16),HudTheme.Bad,8);
        }
        // Free houses on the map are tap targets while claiming.
        private void MapControls(MatchLayout l)
        {
            var m = session.Match; var p = m.Players[0];
            if (p.HouseId >= 0 || p.Eliminated || session.Scouting || m.Phase != MatchPhase.Preparation || session.Paused) return;
            foreach (var h in m.Houses) {
                if (h.OwnerId >= 0) continue;
                Vector2 point = session.Arena.ScreenPoint(h.Center) / scale;
                float unit = Mathf.Abs(session.Arena.ScreenPoint(new Point2(h.Center.X + 1,h.Center.Z)).x / scale - point.x);
                var rect = new Rect(point.x - unit * 2.6f,point.y - unit * 2.2f,unit * 5.2f,unit * 3.8f);
                if (rect.width < Touch || rect.Overlaps(l.Board) || rect.Overlaps(l.Strip) || rect.Overlaps(l.MiniMap) || rect.Overlaps(l.Left) || rect.Overlaps(l.Boss)) continue;
                if (Hit(rect,"Claim house " + (h.Id + 1))) session.WalkToHouse(h.Id);
            }
        }
        private void Toasts(Rect slot)
        {
            var lines = new List<KeyValuePair<string,Color>>();
            if (RouteWarning()) lines.Add(new KeyValuePair<string,Color>("Your house is on the boss route",HudTheme.Bad));
            if (!string.IsNullOrEmpty(session.CurrentNotice) && !session.Match.Finished) lines.Add(new KeyValuePair<string,Color>(session.CurrentNotice,HudTheme.Gold));
            float y = slot.yMax;
            for (int i = lines.Count - 1; i >= 0 && i >= lines.Count - 2; i--) {
                var size = HudTheme.TextStyle(HudTheme.Label,true,TextAnchor.MiddleCenter,false).CalcSize(new GUIContent(lines[i].Key));
                float w = Mathf.Min(slot.width,size.x + 64); var r = new Rect(slot.center.x - w / 2,y - 56,w,56);
                HudTheme.Panel(r,false); HudTheme.Text(Cut.Inset(r,12),lines[i].Key,HudTheme.Label,lines[i].Value,true,TextAnchor.MiddleCenter);
                y -= 64;
            }
        }

        // ---------- Pause and results ----------
        private void PauseScreen()
        {
            Overlay();
            var panel = Cut.Center(safe,Mathf.Min(560,safe.width - M * 2),Mathf.Min(620,safe.height - M * 2));
            HudTheme.Panel(panel); var inner = Cut.Inset(panel,40);
            HudTheme.Text(Cut.Top(ref inner,72,8),"PAUSED",HudTheme.Title,HudTheme.Ink,true,TextAnchor.MiddleCenter);
            HudTheme.Text(Cut.Bottom(ref inner,64,G),"WASD move  ·  E claim or sleep  ·  Tab spectate  ·  Esc pause",HudTheme.Label,HudTheme.Muted,false,TextAnchor.MiddleCenter,true);
            var buttons = Cut.Column(Cut.Bottom(ref inner,Touch * 3 + G * 2),3,G);
            if (HudTheme.Button(buttons[0],"RESUME",ButtonKind.Primary,true,false,HudTheme.Body)) session.TogglePause();
            if (HudTheme.Button(buttons[1],"RESTART",ButtonKind.Secondary,true,false,HudTheme.Body)) session.Play();
            if (HudTheme.Button(buttons[2],"QUIT TO TITLE",ButtonKind.Secondary,true,false,HudTheme.Body)) session.ReturnToTitle();
        }
        private void Results()
        {
            Overlay(); var m = session.Match; var p = m.Players[0];
            bool won = m.Phase == MatchPhase.Victory && !p.Eliminated, lastStanding = m.EndReason == MatchEndReason.LastStanding;
            var panel = Cut.Center(safe,Mathf.Min(760,safe.width - M * 2),Mathf.Min(760,safe.height - M * 2));
            HudTheme.Panel(panel); var inner = Cut.Inset(panel,40);
            string title = won ? (lastStanding ? "LAST ONE STANDING" : "STORM SURVIVED") : "PLACED #" + p.Placement;
            HudTheme.Text(Cut.Top(ref inner,68,4),title,HudTheme.Title,won ? HudTheme.Gold : HudTheme.Bad,true);
            string winner = m.WinnerId >= 0 ? m.Players[m.WinnerId].Name : "";
            string subtitle = won ? (lastStanding ? "Every other home fell." : "Your home held on.") : lastStanding ? winner + " was the last one standing." : m.Phase == MatchPhase.Victory ? "The remaining homes defeated the storm." : "The storm claimed every home.";
            HudTheme.Text(Cut.Top(ref inner,40,G),subtitle,HudTheme.Body,HudTheme.Muted);
            var buttons = Cut.Row(Cut.Bottom(ref inner,Touch,G),2,G);
            if (HudTheme.Button(buttons[0],"PLAY AGAIN",ButtonKind.Play,true,false,HudTheme.Body)) { if (session.PracticeRanked) session.PlayRanked(); else session.Play(); }
            if (HudTheme.Button(buttons[1],"HOME",ButtonKind.Secondary,true,false,HudTheme.Body)) session.ReturnToTitle();
            string[] names = { "Placement","Survival time","Boss damage","Gold earned","Upgrades" };
            string[] values = { "#" + p.Placement + " of 6",HudTheme.Clock(p.SurvivalSeconds),HudTheme.Number(p.DamageDealt),HudTheme.Number(p.GoldEarned),p.UpgradesPurchased.ToString() };
            for (int i = 0; i < names.Length; i++) {
                var row = Cut.Top(ref inner,40,4);
                HudTheme.Text(row,names[i],HudTheme.Body,HudTheme.Muted); HudTheme.Text(row,values[i],HudTheme.Body,HudTheme.Ink,true,TextAnchor.MiddleRight);
            }
            Cut.Top(ref inner,8);
            var reward = session.LastReward;
            if (reward != null) {
                var xpRow = Cut.Top(ref inner,44,6);
                HudTheme.Text(xpRow,"+" + HudTheme.Number(reward.Xp) + " XP",HudTheme.CardTitle,HudTheme.Gold,true);
                HudTheme.Text(xpRow,"Level " + reward.NewLevel,HudTheme.Body,HudTheme.Ink,true,TextAnchor.MiddleRight);
                var account = session.Account;
                HudTheme.Bar(Cut.Top(ref inner,14,8),account.XpNeeded == 0 ? 1 : (float)account.XpInLevel / account.XpNeeded,HudTheme.Good);
                if (reward.Unlocked.Length > 0) HudTheme.Text(Cut.Top(ref inner,30,4),"Unlocked: " + string.Join(", ",System.Array.ConvertAll(reward.Unlocked,id => session.Characters.Get(id).Name)),HudTheme.Label,HudTheme.Good,true);
            }
            if (session.PracticeRanked) HudTheme.Text(Cut.Top(ref inner,30,4),"Practice ranked  ·  " + session.Account.Rank.CurrentRank + "  ·  " + session.Account.Rank.Stars + " / 5 stars",HudTheme.Label,HudTheme.Muted);
            if (session.SaveDirty) HudTheme.Text(Cut.Top(ref inner,30,4),session.SaveStatus,HudTheme.Label,HudTheme.Bad);
        }

        // ---------- Shared helpers ----------
        private void Overlay() { HudTheme.Fill(new Rect(0,0,width,height),HudTheme.Hex(0x070A14,.72f)); }
        private static bool Hit(Rect r,string name) { HudAudit.Interactive(r,name); return GUI.Button(r,GUIContent.none,GUIStyle.none); }
        private void Portrait(Rect rect,string skinId,bool face) { session.Arena.Portraits.Draw(rect,skinId,face); }
        private void MenuBackground()
        {
            MenuArtwork.Background(new Rect(0,0,width,height));
            HudTheme.Fill(new Rect(0,0,width,height),HudTheme.Hex(0x0B1020,.62f));
            HudLayout.Clear();
        }
        // Header panel shared by the menu screens: title plus an optional one-line note and a right-side stat.
        private Rect Header(ref Rect area,string title,string note,string stat = null,string statNote = null)
        {
            var header = Cut.Top(ref area,136,G); HudTheme.Panel(header); var inner = Cut.Inset(header,22);
            if (stat != null) {
                var right = Cut.Right(ref inner,420,G);
                HudTheme.Text(Cut.Top(ref right,44),stat,HudTheme.CardTitle,HudTheme.Gold,true,TextAnchor.MiddleRight);
                HudTheme.Text(right,statNote,HudTheme.Label,HudTheme.Muted,false,TextAnchor.MiddleRight);
            }
            HudTheme.Text(Cut.Top(ref inner,note == null ? inner.height : 64),title,HudTheme.Title,HudTheme.Ink,true);
            if (note != null) HudTheme.Text(inner,note,HudTheme.Label,HudTheme.Muted);
            return header;
        }
        private void OnDestroy() { if (stickerIcons != null) stickerIcons.Dispose(); }
    }
}
