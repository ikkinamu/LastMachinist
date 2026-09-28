using System;
using System.Collections.Generic;
using LastMachinist.Machining.Core;
using LastMachinist.Machining.Turning;
using NUnit.Framework;
using UnityEngine;

namespace LastMachinist.Tests
{
    public sealed class LatheTests
    {
        // PATCH002 REV4: commands carry rotation degrees, not legacy dial steps.
        [TestCase(MovementScale.Fine, 90f)]
        [TestCase(MovementScale.Medium, 9f)]
        [TestCase(MovementScale.Coarse, 0.9f)]
        public void RotationFeedsExactlyOneMillimetre(MovementScale scale, float degrees)
        {
            var controller = new LatheController(Stock(), Tool());
            var router = new LatheInputRouter(controller);
            router.SetMovementScale(scale);
            router.ApplyHandwheelRotation(Axis.X, -degrees);
            Assert.That(controller.ToolX, Is.EqualTo(29).Within(0.0001));
            router.ApplyHandwheelDelta(Axis.X, degrees);
            Assert.That(controller.ToolX, Is.EqualTo(30).Within(0.0001), "Legacy API alias also accepts degrees.");
        }
        private static WorkpieceState Stock() => new WorkpieceState
        {
            InstanceId = "lathe_test_piece", MaterialId = "test_material", SourceItemId = "test_stock",
            Geometry = new WorkpieceGeometry { RoundProfile = new TurningProfile
            {
                TotalLength = 100,
                Segments = new List<TurningSegment> { new TurningSegment { EndZ = 100, Radius = 25 } }
            } }
        };
        private static RoughingToolState Tool(bool cuts = true) => new RoughingToolState("test_roughing", "Test tool", cuts, 2);
        private static LatheInputRouter Coarse(LatheController lathe)
        { var router = new LatheInputRouter(lathe); router.SetMovementScale(MovementScale.Coarse); return router; }
        private static float Radius(TurningProfile profile, float z) => profile.Segments.Find(s => s.StartZ <= z && s.EndZ > z).Radius;

        [Test]
        public void SpindleOffContactOnlyPositions()
        {
            var lathe = new LatheController(Stock(), Tool());
            Coarse(lathe).ApplyHandwheelRotation(Axis.X, -9f);
            Assert.That(lathe.ToolX, Is.EqualTo(20));
            Assert.That(Radius(lathe.Profile, 80), Is.EqualTo(25));
        }

        [Test]
        public void SpindleOnWithoutContactDoesNotCut()
        {
            var lathe = new LatheController(Stock(), Tool()); var router = Coarse(lathe);
            router.SetSpindleRunning(true); router.ApplyHandwheelRotation(Axis.Z, -13.5f);
            Assert.That(lathe.Profile.Segments.Count, Is.EqualTo(1));
            Assert.That(Radius(lathe.Profile, 65), Is.EqualTo(25));
        }

        [Test]
        public void CuttingSweepsAndCreatesPersistentStep()
        {
            var lathe = new LatheController(Stock(), Tool()); var router = Coarse(lathe);
            int events = 0; lathe.Profile.Changed += () => events++;
            router.SetSpindleRunning(true); router.ApplyHandwheelRotation(Axis.X, -9f);
            router.ApplyHandwheelRotation(Axis.Z, -18f);
            Assert.That(Radius(lathe.Profile, 70), Is.EqualTo(20));
            Assert.That(Radius(lathe.Profile, 40), Is.EqualTo(25));
            Assert.That(events, Is.EqualTo(2));
            router.ApplyHandwheelRotation(Axis.X, 9f); router.SetSpindleRunning(false); router.SetSpindleRunning(true);
            Assert.That(Radius(lathe.Profile, 70), Is.EqualTo(20));
            lathe.Profile.Validate();
        }

        [Test]
        public void StartingSpindleAtContactCuts()
        {
            var lathe = new LatheController(Stock(), Tool()); var router = Coarse(lathe);
            router.ApplyHandwheelRotation(Axis.X, -9f); router.SetSpindleRunning(true);
            Assert.That(Radius(lathe.Profile, 80), Is.EqualTo(20));
        }

        [Test]
        public void MissingOrNoncuttingToolPreventsRemoval()
        {
            foreach (var tool in new[] { Tool(false), null })
            {
                var lathe = new LatheController(Stock(), tool); var router = Coarse(lathe);
                router.SetSpindleRunning(true); router.ApplyHandwheelRotation(Axis.X, -13.5f);
                Assert.That(Radius(lathe.Profile, 80), Is.EqualTo(25));
            }
        }

        [Test]
        public void MissingWorkpieceAndPausedStatePreventCutting()
        {
            var empty = new LatheController(null, Tool()); var emptyRouter = Coarse(empty);
            emptyRouter.SetSpindleRunning(true); emptyRouter.ApplyHandwheelRotation(Axis.X, -22.5f);
            Assert.That(empty.SpindleRunning, Is.False);
            var lathe = new LatheController(Stock(), Tool()); var router = Coarse(lathe);
            router.SetSpindleRunning(true); router.SetActive(false); router.ApplyHandwheelRotation(Axis.X, -13.5f);
            Assert.That(lathe.ToolX, Is.EqualTo(30)); Assert.That(lathe.SpindleRunning, Is.False);
            Assert.That(Radius(lathe.Profile, 80), Is.EqualTo(25));
            router.SetActive(true); Assert.That(lathe.SpindleRunning, Is.False);
        }

