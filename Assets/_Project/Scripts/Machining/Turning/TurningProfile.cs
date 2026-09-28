using System;
using System.Collections.Generic;

namespace LastMachinist.Machining.Turning
{
    [Serializable]
    public sealed class ChamferFeature
    {
        public float Position;
        public float Width;
        public float Angle = 45f;
        public bool IsValid;
        public float OriginalRadius;
    }

    [Serializable]
    public sealed class TurningLinearCut
    {
        public float StartZ, EndZ, StartRadius, EndRadius;
    }

    public readonly struct TurningSurfaceSpan
    {
        public readonly float StartZ, EndZ, StartRadius, EndRadius;
        public TurningSurfaceSpan(float start, float end, float startRadius, float endRadius)
        { StartZ = start; EndZ = end; StartRadius = startRadius; EndRadius = endRadius; }
    }

    [Serializable]
    public sealed class TurningSegment
    {
        public float StartZ;
        public float EndZ;
        public float Radius;

        public float Diameter => Radius * 2f;
    }

    [Serializable]
    public sealed class TurningProfile
    {
        // Millimetres in workpiece-local coordinates. The controller authorizes cutting.
        public float TotalLength;
        public List<TurningSegment> Segments = new List<TurningSegment>();
        public List<ChamferFeature> Chamfers = new List<ChamferFeature>();
        // Finite swept cutting edges. Older saves have no entries and remain valid.
        public List<TurningLinearCut> LinearCuts = new List<TurningLinearCut>();
        public HoleProfile InnerProfile = new HoleProfile();
        public const float MinimumWall = 0.2f;
        [field: NonSerialized] public event Action Changed;

        // TotalLength is the coordinate domain; facing can leave empty spans at either end.
        public void GetMaterialBounds(out float start, out float end)
        {
            start = end = 0;
            bool found = false;
            foreach (var span in SurfaceSpans())
            {
                if (span.StartRadius <= 0 && span.EndRadius <= 0) continue;
                if (!found) { start = span.StartZ; found = true; }
                end = span.EndZ;
            }
        }

        public float GetAxialLength()
        {
            GetMaterialBounds(out float start, out float end);
            return end - start;
        }

        public float InnerTrueDiameter(float z)
        {
            if (!Finite(z) || z < 0 || z > TotalLength) throw new ArgumentOutOfRangeException(nameof(z));
            return InnerProfile?.TrueDiameterAt(z) ?? 0;
        }
        public float MinimumOuterRadius(float from, float to)
        {
            float result = float.MaxValue;
            foreach (var s in Segments)
            {
                float a = Math.Max(from, s.StartZ), b = Math.Min(to, s.EndZ);
                if (b <= a) continue;
                result = Math.Min(result, Math.Min(ChamferRadius(a,s.Radius), ChamferRadius(b,s.Radius)));
                if (LinearCuts != null) foreach (var c in LinearCuts)
                {
                    float ca=Math.Max(a,c.StartZ), cb=Math.Min(b,c.EndZ);
                    if (cb <= ca) continue;
                    float At(float z) => c.StartRadius+(c.EndRadius-c.StartRadius)*(z-c.StartZ)/(c.EndZ-c.StartZ);
                    result = Math.Min(result, Math.Min(At(ca),At(cb)));
                }
            }
            return result == float.MaxValue ? 0 : result;
        }
        public bool EnlargeHole(float from, float to, float radius, string toolId, bool left)
        {
            Validate();
            if (!Finite(from) || !Finite(to) || !Finite(radius) || radius <= 0 || string.IsNullOrEmpty(toolId)) throw new ArgumentException("Invalid internal cut.");
            from=Math.Max(0,from); to=Math.Min(TotalLength,to);
            if (to <= from || radius + MinimumWall > MinimumOuterRadius(from,to)) return false;
            if (!InnerProfile.Enlarge(from,to,radius)) return false;
            InnerProfile.OperationHistory.Add(new HoleOperation { ToolId=toolId, Position=left?0:TotalLength, Diameter=radius*2, Depth=left?to:TotalLength-from });
            Changed?.Invoke(); return true;
        }

