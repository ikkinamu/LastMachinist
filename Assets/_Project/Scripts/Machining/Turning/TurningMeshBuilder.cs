using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastMachinist.Machining.Turning
{
    public static class TurningMeshBuilder
    {
        // Mesh X maps to workpiece Z; mesh Y/Z form the radial plane.
        public static void Rebuild(Mesh mesh, TurningProfile profile, int radialSteps = 48, float unitsPerMm = 0.05f)
        {
            profile.Validate();
            radialSteps = Mathf.Clamp(radialSteps, 8, 128);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Surface(float za, float ra, float zb, float rb)
            {
                if (ra == 0 && rb == 0) return;
                for (int i = 0; i < radialSteps; i++)
                {
                    float a = i * 2 * Mathf.PI / radialSteps, b = (i + 1) * 2 * Mathf.PI / radialSteps;
                    int n = vertices.Count;
                    vertices.Add(new Vector3(za - profile.TotalLength / 2, ra * Mathf.Cos(a), ra * Mathf.Sin(a)) * unitsPerMm);
                    vertices.Add(new Vector3(za - profile.TotalLength / 2, ra * Mathf.Cos(b), ra * Mathf.Sin(b)) * unitsPerMm);
                    vertices.Add(new Vector3(zb - profile.TotalLength / 2, rb * Mathf.Cos(b), rb * Mathf.Sin(b)) * unitsPerMm);
                    vertices.Add(new Vector3(zb - profile.TotalLength / 2, rb * Mathf.Cos(a), rb * Mathf.Sin(a)) * unitsPerMm);
                    triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
                }
            }
            var spans = profile.SurfaceSpans();
            Surface(0, profile.InnerProfile.RadiusAt(0), 0, spans[0].StartRadius);
            for (int i = 0; i < spans.Count; i++)
            {
                var segment = spans[i];
                Surface(segment.StartZ, segment.StartRadius, segment.EndZ, segment.EndRadius);
                float nextRadius = i + 1 < spans.Count ? spans[i + 1].StartRadius : profile.InnerProfile.RadiusAt(profile.TotalLength);
                if (nextRadius != segment.EndRadius) Surface(segment.EndZ, segment.EndRadius, segment.EndZ, nextRadius);
            }
            // Reversed winding makes the bore walls visible from inside. Blind bottoms and
            // internal steps are rings; through holes never receive a solid end cap.
            var holes = profile.InnerProfile.Segments;
            for (int i=0;i<holes.Count;i++)
            {
                var h=holes[i];
                Surface(h.EndZ,h.Radius,h.StartZ,h.Radius);
                float previous=i>0 && holes[i-1].EndZ==h.StartZ ? holes[i-1].Radius : 0;
                if (h.StartZ>0 && previous!=h.Radius) Surface(h.StartZ,h.Radius,h.StartZ,previous);
                if (h.EndZ<profile.TotalLength && (i+1==holes.Count || holes[i+1].StartZ!=h.EndZ)) Surface(h.EndZ,0,h.EndZ,h.Radius);
            }
            mesh.Clear();
            mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }
    }
}
