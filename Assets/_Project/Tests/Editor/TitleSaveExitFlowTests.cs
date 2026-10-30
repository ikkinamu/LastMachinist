using System;
using System.IO;
using LastMachinist.Core;
using LastMachinist.Integration;
using LastMachinist.Inventory;
using LastMachinist.Quest;
using LastMachinist.Save;
using NUnit.Framework;
using UnityEngine;

namespace LastMachinist.Tests
{
    public sealed class TitleSaveExitFlowTests
    {
        private static SaveGameData Data(string marker) => new SaveGameData { CurrentSceneId = marker };
        private static SaveGameStore Store() => new SaveGameStore(Path.Combine(Application.temporaryCachePath,
            "last-machinist-flow-" + Guid.NewGuid().ToString("N") + ".json"));

        [Test] public void AutoSaveRollsTwoGenerationsAndFallsBackFromCorruptNewest()
        {
            var store = Store();
            try
            {
                store.SaveAuto(Data("A")); store.SaveAuto(Data("B")); store.SaveAuto(Data("C"));
                Assert.That(File.Exists(store.AutoPath) && File.Exists(store.AutoBackupPath), Is.True);
                Assert.That(store.LoadLatestValid().CurrentSceneId, Is.EqualTo("C"));
                File.WriteAllText(store.AutoPath, "{corrupt");
                Assert.That(store.LoadLatestValid().CurrentSceneId, Is.EqualTo("B"));
                store.SaveAuto(Data("D")); // Corrupt C must not displace B.
                Assert.That(store.LoadLatestValid().CurrentSceneId, Is.EqualTo("D"));
                File.WriteAllText(store.AutoPath, "{corrupt");
                Assert.That(store.LoadLatestValid().CurrentSceneId, Is.EqualTo("B"));
            }
            finally { store.Delete(); }
        }

        [Test] public void ContinueRanksManualAndAutoByTimeAndSkipsTwoInvalidCandidates()
        {
            var store = Store();
            try
            {
                store.SaveAuto(Data("old-auto")); store.SaveAuto(Data("new-auto")); store.Save(Data("manual"));
                var now = DateTime.UtcNow;
                File.SetLastWriteTimeUtc(store.AutoBackupPath, now.AddMinutes(-3));
                File.SetLastWriteTimeUtc(store.AutoPath, now.AddMinutes(-2));
                File.SetLastWriteTimeUtc(store.SlotPath, now.AddMinutes(-1));
                Assert.That(store.LoadLatestValid().CurrentSceneId, Is.EqualTo("manual"));
                File.WriteAllText(store.SlotPath, "invalid");
                Assert.That(store.LoadLatestValid().CurrentSceneId, Is.EqualTo("new-auto"));
                File.WriteAllText(store.AutoPath, "invalid");
                Assert.That(store.LoadLatestValid().CurrentSceneId, Is.EqualTo("old-auto"));
                File.WriteAllText(store.AutoBackupPath, "invalid");
                Assert.That(store.HasAnySave(), Is.True);
                Assert.That(store.HasValidSave(), Is.False);
                Assert.That(store.LoadLatestValid(), Is.Null);
            }
            finally { store.Delete(); }
        }

        [Test] public void FailedSaveKeepsExistingManualAndConfirmedResetRemovesAllCandidates()
        {
            var store = Store();
            try
            {
                store.Save(Data("manual")); store.SaveAuto(Data("auto"));
                var bad = Data("bad"); bad.SaveVersion = -1;
                Assert.Throws<ArgumentException>(() => store.Save(bad));
                Assert.Throws<ArgumentException>(() => store.SaveAuto(bad));
                Assert.That(store.Load().CurrentSceneId, Is.EqualTo("manual"));
                Assert.That(store.LoadLatestValid().CurrentSceneId, Is.Not.EqualTo("bad"));
                store.Delete();
                Assert.That(store.HasAnySave(), Is.False);
            }
            finally { store.Delete(); }
        }

