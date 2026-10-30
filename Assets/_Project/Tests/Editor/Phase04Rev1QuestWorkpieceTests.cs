using System.Linq;
using LastMachinist.Integration;
using LastMachinist.Inventory;
using LastMachinist.Machining.Turning;
using LastMachinist.Quest;
using LastMachinist.Save;
using NUnit.Framework;
using UnityEngine;

namespace LastMachinist.Tests
{
    public sealed class Phase04Rev1QuestWorkpieceTests
    {
        [Test] public void InitialWorkshopStockCreatesThreeIndependentInstancesOnce()
        {
            var inventory = new InventoryState();
            Assert.That(InitialWorkshopWorkpieces.EnsureCreated(inventory, "new-game"), Is.EqualTo(3));
            Assert.That(inventory.Workpieces.Count, Is.EqualTo(3));
            Assert.That(inventory.Workpieces.Select(w => w.InstanceId).Distinct().Count(), Is.EqualTo(3));
            Assert.That(inventory.Workpieces.All(w => w.AcquisitionSource == InitialWorkshopWorkpieces.Source && w.HasMachiningGeometry), Is.True);
            Assert.That(inventory.Workpieces.Select(w => w.MachiningWorkpiece.Geometry.RoundProfile.GetAxialLength()).Distinct().Single(), Is.EqualTo(100));
            Assert.That(InitialWorkshopWorkpieces.EnsureCreated(inventory, "re-entry"), Is.EqualTo(3));
            Assert.That(inventory.Workpieces.Count, Is.EqualTo(3));
        }

        [Test] public void Quest01SelectsAnOwnedInstanceAndUsesItsGeometry()
        {
            var inventory = new InventoryState(); InitialWorkshopWorkpieces.EnsureCreated(inventory, "session");
            var integration = new Quest01Integration(inventory, new QuestRuntimeState {
                State = QuestState.ShapeInferred, PumpShaftInferred = true, TurningSelected = true, StockCount = 3 });
            var selected = inventory.Workpieces[1];
            selected.MachiningWorkpiece.Geometry.RoundProfile = WorkpieceSplitter.Slice(selected.MachiningWorkpiece.Geometry.RoundProfile, 0, 100);
            Assert.That(integration.SelectMaterial(selected.InstanceId), Is.True);
            Assert.That(integration.ActiveWorkpieceInstance, Is.SameAs(selected));
            Assert.That(integration.CurrentWorkpiece.InstanceId, Is.EqualTo(selected.InstanceId));
            Assert.That(integration.CurrentWorkpiece.Geometry.RoundProfile.GetAxialLength(), Is.EqualTo(100));
            Assert.That(selected.Availability, Is.EqualTo(WorkpieceAvailability.InMachining));
            Assert.That(inventory.ActiveMachiningInstanceId, Is.EqualTo(selected.InstanceId));
        }

        [Test] public void SaveLoadDoesNotDuplicateInitialWorkpiecesOrReplaceTheirGeometry()
        {
            var inventory = new InventoryState(); InitialWorkshopWorkpieces.EnsureCreated(inventory, "session");
            var selected = inventory.Workpieces[0]; selected.MachiningWorkpiece.Geometry.RoundProfile = WorkpieceSplitter.Slice(selected.MachiningWorkpiece.Geometry.RoundProfile, 17.5f, 100);
            var integration = new Quest01Integration(inventory, new QuestRuntimeState { State = QuestState.Machining, PumpShaftInferred = true });
            integration.AttachWorkpiece(selected.MachiningWorkpiece);
            var json = JsonUtility.ToJson(SaveGameComposer.Capture(integration));
            var restored = SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(json));
            Assert.That(restored.Inventory.Workpieces.Count, Is.EqualTo(3));
            var copy = restored.Inventory.FindWorkpiece(selected.InstanceId);
            Assert.That(copy.MachiningWorkpiece.Geometry.RoundProfile.GetAxialLength(), Is.EqualTo(82.5f));
            Assert.That(restored.Inventory.Workpieces.Count(w => w.AcquisitionSource == InitialWorkshopWorkpieces.Source), Is.EqualTo(3));
        }
    }
}
