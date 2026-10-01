using System.Collections.Generic;
using ContainerDefense.Domain;
using UnityEngine;
namespace ContainerDefense
{
    // Inside your house: Astra's painted room with live, hand-drawn overlays. The weapons you own hang on the
    // pegboard, the door carries your number, reinforcement per level and cracks as its health drops, the bed gains
    // comforts per level, and the boss shows in the window when it is coming. The resident sleeps in bed (Zzz and
    // gold pops), walks to the station you upgrade, and guards the door when the boss is close.
    // Positions are in room-texture coordinates (u, v from the top-left of room.png, 1536 x 1024).
    public sealed partial class MatchHud
    {
        public bool RoomOpen
        {
            get { return session.Interior != null && session.Interior.Inside; }
            set { if (session.Interior == null) return; if (value) session.Interior.Enter(); else session.Interior.Exit(); }
        }
        private bool CanOpenRoom { get { return session.Interior != null && session.Interior.Available; } }

        private Rect room; private Texture roomArt;
        private MatchSimulation roomMatch;
        // Resident: feet position (u, v), current errand and when it ends.
        private Vector2 resident = new Vector2(.66f,.735f);
        private int errand = -1; private float errandUntil;
        // Level and build tracking for errands and level-up puffs.
        private int seenBed, seenDoor, seenWeapons; private bool seenBuilding; private UpgradeKind seenKind;
        private float nextPop;
        private bool passiveShown; private float passiveUntil;
        private struct Pop { public Vector2 At; public float Start; public string Text; }
        private readonly List<Pop> pops = new List<Pop>();
        private struct RoomPuff { public Vector2 At; public float Start; }
        private readonly List<RoomPuff> puffs = new List<RoomPuff>();
        private static readonly Vector2[] StationFeet = { new Vector2(.60f,.735f),new Vector2(.80f,.735f),new Vector2(.70f,.71f) };
        private static readonly Vector2[] StationMark = { new Vector2(.36f,.60f),new Vector2(.915f,.50f),new Vector2(.722f,.32f) };
        private static readonly Vector2 RestSpot = new Vector2(.66f,.735f), BedExit = new Vector2(.50f,.735f);
        private const float Walk = .2f, ErrandSeconds = 3.4f;

        private Vector2 RP(float u,float v) { return new Vector2(room.x + u * room.width,room.y + v * room.height); }
        private Vector2 RP(Vector2 uv) { return RP(uv.x,uv.y); }
        private Rect RR(float u0,float v0,float u1,float v1) { return Rect.MinMaxRect(room.x + u0 * room.width,room.y + v0 * room.height,room.x + u1 * room.width,room.y + v1 * room.height); }
        // Draws part of the room art elsewhere (a plush from the shelf on the bed, for example).
        private void Crop(Rect dst,float u0,float v0,float u1,float v1) { GUI.DrawTextureWithTexCoords(dst,roomArt,new Rect(u0,1 - v1,u1 - u0,v1 - v0)); }

