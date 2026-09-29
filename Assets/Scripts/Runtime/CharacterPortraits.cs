using ContainerDefense.Domain;
using System.Collections.Generic;
using UnityEngine;

namespace ContainerDefense
{
    public sealed class CharacterPortraits
    {
        private readonly Dictionary<string,RenderTexture> portraits = new Dictionary<string,RenderTexture>();
        public Texture Get(CharacterId id) { return Get(CharacterCatalog.Key(id) + "_default"); }
        public Texture Get(string skinId)
        {
            if (skinId == null) return null;
            var illustration = MenuArtwork.Get(skinId);
            if (illustration != null) return illustration;
            RenderTexture p; return portraits.TryGetValue(skinId,out p) ? p : null;
        }
        public void Build(ToyFactory art, Transform parent, CollectionCatalog catalog)
        {
            var studio = art.Root("Portrait studio",parent,new Vector3(1000,0,0));
            var camera = new GameObject("Portrait camera").AddComponent<Camera>();
            camera.transform.SetParent(studio,false); camera.transform.localPosition = new Vector3(0,1.1f,-4);
            camera.transform.LookAt(studio.position + Vector3.up * 1.02f);
            camera.orthographic = true; camera.orthographicSize = 1.2f; camera.aspect = 1;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << 8; camera.enabled = false;
            var light = new GameObject("Portrait light").AddComponent<Light>(); light.transform.SetParent(studio,false);
            light.type = LightType.Directional; light.intensity = 1; light.color = new Color(1,.88f,.76f);
            light.cullingMask = 1 << 8; light.transform.rotation = Quaternion.Euler(25,-25,0);
            foreach (var skin in catalog.Skins)
            {
                var resident = ResidentFactory.Create(art,skin.CharacterId,studio,skin);
                foreach (var t in resident.GetComponentsInChildren<Transform>()) t.gameObject.layer = 8;
                var texture = new RenderTexture(256,256,16,RenderTextureFormat.ARGB32) { name = skin.SkinId + " portrait", antiAliasing = 4 };
                texture.Create(); camera.targetTexture = texture; camera.Render(); portraits.Add(skin.SkinId,texture);
                resident.gameObject.SetActive(false); Object.Destroy(resident.gameObject);
            }
            camera.targetTexture = null; studio.gameObject.SetActive(false); Object.Destroy(studio.gameObject);
        }
        public void Dispose()
        { foreach (var p in portraits.Values) if (p != null) { p.Release(); Object.Destroy(p); } portraits.Clear(); }
    }
}
