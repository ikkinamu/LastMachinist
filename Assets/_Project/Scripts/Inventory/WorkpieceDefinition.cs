using System;
using LastMachinist.Core;
using LastMachinist.Machining.Core;
using UnityEngine;

namespace LastMachinist.Inventory
{
    public enum WorkpieceShape { RoundBar, Plate }
    [Serializable]
    public sealed class NominalWorkpieceDimensions
    {
        public float Diameter, Length, Width, Thickness;
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
            return new WorkpieceInstance { InstanceId = Guid.NewGuid().ToString("N"),
                WorkpieceDefinitionId = WorkpieceDefinitionId, MaterialId = Material.Id,
                Conditions = conditions.Copy(), IdentificationState = IdentificationState.Unknown,
                AcquiredRegionId = regionId, AcquiredSessionId = sessionId };
        }
    }
}