        private void HouseInterior(MatchLayout l)
        {
            if (!ReferenceEquals(roomMatch,session.Match)) ResetRoom();
            TrackStations();
            if (!RoomOpen) return;
            var m = session.Match; var p = m.Players[0]; var h = m.Houses[p.HouseId];
            roomArt = MenuArtwork.Get("room"); if (roomArt == null) return;
            // Cover the screen; slide the art so the floor in front of the bed meets the top of the house panel.
            float s = Mathf.Max(width / roomArt.width,height / roomArt.height), w = roomArt.width * s, hh = roomArt.height * s;
            room = new Rect((width - w) / 2,Mathf.Clamp(l.Board.y - .77f * hh,height - hh,0),w,hh);
            GUI.DrawTexture(room,roomArt,ScaleMode.StretchToFill);
            WindowBoss();
            WeaponRack(h);
            DoorOverlay(h);
            BedComforts(h);
            Resident(p,h);
            DrawPuffs();
            if (!passiveShown) { passiveShown = true; passiveUntil = Time.unscaledTime + 4; }
        }
        private void ResetRoom()
        {
            roomMatch = session.Match; errand = -1; pops.Clear(); puffs.Clear(); flights.Clear(); nextPop = 0; passiveShown = false; passiveUntil = 0;
            resident = RestSpot; seenBed = seenDoor = seenWeapons = 0; seenBuilding = false;
        }
        // Errands when a build starts, puffs when a level lands. Polled so keyboard buys and the queue count too.
        private void TrackStations()
        {
            puffs.RemoveAll(p => Time.unscaledTime - p.Start >= .8f);
            pops.RemoveAll(p => Time.unscaledTime - p.Start >= 1.5f);
            var m = session.Match; var p = m.Players[0]; if (p.HouseId < 0 || Event.current.type != EventType.Repaint) return;
            var h = m.Houses[p.HouseId]; int weapons = 0; foreach (var w in h.Weapons) if (w != null) weapons += 10 + w.Level;
            if (h.IsBuilding && (!seenBuilding || h.BuildingKind != seenKind)) StartErrand(h.BuildingKind == UpgradeKind.Door ? Station.Door : Station.Bed);
            if (weapons > seenWeapons) { StartErrand(Station.Weapons); Puff(Station.Weapons); }
            if (h.BedLevel > seenBed) Puff(Station.Bed);
            if (h.DoorLevel > seenDoor) Puff(Station.Door);
            seenBuilding = h.IsBuilding; seenKind = h.BuildingKind; seenBed = h.BedLevel; seenDoor = h.DoorLevel; seenWeapons = weapons;
        }
        private void StartErrand(Station s) { errand = (int)s; errandUntil = Time.unscaledTime + ErrandSeconds; }
        private void Puff(Station s) { puffs.Add(new RoomPuff { At = StationMark[(int)s],Start = Time.unscaledTime }); GameAudio.Play("build"); }

