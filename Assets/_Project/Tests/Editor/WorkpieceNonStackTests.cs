using System.Linq;
using LastMachinist.Inventory;
using LastMachinist.Integration;
using LastMachinist.Machining.Turning;
using LastMachinist.Quest;
using LastMachinist.Save;
using NUnit.Framework;
using UnityEngine;

namespace LastMachinist.Tests
{
    public sealed class WorkpieceNonStackTests
    {
        private static WorkpieceDefinition Definition => Resources.Load<WorkpieceDefinition>("WorkpieceDefinitions/RoundBar50");
        private static InventoryState Initial()
        {
            var inventory = new InventoryState(); InitialWorkshopWorkpieces.EnsureCreated(inventory, "new-game"); return inventory;
        }
        private static Quest01Integration Quest(InventoryState inventory) => new Quest01Integration(inventory,
            new QuestRuntimeState { State = QuestState.ShapeInferred, PumpShaftInferred = true, TurningSelected = true });

        [Test] public void IdenticalPiecesRemainThreeSeparateRowsAndSecondSelectionChangesOnlyB()
        {
            var inventory = Initial(); var q = Quest(inventory); var a = inventory.Workpieces[0]; var b = inventory.Workpieces[1]; var c = inventory.Workpieces[2];
            string beforeA = JsonUtility.ToJson(a), beforeC = JsonUtility.ToJson(c);
            var panel = new WorkpieceInventoryPanel();
            Assert.That(panel.Select(inventory, b.InstanceId), Is.True);
            Assert.That(panel.SelectedId, Is.EqualTo(b.InstanceId));
            Assert.That(inventory.Workpieces.Select(w => w.InstanceId).Distinct().Count(), Is.EqualTo(3));
            Assert.That(inventory.Workpieces.Select(w => WorkpieceInventoryPresentation.ListEntry(w, Definition)).Distinct().Count(), Is.EqualTo(1), "Equal labels do not merge individual rows.");
            Assert.That(q.SelectMaterial(panel.SelectedId), Is.True);
            Assert.That(q.CurrentWorkpiece, Is.SameAs(b.MachiningWorkpiece));
            Assert.That(inventory.ActiveMachiningInstanceId, Is.EqualTo(b.InstanceId));
            q.CurrentWorkpiece.Geometry.RoundProfile.RemoveMaterial(98, 100, 0);
            q.CurrentWorkpiece.Geometry.RoundProfile.RemoveMaterial(0, 98, 24);
            Assert.That(WorkpieceInventoryPresentation.ListEntry(b, Definition), Does.Contain("Ø48.0").And.Contain("98.0 mm"));
            Assert.That(WorkpieceInventoryPresentation.ListEntry(a, Definition), Does.Contain("Ø50.0").And.Contain("100.0 mm"));
            Assert.That(JsonUtility.ToJson(a), Is.EqualTo(beforeA)); Assert.That(JsonUtility.ToJson(c), Is.EqualTo(beforeC));
            Assert.That(inventory.Entries, Is.Empty);
        }

        [Test] public void RecoveredStockUsesSameSelectorAndDoesNotMergeWithInitialStock()
        {
            var inventory = Initial(); var recovered = Definition.CreateInstance(new ConditionProperties { CorrosionCondition = 61 }, "yard", "visit");
            inventory.AddWorkpiece(recovered);
            var q = Quest(inventory);
            Assert.That(q.SelectMaterial(recovered.InstanceId), Is.True);
            Assert.That(q.ActiveWorkpieceInstance.InstanceId, Is.EqualTo(recovered.InstanceId));
            Assert.That(q.ActiveWorkpieceInstance.Conditions.CorrosionCondition, Is.EqualTo(61));
            Assert.That(inventory.Workpieces.Count, Is.EqualTo(4));
            Assert.That(inventory.Workpieces.Count(w => w.Availability == WorkpieceAvailability.Available), Is.EqualTo(3));
        }

        [Test] public void TooShortOrOvercutSelectionDoesNotReserveAnotherInstance()
        {
            var inventory = Initial(); var q = Quest(inventory); var b = inventory.Workpieces[1];
            b.MachiningWorkpiece.Geometry.RoundProfile.RemoveMaterial(48, 100, 0);
            Assert.That(q.SelectMaterial(b.InstanceId), Is.False);
            Assert.That(inventory.ActiveMachiningInstanceId, Is.Null);
            Assert.That(inventory.Workpieces.All(w => w.Availability == WorkpieceAvailability.Available), Is.True);
            var c = inventory.Workpieces[2]; c.MachiningWorkpiece.Geometry.RoundProfile.RemoveMaterial(0, 100, 10);
            Assert.That(q.SelectMaterial(c.InstanceId), Is.False);
            Assert.That(q.SelectMaterial("missing"), Is.False);
        }

