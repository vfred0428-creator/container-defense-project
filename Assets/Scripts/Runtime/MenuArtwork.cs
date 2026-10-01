using System.Collections.Generic;
using UnityEngine;

namespace ContainerDefense
{
    public static class MenuArtwork
    {
        private static readonly Dictionary<string,Texture2D> loaded = new Dictionary<string,Texture2D>();
        private static RenderTexture blurred;
        public static Texture2D Get(string id)
        {
            Texture2D texture;
            if (!loaded.TryGetValue(id,out texture)) { texture = Resources.Load<Texture2D>("Art2D/" + id); loaded[id] = texture; }
            return texture;
        }
        // Menu backdrop: the yard art downscaled to 1/16 and back up (a cheap blur), then darkened,
        // so painted numbers and details behind the UI melt into soft colour.
        public static void Background(Rect rect)
        {
            if (blurred != null) GUI.DrawTexture(rect,blurred,ScaleMode.ScaleAndCrop);
            var previous = GUI.color; GUI.color = new Color(.05f,.06f,.12f,blurred != null ? .55f : .9f);
            GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color = previous;
        }
        // Builds the blurred backdrop once. Call outside OnGUI (blitting inside GUI events is unreliable).
        public static void Prepare()
        {
            var texture = Get("Generated/menu_bg") ?? Get("yard");
            if (texture != null) {
                if (blurred == null) {
                    // Blit changes the active target; restore it or later GUI drawing lands in the blur texture.
                    var active = RenderTexture.active;
                    var small = RenderTexture.GetTemporary(Mathf.Max(8,texture.width / 16),Mathf.Max(8,texture.height / 16),0);
                    small.filterMode = FilterMode.Bilinear; Graphics.Blit(texture,small);
                    var mid = RenderTexture.GetTemporary(Mathf.Max(8,texture.width / 32),Mathf.Max(8,texture.height / 32),0);
                    mid.filterMode = FilterMode.Bilinear; Graphics.Blit(small,mid);
                    blurred = new RenderTexture(Mathf.Max(8,texture.width / 8),Mathf.Max(8,texture.height / 8),0) { filterMode = FilterMode.Bilinear,name = "Menu blur" };
                    Graphics.Blit(mid,blurred); RenderTexture.ReleaseTemporary(small); RenderTexture.ReleaseTemporary(mid);
                    RenderTexture.active = active;
                }
            }
        }
    }
}
