using System.Collections.Generic;
using LastMachinist.Measurement;
using LastMachinist.Machining.Turning;
using LastMachinist.Evaluation;
using NUnit.Framework;

namespace LastMachinist.Tests
{
    public class V09Tests
    {
        [Test] public void LengthFollowsCurrentShoulderWithoutChangingManualRecord()
        {
            var profile = new TurningProfile { TotalLength = 100, Segments = new List<TurningSegment> {
                new TurningSegment { StartZ=0, EndZ=30, Radius=20 },
                new TurningSegment { StartZ=30, EndZ=70, Radius=25 },
                new TurningSegment { StartZ=70, EndZ=100, Radius=20 } } };
            var session = new MeasurementSession(profile, "test");
            var point = new MeasurementPoint("L1_Length", MeasurementPointType.Length, 0, 30);
            session.Select(point, "vernier", true); session.Record(123);
            profile.Segments[0].EndZ = 27.6f; profile.Segments[1].StartZ = 27.6f;
            Assert.That(session.TrueDimension(point), Is.EqualTo(27.6f).Within(0.001));
            Assert.That(session.Select(point, "vernier", true).Value, Is.EqualTo(27.6f).Within(0.001));
            Assert.That(session.Records[point.Id].PlayerValue, Is.EqualTo(123));
        }
        [Test] public void TutorialToleranceAllowsShiftedShoulderAndDiameter()
        {
            var p = new TurningProfile { TotalLength=100, Segments=new List<TurningSegment> {
                new TurningSegment { StartZ=0, EndZ=29.5f, Radius=19.6f },
                new TurningSegment { StartZ=29.5f, EndZ=70, Radius=25.4f },
                new TurningSegment { StartZ=70, EndZ=100, Radius=20.4f } } };
            var req = new PartRequirement { PerfectTolerance=0.25f, FunctionalTolerance=1, AssessCurrentFeatureLengths=true,
                Dimensions=new List<DimensionRequirement> { new DimensionRequirement("L1",0,30,40), new DimensionRequirement("L2",30,70,50), new DimensionRequirement("L3",70,100,40) } };
            Assert.That(PartEvaluator.Evaluate(p,req).Result, Is.EqualTo(PartEvaluationResult.Acceptable));
            req.FunctionalTolerance = new PartRequirement().FunctionalTolerance;
            Assert.That(PartEvaluator.Evaluate(p,req).Result, Is.EqualTo(PartEvaluationResult.Rejected));
        }
    }
}
