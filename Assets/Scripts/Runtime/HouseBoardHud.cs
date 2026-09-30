using ContainerDefense.Domain;
using UnityEngine;
namespace ContainerDefense
{
    public sealed partial class MatchHud
    {
        private bool buildOpen;
        private int buildSlot;
        private int movingSlot = -1;
        public void ShowBuildBoard(bool visible) { buildOpen = visible; movingSlot = -1; buildSlot = 0; }
        private void MapControls()
        {
            var m = session.Match; var p = m.Players[0]; float scale = Screen.width / width;
            if (p.HouseId < 0 && !p.Eliminated && !session.Scouting && m.Phase == MatchPhase.Preparation && !session.Paused) {
                foreach (var h in m.Houses) {
                    if (h.OwnerId >= 0) continue;
                    Vector2 point = session.Arena.ScreenPoint(h.Center) / scale;
                    float pixel = Screen.height / (session.Arena.Camera.orthographicSize * 2) / scale;
                    var rect = new Rect(point.x - pixel * 4,point.y - pixel * 2.5f,pixel * 8,pixel * 5);
                    if (point.y > 148 && point.y < height - 172 && GUI.Button(rect,GUIContent.none,GUIStyle.none)) session.WalkToHouse(h.Id);
                }
            }
            var viewed = m.Players[session.ViewedPlayer];
            if (viewed.HouseId >= 0 && !session.Overview && buildOpen) {
                var house = m.Houses[viewed.HouseId];
                for (int slot = 0; slot < 3; slot++) {
                    Vector2 point = session.Arena.ScreenPoint(m.WeaponPoint(house.Id,slot)) / scale;
                    var r = new Rect(point.x - 34,point.y - 32,68,64);
                    if (house.Weapons[slot] == null) Box(r,new Color(.2f,.38f,.42f,.7f));
                    if (Button(new Rect(point.x - 28,point.y + 28,56,30),(slot + 1).ToString(),buildSlot == slot ? gold : muted,!session.Scouting)) {
                        if (movingSlot >= 0) { if (session.MoveWeapon(movingSlot,slot)) movingSlot = -1; }
                        buildSlot = slot;
                    }
                }
            }
            if (m.Boss.Phase == BossPhase.Telegraphing && p.HouseId >= 0 && System.Array.IndexOf(m.Boss.RouteHouses,p.HouseId) >= 0) {
                Box(new Rect(width / 2 - 236,124,472,38),panel);
                Label(new Rect(width / 2 - 220,131,440,28),"YOUR HOUSE IS ON THE BOSS ROUTE",body,red);
            }
        }
        private void MapOverviewButton()
        {
            if (session.Paused || session.Match.Finished) return;
            if (Button(new Rect(24,142,176,42),session.Overview ? "FOCUS HOUSE" : "VIEW MAP",muted,session.Match.Players[session.ViewedPlayer].HouseId >= 0)) session.Overview = !session.Overview;
            if (session.Scouting) {
                if (Button(new Rect(212,142,246,42),"RETURN TO MY HOUSE",gold,!session.Match.Players[0].Eliminated)) { session.ReturnToOwnHouse(); buildOpen = false; movingSlot = -1; }
                if (Button(new Rect(24,196,78,38),"<",muted)) session.Scout((session.ViewedPlayer + 5) % 6);
                if (Button(new Rect(114,196,78,38),">",muted)) session.Scout((session.ViewedPlayer + 1) % 6);
            }
            MiniMap();
        }
        private void MiniMap()
        {
            var m = session.Match; var r = new Rect(width - 216,134,192,168); Box(r,panel);
            if (m.Boss.Phase == BossPhase.Telegraphing || m.Boss.Phase == BossPhase.Travelling) {
                var previous = m.Boss.Position;
                foreach (var point in m.Boss.RoutePath) {
                    int steps = Mathf.Max(1,Mathf.CeilToInt(previous.Distance(point) * 2));
                    for (int i = 0; i <= steps; i++) {
                        float t = (float)i / steps;
                        float px = Mathf.Lerp(previous.X,point.X,t), pz = Mathf.Lerp(previous.Z,point.Z,t);
                        Box(new Rect(r.x + 95 + px * 5,r.y + 79 - pz * 4.8f,2,2),gold);
                    }
                    previous = point;
                }
            }
            foreach (var h in m.Houses) {
                float x = r.x + 96 + h.Center.X * 5, y = r.y + 80 - h.Center.Z * 4.8f;
                Color color = h.Destroyed ? new Color(.25f,.27f,.32f) : h.OwnerId == 0 ? green : h.OwnerId < 0 ? muted : new Color(.45f,.65f,.92f);
                if (h.Id == m.Boss.TargetHouseId) color = red;
                if (Button(new Rect(x - 18,y - 12,36,24),(h.Id + 1).ToString("00"),color)) {
                    if (h.OwnerId >= 0) session.Scout(h.OwnerId); else session.WalkToHouse(h.Id);
                }
            }
            float bx = r.x + 96 + m.Boss.Position.X * 5, by = r.y + 80 - m.Boss.Position.Z * 4.8f;
            Box(new Rect(bx - 4,by - 4,8,8),red);
        }
        private void HouseBoard()
        {
            var m = session.Match; var p = m.Players[session.ViewedPlayer];
            float y = height - 176;
            Box(new Rect(24,y,width - 48,152),panel);
            if (p.HouseId < 0) {
                Label(new Rect(48,y + 24,width - 96,40),p.Eliminated ? "ELIMINATED / Tap a portrait to spectate" : "CHOOSE A FREE HOUSE",heading,cream);
                Label(new Rect(48,y + 76,width - 96,46),"Tap a house to run and claim it. Or move with WASD and press E at its door. Twelve houses; one owner each.",body,muted); return;
            }
            var h = m.Houses[p.HouseId]; bool own = !session.Scouting && !p.Eliminated && !m.Finished && !session.Paused;
            Label(new Rect(48,y + 16,246,40),"HOUSE " + (h.Id + 1).ToString("00"),heading,cream);
            Label(new Rect(48,y + 62,246,38),session.Scouting ? "VIEWING " + p.Name : "MY HOUSE",body,session.Scouting ? muted : gold);
            if (session.Scouting) Label(new Rect(48,y + 108,246,28),"READ ONLY",small,muted);
            else if (Button(new Rect(48,y + 104,246,32),p.Sleeping ? "SLEEPING / WAKE" : "SLEEP FOR GOLD",muted,own)) session.Interact();
            float x = 324, available = width - 374, card = (available - 36) / 4;
            Bar(new Rect(x,y + 16,available - 160,12),h.Health / h.MaxHealth,green);
            Label(new Rect(width - 196,y + 8,146,30),Mathf.CeilToInt(h.Health) + " / " + Mathf.CeilToInt(h.MaxHealth),body,cream);
            int bed = m.UpgradeCost(h.Id,UpgradeKind.Bed), door = m.UpgradeCost(h.Id,UpgradeKind.Door);
            if (Button(new Rect(x,y + 48,card,80),"BED " + (h.BedLevel + 1) + "\n" + (bed < 0 ? "MAX" : bed + " gold"),green,own && bed >= 0 && !h.IsBuilding)) session.Buy(UpgradeKind.Bed);
            if (Button(new Rect(x + card + 12,y + 48,card,80),"DOOR " + (h.DoorLevel + 1) + "\n" + (door < 0 ? "MAX" : door + " gold"),green,own && door >= 0 && !h.IsBuilding)) session.Buy(UpgradeKind.Door);
            if (Button(new Rect(x + (card + 12) * 2,y + 48,card,80),buildOpen ? "CLOSE BUILD" : "BUILD",gold,own || session.Scouting)) { buildOpen = !buildOpen; movingSlot = -1; session.Overview = false; }
            if (Button(new Rect(x + (card + 12) * 3,y + 48,card,80),"REPAIR\n" + MatchSimulation.RepairCost + " gold",muted,own && h.Health < h.MaxHealth)) { if (!session.Repair()) session.Notify("Not enough gold, or a build is in progress."); }
            if (h.IsBuilding) Label(new Rect(x,y - 32,500,25),"Building " + h.BuildingKind + " / " + h.BuildRemaining.ToString("0.0") + "s",small,gold);
            if (buildOpen) {
                BuildPanel(h,own,y);
                if (!string.IsNullOrEmpty(session.CurrentNotice)) Label(new Rect(346,y - 220,width - 380,32),session.CurrentNotice,small,gold);
            }
            else if (!string.IsNullOrEmpty(session.CurrentNotice)) { Box(new Rect(width / 2 - 350,y - 48,700,38),panel); Label(new Rect(width / 2 - 334,y - 40,668,28),session.CurrentNotice,small,gold); }
        }
        private void BuildPanel(HouseState home,bool own,float bottom)
        {
            float y = bottom - 184; Box(new Rect(324,y,width - 348,168),panel);
            Label(new Rect(346,y + 12,550,30),session.Scouting ? "SCOUTING DEFENSES / READ ONLY" : movingSlot >= 0 ? "CHOOSE AN EMPTY DESTINATION SLOT" : "SELECT A ROOFTOP SOCKET",body,cream);
            for (int slot = 0; slot < 3; slot++) {
                var w = home.Weapons[slot]; string text = (slot + 1) + ": " + (w == null ? "EMPTY" : WeaponCatalog.Get(w.Kind).Name + " Lv." + w.Level);
                if (Button(new Rect(346 + slot * 212,y + 48,200,38),text,buildSlot == slot ? gold : muted)) {
                    if (movingSlot >= 0 && own && session.MoveWeapon(movingSlot,slot)) movingSlot = -1;
                    buildSlot = slot;
                }
            }
            var selected = home.Weapons[buildSlot];
            if (selected == null) {
                for (int i = 0; i < 4; i++) {
                    var d = WeaponCatalog.Get((WeaponKind)i); float w = (width - 408) / 4;
                    if (Button(new Rect(346 + i * (w + 6),y + 104,w,44),d.Name + " / " + d.Cost,gold,own)) { if (!session.PlaceWeapon(buildSlot,d.Id)) session.Notify("Cannot place: check gold, socket and current build."); }
                }
            } else {
                int cost = session.Match.WeaponUpgradeCost(home.Id,buildSlot);
                if (Button(new Rect(346,y + 104,240,44),selected.Building ? "BUILDING..." : cost < 0 ? "MAX LEVEL" : "UPGRADE / " + cost,green,own && cost >= 0 && !selected.Building)) session.UpgradeWeapon(buildSlot);
                if (Button(new Rect(598,y + 104,180,44),"MOVE",muted,own && !selected.Building)) movingSlot = buildSlot;
                if (Button(new Rect(790,y + 104,224,44),"SELL / " + System.Math.Floor(selected.Invested * .5),muted,own && !selected.Building)) { session.SellWeapon(buildSlot); movingSlot = -1; }
                Label(new Rect(1034,y + 104,width - 1080,48),"Range " + WeaponCatalog.Get(selected.Kind).Range,small,muted);
            }
        }
    }
}
