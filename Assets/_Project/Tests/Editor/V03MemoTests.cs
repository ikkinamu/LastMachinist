using NUnit.Framework;
using LastMachinist.Quest;
using LastMachinist.Integration;
using LastMachinist.Inventory;
using LastMachinist.Measurement;

namespace LastMachinist.Tests
{
    public sealed class V03MemoTests
    {
        public static void CompleteMemo(WorkMemoState memo)
        {
            foreach (var id in new[] { "L1_Diameter", "L1_Length", "L2_Diameter", "L2_Length", "L3_Diameter" })
                memo.RegisterMeasurement(id, 999);
            memo.L3WearObserved = true;
        }
        [Test] public void TargetShapeStaysHiddenUntilRequiredObservations()
        {
            var memo = new WorkMemoState();
            memo.RegisterMeasurement("L1_Diameter"); memo.RegisterMeasurement("L2_Diameter");
            Assert.That(memo.TryUnlockTarget(), Is.False);
            memo.RegisterMeasurement("L3_Diameter");
            Assert.That(memo.CanInfer, Is.False);
            memo.RegisterMeasurement("L1_Length", 999);
            Assert.That(memo.CanInfer, Is.False);
            memo.RegisterMeasurement("L2_Length", 999);
            Assert.That(memo.CanInfer, Is.False);
            Assert.That(memo.L3WearObserved, Is.False);
            memo.L3WearObserved = true;
            Assert.That(memo.CanInfer, Is.True);
            Assert.That(memo.CanRevealTarget, Is.False);
            Assert.That(memo.TryUnlockTarget(), Is.True);
            Assert.That(memo.CanRevealTarget, Is.True);
        }
        [TestCase("L1_Diameter")] [TestCase("L1_Length")]
        [TestCase("L2_Diameter")] [TestCase("L2_Length")] [TestCase("L3_Diameter")]
        public void EachRequiredRecordIndependentlyBlocksInference(string missing)
        {
            var memo = new WorkMemoState(); CompleteMemo(memo);
            memo.Measurements.RemoveAll(m => m.PointId == missing);
            Assert.That(memo.TryUnlockTarget(), Is.False);
        }
        [Test] public void BrokenShaftSessionRejectsAnotherWorkpiecesRecord()
        {
            var integration = new Quest01Integration(new InventoryState(), new QuestRuntimeState { State = QuestState.InspectingPump });
            Assert.That(integration.RecoverPrototypeBrokenShaft(), Is.True);
            integration.Quest.Data.State = QuestState.InspectingBrokenShaft;
            integration.Quest.Data.HasReceivedVernier = true;
            var point = new MeasurementPoint("L3_Diameter", MeasurementPointType.Diameter, 70, 100);
            Assert.That(integration.Measurement.Select(point, "old_vernier", true).Value, Is.EqualTo(36));
            var record = integration.Measurement.Record(999);
            Assert.That(integration.RecordMeasurement(record), Is.True);
            Assert.That(record.PlayerValue, Is.EqualTo(999));
            Assert.That(integration.Quest.Data.WorkMemo.RecordedText("L3_Diameter"), Is.EqualTo("999"));
            Assert.That(integration.Quest.Data.WorkMemo.L3WearObserved, Is.False);
            var foreign = new MeasurementSession(integration.CurrentWorkpiece.Geometry.RoundProfile, "other");
            foreign.Select(point, "old_vernier", true);
            Assert.That(integration.RecordMeasurement(foreign.Record(36)), Is.False);
        }
    }
}
