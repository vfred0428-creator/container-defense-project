using UnityEngine;
namespace ContainerDefense
{
    public sealed partial class MatchHud
    {
        public bool RoomOpen { get; set; }
        private void HouseInterior()
        {
            var p = session.Match.Players[0];
            if (p.HouseId < 0 || p.Eliminated || !p.Sleeping) { RoomOpen = false; return; }
            if (RoomOpen)
            {
                var texture = MenuArtwork.Get("room");
                float scale = Mathf.Max(width / texture.width,height / texture.height);
                Rect room = new Rect((width - texture.width * scale) / 2,(height - texture.height * scale) / 2,texture.width * scale,texture.height * scale);
                GUI.DrawTexture(room,texture,ScaleMode.StretchToFill);
                float size = room.width * .28f;
                session.Arena.Portraits.Draw(new Rect(room.x + room.width * .10f,room.y + room.height * .23f,size,size),session.Inventory.Equipped(p.Character.Id),false,3);
                float t = session.Match.Elapsed;
                for (int i = 0; i < 3; i++)
                {
                    float progress = Mathf.Repeat(t * .45f + i / 3f,1);
                    float x = room.x + room.width * (.37f + i * .035f), y = room.y + room.height * (.58f - progress * .19f);
                    Color old = GUI.color; GUI.color = new Color(1,1,1,1 - progress);
                    Box(new Rect(x,y,25,25),gold); Label(new Rect(x + 6,y + 1,20,23),"+",body,panel); GUI.color = old;
                }
                Box(new Rect(24,196,292,92),panel);
                Label(new Rect(40,209,260,28),p.Character.Id + " / Resting",heading,cream);
                Label(new Rect(40,247,260,28),"+" + session.Match.Income(p.HouseId).ToString("0.##") + " personal gold / sec",body,gold);
            }
        }
        private void RoomButton()
        {
            var p = session.Match.Players[0];
            if (p.Sleeping && !p.Eliminated && Button(new Rect(24,140,220,42),RoomOpen ? "< VIEW YARD" : "VIEW MY ROOM >",muted)) RoomOpen = !RoomOpen;
        }
    }
}