        [Test] public void ExitConfirmationOnlyQuitsAfterSuccessfulSaveOrExplicitDiscard()
        {
            var host = new GameObject("system menu test");
            try
            {
                var menu = host.AddComponent<GameplaySystemMenu>();
                int quits = 0; bool saveSucceeds = false;
                menu.Bind(() => saveSucceeds, () => quits++, () => true, null);
                menu.OpenMenu(); menu.RequestExit();
                Assert.That(menu.SaveAndExit(), Is.False);
                Assert.That(quits, Is.Zero);
                menu.CancelExit(); Assert.That(menu.ExitConfirmation, Is.False);
                menu.RequestExit(); saveSucceeds = true;
                Assert.That(menu.SaveAndExit(), Is.True);
                Assert.That(quits, Is.EqualTo(1));
                menu.CloseMenu(); menu.OpenMenu(); menu.RequestExit();
                Assert.That(menu.ExitWithoutSave(), Is.True);
                Assert.That(quits, Is.EqualTo(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test] public void TitleRequiresConfirmationBeforeDiscardingExistingProgress()
        {
            var host = new GameObject("title flow test");
            try
            {
                var title = host.AddComponent<MvpStartupOverlay>();
                int resets = 0, quits = 0;
                title.BindPersistence(null, () => true, () => { resets++; return true; }, () => quits++, () => true, () => true);
                Assert.That(title.TitleHasContinue, Is.True);
                title.RequestNewGame();
                Assert.That(title.NewGameConfirmationOpen, Is.True);
                Assert.That(resets, Is.Zero);
                title.CancelNewGame();
                Assert.That(resets, Is.Zero);
                title.RequestNewGame();
                Assert.That(title.ConfirmNewGame(), Is.True);
                Assert.That(resets, Is.EqualTo(1));
                title.RequestTitleExit(); title.CancelTitleExit();
                title.ConfirmTitleExit(); Assert.That(quits, Is.Zero);
                title.RequestTitleExit(); title.ConfirmTitleExit(); Assert.That(quits, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test] public void ContinueRestoresCompletedUnlocksIndividualGeometryAndRegionState()
        {
            var store = Store();
            try
            {
                var inventory = new InventoryState(); InitialWorkshopWorkpieces.EnsureCreated(inventory, "save-flow");
                var piece = inventory.Workpieces[1];
                piece.IdentificationState = IdentificationState.Identified;
                piece.Conditions.SurfaceCondition = 73;
                piece.MachiningWorkpiece.Geometry.RoundProfile.RemoveMaterial(98, 100, 0);
                var game = new Quest01Integration(inventory, new QuestRuntimeState { State = QuestState.Completed, PumpRepaired = true });
                Assert.That(game.LearnAbandonedWorkshopLocation(), Is.True);
                game.Regions.SetAccess(RegionProgression.AbandonedWorkshopId, false, "도로 붕괴");
                var order = game.RepeatableQuests.Data.Instances[0];
                game.RepeatableQuests.Accept(order.QuestInstanceId);
                store.SaveAuto(SaveGameComposer.Capture(game, "Workshop"));
                var restored = SaveGameComposer.Restore(store.LoadLatestValid());
                var savedPiece = restored.Inventory.FindWorkpiece(piece.InstanceId);
                Assert.That(savedPiece.MachiningWorkpiece.Geometry.RoundProfile.GetAxialLength(), Is.EqualTo(98));
                Assert.That(savedPiece.IdentificationState, Is.EqualTo(IdentificationState.Identified));
                Assert.That(savedPiece.Conditions.SurfaceCondition, Is.EqualTo(73));
                Assert.That(restored.EarlyGameUnlocked && restored.RepeatableQuests.IsUnlocked, Is.True);
                Assert.That(restored.RepeatableQuests.Find(order.QuestInstanceId).State, Is.EqualTo(RepeatableQuestStatus.Active));
                Assert.That(restored.Regions.Find(RegionProgression.AbandonedWorkshopId).DiscoveryState, Is.EqualTo(RegionDiscoveryState.Rumored));
                Assert.That(restored.Regions.Find(RegionProgression.AbandonedWorkshopId).IsAccessible, Is.False);
            }
            finally { store.Delete(); }
        }
    }
}
