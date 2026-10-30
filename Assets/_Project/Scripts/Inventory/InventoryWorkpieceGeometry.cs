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
                !Enum.IsDefined(typeof(WorkpieceShape), definition.ShapeType))
                throw new ArgumentException("Missing or unsupported initial stock geometry.");
            var size = definition.NominalDimensions;
            if (definition.ShapeType != WorkpieceShape.RoundBar)
            {
                var type = definition.ShapeType == WorkpieceShape.Plate ? GeometryType.Plate :
                    definition.ShapeType == WorkpieceShape.SquareBar ? GeometryType.SquareBar :
                    definition.ShapeType == WorkpieceShape.RectangularBar ? GeometryType.RectangularBar : GeometryType.Tube;
                var state = new WorkpieceState { InstanceId = item.InstanceId, MaterialId = item.MaterialId,
                    SourceItemId = item.WorkpieceDefinitionId, Geometry = new WorkpieceGeometry { Type = type,
                    General = new GeneralWorkpieceGeometry { Shape = type, Width = type == GeometryType.Tube ? size.Diameter : size.Width,
                        Height = size.Height, Length = size.Length, Thickness = size.Thickness, OuterDiameter = size.Diameter } } };
                state.Validate(); return state;
            }
            if (!InventoryMachiningSession.SupportsTurning(definition)) throw new ArgumentException("Invalid round stock dimensions.");
            // The starter bars are already cut to quest length. The definition's
            // nominal stock size remains the source for recovered/ordinary bars.
            float length = item.AcquisitionSource == InitialWorkshopWorkpieces.Source
                ? InitialWorkshopWorkpieces.InitialLength : size.Length;
            return new WorkpieceState {
                InstanceId = item.InstanceId, MaterialId = item.MaterialId, SourceItemId = item.WorkpieceDefinitionId,
                Geometry = new WorkpieceGeometry { Type = GeometryType.RoundProfile, RoundProfile = new TurningProfile {
                    TotalLength = length, Segments = new List<TurningSegment> {
                        new TurningSegment { EndZ = length, Radius = size.Diameter / 2 } } } } };
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

        public static bool TryGetRepresentativeDiameter(WorkpieceInstance item, WorkpieceDefinition definition, out float diameter)
        {
            diameter = 0;
            if (item == null) return false;
            var profile = item.MachiningWorkpiece?.Geometry?.RoundProfile;
            if (profile == null)
            {
                if (item.HasMachiningGeometry || !InventoryMachiningSession.SupportsTurning(definition) || item.WorkpieceDefinitionId != definition.WorkpieceDefinitionId) return false;
                profile = CreateInitialState(item, definition).Geometry.RoundProfile;
            }
            foreach (var span in profile.SurfaceSpans()) diameter = Math.Max(diameter, 2 * Math.Max(span.StartRadius, span.EndRadius));
            return true;
        }
    }
}
