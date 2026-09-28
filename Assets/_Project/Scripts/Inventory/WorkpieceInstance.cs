using System;

namespace LastMachinist.Inventory
{
    public enum IdentificationState { Unknown, Identified }
    public enum WorkpieceAvailability { Available, InMachining }

    [Serializable]
    public sealed class ConditionProperties
    {
        public int CorrosionCondition = 100;
        public int SurfaceCondition = 100;
        public int ShapeIntegrity = 100;
        public int DefectIntegrity = 100;
        public float OverallConditionScore => (CorrosionCondition + SurfaceCondition + ShapeIntegrity + DefectIntegrity) / 4f;
        public void Validate()
        {
            foreach (var score in new[] { CorrosionCondition, SurfaceCondition, ShapeIntegrity, DefectIntegrity })
                if (score < 0 || score > 100) throw new ArgumentOutOfRangeException("Condition must be 0..100.");
        }
        public ConditionProperties Copy() => (ConditionProperties)MemberwiseClone();
    }

    // Owned stock metadata, separate from the existing mounted/cut WorkpieceState geometry.
    // Only IDs and values enter the existing save DTO, never ScriptableObject/scene references.
    [Serializable]
    public sealed class WorkpieceInstance
    {
        public string InstanceId;
        public string ParentInstanceId;
        public string WorkpieceDefinitionId;
        public string MaterialId;
        public ConditionProperties Conditions = new ConditionProperties();
        public IdentificationState IdentificationState;
        public string AcquiredRegionId;
        public string AcquiredSessionId;
        public WorkpieceAvailability Availability;
        public bool HasMachiningGeometry;
        public LastMachinist.Machining.Core.WorkpieceState MachiningWorkpiece;
        public System.Collections.Generic.List<LastMachinist.Measurement.RecordedMeasurement> MachiningMeasurements = new System.Collections.Generic.List<LastMachinist.Measurement.RecordedMeasurement>();
        public float OverallConditionScore => Conditions.OverallConditionScore;
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(InstanceId) || string.IsNullOrWhiteSpace(WorkpieceDefinitionId) ||
                string.IsNullOrWhiteSpace(MaterialId) || Conditions == null || !Enum.IsDefined(typeof(IdentificationState), IdentificationState))
                throw new ArgumentException("Invalid workpiece instance.");
            Conditions.Validate();
            MachiningMeasurements ??= new System.Collections.Generic.List<LastMachinist.Measurement.RecordedMeasurement>();
            foreach (var record in MachiningMeasurements)
                if (record == null || record.WorkpieceInstanceId != InstanceId || string.IsNullOrWhiteSpace(record.MeasurementPointId) || float.IsNaN(record.PlayerValue) || float.IsInfinity(record.PlayerValue))
                    throw new ArgumentException("Invalid inventory measurement record.");
            if (!Enum.IsDefined(typeof(WorkpieceAvailability), Availability)) throw new ArgumentException("Invalid availability.");
            // JsonUtility expands null serializable classes into empty objects on load.
            // Keep absence explicit so virgin stock is not mistaken for corrupt cut geometry.
            if (!HasMachiningGeometry)
            {
                if (MachiningWorkpiece != null && !string.IsNullOrEmpty(MachiningWorkpiece.InstanceId))
                    throw new ArgumentException("Unmarked machining geometry.");
                MachiningWorkpiece = null;
            }
            else
            {
                if (MachiningWorkpiece == null) throw new ArgumentException("Missing machining geometry.");
                MachiningWorkpiece.Validate();
                if (MachiningWorkpiece.InstanceId != InstanceId || MachiningWorkpiece.MaterialId != MaterialId || MachiningWorkpiece.SourceItemId != WorkpieceDefinitionId)
                    throw new ArgumentException("Machining identity does not match owned workpiece.");
            }
        }
        public WorkpieceInstance Copy()
        {
            var copy = (WorkpieceInstance)MemberwiseClone();
            copy.Conditions = Conditions.Copy();
            copy.MachiningWorkpiece = MachiningWorkpiece?.Copy();
            copy.MachiningMeasurements = MachiningMeasurements.ConvertAll(r => new LastMachinist.Measurement.RecordedMeasurement {
                WorkpieceInstanceId = r.WorkpieceInstanceId, MeasurementPointId = r.MeasurementPointId,
                InstrumentId = r.InstrumentId, InstrumentReadingValue = r.InstrumentReadingValue, PlayerValue = r.PlayerValue, RecordedAtTicks = r.RecordedAtTicks });
            return copy;
        }
    }
}