        public bool RemoveMaterial(float startZ, float endZ, float radius)
        {
            Validate();
            if (!Finite(startZ) || !Finite(endZ) || !Finite(radius) || radius < 0 || endZ < startZ)
                throw new ArgumentException("Invalid removal interval or radius.");
            startZ = Math.Max(0, startZ);
            endZ = Math.Min(TotalLength, endZ);
            if (endZ <= startZ) return false;
            float inner = InnerProfile.MaximumRadius(startZ,endZ);
            if (inner > 0) radius = Math.Max(radius, inner + MinimumWall);
            var next = new List<TurningSegment>();
            bool changed = false;
            foreach (var segment in Segments)
            {
                float from = Math.Max(startZ, segment.StartZ);
                float to = Math.Min(endZ, segment.EndZ);
                if (to <= from || radius >= segment.Radius)
                {
                    Append(next, segment.StartZ, segment.EndZ, segment.Radius);
                    continue;
                }
                Append(next, segment.StartZ, from, segment.Radius);
                Append(next, from, to, radius);
                Append(next, to, segment.EndZ, segment.Radius);
                changed = true;
            }
            if (!changed) return false;
            // Anchor older serialized features before replacing their base cylinders.
            foreach (var chamfer in Chamfers)
                if (chamfer.OriginalRadius == 0)
                    chamfer.OriginalRadius = chamfer.Position == 0 ? Segments[0].Radius : Segments[Segments.Count - 1].Radius;
            Segments = next;
            Changed?.Invoke();
            return true;
        }

        public bool RemoveLinearMaterial(float start, float end, float startRadius, float endRadius)
        {
            Validate();
            if (!Finite(start) || !Finite(end) || !Finite(startRadius) || !Finite(endRadius) ||
                end <= start || startRadius < 0 || endRadius < 0) throw new ArgumentException("Invalid cutting edge.");
            float from = Math.Max(0, start), to = Math.Min(TotalLength, end);
            if (to <= from) return false;
            float inner = InnerProfile.MaximumRadius(from,to);
            if (inner > 0) { startRadius = Math.Max(startRadius,inner+MinimumWall); endRadius = Math.Max(endRadius,inner+MinimumWall); }
            float At(float z) => startRadius + (endRadius - startRadius) * ((z - start) / (end - start));
            bool removes = false;
            foreach (var span in SurfaceSpans())
            {
                float a = Math.Max(from, span.StartZ), b = Math.Min(to, span.EndZ);
                if (b <= a) continue;
                float slope = (span.EndRadius - span.StartRadius) / (span.EndZ - span.StartZ);
                if (At(a) < span.StartRadius + slope * (a - span.StartZ) - 0.00001f ||
                    At(b) < span.StartRadius + slope * (b - span.StartZ) - 0.00001f) { removes = true; break; }
            }
            if (!removes) return false;
            if (LinearCuts == null) LinearCuts = new List<TurningLinearCut>();
            // Discard only cuts wholly covered by this deeper cut; never restore material.
            LinearCuts.RemoveAll(c => from <= c.StartZ && to >= c.EndZ && At(c.StartZ) <= c.StartRadius && At(c.EndZ) <= c.EndRadius);
            LinearCuts.Add(new TurningLinearCut { StartZ = from, EndZ = to, StartRadius = At(from), EndRadius = At(to) });
            Changed?.Invoke();
            return true;
        }

        private static void Append(List<TurningSegment> list, float from, float to, float radius)
        {
            if (to <= from) return;
            if (list.Count > 0 && list[list.Count - 1].Radius == radius && list[list.Count - 1].EndZ == from)
                list[list.Count - 1].EndZ = to;
            else list.Add(new TurningSegment { StartZ = from, EndZ = to, Radius = radius });
        }

