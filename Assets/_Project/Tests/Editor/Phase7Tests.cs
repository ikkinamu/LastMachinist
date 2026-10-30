using System.Collections.Generic;
using NUnit.Framework;
using LastMachinist.Evaluation;
using LastMachinist.Integration;
using LastMachinist.Inventory;
using LastMachinist.Machining.Core;
using LastMachinist.Machining.Turning;
using LastMachinist.Quest;

namespace LastMachinist.Tests
{
    public sealed class Phase7Tests
    {
        private static WorkpieceState Shaft() => new WorkpieceState { InstanceId = "shaft", MaterialId = "steel", SourceItemId = "broken", Geometry = new WorkpieceGeometry { Type = GeometryType.RoundProfile, RoundProfile = new TurningProfile { TotalLength = 100, Segments = new List<TurningSegment> { new TurningSegment { StartZ = 0, EndZ = 30, Radius = 20 }, new TurningSegment { StartZ = 30, EndZ = 70, Radius = 25 }, new TurningSegment { StartZ = 70, EndZ = 100, Radius = 20 } } } } };
        private static PartRequirement Requirement() => new PartRequirement { RequiredChamferPositions = new List<float> { 0 }, Dimensions = new List<DimensionRequirement> { new DimensionRequirement("L1", 0, 30, 40), new DimensionRequirement("L2", 30, 70, 50), new DimensionRequirement("L3", 70, 100, 40) } };

        [Test] public void IntegrationConsumesStockAndCompletesPump()
        {
            var inv = new InventoryState(); inv.Add(Quest01Integration.StockItemId, 3);
            var q = new Quest01Integration(inv); q.Quest.Offer(); q.Quest.Accept(); q.Quest.TryAdvance(QuestState.GoingToFarm); q.Quest.TryAdvance(QuestState.InspectingPump);
            Assert.That(q.RecoverBrokenShaft(Shaft()), Is.True); Assert.That(q.ReturnToWorkshop(), Is.True); Assert.That(q.AttemptRuler(), Is.True); Assert.That(q.AcquireVernier(), Is.True);
            Assert.That(q.BeginBrokenShaftInspection(), Is.True);
            V03MemoTests.CompleteMemo(q.Quest.Data.WorkMemo);
            Assert.That(q.InferShape(), Is.True); Assert.That(q.Quest.SelectTurning(), Is.True); Assert.That(q.SelectMaterial(), Is.True);
            Assert.That(inv.GetQuantity(Quest01Integration.StockItemId), Is.Zero);
            Assert.That(inv.Workpieces.FindAll(w => w.Availability == WorkpieceAvailability.Available).Count, Is.EqualTo(2));
            Assert.That(q.BeginMachining(), Is.True);
            var controller = new LatheController(q.CurrentWorkpiece, new RoughingToolState("rough", "rough", true, 2),
                initialX: 30, initialZ: 101, chamfer: new ChamferToolState("chamfer", "chamfer", 1));
            var router = new LatheInputRouter(controller); router.SetMovementScale(MovementScale.Medium);
            router.SetSpindleRunning(true); router.ApplyHandwheelRotation(Axis.X, -90f); router.ApplyHandwheelRotation(Axis.Z, -270f);
            router.SetSpindleRunning(false); router.ApplyHandwheelRotation(Axis.X, 45f); router.RequestReclamp();
            router.ApplyHandwheelRotation(Axis.Z, 270f); router.SetSpindleRunning(true);
            router.ApplyHandwheelRotation(Axis.X, -45f); router.ApplyHandwheelRotation(Axis.Z, -270f);
            router.SetSpindleRunning(false); router.SelectTool(ToolKind.Chamfer); router.ApplyHandwheelRotation(Axis.Z, 261f);
            router.SetSpindleRunning(true); router.ApplyHandwheelRotation(Axis.X, -9f); router.SetSpindleRunning(false);
            Assert.That(q.BeginFinalInspection(false), Is.False); Assert.That(q.BeginFinalInspection(true), Is.True);
            Assert.That(q.Evaluate(Requirement()).Result, Is.EqualTo(PartEvaluationResult.Perfect));
            Assert.That(q.DeliverAndInstall(), Is.True); Assert.That(q.CompleteInstallation(), Is.True); Assert.That(q.PumpRepaired, Is.True);
        }

