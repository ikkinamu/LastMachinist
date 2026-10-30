using System.Collections;
using System.IO;
using LastMachinist.Core;
using LastMachinist.Integration;
using LastMachinist.Inventory;
using LastMachinist.Machining.BenchDrill;
using LastMachinist.Machining.Turning;
using LastMachinist.Quest;
using LastMachinist.Save;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace LastMachinist.Tests
{
    public sealed class BenchDrillPlayModeTests
    {
        private SceneAsset previous;
        [UnityTest] public IEnumerator BootBenchDrillQuestDeliveryAndRestore()
        {
            previous = EditorSceneManager.playModeStartScene;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/Boot.unity");
            yield return new EnterPlayMode(); yield return null; yield return null;
            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>(); bootstrap.enabled = false;
            var lathe = Object.FindFirstObjectByType<LathePrototype>();
            var world = Object.FindFirstObjectByType<MvpWorldPanel>();
            var overlay = Object.FindFirstObjectByType<MvpStartupOverlay>();
            var integration = new Quest01Integration(new InventoryState(), new QuestRuntimeState {
                State = QuestState.Completed, Prologue = PrologueStatus.Completed, HasReceivedVernier = true, PumpRepaired = true });
            var plate = BenchDrillPhase02Tests.AddPlate(integration.Inventory);
            integration.Story.Offer02(); integration.Story.Accept02(); integration.Story.DiscoverBench(); integration.Story.RecoverBench(); integration.Story.InstallBench();
            // A separate test fixture, never injected into production inventory at startup.
            string fixture = Path.GetFullPath(Path.Combine(Application.dataPath, "../../bench-rev1-start.json"));
            File.WriteAllText(fixture, JsonUtility.ToJson(SaveGameComposer.Capture(integration, "Workshop"), true));
            lathe.RestoreQuest(integration); world.Bind(integration, lathe); overlay.Resume(integration); lathe.IsDialogueOpen = () => false;
            Assert.That(integration.Story.Offer03(), Is.True); Assert.That(integration.Story.Accept03(), Is.True);
            lathe.OpenPage(HudPage.BenchDrill); Assert.That(lathe.CurrentPage, Is.EqualTo(HudPage.BenchDrill));
            Assert.That(lathe.BenchDrill.TryLoad(plate), Is.True);
            Assert.That(lathe.ActiveMachiningWorkpiece, Is.Null, "Bench reservation must not appear as lathe stock.");
            lathe.BenchDrill.Execute(BenchDrillCommand.Clamp); lathe.BenchDrill.Execute(BenchDrillCommand.SelectBit, 1); lathe.BenchDrill.Execute(BenchDrillCommand.SpindleOn);
            BenchDrillPhase02Tests.Drill(lathe.BenchDrill, 20, 20); yield return null;
            BenchDrillPhase02Tests.Drill(lathe.BenchDrill, 70, 50); yield return null;
            lathe.BenchDrill.Execute(BenchDrillCommand.SpindleOff); lathe.BenchDrill.Execute(BenchDrillCommand.Feed, -100);
            Assert.That(lathe.BenchDrill.Record(0, 0, 6), Is.True);
            lathe.SyncInventoryMeasurements();
            Assert.That(plate.MachiningMeasurements.Count, Is.EqualTo(1), "Lathe sync must not overwrite bench measurements.");
            lathe.OpenPage(HudPage.Quest); Assert.That(plate.Availability, Is.EqualTo(WorkpieceAvailability.Available));
            Assert.That(plate.MachiningMeasurements.Count, Is.EqualTo(1));
            Assert.That(integration.Story.Deliver(plate.InstanceId), Is.True);
            var restored = SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(SaveGameComposer.Capture(integration))));
            Assert.That(restored.Story.Data.Quest03, Is.EqualTo(WorkshopQuestStatus.Completed));
            Assert.That(restored.Inventory.FindWorkpiece(plate.InstanceId), Is.Null);
            // UnityTest already fails on unexpected Error/Exception logs. Boot's normal Log is allowed.
            yield return new ExitPlayMode();
            EditorSceneManager.playModeStartScene = previous;
        }
        [UnityTearDown] public IEnumerator RestoreEditor()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.playModeStartScene = previous;
        }
    }
}