        [Test]
        public void FinishedStateAndGripAreProtected()
        {
            var stock = Stock(); stock.IsFinished = true;
            var lathe = new LatheController(stock, Tool()); var router = Coarse(lathe);
            router.SetSpindleRunning(true); router.ApplyHandwheelRotation(Axis.X, -13.5f);
            Assert.That(Radius(lathe.Profile, 80), Is.EqualTo(25));
            lathe = new LatheController(Stock(), Tool()); router = Coarse(lathe);
            router.SetSpindleRunning(true); router.ApplyHandwheelRotation(Axis.X, -13.5f); router.ApplyHandwheelRotation(Axis.Z, -450f);
            Assert.That(Radius(lathe.Profile, 14), Is.EqualTo(25));
            Assert.That(lathe.ToolZ, Is.GreaterThanOrEqualTo(16));
        }

        [Test]
        public void RouterInputsShareAxisAndScaleSemantics()
        {
            var first = new LatheController(Stock(), Tool()); var a = Coarse(first);
            var second = new LatheController(Stock(), Tool()); var b = Coarse(second);
            a.SelectAxis(Axis.Z); a.Scroll(-9); b.ApplyHandwheelRotation(Axis.Z, -9f);
            Assert.That(first.ToolZ, Is.EqualTo(second.ToolZ));
            a.SetMovementScale(MovementScale.Fine); a.ApplyHandwheelRotation(Axis.X, -4.5f);
            Assert.That(first.ToolX, Is.EqualTo(29.95f).Within(0.0001f));
            a.SetMovementScale(MovementScale.Medium); a.ApplyHandwheelRotation(Axis.X, -4.5f);
            Assert.That(first.ToolX, Is.EqualTo(29.45f).Within(0.0001f));
        }

        [Test]
        public void InvalidInputDoesNotCorruptGeometry()
        {
            var lathe = new LatheController(Stock(), Tool()); var router = Coarse(lathe);
            Assert.Throws<ArgumentException>(() => router.Scroll(float.NaN));
            Assert.Throws<ArgumentException>(() => router.SelectAxis((Axis)99));
            Assert.Throws<ArgumentException>(() => router.SetMovementScale((MovementScale)3));
            router.SetSpindleRunning(true); router.ApplyHandwheelRotation(Axis.X, -450000f);
            Assert.That(lathe.ToolX, Is.Zero); lathe.Profile.Validate();
            router.ApplyHandwheelRotation(Axis.X, 450000f);
            Assert.That(Radius(lathe.Profile, 80), Is.Zero);
        }

        [Test]
        public void RemovalMergesIntervalsAndNeverAddsMaterial()
        {
            var profile = Stock().Geometry.RoundProfile;
            profile.RemoveMaterial(20, 30, 15); profile.RemoveMaterial(30, 40, 15);
            Assert.That(profile.Segments.Count, Is.EqualTo(3));
            Assert.That(profile.RemoveMaterial(20, 40, 20), Is.False);
            Assert.That(Radius(profile, 25), Is.EqualTo(15));
            profile.Validate();
        }

        [Test]
        public void MeshRefreshesFromProfileNotifications()
        {
            var obj = new GameObject("View test");
            try
            {
                var profile = Stock().Geometry.RoundProfile;
                var view = obj.AddComponent<TurningWorkpieceView>(); view.Bind(profile);
                var mesh = obj.GetComponent<MeshFilter>().sharedMesh;
                int originalVertices = mesh.vertexCount;
                profile.RemoveMaterial(60, 81, 20);
                Assert.That(view.Revision, Is.EqualTo(2));
                Assert.That(mesh.vertexCount, Is.GreaterThan(originalVertices));
                Assert.That(Array.Exists(mesh.vertices, v => Mathf.Abs(v.x - 0.5f) < 0.001f), Is.True);
                profile.RemoveMaterial(0, 100, 10);
                Assert.That(mesh.bounds.size.y, Is.EqualTo(1).Within(0.001f));
                Assert.That(mesh.bounds.size.x, Is.EqualTo(5).Within(0.001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }

        [Test]
        public void ToolCanOnlyBeChangedWithStoppedSpindle()
        {
            var lathe = new LatheController(Stock(), Tool()); var router = Coarse(lathe);
            router.SetSpindleRunning(true); router.SetToolMounted(false);
            Assert.That(lathe.ToolMounted, Is.True);
            router.SetSpindleRunning(false); router.SetToolMounted(false);
            Assert.That(lathe.ToolMounted, Is.False);
            router.SetToolMounted(true); Assert.That(lathe.ToolMounted, Is.True);
        }
    }
}
