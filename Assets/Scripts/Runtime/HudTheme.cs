using System.Collections.Generic;
using UnityEngine;

namespace ContainerDefense
{
    public enum ButtonKind { Primary, Play, Secondary, Danger }

    // One hand-built visual language for every screen: 1920x1080 reference, opaque framed cards with an inner bevel,
    // one button system (pressed and disabled states, optional icon), Lilita One for headings and labels,
    // Nunito for body text, a single gold selection ring. Everything is drawn in code; textures are generated once.
    public static class HudTheme
    {
        public const float ReferenceWidth = 1920, ReferenceHeight = 1080;
        public const int Title = 48, CardTitle = 32, Body = 24, Label = 20;
        public const float Touch = 96, Gap = 16, Margin = 24, Radius = 20;
        public static readonly Color PanelFill = Hex(0x1A2036), PanelBorder = Hex(0x7A88BC), PanelBevel = Hex(0x111627), CardFill = Hex(0x252D49), CardBorder = Hex(0x55628F);
        public static readonly Color Ink = Color.white, Muted = Hex(0xAEB8D6), Gold = Hex(0xFFD04A), Good = Hex(0x39C46A), Bad = Hex(0xFF5A64), Info = Hex(0x6FA8FF);
        public static readonly Color PrimaryFill = Hex(0x3BB86A), PlayFill = Hex(0xF59E1B), SecondaryFill = Hex(0x46538A), DangerFill = Hex(0xD9434F), DisabledFill = Hex(0x343A52), DisabledText = Hex(0x8990A8);
        public static readonly Color Outline = Hex(0x0B1020,.9f), BarBack = Hex(0x0E1322,.95f);

        private static Texture2D panel, card, shadow, button, pressedButton, ring, pill;
        private static GUIStyle panelStyle, cardStyle, shadowStyle, buttonStyle, pressedStyle, ringStyle, pillStyle;
        private static readonly Dictionary<long,GUIStyle> textStyles = new Dictionary<long,GUIStyle>();
        private static Font regular, bold, display;

        public static Color Hex(int rgb,float alpha = 1) { return new Color((rgb >> 16 & 255) / 255f,(rgb >> 8 & 255) / 255f,(rgb & 255) / 255f,alpha); }

        // Canvas-scaler equivalent: Scale With Screen Size, 1920x1080, match 0.5 (geometric mean).
        public static float Scale { get { return Mathf.Sqrt(Screen.width / ReferenceWidth * (Screen.height / ReferenceHeight)); } }

