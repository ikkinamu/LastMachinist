using System;
using System.Collections.Generic;
using LastMachinist.Machining.Turning;

namespace LastMachinist.Machining.Core
{
    public enum Orientation { Normal, Reversed }
    public enum GeometryType { RoundProfile }

    // Tagged geometry payload: add another kind only when a later process needs it.
    [Serializable]
    public sealed class WorkpieceGeometry
    {
        public GeometryType Type;
        public TurningProfile RoundProfile;

        public void Validate()
        {
            if (Type != GeometryType.RoundProfile || RoundProfile == null)
                throw new ArgumentException("Unsupported or missing geometry payload.");
            RoundProfile.Validate();
        }
    }

    [Serializable]
    public sealed class WorkpieceState
    {
        public string InstanceId;
        public string MaterialId;
        public string SourceItemId;
        public WorkpieceGeometry Geometry;
        public Orientation Orientation;
        public bool IsFinished;
        public List<string> ProcessHistory = new List<string>();
        public WorkpieceState Copy()
        {
            Validate();
            var p = Geometry.RoundProfile;
            return new WorkpieceState { InstanceId = InstanceId, MaterialId = MaterialId, SourceItemId = SourceItemId,
                Orientation = Orientation, IsFinished = IsFinished, ProcessHistory = new List<string>(ProcessHistory),
                Geometry = new WorkpieceGeometry { Type = Geometry.Type, RoundProfile = new TurningProfile {
                    TotalLength = p.TotalLength,
                    InnerProfile = p.InnerProfile.Copy(),
                    Segments = p.Segments.ConvertAll(s => new TurningSegment { StartZ = s.StartZ, EndZ = s.EndZ, Radius = s.Radius }),
                    Chamfers = p.Chamfers.ConvertAll(c => new ChamferFeature { Position = c.Position, Width = c.Width, Angle = c.Angle, IsValid = c.IsValid, OriginalRadius = c.OriginalRadius }),
                    LinearCuts = p.LinearCuts.ConvertAll(c => new TurningLinearCut { StartZ = c.StartZ, EndZ = c.EndZ, StartRadius = c.StartRadius, EndRadius = c.EndRadius }) } } };
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(InstanceId) || string.IsNullOrWhiteSpace(MaterialId) ||
                string.IsNullOrWhiteSpace(SourceItemId))
                throw new ArgumentException("Workpiece identity, material and source IDs are required.");
            if (!Enum.IsDefined(typeof(Orientation), Orientation) || Geometry == null || ProcessHistory == null)
                throw new ArgumentException("Invalid workpiece state.");
            Geometry.Validate();
            foreach (var processId in ProcessHistory)
                if (string.IsNullOrWhiteSpace(processId)) throw new ArgumentException("Invalid process ID.");
        }
    }
}
