using ContainerDefense.Domain;
using UnityEngine;
namespace ContainerDefense
{
    [CreateAssetMenu(menuName = "Container Defense/Fixed Map")]
    public sealed class MapConfig : ScriptableObject
    { public MapDefinition Definition = MapDefinition.Default(); }
}