        private static void Ensure()
        {
            if (panel != null) return;
            regular = Resources.Load<Font>("Fonts/Nunito"); bold = Resources.Load<Font>("Fonts/NunitoBold") ?? regular; display = Resources.Load<Font>("Fonts/LilitaOne") ?? bold;
            panel = Framed(96,18,PanelFill,PanelBorder,PanelBevel); card = Framed(96,14,CardFill,CardBorder,PanelBevel);
            shadow = Shadow(96,28); button = Face(96,16,false); pressedButton = Face(96,16,true); ring = Rounded(96,22,Color.clear,Gold,4,false);
            pill = Framed(96,32,PanelFill,PanelBorder,PanelBevel);
            panelStyle = Nine(panel,24); cardStyle = Nine(card,20); shadowStyle = Nine(shadow,34); buttonStyle = Nine(button,22); pressedStyle = Nine(pressedButton,22); ringStyle = Nine(ring,26); pillStyle = Nine(pill,34);
        }
        private static GUIStyle Nine(Texture2D texture,int border) { var s = new GUIStyle(); s.normal.background = texture; s.border = new RectOffset(border,border,border,border); return s; }
        private static Texture2D Rounded(int size,float radius,Color fill,Color border,float borderWidth,bool bevel)
        {
            var t = new Texture2D(size,size,TextureFormat.RGBA32,false) { filterMode = FilterMode.Bilinear,wrapMode = TextureWrapMode.Clamp,name = "HUD rounded" };
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
                float d = RoundedDistance(x + .5f,y + .5f,size,radius);
                float inside = Mathf.Clamp01(.5f - d);
                Color c = fill;
                if (borderWidth > 0) c = Color.Lerp(fill,border,Mathf.Clamp01(d + borderWidth + .5f));
                if (bevel) {
                    // Texture row 0 is the bottom: darker lower lip, lighter upper half.
                    float lip = Mathf.Clamp01((7 - y) / 3f), body = y > size * .5f ? 1 : .88f;
                    float shade = Mathf.Lerp(body,.62f,lip);
                    c = new Color(shade,shade,shade,1);
                }
                c.a *= inside; t.SetPixel(x,y,c);
            }
            t.Apply(); return t;
        }
        // Opaque card: solid fill, 2px light frame, and a darker bevel band just inside the frame.
        private static Texture2D Framed(int size,float radius,Color fill,Color border,Color bevel)
        {
            var t = new Texture2D(size,size,TextureFormat.RGBA32,false) { filterMode = FilterMode.Bilinear,wrapMode = TextureWrapMode.Clamp,name = "HUD framed" };
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
                float d = RoundedDistance(x + .5f,y + .5f,size,radius);
                Color c = fill;
                float inner = -d - 2;   // distance inside the frame
                if (inner < 6) c = Color.Lerp(bevel,fill,Mathf.Clamp01(inner / 6f));
                if (d > -2.5f) c = border;
                c.a = Mathf.Clamp01(.5f - d); t.SetPixel(x,y,c);
            }
            t.Apply(); return t;
        }
        // Button face: white so it tints, with a dark rim, a lower lip (raised) or none (pressed).
        private static Texture2D Face(int size,float radius,bool pressed)
        {
            var t = new Texture2D(size,size,TextureFormat.RGBA32,false) { filterMode = FilterMode.Bilinear,wrapMode = TextureWrapMode.Clamp,name = pressed ? "Button pressed" : "Button raised" };
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
                float d = RoundedDistance(x + .5f,y + .5f,size,radius);
                float shade = pressed ? .86f : y > size * .52f ? 1 : .9f;
                if (!pressed && y < 7) shade = .62f;                 // lower lip
                if (d > -2.5f) shade = .38f;                          // dark rim
                t.SetPixel(x,y,new Color(shade,shade,shade,Mathf.Clamp01(.5f - d)));
            }
            t.Apply(); return t;
        }
        private static Texture2D Shadow(int size,float radius)
        {
            var t = new Texture2D(size,size,TextureFormat.RGBA32,false) { filterMode = FilterMode.Bilinear,wrapMode = TextureWrapMode.Clamp,name = "HUD shadow" };
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
                float d = RoundedDistance(x + .5f,y + .5f,size,radius) + 12;
                t.SetPixel(x,y,new Color(0,0,0,.38f * Mathf.Clamp01(-d / 12f)));
            }
            t.Apply(); return t;
        }
        // Signed distance to a rounded square of the given size (negative inside).
        private static float RoundedDistance(float x,float y,int size,float r)
        {
            float qx = Mathf.Abs(x - size / 2f) - (size / 2f - r), qy = Mathf.Abs(y - size / 2f) - (size / 2f - r);
            return new Vector2(Mathf.Max(qx,0),Mathf.Max(qy,0)).magnitude + Mathf.Min(Mathf.Max(qx,qy),0) - r;
        }

        public static GUIStyle TextStyle(int size,bool strong,TextAnchor align = TextAnchor.MiddleLeft,bool wrap = true)
        {
            Ensure();
            long key = size * 1000 + (strong ? 100 : 0) + (int)align * 2 + (wrap ? 1 : 0);
            GUIStyle s;
            if (!textStyles.TryGetValue(key,out s)) {
                s = new GUIStyle { font = strong ? display : regular,fontSize = size,alignment = align,wordWrap = wrap,clipping = TextClipping.Clip };
                s.normal.textColor = Color.white; textStyles[key] = s;
            }
            return s;
        }
        public static void Panel(Rect r,bool shadowed = true)
        {
            Ensure(); if (Event.current.type != EventType.Repaint) return;
            if (shadowed) shadowStyle.Draw(new Rect(r.x - 10,r.y - 4,r.width + 20,r.height + 20),false,false,false,false);
            panelStyle.Draw(r,false,false,false,false);
        }
        public static void Card(Rect r) { Ensure(); if (Event.current.type == EventType.Repaint) cardStyle.Draw(r,false,false,false,false); }
        public static void Pill(Rect r) { Ensure(); if (Event.current.type == EventType.Repaint) pillStyle.Draw(r,false,false,false,false); }
        public static void Ring(Rect r) { Ensure(); if (Event.current.type == EventType.Repaint) ringStyle.Draw(new Rect(r.x - 6,r.y - 6,r.width + 12,r.height + 12),false,false,false,false); }
        public static void Fill(Rect r,Color color,float radius = 0)
        {
            if (Event.current.type != EventType.Repaint || r.width <= 0 || r.height <= 0) return;
            GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,Mathf.Min(radius,Mathf.Min(r.width,r.height) / 2));
        }
        public static Color FillFor(ButtonKind kind)
        { return kind == ButtonKind.Primary ? PrimaryFill : kind == ButtonKind.Play ? PlayFill : kind == ButtonKind.Danger ? DangerFill : SecondaryFill; }
        // The one button style: raised face with a lip, pressed (sinks, no lip), disabled (grey, flat text),
        // optional icon on the left. Returns true when clicked.
        public static bool Button(Rect r,string text,ButtonKind kind,bool enabled = true,bool selected = false,int size = Label)
        { return Button(r,text,kind,null,enabled,selected,size); }
        public static bool Button(Rect r,string text,ButtonKind kind,string icon,bool enabled = true,bool selected = false,int size = Label)
        {
            Ensure(); HudAudit.Interactive(r,text);
            // Always issue the control so IMGUI ids stay stable when a button toggles enabled.
            bool clicked = GUI.Button(r,GUIContent.none,GUIStyle.none) && enabled;
            if (Event.current.type == EventType.Repaint) {
                bool over = enabled && r.Contains(Event.current.mousePosition), pressed = over && Input.GetMouseButton(0);
                Color fill = enabled ? FillFor(kind) : DisabledFill; if (over && !pressed) fill = Color.Lerp(fill,Color.white,.1f);
                var face = pressed ? new Rect(r.x,r.y + 3,r.width,r.height - 3) : r;
                var old = GUI.color; GUI.color = fill; (pressed ? pressedStyle : buttonStyle).Draw(face,false,false,false,false); GUI.color = old;
                if (selected) Ring(r);
                var content = new Rect(face.x + 12,face.y + 4,face.width - 24,face.height - (pressed ? 8 : 12));
                if (!string.IsNullOrEmpty(icon)) {
                    float s = Mathf.Min(content.height * .62f,44);
                    bool iconOnly = string.IsNullOrEmpty(text), stacked = !iconOnly && face.width < face.height * 1.6f;
                    if (stacked) {
                        s = Mathf.Min(content.height * .5f,44);
                        var top = new Rect(content.center.x - s / 2,content.y + 2,s,s);
                        var fade = GUI.color; if (!enabled) GUI.color = new Color(1,1,1,.45f); HudIcons.Draw(top,icon); GUI.color = fade;
                        OutlinedText(new Rect(content.x - 6,top.yMax,content.width + 12,content.yMax - top.yMax + 2),text,size,enabled ? Ink : DisabledText,TextAnchor.MiddleCenter);
                        return clicked;
                    }
                    var ir = iconOnly ? new Rect(content.center.x - s / 2,content.center.y - s / 2,s,s) : new Rect(content.x,content.center.y - s / 2,s,s);
                    var c = GUI.color; if (!enabled) GUI.color = new Color(1,1,1,.45f); HudIcons.Draw(ir,icon); GUI.color = c;
                    if (!iconOnly) content = new Rect(content.x + s + 6,content.y,content.width - s - 6,content.height);
                }
                if (!string.IsNullOrEmpty(text)) OutlinedText(content,text,size,enabled ? Ink : DisabledText,TextAnchor.MiddleCenter);
            }
            return clicked;
        }
        // Game title in the display font: a cream line over a gold line, each with a thick dark outline and a drop shadow.
        public static void Logo(Rect r)
        {
            if (Event.current.type != EventType.Repaint) return;
            float line = r.height / 2; int size = Mathf.RoundToInt(Mathf.Min(line * 1.05f,r.width / 6.2f));
            string[] words = { "CONTAINER","DEFENSE" }; Color[] colours = { Hex(0xFFF1D6),Hex(0xFFC23A) };
            for (int i = 0; i < 2; i++) {
                var lr = new Rect(r.x + i * size * .5f,r.y + i * line,r.width,line); var style = TextStyle(size,true,TextAnchor.MiddleLeft,false); var old = GUI.color;
                GUI.color = new Color(0,0,0,.55f); GUI.Label(new Rect(lr.x + 4,lr.y + 6,lr.width,lr.height),words[i],style);
                GUI.color = Hex(0x2A1A10);
                for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4; GUI.Label(new Rect(lr.x + Mathf.Cos(a) * 3.5f,lr.y + Mathf.Sin(a) * 3.5f,lr.width,lr.height),words[i],style); }
                GUI.color = colours[i]; GUI.Label(lr,words[i],style); GUI.color = old;
            }
        }
        public static void OutlinedText(Rect r,string text,int size,Color color,TextAnchor align)
        {
            var style = TextStyle(size,true,align); var old = GUI.color;
            if (color != DisabledText) {
                GUI.color = Outline;
                for (int i = 0; i < 4; i++) GUI.Label(new Rect(r.x + (i < 2 ? -1.5f : 1.5f),r.y + (i % 2 == 0 ? -1.5f : 1.5f),r.width,r.height),text,style);
            }
            GUI.color = color; GUI.Label(r,text,style); GUI.color = old;
        }
        // Draws text at the requested size, stepping down to the 22 px floor if it would not fit.
        public static void Text(Rect r,string text,int size,Color color,bool strong = false,TextAnchor align = TextAnchor.MiddleLeft,bool wrap = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            int chosen = size; var content = new GUIContent(text);
            while (chosen > Label && !Fits(content,TextStyle(chosen,strong,align,wrap),r)) chosen -= 2;
            var style = TextStyle(Mathf.Max(Label,chosen),strong,align,wrap);
            if (!Fits(content,style,r)) HudAudit.Clipped(r,text);
            var old = GUI.color; GUI.color = color; GUI.Label(r,content,style); GUI.color = old;
        }
        private static bool Fits(GUIContent content,GUIStyle style,Rect r)
        {
            if (style.wordWrap) return style.CalcHeight(content,r.width) <= r.height + 1;
            var size = style.CalcSize(content); return size.x <= r.width + 1 && size.y <= r.height + 4;
        }
        public static void Bar(Rect r,float ratio,Color color,string caption = null)
        {
            Fill(r,BarBack,r.height / 2);
            if (ratio > 0) Fill(new Rect(r.x,r.y,Mathf.Max(r.height,r.width * Mathf.Clamp01(ratio)),r.height),color,r.height / 2);
            if (!string.IsNullOrEmpty(caption) && Event.current.type == EventType.Repaint) OutlinedText(r,caption,Label,Ink,TextAnchor.MiddleCenter);
        }
        public static string Clock(float seconds) { int s = Mathf.Max(0,Mathf.FloorToInt(seconds)); return (s / 60).ToString("00") + ":" + (s % 60).ToString("00"); }
        public static string Number(double value) { return System.Math.Floor(value).ToString("N0",System.Globalization.CultureInfo.InvariantCulture); }
    }

    // Rect cutting: every region is carved from its parent, so siblings can never overlap.
    public static class Cut
    {
        public static Rect Top(ref Rect r,float h,float gap = 0) { var a = new Rect(r.x,r.y,r.width,Mathf.Min(h,r.height)); r.yMin = Mathf.Min(r.yMax,r.yMin + h + gap); return a; }
        public static Rect Bottom(ref Rect r,float h,float gap = 0) { var a = new Rect(r.x,r.yMax - Mathf.Min(h,r.height),r.width,Mathf.Min(h,r.height)); r.yMax = Mathf.Max(r.yMin,r.yMax - h - gap); return a; }
        public static Rect Left(ref Rect r,float w,float gap = 0) { var a = new Rect(r.x,r.y,Mathf.Min(w,r.width),r.height); r.xMin = Mathf.Min(r.xMax,r.xMin + w + gap); return a; }
        public static Rect Right(ref Rect r,float w,float gap = 0) { var a = new Rect(r.xMax - Mathf.Min(w,r.width),r.y,Mathf.Min(w,r.width),r.height); r.xMax = Mathf.Max(r.xMin,r.xMax - w - gap); return a; }
        public static Rect Inset(Rect r,float pad) { return new Rect(r.x + pad,r.y + pad,Mathf.Max(0,r.width - pad * 2),Mathf.Max(0,r.height - pad * 2)); }
        public static Rect Center(Rect r,float w,float h) { return new Rect(r.x + (r.width - w) / 2,r.y + (r.height - h) / 2,w,h); }
        public static Rect[] Row(Rect r,int count,float gap)
        {
            var result = new Rect[count]; float w = (r.width - gap * (count - 1)) / count;
            for (int i = 0; i < count; i++) result[i] = new Rect(r.x + i * (w + gap),r.y,w,r.height);
            return result;
        }
        public static Rect[] Column(Rect r,int count,float gap)
        {
            var result = new Rect[count]; float h = (r.height - gap * (count - 1)) / count;
            for (int i = 0; i < count; i++) result[i] = new Rect(r.x,r.y + i * (h + gap),r.width,h);
            return result;
        }
    }

    // Development check: every interactive rect drawn this frame must stay inside the safe area and
    // must not overlap another. Violations are logged once per unique message.
    public static class HudAudit
    {
        public static bool Enabled = Debug.isDebugBuild;
        public static Rect Safe;
        private static readonly List<KeyValuePair<Rect,string>> frame = new List<KeyValuePair<Rect,string>>();
        private static readonly HashSet<string> reported = new HashSet<string>();
        public static int Violations { get { return reported.Count; } }
        public static void Begin(Rect safe) { Safe = safe; frame.Clear(); }
        public static void Interactive(Rect r,string label)
        {
            if (!Enabled || Event.current.type != EventType.Repaint) return;
            if (r.xMin < Safe.xMin - .5f || r.yMin < Safe.yMin - .5f || r.xMax > Safe.xMax + .5f || r.yMax > Safe.yMax + .5f) Report("Outside safe area: " + label);
            foreach (var other in frame) if (other.Key.Overlaps(r)) Report("Overlap: " + label + " / " + other.Value);
            frame.Add(new KeyValuePair<Rect,string>(r,label));
        }
        public static void Clipped(Rect r,string text) { if (Enabled && Event.current.type == EventType.Repaint) Report("Text does not fit at 22 px: " + text); }
        private static void Report(string message) { if (reported.Add(message)) Debug.LogWarning("[HUD audit] " + message); }
    }

    // Chunky rounded HUD icons. Generated art in Resources/Art2D/Generated replaces these when present.
    public static class HudIcons
    {
        private static readonly Dictionary<string,Texture2D> cache = new Dictionary<string,Texture2D>();
        public static Texture2D Get(string id)
        {
            Texture2D t;
            if (cache.TryGetValue(id,out t) && t != null) return t;
            t = Resources.Load<Texture2D>("Art2D/Generated/" + id) ?? Draw(id); cache[id] = t; return t;
        }
        private static Texture2D Draw(string id)
        {
            const int n = 128; var t = new Texture2D(n,n,TextureFormat.RGBA32,false) { filterMode = FilterMode.Bilinear,name = id };
            Color outline = HudTheme.Hex(0x1E2230);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) {
                float u = (x + .5f) / n * 2 - 1, v = (y + .5f) / n * 2 - 1, r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v,u);
                Color c = Color.clear;
                switch (id) {
                    case "icon_coin":
                        if (r < .9f) c = outline; if (r < .82f) c = HudTheme.Hex(0xE3A12A); if (r < .64f) c = HudTheme.Hex(0xFFD04A);
                        if (r < .64f && u < -.1f && v > .1f && r > .4f) c = HudTheme.Hex(0xFFE58A); break;
                    case "icon_timer":
                        if (r < .88f) c = outline; if (r < .76f) c = HudTheme.Hex(0xFFF4DC);
                        if ((Mathf.Abs(u) < .07f && v > -.05f && v < .55f) || (Mathf.Abs(v) < .07f && u > -.05f && u < .4f)) c = HudTheme.Hex(0x1B2238);
                        if (Mathf.Abs(u) < .14f && v > .82f && v < .98f) c = outline; break;
                    case "icon_gear": {
                        float tooth = .72f + .16f * Mathf.Clamp01(Mathf.Cos(a * 8) * 3);
                        if (r < tooth + .08f) c = outline; if (r < tooth) c = HudTheme.Hex(0xAEB8D6);
                        if (r < .34f) c = outline; if (r < .26f) c = HudTheme.Hex(0x4B5784); break;
                    }
                    case "icon_heart": {
                        float hx = Mathf.Abs(u) * 1.05f, hy = -v * 1.05f + .15f;
                        float d = Mathf.Pow(hx * hx + hy * hy - .5f,3) - hx * hx * hy * hy * hy;
                        if (d < .02f) c = outline; if (d < 0) c = HudTheme.Hex(0xFF5A64); break;
                    }
                    case "icon_lock": {
                        // Chunky padlock: gold shackle over a rounded body with a dark keyhole.
                        bool shackle = v > .05f && v < .78f && Mathf.Abs(Mathf.Sqrt(u * u + (v - .3f) * (v - .3f) * 1.2f) - .36f) < .1f && v > .25f;
                        bool body = Mathf.Abs(u) < .55f && v > -.75f && v < .2f;
                        if (shackle) c = HudTheme.Hex(0xC9CFE0); if (body) c = HudTheme.Hex(0xFFD04A);
                        if (body && (Mathf.Abs(u) > .47f || v < -.67f || v > .12f)) c = HudTheme.Hex(0xB8862A);
                        if (body && Mathf.Abs(u) < .08f && v > -.45f && v < -.05f) c = HudTheme.Hex(0x1E2230); break;
                    }
                    case "round_frame": {
                        // Navy corners with a round window and a slate rim: laid over a square portrait it reads as a round frame.
                        float d = r * 64; c = HudTheme.PanelFill; c.a = 1;
                        if (d < 61) c = HudTheme.PanelBorder; if (d < 57) c = Color.clear; break;
                    }
                    case "round_ring": { float d = r * 64; c = d > 56 && d < 63 ? HudTheme.Gold : Color.clear; break; }
                    case "boss_entry_marker": {
                        // Rounded coral badge with a cream chevron pointing down (rotate per edge).
                        if (r < .92f) c = outline; if (r < .82f) c = HudTheme.Hex(0xE86A4A);
                        float chevron = Mathf.Abs(u) * .9f - v * .9f;
                        if (chevron > -.05f && chevron < .32f && v < .45f && v > -.55f && r < .7f) c = HudTheme.Hex(0xFFE9C7); break;
                    }
                }
                t.SetPixel(x,y,c);
            }
            t.Apply(); return t;
        }
        public static void Draw(Rect r,string id)
        { if (Event.current.type == EventType.Repaint) GUI.DrawTexture(r,Get(id),ScaleMode.ScaleToFit,true); }
    }
}
