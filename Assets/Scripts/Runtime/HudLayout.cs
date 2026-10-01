using System.Collections.Generic;
using UnityEngine;

namespace ContainerDefense
{
    // Screen regions the HUD claims this frame. The camera frames the map inside what is left,
    // and world labels skip anything that would sit under a panel.
    public static class HudLayout
    {
        // Screenshot/testing hook: simulated left/right notch inset in physical pixels.
        public static float SimulatedInset;
        // Fractions of the screen reserved by the HUD along each edge.
        public static float ReservedTop, ReservedBottom, ReservedLeft, ReservedRight;
        // Screen point (GUI pixels) of the gold counter, where flying coins land.
        public static Vector2 GoldTarget;
        private static readonly List<Rect> blocked = new List<Rect>();
        public static Rect SafeArea
        {
            get
            {
                var area = Screen.safeArea;
                if (SimulatedInset > 0) area = new Rect(Mathf.Max(area.x,SimulatedInset),area.y,Mathf.Min(area.width,Screen.width - 2 * SimulatedInset),area.height);
                return area;
            }
        }
        // Safe area in top-left (GUI) coordinates, in physical pixels.
        public static Rect SafeAreaGui { get { var a = SafeArea; return new Rect(a.x,Screen.height - a.yMax,a.width,a.height); } }
        public static void Clear() { blocked.Clear(); ReservedTop = ReservedBottom = ReservedLeft = ReservedRight = 0; }
        // Records a HUD panel given in physical GUI pixels.
        public static void Block(Rect screenRect) { blocked.Add(screenRect); }
        public static bool Blocks(Rect screenRect) { foreach (var r in blocked) if (r.Overlaps(screenRect)) return true; return false; }
        // Paints the simulated notch bands last so anything placed under them stays visible in captures.
        public static void DrawSimulatedNotch()
        {
            if (SimulatedInset <= 0 || Event.current.type != EventType.Repaint) return;
            var old = GUI.matrix; GUI.matrix = Matrix4x4.identity; var c = GUI.color; GUI.color = new Color(0,0,0,.92f);
            GUI.DrawTexture(new Rect(0,0,SimulatedInset,Screen.height),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Screen.width - SimulatedInset,0,SimulatedInset,Screen.height),Texture2D.whiteTexture);
            GUI.color = c; GUI.matrix = old;
        }
    }
}
