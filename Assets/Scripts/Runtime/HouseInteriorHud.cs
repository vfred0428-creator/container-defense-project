using UnityEngine;
namespace ContainerDefense
{
    // VIEW MY ROOM while sleeping: the shared cozy interior with the resident asleep and rising income.
    public sealed partial class MatchHud
    {
        public bool RoomOpen { get; set; }
        private bool CanOpenRoom { get { var p = session.Match.Players[0]; return p.HouseId >= 0 && !p.Eliminated && p.Sleeping && !session.Scouting && !session.Match.Finished; } }
        private void HouseInterior()
        {
            if (!CanOpenRoom) { RoomOpen = false; return; }
            if (!RoomOpen) return;
            var p = session.Match.Players[0]; var texture = MenuArtwork.Get("room");
            if (texture == null) return;
            float s = Mathf.Max(width / texture.width,height / texture.height);
            Rect room = new Rect((width - texture.width * s) / 2,(height - texture.height * s) / 2,texture.width * s,texture.height * s);
            GUI.DrawTexture(room,texture,ScaleMode.StretchToFill);
            float size = room.width * .28f;
            session.Arena.Portraits.Draw(new Rect(room.x + room.width * .10f,room.y + room.height * .23f,size,size),session.Inventory.Equipped(p.Character.Id),false,3);
            float t = session.Match.Elapsed;
            for (int i = 0; i < 3; i++)
            {
                float progress = Mathf.Repeat(t * .45f + i / 3f,1);
                var coin = new Rect(room.x + room.width * (.37f + i * .035f),room.y + room.height * (.58f - progress * .19f),44,44);
                var old = GUI.color; GUI.color = new Color(1,1,1,1 - progress); HudIcons.Draw(coin,"icon_coin"); GUI.color = old;
            }
        }
    }
}
