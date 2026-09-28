using System;
using System.Collections.Generic;
using System.Linq;

namespace LastMachinist.Machining.Turning
{
    [Serializable] public sealed class HoleSegment
    {
        public float StartZ, EndZ, Radius;
    }
    [Serializable] public sealed class HoleOperation
    {
        public string ToolId;
        public float Position, Diameter, Depth;
    }
    [Serializable] public sealed class HoleProfile
    {
        public List<HoleSegment> Segments = new List<HoleSegment>();
        public List<HoleOperation> OperationHistory = new List<HoleOperation>();
        public bool CenteredLeft, CenteredRight;
        public HoleProfile Copy() => new HoleProfile { CenteredLeft = CenteredLeft, CenteredRight = CenteredRight,
            Segments = Segments.ConvertAll(s => new HoleSegment { StartZ = s.StartZ, EndZ = s.EndZ, Radius = s.Radius }),
            OperationHistory = OperationHistory.ConvertAll(o => new HoleOperation { ToolId = o.ToolId, Position = o.Position, Diameter = o.Diameter, Depth = o.Depth }) };
        public float RadiusAt(float z) => Segments.Where(s => z >= s.StartZ && z <= s.EndZ).Select(s => s.Radius).DefaultIfEmpty(0).Max();
        public float TrueDiameterAt(float z)
        { if (float.IsNaN(z) || float.IsInfinity(z) || z < 0) throw new ArgumentOutOfRangeException(nameof(z)); return 2 * RadiusAt(z); }
        public float MaximumRadius(float from, float to) => Segments.Where(s => s.EndZ > from && s.StartZ < to).Select(s => s.Radius).DefaultIfEmpty(0).Max();
        public float DepthFromFace(float length, bool left)
        {
            float z = left ? 0 : length;
            foreach (var s in left ? Segments : Segments.AsEnumerable().Reverse())
            {
                if (left ? s.StartZ != z : s.EndZ != z) break;
                z = left ? s.EndZ : s.StartZ;
            }
            return left ? z : length - z;
        }
        public bool Enlarge(float from, float to, float radius)
        {
            if (to <= from || radius <= 0) return false;
            var knots = Segments.SelectMany(s => new[]{s.StartZ, s.EndZ}).Concat(new[]{from,to}).Distinct().OrderBy(z => z).ToArray();
            var next = new List<HoleSegment>(); bool changed = false;
            for (int i = 1; i < knots.Length; i++)
            {
                float a = knots[i-1], b = knots[i], mid = (a+b)/2;
                float old = RadiusAt(mid), value = mid > from && mid < to ? Math.Max(old,radius) : old;
                changed |= value > old;
                if (value == 0) continue;
                if (next.Count > 0 && next.Last().EndZ == a && next.Last().Radius == value) next.Last().EndZ = b;
                else next.Add(new HoleSegment { StartZ = a, EndZ = b, Radius = value });
            }
            if (changed) Segments = next;
            return changed;
        }
        public void Validate(float length)
        {
            if (Segments == null || OperationHistory == null) throw new ArgumentException("Missing hole data.");
            float previous = 0;
            foreach (var s in Segments)
            {
                if (s == null || !Finite(s.StartZ) || !Finite(s.EndZ) || !Finite(s.Radius) || s.StartZ < previous || s.EndZ <= s.StartZ || s.EndZ > length || s.Radius <= 0)
                    throw new ArgumentException("Invalid internal profile.");
                previous = s.EndZ;
            }
            foreach (var o in OperationHistory)
                if (o == null || string.IsNullOrEmpty(o.ToolId) || !Finite(o.Position) || (o.Position != 0 && o.Position != length) ||
                    !Finite(o.Diameter) || !Finite(o.Depth) || o.Diameter <= 0 || o.Depth <= 0 || o.Depth > length)
                    throw new ArgumentException("Invalid hole operation.");
        }
        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    }
}
