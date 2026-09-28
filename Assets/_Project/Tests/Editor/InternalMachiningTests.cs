using System;
using System.Linq;
using LastMachinist.Inventory;
using LastMachinist.Integration;
using LastMachinist.Save;
using LastMachinist.Core;
using LastMachinist.Machining.Core;
using LastMachinist.Machining.Turning;
using NUnit.Framework;
using UnityEngine;

namespace LastMachinist.Tests
{
    public sealed class InternalMachiningTests
    {
        private static WorkpieceState Stock(float length=100) => new WorkpieceState { InstanceId="test", MaterialId="steel", SourceItemId="stock",
            Geometry=new WorkpieceGeometry { RoundProfile=new TurningProfile { TotalLength=length, Segments={new TurningSegment { EndZ=length, Radius=25 }} } } };
        private static LatheController Controller(WorkpieceState state) => new LatheController(state,new RoughingToolState("rough","황삭",true,2),.05f,30,state.Geometry.RoundProfile.TotalLength+1,15,new ChamferToolState("chamfer","모따기",1));
        public static void Drill(LatheController c, LatheInputRouter r, float depth=20, TailstockToolKind drill=TailstockToolKind.Drill10)
        {
            r.SetActive(true); r.SetSpindleRunning(false); r.SelectTailstockTool(TailstockToolKind.CenterDrill);
            r.SetSpindleRunning(true); r.FeedQuill(9); r.FeedQuill(1); r.SetSpindleRunning(false); r.FeedQuill(-100);
            r.SelectTailstockTool(drill); r.SetSpindleRunning(true); r.FeedQuill(8+depth); r.SetSpindleRunning(false); r.FeedQuill(-100);
        }
        [Test] public void TailstockNeedsToolRotationContactAndSeparateCenterOperation()
        {
            var c=Controller(Stock()); var r=new LatheInputRouter(c);
            r.FeedQuill(1); Assert.That(c.StatusMessage,Does.Contain("장착"));
            r.SelectTailstockTool(TailstockToolKind.CenterDrill); r.FeedQuill(9);
            Assert.That(c.Profile.InnerProfile.Segments,Is.Empty);
            r.FeedQuill(-100); r.SetSpindleRunning(true); r.FeedQuill(5);
            Assert.That(c.Profile.InnerProfile.Segments,Is.Empty);
            r.FeedQuill(5); Assert.That(c.Profile.InnerTrueDiameter(99),Is.EqualTo(4));
            Assert.That(c.Profile.InnerProfile.OperationHistory.Single().ToolId,Is.EqualTo(TailstockTools.Center.ToolId));
            r.SelectTailstockTool(TailstockToolKind.Drill10); Assert.That(c.Tailstock.MountedTool,Is.EqualTo(TailstockToolKind.CenterDrill));
            r.SetSpindleRunning(false); r.SelectTailstockTool(TailstockToolKind.Drill10); Assert.That(c.StatusMessage,Does.Contain("후퇴"));
            r.FeedQuill(-100); r.SelectTailstockTool(TailstockToolKind.Drill10); r.SetSpindleRunning(true); r.FeedQuill(28);
            Assert.That(c.Profile.InnerTrueDiameter(90),Is.EqualTo(10));
            Assert.That(c.Profile.InnerProfile.DepthFromFace(100,false),Is.EqualTo(20));
            Assert.That(c.Profile.InnerProfile.OperationHistory.Last().ToolId,Is.EqualTo(TailstockTools.Drill10.ToolId));
        }
        [Test] public void DrillWithoutCenterAndExcessDepthAreConstrained()
        {
            var c=Controller(Stock()); var r=new LatheInputRouter(c); r.SelectTailstockTool(TailstockToolKind.Drill10); r.SetSpindleRunning(true); r.FeedQuill(28);
            Assert.That(c.Profile.InnerProfile.Segments,Is.Empty); Assert.That(c.StatusMessage,Does.Contain("센터드릴"));
            r.SetSpindleRunning(false); r.FeedQuill(-100); Drill(c,r,1000,TailstockToolKind.Drill16);
            Assert.That(c.Profile.InnerProfile.DepthFromFace(100,false),Is.EqualTo(80));
            Assert.That(c.Profile.InnerTrueDiameter(50),Is.EqualTo(16));
        }
        [Test] public void BoringRequiresRealEntryAndPreservesOuterShape()
        {
            var c=Controller(Stock()); var r=new LatheInputRouter(c); r.SetMovementScale(MovementScale.Fine); r.SelectTool(ToolKind.Boring);
            r.SetSpindleRunning(true); r.ApplyHandwheelRotation(Axis.Z,-900);
            Assert.That(c.Profile.InnerProfile.Segments,Is.Empty); Assert.That(c.StatusMessage,Does.Contain("구멍"));
            r.SetSpindleRunning(false); Drill(c,r); r.SelectTool(ToolKind.Boring);
            string outer=JsonUtility.ToJson(c.Workpiece.Geometry.RoundProfile.Segments[0]);
            r.ApplyHandwheelRotation(Axis.X,(3-c.ToolX)*90); r.ApplyHandwheelRotation(Axis.Z,(99.5f-c.ToolZ)*90);
            Assert.That(c.ToolZ,Is.EqualTo(99.5f));
            r.SetSpindleRunning(true); r.ApplyHandwheelRotation(Axis.X,(7-c.ToolX)*90);
            r.ApplyHandwheelRotation(Axis.Z,(90-c.ToolZ)*90);
            Assert.That(c.Profile.InnerTrueDiameter(90),Is.EqualTo(14));
            Assert.That(JsonUtility.ToJson(c.Profile.Segments[0]),Is.EqualTo(outer));
            r.ApplyHandwheelRotation(Axis.Z,(70-c.ToolZ)*90); Assert.That(c.ToolZ,Is.EqualTo(90),"Cannot tunnel beyond the drill bottom");
            r.SetSpindleRunning(false); r.ApplyHandwheelRotation(Axis.X,(3-c.ToolX)*90); r.ApplyHandwheelRotation(Axis.Z,(101-c.ToolZ)*90);
            r.SelectTool(ToolKind.Roughing); r.ApplyHandwheelRotation(Axis.X,(20-c.ToolX)*90); r.SetSpindleRunning(true); r.ApplyHandwheelRotation(Axis.Z,(85-c.ToolZ)*90);
            Assert.That(c.Profile.InnerTrueDiameter(90),Is.EqualTo(14)); Assert.That(c.Profile.RadiusAt(90),Is.EqualTo(20));
        }
        [Test] public void ReversalRetainsLocalHoleAndRequiresCenterAtOtherEnd()
        {
            var c=Controller(Stock()); var r=new LatheInputRouter(c); Drill(c,r);
            r.RequestReclamp(); r.SelectTailstockTool(TailstockToolKind.Drill10); r.SetSpindleRunning(true); r.FeedQuill(20);
            Assert.That(c.Profile.InnerTrueDiameter(1),Is.Zero); Assert.That(c.Profile.InnerTrueDiameter(99),Is.EqualTo(10));
            r.SetSpindleRunning(false); r.FeedQuill(-100); Drill(c,r);
            Assert.That(c.Profile.InnerTrueDiameter(1),Is.EqualTo(10)); c.Profile.Validate();
        }
        [Test] public void MeshHasBoreWallBlindBottomAndFiniteBounds()
        {
            var c=Controller(Stock()); var mesh=new Mesh();
            try
            {
                TurningMeshBuilder.Rebuild(mesh,c.Profile); int before=mesh.vertexCount;
                Drill(c,new LatheInputRouter(c)); TurningMeshBuilder.Rebuild(mesh,c.Profile);
                Assert.That(mesh.vertexCount,Is.GreaterThan(before));
                Assert.That(mesh.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)),Is.True);
                Assert.That(mesh.vertices.Any(v=>Math.Abs(v.x-1.5f)<.001f && Math.Abs(new Vector2(v.y,v.z).magnitude-.25f)<.001f),Is.True,"Hole bottom at Z80");
                Assert.That(mesh.vertices.Where(v=>Math.Abs(v.x-2.5f)<.001f).All(v=>new Vector2(v.y,v.z).magnitude>=.249f),Is.True,"Open end has no solid center cap");
                Assert.That(mesh.vertices.Zip(mesh.normals,(v,n)=>Math.Abs(v.y*v.y+v.z*v.z-.0625f)<.001f && Math.Abs(n.x)<.1f && v.y*n.y+v.z*n.z<0).Any(v=>v),Is.True,"Bore normals point into the cavity");
                c.Profile.RemoveMaterial(70,100,0); c.Profile.RemoveLinearMaterial(75,100,0,0); c.Profile.Validate();
                TurningMeshBuilder.Rebuild(mesh,c.Profile);
                Assert.That(c.Profile.InnerTrueDiameter(90),Is.EqualTo(10)); Assert.That(c.Profile.RadiusAt(90),Is.GreaterThanOrEqualTo(5.19f));
                Assert.Throws<ArgumentException>(()=>c.Profile.EnlargeHole(0,10,float.NaN,"invalid",true));
            } finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
        [Test] public void InternalGrooveIsBlockedAndDialDoesNotAdvanceThroughSolid()
        {
            var c=Controller(Stock()); var r=new LatheInputRouter(c); Drill(c,r); r.SelectTool(ToolKind.Boring); r.SetMovementScale(MovementScale.Fine);
            r.ApplyHandwheelRotation(Axis.X,(3-c.ToolX)*90); r.ApplyHandwheelRotation(Axis.Z,(90-c.ToolZ)*90);
            r.SetSpindleRunning(true); r.ApplyHandwheelRotation(Axis.X,360);
            Assert.That(c.Profile.InnerTrueDiameter(90),Is.EqualTo(10)); Assert.That(c.StatusMessage,Does.Contain("입구"));
            float dial=c.ZHandwheelDegrees;
            r.ApplyHandwheelRotation(Axis.Z,-1800);
            Assert.That(c.ToolZ,Is.EqualTo(90)); Assert.That(c.ZHandwheelDegrees,Is.EqualTo(dial));
        }
        [Test] public void InventorySaveAndCopyPreserveIndependentInnerAndOuterGeometry()
        {
            var d=Resources.Load<WorkpieceDefinition>("WorkpieceDefinitions/RoundBar50"); var inventory=new InventoryState();
            inventory.AddWorkpiece(d.CreateInstance(new ConditionProperties(),"yard","session")); var item=inventory.Workpieces[0];
            var session=new InventoryMachiningSession(inventory,_=>d); Assert.That(session.TryBegin(item.InstanceId,_=>true),Is.True);
            var c=Controller(item.MachiningWorkpiece); Drill(c,new LatheInputRouter(c));
            var copy=item.Copy(); copy.MachiningWorkpiece.Geometry.RoundProfile.EnlargeHole(285,300,7,"test",false);
            Assert.That(item.MachiningWorkpiece.Geometry.RoundProfile.InnerTrueDiameter(290),Is.EqualTo(10));
            var saved=SaveGameComposer.Capture(new Quest01Integration(inventory));
            var loaded=SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(saved)));
            Assert.That(loaded.Inventory.Workpieces[0].MachiningWorkpiece.Geometry.RoundProfile.InnerTrueDiameter(290),Is.EqualTo(10));
            Assert.That(loaded.Inventory.Workpieces[0].MachiningWorkpiece.Geometry.RoundProfile.InnerProfile.OperationHistory.Count,Is.GreaterThanOrEqualTo(2));
        }
        [TestCase(1920,1080)] [TestCase(1920,1200)] [TestCase(2520,1080)]
        public void TailstockPanelFitsAllSupportedAspectRatios(int width,int height)
        {
            var tail=LathePrototype.TailstockControls;
            Assert.That(tail.Overlaps(new Rect(408,648,164,142)),Is.False);
            Assert.That(tail.Overlaps(new Rect(582,648,164,142)),Is.False);
            var screen=MvpUiScale.ToScreenRect(tail,width,height);
            Assert.That(screen.xMin,Is.GreaterThanOrEqualTo(0)); Assert.That(screen.xMax,Is.LessThanOrEqualTo(width));
            Assert.That(screen.yMin,Is.GreaterThanOrEqualTo(0)); Assert.That(screen.yMax,Is.LessThanOrEqualTo(height));
        }
    }
}
