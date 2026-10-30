using System.Collections;
using System.Reflection;
using LastMachinist.Core;
using LastMachinist.Machining.Turning;
using LastMachinist.Quest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LastMachinist.Tests
{
    public sealed class ExplorationInspectionTests
    {
        [Test] public void SessionRecoveryRejectsDuplicateAndEmptyIds()
        {
            var state = new ExplorationRecoveryState();
            Assert.That(state.TryRecord(null), Is.False);
            Assert.That(state.TryRecord(" "), Is.False);
            Assert.That(state.TryRecord("sample-instance"), Is.True);
            Assert.That(state.TryRecord("sample-instance"), Is.False);
            Assert.That(state.ItemIds.Count, Is.EqualTo(1));
            Assert.That(new ExplorationRecoveryState().ItemIds.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator InspectCloseRecoverAndReturn()
        {
            yield return new EnterPlayMode();
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Boot.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            var boot = Object.FindAnyObjectByType<GameBootstrap>();
            boot.enabled = false;
            var flow = (QuestFlowController)typeof(GameBootstrap).GetField("questFlow", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(boot);
            flow.CompleteIntro(flow.IntroDialogue.Choices[0].Id); flow.AcknowledgeDialogue();
            Object.FindAnyObjectByType<MvpStartupOverlay>().Resume(boot.Quest01);
            var station = Object.FindAnyObjectByType<LathePrototype>();
            var stock = station.Controller.Workpiece;
            var inventory = station.PlayerInventory;
            Assert.That(inventory.ListWorkpieces.Count, Is.EqualTo(3));
            var region = Resources.Load<ExplorationRegionDefinition>("ExplorationRegions/TestYard");
            boot.Quest01.Quest.Data.State = QuestState.Completed;
            boot.Quest01.RepeatableQuests.Unlock();
            station.OpenPage(HudPage.Exploration);
            Assert.That(ExplorationSession.Enter(region, station), Is.True);
            var session = Object.FindAnyObjectByType<ExplorationSession>();
            var player = session.Player;
            var systemMenu = GameplaySystemMenu.Current;
            Assert.That(systemMenu, Is.Not.Null);
            var beforeMenu = player.transform.position;
            systemMenu.OpenMenu();
            Assert.That(player.SystemBlocked && !player.CanInteract, Is.True);
            player.MoveStep(Vector2.up, 1);
            player.ApplyLook(new Vector2(100, 100));
            Assert.That(player.transform.position, Is.EqualTo(beforeMenu));
            Assert.That(player.Pitch, Is.Zero);
            Assert.That(systemMenu.CanReturn, Is.True);
            systemMenu.RequestReturn();
            Assert.That(systemMenu.ReturnConfirmation, Is.True);
            systemMenu.CancelReturn();
            Assert.That(systemMenu.IsOpen && ExplorationSession.IsExploring, Is.True);
            systemMenu.CloseMenu();
            Assert.That(player.SystemBlocked, Is.False);
            var inspection = session.Inspection;
            var camera = player.GetComponentInChildren<Camera>();
            player.SetMenu(true);
            yield return null;
            var items = Object.FindObjectsByType<InspectableItem>();
            Assert.That(items.Length, Is.EqualTo(2));
            Assert.That(items[0].Instance.WorkpieceDefinitionId, Is.EqualTo(items[1].Instance.WorkpieceDefinitionId));
            Assert.That(items[0].Instance.InstanceId, Is.Not.EqualTo(items[1].Instance.InstanceId));
            Assert.That(items[0].Instance.Conditions.CorrosionCondition, Is.Not.EqualTo(items[1].Instance.Conditions.CorrosionCondition));
            Assert.That(items[0].Definition.InspectionProperties.Length, Is.Not.EqualTo(items[1].Definition.InspectionProperties.Length));
            Assert.That(items[0].Definition.ActualMaterialType, Is.Not.Empty);

            // Invoke the same event path as left click. Aim after Update to avoid real device input.
            player.SetMenu(false); yield return null;
            camera.transform.rotation = Quaternion.LookRotation(Vector3.up);
            player.RequestInteract(); Assert.That(inspection.IsOpen, Is.False, "Empty space");
            var first = items[0];
            first.transform.position = camera.transform.position + Vector3.forward * (region.InteractionRange + 5);
            camera.transform.rotation = Quaternion.identity; Physics.SyncTransforms();
            player.RequestInteract(); Assert.That(inspection.IsOpen, Is.False, "Out of range");
            first.transform.position = camera.transform.position + Vector3.forward * 2;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = camera.transform.position + Vector3.forward;
            wall.transform.localScale = new Vector3(3, 3, .2f);
            Physics.SyncTransforms();
            player.RequestInteract(); Assert.That(inspection.IsOpen, Is.False, "Wall occludes item");
            wall.SetActive(false);
            Physics.SyncTransforms();
            Assert.That(player.CanInteract, Is.True, "Input gate: menu=" + player.MenuOpen + ", inspection=" + player.InspectionOpen + ", frame=" + Time.frameCount + ", cursor=" + Cursor.lockState);
            player.RequestInteract();
            Assert.That(inspection.Target, Is.SameAs(first));
            Assert.That(first.gameObject.activeSelf, Is.True, "Inspect is not recovery");
            Assert.That(inspection.RecoveredItemIds.Count, Is.Zero);
            var originalDefinition = first.Definition;
            var lockedDefinition = Object.Instantiate(originalDefinition);
            lockedDefinition.CanRecover = false;
            first.Initialize(lockedDefinition);
            Assert.That(inspection.Recover(), Is.False, "CanRecover guard");
            Assert.That(first.gameObject.activeSelf, Is.True);
            first.Initialize(originalDefinition);
            Object.Destroy(lockedDefinition);
            Assert.That(player.InspectionOpen, Is.True);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);
            var position = player.transform.position;
            var rotation = player.transform.rotation;
            var eyeRotation = camera.transform.rotation;
            player.MoveStep(Vector2.up, 1); player.ApplyLook(new Vector2(100, 100));
            Assert.That(player.transform.position, Is.EqualTo(position));
            Assert.That(player.transform.rotation, Is.EqualTo(rotation));
            Assert.That(camera.transform.rotation, Is.EqualTo(eyeRotation));
            int requests = 0; player.InteractRequested += () => requests++;
            player.RequestInteract(); Assert.That(requests, Is.Zero, "Modal blocks world click");
            inspection.Close();
            Assert.That(inventory.ListWorkpieces.Count, Is.EqualTo(3), "Close preserves the three initial pieces without acquiring stock");
            Assert.That(first.gameObject.activeSelf, Is.True);
            Assert.That(player.InspectionOpen, Is.False);
            player.RequestInteract(); Assert.That(requests, Is.Zero, "Closing frame blocks click-through");
            yield return null;
            player.SetMenu(false); yield return null;
            camera.transform.LookAt(first.transform.position);
            player.RequestInteract(); Assert.That(inspection.IsOpen, Is.True, "Reinspect");
            Assert.That(inventory.AddWorkpiece(first.Instance), Is.True);
            Assert.That(inspection.Recover(), Is.False, "Duplicate inventory write must leave world intact");
            Assert.That(first.gameObject.activeSelf, Is.True);
            Assert.That(inspection.IsOpen, Is.True);
            Assert.That(inspection.RecoveredItemIds.Count, Is.Zero);
            Assert.That(inventory.RemoveWorkpiece(first.Instance.InstanceId), Is.True);
            Assert.That(inspection.Recover(), Is.True);
            Assert.That(inventory.FindWorkpiece(first.Instance.InstanceId), Is.Not.Null);
            Assert.That(first.gameObject.activeSelf, Is.False);
            Assert.That(inspection.RecoveredItemIds, Does.Contain(first.RecoveryKey));
            Assert.That(inspection.Recover(), Is.False, "No double recovery");
            Assert.That(inspection.RecoveredItemIds.Count, Is.EqualTo(1));
            player.RequestInteract(); Assert.That(inspection.IsOpen, Is.False);
            yield return null;
            player.SetMenu(false); yield return null;
            var second = items[1];
            second.transform.position = camera.transform.position + Vector3.forward * 2;
            camera.transform.rotation = Quaternion.identity; Physics.SyncTransforms();
            player.RequestInteract(); Assert.That(inspection.Target, Is.SameAs(second));
            yield return null; // Exercise IMGUI layout/repaint with the second property count.
            Assert.That(inspection.Recover(), Is.True);
            Assert.That(inspection.RecoveredItemIds.Count, Is.EqualTo(2));
            Assert.That(inventory.ListWorkpieces.Count, Is.EqualTo(5));
            Assert.That(player.InspectionOpen, Is.False);
            bool savedAfterReturn = false;
            System.Action checkpoint = () => {
                if (!ExplorationSession.IsExploring)
                {
                    Assert.That(station.CurrentPage, Is.EqualTo(HudPage.Workshop));
                    var snapshot = LastMachinist.Save.SaveGameComposer.Capture(boot.Quest01, "Workshop");
                    var restored = LastMachinist.Save.SaveGameComposer.Restore(
                        JsonUtility.FromJson<LastMachinist.Save.SaveGameData>(JsonUtility.ToJson(snapshot)));
                    Assert.That(restored.Inventory.ListWorkpieces.Count, Is.EqualTo(5));
                    savedAfterReturn = true;
                }
            };
            ExplorationSession.AutoSaveRequested += checkpoint;
            systemMenu.OpenMenu(); systemMenu.RequestReturn();
            Assert.That(systemMenu.ConfirmReturn(), Is.True);
            Assert.That(systemMenu.ConfirmReturn(), Is.False);
            for (int i = 0; i < 120 && ExplorationSession.IsExploring; i++) yield return null;
            ExplorationSession.AutoSaveRequested -= checkpoint;
            Assert.That(savedAfterReturn, Is.True);
            Assert.That(ExplorationSession.IsExploring, Is.False);
            Assert.That(station.isActiveAndEnabled, Is.True);
            Assert.That(station.CurrentPage, Is.EqualTo(HudPage.Workshop));
            Assert.That(station.Controller.Workpiece, Is.SameAs(stock));
            station.OpenPage(HudPage.Inventory);
            Assert.That(station.CurrentPage, Is.EqualTo(HudPage.Inventory));
            Assert.That(station.PlayerInventory.ListWorkpieces.Count, Is.EqualTo(5));
            yield return null;
            // Phase 04: recovered inventory instance -> real lathe -> cut -> measure -> return.
            var owned = inventory.ListWorkpieces[3];
            var other = inventory.ListWorkpieces[4];
            var questStateBefore = boot.Quest01.Quest.Data.State;
            station.OpenPage(HudPage.MachineSelect);
            Assert.That(station.MachiningCandidates().Count, Is.EqualTo(5));
            Assert.That(station.SelectInventoryWorkpiece(owned.InstanceId), Is.True);
            station.CancelInventoryPreparation();
            Assert.That(station.StartInventoryMachining(), Is.False);
            Assert.That(owned.Availability, Is.EqualTo(LastMachinist.Inventory.WorkpieceAvailability.Available));
            Assert.That(station.SelectInventoryWorkpiece(other.InstanceId), Is.True);
            Assert.That(station.SelectInventoryWorkpiece(owned.InstanceId), Is.True);
            Assert.That(station.StartInventoryMachining(), Is.True);
            Assert.That(station.ActiveMachiningWorkpiece, Is.SameAs(owned));
            Assert.That(station.Controller.Workpiece, Is.SameAs(owned.MachiningWorkpiece));
            Assert.That(station.Controller.Profile.TotalLength, Is.EqualTo(300));
            Assert.That(station.Controller.Profile.RadiusAt(150) * 2, Is.EqualTo(50));
            Assert.That(station.Controller.Workpiece.InstanceId, Is.EqualTo(owned.InstanceId));
            Assert.That(station.InventoryMaterialLabel(), Is.EqualTo("재료 미식별"));
            Assert.That(station.StartInventoryMachining(), Is.False);
            foreach (LatheViewMode mode in System.Enum.GetValues(typeof(LatheViewMode)))
            {
                station.SetCameraView(mode);
                Assert.That(Camera.main.orthographic, Is.True);
            }
            station.Router.SetMovementScale(MovementScale.Medium);
            station.Router.ApplyHandwheelRotation(Axis.Z, LatheDialMath.MillimetresToDegrees(150 - station.Controller.ToolZ, MovementScale.Medium));
            station.Router.SetSpindleRunning(true);
            station.Router.ApplyHandwheelRotation(Axis.X, -90);
            station.Router.SetSpindleRunning(false);
            Assert.That(owned.MachiningWorkpiece.Geometry.RoundProfile.RadiusAt(150), Is.EqualTo(20));
            Assert.That(station.FinishMachiningAndMeasure(), Is.True);
            Assert.That(station.CurrentPage, Is.EqualTo(HudPage.Measurement));
            var point = new LastMachinist.Measurement.MeasurementPoint("StockDiameter", LastMachinist.Measurement.MeasurementPointType.Diameter, 0, 300);
            station.CurrentMeasurement.Select(point, "old_vernier", true);
            station.CurrentMeasurement.Record(999);
            station.SyncInventoryMeasurements();
            Assert.That(owned.MachiningMeasurements[0].PlayerValue, Is.EqualTo(999));
            Assert.That(owned.MachiningMeasurements[0].InstrumentReadingValue, Is.EqualTo(40));
            station.OpenPage(HudPage.Inventory);
            Assert.That(station.ActiveMachiningWorkpiece, Is.Null);
            Assert.That(owned.Availability, Is.EqualTo(LastMachinist.Inventory.WorkpieceAvailability.Available));
            Assert.That(station.Controller.Workpiece, Is.SameAs(stock), "Tutorial stock is restored without losing its state");
            Assert.That(boot.Quest01.Quest.Data.State, Is.EqualTo(questStateBefore));
            Assert.That(station.SelectInventoryWorkpiece(owned.InstanceId), Is.True);
            Assert.That(station.StartInventoryMachining(), Is.True);
            Assert.That(station.Controller.Profile.RadiusAt(150), Is.EqualTo(20), "Remount must not regenerate removed material");
            Assert.That(station.CurrentMeasurement.Records["StockDiameter"].PlayerValue, Is.EqualTo(999));
            station.OpenPage(HudPage.Workshop);
            Assert.That(ExplorationSession.Enter(region, station), Is.True);
            session = Object.FindAnyObjectByType<ExplorationSession>();
            Assert.That(session.Inspection.RecoveredItemIds.Count, Is.Zero, "New session resets its recovery log");
            Assert.That(station.PlayerInventory.ListWorkpieces.Count, Is.EqualTo(5), "Initial and recovered workpieces persist on reentry");
            Assert.That(Object.FindObjectsByType<InspectableItem>().Length, Is.EqualTo(2));
            session.ReturnToWorkshop();
            for (int i = 0; i < 120 && ExplorationSession.IsExploring; i++) yield return null;
            var repeat = station.RepeatableQuests;
            var order = repeat.Data.Instances[0];
            Assert.That(station.PrepareRepeatableQuest(order.QuestInstanceId), Is.False);
            Assert.That(repeat.Accept(order.QuestInstanceId), Is.True);
            station.OpenPage(HudPage.Quest);
            typeof(LathePrototype).GetField("repeatableQuestTab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(station, true);
            typeof(LathePrototype).GetField("selectedRepeatableQuestId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(station, order.QuestInstanceId);
            yield return null; yield return null;
            Assert.That(station.PrepareRepeatableQuest(order.QuestInstanceId), Is.True);
            yield return null;
            Assert.That(station.SelectInventoryWorkpiece(other.InstanceId), Is.True);
            Assert.That(station.StartInventoryMachining(), Is.True);
            Assert.That(order.AssignedWorkpieceInstanceId, Is.EqualTo(other.InstanceId));
            Phase06RevisionTests.Part(station.Controller,station.Router,order.GeneratedTargetGeometry.RequiredTotalLength);
            Assert.That(station.CompleteParting(),Is.True);
            Assert.That(order.AssignedWorkpieceInstanceId,Is.Not.EqualTo(other.InstanceId));
            Assert.That(station.PrepareRepeatableQuest(order.QuestInstanceId),Is.True);
            Assert.That(station.SelectInventoryWorkpiece(order.AssignedWorkpieceInstanceId),Is.True);
            Assert.That(station.StartInventoryMachining(),Is.True);
            RepeatableQuestTests.Machine(station.Controller, station.Router, order.GeneratedTargetGeometry);
            Assert.That(station.FinishMachiningAndMeasure(), Is.True);
            var points = (LastMachinist.Measurement.MeasurementPoint[])typeof(LathePrototype).GetField("measurementPoints", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(station);
            Assert.That(points.Length, Is.EqualTo(6));
            station.CurrentMeasurement.Select(points[0], "old_vernier", true);
            station.CurrentMeasurement.Record(999);
            station.SyncInventoryMeasurements();
            yield return null;
            station.OpenPage(HudPage.Quest);
            Assert.That(repeat.Deliver(order.QuestInstanceId), Is.True, order.EvaluationFeedback);
            Assert.That(order.DeliveredWorkpiece.MachiningMeasurements[0].PlayerValue, Is.EqualTo(999));
            Assert.That(boot.Quest01.Quest.Data.State, Is.EqualTo(questStateBefore));
            yield return null;
            // Phase 06: retained inventory workpiece, real station visuals and common router.
            station.OpenPage(HudPage.MachineSelect);
            Assert.That(station.SelectInventoryWorkpiece(owned.InstanceId), Is.True);
            Assert.That(station.StartInventoryMachining(), Is.True);
            var view=station.GetComponentInChildren<TurningWorkpieceView>();
            int revision=view.Revision;
            station.Router.SelectTailstockTool(TailstockToolKind.CenterDrill);
            yield return null;
            Assert.That(station.VisibleTailstockTool, Is.EqualTo(TailstockToolKind.CenterDrill));
            Assert.That(station.Controller.ActiveToolLabel, Does.Contain("센터드릴"));
            InternalMachiningTests.Drill(station.Controller,station.Router);
            yield return null;
            Assert.That(station.VisibleTailstockTool, Is.EqualTo(TailstockToolKind.Drill10));
            Assert.That(view.Revision, Is.GreaterThan(revision));
            Assert.That(station.Controller.Profile.InnerTrueDiameter(290), Is.EqualTo(10));
            foreach(LatheViewMode mode in System.Enum.GetValues(typeof(LatheViewMode)))
            { station.SetCameraView(mode); yield return null; Assert.That(Camera.main.orthographic,Is.True); }
            station.Router.SelectTool(ToolKind.Boring);
            station.Router.SetMovementScale(MovementScale.Fine);
            station.Router.ApplyHandwheelRotation(Axis.X,(3-station.Controller.ToolX)*90);
            station.Router.ApplyHandwheelRotation(Axis.Z,(299.5f-station.Controller.ToolZ)*90);
            station.Router.SetSpindleRunning(true);
            station.Router.ApplyHandwheelRotation(Axis.X,(7-station.Controller.ToolX)*90);
            station.Router.ApplyHandwheelRotation(Axis.Z,(290-station.Controller.ToolZ)*90);
            station.Router.SetSpindleRunning(false);
            yield return null;
            Assert.That(station.Controller.Profile.InnerTrueDiameter(290),Is.EqualTo(14));
            Assert.That(station.Controller.ActiveToolLabel,Does.Contain("내경 바이트"));
            station.ToggleMiniTab(LatheMiniTab.Tools);
            Assert.That(station.OpenMiniTab,Is.EqualTo(LatheMiniTab.Tools));
            station.ToggleMiniTab(LatheMiniTab.Tailstock);
            Assert.That(station.OpenMiniTab,Is.EqualTo(LatheMiniTab.Tailstock));
            station.ToggleMiniTab(LatheMiniTab.Tailstock);
            Assert.That(station.OpenMiniTab,Is.EqualTo(LatheMiniTab.None));
            string geometryBeforeToggle=JsonUtility.ToJson(station.Controller.Workpiece);
            float quillBeforeToggle=station.Controller.Tailstock.QuillExtension;
            station.SetTailstockVisualVisible(false); yield return null;
            Assert.That(station.TailstockVisualVisible,Is.False);
            Assert.That(station.transform.Find("심압대 본체").GetComponent<Renderer>().enabled,Is.False);
            station.SetTailstockVisualVisible(true); yield return null;
            Assert.That(station.transform.Find("심압대 본체").GetComponent<Renderer>().enabled,Is.True);
            Assert.That(station.Controller.Tailstock.QuillExtension,Is.EqualTo(quillBeforeToggle));
            Assert.That(JsonUtility.ToJson(station.Controller.Workpiece),Is.EqualTo(geometryBeforeToggle));
            station.Router.SelectTailstockTool(TailstockToolKind.Drill10);yield return null;
            Assert.That(station.ShowsCarriageX,Is.False);
            station.Router.SelectTool(ToolKind.Roughing);yield return null;
            Assert.That(station.ShowsCarriageX,Is.True);
            station.OpenPage(HudPage.Workshop);
            Assert.That(owned.MachiningWorkpiece.Geometry.RoundProfile.InnerTrueDiameter(290),Is.EqualTo(14));
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        }
    }
}