        // The boss grows in the window as it gets closer, with a red glow on the glass.
        private void WindowBoss()
        {
            var v = session.Interior; if (!v.BossInWindow) return;
            var tex = MenuArtwork.Get("boss"); var glass = RR(.458f,.168f,.623f,.392f);
            float k = 1 - Mathf.Clamp01(v.BossEta / InteriorView.WindowSeconds);
            GUI.BeginGroup(glass);
            HudTheme.Fill(new Rect(0,0,glass.width,glass.height),HudTheme.Hex(0xFF3B4A,.08f + .14f * k));
            if (tex != null) {
                float size = glass.height * (.5f + .8f * k) * (1 + Mathf.Sin(Time.unscaledTime * 3) * .03f);
                var old = GUI.color; GUI.color = new Color(1,1,1,.55f + .45f * k);
                GUI.DrawTexture(new Rect(glass.width * .55f - size / 2,glass.height * .65f - size / 2,size,size),tex,ScaleMode.ScaleToFit,true);
                GUI.color = old;
            }
            GUI.EndGroup();
        }
        // A plain pegboard patch over the painted launchers, with the weapons you actually own hanging on it.
        private void WeaponRack(HouseState h)
        {
            var board = RR(.682f,.232f,.763f,.402f);
            HudTheme.Fill(board,HudTheme.Hex(0xE7832A),3);
            HudTheme.Fill(new Rect(board.x,board.yMax - board.height * .35f,board.width,board.height * .35f),HudTheme.Hex(0xC86E24,.55f));
            float step = Mathf.Max(8,board.width / 9);
            for (float y = board.y + step / 2; y < board.yMax - 2; y += step)
                for (float x = board.x + step / 2; x < board.xMax - 2; x += step) HudTheme.Fill(new Rect(x - 2,y - 2,4,4),HudTheme.Hex(0x5A2A0A,.75f),2);
            var rows = Cut.Column(Cut.Inset(board,4),3,4);
            for (int i = 0; i < 3; i++) {
                var w = h.Weapons[i]; var r = rows[i];
                // Two pegs per slot; the weapon rests on them.
                HudTheme.Fill(new Rect(r.x + r.width * .2f,r.center.y + r.height * .2f,5,8),HudTheme.Hex(0x3A2A20),2);
                HudTheme.Fill(new Rect(r.x + r.width * .75f,r.center.y + r.height * .2f,5,8),HudTheme.Hex(0x3A2A20),2);
                if (w == null) continue;
                HudTheme.Fill(new Rect(r.x + r.width * .15f,r.yMax - r.height * .18f,r.width * .7f,r.height * .12f),HudTheme.Hex(0x000000,.25f),r.height * .06f);
                session.Arena.DrawWeaponIcon(Cut.Center(r,r.height * 1.25f,r.height * 1.1f),w.Kind);
                for (int lv = 0; lv < w.Level; lv++) HudTheme.Fill(new Rect(r.xMax - 9 - lv * 9,r.y + 2,7,7),HudTheme.Gold,3.5f);
                if (w.Building) HudTheme.Fill(r,HudTheme.Hex(0xFFFFFF,.18f + Mathf.Sin(Time.unscaledTime * 8) * .08f),3);
            }
        }
        // House number plate, steel bands per door level, and cracks that grow as the door loses health.
        private void DoorOverlay(HouseState h)
        {
            var plate = RR(.871f,.448f,.939f,.546f);
            HudTheme.Fill(new Rect(plate.x + 3,plate.y + 4,plate.width,plate.height),HudTheme.Hex(0x000000,.35f),6);
            HudTheme.Fill(plate,HudTheme.Hex(0x4C5468),6); HudTheme.Fill(Cut.Inset(plate,3),HudTheme.Hex(0x6A7389),5);
            foreach (var c in new[] { new Vector2(plate.x + 7,plate.y + 7),new Vector2(plate.xMax - 7,plate.y + 7),new Vector2(plate.x + 7,plate.yMax - 7),new Vector2(plate.xMax - 7,plate.yMax - 7) })
                HudTheme.Fill(new Rect(c.x - 3,c.y - 3,6,6),HudTheme.Hex(0x2E3342),3);
            HudTheme.OutlinedText(plate,(h.Id + 1).ToString("00"),Mathf.RoundToInt(Mathf.Clamp(plate.height * .62f,18,64)),HudTheme.Hex(0xF3E7C8),TextAnchor.MiddleCenter);
            // Level 2+: steel bands; level 3+: rivets; level 4+: corner brackets; level 5: brass trim.
            int level = h.DoorLevel + 1;
            if (level >= 2) foreach (float v in new[] { .424f,.566f }) {
                var band = RR(.858f,v,.975f,v + .014f);
                HudTheme.Fill(band,HudTheme.Hex(0x7D879E)); HudTheme.Fill(new Rect(band.x,band.yMax - 2,band.width,2),HudTheme.Hex(0x2E3342,.8f));
                if (level >= 3) for (int i = 0; i < 5; i++) HudTheme.Fill(new Rect(band.x + (i + .5f) * band.width / 5 - 3,band.center.y - 3,6,6),HudTheme.Hex(0x3A4052),3);
            }
            if (level >= 4) foreach (var corner in new[] { RR(.858f,.145f,.885f,.17f),RR(.948f,.145f,.975f,.17f),RR(.858f,.655f,.885f,.68f),RR(.948f,.655f,.975f,.68f) })
                HudTheme.Fill(corner,HudTheme.Hex(0x8A93A8),3);
            if (level >= 5) { var door = RR(.856f,.14f,.977f,.685f); var gold = HudTheme.Hex(0xE8B64A,.9f);
                HudTheme.Fill(new Rect(door.x,door.y,door.width,4),gold); HudTheme.Fill(new Rect(door.x,door.yMax - 4,door.width,4),gold);
                HudTheme.Fill(new Rect(door.x,door.y,4,door.height),gold); HudTheme.Fill(new Rect(door.xMax - 4,door.y,4,door.height),gold); }
            // Cracks: a fixed pattern per house, revealed as health drops below 90%.
            float damage = h.MaxHealth > 0 ? 1 - h.Health / h.MaxHealth : 0;
            int cracks = Mathf.Clamp(Mathf.FloorToInt((damage - .1f) / .9f * 9) + (damage > .1f ? 1 : 0),0,9);
            var rng = new System.Random(h.Id * 97 + 13);
            for (int i = 0; i < 9; i++) {
                var start = new Vector2(.862f + (float)rng.NextDouble() * .105f,.18f + (float)rng.NextDouble() * .45f);
                if (start.x > .87f && start.x < .96f && start.y > .22f && start.y < .41f) start.y += .2f;   // keep the porthole glass clear
                float angle = (float)rng.NextDouble() * 360; var a = RP(start);
                for (int seg = 0; seg < 3; seg++) {
                    float len = room.height * (.025f + (float)rng.NextDouble() * .03f); angle += (float)rng.NextDouble() * 70 - 35;
                    var b = a + new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad),Mathf.Sin(angle * Mathf.Deg2Rad)) * len;
                    if (i < cracks) Line(a,b,3,HudTheme.Hex(0x15121A,.9f));
                    a = b;
                }
            }
        }
        // Bed comforts per level, all cut from the room art itself: a plush on the blanket, string lights, a second
        // plush by the pillow, and a warm lamp glow at the top level.
        private void BedComforts(HouseState h)
        {
            int level = h.BedLevel + 1;
            if (level >= 3) for (int i = 0; i < 7; i++) {
                var c = RP(.14f + i * .03f,.425f + Mathf.Sin(i * 1.3f) * .006f); float glow = .6f + Mathf.Sin(Time.unscaledTime * 2 + i) * .2f;
                HudTheme.Fill(new Rect(c.x - 9,c.y - 9,18,18),HudTheme.Hex(0xFFC860,.25f * glow),9); HudTheme.Fill(new Rect(c.x - 4,c.y - 4,8,8),HudTheme.Hex(0xFFE7A0,glow),4);
            }
            if (level >= 2) Plush(RR(.505f,.505f,.556f,.615f),.358f,.29f,.408f,.43f);
            if (level >= 4) Plush(RR(.15f,.455f,.19f,.54f),.733f,.41f,.775f,.515f);
            if (level >= 5) { var lamp = RP(.07f,.50f); HudTheme.Fill(new Rect(lamp.x - 60,lamp.y - 60,120,120),HudTheme.Hex(0xFFD080,.18f + Mathf.Sin(Time.unscaledTime) * .04f),60); }
        }
        private void Plush(Rect dst,float u0,float v0,float u1,float v1)
        {
            HudTheme.Fill(new Rect(dst.x + dst.width * .1f,dst.yMax - dst.height * .14f,dst.width * .8f,dst.height * .16f),HudTheme.Hex(0x3A1E10,.3f),dst.height * .08f);
            Crop(dst,u0,v0,u1,v1);
        }
        // The resident: in bed while sleeping, otherwise standing; walks to a station on an errand, guards the door
        // when the boss is about to arrive. Atlas frames: 0 idle, 1-2 walk, 3 sleep, 4 shoot, 7 cheer.
        private void Resident(PlayerState p,HouseState h)
        {
            string skin = session.Inventory.Equipped(p.Character.Id); float now = Time.unscaledTime;
            bool siege = session.Interior.BossIncoming;
            if (errand >= 0 && now >= errandUntil) errand = -1;
            bool inBed = p.Sleeping && errand < 0 && !siege;
            if (inBed && Vector2.Distance(resident,BedExit) < .02f || inBed && resident == RestSpot) {
                resident = BedExit;
                float size = room.width * .28f; var bed = new Rect(room.x + room.width * .10f,room.y + room.height * .23f,size,size);
                session.Arena.Portraits.Draw(bed,skin,false,3);
                for (int i = 0; i < 3; i++) {
                    float t = Mathf.Repeat(now * .5f + i / 3f,1); var z = RP(.30f + t * .05f,.40f - t * .1f);
                    var old = GUI.color; GUI.color = new Color(1,1,1,1 - t);
                    HudTheme.OutlinedText(new Rect(z.x - 20,z.y - 20,40,40),"z",Mathf.RoundToInt(22 + 14 * t),HudTheme.Hex(0x9FC4FF),TextAnchor.MiddleCenter);
                    GUI.color = old;
                }
                GoldPops(h);
                return;
            }
            var target = siege ? StationFeet[(int)Station.Door] : errand >= 0 ? StationFeet[errand] : p.Sleeping ? BedExit : RestSpot;
            var delta = target - resident; bool moving = delta.magnitude > .004f;
            if (Event.current.type == EventType.Repaint && moving) resident += delta.normalized * Mathf.Min(delta.magnitude,Walk * Time.unscaledDeltaTime);
            int frame = moving ? (Mathf.FloorToInt(now * 7) % 2 == 0 ? 1 : 2) : siege ? 4 : errand >= 0 ? 7 : 0;
            // Walk frames face left and the shot points left; flip them to face right (the door is on the right).
            bool flip = moving ? delta.x > 0 : siege;
            float tall = room.height * .27f; var feet = RP(resident);
            HudTheme.Fill(new Rect(feet.x - tall * .22f,feet.y - tall * .05f,tall * .44f,tall * .1f),HudTheme.Hex(0x2A1408,.35f),tall * .05f);
            var body = new Rect(feet.x - tall / 2,feet.y - tall * .97f,tall,tall);
            var matrix = GUI.matrix;
            if (flip) {
                var pivot = new Vector3(body.center.x,body.center.y,0);
                GUI.matrix = matrix * Matrix4x4.Translate(pivot) * Matrix4x4.Scale(new Vector3(-1,1,1)) * Matrix4x4.Translate(-pivot);
            }
            session.Arena.Portraits.Draw(body,skin,false,frame);
            GUI.matrix = matrix;
            if (p.Sleeping) GoldPops(h);
        }
        // While asleep: a coin with the amount rises from the bed every second.
        private void GoldPops(HouseState h)
        {
            var m = session.Match; float now = Time.unscaledTime;
            if (now >= nextPop) { nextPop = now + 1; if (!m.Finished) pops.Add(new Pop { At = new Vector2(.40f,.55f),Start = now,Text = "+" + m.Income(h.Id).ToString("0.#") }); }
            for (int i = pops.Count - 1; i >= 0; i--) {
                float t = (now - pops[i].Start) / 1.5f; if (t >= 1) { pops.RemoveAt(i); continue; }
                var c = RP(pops[i].At.x + Mathf.Sin(pops[i].Start * 3) * .02f,pops[i].At.y - t * .14f);
                var old = GUI.color; GUI.color = new Color(1,1,1,t < .75f ? 1 : (1 - t) * 4);
                HudIcons.Draw(new Rect(c.x - 22,c.y - 22,44,44),"icon_coin");
                HudTheme.OutlinedText(new Rect(c.x + 24,c.y - 20,120,40),pops[i].Text,HudTheme.Label,HudTheme.Gold,TextAnchor.MiddleLeft);
                GUI.color = old;
            }
        }
        // Build puff: a ring of soft clouds that swell and fade at the station that just levelled up.
        private void DrawPuffs()
        {
            float now = Time.unscaledTime;
            for (int i = puffs.Count - 1; i >= 0; i--) {
                float t = (now - puffs[i].Start) / .8f; if (t >= 1) { puffs.RemoveAt(i); continue; }
                var c = RP(puffs[i].At); float r = room.height * (.03f + .06f * t);
                for (int k = 0; k < 7; k++) {
                    float a = k / 7f * Mathf.PI * 2; var at = c + new Vector2(Mathf.Cos(a),Mathf.Sin(a) * .7f) * r;
                    float size = room.height * .035f * (1 - t * .4f);
                    HudTheme.Fill(new Rect(at.x - size / 2,at.y - size / 2,size,size),new Color(1,.97f,.9f,.85f * (1 - t)),size / 2);
                }
                if (t < .5f) HudTheme.OutlinedText(new Rect(c.x - 80,c.y - r - 50,160,40),"LEVEL UP!",HudTheme.Label,HudTheme.Gold,TextAnchor.MiddleCenter);
            }
        }
        private void Line(Vector2 a,Vector2 b,float thickness,Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var d = b - a; float angle = Mathf.Atan2(d.y,d.x) * Mathf.Rad2Deg; var matrix = GUI.matrix;
            var pivot = new Vector3(a.x,a.y,0);
            GUI.matrix = matrix * Matrix4x4.Translate(pivot) * Matrix4x4.Rotate(Quaternion.Euler(0,0,angle)) * Matrix4x4.Translate(-pivot);
            HudTheme.Fill(new Rect(a.x,a.y - thickness / 2,d.magnitude,thickness),color);
            GUI.matrix = matrix;
        }
        // On your board, the house itself is the way in.
        private void RoomEntry(MatchLayout l)
        {
            if (!CanOpenRoom || RoomOpen || session.Paused || session.Interior.ForcedOut || placingKind >= 0 || movingSlot >= 0 || session.Arena.BoardHouse != session.Match.Players[0].HouseId) return;
            var px = session.Arena.BoardHouseScreenRect(); var r = new Rect(px.x / scale,px.y / scale,px.width / scale,px.height / scale);
            r = Rect.MinMaxRect(Mathf.Max(r.xMin,safe.xMin),Mathf.Max(r.yMin,l.Strip.yMax + G),Mathf.Min(r.xMax,safe.xMax),Mathf.Min(r.yMax,l.Board.y - G));
            if (r.width < Touch || r.height < Touch || r.Overlaps(l.Left) || r.Overlaps(l.MiniMap) || r.Overlaps(l.Boss)) return;
            if (Hit(r,"Enter house")) { session.Interior.Enter(); ShowBuildBoard(false); }
        }
        // Coins that fly from the gold pill to the card that was just bought.
        private struct Flight { public Vector2 To; public float Start; }
        private readonly List<Flight> flights = new List<Flight>();
        private void FlyCoins(Vector2 to) { for (int i = 0; i < 3; i++) flights.Add(new Flight { To = to,Start = Time.unscaledTime + i * .07f }); }
        private void CoinFlights()
        {
            var from = HudLayout.GoldTarget / scale; float now = Time.unscaledTime;
            for (int i = flights.Count - 1; i >= 0; i--) {
                float t = (now - flights[i].Start) / .45f; if (t < 0) continue; if (t >= 1) { flights.RemoveAt(i); continue; }
                var mid = (from + flights[i].To) / 2 + Vector2.up * -120;
                var at = Vector2.Lerp(Vector2.Lerp(from,mid,t),Vector2.Lerp(mid,flights[i].To,t),t); float size = 46 - 14 * t;
                HudIcons.Draw(new Rect(at.x - size / 2,at.y - size / 2,size,size),"icon_coin");
            }
        }
        // First time inside each match: the resident's passive, top left, for four seconds.
        private static readonly string[] PassiveTitles = { "Gold Rush","Tough Door","Sharp Shots","Faster Income","Quick Feet","Lucky Discount","Fast Builder" };
        private void PassiveToast(MatchLayout l)
        {
            if (!RoomOpen || Time.unscaledTime >= passiveUntil) return;
            var p = session.Match.Players[0]; var def = session.Characters.Get(p.Character.Id);
            float alpha = Mathf.Clamp01((passiveUntil - Time.unscaledTime) / .4f);
            var r = new Rect(l.Left.xMax + G,l.Left.y,Mathf.Min(460,l.MiniMap.x - l.Left.xMax - G * 2),150);
            if (r.width < 300) r = new Rect(l.Left.x,l.Left.yMax + G,460,150);
            if (r.yMax > l.Board.y - G) return;
            var old = GUI.color; GUI.color = new Color(1,1,1,alpha);
            HudTheme.Panel(r); var inner = Cut.Inset(r,14);
            var face = Cut.Left(ref inner,inner.height,14); HudTheme.Fill(face,HudTheme.Hex(0x2C5E46),face.height / 2);
            Portrait(Cut.Inset(face,4),session.Inventory.Equipped(p.Character.Id),true);
            HudTheme.Text(Cut.Top(ref inner,40),def.Name,HudTheme.CardTitle,HudTheme.Ink,true);
            HudTheme.Text(Cut.Top(ref inner,30),PassiveTitles[(int)def.Passive],HudTheme.Body,HudTheme.Gold,true);
            HudTheme.Text(inner,def.Description,HudTheme.Label,HudTheme.Muted,false,TextAnchor.UpperLeft,true);
            GUI.color = old;
        }
    }
}
