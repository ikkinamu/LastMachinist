using System.Collections.Generic;
using UnityEngine;

namespace LastMachinist.Quest
{
    [CreateAssetMenu(menuName = "Last Machinist/Dialogue Catalog")]
    public sealed class DialogueCatalogDefinition : ScriptableObject
    {
        public List<DialogueNode> Nodes = new List<DialogueNode>();
        public DialogueCatalog BuildRuntimeCatalog()
        {
            var catalog = new DialogueCatalog();
            foreach (var node in Nodes) catalog.Add(node);
            return catalog;
        }
    }
}
