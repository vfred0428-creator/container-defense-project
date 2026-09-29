using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    [CreateAssetMenu(menuName = "Container Defense/Collections")]
    public sealed class CollectionConfig : ScriptableObject
    {
        public SkinDefinition[] Skins = CollectionCatalog.DefaultSkins();
        public StickerDefinition[] Stickers = CollectionCatalog.DefaultStickers();
        public string[] StarterSkins = CollectionCatalog.DefaultStarterSkins();
        public StickerStack[] StarterStickers = CollectionCatalog.DefaultStarterStickers();
        public CollectionCatalog Catalog() { return new CollectionCatalog(Skins,Stickers,StarterSkins,StarterStickers); }
    }
}
