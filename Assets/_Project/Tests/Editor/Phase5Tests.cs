using System.Collections.Generic;
using NUnit.Framework;
using LastMachinist.Evaluation;
using LastMachinist.Machining.Turning;

namespace LastMachinist.Tests
{
    public sealed class Phase5Tests
    {
        private static TurningProfile Profile(float a = 20, float b = 25, float c = 20)
        {
            return new TurningProfile { TotalLength = 100, Segments = new List<TurningSegment> {
                new TurningSegment { StartZ = 0, EndZ = 30, Radius = a },
                new TurningSegment { StartZ = 30, EndZ = 70, Radius = b },
                new TurningSegment { StartZ = 70, EndZ = 100, Radius = c } } };
        }
        private static PartRequirement Requirement() => new PartRequirement { Dimensions = new List<DimensionRequirement> {
            new DimensionRequirement("L1", 0, 30, 40), new DimensionRequirement("L2", 30, 70, 50), new DimensionRequirement("L3", 70, 100, 40) },
            RequiredChamferPositions = new List<float> { 0 } };

        [Test] public void PerfectRequiresGeometryAndChamfer() {
            var p = Profile(); p.AddChamfer(0, 1); Assert.That(PartEvaluator.Evaluate(p, Requirement()).Result, Is.EqualTo(PartEvaluationResult.Perfect));
        }
        [Test] public void FunctionalButRoughPartIsAcceptable() {
            // Radius +0.2 means displayed diameter +0.4, inside the default +/-0.5 tolerance.
            var p = Profile(20.2f, 25.2f, 20.2f); p.AddChamfer(0, 1); Assert.That(PartEvaluator.Evaluate(p, Requirement()).Result, Is.EqualTo(PartEvaluationResult.Acceptable));
        }
        [TestCase(20.25f, PartEvaluationResult.Acceptable)]
        [TestCase(20.26f, PartEvaluationResult.Reworkable)]
        [TestCase(20.4f, PartEvaluationResult.Reworkable)]
        [TestCase(19.75f, PartEvaluationResult.Acceptable)]
        [TestCase(19.74f, PartEvaluationResult.Rejected)]
        public void FunctionalToleranceUsesDiameterNotRadius(float radius, PartEvaluationResult expected)
        {
            var p = Profile(radius); p.AddChamfer(0, 1);
            Assert.That(PartEvaluator.Evaluate(p, Requirement()).Result, Is.EqualTo(expected));
        }
        [Test] public void OversizeCanBeReworked() {
            var p = Profile(21, 25, 20); p.AddChamfer(0, 1); Assert.That(PartEvaluator.Evaluate(p, Requirement()).Result, Is.EqualTo(PartEvaluationResult.Reworkable));
        }
        [Test] public void UndersizeIsRejectedEvenWithRecordedAnswer() {
            var p = Profile(19, 25, 20); p.AddChamfer(0, 1); Assert.That(PartEvaluator.Evaluate(p, Requirement()).Result, Is.EqualTo(PartEvaluationResult.Rejected));
        }
        [Test] public void MissingChamferIsReworkable() {
            Assert.That(PartEvaluator.Evaluate(Profile(), Requirement()).Result, Is.EqualTo(PartEvaluationResult.Reworkable));
        }
        [Test] public void ChamferCutAwayMustBeMachinedAgain() {
            var p = Profile(21, 25, 20); p.AddChamfer(0, 1);
            p.RemoveMaterial(0, 30, 20);
            Assert.That(p.HasVisibleChamfer(0), Is.False);
            Assert.That(PartEvaluator.Evaluate(p, Requirement()).Result, Is.EqualTo(PartEvaluationResult.Reworkable));
            Assert.That(p.AddChamfer(0, 1), Is.True);
            Assert.That(p.RadiusAt(0), Is.EqualTo(19).Within(0.001));
            Assert.That(PartEvaluator.Evaluate(p, Requirement()).Result, Is.EqualTo(PartEvaluationResult.Perfect));
        }
        [Test] public void CuttingOnlySmallPatchDoesNotPassEntireRegion() {
            var p = Profile(25, 25, 20); p.RemoveMaterial(0, 1, 20); p.AddChamfer(0, 1);
            Assert.That(PartEvaluator.Evaluate(p, Requirement()).Result, Is.EqualTo(PartEvaluationResult.Reworkable));
        }
        [Test] public void SmallUndersizeWithinFunctionalToleranceIsAcceptable() {
            var p = Profile(19.75f, 25, 20); p.AddChamfer(0, 1);
            Assert.That(PartEvaluator.Evaluate(p, Requirement()).Result, Is.EqualTo(PartEvaluationResult.Acceptable));
        }
        [Test] public void RemainingMaterialIsReworkableEvenAboveOldOversizeLimit() {
            var p = Profile(30, 30, 30); p.AddChamfer(0, 1);
            Assert.That(PartEvaluator.Evaluate(p, Requirement()).Result, Is.EqualTo(PartEvaluationResult.Reworkable));
        }
        [Test] public void LocalUndercutOverridesRemainingOversize() {
            var p = Profile(25, 25, 20); p.RemoveMaterial(10, 11, 18); p.AddChamfer(0, 1);
            Assert.That(PartEvaluator.Evaluate(p, Requirement()).Result, Is.EqualTo(PartEvaluationResult.Rejected));
        }
    }
}