        public void Validate()
        {
            if (!Finite(TotalLength) || TotalLength <= 0 || Segments == null || Segments.Count == 0 || Chamfers == null)
                throw new ArgumentException("A profile requires positive length and segments.");
            float expectedStart = 0;
            foreach (var segment in Segments)
            {
                if (segment == null || !Finite(segment.StartZ) || !Finite(segment.EndZ) ||
                    !Finite(segment.Radius) || segment.Radius < 0 ||
                    segment.StartZ != expectedStart || segment.EndZ <= segment.StartZ)
                    throw new ArgumentException("Segments must be finite, ordered and contiguous from zero.");
                expectedStart = segment.EndZ;
            }
            if (expectedStart != TotalLength)
                throw new ArgumentException("Segments must cover the total length exactly.");
            InnerProfile ??= new HoleProfile();
            InnerProfile.Validate(TotalLength);
            if (LinearCuts != null)
                foreach (var cut in LinearCuts)
                    if (cut == null || !Finite(cut.StartZ) || !Finite(cut.EndZ) || !Finite(cut.StartRadius) || !Finite(cut.EndRadius) ||
                        cut.StartZ < 0 || cut.EndZ > TotalLength || cut.EndZ <= cut.StartZ || cut.StartRadius < 0 || cut.EndRadius < 0)
                        throw new ArgumentException("Invalid saved cutting edge.");
            foreach (var chamfer in Chamfers)
                if (chamfer == null || !Finite(chamfer.Position) || !Finite(chamfer.Width) || !Finite(chamfer.Angle) ||
                    chamfer.Width <= 0 || chamfer.Position < 0 || chamfer.Position > TotalLength ||
                    (chamfer.Position != 0 && chamfer.Position != TotalLength) || !chamfer.IsValid ||
                    chamfer.Angle <= 0 || chamfer.Angle >= 90 || !Finite(chamfer.OriginalRadius) || chamfer.OriginalRadius < 0)
                    throw new ArgumentException("Chamfers must be valid end features.");
            foreach (var hole in InnerProfile.Segments)
                if (hole.Radius + MinimumWall > MinimumOuterRadius(hole.StartZ,hole.EndZ) + 0.00001f)
                    throw new ArgumentException("Inner and outer profiles intersect.");
        }

        public bool AddChamfer(float position, float width, float angle = 45f)
        {
            Validate();
            if (!Finite(position) || !Finite(width) || !Finite(angle) || width <= 0 ||
                (position != 0 && position != TotalLength) || angle <= 0 || angle >= 90)
                throw new ArgumentException("Chamfer must be placed at a profile end.");
            if (HasVisibleChamfer(position)) return false;
            float radius = position == 0 ? Segments[0].Radius : Segments[Segments.Count - 1].Radius;
            if (width > TotalLength / 2 || width * Math.Tan(angle * Math.PI / 180) >= radius)
                throw new ArgumentException("Chamfer exceeds the available end material.");
            float inner = InnerProfile.MaximumRadius(position == 0 ? 0 : TotalLength-width, position == 0 ? width : TotalLength);
            if (inner > 0 && radius-width*(float)Math.Tan(angle*Math.PI/180) < inner+MinimumWall)
                return false;
            Chamfers.RemoveAll(c => c.Position == position);
            Chamfers.Add(new ChamferFeature { Position = position, Width = width, Angle = angle, IsValid = true, OriginalRadius = radius });
            Changed?.Invoke();
            return true;
        }

        public bool HasVisibleChamfer(float position)
        {
            foreach (var span in SurfaceSpans())
            {
                bool inFeature = position == 0 ? span.StartZ == 0 : span.EndZ == TotalLength;
                float slope = span.EndRadius - span.StartRadius;
                if (inFeature && (position == 0 ? slope > 0.0001f : slope < -0.0001f)) return true;
            }
            return false;
        }

        private float ChamferRadius(float z, float radius)
        {
            foreach (var c in Chamfers)
            {
                float distance = Math.Abs(z - c.Position);
                if (distance >= c.Width) continue;
                float original = c.OriginalRadius > 0 ? c.OriginalRadius :
                    c.Position == 0 ? Segments[0].Radius : Segments[Segments.Count - 1].Radius;
                radius = Math.Min(radius, Math.Max(0, original - (c.Width - distance) * (float)Math.Tan(c.Angle * Math.PI / 180)));
            }
            return radius;
        }

