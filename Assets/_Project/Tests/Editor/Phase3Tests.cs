using System;
using System.Collections.Generic;
using LastMachinist.Machining.Core;
using LastMachinist.Machining.Turning;
using NUnit.Framework;
using UnityEngine;

namespace LastMachinist.Tests
{
    public sealed class Phase3Tests
    {
        private static WorkpieceState Stock() => new WorkpieceState { InstanceId = "p3", MaterialId = "steel", SourceItemId = "stock", Geometry = new WorkpieceGeometry { Type = GeometryType.RoundProfile, RoundProfile = new TurningProfile { TotalLength = 100, Segments = new List<TurningSegment> { new TurningSegment { StartZ = 0, EndZ = 30, Radius = 20 }, new TurningSegment { StartZ = 30, EndZ = 70, Radius = 25 }, new TurningSegment { StartZ = 70, EndZ = 100, Radius = 20 } } } } };
        private static RoughingToolState Rough() => new RoughingToolState("rough", "Roughing", true, 2);
        private static ChamferToolState Chamfer() => new ChamferToolState("chamfer", "Chamfer", 1);

        [Test]
        public void CoordinateConversionPreservesMachineDirection()
        {
            Assert.That(TurningCoordinateConverter.MachineToWorkpieceZ(20, 100, Orientation.Normal), Is.EqualTo(20));
            Assert.That(TurningCoordinateConverter.MachineToWorkpieceZ(20, 100, Orientation.Reversed), Is.EqualTo(80));
            TurningCoordinateConverter.MachineIntervalToWorkpiece(10, 30, 100, Orientation.Reversed, out var start, out var end);
            Assert.That(start, Is.EqualTo(70)); Assert.That(end, Is.EqualTo(90));
        }

        [Test] public void TopAndFrontKeepMachineLengthPointingScreenRight()
        {
            foreach (var view in new[] { LatheViewMode.Front, LatheViewMode.Top })
                Assert.That(Vector3.Dot(LathePrototype.CameraRotation(view) * Vector3.right, Vector3.right), Is.EqualTo(1).Within(0.0001));
            Assert.That(Vector3.Dot(LathePrototype.CameraRotation(LatheViewMode.Top) * Vector3.forward, Vector3.down), Is.EqualTo(1).Within(0.0001));
            Assert.That(Vector3.Dot(LathePrototype.CameraRotation(LatheViewMode.Right) * Vector3.forward, Vector3.left), Is.EqualTo(1).Within(0.0001));
            var iso = LathePrototype.CameraRotation(LatheViewMode.Isometric) * Vector3.forward;
            Assert.That(Mathf.Abs(iso.x), Is.EqualTo(Mathf.Abs(iso.y)).Within(0.0001));
            Assert.That(Mathf.Abs(iso.y), Is.EqualTo(Mathf.Abs(iso.z)).Within(0.0001));
        }

        [Test]
        public void ReclampRequiresStoppedSpindleAndDoesNotRewriteProfile()
        {
            var stock = Stock(); var before = stock.Geometry.RoundProfile.Segments[0].StartZ;
            var lathe = new LatheController(stock, Rough(), 0.05f, 30, 80, 15, Chamfer()); var router = new LatheInputRouter(lathe);
            router.SetSpindleRunning(true); router.RequestReclamp();
            Assert.That(lathe.Orientation, Is.EqualTo(Orientation.Normal));
            router.SetSpindleRunning(false); router.RequestReclamp();
            Assert.That(lathe.Orientation, Is.EqualTo(Orientation.Reversed));
            Assert.That(stock.Geometry.RoundProfile.Segments[0].StartZ, Is.EqualTo(before));
        }

        [Test]
        public void OppositeSideCanBeCutAfterReclamp()
        {
            var stock = Stock(); stock.Geometry.RoundProfile.Segments[0].Radius = 25;
            var lathe = new LatheController(stock, Rough(), 0.05f, 30, 80, 15, Chamfer()); var router = new LatheInputRouter(lathe);
            router.SetMovementScale(MovementScale.Coarse); router.SetSpindleRunning(false); router.RequestReclamp();
            router.SetSpindleRunning(true); router.ApplyHandwheelRotation(Axis.X, -9f); router.ApplyHandwheelRotation(Axis.Z, 18f);
            Assert.That(stock.Geometry.RoundProfile.Segments.Exists(s => s.Radius == 20 && s.StartZ < 30), Is.True);
        }

        [Test]
        public void ChamferRequiresContactToolAndRunningSpindle()
        {
            var stock = Stock(); var lathe = new LatheController(stock, Rough(), 0.05f, 20, 100, 15, Chamfer()); var router = new LatheInputRouter(lathe);
            router.ApplyChamfer(1); Assert.That(stock.Geometry.RoundProfile.Chamfers, Is.Empty);
            router.SelectTool(ToolKind.Chamfer); router.ApplyChamfer(1);
            Assert.That(stock.Geometry.RoundProfile.Chamfers, Is.Empty);
            router.SetSpindleRunning(true); router.ApplyHandwheelRotation(Axis.X, -9f);
            Assert.That(stock.Geometry.RoundProfile.HasVisibleChamfer(100), Is.True);
            Assert.That(stock.Geometry.RoundProfile.RadiusAt(100), Is.EqualTo(19).Within(0.001));
        }

