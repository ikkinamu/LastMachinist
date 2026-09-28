using System.Linq;
using LastMachinist.Inventory;
using LastMachinist.Integration;
using LastMachinist.Machining.Core;
using LastMachinist.Machining.Turning;
using LastMachinist.Quest;
using LastMachinist.Save;
using NUnit.Framework;
using UnityEngine;

namespace LastMachinist.Tests
{
    public sealed class InventoryActualLengthTests
    {
        private static WorkpieceDefinition Definition => Resources.Load<WorkpieceDefinition>("WorkpieceDefinitions/RoundBar50");
        private static WorkpieceInstance Add(InventoryState inventory)
        {
            var item = Definition.CreateInstance(new ConditionProperties(), "yard", "session");
            Assert.That(inventory.AddWorkpiece(item), Is.True);
            return inventory.FindWorkpiece(item.InstanceId);
        }
        private static InventoryMachiningSession Session(InventoryState inventory) => new InventoryMachiningSession(inventory, _ => Definition);
        private static void AssertDisplay(WorkpieceInstance item, string expected)
        {
            Assert.That(WorkpieceInventoryPresentation.ListEntry(item, Definition), Does.Contain("현재 길이: " + expected + " mm"));
            Assert.That(WorkpieceInventoryPresentation.Details(item, Definition), Does.Contain("현재 길이: " + expected + " mm"));
        }
        private static void Split(InventoryState inventory, out WorkpieceInstance a, out WorkpieceInstance b)
        {
            var controller = Phase06RevisionTests.Mount(inventory);
            Phase06RevisionTests.Part(controller, new LatheInputRouter(controller), 40);
            Assert.That(WorkpieceSplitter.TrySplit(inventory, controller, out a, out b), Is.True);
        }

        [Test] public void Test31_VirginStockUsesTheSameInitialGeometryAsMounting()
        {
            var inventory = new InventoryState(); var item = Add(inventory);
            AssertDisplay(item, "300.0");
            // A read does not change virgin-stock semantics or introduce another saved value.
            Assert.That(item.HasMachiningGeometry, Is.False);
            var session = Session(inventory);
            Assert.That(session.TryBegin(item.InstanceId, state => {
                Assert.That(InventoryWorkpieceGeometry.TryGetActualLength(item, Definition, out float length), Is.True);
                Assert.That(length, Is.EqualTo(state.Geometry.RoundProfile.GetAxialLength()));
                return true;
            }), Is.True);
            AssertDisplay(item, "300.0");
        }

        [Test] public void Test32_SameDefinitionKeepsEachInstanceLengthAndPrecision()
        {
            var inventory = new InventoryState(); var session = Session(inventory);
            float[] lengths = { 82.437f, 46, 31.5f };
            string[] expected = { "82.4", "46.0", "31.5" };
            for (int i = 0; i < lengths.Length; i++)
            {
                var item = Add(inventory);
                Assert.That(session.TryBegin(item.InstanceId, _ => true), Is.True);
                item.MachiningWorkpiece.Geometry.RoundProfile = WorkpieceSplitter.Slice(item.MachiningWorkpiece.Geometry.RoundProfile, 0, lengths[i]);
                session.End(); AssertDisplay(item, expected[i]);
                Assert.That(item.MachiningWorkpiece.Geometry.RoundProfile.GetAxialLength(), Is.EqualTo(lengths[i]));
            }
            Assert.That(inventory.Workpieces.Select(w => w.InstanceId).Distinct().Count(), Is.EqualTo(3));
        }

        [Test] public void Test33_FacingUpdatesLengthWithoutChangingCoordinateDomainOrOrientation()
        {
            var inventory = new InventoryState(); var controller = Phase06RevisionTests.Mount(inventory);
            var item = inventory.Workpieces[0]; AssertDisplay(item, "100.0");
            // Remove end stock in the authoritative geometry, as an axial/facing cut does.
            controller.Profile.RemoveMaterial(0, 7.6f, 0);
            controller.Profile.RemoveMaterial(90, 100, 0);
            inventory.ReleaseMachining(); AssertDisplay(item, "82.4");
            Assert.That(controller.Profile.TotalLength, Is.EqualTo(100));
            item.MachiningWorkpiece.Orientation = Orientation.Reversed;
            AssertDisplay(item, "82.4");
            controller.Profile.RemoveMaterial(0, 100, 0);
            AssertDisplay(item, "0.0");
        }

