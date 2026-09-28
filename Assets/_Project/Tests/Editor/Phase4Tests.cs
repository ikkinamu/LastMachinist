using System.Collections.Generic;
using NUnit.Framework;
using LastMachinist.Measurement;
using LastMachinist.Machining.Turning;

namespace LastMachinist.Tests
{
    public sealed class Phase4Tests
    {
        [TestCase(10)] [TestCase(20)] [TestCase(50)]
        public void VernierAlignedMarkRepresentsFraction(int divisions)
        {
            var scale = new VernierScale(divisions);
            for (int index = 0; index < divisions; index++)
            {
                var reading = scale.Read(40f + (float)index / divisions, "old_vernier");
                Assert.That(scale.TickPosition(reading, index), Is.EqualTo(40 + index).Within(0.0001));
            }
        }
        private static TurningProfile Profile()
        {
            return new TurningProfile { TotalLength = 100, Segments = new List<TurningSegment> {
                new TurningSegment { StartZ = 0, EndZ = 30, Radius = 20 },
                new TurningSegment { StartZ = 30, EndZ = 70, Radius = 25 },
                new TurningSegment { StartZ = 70, EndZ = 100, Radius = 20 } } };
        }

        [Test] public void TrueDimensionReadsGeometry() {
            var s = new MeasurementSession(Profile(), "wp");
            Assert.That(s.TrueDimension(new MeasurementPoint("L2_Diameter", MeasurementPointType.Diameter, 30, 70)), Is.EqualTo(50).Within(0.001));
            Assert.That(s.TrueDimension(new MeasurementPoint("L3_Length", MeasurementPointType.Length, 70, 100)), Is.EqualTo(30).Within(0.001));
        }

        [Test] public void DiameterSamplesVisibleContactInsteadOfSmallestRadiusInRegion() {
            var profile = Profile();
            profile.Segments.Insert(0, new TurningSegment { StartZ = 0, EndZ = 5, Radius = 10 });
            profile.Segments[1].StartZ = 5;
            var session = new MeasurementSession(profile, "wp");
            var point = new MeasurementPoint("L1_Diameter", MeasurementPointType.Diameter, 0, 30);
            Assert.That(session.ContactPosition(point), Is.EqualTo(15));
            Assert.That(session.Select(point, "old_vernier", true).Value, Is.EqualTo(40));
        }

        [Test] public void MutatedInvalidPointCannotBeMeasured() {
            var session = new MeasurementSession(Profile(), "wp");
            var point = new MeasurementPoint("L1_Diameter", MeasurementPointType.Diameter, 0, 30);
            point.StartZ = float.NaN;
            Assert.That(() => session.TrueDimension(point), Throws.ArgumentException);
        }

        [Test] public void ReadingAndPlayerRecordStaySeparate() {
            var s = new MeasurementSession(Profile(), "wp");
            var reading = s.Select(new MeasurementPoint("L1_Diameter", MeasurementPointType.Diameter, 0, 30), "old_vernier", true);
            var record = s.Record(41.25f);
            Assert.That(reading.Value, Is.EqualTo(40).Within(0.001));
            Assert.That(record.PlayerValue, Is.EqualTo(41.25f));
            Assert.That(record.InstrumentReadingValue, Is.EqualTo(40).Within(0.001));
        }

        [Test] public void PrecisionMeasurementRequiresStoppedSpindle() {
            var s = new MeasurementSession(Profile(), "wp");
            Assert.That(() => s.Select(new MeasurementPoint("L1_Diameter", MeasurementPointType.Diameter, 0, 30), "old_vernier", false), Throws.InvalidOperationException);
        }
    }
}
