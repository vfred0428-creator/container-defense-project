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
                if (yardOpen) YardScreen(); else if (rankedOpen) RankedScreen(); else if (socialOpen) SocialScreen(); else if (collectionOpen) CollectionScreen(); else TitleScreen();
                return;
            }
            var layout = PlanMatch();
            bool controlsEnabled = GUI.enabled;
            GUI.enabled = controlsEnabled && !session.Paused && !session.Match.Finished;
            session.Arena.DrawLabels(); HouseInterior(layout);
            MapControls(layout); RoomEntry(layout); FullMapOverlay(layout); PadTargets(layout); ThreatEdge(); TopBar(layout); LeftColumn(layout); MiniMap(layout.MiniMap); HouseBoard(layout.Board); Toasts(layout.Toasts); PassiveToast(layout); CoinFlights();
            GUI.enabled = controlsEnabled;
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
            l.MiniMap = new Rect(area.xMax - 264,area.y,264,264);
            var view = session.View; float leftRows = (view.HomeUnderThreat ? 1 : 0) + (view.Mode == ViewMode.FullMap ? 1 : view.Mode == ViewMode.Neighborhood ? 0 : view.ViewingOwnBase ? 1 + (CanOpenRoom ? 1 : 0) : 1 + (session.Match.Players[0].Eliminated || view.HomeUnderThreat ? 0 : 1));
            l.Left = new Rect(area.x,area.y,232,Mathf.Max(0,leftRows * Touch + (leftRows - 1) * G));
            // Publish reserved space so the camera and world labels stay clear of the HUD.
            HudLayout.Clear();
            HudLayout.GoldTarget = new Vector2((l.Gold.x + 44) * scale,(l.Gold.y + 32) * scale);
            HudLayout.ReservedTop = (l.Strip.yMax + G) / height;
            HudLayout.ReservedBottom = (height - l.Board.y + G) / height;
            HudLayout.ReservedLeft = (l.Left.xMax + G) / width;
            HudLayout.ReservedRight = (width - l.MiniMap.x + G) / width;
            foreach (var r in new[] { l.Strip,l.Boss,l.Gold,l.Timer,l.Gear,l.MiniMap,l.Left,l.Board })
                HudLayout.Block(new Rect(r.x * scale,r.y * scale,r.width * scale,r.height * scale));
            if (!string.IsNullOrEmpty(session.CurrentNotice) || RouteWarning() || session.Interior.BossIncoming) HudLayout.Block(new Rect(l.Toasts.x * scale,l.Toasts.y * scale,l.Toasts.width * scale,l.Toasts.height * scale));
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
                HudTheme.Fill(face,p.Id == 0 ? HudTheme.Hex(0x2C5E46) : HudTheme.Hex(0x2B3558),48);
                var old = GUI.color; if (p.Eliminated) GUI.color = new Color(.45f,.45f,.5f,1);
                Portrait(Cut.Inset(face,4),p.Id == 0 ? session.Inventory.Equipped(p.Character.Id) : session.Collections.DefaultSkin(p.Character.Id),true);
                GUI.color = old;
                // Round frame as in the mocks; the gold ring marks whose base you are looking at.
                HudIcons.Draw(face,"round_frame");
                if (session.ViewedPlayer == p.Id && session.View.Mode == ViewMode.Base) HudIcons.Draw(new Rect(face.x - 4,face.y - 4,104,104),"round_ring");
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
            HudIcons.Draw(Cut.Left(ref row,36,8),"icon_skull");
            HudTheme.Text(Cut.Left(ref row,180),prep ? "PREPARE" : "WAVE " + m.Boss.Wave,HudTheme.Body,prep ? HudTheme.Gold : HudTheme.Ink,true);
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
        // Left column, by view: your base (full map, room), someone else's base (back, prev/next),
        // the full map (close), plus a GO HOME shortcut whenever the boss lines up your house.
        private void LeftColumn(MatchLayout l)
        {
            var m = session.Match; var view = session.View; bool finished = m.Finished || session.Paused;
            var area = l.Left;
            if (view.HomeUnderThreat && HudTheme.Button(Cut.Top(ref area,Touch,G),"GO HOME",ButtonKind.Danger,!finished)) { session.ReturnToOwnHouse(); ShowBuildBoard(false); }
            if (view.Mode == ViewMode.FullMap) {
                if (HudTheme.Button(Cut.Top(ref area,Touch,G),"CLOSE MAP",ButtonKind.Secondary,!finished)) session.CloseFullMap();
                return;
            }
            if (view.Mode == ViewMode.Neighborhood) return;
            if (view.ViewingOwnBase) {
                if (HudTheme.Button(Cut.Top(ref area,Touch,G),"FULL MAP",ButtonKind.Secondary,!finished)) session.OpenFullMap();
                if (CanOpenRoom && HudTheme.Button(Cut.Top(ref area,Touch,G),session.Interior.ForcedOut ? "UNDER ATTACK" : RoomOpen ? "GO OUTSIDE" : "GO INSIDE",ButtonKind.Secondary,!finished && !session.Interior.ForcedOut)) { session.Interior.Toggle(); ShowBuildBoard(false); }
                return;
            }
            if (!m.Players[0].Eliminated && !view.HomeUnderThreat) {
                if (HudTheme.Button(Cut.Top(ref area,Touch,G),"MY BASE",ButtonKind.Primary,!finished)) { session.ReturnToOwnHouse(); ShowBuildBoard(false); }
            }
            var arrows = Cut.Row(Cut.Top(ref area,Touch),2,G);
            if (HudTheme.Button(new Rect(arrows[0].x,arrows[0].y,Touch,Touch),"<",ButtonKind.Secondary,!finished,false,HudTheme.Body)) view.Spectate(-1);
            if (HudTheme.Button(new Rect(arrows[1].xMax - Touch,arrows[1].y,Touch,Touch),">",ButtonKind.Secondary,!finished,false,HudTheme.Body)) view.Spectate(1);
        }
        // Full-map view: owner portraits and HP over every house; tapping a house scouts it.
        private void FullMapOverlay(MatchLayout l)
        {
            var m = session.Match; if (session.View.Mode != ViewMode.FullMap || session.Paused) return;
            foreach (var h in m.Houses) {
                var screen = session.Arena.HouseScreenRect(h.Id);
                var r = new Rect(screen.x / scale,screen.y / scale,screen.width / scale,screen.height / scale);
                if (r.Overlaps(l.Board) || r.Overlaps(l.Strip) || r.Overlaps(l.MiniMap) || r.Overlaps(l.Left) || r.Overlaps(l.Boss)) continue;
                if (h.OwnerId >= 0) {
                    var owner = m.Players[h.OwnerId]; float size = Mathf.Clamp(r.height * .45f,48,88);
                    var face = new Rect(r.center.x - size / 2,r.y - size * .35f,size,size);
                    HudTheme.Fill(face,HudTheme.Hex(0x1B2238,.92f),size / 2);
                    var old = GUI.color; if (!h.Occupied) GUI.color = new Color(.45f,.45f,.5f,1);
                    Portrait(Cut.Inset(face,4),owner.Id == 0 ? session.Inventory.Equipped(owner.Character.Id) : session.Collections.DefaultSkin(owner.Character.Id),true);
                    GUI.color = old;
                    if (h.Id == m.Boss.TargetHouseId) HudTheme.Ring(face);
                }
                // Tap the front of the container: house art overlaps the row behind it, the fronts never do.
                var tap = new Rect(r.x + r.width * .12f,r.y + r.height * .45f,r.width * .76f,r.height * .5f);
                bool inside = tap.xMin >= safe.xMin && tap.xMax <= safe.xMax && tap.yMin >= safe.yMin && tap.yMax <= safe.yMax;
                if (inside && tap.width >= Touch && Hit(tap,"Map house " + (h.Id + 1))) session.ViewHouse(h.Id);
            }
        }
        // Red screen edge while the boss lines up your house and you are looking elsewhere.
        private void ThreatEdge()
        {
            if (!session.View.HomeUnderThreat || session.Match.Finished) return;
            float pulse = .35f + Mathf.Sin(Time.unscaledTime * 6) * .15f, e = 18; var c = HudTheme.Hex(0xFF3B4A,pulse);
            HudTheme.Fill(new Rect(0,0,width,e),c); HudTheme.Fill(new Rect(0,height - e,width,e),c);
            HudTheme.Fill(new Rect(0,0,e,height),c); HudTheme.Fill(new Rect(width - e,0,e,height),c);
        }
        private void MiniMap(Rect r)
        {
            // Round minimap as in the mocks: rim, navy disc, houses laid out inside the inscribed square.
            var m = session.Match;
            HudTheme.Fill(new Rect(r.x - 3,r.y - 3,r.width + 6,r.height + 6),HudTheme.Hex(0xE8ECF6,.9f),r.width / 2 + 3);
            HudTheme.Fill(r,HudTheme.Hex(0x1B2238,.95f),r.width / 2);
            var inner = Cut.Center(r,180,180);
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
                Color c = h.Destroyed || h.Vacated ? HudTheme.Hex(0x4A5070) : h.OwnerId == 0 ? HudTheme.PrimaryFill : h.OwnerId < 0 ? HudTheme.Hex(0x8A93AD) : HudTheme.Info;
                // House-shaped markers tinted by owner, as in the mocks' minimap.
                var oldColor = GUI.color; GUI.color = c; HudIcons.Draw(Cut.Center(cell,cellH + 8,cellH + 8),"icon_house"); GUI.color = oldColor;
                if (h.Id == m.Boss.TargetHouseId) HudTheme.Ring(cell);
                HudTheme.OutlinedText(cell,(h.Id + 1).ToString("00"),HudTheme.Label,h.OwnerId < 0 ? HudTheme.Muted : Color.white,TextAnchor.MiddleCenter);
            }
            var b = arena.MapFraction(m.Boss.Position);
            if (m.Boss.Phase != BossPhase.Dead) HudTheme.Fill(new Rect(inner.x + b.x * inner.width - 8,inner.y + b.y * inner.height - 8,16,16),HudTheme.Bad,8);
            // The whole minimap opens the full-map view.
            if (!session.Paused && !m.Finished && Hit(r,"Minimap")) { if (session.View.Mode == ViewMode.FullMap) session.CloseFullMap(); else session.OpenFullMap(); }
        }
        // Free houses on the map are tap targets while claiming.
        private void MapControls(MatchLayout l)
        {
            var m = session.Match; var p = m.Players[0];
            if (p.HouseId >= 0 || p.Eliminated || session.View.Mode != ViewMode.Neighborhood || m.Phase != MatchPhase.Preparation || session.Paused) return;
            foreach (var h in m.Houses) {
                if (h.OwnerId >= 0) continue;
                Vector2 point = session.Arena.ScreenPoint(h.Center) / scale;
                float unit = Mathf.Abs(session.Arena.ScreenPoint(new Point2(h.Center.X + 1,h.Center.Z)).x / scale - point.x);
                var rect = new Rect(point.x - unit * 2.6f,point.y - unit * 2.2f,unit * 5.2f,unit * 3.8f);
                if (rect.xMin < safe.xMin || rect.xMax > safe.xMax || rect.yMin < safe.yMin || rect.yMax > safe.yMax) continue;
                if (rect.width < Touch || rect.Overlaps(l.Board) || rect.Overlaps(l.Strip) || rect.Overlaps(l.MiniMap) || rect.Overlaps(l.Left) || rect.Overlaps(l.Boss)) continue;
                if (Hit(rect,"Claim house " + (h.Id + 1))) session.WalkToHouse(h.Id);
            }
        }
        private void Toasts(Rect slot)
        {
            var lines = new List<KeyValuePair<string,Color>>();
            var view = session.View;
            if (view.Mode == ViewMode.FullMap) lines.Add(new KeyValuePair<string,Color>("Full map  ·  tap a house to view that base",HudTheme.Ink));
            else if (view.ViewingOther) lines.Add(new KeyValuePair<string,Color>((view.Spectating ? "Spectating " : "Viewing ") + session.Match.Players[view.ViewedPlayer].Name + "'s base  ·  read only",HudTheme.Ink));
            if (session.Interior.BossIncoming) lines.Add(new KeyValuePair<string,Color>("The boss is coming to your house!  " + Mathf.CeilToInt(session.Interior.BossEta) + "s",HudTheme.Bad));
            else if (view.HomeUnderThreat) lines.Add(new KeyValuePair<string,Color>("The boss is heading for your house!",HudTheme.Bad));
            else if (RouteWarning()) lines.Add(new KeyValuePair<string,Color>("Your house is on the boss route",HudTheme.Bad));
            if (lines.Count < 2 && !string.IsNullOrEmpty(session.CurrentNotice) && !session.Match.Finished) lines.Add(new KeyValuePair<string,Color>(session.CurrentNotice,HudTheme.Gold));
            float y = slot.yMax;
            for (int i = lines.Count - 1; i >= 0 && i >= lines.Count - 2; i--) {
                var size = HudTheme.TextStyle(HudTheme.Label,true,TextAnchor.MiddleCenter,false).CalcSize(new GUIContent(lines[i].Key));
                float w = Mathf.Min(slot.width,size.x + 64); var r = new Rect(slot.center.x - w / 2,y - 56,w,56);
                bool danger = lines[i].Value == HudTheme.Bad;
                if (danger) { HudTheme.Fill(r,HudTheme.Hex(0x000000,.35f),14); HudTheme.Fill(Cut.Inset(r,2),HudTheme.DangerFill,12); } else HudTheme.Panel(r,false);
                HudTheme.Text(Cut.Inset(r,12),lines[i].Key,HudTheme.Label,danger ? Color.white : lines[i].Value,true,TextAnchor.MiddleCenter);
                y -= 64;
            }
        }

        // ---------- Pause and results ----------
        private void PauseScreen()
        {
            Overlay();
            var panel = Cut.Center(safe,Mathf.Min(620,safe.width - M * 2),Mathf.Min(820,safe.height - M * 2));
            HudTheme.Panel(panel); var inner = Cut.Inset(panel,32);
            HudTheme.Text(Cut.Top(ref inner,64,8),"PAUSED",HudTheme.Title,HudTheme.Ink,true,TextAnchor.MiddleCenter);
            SoundSettings(ref inner);
            var buttons = Cut.Column(Cut.Bottom(ref inner,Touch * 3 + G * 2),3,G);
            if (HudTheme.Button(buttons[0],"RESUME",ButtonKind.Primary,true,false,HudTheme.Body)) session.TogglePause();
            if (HudTheme.Button(buttons[1],"RESTART",ButtonKind.Secondary,true,false,HudTheme.Body)) session.Play();
            if (HudTheme.Button(buttons[2],"QUIT TO TITLE",ButtonKind.Secondary,true,false,HudTheme.Body)) session.ReturnToTitle();
        }
        private void Results()
        {
            Overlay(); var m = session.Match; var p = m.Players[0];
            bool won = m.Phase == MatchPhase.Victory && !p.Eliminated && p.Placement == 1, survived = m.Phase == MatchPhase.Victory && !p.Eliminated, lastStanding = m.EndReason == MatchEndReason.LastStanding;
            var panel = Cut.Center(safe,Mathf.Min(760,safe.width - M * 2),Mathf.Min(760,safe.height - M * 2));
            HudTheme.Panel(panel); var inner = Cut.Inset(panel,40);
            string title = won ? (lastStanding ? "LAST ONE STANDING" : "STORM SURVIVED") : "PLACED #" + p.Placement;
            HudTheme.Text(Cut.Top(ref inner,68,4),title,HudTheme.Title,won ? HudTheme.Gold : HudTheme.Bad,true);
            string winner = m.WinnerId >= 0 ? m.Players[m.WinnerId].Name : "";
            string subtitle = won ? (lastStanding ? "Every other home fell." : "Your home held on and dealt the most damage.") : survived ? "The storm fell. Survivors rank by damage dealt to it." : lastStanding ? winner + " was the last one standing." : m.Phase == MatchPhase.Victory ? "The remaining homes defeated the storm." : "The storm claimed every home.";
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

        // Music and SFX volume with mute, saved in the account (AudioPrefs). Slider rows are touch-sized.
        private void SoundSettings(ref Rect area)
        {
            var prefs = session.Account.Audio; bool changed = false;
            changed |= VolumeRow(Cut.Top(ref area,Touch,G),"MUSIC",ref prefs.MusicVolume);
            changed |= VolumeRow(Cut.Top(ref area,Touch,G),"SOUND",ref prefs.SfxVolume);
            if (HudTheme.Button(Cut.Top(ref area,Touch,G),prefs.Muted ? "SOUND OFF" : "SOUND ON",prefs.Muted ? ButtonKind.Danger : ButtonKind.Secondary,true,false,HudTheme.Body)) { prefs.Muted = !prefs.Muted; changed = true; }
            if (changed) { session.Account.SetAudio(prefs); GameAudio.Apply(prefs); session.PersistAccount(); }
        }
        private bool VolumeRow(Rect row,string name,ref float value)
        {
            HudTheme.Text(Cut.Left(ref row,150,G),name,HudTheme.Body,HudTheme.Ink,true);
            var minus = Cut.Left(ref row,Touch,G); var plus = Cut.Right(ref row,Touch,G);
            bool changed = false;
            if (HudTheme.Button(minus,"-",ButtonKind.Secondary,value > 0,false,HudTheme.CardTitle)) { value = Mathf.Max(0,Mathf.Round(value * 10 - 1) / 10); changed = true; }
            if (HudTheme.Button(plus,"+",ButtonKind.Secondary,value < 1,false,HudTheme.CardTitle)) { value = Mathf.Min(1,Mathf.Round(value * 10 + 1) / 10); changed = true; }
            var bar = Cut.Center(row,row.width,24); HudTheme.Bar(bar,value,HudTheme.Gold,Mathf.RoundToInt(value * 100) + "%");
            return changed;
        }
        // ---------- Shared helpers ----------
        private void Overlay() { HudTheme.Fill(new Rect(0,0,width,height),HudTheme.Hex(0x070A14,.72f)); }
        private static bool Hit(Rect r,string name) { HudAudit.Interactive(r,name); return GUI.Button(r,GUIContent.none,GUIStyle.none); }
        private void Portrait(Rect rect,string skinId,bool face) { session.Arena.Portraits.Draw(rect,skinId,face); }
        private void MenuBackground()
        {
            MenuArtwork.Background(new Rect(0,0,width,height));
            // Keep the art visible; darken only enough for the panels, more toward the bottom where the UI sits.
            // MenuArtwork already blurs and darkens; add a deeper floor where the cards and buttons sit.
            HudTheme.Fill(new Rect(0,height * .6f,width,height * .4f),HudTheme.Hex(0x0B1020,.35f));
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
