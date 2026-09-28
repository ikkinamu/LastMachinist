using System;
using LastMachinist.Inventory;
using LastMachinist.Integration;
using LastMachinist.Save;
using NUnit.Framework;
using UnityEngine;

namespace LastMachinist.Tests
{
    public sealed class InventoryMachiningTests
    {
        private static WorkpieceDefinition Definition => Resources.Load<WorkpieceDefinition>("WorkpieceDefinitions/RoundBar50");
        private static InventoryState Stock()
        {
            var inventory = new InventoryState();
            inventory.AddWorkpiece(Definition.CreateInstance(new ConditionProperties { CorrosionCondition = 82 }, "yard", "session"));
            inventory.AddWorkpiece(Definition.CreateInstance(new ConditionProperties { CorrosionCondition = 43 }, "yard", "session"));
            return inventory;
        }
        private static InventoryMachiningSession Session(InventoryState inventory) => new InventoryMachiningSession(inventory, id => id == Definition.WorkpieceDefinitionId ? Definition : null);

        [Test] public void SeparateCandidatesKeepIdentityAndNominalGeometry()
        {
            var inventory = Stock(); var session = Session(inventory);
            var a = inventory.Workpieces[0]; var b = inventory.Workpieces[1];
            Assert.That(session.Candidates().Count, Is.EqualTo(2));
            Assert.That(session.TryBegin(a.InstanceId, state => {
                Assert.That(state.InstanceId, Is.EqualTo(a.InstanceId));
                Assert.That(state.Geometry.RoundProfile.TotalLength, Is.EqualTo(300));
                Assert.That(state.Geometry.RoundProfile.RadiusAt(150) * 2, Is.EqualTo(50));
                return true;
            }), Is.True);
            Assert.That(session.Active, Is.SameAs(a));
            Assert.That(a.Conditions.CorrosionCondition, Is.EqualTo(82));
            Assert.That(a.Availability, Is.EqualTo(WorkpieceAvailability.InMachining));
            Assert.That(inventory.RemoveWorkpiece(a.InstanceId), Is.False);
            Assert.That(Session(inventory).TryBegin(a.InstanceId, _ => true), Is.False);
            Assert.That(session.TryBegin(b.InstanceId, _ => true), Is.False);
            session.End();
            Assert.That(a.Availability, Is.EqualTo(WorkpieceAvailability.Available));
            Assert.That(inventory.Workpieces.Count, Is.EqualTo(2));
            Assert.That(session.TryBegin(b.InstanceId, _ => true), Is.True);
            Assert.That(session.Active.Conditions.CorrosionCondition, Is.EqualTo(43));
            session.End();
        }

        [Test] public void IncompatibleAndEmptyInventoryCannotStart()
        {
            var empty = Session(new InventoryState());
            Assert.That(empty.Candidates(), Is.Empty);
            Assert.That(empty.TryBegin("absent", _ => true), Is.False);
            var plate = UnityEngine.Object.Instantiate(Definition);
            try
            {
                plate.ShapeType = WorkpieceShape.Plate;
                var inventory = Stock();
                var session = new InventoryMachiningSession(inventory, _ => plate);
                Assert.That(session.Candidates(), Is.Empty);
                Assert.That(session.TryBegin(inventory.Workpieces[0].InstanceId, _ => true), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(plate); }
        }

        [Test] public void FailedInitializationPreservesInventoryAndExistingCuts()
        {
            var inventory = Stock(); var a = inventory.Workpieces[0]; var session = Session(inventory);
            Assert.That(session.TryBegin(a.InstanceId, _ => false), Is.False);
            Assert.That(a.MachiningWorkpiece, Is.Null);
            Assert.That(a.Availability, Is.EqualTo(WorkpieceAvailability.Available));
            Assert.That(session.TryBegin(a.InstanceId, state => { state.Geometry.RoundProfile.RemoveMaterial(100, 200, 20); return true; }), Is.True);
            session.End();
            Assert.That(session.TryBegin(a.InstanceId, state => {
                state.Geometry.RoundProfile.RemoveMaterial(100, 200, 1);
                throw new InvalidOperationException("Simulated session initialization failure");
            }), Is.False);
            Assert.That(a.MachiningWorkpiece.Geometry.RoundProfile.RadiusAt(150), Is.EqualTo(20));
            Assert.That(a.Availability, Is.EqualTo(WorkpieceAvailability.Available));
            Assert.That(inventory.ActiveMachiningInstanceId, Is.Null);
            Assert.That(inventory.Workpieces.Count, Is.EqualTo(2));
            Assert.That(session.TryBegin(a.InstanceId, state => state.Geometry.RoundProfile.RadiusAt(150) == 20), Is.True);
            session.End();
        }

        [Test] public void SaveDuringMachiningRetainsCutsAndReleasesReservationOnLoad()
        {
            var inventory = Stock(); var item = inventory.Workpieces[0]; var session = Session(inventory);
            Assert.That(session.TryBegin(item.InstanceId, state => true), Is.True);
            item.MachiningWorkpiece.Geometry.RoundProfile.RemoveMaterial(100, 200, 20);
            var data = SaveGameComposer.Capture(new Quest01Integration(inventory), "Workshop");
            var restored = SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(data)));
            var copy = restored.Inventory.FindWorkpiece(item.InstanceId);
            Assert.That(copy.Availability, Is.EqualTo(WorkpieceAvailability.Available));
            Assert.That(restored.Inventory.ActiveMachiningInstanceId, Is.Null);
            Assert.That(copy.MachiningWorkpiece.Geometry.RoundProfile.RadiusAt(150), Is.EqualTo(20));
            Assert.That(copy.Conditions.CorrosionCondition, Is.EqualTo(82));
            Assert.That(copy.IdentificationState, Is.EqualTo(IdentificationState.Unknown));
            Assert.That(Session(restored.Inventory).TryBegin(copy.InstanceId, _ => true), Is.True);
            session.End();
        }
    }
}