        public float RadiusAt(float z)
        {
            if (!Finite(z) || z < 0 || z > TotalLength) throw new ArgumentOutOfRangeException(nameof(z));
            foreach (var s in Segments)
                if (z >= s.StartZ && (z < s.EndZ || z == TotalLength))
                {
                    float radius = ChamferRadius(z, s.Radius);
                    if (LinearCuts != null)
                        foreach (var c in LinearCuts)
                            if (z >= c.StartZ && z <= c.EndZ)
                                radius = Math.Min(radius, c.StartRadius + (c.EndRadius - c.StartRadius) * (z - c.StartZ) / (c.EndZ - c.StartZ));
                    return radius;
                }
            throw new ArgumentException("No geometry at coordinate.");
        }

        public List<TurningSurfaceSpan> SurfaceSpans()
        {
            Validate();
            var result = new List<TurningSurfaceSpan>();
            foreach (var s in Segments)
            {
                var knots = new List<float> { s.StartZ, s.EndZ };
                foreach (var c in Chamfers)
                {
                    float original = c.OriginalRadius > 0 ? c.OriginalRadius :
                        c.Position == 0 ? Segments[0].Radius : Segments[Segments.Count - 1].Radius;
                    float distance = c.Width - (original - s.Radius) / (float)Math.Tan(c.Angle * Math.PI / 180);
                    float crossing = c.Position == 0 ? distance : TotalLength - distance;
                    float boundary = c.Position == 0 ? c.Width : TotalLength - c.Width;
                    if (crossing > s.StartZ && crossing < s.EndZ && distance >= 0 && distance <= c.Width) knots.Add(crossing);
                    if (boundary > s.StartZ && boundary < s.EndZ) knots.Add(boundary);
                }
                knots.Sort();
                for (int i = 1; i < knots.Count; i++)
                    if (knots[i] > knots[i - 1]) result.Add(new TurningSurfaceSpan(knots[i - 1], knots[i],
                        ChamferRadius(knots[i - 1], s.Radius), ChamferRadius(knots[i], s.Radius)));
            }
            if (LinearCuts != null)
                foreach (var cut in LinearCuts)
                {
                    var next = new List<TurningSurfaceSpan>();
                    foreach (var span in result)
                    {
                        var knots = new List<float> { span.StartZ, span.EndZ };
                        if (cut.StartZ > span.StartZ && cut.StartZ < span.EndZ) knots.Add(cut.StartZ);
                        if (cut.EndZ > span.StartZ && cut.EndZ < span.EndZ) knots.Add(cut.EndZ);
                        float slope = (span.EndRadius - span.StartRadius) / (span.EndZ - span.StartZ);
                        float cutSlope = (cut.EndRadius - cut.StartRadius) / (cut.EndZ - cut.StartZ);
                        float Surface(float z) => span.StartRadius + slope * (z - span.StartZ);
                        float Edge(float z) => cut.StartRadius + cutSlope * (z - cut.StartZ);
                        if (Math.Abs(slope - cutSlope) > 0.000001f)
                        {
                            float crossing = span.StartZ + (Edge(span.StartZ) - span.StartRadius) / (slope - cutSlope);
                            if (crossing > Math.Max(span.StartZ, cut.StartZ) && crossing < Math.Min(span.EndZ, cut.EndZ)) knots.Add(crossing);
                        }
                        knots.Sort();
                        for (int i = 1; i < knots.Count; i++)
                        {
                            float a = knots[i - 1], b = knots[i];
                            if (b <= a) continue;
                            bool inside = (a + b) / 2 >= cut.StartZ && (a + b) / 2 <= cut.EndZ;
                            next.Add(new TurningSurfaceSpan(a, b, inside ? Math.Min(Surface(a), Edge(a)) : Surface(a),
                                inside ? Math.Min(Surface(b), Edge(b)) : Surface(b)));
                        }
                    }
                    result = next;
                }
            return result;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
