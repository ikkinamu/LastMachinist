using System.Collections;
using System.Reflection;
using LastMachinist.Core;
using LastMachinist.Machining.Turning;
using LastMachinist.Quest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using System.Linq;
using LastMachinist.Save;
using LastMachinist.Integration;

namespace LastMachinist.Tests
{
    public sealed class ExplorationFoundationTests
    {
        [Test] public void RegionRequiresUnlockAndIdentity()
        {
            var region = ScriptableObject.CreateInstance<ExplorationRegionDefinition>();
            Assert.That(region.CanEnter, Is.False);
            region.RegionId = "test"; region.SceneName = "test";
            Assert.That(region.CanEnter, Is.True);
            region.IsUnlocked = false; Assert.That(region.CanEnter, Is.False);
            Object.DestroyImmediate(region);
        }

        [Test] public void RegionProgressionSeparatesDiscoveryAccessAndMajorDiscovery()
        {
            var region = ScriptableObject.CreateInstance<ExplorationRegionDefinition>();
            region.RegionId = "ruin"; region.SceneName = "Ruin"; region.InitialDiscoveryState = RegionDiscoveryState.Hidden;
            region.RumoredName = "폐허로 추정되는 곳"; region.RumoredInformation = new[] { "위치를 전해 들었다." };
            region.MajorDiscoveries = new[] { new MajorDiscoveryDefinition { Id = "machine", DisplayName = "소형 공작기계" } };
            var progression = new RegionProgression(new[] { region });
            Assert.That(progression.IsVisible("ruin"), Is.False);
            Assert.That(progression.DiscoverRegion("ruin", RegionDiscoveryState.Rumored), Is.True);
            Assert.That(progression.IsVisible("ruin"), Is.True);
            Assert.That(progression.CanEnter("ruin"), Is.True);
            Assert.That(progression.SetAccess("ruin", false, "길이 막혀 있습니다."), Is.True);
            Assert.That(progression.CanEnter("ruin"), Is.False);
            Assert.That(progression.Information(region), Does.Contain("접근 불가").And.Contain("위치를 전해 들었다.").And.Not.Contain("소형 공작기계"));
            Assert.That(progression.RecordMajorDiscovery("ruin", "machine"), Is.False);
            Assert.That(progression.SetAccess("ruin", true), Is.True);
            Assert.That(progression.DiscoverRegion("ruin", RegionDiscoveryState.Discovered), Is.True);
            Assert.That(progression.RecordMajorDiscovery("ruin", "machine"), Is.True);
            Assert.That(progression.Information(region), Does.Contain("소형 공작기계"));
            Object.DestroyImmediate(region);
        }

        [Test] public void Quest01CompletionRevealsAbandonedWorkshopWithoutDrillSpoiler()
        {
            var regions = Resources.LoadAll<ExplorationRegionDefinition>("ExplorationRegions");
            var progression = new RegionProgression(regions);
            Assert.That(progression.IsVisible(RegionProgression.AbandonedWorkshopId), Is.False);
            progression.ApplyQuestCompletion(RegionProgression.PumpQuestId);
            var abandoned = regions.Single(r => r.RegionId == RegionProgression.AbandonedWorkshopId);
            Assert.That(progression.Find(abandoned.RegionId).DiscoveryState, Is.EqualTo(RegionDiscoveryState.Rumored));
            Assert.That(progression.Information(abandoned), Does.Contain("박성철").And.Not.Contain("탁상드릴"));
            Assert.That(progression.Find(abandoned.RegionId).MajorDiscoveryIds, Is.Empty);
        }

