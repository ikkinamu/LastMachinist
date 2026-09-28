using System.Collections.Generic;

namespace LastMachinist.Core
{
    // Session-only domain state; no scene objects, UI or permanent inventory.
    public sealed class ExplorationRecoveryState
    {
        private readonly HashSet<string> itemIds = new HashSet<string>();
        public IReadOnlyCollection<string> ItemIds => itemIds;
        public bool Contains(string itemId) => itemIds.Contains(itemId);
        public bool TryRecord(string itemId) => !string.IsNullOrWhiteSpace(itemId) && itemIds.Add(itemId);
    }
}
