using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    [CreateAssetMenu(menuName = "Container Defense/Characters and Account Progression")]
    public sealed class CharacterConfig : ScriptableObject
    {
        public CharacterDefinition[] Characters = CharacterCatalog.Defaults();
        public ProgressionRules Progression = new ProgressionRules();
        public void Validate() { new CharacterCatalog(Characters); Progression.Snapshot(); }
        private void OnValidate()
        {
            try { Validate(); }
            catch (System.ArgumentException e) { Debug.LogError(e.Message,this); }
        }
    }
}