        [Test] public void RegionProgressionSaveLoadPreservesRumoredAndAccessibleState()
        {
            var regions = Resources.LoadAll<ExplorationRegionDefinition>("ExplorationRegions");
            var progression = new RegionProgression(regions); progression.ApplyQuestCompletion(RegionProgression.PumpQuestId);
            progression.SetAccess(RegionProgression.AbandonedWorkshopId, false, "도로 붕괴");
            var data = new SaveGameData { Regions = progression.Data };
            var json = JsonUtility.ToJson(data); var loaded = JsonUtility.FromJson<SaveGameData>(json);
            loaded.Regions.Validate();
            var restored = new RegionProgression(regions, loaded.Regions);
            Assert.That(restored.Find(RegionProgression.AbandonedWorkshopId).DiscoveryState, Is.EqualTo(RegionDiscoveryState.Rumored));
            Assert.That(restored.CanEnter(RegionProgression.AbandonedWorkshopId), Is.False);
            Assert.That(restored.Find(RegionProgression.AbandonedWorkshopId).AccessReason, Is.EqualTo("도로 붕괴"));
        }
        [UnityTest] public IEnumerator WorkshopExplorationReturnAndReentry()
        {
            yield return new EnterPlayMode();
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Boot.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            var boot = Object.FindAnyObjectByType<GameBootstrap>();
            boot.enabled = false; // Do not write the player's checkpoint slot.
            var station = Object.FindAnyObjectByType<LathePrototype>();
            var flow = (QuestFlowController)typeof(GameBootstrap).GetField("questFlow", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(boot);
            Assert.That(flow.CompleteIntro(flow.IntroDialogue.Choices[0].Id), Is.True);
            flow.AcknowledgeDialogue();
            Object.FindAnyObjectByType<MvpStartupOverlay>().Resume(boot.Quest01);
            station.OpenPage(HudPage.Exploration);
            var region = System.Array.Find(Resources.LoadAll<ExplorationRegionDefinition>("ExplorationRegions"), r => r.RegionId == "test_yard");
            var questBefore = boot.Quest01.Quest.Data.State;
            var stockBefore = station.Controller.Workpiece;
            Assert.That(ExplorationSession.Enter(null, station), Is.False);
            for (int loop = 0; loop < 2; loop++)
            {
                Assert.That(ExplorationSession.Enter(region, station), Is.True);
                Assert.That(ExplorationSession.Enter(region, station), Is.False);
                var session = Object.FindAnyObjectByType<ExplorationSession>();
                var player = session.Player;
                player.SetMenu(true); // Direct test steps, no real cursor dependency.
                Assert.That(player.transform.position, Is.EqualTo(region.SpawnPosition));
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(region.SceneName));
                Assert.That(player.GetComponentInChildren<Camera>().orthographic, Is.False);
                Physics.SyncTransforms();
                for (int i = 0; i < 180; i++) player.MoveStep(Vector2.zero, 1f / 60);
                Assert.That(player.transform.position.y, Is.InRange(-0.1f, 0.2f));
                var start = player.transform.position;
                for (int i = 0; i < 30; i++) player.MoveStep(Vector2.up, 1f / 60);
                Assert.That(player.transform.position.z, Is.GreaterThan(start.z + 1));
                for (int i = 0; i < 30; i++) player.MoveStep(Vector2.down, 1f / 60);
                Assert.That(player.transform.position.z, Is.EqualTo(start.z).Within(0.1));
                for (int i = 0; i < 30; i++) player.MoveStep(Vector2.right, 1f / 60);
                Assert.That(player.transform.position.x, Is.GreaterThan(start.x + 1));
                for (int i = 0; i < 30; i++) player.MoveStep(Vector2.left, 1f / 60);
                Assert.That(player.transform.position.x, Is.EqualTo(start.x).Within(0.1));
                for (int i = 0; i < 900; i++) player.MoveStep(Vector2.up, 1f / 60);
                Assert.That(player.transform.position.z, Is.InRange(10.5f, 11.5f));
                player.ApplyLook(new Vector2(90 / region.MouseSensitivity, 100000));
                Assert.That(Mathf.Abs(player.Pitch), Is.LessThanOrEqualTo(85));
                Assert.That(Vector3.Dot(player.transform.forward, Vector3.right), Is.GreaterThan(0.99f));
                int interactions = 0; player.InteractRequested += () => interactions++;
                player.SetMenu(false);
                yield return null;
                Assert.That(player.CanInteract, Is.True, "Input gate: menu=" + player.MenuOpen + ", frame=" + Time.frameCount + ", cursor=" + Cursor.lockState);
                player.RequestInteract(); Assert.That(interactions, Is.EqualTo(1));
                session.ReturnToWorkshop();
                for (int i = 0; i < 120 && ExplorationSession.IsExploring; i++) yield return null;
                Assert.That(ExplorationSession.IsExploring, Is.False);
                Assert.That(station.isActiveAndEnabled, Is.True);
                Assert.That(station.CurrentPage, Is.EqualTo(HudPage.Workshop));
                Assert.That(boot.Quest01.Quest.Data.State, Is.EqualTo(questBefore));
                Assert.That(station.Controller.Workpiece, Is.SameAs(stockBefore));
                Assert.That(SceneManager.GetSceneByName(region.SceneName).isLoaded, Is.False);
                station.OpenPage(HudPage.Exploration);
            }
            yield return new ExitPlayMode();
        }
    }
}