        [Test] public void RecoveredPieceSplitPreservesTwoIndependentRowsAndOffcutOnSave()
        {
            var inventory = Initial(); var b = Definition.CreateInstance(new ConditionProperties(), "yard", "visit"); inventory.AddWorkpiece(b); var q = Quest(inventory);
            Assert.That(q.SelectMaterial(b.InstanceId), Is.True);
            var controller = new LatheController(q.CurrentWorkpiece, new RoughingToolState("rough", "황삭", true, 2), .05f, 30, 301, 15, null, true);
            Phase06RevisionTests.Part(controller, new LatheInputRouter(controller), 100);
            Assert.That(WorkpieceSplitter.TrySplit(inventory, controller, out var a, out var offcut), Is.True);
            q.AfterWorkpieceSplit(b.InstanceId, a, offcut);
            Assert.That(inventory.Workpieces.Count, Is.EqualTo(5));
            Assert.That(inventory.FindWorkpiece(b.InstanceId), Is.Null);
            Assert.That(a.MachiningWorkpiece.Geometry.RoundProfile.GetAxialLength() + WorkpieceSplitter.Kerf + offcut.MachiningWorkpiece.Geometry.RoundProfile.GetAxialLength(), Is.EqualTo(300));
            Assert.That(WorkpieceInventoryPresentation.ListEntry(offcut, Definition), Does.Contain("198.0 mm"));
            var restored = SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(SaveGameComposer.Capture(q))));
            Assert.That(restored.Inventory.Workpieces.Select(w => w.InstanceId), Is.EquivalentTo(inventory.Workpieces.Select(w => w.InstanceId)));
            Assert.That(restored.CurrentWorkpiece, Is.SameAs(restored.Inventory.FindWorkpiece(a.InstanceId).MachiningWorkpiece));
        }

        [Test] public void SaveKeepsDifferentLengthsAndConditionsWithoutDuplicateGeometryOrInitialGrant()
        {
            var inventory = Initial(); var q = Quest(inventory);
            q.SelectMaterial(inventory.Workpieces[1].InstanceId);
            q.CurrentWorkpiece.Geometry.RoundProfile.RemoveMaterial(98, 100, 0);
            inventory.Workpieces[1].Conditions.SurfaceCondition = 64;
            var data = SaveGameComposer.Capture(q);
            Assert.That(data.WorkpieceInstances, Is.Empty, "Owned geometry is serialized only in inventory.");
            for (int i = 0; i < 3; i++)
            {
                q = SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(data)));
                Assert.That(q.Inventory.Workpieces.Count, Is.EqualTo(3));
                Assert.That(q.Inventory.Workpieces[1].MachiningWorkpiece.Geometry.RoundProfile.GetAxialLength(), Is.EqualTo(98));
                Assert.That(q.Inventory.Workpieces[1].Conditions.SurfaceCondition, Is.EqualTo(64));
                Assert.That(q.Inventory.Workpieces[0].MachiningWorkpiece.Geometry.RoundProfile.GetAxialLength(), Is.EqualTo(100));
                Assert.That(q.Inventory.ActiveMachiningInstanceId, Is.Null);
                data = SaveGameComposer.Capture(q);
            }
            q.Inventory.Workpieces.Clear();
            Assert.That(InitialWorkshopWorkpieces.EnsureCreated(q.Inventory, "again"), Is.Zero);
        }

        [Test] public void LegacyQuantityBecomesIndividualStockWhileOtherStackItemsRemain()
        {
            var data = new SaveGameData(); data.Inventory.Add(Quest01Integration.StockItemId, 2); data.Inventory.Add("consumable", 7);
            var restored = SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(data)));
            Assert.That(restored.Inventory.Workpieces.Count, Is.EqualTo(2));
            Assert.That(restored.Inventory.Workpieces.Select(w => w.InstanceId).Distinct().Count(), Is.EqualTo(2));
            Assert.That(restored.Inventory.GetQuantity(Quest01Integration.StockItemId), Is.Zero);
            Assert.That(restored.Inventory.GetQuantity("consumable"), Is.EqualTo(7));
            var again = SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(SaveGameComposer.Capture(restored))));
            Assert.That(again.Inventory.Workpieces.Select(w => w.InstanceId), Is.EquivalentTo(restored.Inventory.Workpieces.Select(w => w.InstanceId)));
        }

        [Test] public void InstallationConsumesOnlyLinkedPieceAndPreservesAnotherMachiningReservation()
        {
            var inventory = Initial(); var q = Quest(inventory); var installed = inventory.Workpieces[1]; var other = inventory.Workpieces[2];
            Assert.That(q.SelectMaterial(installed.InstanceId), Is.True);
            inventory.ReleaseMachining();
            q.Quest.Data.State = QuestState.Installing;
            var session = new InventoryMachiningSession(inventory, _ => Definition);
            Assert.That(session.TryBegin(other.InstanceId, _ => true), Is.True);
            Assert.That(q.CompleteInstallation(), Is.True);
            Assert.That(inventory.FindWorkpiece(installed.InstanceId), Is.Null);
            Assert.That(inventory.ActiveMachiningInstanceId, Is.EqualTo(other.InstanceId));
            Assert.That(other.Availability, Is.EqualTo(WorkpieceAvailability.InMachining));
            Assert.That(inventory.Workpieces.Count, Is.EqualTo(2));
            inventory.Validate();
        }
    }
}
