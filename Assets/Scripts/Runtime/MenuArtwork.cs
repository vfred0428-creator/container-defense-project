using System.Collections.Generic;
using UnityEngine;

namespace ContainerDefense
{
    public static class MenuArtwork
    {
        private static readonly Dictionary<string,Texture2D> loaded = new Dictionary<string,Texture2D>();
        public static Texture2D Get(string id)
        {
            Texture2D texture;
            if (!loaded.TryGetValue(id,out texture)) { texture = Resources.Load<Texture2D>("Art/" + id); loaded[id] = texture; }
            return texture;
        }
        public static void Background(Rect rect)
        {
            var texture = Get("menu_yard");
            if (texture != null) GUI.DrawTexture(rect,texture,ScaleMode.ScaleAndCrop);
            var previous = GUI.color; GUI.color = new Color(.035f,.045f,.1f,.2f);
            GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color = previous;
        }
    }
}
