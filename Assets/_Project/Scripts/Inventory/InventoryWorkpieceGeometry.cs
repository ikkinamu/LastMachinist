using System;
using System.Collections.Generic;
using LastMachinist.Machining.Core;
using LastMachinist.Machining.Turning;

namespace LastMachinist.Inventory
{
    public static class InventoryWorkpieceGeometry
    {
        // Virgin stock is represented lazily in existing saves. Both mounting and queries
        // must construct that initial geometry through this same path.
        public static WorkpieceState CreateInitialState(WorkpieceInstance item, WorkpieceDefinition definition)
        {
            if (item == null || definition == null || item.WorkpieceDefinitionId != definition.WorkpieceDefinitionId ||
                !InventoryMachiningSession.SupportsTurning(definition))
                throw new ArgumentException("Missing or unsupported initial stock geometry.");
            var size = definition.NominalDimensions;
            return new WorkpieceState {
                InstanceId = item.InstanceId, MaterialId = item.MaterialId, SourceItemId = item.WorkpieceDefinitionId,
                Geometry = new WorkpieceGeometry { Type = GeometryType.RoundProfile, RoundProfile = new TurningProfile {
                    TotalLength = size.Length, Segments = new List<TurningSegment> {
                        new TurningSegment { EndZ = size.Length, Radius = size.Diameter / 2 } } } } };
        }

        public static bool TryGetActualLength(WorkpieceInstance item, WorkpieceDefinition definition, out float length)
        {
            length = 0;
            if (item == null) return false;
            var profile = item.MachiningWorkpiece?.Geometry?.RoundProfile;
            if (profile == null)
            {
                // Never replace missing saved/cut geometry with nominal dimensions.
                if (item.HasMachiningGeometry || definition == null || item.WorkpieceDefinitionId != definition.WorkpieceDefinitionId ||
                    !InventoryMachiningSession.SupportsTurning(definition)) return false;
                profile = CreateInitialState(item, definition).Geometry.RoundProfile;
            }
            length = profile.GetAxialLength();
            return true;
        }
    }
}
