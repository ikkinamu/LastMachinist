using UnityEngine;

namespace LastMachinist.Core
{
    // World identity/data binding; no inventory or quest mutation.
    public sealed class InspectableItem : MonoBehaviour
    {
        [SerializeField] private InspectableItemDefinition definition;
        public InspectableItemDefinition Definition => definition;
        public LastMachinist.Inventory.WorkpieceInstance Instance { get; private set; }
        public string RecoveryKey => Instance?.InstanceId ?? definition?.ItemId;
        public void Initialize(InspectableItemDefinition value) => definition = value;
        public void BindInstance(LastMachinist.Inventory.WorkpieceInstance instance) => Instance = instance;
    }
}
