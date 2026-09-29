using System.Collections.Generic;
using UnityEngine;

namespace ContainerDefense
{
    // Small original procedural icons for the prototype collection.
    public sealed class StickerIcons
    {
        private readonly Dictionary<string,Texture2D> cache = new Dictionary<string,Texture2D>();
        public Texture2D Get(string key)
        {
            Texture2D icon; if (cache.TryGetValue(key,out icon)) return icon;
            icon = new Texture2D(128,128,TextureFormat.RGBA32,false) { name = key + " sticker", filterMode = FilterMode.Bilinear };
            Color fill = key == "star" ? new Color(1,.78f,.23f) : key == "cat" ? new Color(1,.63f,.34f) :
                key == "heart" ? new Color(1,.35f,.61f) : key == "good" ? new Color(.69f,.48f,.92f) : new Color(1,.93f,.9f);
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float px = (x - 63.5f) / 64, py = (y - 63.5f) / 64;
                bool shape = Shape(key,px,py), border = false;
                if (shape) for (int i = 0; i < 8; i++)
                    if (!Shape(key,px + Mathf.Cos(i * Mathf.PI / 4) * .045f,py + Mathf.Sin(i * Mathf.PI / 4) * .045f)) border = true;
                Color c = shape ? (border ? Color.white : fill) : Color.clear;
                if (shape && !border)
                {
                    if (Ellipse(px,py,-.19f,-.1f,.045f,.065f) || Ellipse(px,py,.19f,-.1f,.045f,.065f)) c = new Color(.22f,.13f,.26f);
                    if (Ellipse(px,py,0,-.26f,.09f,.037f)) c = new Color(.45f,.15f,.3f);
                    if (Ellipse(px,py,-.34f,-.23f,.08f,.035f) || Ellipse(px,py,.34f,-.23f,.08f,.035f)) c = new Color(1,.4f,.55f);
                    if (key == "bunny" && (Ellipse(px,py,-.25f,.49f,.07f,.2f) || Ellipse(px,py,.25f,.49f,.07f,.2f))) c = new Color(1,.65f,.75f);
                }
                icon.SetPixel(x,y,c);
            }
            icon.Apply(); cache.Add(key,icon); return icon;
        }
        private static bool Ellipse(float x,float y,float cx,float cy,float rx,float ry)
        { return (x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry) <= 1; }
        private static bool Shape(string key,float x,float y)
        {
            if (key == "bunny") return Ellipse(x,y,0,-.22f,.62f,.5f) || Ellipse(x,y,-.25f,.43f,.16f,.47f) || Ellipse(x,y,.25f,.43f,.16f,.47f);
            if (key == "heart") return Ellipse(x,y,-.28f,.2f,.4f,.4f) || Ellipse(x,y,.28f,.2f,.4f,.4f) || (y < .22f && y > -.72f && Mathf.Abs(x) < (y + .72f) * .75f);
            if (key == "star")
            {
                float angle = Mathf.Atan2(x,y), radius = Mathf.Sqrt(x * x + y * y);
                float sector = Mathf.Repeat(angle + Mathf.PI / 5,Mathf.PI * 2 / 5) - Mathf.PI / 5;
                return radius < Mathf.Lerp(.82f,.4f,Mathf.Abs(sector) / (Mathf.PI / 5));
            }
            if (key == "cat") return Ellipse(x,y,0,-.12f,.65f,.57f) || (y > .12f && y < .75f && Mathf.Abs(x) > .22f + (y - .12f) * .38f && Mathf.Abs(x) < .64f);
            return Ellipse(x,y,0,0,.73f,.73f);
        }
        public void Dispose() { foreach (var icon in cache.Values) Object.Destroy(icon); cache.Clear(); }
    }
}