        [Test] public void ChamferFeedControlsDepthAndRetractionDoesNotRestoreMaterial()
        {
            var stock = Stock();
            var lathe = new LatheController(stock, Rough(), initialX: 30, initialZ: 80, chamfer: Chamfer());
            var router = new LatheInputRouter(lathe);
            router.SelectTool(ToolKind.Chamfer);
            router.SetMovementScale(MovementScale.Coarse);
            router.ApplyHandwheelRotation(Axis.Z, 18f);
            router.ApplyHandwheelRotation(Axis.X, -9f);
            router.SetSpindleRunning(true);
            router.SetMovementScale(MovementScale.Medium);
            router.ApplyHandwheelRotation(Axis.X, -9f);
            Assert.That(stock.Geometry.RoundProfile.RadiusAt(100), Is.EqualTo(19).Within(0.001));
            router.ApplyHandwheelRotation(Axis.X, -9f);
            Assert.That(stock.Geometry.RoundProfile.RadiusAt(100), Is.EqualTo(18).Within(0.001));
            router.ApplyHandwheelRotation(Axis.X, 36f);
            Assert.That(stock.Geometry.RoundProfile.RadiusAt(100), Is.EqualTo(18).Within(0.001));
        }

        [Test] public void ChamferUsesReversedExposedEnd()
        {
            var stock = Stock(); var router = new LatheInputRouter(new LatheController(stock, Rough(), 0.05f, 20, 100, 15, Chamfer()));
            router.SelectTool(ToolKind.Chamfer); router.RequestReclamp(); router.SetSpindleRunning(true); router.ApplyHandwheelRotation(Axis.X, -9f);
            Assert.That(stock.Geometry.RoundProfile.HasVisibleChamfer(0), Is.True);
        }

        [Test] public void ChamferRefreshesBoundMeshAndSurvivesUnitySerialization()
        {
            var stock = Stock();
            var go = new GameObject("Chamfer view regression");
            try
            {
                var view = go.AddComponent<TurningWorkpieceView>();
                view.Bind(stock.Geometry.RoundProfile);
                int before = view.Revision;
                var router = new LatheInputRouter(new LatheController(stock, Rough(), initialX:20, initialZ:100, chamfer:Chamfer()));
                router.SelectTool(ToolKind.Chamfer);
                router.RequestReclamp();
                router.SetSpindleRunning(true);
                router.ApplyHandwheelRotation(Axis.X, -9f);
                Assert.That(view.Revision, Is.GreaterThan(before));
                var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(System.Array.Exists(mesh.vertices, v => Mathf.Abs(v.x + 2.5f) < 0.0001f && Mathf.Abs(new Vector2(v.y,v.z).magnitude - 0.95f) < 0.0001f), Is.True);
                var restored = JsonUtility.FromJson<WorkpieceState>(JsonUtility.ToJson(stock));
                restored.Validate();
                Assert.That(restored.Geometry.RoundProfile.HasVisibleChamfer(0), Is.True);
                Assert.That(restored.Geometry.RoundProfile.RadiusAt(0), Is.EqualTo(19).Within(0.001));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase(false)] [TestCase(true)]
        public void ContactChamferOvercutPersistsAndSurfaceMatchesGeometry(bool reversed)
        {
            var stock = Stock(); var p = stock.Geometry.RoundProfile;
            var controller = new LatheController(stock, Rough(), initialX:20, initialZ:100, chamfer:Chamfer());
            var router = new LatheInputRouter(controller);
            router.SelectTool(ToolKind.Chamfer);
            if (reversed) router.RequestReclamp();
            router.SetSpindleRunning(true);
            router.ApplyHandwheelRotation(Axis.X, -117f);
            float end = reversed ? 0 : 100;
            Assert.That(p.RadiusAt(end), Is.EqualTo(7).Within(0.001));
            router.ApplyHandwheelRotation(Axis.X, 135f);
            Assert.That(p.RadiusAt(end), Is.EqualTo(7).Within(0.001));
            foreach (var span in p.SurfaceSpans())
                Assert.That(p.RadiusAt((span.StartZ + span.EndZ) / 2), Is.EqualTo((span.StartRadius + span.EndRadius) / 2).Within(0.001));
            var restored = JsonUtility.FromJson<WorkpieceState>(JsonUtility.ToJson(stock));
            restored.Validate();
            Assert.That(restored.Geometry.RoundProfile.RadiusAt(end), Is.EqualTo(7).Within(0.001));
        }

        [Test] public void ChamferProducesSlopeAndLaterCutsNeverRestoreMaterial()
        {
            var profile = Stock().Geometry.RoundProfile; profile.AddChamfer(0, 2);
            Assert.That(profile.RadiusAt(0), Is.EqualTo(18).Within(0.001));
            Assert.That(profile.SurfaceSpans().Exists(s => s.StartRadius != s.EndRadius), Is.True);
            profile.RemoveMaterial(0, 30, 19);
            Assert.That(profile.RadiusAt(0), Is.EqualTo(18).Within(0.001));
            Assert.That(profile.RadiusAt(2), Is.EqualTo(19).Within(0.001));
        }

        [Test]
        public void DialWrapUsesShortestAngularDelta()
        {
            Assert.That(LatheDialMath.SafeDeltaAngle(179, -179), Is.EqualTo(2).Within(0.001f));
            Assert.That(LatheDialMath.SafeDeltaAngle(-179, 179), Is.EqualTo(-2).Within(0.001f));
            var lathe = new LatheController(Stock(), Rough()); var dial = new LatheDial(new LatheInputRouter(lathe), Axis.X);
            dial.BeginDrag(Vector2.zero, new Vector2(-1, 0.017f)); dial.Drag(new Vector2(-1, -0.017f)); dial.EndDrag();
            Assert.That(lathe.ToolX, Is.GreaterThan(29.9f));
        }

        [Test]
        public void InvalidChamferIsRejected()
        {
            var profile = Stock().Geometry.RoundProfile;
            Assert.Throws<ArgumentException>(() => profile.AddChamfer(40, 1));
            Assert.Throws<ArgumentException>(() => profile.AddChamfer(0, 0));
            profile.AddChamfer(100, 1); profile.Validate();
        }
    }
}
