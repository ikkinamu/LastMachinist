using NUnit.Framework;
using LastMachinist.Integration;
using LastMachinist.Inventory;
using LastMachinist.Save;

namespace LastMachinist.Tests
{
    public sealed class V03SaveComposerTests
    {
        [Test] public void CheckpointPolicyIncludesRequiredMilestones()
        {
            foreach (var state in new[] { LastMachinist.Quest.QuestState.FarmerArrived, LastMachinist.Quest.QuestState.VernierReceived,
                LastMachinist.Quest.QuestState.MaterialSelected, LastMachinist.Quest.QuestState.ReadyToDeliver, LastMachinist.Quest.QuestState.Completed })
                Assert.That(SaveGameComposer.IsCheckpoint(state), Is.True);
            Assert.That(SaveGameComposer.IsCheckpoint(LastMachinist.Quest.QuestState.NotStarted), Is.False);
            Assert.That(SaveGameComposer.IsCheckpoint(LastMachinist.Quest.QuestState.Machining), Is.False);
        }
        [Test] public void UnityJsonRestoresManualRecordMemoAndOrientation()
        {
            var q = new Quest01Integration(new InventoryState(), new LastMachinist.Quest.QuestRuntimeState { State = LastMachinist.Quest.QuestState.InspectingPump });
            q.RecoverPrototypeBrokenShaft();
            q.CurrentWorkpiece.Orientation = LastMachinist.Machining.Core.Orientation.Reversed;
            q.Quest.Data.WorkMemo.RegisterMeasurement("L1_Length", 999);
            q.Measurement.Select(new LastMachinist.Measurement.MeasurementPoint("L1_Diameter", LastMachinist.Measurement.MeasurementPointType.Diameter, 0, 30), "old_vernier", true);
            q.Measurement.Record(123);
            var json = UnityEngine.JsonUtility.ToJson(SaveGameComposer.Capture(q, "Workshop"));
            var restored = SaveGameComposer.Restore(UnityEngine.JsonUtility.FromJson<SaveGameData>(json));
            Assert.That(restored.CurrentWorkpiece.Orientation, Is.EqualTo(LastMachinist.Machining.Core.Orientation.Reversed));
            Assert.That(restored.Measurement.Records["L1_Diameter"].PlayerValue, Is.EqualTo(123));
            Assert.That(restored.Quest.Data.WorkMemo.RecordedText("L1_Length"), Is.EqualTo("999"));
        }
        [Test] public void CaptureIncludesIntegrationStateWithoutSceneReferences()
        {
            var inventory = new InventoryState(); inventory.Add(Quest01Integration.StockItemId, 3);
            var integration = new Quest01Integration(inventory);
            var data = SaveGameComposer.Capture(integration, "Workshop");
            Assert.That(data.CurrentSceneId, Is.EqualTo("Workshop"));
            Assert.That(data.Inventory.GetQuantity(Quest01Integration.StockItemId), Is.EqualTo(3));
            Assert.That(data.Quest, Is.SameAs(integration.Quest.Data));
        }
    }
}
