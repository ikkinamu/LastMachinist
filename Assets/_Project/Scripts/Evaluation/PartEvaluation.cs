using System;
using System.Collections.Generic;
using LastMachinist.Machining.Turning;
using LastMachinist.Measurement;

namespace LastMachinist.Evaluation
{
    public enum PartEvaluationResult { Perfect, Acceptable, Reworkable, Rejected }

    [Serializable]
    public sealed class DimensionRequirement
    {
        public string Id;
        public float StartZ;
        public float EndZ;
        public float TargetDiameter;
        public DimensionRequirement(string id, float startZ, float endZ, float targetDiameter)
        {
            if (string.IsNullOrWhiteSpace(id) || startZ < 0 || endZ <= startZ || targetDiameter <= 0) throw new ArgumentException("Invalid dimension requirement.");
            Id = id; StartZ = startZ; EndZ = endZ; TargetDiameter = targetDiameter;
        }
    }

    [Serializable]
    public sealed class PartRequirement
    {
        public float PerfectTolerance = 0.1f;
        public float FunctionalTolerance = 0.5f;
        public bool AssessCurrentFeatureLengths;
        public float RequiredTotalLength;
        // Legacy serialized field retained for compatibility. Excess stock alone is recoverable.
        public float MaxRecoverableOversize = 8f;
        public List<DimensionRequirement> Dimensions = new List<DimensionRequirement>();
        public List<float> RequiredChamferPositions = new List<float>();
    }

    public sealed class PartEvaluation
    {
        public PartEvaluationResult Result { get; }
        public bool CanRework => Result == PartEvaluationResult.Reworkable;
        public string Reason { get; }
        public PartEvaluation(PartEvaluationResult result, string reason) { Result = result; Reason = reason; }
    }

    public static class PartEvaluator
    {
        public static PartEvaluation Evaluate(TurningProfile profile, PartRequirement requirement)
        {
            if (profile == null || requirement == null || requirement.Dimensions == null) throw new ArgumentNullException();
            profile.Validate();
            if (!Finite(requirement.PerfectTolerance) || !Finite(requirement.FunctionalTolerance) ||
                requirement.PerfectTolerance < 0 || requirement.FunctionalTolerance < requirement.PerfectTolerance || requirement.Dimensions.Count == 0)
                throw new ArgumentException("Invalid evaluation tolerances.");
            bool outsideFunctional = false, oversized = false, excessLength = false;
            if (!Finite(requirement.RequiredTotalLength) || requirement.RequiredTotalLength<0) throw new ArgumentException("Invalid total length.");
            if(requirement.RequiredTotalLength>0)
            {
                LastMachinist.Inventory.WorkpieceSplitter.MaterialBounds(profile,out float start,out float end);
                float error=end-start-requirement.RequiredTotalLength;
                if(error < -requirement.FunctionalTolerance) return new PartEvaluation(PartEvaluationResult.Rejected,"완성품의 전체 길이가 부족합니다.");
                if(error > requirement.FunctionalTolerance) { oversized=true; excessLength=true; }
                outsideFunctional=Math.Abs(error)>requirement.PerfectTolerance;
            }
            foreach (var req in requirement.Dimensions)
            {
                if (req == null || !Finite(req.StartZ) || !Finite(req.EndZ) || !Finite(req.TargetDiameter) ||
                    req.StartZ < 0 || req.EndZ <= req.StartZ || req.TargetDiameter <= 0)
                    throw new ArgumentException("Invalid dimension requirement.");
                if (req.EndZ > profile.TotalLength && (!requirement.AssessCurrentFeatureLengths || req.EndZ - profile.TotalLength > requirement.FunctionalTolerance))
                    return new PartEvaluation(PartEvaluationResult.Rejected, "필요한 길이의 재료가 부족합니다.");
                float regionStart = req.StartZ, regionEnd = req.EndZ;
                if (requirement.AssessCurrentFeatureLengths)
                {
                    var session = new MeasurementSession(profile, "evaluation");
                    var point = new MeasurementPoint(req.Id, MeasurementPointType.Length, req.StartZ, Math.Min(req.EndZ, profile.TotalLength));
                    session.MaterialBounds(point, out regionStart, out regionEnd);
                    float lengthError = (regionEnd - regionStart) - (req.EndZ - req.StartZ);
                    if (Math.Abs(lengthError) > requirement.FunctionalTolerance) oversized = true;
                    if (Math.Abs(lengthError) > requirement.PerfectTolerance) outsideFunctional = true;
                }
                foreach (var segment in profile.Segments)
                {
                    if (segment.EndZ <= regionStart || segment.StartZ >= regionEnd) continue;
                    float error = segment.Diameter - req.TargetDiameter;
                    if (error < -requirement.FunctionalTolerance)
                        return new PartEvaluation(PartEvaluationResult.Rejected, req.Id + " 구간이 과가공되었습니다.");
                    if (error > requirement.FunctionalTolerance) oversized = true;
                    if (Math.Abs(error) > requirement.PerfectTolerance) outsideFunctional = true;
                }
            }
            bool missingChamfer = false;
            if (requirement.RequiredChamferPositions != null)
                foreach (var position in requirement.RequiredChamferPositions)
                    if (!profile.HasVisibleChamfer(position)) missingChamfer = true;
            if (oversized) return new PartEvaluation(PartEvaluationResult.Reworkable,
                (excessLength ? "절단 바이트로 잔재를 분리하고 완성품 전체 길이를 다시 측정하세요." : "직경 또는 길이가 허용 범위를 벗어났습니다. 다시 측정하세요.") + (missingChamfer ? "\n필요한 끝단 모따기도 완료되지 않았습니다." : ""));
            if (missingChamfer) return new PartEvaluation(PartEvaluationResult.Reworkable,
                "직경과 길이는 합격입니다. 필요한 끝단 모따기가 아직 완료되지 않았습니다.\n추가 작업으로 돌아가 모따기를 마친 뒤 다시 검사하세요.");
            return new PartEvaluation(outsideFunctional ? PartEvaluationResult.Acceptable : PartEvaluationResult.Perfect,
                outsideFunctional ? "기능 허용 범위 안의 치수입니다." : "모든 치수와 모따기가 목표에 맞습니다.");
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    }
}
