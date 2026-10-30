using System;
using System.Collections.Generic;
using LastMachinist.Machining.Core;
using LastMachinist.Machining.Turning;
using UnityEngine;

namespace LastMachinist.Inventory
{
    public static class InitialWorkshopWorkpieces
    {
        public const string Source = "InitialWorkshopStock";
        public const float InitialLength = 100f;
        public static int EnsureCreated(InventoryState inventory, string sessionId)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            int existing = 0;
            foreach (var piece in inventory.Workpieces)
                if (piece.AcquisitionSource == Source) existing++;
            if (existing > 0 || inventory.InitialWorkshopStockCreated) { inventory.InitialWorkshopStockCreated = true; return existing; }
            var definition = Resources.Load<WorkpieceDefinition>("WorkpieceDefinitions/RoundBar50");
            if (definition == null || !InventoryMachiningSession.SupportsTurning(definition))
                throw new InvalidOperationException("Initial workshop workpiece definition is missing.");
            for (int i = 0; i < 3; i++)
            {
                var item = definition.CreateInstance(new ConditionProperties {
                    CorrosionCondition = 100, SurfaceCondition = 100, ShapeIntegrity = 100, DefectIntegrity = 100 },
                    "workshop", sessionId);
                item.AcquisitionSource = Source;
                item.HasMachiningGeometry = true;
                item.MachiningWorkpiece = InventoryWorkpieceGeometry.CreateInitialState(item, definition);
                item.Validate();
                if (!inventory.AddWorkpiece(item)) throw new InvalidOperationException("Initial workpiece could not be added.");
            }
            inventory.InitialWorkshopStockCreated = true;
            return 3;
        }

        // Compatibility conversion only: each old stock unit becomes one owned object.
        // General stackable items remain in Entries. No grant is made for an empty save.
        public static void MigrateLegacyStock(InventoryState inventory, Func<WorkpieceState> legacyFactory)
        {
            var entries = inventory.Entries.FindAll(e => e.ItemId == LastMachinist.Integration.Quest01Integration.StockItemId);
            if (entries.Count == 0) return;
            var definition = Resources.Load<WorkpieceDefinition>("WorkpieceDefinitions/RoundBar50");
            var converted = new List<WorkpieceInstance>();
            foreach (var entry in entries)
                for (int i = 0; i < entry.Quantity; i++)
                {
                    var item = definition.CreateInstance(new ConditionProperties(), "workshop", "legacy-save");
                    item.AcquisitionSource = Source;
                    var geometry = legacyFactory().Copy();
                    geometry.InstanceId = item.InstanceId; geometry.MaterialId = item.MaterialId; geometry.SourceItemId = item.WorkpieceDefinitionId;
                    item.HasMachiningGeometry = true; item.MachiningWorkpiece = geometry;
                    item.Validate(); converted.Add(item);
                }
            inventory.Entries.RemoveAll(e => entries.Contains(e));
            inventory.Workpieces.AddRange(converted);
            inventory.InitialWorkshopStockCreated = true;
            inventory.Validate();
        }
    }
}
