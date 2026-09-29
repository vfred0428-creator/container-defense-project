using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    [CreateAssetMenu(menuName = "Container Defense/Match Rules")]
    public sealed class MatchConfig : ScriptableObject
    {
        public MatchRules Rules = new MatchRules();
        private void OnValidate()
        {
            try { Rules.Validate(); }
            catch (System.ArgumentException e) { Debug.LogError("Invalid match rules: " + e.Message, this); }
        }
    }
}
