using System.Collections.Generic;
using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    // Shared nine-cell production atlas; visual IDs never affect passive calculations.
    public sealed class CharacterSpriteSet
    {
        public readonly Texture2D Texture;
        public readonly string SkinId;
        private readonly Sprite[] frames = new Sprite[9];
        public CharacterSpriteSet(string skinId)
        {
            SkinId = skinId; Texture = Resources.Load<Texture2D>("Art2D/" + skinId);
            if (Texture == null) throw new System.InvalidOperationException("Missing 2D sprite set: " + skinId);
            for (int i = 0; i < frames.Length; i++)
            {
                Rect uv = Uv(i);
                frames[i] = Sprite.Create(Texture,new Rect(uv.x * Texture.width,uv.y * Texture.height,uv.width * Texture.width,uv.height * Texture.height),new Vector2(.5f,.1f),Texture.width / 3f);
                frames[i].name = skinId + " / " + i;
            }
        }
        public Sprite Frame(int index) { return frames[Mathf.Clamp(index,0,8)]; }
        public static Rect Uv(int index) { return new Rect(index % 3 / 3f,(2 - index / 3) / 3f,1f / 3,1f / 3); }
        public void Dispose() { foreach (var frame in frames) Object.Destroy(frame); }
    }

    public sealed class CharacterPortraits
    {
        private readonly Dictionary<string,CharacterSpriteSet> sets = new Dictionary<string,CharacterSpriteSet>();
        public void Build(CollectionCatalog catalog)
        { foreach (var skin in catalog.Skins) sets.Add(skin.SkinId,new CharacterSpriteSet(skin.SkinId)); }
        public CharacterSpriteSet Set(string skinId) { CharacterSpriteSet set; return skinId != null && sets.TryGetValue(skinId,out set) ? set : null; }
        public Texture Get(CharacterId id) { return Get(CharacterCatalog.Key(id) + "_default"); }
        public Texture Get(string skinId) { var set = Set(skinId); return set == null ? null : set.Texture; }
        public void Dispose() { foreach (var set in sets.Values) set.Dispose(); sets.Clear(); }
        public void Draw(Rect rect,string skinId,bool face = false,int frame = 8)
        {
            var set = Set(skinId); if (set == null) return;
            Rect uv = CharacterSpriteSet.Uv(frame);
            if (face) uv = new Rect(uv.x + uv.width * .1f,uv.y + uv.height * .35f,uv.width * .8f,uv.height * .6f);
            float aspect = face ? 1.3333f : 1;
            float w = Mathf.Min(rect.width,rect.height * aspect), h = w / aspect;
            GUI.DrawTextureWithTexCoords(new Rect(rect.center.x - w / 2,rect.center.y - h / 2,w,h),set.Texture,uv);
        }
    }
}