        [Test] public void Test34_SplitListsActualLengthsInsteadOfOriginalStockLength()
        {
            var inventory = new InventoryState(); Split(inventory, out var a, out var b);
            AssertDisplay(a, "40.0"); AssertDisplay(b, "58.0");
            Assert.That(WorkpieceInventoryPresentation.ListEntry(a, Definition), Does.Not.Contain("300"));
            Assert.That(WorkpieceInventoryPresentation.Details(b, Definition), Does.Contain("기준 길이: 300.0 mm"));
        }

        [Test] public void Test35_DisplayedSplitLengthsConserveStockWithKerf()
        {
            var inventory = new InventoryState(); Split(inventory, out var a, out var b);
            float Displayed(WorkpieceInstance item) => float.Parse(WorkpieceInventoryPresentation.CurrentLength(item, Definition)
                .Replace("현재 길이: ", "").Replace(" mm", ""), System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(Displayed(a) + WorkpieceSplitter.Kerf + Displayed(b), Is.EqualTo(100));
        }

        [Test] public void Test36_SelectionAndRemountUseTheInventoryGeometryAndSharedProjection()
        {
            var inventory = new InventoryState(); Split(inventory, out _, out var remnant);
            var session = Session(inventory);
            var candidate = session.Candidates().Single(w => w.InstanceId == remnant.InstanceId);
            Assert.That(candidate, Is.SameAs(remnant));
            AssertDisplay(candidate, "58.0");
            Assert.That(session.TryBegin(candidate.InstanceId, state => {
                Assert.That(state.Geometry.RoundProfile.GetAxialLength(), Is.EqualTo(58)); return true;
            }), Is.True);
            session.End(); AssertDisplay(candidate, "58.0");
        }

        [Test] public void Test37_QuestRejectsShortActualStockEvenWhenNominalAndRoundedDisplayAreLongEnough()
        {
            var inventory = new InventoryState(); var controller = Phase06RevisionTests.Mount(inventory, 120);
            var item = inventory.Workpieces[0]; inventory.ReleaseMachining();
            var service = new RepeatableQuestService(inventory);
            var quest = service.Data.Instances[0]; Assert.That(service.Accept(quest.QuestInstanceId), Is.True);
            Assert.That(service.CanAssign(quest.QuestInstanceId, item, Definition, out _), Is.True);
            float target = quest.GeneratedTargetGeometry.Dimensions.Last().EndZ;
            controller.Profile.RemoveMaterial(target - .01f, 120, 0);
            Assert.That(InventoryWorkpieceGeometry.TryGetActualLength(item, Definition, out float length), Is.True);
            Assert.That(length, Is.LessThan(target));
            Assert.That(WorkpieceInventoryPresentation.CurrentLength(item, Definition), Does.Contain(target.ToString("0.0") + " mm"));
            Assert.That(service.CanAssign(quest.QuestInstanceId, item, Definition, out _), Is.False);
        }

        [Test] public void Test38_SaveLoadDerivesLengthsFromRestoredGeometryIncludingFacingAndSplit()
        {
            var inventory = new InventoryState(); Split(inventory, out var a, out var b);
            b.MachiningWorkpiece.Geometry.RoundProfile.RemoveMaterial(50.237f, 58, 0);
            var virgin = Add(inventory);
            string json = JsonUtility.ToJson(SaveGameComposer.Capture(new Quest01Integration(inventory)));
            var loaded = SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(json));
            AssertDisplay(loaded.Inventory.FindWorkpiece(a.InstanceId), "40.0");
            var remnant = loaded.Inventory.FindWorkpiece(b.InstanceId); AssertDisplay(remnant, "50.2");
            Assert.That(remnant.MachiningWorkpiece.Geometry.RoundProfile.GetAxialLength(), Is.EqualTo(50.237f));
            AssertDisplay(loaded.Inventory.FindWorkpiece(virgin.InstanceId), "300.0");
            // Stored geometry still provides length even if the static definition is unavailable.
            Assert.That(WorkpieceInventoryPresentation.CurrentLength(remnant, null), Is.EqualTo("현재 길이: 50.2 mm"));
            Assert.That(json, Does.Not.Contain("ActualLength"));
            remnant.MachiningWorkpiece = null;
            Assert.That(InventoryWorkpieceGeometry.TryGetActualLength(remnant, Definition, out _), Is.False);
        }
    }
}
