using System;
using UnityEngine;

namespace LastMachinist.Core
{
    [Serializable]
    public sealed class InspectionProperty
    {
        public string PropertyLabel;
        public string PropertyValue;
    }

    [CreateAssetMenu(menuName = "Last Machinist/Inspectable Item")]
    public sealed class InspectableItemDefinition : ScriptableObject
    {
        public string ItemId;
        public string DisplayName;
        public InspectionProperty[] InspectionProperties = Array.Empty<InspectionProperty>();
        public bool CanRecover = true;
        public string RecoveryId;
        public LastMachinist.Inventory.WorkpieceDefinition Workpiece;
        // Internal facts are never used by the inspection window.
        public string ActualMaterialType;
        public Vector3 ActualDimensions;
    }

    [Serializable]
    public sealed class ExplorationItemPlacement
    {
        public InspectableItemDefinition Definition;
        public Vector3 Position;
        public Vector3 Scale = Vector3.one;
        public PrimitiveType Shape = PrimitiveType.Cube;
        public LastMachinist.Inventory.ConditionProperties Conditions = new LastMachinist.Inventory.ConditionProperties();
    }
}
