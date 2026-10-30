using System;
using System.Linq;
using LastMachinist.Inventory;
using LastMachinist.Integration;
using LastMachinist.Machining.BenchDrill;
using LastMachinist.Machining.Core;
using LastMachinist.Machining.Turning;
using LastMachinist.Quest;
using LastMachinist.Save;
using NUnit.Framework;
using UnityEngine;

namespace LastMachinist.Tests
{
    public sealed class BenchDrillPhase02Tests
    {
        public static WorkpieceDefinition Definition(string id) => Resources.LoadAll<WorkpieceDefinition>("WorkpieceDefinitions").FirstOrDefault(d => d.WorkpieceDefinitionId == id);
        public static WorkpieceInstance AddPlate(InventoryState inventory)
        {
            var definition = Resources.Load<WorkpieceDefinition>("WorkpieceDefinitions/Plate100x80");
            var item = definition.CreateInstance(new ConditionProperties(), "abandoned_workshop", "test");
            Assert.That(inventory.AddWorkpiece(item), Is.True);
            return inventory.FindWorkpiece(item.InstanceId);
        }
        public static void Drill(BenchDrillSession s, float x, float y, float depth = 5)
        {
            Assert.That(s.Execute(BenchDrillCommand.Feed, -100), Is.True);
            Assert.That(s.Execute(BenchDrillCommand.MoveX, x - s.X), Is.True);
            Assert.That(s.Execute(BenchDrillCommand.MoveY, y - s.Y), Is.True);
            Assert.That(s.Execute(BenchDrillCommand.Feed, 5 + depth), Is.True);
        }
        public static BenchDrillSession Mount(InventoryState inventory, WorkpieceInstance item, int bit = 1)
        {
            var s = new BenchDrillSession(inventory, Definition);
            Assert.That(s.TryLoad(item), Is.True); Assert.That(s.Clamp(), Is.True);
            Assert.That(s.Execute(BenchDrillCommand.SelectBit, bit), Is.True);
            Assert.That(s.SetSpindle(true), Is.True); return s;
        }
        [Test] public void ContactFixtureSpindleAndRetractAreRealPreconditions()
        {
            var inv = new InventoryState(); var item = AddPlate(inv); var s = new BenchDrillSession(inv, Definition);
            Assert.That(s.TryLoad(item), Is.True); s.Execute(BenchDrillCommand.MoveX, 20); s.Execute(BenchDrillCommand.MoveY, 20);
            s.Execute(BenchDrillCommand.SelectBit, 1);
            Assert.That(s.SetSpindle(true), Is.False); Assert.That(s.Execute(BenchDrillCommand.Feed, 10), Is.False);
            Assert.That(s.Clamp(), Is.True); Assert.That(s.Execute(BenchDrillCommand.Feed, 10), Is.False);
            s.SetSpindle(true); s.Execute(BenchDrillCommand.Feed, 4);
            Assert.That(item.MachiningWorkpiece.Geometry.HoleFeatures, Is.Empty);
            s.Execute(BenchDrillCommand.Feed, 2);
            Assert.That(item.MachiningWorkpiece.Geometry.HoleFeatures.Single().IsThrough, Is.False);
            Assert.That(s.Execute(BenchDrillCommand.MoveX, 5), Is.False);
            Assert.That(s.Execute(BenchDrillCommand.Unclamp), Is.False);
            Assert.That(s.Execute(BenchDrillCommand.SelectBit, 2), Is.False);
            s.Execute(BenchDrillCommand.Feed, 4);
            Assert.That(item.MachiningWorkpiece.Geometry.HoleFeatures.Single().IsThrough, Is.True);
            s.Execute(BenchDrillCommand.Feed, -100); Assert.That(s.QuillDepth, Is.EqualTo(-5));
            inv.Validate(); s.Finish(); inv.Validate();
        }
        [Test] public void LocalPositionsZeroMultipleFeaturesAndCopyRemainIndependent()
        {
            var inv = new InventoryState(); var item = AddPlate(inv); var s = Mount(inv, item);
            Drill(s, 20, 20); string first = item.MachiningWorkpiece.Geometry.HoleFeatures[0].FeatureId;
            s.Execute(BenchDrillCommand.ZeroX); s.Execute(BenchDrillCommand.ZeroY);
            Assert.That(s.ReadingX, Is.Zero); Assert.That(s.X, Is.EqualTo(20));
            Drill(s, 70, 50); var copy = item.Copy();
            Assert.That(copy.MachiningWorkpiece.Geometry.HoleFeatures.Count, Is.EqualTo(2));
            Assert.That(copy.MachiningWorkpiece.Geometry.HoleFeatures[0].FeatureId, Is.EqualTo(first));
            Assert.That(copy.MachiningWorkpiece.Geometry.HoleFeatures[1].FeatureId, Is.Not.EqualTo(first));
            Assert.That(WorkshopStory.Evaluate(item.MachiningWorkpiece.Geometry, out _), Is.True);
            var rect = new Rect(100, 100, 200, 160); var g = item.MachiningWorkpiece.Geometry.General;
            Assert.That(LathePrototype.BenchLocalToView(rect, g, 20, 20), Is.EqualTo(new Vector2(140, 220)));
            Assert.That(LathePrototype.BenchLocalToView(new Rect(110, 120, 200, 160), g, 20, 20), Is.EqualTo(new Vector2(150, 240)));
            s.Finish(); Assert.That(inv.FindWorkpiece(item.InstanceId), Is.SameAs(item));
        }
        [TestCase(0)] [TestCase(2)] [TestCase(3)] public void WrongBitCreatesActualDiameterAndFails(int bit)
        {
            var inv = new InventoryState(); var p = AddPlate(inv); var s = Mount(inv, p, bit);
            Drill(s, 20, 20); Drill(s, 70, 50);
            Assert.That(p.MachiningWorkpiece.Geometry.HoleFeatures[0].Diameter, Is.EqualTo(BenchDrillBit.Presets[bit].Diameter));
            Assert.That(WorkshopStory.Evaluate(p.MachiningWorkpiece.Geometry, out var reason), Is.False); Assert.That(reason, Does.Contain("직경"));
        }
        [Test] public void WrongPositionExtraHoleAndRetryDoNotRestoreMaterial()
        {
            var inv = new InventoryState(); var p = AddPlate(inv); var s = Mount(inv, p);
            Drill(s, 30, 20); Drill(s, 70, 50);
            Assert.That(WorkshopStory.Evaluate(p.MachiningWorkpiece.Geometry, out var reason), Is.False); Assert.That(reason, Does.Contain("위치"));
            Drill(s, 20, 60); Assert.That(WorkshopStory.Evaluate(p.MachiningWorkpiece.Geometry, out reason), Is.False); Assert.That(reason, Does.Contain("추가"));
            s.Finish(); var replacement = AddPlate(inv); Assert.That(s.TryLoad(replacement), Is.True);
            Assert.That(p.MachiningWorkpiece.Geometry.HoleFeatures.Count, Is.EqualTo(3)); Assert.That(replacement.InstanceId, Is.Not.EqualTo(p.InstanceId));
        }
        [TestCase("Plate100x80", true)] [TestCase("SquareBar30", true)] [TestCase("RectangularBar20x40", true)]
        [TestCase("RoundBar50", false)] [TestCase("Tube40x3", false)]
        public void RealDefinitionsLoadOnlyCompatibleOwnedInstances(string path, bool compatible)
        {
            var inv = new InventoryState(); var d = Resources.Load<WorkpieceDefinition>("WorkpieceDefinitions/" + path);
            var item = d.CreateInstance(new ConditionProperties(), "region", "session"); Assert.That(inv.AddWorkpiece(item), Is.True);
            var s = new BenchDrillSession(inv, Definition);
            Assert.That(s.TryLoad(item), Is.False, "The unowned copy is not the inventory's instance.");
            var owned = inv.FindWorkpiece(item.InstanceId); Assert.That(s.TryLoad(owned), Is.EqualTo(compatible));
            if (compatible) s.Finish();
            Assert.That(owned.Availability, Is.EqualTo(WorkpieceAvailability.Available)); Assert.That(inv.Workpieces.Count, Is.EqualTo(1));
        }
        [Test] public void BoundariesClampsAndNonFiniteCommandsCannotRemoveMaterial()
        {
            var inv = new InventoryState(); var p = AddPlate(inv); var s = Mount(inv, p);
            Assert.That(s.Execute(BenchDrillCommand.Feed, 10), Is.False);
            Assert.That(s.Execute(BenchDrillCommand.MoveX, float.NaN), Is.False);
            Assert.That(s.Execute(BenchDrillCommand.Feed, float.PositiveInfinity), Is.False);
            Assert.That(p.MachiningWorkpiece.Geometry.HoleFeatures, Is.Empty);
        }
        [Test] public void LatheCannotTakeOrReleaseBenchReservation()
        {
            var inv = new InventoryState(); var plate = AddPlate(inv); var bench = Mount(inv, plate);
            var round = Resources.Load<WorkpieceDefinition>("WorkpieceDefinitions/RoundBar50").CreateInstance(new ConditionProperties(), "test", "test");
            Assert.That(inv.AddWorkpiece(round), Is.True);
            var lathe = new InventoryMachiningSession(inv, Definition);
            Assert.That(lathe.Active, Is.Null);
            Assert.That(lathe.TryBegin(round.InstanceId, _ => true), Is.False);
            lathe.End(); Assert.That(plate.Availability, Is.EqualTo(WorkpieceAvailability.InMachining));
            bench.Finish(); Assert.That(lathe.TryBegin(round.InstanceId, _ => true), Is.True);
            Assert.That(bench.TryLoad(plate), Is.False);
            lathe.End(); Assert.That(bench.TryLoad(plate), Is.True);
        }
        [Test] public void GatesOfferAcceptDeliveryAndSaveContinueUseActualOwnedGeometry()
        {
            var inv = new InventoryState(); var p = AddPlate(inv);
            var q = new Quest01Integration(inv); Assert.That(q.Story.Offer02(), Is.False); Assert.That(q.Story.Offer03(), Is.False);
            q.Quest.Data.State = QuestState.Completed;
            Assert.That(q.Story.Offer03(), Is.False); Assert.That(q.Story.Offer02(), Is.True);
            Assert.That(q.Story.DiscoverBench(), Is.False); Assert.That(q.Story.Accept02(), Is.True);
            Assert.That(q.Story.DiscoverBench(), Is.True); Assert.That(q.Story.RecoverBench(), Is.True); Assert.That(q.Story.InstallBench(), Is.True);
            Assert.That(q.Story.Data.Quest03, Is.EqualTo(WorkshopQuestStatus.NotStarted));
            Assert.That(q.Story.Offer03(), Is.True); Assert.That(q.Story.Deliver(p.InstanceId), Is.False);
            Assert.That(q.Story.Accept03(), Is.True); Assert.That(q.Story.Deliver(p.InstanceId), Is.False);
            var s = Mount(inv, p); Drill(s, 20, 20); Drill(s, 70, 50);
            s.SetSpindle(false); s.Execute(BenchDrillCommand.Feed, -100);
            Assert.That(s.Record(0, 0, 999), Is.True); Assert.That(p.MachiningMeasurements[0].PlayerValue, Is.EqualTo(999));
            Assert.That(p.MachiningMeasurements[0].InstrumentReadingValue, Is.EqualTo(6));
            string json = JsonUtility.ToJson(SaveGameComposer.Capture(q));
            var loaded = SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(json));
            var restored = loaded.Inventory.FindWorkpiece(p.InstanceId);
            Assert.That(restored.Availability, Is.EqualTo(WorkpieceAvailability.Available));
            for (int i = 0; i < 2; i++) Assert.That(JsonUtility.ToJson(restored.MachiningWorkpiece.Geometry.HoleFeatures[i]), Is.EqualTo(JsonUtility.ToJson(p.MachiningWorkpiece.Geometry.HoleFeatures[i])));
            Assert.That(restored.MachiningMeasurements[0].PlayerValue, Is.EqualTo(999));
            Assert.That(loaded.Story.Deliver(p.InstanceId), Is.True); Assert.That(loaded.Inventory.FindWorkpiece(p.InstanceId), Is.Null);
            loaded = SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(SaveGameComposer.Capture(loaded))));
            Assert.That(loaded.Story.Data.Quest03, Is.EqualTo(WorkshopQuestStatus.Completed)); Assert.That(loaded.Story.Data.BenchInstalled, Is.True);
        }
    }
}
