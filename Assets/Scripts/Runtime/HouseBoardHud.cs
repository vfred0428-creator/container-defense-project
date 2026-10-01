using ContainerDefense.Domain;
using UnityEngine;
namespace ContainerDefense
{
    // Bottom house panel: at most 30% of the screen. Your house (or the one you are viewing) with its
    // three rooftop sockets as large cards that never scroll. Viewing other houses is read-only.
    public sealed partial class MatchHud
    {
        private int selectedSlot = -1, movingSlot = -1;
        private bool picking;
        public void ShowBuildBoard(bool visible) { selectedSlot = visible ? 0 : -1; movingSlot = -1; picking = false; }
        private void HouseBoard(Rect panel)
        {
            var m = session.Match; var viewed = m.Players[session.ViewedPlayer];
            HudTheme.Panel(panel); var inner = Cut.Inset(panel,20);
            if (viewed.HouseId < 0) { ClaimBoard(inner,viewed); return; }
            var h = session.ScoutView[viewed.HouseId];
            bool own = session.View.CanCommand && !session.Paused;
            var left = Cut.Left(ref inner,432,24);
            HouseSummary(left,h,viewed,own);
            if (own && picking && selectedSlot >= 0) WeaponPicker(inner,h); else Sockets(inner,h,own);
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
            var buttons = Cut.Row(Cut.Top(ref r,Touch),4,G);
            if (!own) {
                string[] facts = { "BED\nLv " + (h.BedLevel + 1),"DOOR\nLv " + (h.DoorLevel + 1),"GOLD\n" + (h.Wealth == WealthBand.Unknown ? "-" : h.Wealth.ToString()),"" };
                for (int i = 0; i < 3; i++) { HudTheme.Card(buttons[i]); HudTheme.Text(Cut.Inset(buttons[i],6),facts[i],HudTheme.Label,HudTheme.Muted,true,TextAnchor.MiddleCenter,true); }
                if (m.Players[0].Eliminated && HudTheme.Button(buttons[3],"NEXT",ButtonKind.Secondary,!m.Finished)) session.CycleSpectator();
                return;
            }
            var live = m.Houses[h.HouseId];
            if (HudTheme.Button(buttons[0],viewed.Sleeping ? "WAKE" : "SLEEP",ButtonKind.Secondary)) session.Interact();
            HouseUpgrade(buttons[1],live,UpgradeKind.Bed,"BED");
            HouseUpgrade(buttons[2],live,UpgradeKind.Door,"DOOR");
            bool canRepair = live.Health < live.MaxHealth && !live.IsBuilding && m.Players[0].Gold >= MatchSimulation.RepairCost;
            if (HudTheme.Button(buttons[3],MatchSimulation.RepairCost.ToString(),ButtonKind.Primary,"icon_repair",canRepair) && !session.Repair()) session.Notify("Repair needs 40 gold and a damaged door.");
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
            var actions = own && (selected || (movingSlot >= 0 && !w.Present)) ? Cut.Bottom(ref inner,Touch,8) : Rect.zero;
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
            if (movingSlot >= 0) {
                if (movingSlot == slot) { if (HudTheme.Button(actions,"CANCEL MOVE",ButtonKind.Secondary)) movingSlot = -1; }
                else if (!w.Present && HudTheme.Button(actions,"MOVE HERE",ButtonKind.Primary) && session.MoveWeapon(movingSlot,slot)) { selectedSlot = slot; movingSlot = -1; }
                return;
            }
            if (!w.Present) { if (HudTheme.Button(actions,"BUILD",ButtonKind.Primary)) picking = true; return; }
            int cost = session.Match.WeaponUpgradeCost(h.HouseId,slot);
            var sell = Cut.Right(ref actions,Touch + 12,8); var move = Cut.Right(ref actions,Touch,8);
            if (HudTheme.Button(actions,cost < 0 ? "MAX" : "UPGRADE\n" + HudTheme.Number(cost),ButtonKind.Primary,cost >= 0 && !w.Building && session.Match.Players[0].Gold >= cost)) session.UpgradeWeapon(slot);
            if (HudTheme.Button(move,"MOVE",ButtonKind.Secondary,!w.Building)) movingSlot = slot;
            if (HudTheme.Button(sell,"SELL\n+" + HudTheme.Number(session.Match.SellValue(h.HouseId,slot)),ButtonKind.Secondary,!w.Building)) { session.SellWeapon(slot); movingSlot = -1; }
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
                    if (session.PlaceWeapon(selectedSlot,d.Id)) picking = false; else session.Notify("Cannot build there right now.");
                }
            }
        }
    }
}
