using System;
using System.Collections.Generic;

namespace LastMachinist.Inventory
{
    // ItemId resolves a static definition; InstanceId identifies unique owned state.
    [Serializable]
    public sealed class InventoryEntry
    {
        public string ItemId;
        public int Quantity;
        public string InstanceId;
    }

    [Serializable]
    public sealed class InventoryState
    {
        public List<InventoryEntry> Entries = new List<InventoryEntry>();
        public List<WorkpieceInstance> Workpieces = new List<WorkpieceInstance>();
        public string ActiveMachiningInstanceId;
        public IReadOnlyList<WorkpieceInstance> ListWorkpieces => Workpieces;
        public WorkpieceInstance FindWorkpiece(string instanceId) => Workpieces.Find(w => w.InstanceId == instanceId);
        public bool AddWorkpiece(WorkpieceInstance instance)
        {
            if (instance == null || instance.Availability != WorkpieceAvailability.Available) return false;
            try { instance.Validate(); Validate(); }
            catch (ArgumentException) { return false; }
            if (FindWorkpiece(instance.InstanceId) != null || Entries.Exists(e => e.InstanceId == instance.InstanceId)) return false;
            Workpieces.Add(instance.Copy());
            return true;
        }
        public bool RemoveWorkpiece(string instanceId)
        {
            var instance = FindWorkpiece(instanceId);
            return instance != null && instance.Availability == WorkpieceAvailability.Available && Workpieces.Remove(instance);
        }
        public void ReleaseMachining()
        {
            foreach (var piece in Workpieces) piece.Availability = WorkpieceAvailability.Available;
            ActiveMachiningInstanceId = null;
        }

        public void Add(string itemId, int quantity, string instanceId = null)
        {
            CheckArguments(itemId, quantity);
            Validate();
            instanceId = string.IsNullOrEmpty(instanceId) ? null : instanceId;
            if (instanceId != null)
            {
                if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("Instance ID cannot be whitespace.", nameof(instanceId));
                if (quantity != 1) throw new ArgumentException("Unique items must have quantity one.");
                if (Entries.Exists(e => e.InstanceId == instanceId) || FindWorkpiece(instanceId) != null)
                    throw new ArgumentException("Instance already owned.", nameof(instanceId));
            }
            var entry = Entries.Find(e => e.ItemId == itemId && string.IsNullOrEmpty(e.InstanceId));
            if (instanceId == null && entry != null)
                entry.Quantity = checked(entry.Quantity + quantity);
            else
                Entries.Add(new InventoryEntry { ItemId = itemId, Quantity = quantity, InstanceId = instanceId });
        }

        // A unique item must be removed by its exact instance ID. Insufficient stock is atomic.
        public bool TryRemove(string itemId, int quantity, string instanceId = null)
        {
            CheckArguments(itemId, quantity);
            Validate();
            if (!string.IsNullOrEmpty(instanceId) && quantity != 1)
                throw new ArgumentException("Unique items must have quantity one.");
            var entry = Entries.Find(e => e.ItemId == itemId &&
                (string.IsNullOrEmpty(instanceId) ? string.IsNullOrEmpty(e.InstanceId) : e.InstanceId == instanceId));
            if (entry == null || entry.Quantity < quantity) return false;
            entry.Quantity -= quantity;
            if (entry.Quantity == 0) Entries.Remove(entry);
            return true;
        }

        public int GetQuantity(string itemId)
        {
            CheckArguments(itemId, 1);
            Validate();
            int quantity = 0;
            foreach (var entry in Entries)
                if (entry.ItemId == itemId) quantity = checked(quantity + entry.Quantity);
            return quantity;
        }

        public void Validate()
        {
            if (Entries == null) throw new ArgumentException("Missing inventory entries.");
            var stacks = new HashSet<string>(StringComparer.Ordinal);
            var instances = new HashSet<string>(StringComparer.Ordinal);
            // Version 1 saves written before Phase 03 have no Workpieces field.
            Workpieces ??= new List<WorkpieceInstance>();
            foreach (var piece in Workpieces)
            {
                if (piece == null) throw new ArgumentException("Null inventory workpiece.");
                piece.Validate();
                if (!instances.Add(piece.InstanceId)) throw new ArgumentException("Duplicate inventory workpiece.");
                if (piece.Availability == WorkpieceAvailability.InMachining && (piece.InstanceId != ActiveMachiningInstanceId || piece.MachiningWorkpiece == null))
                    throw new ArgumentException("Unlinked active workpiece.");
            }
            if (!string.IsNullOrEmpty(ActiveMachiningInstanceId) &&
                (FindWorkpiece(ActiveMachiningInstanceId) == null || FindWorkpiece(ActiveMachiningInstanceId).Availability != WorkpieceAvailability.InMachining))
                throw new ArgumentException("Invalid active machining ID.");
            foreach (var entry in Entries)
            {
                if (entry == null) throw new ArgumentException("Null inventory entry.");
                CheckArguments(entry.ItemId, entry.Quantity);
                if (string.IsNullOrEmpty(entry.InstanceId))
                {
                    if (!stacks.Add(entry.ItemId)) throw new ArgumentException("Duplicate aggregate entry.");
                }
                else if (string.IsNullOrWhiteSpace(entry.InstanceId) || entry.Quantity != 1 || !instances.Add(entry.InstanceId))
                    throw new ArgumentException("Invalid unique inventory entry.");
            }
        }

        private static void CheckArguments(string itemId, int quantity)
        {
            if (string.IsNullOrWhiteSpace(itemId)) throw new ArgumentException("Item ID is required.", nameof(itemId));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        }
    }
}
