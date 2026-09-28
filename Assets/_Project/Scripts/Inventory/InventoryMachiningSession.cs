using System;
using System.Collections.Generic;
using LastMachinist.Machining.Core;
using LastMachinist.Machining.Turning;

namespace LastMachinist.Inventory
{
    public sealed class InventoryMachiningSession
    {
        private readonly InventoryState inventory;
        private readonly Func<string, WorkpieceDefinition> resolve;
        private bool starting;
        public WorkpieceInstance Active => inventory.FindWorkpiece(inventory.ActiveMachiningInstanceId);
        public InventoryMachiningSession(InventoryState inventory, Func<string, WorkpieceDefinition> resolve)
        { this.inventory = inventory; this.resolve = resolve; }
        public static bool SupportsTurning(WorkpieceDefinition definition)
        {
            if (definition == null || definition.ShapeType != WorkpieceShape.RoundBar || definition.NominalDimensions == null) return false;
            var d = definition.NominalDimensions;
            return d.Diameter > 0 && d.Length > 0 && !float.IsInfinity(d.Diameter) && !float.IsInfinity(d.Length);
        }
        public List<WorkpieceInstance> Candidates()
        {
            var result = new List<WorkpieceInstance>();
            foreach (var item in inventory.ListWorkpieces)
                if (item.Availability == WorkpieceAvailability.Available && SupportsTurning(resolve(item.WorkpieceDefinitionId))) result.Add(item);
            return result;
        }
        public bool TryBegin(string instanceId, Func<WorkpieceState, bool> initialize)
        {
            if (starting || Active != null || initialize == null) return false;
            var item = inventory.FindWorkpiece(instanceId);
            if (item == null || item.Availability != WorkpieceAvailability.Available) return false;
            var definition = resolve(item.WorkpieceDefinitionId);
            if (!SupportsTurning(definition)) return false;
            starting = true;
            try
            {
                item.Validate();
                // Initialize from a working copy so a failed adapter cannot corrupt stored cuts.
                var state = item.MachiningWorkpiece?.Copy() ?? InventoryWorkpieceGeometry.CreateInitialState(item, definition);
                state.Validate();
                if (!initialize(state)) return false;
                item.MachiningWorkpiece = state;
                item.HasMachiningGeometry = true;
                item.Availability = WorkpieceAvailability.InMachining;
                inventory.ActiveMachiningInstanceId = item.InstanceId;
                return true;
            }
            catch (Exception) { return false; }
            finally { starting = false; }
        }
        public void End() => inventory.ReleaseMachining();
    }
}
