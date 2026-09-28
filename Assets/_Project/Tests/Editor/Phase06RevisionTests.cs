using System;
using System.Linq;
using LastMachinist.Inventory;
using LastMachinist.Integration;
using LastMachinist.Save;
using LastMachinist.Machining.Core;
using LastMachinist.Machining.Turning;
using NUnit.Framework;
using UnityEngine;
namespace LastMachinist.Tests
{
    public sealed class Phase06RevisionTests
    {
        private static WorkpieceDefinition Definition=>Resources.Load<WorkpieceDefinition>("WorkpieceDefinitions/RoundBar50");
        public static LatheController Mount(InventoryState inventory,float length=100)
        {
            var item=Definition.CreateInstance(new ConditionProperties { CorrosionCondition=73 },"yard","session");
            inventory.AddWorkpiece(item);
            var session=new InventoryMachiningSession(inventory,_=>Definition); session.TryBegin(item.InstanceId,_=>true);
            var owned=session.Active;
            owned.MachiningWorkpiece.Geometry.RoundProfile=WorkpieceSplitter.Slice(owned.MachiningWorkpiece.Geometry.RoundProfile,0,length);
            return new LatheController(owned.MachiningWorkpiece,DefinitionTool(),.05f,30,length+1,Math.Min(15,length*.15f),null,true);
        }
        private static RoughingToolState DefinitionTool()=>new RoughingToolState("rough","황삭",true,2);
        public static void Part(LatheController c,LatheInputRouter r,float localStart,bool complete=true)
        {
            r.SetSpindleRunning(false); r.SelectTool(ToolKind.Parting); r.SetMovementScale(MovementScale.Fine);
            r.ApplyHandwheelRotation(Axis.X,(30-c.ToolX)*90);
            float z=c.Orientation==Orientation.Normal?localStart+1:c.Profile.TotalLength-localStart-1;
            r.ApplyHandwheelRotation(Axis.Z,(z-c.ToolZ)*90); r.SetSpindleRunning(true);
            r.ApplyHandwheelRotation(Axis.X,((complete?0:10)-c.ToolX)*90);
        }
        [Test] public void PartialPartingKeepsOneAndCompleteSplitConservesActualLength()
        {
            var inventory=new InventoryState();var c=Mount(inventory);var r=new LatheInputRouter(c);string id=c.Workpiece.InstanceId;
            Part(c,r,40,false); Assert.That(c.Profile.RadiusAt(41),Is.EqualTo(10));Assert.That(c.PartingComplete,Is.False);
            Assert.That(WorkpieceSplitter.TrySplit(inventory,c,out _,out _),Is.False);Assert.That(inventory.Workpieces.Count,Is.EqualTo(1));
            r.ApplyHandwheelRotation(Axis.X,-900);Assert.That(c.PartingComplete,Is.True);
            Assert.That(WorkpieceSplitter.TrySplit(inventory,c,out var a,out var b),Is.True);
            Assert.That(a.InstanceId,Is.Not.EqualTo(b.InstanceId));Assert.That(a.ParentInstanceId,Is.EqualTo(id));
            Assert.That(a.MachiningWorkpiece.Geometry.RoundProfile.TotalLength,Is.EqualTo(40));Assert.That(b.MachiningWorkpiece.Geometry.RoundProfile.TotalLength,Is.EqualTo(58));
            Assert.That(a.Conditions.CorrosionCondition,Is.EqualTo(73));Assert.That(b.IdentificationState,Is.EqualTo(a.IdentificationState));
            Assert.That(inventory.FindWorkpiece(id),Is.Null);
            Assert.That(WorkpieceSplitter.TrySplit(inventory,c,out _,out _),Is.False);
            var session=new InventoryMachiningSession(inventory,_=>Definition);
            Assert.That(session.TryBegin(b.InstanceId,_=>true),Is.True);Assert.That(session.Active.MachiningWorkpiece.Geometry.RoundProfile.TotalLength,Is.EqualTo(58));session.End();
            var loaded=SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(SaveGameComposer.Capture(new Quest01Integration(inventory)))));
            Assert.That(loaded.Inventory.Workpieces.Count,Is.EqualTo(2));Assert.That(loaded.Inventory.Workpieces[1].MachiningWorkpiece.Geometry.RoundProfile.TotalLength,Is.EqualTo(58));
        }
        [Test] public void SplitClipsHoleAndSlopeAndCanRepeatOnRemnant()
        {
            var inventory=new InventoryState();var c=Mount(inventory);var r=new LatheInputRouter(c);
            c.Profile.RemoveMaterial(0,20,20);c.Profile.RemoveLinearMaterial(98,100,25,23);
            c.Profile.EnlargeHole(30,100,5,"drill",false);Part(c,r,40);
            Assert.That(WorkpieceSplitter.TrySplit(inventory,c,out var a,out var b),Is.True);
            Assert.That(a.MachiningWorkpiece.Geometry.RoundProfile.InnerTrueDiameter(35),Is.EqualTo(10));
            Assert.That(b.MachiningWorkpiece.Geometry.RoundProfile.InnerTrueDiameter(1),Is.EqualTo(10));
            Assert.That(b.MachiningWorkpiece.Geometry.RoundProfile.RadiusAt(58),Is.EqualTo(23));
            a.MachiningWorkpiece.Geometry.RoundProfile.EnlargeHole(30,40,6,"boring",false);
            Assert.That(b.MachiningWorkpiece.Geometry.RoundProfile.InnerTrueDiameter(1),Is.EqualTo(10));
            var session=new InventoryMachiningSession(inventory,_=>Definition);session.TryBegin(b.InstanceId,_=>true);
            var next=new LatheController(session.Active.MachiningWorkpiece,DefinitionTool(),.05f,30,59,8,null,true);
            Part(next,new LatheInputRouter(next),20);
            Assert.That(WorkpieceSplitter.TrySplit(inventory,next,out var x,out var y),Is.True);
            Assert.That(x.MachiningWorkpiece.Geometry.RoundProfile.TotalLength+y.MachiningWorkpiece.Geometry.RoundProfile.TotalLength+2,Is.EqualTo(58));
        }
        [Test] public void InvalidSplitKeepsSourceAndReversedPartingUsesLocalCoordinates()
        {
            var inventory=new InventoryState();var c=Mount(inventory);var r=new LatheInputRouter(c);
            Part(c,r,2);Assert.That(c.PartingComplete,Is.False);Assert.That(inventory.Workpieces.Count,Is.EqualTo(1));
            r.SetSpindleRunning(false);r.RequestReclamp();Part(c,r,40);
            Assert.That(c.PartingStart,Is.EqualTo(40));Assert.That(c.PartingEnd,Is.EqualTo(42));
            var source=inventory.Workpieces[0];inventory.Entries.Add(new InventoryEntry { ItemId="bad",Quantity=1,InstanceId=source.InstanceId });
            Assert.That(WorkpieceSplitter.TrySplit(inventory,c,out _,out _),Is.False);Assert.That(inventory.Workpieces[0],Is.SameAs(source));
        }
        [Test] public void QuillRotationClampZeroAndMouseWheelHaveIndependentState()
        {
            var c=Mount(new InventoryState());var r=new LatheInputRouter(c);r.SetMovementScale(MovementScale.Fine);r.SelectTailstockTool(TailstockToolKind.CenterDrill);
            float z=c.ToolZ, degrees=c.ZHandwheelDegrees;
            r.RotateQuill(90);Assert.That(c.Tailstock.QuillExtension,Is.EqualTo(1));Assert.That(c.Tailstock.HandwheelDegrees,Is.EqualTo(90));
            r.ZeroQuill();Assert.That(c.Tailstock.ZeroExtension,Is.EqualTo(1));r.ApplyMouseWheel(1,1);
            Assert.That(c.ToolZ,Is.EqualTo(z));Assert.That(c.ZHandwheelDegrees,Is.EqualTo(degrees));
            r.RotateQuill(90000);float limit=c.Tailstock.QuillExtension, angle=c.Tailstock.HandwheelDegrees;
            r.RotateQuill(90);Assert.That(c.Tailstock.QuillExtension,Is.EqualTo(limit));Assert.That(c.Tailstock.HandwheelDegrees,Is.EqualTo(angle));
        }
        [Test] public void VisibleParallelogramEdgeIsTheDomainCuttingEdge()
        {
            var definition=DefinitionTool();var mesh=RoughingToolGeometry.Build(definition);
            try {
                Assert.That(mesh.vertices[0].x/RoughingToolGeometry.UnitsPerMm,Is.EqualTo(definition.EdgeStart));
                Assert.That(mesh.vertices[1].x/RoughingToolGeometry.UnitsPerMm,Is.EqualTo(definition.EdgeEnd));
                Assert.That(mesh.vertices[0].y,Is.Zero);Assert.That(mesh.vertices[1].y,Is.Zero);
                Assert.That(mesh.vertices[3].x-mesh.vertices[0].x,Is.GreaterThan(0));
                var c=Mount(new InventoryState());var r=new LatheInputRouter(c);r.SetMovementScale(MovementScale.Fine);
                r.ApplyHandwheelRotation(Axis.Z,(50-c.ToolZ)*90);r.SetSpindleRunning(true);r.ApplyHandwheelRotation(Axis.X,-900);
                Assert.That(c.Profile.RadiusAt(48.9f),Is.EqualTo(25));Assert.That(c.Profile.RadiusAt(49.1f),Is.EqualTo(20));
            } finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
        [TestCase(1920,1080)] [TestCase(1920,1200)] [TestCase(2520,1080)]
        public void MiniPanelNeverOverlapsFixedCoreControls(int w,int h)
        {
            var panel=LathePrototype.MiniPanelRect;
            Assert.That(panel.Overlaps(LathePrototype.SpindleControlRect),Is.False);
            Assert.That(panel.Overlaps(new Rect(400,642,840,154)),Is.False);
            Assert.That(panel.Overlaps(new Rect(384,120,896,520)),Is.False);
            var screen=LastMachinist.Core.MvpUiScale.ToScreenRect(panel,w,h);
            Assert.That(screen.xMax,Is.LessThanOrEqualTo(w));Assert.That(screen.yMin,Is.GreaterThanOrEqualTo(0));
        }
    }
}
