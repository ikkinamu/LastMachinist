using System.Linq;
using LastMachinist.Core;
using LastMachinist.Integration;
using LastMachinist.Inventory;
using LastMachinist.Quest;
using LastMachinist.Save;
using NUnit.Framework;
using UnityEngine;

namespace LastMachinist.Tests
{
    public sealed class CrossPhaseProgressionGateTests
    {
        private static Quest01Integration NewGame()
        {
            var inventory = new InventoryState();
            InitialWorkshopWorkpieces.EnsureCreated(inventory, "cross-phase-test");
            return new Quest01Integration(inventory);
        }

        private static Quest01Integration RoundTrip(Quest01Integration game)
            => SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(SaveGameComposer.Capture(game))));

        [Test] public void ThreeStarterPiecesAreIndependentHundredMillimetreGeometry()
        {
            var game = NewGame();
            var pieces = game.Inventory.Workpieces;
            Assert.That(pieces.Count, Is.EqualTo(3));
            Assert.That(pieces.Select(p => p.InstanceId).Distinct().Count(), Is.EqualTo(3));
            Assert.That(pieces.All(p => p.HasMachiningGeometry && p.MachiningWorkpiece.Geometry.RoundProfile.GetAxialLength() == 100), Is.True);
            var definition = Resources.Load<WorkpieceDefinition>("WorkpieceDefinitions/RoundBar50");
            Assert.That(definition.NominalDimensions.Length, Is.EqualTo(300), "The stock definition is unchanged.");
            pieces[1].MachiningWorkpiece.Geometry.RoundProfile.RemoveMaterial(98, 100, 0);
            Assert.That(InventoryWorkpieceGeometry.TryGetActualLength(pieces[1], definition, out float length), Is.True);
            Assert.That(length, Is.EqualTo(98));
            Assert.That(pieces[0].MachiningWorkpiece.Geometry.RoundProfile.GetAxialLength(), Is.EqualTo(100));
            Assert.That(pieces[2].MachiningWorkpiece.Geometry.RoundProfile.GetAxialLength(), Is.EqualTo(100));
            Assert.That(WorkpieceInventoryPresentation.ListEntry(pieces[1], definition), Does.Contain("98.0 mm"));
        }

        [Test] public void IncompleteQuestLocksBothSystemsEvenWithOlderSavedOffers()
        {
            var game = NewGame();
            Assert.That(game.EarlyGameUnlocked, Is.False);
            Assert.That(game.RepeatableQuests.Data.Instances, Is.Empty);
            Assert.That(game.LearnAbandonedWorkshopLocation(), Is.False);
            game.Quest.Data.State = QuestState.Machining;
            var oldOffers = new RepeatableQuestService(game.Inventory).Data;
            game.RestoreRepeatableQuests(oldOffers);
            Assert.That(game.RepeatableQuests.IsUnlocked, Is.False);
            Assert.That(game.RepeatableQuests.Accept(oldOffers.Instances[0].QuestInstanceId), Is.False);
            game = RoundTrip(game);
            Assert.That(game.EarlyGameUnlocked, Is.False);
            Assert.That(game.RepeatableQuests.Accept(game.RepeatableQuests.Data.Instances[0].QuestInstanceId), Is.False);
            Assert.That(game.Regions.IsVisible(RegionProgression.AbandonedWorkshopId), Is.False);
        }

        [Test] public void CompletionUnlocksBothWithoutDiscoveringHiddenWorkshop()
        {
            var game = NewGame();
            game.Quest.Data.State = QuestState.Installing;
            Assert.That(game.CompleteInstallation(), Is.True);
            Assert.That(game.EarlyGameUnlocked, Is.True);
            Assert.That(game.RepeatableQuests.Data.Instances.Count, Is.EqualTo(3));
            Assert.That(game.Regions.Find(RegionProgression.AbandonedWorkshopId).DiscoveryState, Is.EqualTo(RegionDiscoveryState.Hidden));
            var order = game.RepeatableQuests.Data.Instances[0];
            Assert.That(game.RepeatableQuests.Accept(order.QuestInstanceId), Is.True, "Repeatables do not depend on exploration.");
            game = RoundTrip(game);
            Assert.That(game.EarlyGameUnlocked && game.RepeatableQuests.IsUnlocked, Is.True);
            Assert.That(game.RepeatableQuests.Find(order.QuestInstanceId).State, Is.EqualTo(RepeatableQuestStatus.Active));
            Assert.That(game.Regions.Find(RegionProgression.AbandonedWorkshopId).DiscoveryState, Is.EqualTo(RegionDiscoveryState.Hidden));
            Assert.That(game.LearnAbandonedWorkshopLocation(), Is.True);
            Assert.That(game.Regions.Find(RegionProgression.AbandonedWorkshopId).DiscoveryState, Is.EqualTo(RegionDiscoveryState.Rumored));
            game = RoundTrip(game);
            Assert.That(game.Regions.Find(RegionProgression.AbandonedWorkshopId).DiscoveryState, Is.EqualTo(RegionDiscoveryState.Rumored));
            Assert.That(game.RepeatableQuests.Find(order.QuestInstanceId).State, Is.EqualTo(RepeatableQuestStatus.Active));
        }

        [Test] public void ExistingCompletedSaveUnlocksAndLockedSaveKeepsMachinedLength()
        {
            var locked = NewGame();
            locked.Inventory.Workpieces[1].MachiningWorkpiece.Geometry.RoundProfile.RemoveMaterial(98, 100, 0);
            locked = RoundTrip(locked);
            Assert.That(locked.Inventory.Workpieces[1].MachiningWorkpiece.Geometry.RoundProfile.GetAxialLength(), Is.EqualTo(98));
            Assert.That(locked.EarlyGameUnlocked || locked.RepeatableQuests.IsUnlocked, Is.False);

            var old = new SaveGameData();
            old.Quest.State = QuestState.Completed; old.Quest.PumpRepaired = true; old.PumpRepaired = true;
            var completed = SaveGameComposer.Restore(old);
            Assert.That(completed.EarlyGameUnlocked, Is.True);
            Assert.That(completed.RepeatableQuests.Data.Instances.Count, Is.EqualTo(3));
            Assert.That(completed.Regions.Find(RegionProgression.AbandonedWorkshopId).DiscoveryState, Is.EqualTo(RegionDiscoveryState.Hidden));
        }
    }
}
