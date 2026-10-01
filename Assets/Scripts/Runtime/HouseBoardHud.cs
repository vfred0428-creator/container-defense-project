using ContainerDefense.Domain;
using UnityEngine;
namespace ContainerDefense
{
    // Bottom house panel: at most 30% of the screen. Your house (or the one you are viewing) with its three
    // weapon slots as large cards. Weapons stand on build pads in the yard: pick a weapon, then tap a free pad;
    // MOVE then a free pad moves it. Viewing other houses is read-only.
    public sealed partial class MatchHud
    {
        private int selectedSlot = -1, movingSlot = -1;
        private bool picking;
        private int placingKind = -1;   // weapon chosen in the picker, waiting for a pad tap
        public void BeginPlacing(WeaponKind kind) { placingKind = (int)kind; picking = false; }
        public void ShowBuildBoard(bool visible) { selectedSlot = visible ? 0 : -1; movingSlot = -1; picking = false; placingKind = -1; }
        private void HouseBoard(Rect panel)
        {
            var m = session.Match; var viewed = m.Players[session.ViewedPlayer];
            HudTheme.Panel(panel); var inner = Cut.Inset(panel,20);
            if (viewed.HouseId < 0) { ClaimBoard(inner,viewed); return; }
            var h = session.ScoutView[viewed.HouseId];
            bool own = session.View.CanCommand && !session.Paused;
            var left = Cut.Left(ref inner,432,24);
            HouseSummary(left,h,viewed,own);
            if (RoomOpen && own) { StationCards(inner,h.HouseId); return; }
            if (!own) { placingKind = -1; movingSlot = -1; }
            if (own && (placingKind >= 0 || movingSlot >= 0)) PadPrompt(inner,h);
            else if (own && picking && selectedSlot >= 0) WeaponPicker(inner,h); else Sockets(inner,h,own);
        }
        private void ClaimBoard(Rect inner,PlayerState viewed)
        {
            var m = session.Match; bool human = viewed.Id == 0 && !viewed.Eliminated;
            var action = Cut.Right(ref inner,420,24);
            HudTheme.Text(Cut.Top(ref inner,48,8),viewed.Eliminated ? viewed.Name + " is out" : human ? "CHOOSE A FREE HOUSE" : viewed.Name + " has no house yet",HudTheme.CardTitle,HudTheme.Ink,true);
            HudTheme.Text(Cut.Top(ref inner,84),human ? "Tap a free house on the map, or walk to its door and press E. Twelve houses, one owner each." : "Scouting is read-only.",HudTheme.Body,HudTheme.Muted,false,TextAnchor.UpperLeft,true);
            if (!human) return;
            int nearby = session.NearbyHouse(); bool free = nearby >= 0 && m.Houses[nearby].OwnerId < 0;
            var button = Cut.Center(action,action.width,Touch);
            if (HudTheme.Button(button,free ? "CLAIM HOUSE " + (nearby + 1).ToString("00") : "WALK TO A DOOR",ButtonKind.Play,free && m.Phase == MatchPhase.Preparation,false,HudTheme.Body)) session.Interact();
        }
        private void HouseSummary(Rect r,HouseScout h,PlayerState viewed,bool own)
        {
            var m = session.Match;
            // House thumbnail and big number, as in the mocks.
            var title = Cut.Top(ref r,76,8);
            var thumb = Cut.Left(ref title,112,12); HudTheme.Card(thumb); session.Arena.DrawHouseThumb(Cut.Inset(thumb,4),h.HouseId);
            HudTheme.Text(Cut.Top(ref title,46),(h.HouseId + 1).ToString("00"),HudTheme.Title,HudTheme.Ink,true);
            bool mine = viewed.Id == 0 && !m.Players[0].Eliminated;
            string tag = !own && mine ? (m.Finished ? "Match over" : "Full map") : own ? (viewed.Sleeping ? "+" + m.Income(h.HouseId).ToString("0.#") + " gold/s" : "Awake") : viewed.Eliminated ? "Out" : m.Players[0].Eliminated ? "Spectating " + viewed.Name : viewed.Name + " · read only";
            HudTheme.Text(title,tag,HudTheme.Label,own && viewed.Sleeping ? HudTheme.Good : HudTheme.Muted,true);
            var hp = Cut.Top(ref r,28,12);
            HudIcons.Draw(Cut.Left(ref hp,32,8),"icon_heart");
            HudTheme.Bar(hp,h.MaxHealth > 0 ? h.Health / h.MaxHealth : 0,own || viewed.Id == 0 ? HudTheme.Good : HudTheme.Info,HudTheme.Number(Mathf.Ceil(h.Health)) + " / " + HudTheme.Number(h.MaxHealth));
            var buttons = Cut.Row(Cut.Top(ref r,Touch),RoomOpen && own ? 2 : 4,G);
            if (!own) {
                string[] facts = { "BED\nLv " + (h.BedLevel + 1),"DOOR\nLv " + (h.DoorLevel + 1),"GOLD\n" + (h.Wealth == WealthBand.Unknown ? "-" : h.Wealth.ToString()),"" };
                for (int i = 0; i < 3; i++) { HudTheme.Card(buttons[i]); HudTheme.Text(Cut.Inset(buttons[i],6),facts[i],HudTheme.Label,HudTheme.Muted,true,TextAnchor.MiddleCenter,true); }
                if (m.Players[0].Eliminated && HudTheme.Button(buttons[3],"NEXT",ButtonKind.Secondary,!m.Finished)) session.CycleSpectator();
                return;
            }
            var live = m.Houses[h.HouseId];
            if (HudTheme.Button(buttons[0],viewed.Sleeping ? "WAKE" : "SLEEP",ButtonKind.Secondary)) session.Interact();
            if (!RoomOpen) {
                HouseUpgrade(buttons[1],live,UpgradeKind.Bed,"BED");
                HouseUpgrade(buttons[2],live,UpgradeKind.Door,"DOOR");
            }
            bool canRepair = live.Health < live.MaxHealth && !live.IsBuilding && m.Players[0].Gold >= MatchSimulation.RepairCost;
            if (HudTheme.Button(buttons[buttons.Length - 1],MatchSimulation.RepairCost.ToString(),ButtonKind.Primary,"icon_repair",canRepair) && !session.Repair()) session.Notify("Repair needs 40 gold and a damaged door.");
        }
        private void StationCards(Rect area,int house)
        {
            var cards = Cut.Row(area,3,G); var m = session.Match;
            for (int i = 0; i < cards.Length; i++) {
                var station = (Station)i; var card = cards[i]; HudTheme.Card(card);
                var inner = Cut.Inset(card,14); var action = Cut.Bottom(ref inner,Touch,8);
                var state = HouseStations.State(m,0,station,session.Queue);
                string title = station == Station.Weapons ? "WEAPONS" : station.ToString().ToUpperInvariant() + "  Lv " + HouseStations.Level(m,house,station);
                HudTheme.Text(Cut.Top(ref inner,38),title,HudTheme.Body,HudTheme.Ink,true);
                HudTheme.Text(Cut.Top(ref inner,32),state == CardState.Queued ? "Waiting for gold / builder" : HouseStations.Effect(m,house,station),HudTheme.Label,state == CardState.Queued ? HudTheme.Gold : HudTheme.Muted);
                if (state == CardState.Building) {
                    var h = m.Houses[house];
                    HudTheme.Bar(Cut.Center(action,action.width,36),1 - h.BuildRemaining / Mathf.Max(.01f,h.BuildDuration),HudTheme.Good,"BUILDING  " + h.BuildRemaining.ToString("0.0") + "s");
                    continue;
                }
                int cost = HouseStations.Cost(m,house,station);
                string label = station == Station.Weapons ? "OPEN YARD" : state == CardState.Max ? "MAX LEVEL" : state == CardState.Queued ? "CANCEL QUEUE" : (state == CardState.Waiting || state == CardState.TooExpensive ? "QUEUE  " : "UPGRADE  ") + HudTheme.Number(cost);
                if (!HudTheme.Button(action,label,state == CardState.Queued ? ButtonKind.Secondary : ButtonKind.Primary,state != CardState.Max,false,HudTheme.Label)) continue;
                if (station == Station.Weapons) { session.Interior.Exit(); ShowBuildBoard(true); }
                else if (session.BuyStation(station)) FlyCoins(action.center);
            }
        }
        private void HouseUpgrade(Rect r,HouseState house,UpgradeKind kind,string name)
        {
            int cost = session.Match.UpgradeCost(house.Id,kind);
            if (house.IsBuilding && house.BuildingKind == kind) {
                HudTheme.Card(r); HudTheme.Bar(new Rect(r.x + 10,r.yMax - 20,r.width - 20,8),1 - house.BuildRemaining / Mathf.Max(.01f,house.BuildDuration),HudTheme.Good);
                HudTheme.Text(new Rect(r.x,r.y,r.width,r.height - 24),name + "\n" + house.BuildRemaining.ToString("0.0") + "s",HudTheme.Label,HudTheme.Ink,true,TextAnchor.MiddleCenter,true);
                return;
            }
            bool allowed = cost >= 0 && !house.IsBuilding && session.Match.Players[0].Gold >= cost;
            // Icon-led house buttons: the icon says what it is, the label says the price.
            if (HudTheme.Button(r,cost < 0 ? "MAX" : HudTheme.Number(cost),ButtonKind.Primary,kind == UpgradeKind.Bed ? "icon_bed" : "icon_door",allowed)) session.Buy(kind);
        }
        private void Sockets(Rect area,HouseScout h,bool own)
        {
            var cards = Cut.Row(area,3,G);
            for (int slot = 0; slot < 3; slot++) SocketCard(cards[slot],h,slot,own);
        }
        private void SocketCard(Rect card,HouseScout h,int slot,bool own)
        {
            var w = h.Weapons[slot]; bool selected = own && selectedSlot == slot;
            HudTheme.Card(card); if (selected) HudTheme.Ring(card);
            var inner = Cut.Inset(card,14);
            var actions = own && selected ? Cut.Bottom(ref inner,Touch,8) : Rect.zero;
            var icon = Cut.Right(ref inner,Mathf.Min(150,inner.height),8);
            if (w.Present) session.Arena.DrawWeaponIcon(Cut.Center(icon,icon.width,icon.width),w.Kind);
            var d = w.Present ? WeaponCatalog.Get(w.Kind) : null;
            HudTheme.Text(Cut.Top(ref inner,38),w.Present ? d.Name : "Empty socket",HudTheme.Body,w.Present ? HudTheme.Ink : HudTheme.Muted,true);
            string detail = !w.Present ? (own ? "Tap to build" : "Slot " + (slot + 1)) : w.Building ? "Lv " + w.Level + "  ·  building" : "Lv " + w.Level + "  ·  range " + d.Range;
            HudTheme.Text(Cut.Top(ref inner,30),detail,HudTheme.Label,HudTheme.Muted);
            if (!own) return;
            // The card body (above its action row) selects the socket.
            var body = new Rect(card.x,card.y,card.width,actions.height > 0 ? actions.y - card.y - 4 : card.height);
            if (body.height >= Touch && Hit(body,"Socket " + (slot + 1))) { selectedSlot = selectedSlot == slot && movingSlot < 0 ? -1 : slot; picking = false; }
            if (actions.height <= 0) return;
            if (!w.Present) { if (HudTheme.Button(actions,"BUILD",ButtonKind.Primary)) picking = true; return; }
            int cost = session.Match.WeaponUpgradeCost(h.HouseId,slot);
            var sell = Cut.Right(ref actions,Touch + 12,8); var move = Cut.Right(ref actions,Touch,8);
            if (HudTheme.Button(actions,cost < 0 ? "MAX" : "UPGRADE\n" + HudTheme.Number(cost),ButtonKind.Primary,cost >= 0 && !w.Building && session.Match.Players[0].Gold >= cost)) session.UpgradeWeapon(slot);
            if (HudTheme.Button(move,"MOVE",ButtonKind.Secondary,!w.Building)) movingSlot = slot;
            if (HudTheme.Button(sell,"SELL\n+" + HudTheme.Number(session.Match.SellValue(h.HouseId,slot)),ButtonKind.Secondary,!w.Building)) { session.SellWeapon(slot); movingSlot = -1; }
        }
        // Shown while a weapon waits for a pad: which weapon, what to do, and a cancel.
        private void PadPrompt(Rect area,HouseScout h)
        {
            HudTheme.Card(area); var inner = Cut.Inset(area,14);
            var cancel = Cut.Right(ref inner,200,G);
            string what = placingKind >= 0 ? WeaponCatalog.Get((WeaponKind)placingKind).Name : WeaponCatalog.Get(h.Weapons[movingSlot].Kind).Name;
            HudTheme.Text(Cut.Top(ref inner,44,6),(placingKind >= 0 ? "Place your " : "Move your ") + what,HudTheme.CardTitle,HudTheme.Ink,true);
            HudTheme.Text(inner,"Tap a glowing pad in your yard.",HudTheme.Body,HudTheme.Gold,true);
            if (HudTheme.Button(Cut.Center(cancel,cancel.width,Touch),"CANCEL",ButtonKind.Secondary,true,false,HudTheme.Body)) { placingKind = -1; movingSlot = -1; }
        }
        // Free pads become tap targets on the board while placing or moving.
        private void PadTargets(MatchLayout l)
        {
            if (placingKind < 0 && movingSlot < 0) return;
            var m = session.Match; int house = m.Players[0].HouseId; if (house < 0 || session.Arena.BoardHouse != house) return;
            var weapons = m.Houses[house].Weapons;
            for (int pad = 0; pad < YardLayout.PadCount; pad++) {
                bool taken = false; for (int s = 0; s < 3; s++) if (weapons[s] != null && weapons[s].Spot == pad && s != movingSlot) taken = true;
                if (taken) continue;
                session.Arena.HighlightPad(pad,true);
                var px = session.Arena.PadScreenRect(pad); var r = new Rect(px.x / scale,px.y / scale,px.width / scale,px.height / scale);
                r = Cut.Center(r,Mathf.Max(r.width,Touch),Mathf.Max(r.height,Touch));
                if (r.Overlaps(l.Board) || r.Overlaps(l.Strip) || r.Overlaps(l.MiniMap) || r.Overlaps(l.Left) || r.Overlaps(l.Boss) || r.Overlaps(l.Toasts)) continue;
                if (!Hit(r,"Pad " + pad)) continue;
                if (placingKind >= 0) {
                    int slot = selectedSlot >= 0 && weapons[selectedSlot] == null ? selectedSlot : System.Array.FindIndex(weapons,w => w == null);
                    if (slot >= 0 && session.PlaceWeapon(slot,(WeaponKind)placingKind,pad)) { placingKind = -1; selectedSlot = slot; } else session.Notify("Cannot build there right now.");
                } else if (session.MoveWeaponToSpot(movingSlot,pad)) movingSlot = -1;
            }
        }
        private void WeaponPicker(Rect area,HouseScout h)
        {
            HudTheme.Card(area); var inner = Cut.Inset(area,14);
            HudTheme.Text(Cut.Top(ref inner,40,8),"Choose a weapon for slot " + (selectedSlot + 1),HudTheme.Body,HudTheme.Ink,true);
            var row = Cut.Top(ref inner,Touch);
            if (HudTheme.Button(Cut.Right(ref row,160,G),"CANCEL",ButtonKind.Secondary)) picking = false;
            var buttons = Cut.Row(row,4,G);
            for (int i = 0; i < 4; i++) {
                var d = WeaponCatalog.Get((WeaponKind)i);
                bool affordable = session.Match.Players[0].Gold >= d.Cost && !session.Match.Houses[h.HouseId].IsBuilding;
                if (HudTheme.Button(buttons[i],d.Name.ToUpperInvariant() + "\n" + d.Cost + " gold",ButtonKind.Primary,affordable)) {
                    // Next step: tap a free pad in the yard.
                    placingKind = (int)d.Id; picking = false;
                }
            }
        }
    }
}
