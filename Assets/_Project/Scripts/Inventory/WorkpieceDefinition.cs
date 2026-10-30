using System;
using LastMachinist.Core;
using LastMachinist.Machining.Core;
using UnityEngine;

namespace LastMachinist.Inventory
{
    // Plate was value 1 in shipped inventory definitions. Never renumber it.
    public enum WorkpieceShape { RoundBar = 0, Plate = 1, SquareBar = 2, RectangularBar = 3, Tube = 4 }
    [Serializable]
    public sealed class NominalWorkpieceDimensions
    {
        public float Diameter, Length, Width, Height, Thickness, InnerDiameter;
    }

    [CreateAssetMenu(menuName = "Last Machinist/Workpiece Definition")]
    public sealed class WorkpieceDefinition : ScriptableObject
    {
        public string WorkpieceDefinitionId;
        public string DisplayName;
        public WorkpieceShape ShapeType;
        public NominalWorkpieceDimensions NominalDimensions = new NominalWorkpieceDimensions();
        public MaterialDefinition Material;
        public InspectionProperty[] BaseInspectionProperties = Array.Empty<InspectionProperty>();
        public Texture2D InventoryIcon;

        public WorkpieceInstance CreateInstance(ConditionProperties conditions, string regionId, string sessionId)
        {
            if (string.IsNullOrWhiteSpace(WorkpieceDefinitionId) || Material == null || conditions == null)
                throw new ArgumentException("Missing stock definition data.");
            Material.Validate(); conditions.Validate();
            var item = new WorkpieceInstance { InstanceId = Guid.NewGuid().ToString("N"),
                WorkpieceDefinitionId = WorkpieceDefinitionId, MaterialId = Material.Id,
                Conditions = conditions.Copy(), IdentificationState = IdentificationState.Unknown,
                AcquiredRegionId = regionId, AcquiredSessionId = sessionId, AcquisitionSource = "ExplorationRecovery" };
            if (ShapeType != WorkpieceShape.RoundBar)
            {
                item.MachiningWorkpiece = InventoryWorkpieceGeometry.CreateInitialState(item, this);
                item.HasMachiningGeometry = true;
            }
            return item;
        }
    }
}
