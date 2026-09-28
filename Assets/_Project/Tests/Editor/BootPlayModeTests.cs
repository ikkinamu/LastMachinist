using System.Collections;
using System.Reflection;
using LastMachinist.Core;
using LastMachinist.Evaluation;
using LastMachinist.Save;
using LastMachinist.Machining.Turning;
using LastMachinist.Measurement;
using LastMachinist.Quest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LastMachinist.Tests
{
    public sealed class BootPlayModeTests
    {
        private SceneAsset previousStartScene;
        private bool startSceneChanged;
        [UnityTest]
        public IEnumerator BootSceneMachiningAndInstallationRestore()
        {
            previousStartScene = EditorSceneManager.playModeStartScene;
            startSceneChanged = true;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/Boot.unity");
            Assert.That(EditorSceneManager.playModeStartScene, Is.Not.Null);
            yield return new EnterPlayMode();
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo("Assets/_Project/Scenes/Boot.unity"));
            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.Quest01, Is.Not.Null);
            // No checkpoint writes to the player's persistent slot during the test.
            bootstrap.enabled = false;
            var lathe = Object.FindFirstObjectByType<LathePrototype>();
            var world = Object.FindFirstObjectByType<MvpWorldPanel>();
            var overlay = Object.FindFirstObjectByType<MvpStartupOverlay>();
            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.isActiveAndEnabled, Is.True, "Existing IMGUI title must be active without a Canvas.");
            Assert.That(lathe.enabled, Is.False);
            Assert.That(world.enabled, Is.False);
            foreach (var renderer in lathe.GetComponentsInChildren<Renderer>(true)) Assert.That(renderer.enabled, Is.False);
            var flow = (QuestFlowController)typeof(GameBootstrap).GetField("questFlow", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bootstrap);
            Assert.That(flow.IntroDialogue.Choices.Count, Is.EqualTo(3));
            Assert.That(flow.CompleteIntro(flow.IntroDialogue.Choices[0].Id), Is.True);
            var quest = bootstrap.Quest01;
            overlay.Resume(quest);
            flow.AcknowledgeDialogue();
            Assert.That(quest.Quest.Data.State, Is.EqualTo(QuestState.Offered));
            Assert.That(quest.GoToFarm(), Is.False);
            Assert.That(quest.Quest.Accept(), Is.True);
            Assert.That(quest.GoToFarm(), Is.True); world.RestoreLocation("Farm");
            Assert.That(lathe.gameObject.activeSelf, Is.False);
            Assert.That(quest.InspectPump(), Is.True); flow.AcknowledgeDialogue();
            Assert.That(quest.RecoverPrototypeBrokenShaft(), Is.True);
            Assert.That(quest.ReturnToWorkshop(), Is.True); world.RestoreLocation("Workshop");
            Assert.That(quest.AttemptRuler(), Is.True); flow.AcknowledgeDialogue();
            Assert.That(quest.AcquireVernier(), Is.True); flow.AcknowledgeDialogue();
            Assert.That(quest.BeginBrokenShaftInspection(), Is.True);
            foreach (var point in new[] {
                new MeasurementPoint("L1_Diameter", MeasurementPointType.Diameter, 0, 30),
                new MeasurementPoint("L1_Length", MeasurementPointType.Length, 0, 30),
                new MeasurementPoint("L2_Diameter", MeasurementPointType.Diameter, 30, 70),
                new MeasurementPoint("L2_Length", MeasurementPointType.Length, 30, 70),
                new MeasurementPoint("L3_Diameter", MeasurementPointType.Diameter, 70, 100) })
            {
                quest.Measurement.Select(point, "old_vernier", true);
                Assert.That(quest.RecordMeasurement(quest.Measurement.Record(999)), Is.True);
            }
            Assert.That(quest.ObserveL3Wear() && quest.InferShapeFromMemo() && quest.Quest.SelectTurning() && quest.SelectMaterial(), Is.True);
            yield return null;
            yield return null;
            Assert.That(lathe.Controller.Workpiece, Is.SameAs(quest.CurrentWorkpiece));
            Assert.That(lathe.Controller.SpindleRunning, Is.False);
            Assert.That(lathe.gameObject.activeSelf && lathe.enabled, Is.True);
            Assert.That(lathe.GetComponentInChildren<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(0));
            Assert.That(quest.BeginMachining(), Is.True);
            Assert.That(lathe.CurrentPage, Is.EqualTo(HudPage.MachineSelect));
            lathe.OpenPage(HudPage.Lathe);
            var router = lathe.Router;
            router.SetMovementScale(MovementScale.Medium);
            router.ApplyHandwheelRotation(Axis.Z, LatheDialMath.MillimetresToDegrees(101 - lathe.Controller.ToolZ, MovementScale.Medium));
            router.SetSpindleRunning(true);
            Assert.That(lathe.FinishMachiningAndMeasure(), Is.False);
            router.ApplyHandwheelRotation(Axis.X, LatheDialMath.MillimetresToDegrees(20 - lathe.Controller.ToolX, MovementScale.Medium));
            router.ApplyHandwheelRotation(Axis.Z, -270f);
            router.SetSpindleRunning(false); router.ApplyHandwheelRotation(Axis.X, 45f); router.RequestReclamp();
            router.ApplyHandwheelRotation(Axis.Z, 270f); router.SetSpindleRunning(true);
            router.ApplyHandwheelRotation(Axis.X, -45f); router.ApplyHandwheelRotation(Axis.Z, -270f);
            router.SetSpindleRunning(false); router.SelectTool(ToolKind.Chamfer); router.ApplyHandwheelRotation(Axis.Z, 261f);
            router.SetSpindleRunning(true); router.ApplyHandwheelRotation(Axis.X, -9f); router.SetSpindleRunning(false);
            var sameWorkpiece = lathe.Controller.Workpiece;
            var sameSession = quest.Measurement;
            string geometryBefore = JsonUtility.ToJson(sameWorkpiece);
            float xBefore = lathe.Controller.ToolX, zBefore = lathe.Controller.ToolZ;
            var orientationBefore = lathe.Controller.Orientation;
            var repeatPoint = new MeasurementPoint("L1_Diameter", MeasurementPointType.Diameter, 0, 30);
            foreach (float manualValue in new[] { 41.2f, 40.4f, 40.1f })
            {
                lathe.OpenPage(HudPage.Measurement);
                Assert.That(lathe.CurrentPage, Is.EqualTo(HudPage.Measurement));
                Assert.That(lathe.HudVisible, Is.True);
                var dialogueProvider = lathe.IsDialogueOpen;
                try
                {
                    lathe.IsDialogueOpen = () => true;
                    Assert.That(lathe.HudVisible, Is.False, "Dialogue hides the measurement HUD instead of overlapping it.");
                }
                finally { lathe.IsDialogueOpen = dialogueProvider; }
                Assert.That(lathe.HudVisible, Is.True);
                Assert.That(lathe.Router.IsActive, Is.False);
                sameSession.Select(repeatPoint, "old_vernier", true);
                Assert.That(quest.RecordMeasurement(sameSession.Record(manualValue)), Is.True);
                Assert.That(sameSession.Records[repeatPoint.Id].PlayerValue, Is.EqualTo(manualValue));
                lathe.OpenPage(HudPage.Lathe);
                Assert.That(lathe.Controller.Workpiece, Is.SameAs(sameWorkpiece));
                Assert.That(quest.Measurement, Is.SameAs(sameSession));
                Assert.That(JsonUtility.ToJson(sameWorkpiece), Is.EqualTo(geometryBefore));
                Assert.That(lathe.Controller.ToolX, Is.EqualTo(xBefore));
                Assert.That(lathe.Controller.ToolZ, Is.EqualTo(zBefore));
                Assert.That(lathe.Controller.Orientation, Is.EqualTo(orientationBefore));
                Assert.That(lathe.Router.IsActive, Is.True);
            }
            router.SetSpindleRunning(true);
            lathe.OpenPage(HudPage.Measurement);
            Assert.That(lathe.CurrentPage, Is.EqualTo(HudPage.Lathe), "Running spindle must block measurement.");
            router.SetSpindleRunning(false);
            yield return null;
            Assert.That(lathe.FinishMachiningAndMeasure(), Is.True);
            Assert.That(lathe.CurrentPage, Is.EqualTo(HudPage.Measurement));
            var requirement = new PartRequirement {
                Dimensions = new System.Collections.Generic.List<DimensionRequirement> {
                    new DimensionRequirement("L1", 0, 30, 40), new DimensionRequirement("L2", 30, 70, 50), new DimensionRequirement("L3", 70, 100, 40) },
                RequiredChamferPositions = new System.Collections.Generic.List<float> { 0 } };
            Assert.That(quest.Evaluate(requirement).Result, Is.EqualTo(PartEvaluationResult.Perfect));
            world.RestoreLocation("Farm");
            Assert.That(quest.DeliverAndInstall(), Is.True);
            var json = JsonUtility.ToJson(SaveGameComposer.Capture(quest, world.CurrentLocation));
            var restored = SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(json), lathe.CreateStockWorkpiece);
            lathe.RestoreQuest(restored); world.Bind(restored, lathe); world.RestoreLocation("Farm"); overlay.Resume(restored);
            yield return null;
            float timer = (float)typeof(MvpWorldPanel).GetField("installationStartedAt", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(world);
            Assert.That(timer, Is.GreaterThanOrEqualTo(0), "Restored installation must have a running presentation timer.");
            Assert.That(restored.Quest.Data.WorkMemo.RecordedText("L1_Diameter"), Is.EqualTo("999"));
            Assert.That(lathe.Controller.Workpiece, Is.SameAs(restored.CurrentWorkpiece));
            Assert.That(restored.CompleteInstallation(), Is.True);
            world.RestoreLocation("Workshop"); world.RestoreLocation("Farm");
            yield return null;
            Assert.That(restored.PumpRepaired, Is.True);
            Assert.That(world.GetComponent<AudioSource>(), Is.Not.Null, "Repaired farm initializes its pump audio component.");
            world.RestoreLocation("Workshop"); overlay.Resume(restored);
            Assert.That(lathe.gameObject.activeSelf && lathe.enabled, Is.True, "Loaded workshop must reactivate the lathe.");
            Assert.That(lathe.CurrentPage, Is.EqualTo(HudPage.Workshop), "Completed saves reopen the workshop overview.");
            Assert.That(lathe.Router.IsActive, Is.False, "The workshop overview must not enable machining input.");
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            if (startSceneChanged) EditorSceneManager.playModeStartScene = previousStartScene;
        }
    }
}
