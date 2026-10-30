using System;
using System.Collections.Generic;
using LastMachinist.Machining.Turning;
using UnityEngine;

namespace LastMachinist.Machining.Core
{
    public enum Orientation { Normal, Reversed }
    public enum GeometryType { RoundProfile, SquareBar, RectangularBar, Plate, Tube }

    [Serializable]
    public sealed class GeneralWorkpieceGeometry
    {
        public GeometryType Shape;
        public float Width, Height, Length, Thickness, OuterDiameter;
        // Tube uses wall thickness as its source of truth; InnerDiameter is derived.
        public float InnerDiameter => Mathf.Max(0, OuterDiameter - Thickness * 2);
        public void Validate()
        {
            foreach (float value in new[] { Width, Height, Length, Thickness, OuterDiameter })
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Non-finite geometry.");
            if (!Enum.IsDefined(typeof(GeometryType), Shape) || Shape == GeometryType.RoundProfile || Length <= 0 || Width <= 0)
                throw new ArgumentException("Invalid general geometry.");
            if ((Shape == GeometryType.SquareBar || Shape == GeometryType.RectangularBar) && Height <= 0)
                throw new ArgumentException("Bar height is required.");
            if (Shape == GeometryType.Plate && Thickness <= 0) throw new ArgumentException("Plate thickness is required.");
            if (Shape == GeometryType.Tube && (OuterDiameter <= 0 || Thickness <= 0 || InnerDiameter <= 0)) throw new ArgumentException("Tube dimensions are invalid.");
        }
    }

    [Serializable]
    public sealed class HoleFeature
    {
        public string FeatureId;
        public float CenterX, CenterY, Diameter, Depth;
        public bool IsThrough;
        public string ToolId;
        public List<string> OperationHistory = new List<string>();
        public void Validate()
        {
            foreach (float value in new[] { CenterX, CenterY, Diameter, Depth })
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Non-finite hole.");
            if (string.IsNullOrWhiteSpace(FeatureId) || Diameter <= 0 || Depth <= 0 || string.IsNullOrWhiteSpace(ToolId) || OperationHistory == null)
                throw new ArgumentException("Invalid hole feature.");
        }
        public HoleFeature Copy() => new HoleFeature { FeatureId = FeatureId, CenterX = CenterX, CenterY = CenterY,
            Diameter = Diameter, Depth = Depth, IsThrough = IsThrough, ToolId = ToolId, OperationHistory = new List<string>(OperationHistory) };
    }

    // Tagged geometry payload: add another kind only when a later process needs it.
    [Serializable]
    public sealed class WorkpieceGeometry
    {
        public GeometryType Type;
        public TurningProfile RoundProfile;
        public GeneralWorkpieceGeometry General;
        public List<HoleFeature> HoleFeatures = new List<HoleFeature>();

        public void Validate()
        {
            if (Type == GeometryType.RoundProfile && RoundProfile == null)
                throw new ArgumentException("Unsupported or missing geometry payload.");
            if (Type == GeometryType.RoundProfile) RoundProfile.Validate();
            else if (General == null) throw new ArgumentException("Missing general geometry payload.");
            else { General.Validate(); if (General.Shape != Type) throw new ArgumentException("Mismatched geometry tag."); }
            HoleFeatures ??= new List<HoleFeature>(); // Saves predating bench drilling have no features.
            var ids = new HashSet<string>();
            foreach (var hole in HoleFeatures) { if (hole == null || !ids.Add(hole.FeatureId)) throw new ArgumentException("Duplicate hole feature."); hole.Validate(); }
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
            if (Geometry.Type != GeometryType.RoundProfile)
                return new WorkpieceState { InstanceId = InstanceId, MaterialId = MaterialId, SourceItemId = SourceItemId,
                    Orientation = Orientation, IsFinished = IsFinished, ProcessHistory = new List<string>(ProcessHistory),
                    Geometry = new WorkpieceGeometry { Type = Geometry.Type,
                        General = new GeneralWorkpieceGeometry { Shape = Geometry.General.Shape, Width = Geometry.General.Width, Height = Geometry.General.Height, Length = Geometry.General.Length, Thickness = Geometry.General.Thickness, OuterDiameter = Geometry.General.OuterDiameter },
                        HoleFeatures = Geometry.HoleFeatures.ConvertAll(h => h.Copy()) } };
            var p = Geometry.RoundProfile;
            return new WorkpieceState { InstanceId = InstanceId, MaterialId = MaterialId, SourceItemId = SourceItemId,
                Orientation = Orientation, IsFinished = IsFinished, ProcessHistory = new List<string>(ProcessHistory),
                Geometry = new WorkpieceGeometry { Type = Geometry.Type, RoundProfile = new TurningProfile {
                    TotalLength = p.TotalLength,
                    InnerProfile = p.InnerProfile.Copy(),
                    Segments = p.Segments.ConvertAll(s => new TurningSegment { StartZ = s.StartZ, EndZ = s.EndZ, Radius = s.Radius }),
                    Chamfers = p.Chamfers.ConvertAll(c => new ChamferFeature { Position = c.Position, Width = c.Width, Angle = c.Angle, IsValid = c.IsValid, OriginalRadius = c.OriginalRadius }),
                    LinearCuts = p.LinearCuts.ConvertAll(c => new TurningLinearCut { StartZ = c.StartZ, EndZ = c.EndZ, StartRadius = c.StartRadius, EndRadius = c.EndRadius }) },
                    General = Geometry.General == null ? null : new GeneralWorkpieceGeometry { Shape = Geometry.General.Shape, Width = Geometry.General.Width, Height = Geometry.General.Height, Length = Geometry.General.Length, Thickness = Geometry.General.Thickness, OuterDiameter = Geometry.General.OuterDiameter },
                    HoleFeatures = Geometry.HoleFeatures.ConvertAll(h => h.Copy()) } };
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
