using System;
using System.Collections.Generic;
using System.Reflection;
using LastMachinist.Core;
using LastMachinist.Inventory;
using LastMachinist.Machining.Core;
using LastMachinist.Machining.Turning;
using LastMachinist.Save;
using NUnit.Framework;
using UnityEngine;

namespace LastMachinist.Tests
{
    public sealed class CoreDataTests
    {
        private const string TestStockId = "test_stock";
        private const string TestShaftId = "test_shaft";

        private static WorkpieceState CreateWorkpiece()
        {
            return new WorkpieceState
            {
                InstanceId = "test_instance",
                MaterialId = DefinitionIds.StructuralCarbonSteel,
                SourceItemId = TestStockId,
                Geometry = new WorkpieceGeometry
                {
                    Type = GeometryType.RoundProfile,
                    RoundProfile = new TurningProfile
                    {
                        TotalLength = 100,
                        Segments = new List<TurningSegment>
                        {
                            new TurningSegment { StartZ = 0, EndZ = 30, Radius = 20 },
                            new TurningSegment { StartZ = 30, EndZ = 70, Radius = 25 },
                            new TurningSegment { StartZ = 70, EndZ = 100, Radius = 20 }
                        }
                    }
                }
            };
        }

        [Test]
        public void WorkpieceRepresentsSteppedProfile()
        {
            var workpiece = CreateWorkpiece();
            workpiece.Validate();
            Assert.That(workpiece.Orientation, Is.EqualTo(Orientation.Normal));
            Assert.That(workpiece.Geometry.RoundProfile.Segments[1].Diameter, Is.EqualTo(50));
            workpiece.Orientation = Orientation.Reversed;
            Assert.That(workpiece.Geometry.RoundProfile.Segments[0].StartZ, Is.Zero);
        }

        [Test]
        public void InventoryAddsAndRemovesAtomically()
        {
            var inventory = new InventoryState();
            inventory.Add(TestStockId, 3);
            inventory.Add(TestStockId, 2);
            Assert.That(inventory.GetQuantity(TestStockId), Is.EqualTo(5));
            Assert.That(inventory.TryRemove(TestStockId, 6), Is.False);
            Assert.That(inventory.GetQuantity(TestStockId), Is.EqualTo(5));
            Assert.That(inventory.TryRemove(TestStockId, 5), Is.True);
            Assert.That(inventory.Entries, Is.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.Add(TestStockId, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.TryRemove(TestStockId, 0));
            inventory.Add(TestStockId, int.MaxValue);
            Assert.Throws<OverflowException>(() => inventory.Add(TestStockId, 1));
            Assert.That(inventory.GetQuantity(TestStockId), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void UniqueItemsKeepIndependentIdentity()
        {
            var inventory = new InventoryState();
            inventory.Add(TestShaftId, 1, "a");
            inventory.Add(TestShaftId, 1, "b");
            Assert.Throws<ArgumentException>(() => inventory.Add(TestStockId, 1, "a"));
            Assert.That(inventory.TryRemove(TestShaftId, 1), Is.False);
            Assert.That(inventory.TryRemove(TestShaftId, 1, "a"), Is.True);
            Assert.That(inventory.Entries[0].InstanceId, Is.EqualTo("b"));
        }

        [Test]
        public void JsonRoundTripPreservesRequiredState()
        {
            var workpiece = CreateWorkpiece();
            workpiece.Orientation = Orientation.Reversed;
            workpiece.IsFinished = true;
            workpiece.ProcessHistory.Add("test_process");
            var save = new SaveGameData();
            save.Inventory.Add(TestStockId, 3);
            save.Inventory.Add(TestShaftId, 1, workpiece.InstanceId);
            save.WorkpieceInstances.Add(workpiece);
            save.Validate();
            var restored = JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(save));
            restored.Validate();
            var actual = restored.WorkpieceInstances[0];
            Assert.That(actual.Orientation, Is.EqualTo(Orientation.Reversed));
            Assert.That(actual.InstanceId, Is.EqualTo(workpiece.InstanceId));
            Assert.That(actual.MaterialId, Is.EqualTo(workpiece.MaterialId));
            Assert.That(actual.SourceItemId, Is.EqualTo(workpiece.SourceItemId));
            Assert.That(actual.IsFinished, Is.True);
            Assert.That(actual.ProcessHistory, Is.EqualTo(workpiece.ProcessHistory));
            Assert.That(actual.Geometry.Type, Is.EqualTo(GeometryType.RoundProfile));
            Assert.That(actual.Geometry.RoundProfile.TotalLength, Is.EqualTo(100));
            for (int i = 0; i < 3; i++)
            {
                var before = workpiece.Geometry.RoundProfile.Segments[i];
                var after = actual.Geometry.RoundProfile.Segments[i];
                Assert.That(after.StartZ, Is.EqualTo(before.StartZ));
                Assert.That(after.EndZ, Is.EqualTo(before.EndZ));
                Assert.That(after.Radius, Is.EqualTo(before.Radius));
            }
            Assert.That(restored.Inventory.GetQuantity(TestStockId), Is.EqualTo(3));
            Assert.That(restored.Inventory.Entries[1].InstanceId, Is.EqualTo(workpiece.InstanceId));
            actual.Geometry.RoundProfile.Segments[0].Radius = 10;
            Assert.That(workpiece.Geometry.RoundProfile.Segments[0].Radius, Is.EqualTo(20));
        }

        [Test]
        public void InvalidProfilesAndSaveDataAreRejected()
        {
            var workpiece = CreateWorkpiece();
            workpiece.Geometry.RoundProfile.Segments[1].StartZ = 31;
            Assert.Throws<ArgumentException>(() => workpiece.Validate());
            workpiece = CreateWorkpiece();
            workpiece.Geometry.RoundProfile.Segments[0].Radius = float.NaN;
            Assert.Throws<ArgumentException>(() => workpiece.Validate());
            var save = new SaveGameData();
            save.WorkpieceInstances.Add(CreateWorkpiece());
            save.WorkpieceInstances.Add(CreateWorkpiece());
            Assert.Throws<ArgumentException>(() => save.Validate());
        }

        [Test]
        public void SaveGraphContainsNoUnityObjectReferences()
        {
            CheckFields(typeof(SaveGameData), new HashSet<Type>());
        }

        private static void CheckFields(Type type, HashSet<Type> visited)
        {
            Assert.That(typeof(UnityEngine.Object).IsAssignableFrom(type), Is.False, type.FullName);
            if (!visited.Add(type) || type.IsPrimitive || type.IsEnum || type == typeof(string)) return;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                CheckFields(type.GetGenericArguments()[0], visited);
                return;
            }
            Assert.That(type.IsDefined(typeof(SerializableAttribute), false), Is.True, type.FullName);
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                CheckFields(field.FieldType, visited);
        }
    }
}