        [Test] public void ReworkAndRejectedRetryHaveDistinctPaths()
        {
            var inv = new InventoryState(); inv.Add(Quest01Integration.StockItemId, 2); var q = new Quest01Integration(inv);
            V03MemoTests.CompleteMemo(q.Quest.Data.WorkMemo);
            q.Quest.Offer(); q.Quest.Accept(); q.Quest.TryAdvance(QuestState.GoingToFarm); q.Quest.TryAdvance(QuestState.InspectingPump); q.RecoverBrokenShaft(Shaft()); q.ReturnToWorkshop(); q.AttemptRuler(); q.AcquireVernier(); q.BeginBrokenShaftInspection(); q.InferShape(); q.Quest.SelectTurning(); q.SelectMaterial(); q.BeginMachining(); q.BeginFinalInspection(true);
            var result = q.Evaluate(Requirement()); Assert.That(result.Result, Is.EqualTo(PartEvaluationResult.Reworkable)); Assert.That(q.Rework(), Is.True);
        }

        [Test] public void RetryConsumesLastStockAndPreservesRejectedGeometry()
        {
            var inv = new InventoryState(); inv.Add(Quest01Integration.StockItemId, 2);
            var q = new Quest01Integration(inv, new QuestRuntimeState { State = QuestState.ShapeInferred, PumpShaftInferred = true });
            Assert.That(q.Quest.SelectTurning(), Is.True); Assert.That(q.SelectMaterial(), Is.True);
            var first = q.CurrentWorkpiece;
            first.Geometry.RoundProfile.RemoveMaterial(0, 30, 5);
            q.BeginMachining(); q.BeginFinalInspection(true);
            Assert.That(q.Evaluate(Requirement()).Result, Is.EqualTo(PartEvaluationResult.Rejected));
            Assert.That(q.RetryRejected(), Is.True);
            Assert.That(q.CurrentWorkpiece.InstanceId, Is.Not.EqualTo(first.InstanceId));
            Assert.That(first.Geometry.RoundProfile.Segments[0].Radius, Is.EqualTo(5));
            Assert.That(q.CurrentWorkpiece.Geometry.RoundProfile.Segments[0].Radius, Is.EqualTo(25));
            Assert.That(q.LastEvaluation, Is.Null);
            Assert.That(inv.GetQuantity(Quest01Integration.StockItemId), Is.Zero);
            Assert.That(q.RetryRejected(), Is.False);
        }
        [Test] public void ExhaustedStockPreservesRejectedInstanceWithoutFreeGrant()
        {
            var inv = new InventoryState(); inv.Add(Quest01Integration.StockItemId, 1); inv.Add("unrelated", 2);
            var q = new Quest01Integration(inv, new QuestRuntimeState { State = QuestState.ShapeInferred, PumpShaftInferred = true });
            q.Quest.SelectTurning(); q.SelectMaterial();
            var discarded = q.CurrentWorkpiece;
            discarded.Geometry.RoundProfile.RemoveMaterial(0, 30, 5);
            q.BeginMachining(); q.BeginFinalInspection(true); q.Evaluate(Requirement());
            Assert.That(q.RestartMachiningPractice(), Is.False);
            Assert.That(inv.GetQuantity(Quest01Integration.StockItemId), Is.Zero);
            Assert.That(inv.Workpieces.Count, Is.EqualTo(1));
            Assert.That(inv.GetQuantity("unrelated"), Is.EqualTo(2));
            Assert.That(discarded.Geometry.RoundProfile.Segments[0].Radius, Is.EqualTo(5));
            Assert.That(q.CurrentWorkpiece.InstanceId, Is.EqualTo(discarded.InstanceId));
            Assert.That(q.RestartMachiningPractice(), Is.False);
        }
    }
}
