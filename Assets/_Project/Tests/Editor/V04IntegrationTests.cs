using System.Collections.Generic;
using LastMachinist.Core;
using LastMachinist.Inventory;
using LastMachinist.Integration;
using LastMachinist.Machining.Turning;
using LastMachinist.Measurement;
using LastMachinist.Quest;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LastMachinist.Tests
{
    public sealed class V04IntegrationTests
    {
        [TestCase(37.60f)] [TestCase(42.20f)]
        public void InstrumentMovesAndAlignsWithoutUsingPlayerRecord(float diameter)
        {
            var profile = new TurningProfile { TotalLength = 100, Segments = new List<TurningSegment> {
                new TurningSegment { StartZ = 0, EndZ = 100, Radius = diameter / 2 } } };
            var session = new MeasurementSession(profile, "test");
            var point = new MeasurementPoint("L1_Diameter", MeasurementPointType.Diameter, 0, 30);
            var reading = session.Select(point, "old_vernier", true);
            var display = new VernierDisplayModel(reading);
            session.Record(39.3f);
            Assert.That(display.JawOpening, Is.EqualTo(diameter).Within(0.001));
            int aligned = Mathf.RoundToInt(reading.VernierOffset * display.Scale.Divisions);
            Assert.That(display.TickPosition(aligned), Is.EqualTo(Mathf.Round(display.TickPosition(aligned))).Within(0.001));
            Assert.That(session.Records[point.Id].PlayerValue, Is.EqualTo(39.3f));
            Assert.That(session.TrueDimension(point), Is.EqualTo(diameter).Within(0.001));
        }

        [Test] public void BrokenL3ContainsDiameterAndAxialWear()
        {
            var quest = new Quest01Integration(new InventoryState(), new QuestRuntimeState { State = QuestState.InspectingPump });
            Assert.That(quest.RecoverPrototypeBrokenShaft(), Is.True);
            var diameter = quest.Measurement.Select(new MeasurementPoint("L3_Diameter", MeasurementPointType.Diameter, 70, 100), "old_vernier", true);
            Assert.That(diameter.Value, Is.LessThan(40));
            var length = quest.Measurement.Select(new MeasurementPoint("L3_Length", MeasurementPointType.Length, 70, 100), "old_vernier", true);
            Assert.That(length.Value, Is.EqualTo(27.5f));
            Assert.That(quest.Quest.Data.WorkMemo.TargetShapeUnlocked, Is.False);
        }

        [Test] public void AllFourViewsEnforceOrthographicAndHaveNoDepthScaling()
        {
            var host = new GameObject("v04 test lathe");
            var cameraObject = new GameObject("v04 test camera");
            try
            {
                var config = AssetDatabase.LoadAssetAtPath<LathePrototypeSettings>("Assets/_Project/Data/LathePrototypeSettings.asset");
                if (config == null)
                {
                    string[] ids = AssetDatabase.FindAssets("t:LathePrototypeSettings");
                    Assert.That(ids, Is.Not.Empty);
                    config = AssetDatabase.LoadAssetAtPath<LathePrototypeSettings>(AssetDatabase.GUIDToAssetPath(ids[0]));
                }
                var camera = cameraObject.AddComponent<Camera>();
                var lathe = host.AddComponent<LathePrototype>();
                lathe.Initialize(config, camera);
                foreach (LatheViewMode view in System.Enum.GetValues(typeof(LatheViewMode)))
                {
                    camera.orthographic = false;
                    lathe.SetCameraView(view);
                    Assert.That(camera.orthographic, Is.True, view.ToString());
                    Vector3 a = camera.transform.position + camera.transform.forward * 5;
                    Vector3 b = a + camera.transform.forward * 10;
                    float nearWidth = (camera.WorldToViewportPoint(a + camera.transform.right) - camera.WorldToViewportPoint(a)).x;
                    float farWidth = (camera.WorldToViewportPoint(b + camera.transform.right) - camera.WorldToViewportPoint(b)).x;
                    Assert.That(nearWidth, Is.EqualTo(farWidth).Within(0.0001));
                }
                Assert.That(lathe.MeasurementInput, Is.Empty);
                lathe.OpenPage(HudPage.Measurement);
                Assert.That(lathe.Router.IsActive, Is.False);
                float x = lathe.Controller.ToolX;
                lathe.Router.SetSpindleRunning(true); lathe.Router.ApplyHandwheelDelta(Axis.X, -5);
                Assert.That(lathe.Controller.SpindleRunning, Is.False);
                Assert.That(lathe.Controller.ToolX, Is.EqualTo(x));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(cameraObject); }
        }

        [Test] public void DialogueAndMeasurementFitTheirSeparatePages()
        {
            Assert.That(DialoguePage.Bounds.height / 800, Is.InRange(0.30f, 0.35f));
            Assert.That(DialoguePage.Bounds.yMin, Is.GreaterThanOrEqualTo(800 * 0.65f));
            // These pages are mutually exclusive; the enlarged scale may use the lower screen.
            Assert.That(VernierView.ScaleBounds.xMin, Is.GreaterThanOrEqualTo(0));
            Assert.That(VernierView.ScaleBounds.yMin, Is.GreaterThanOrEqualTo(0));
            Assert.That(VernierView.ScaleBounds.xMax, Is.LessThanOrEqualTo(1280));
            Assert.That(VernierView.ScaleBounds.yMax, Is.LessThanOrEqualTo(800));
        }
    }
}
