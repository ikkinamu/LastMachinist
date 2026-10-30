using System;
using System.Linq;
using LastMachinist.Quest;
using LastMachinist.Inventory;
using LastMachinist.Integration;
using LastMachinist.Save;
using LastMachinist.Evaluation;
using LastMachinist.Machining.Core;
using LastMachinist.Machining.Turning;
using NUnit.Framework;
using UnityEngine;

namespace LastMachinist.Tests
{
    public sealed class RepeatableQuestTests
    {
        private static WorkpieceDefinition Definition => Resources.Load<WorkpieceDefinition>("WorkpieceDefinitions/RoundBar50");
        private static WorkpieceInstance Add(InventoryState inventory)
        {
            var item = Definition.CreateInstance(new ConditionProperties { CorrosionCondition = 71 }, "test_yard", Guid.NewGuid().ToString("N"));
            inventory.AddWorkpiece(item); return inventory.FindWorkpiece(item.InstanceId);
        }
        private static InventoryMachiningSession Session(InventoryState inventory) => new InventoryMachiningSession(inventory, _ => Definition);
        private static WorkpieceInstance Trim(InventoryState inventory,RepeatableQuestService service,RepeatableQuestInstance q,WorkpieceInstance item)
        {
            var c=new LatheController(item.MachiningWorkpiece,new RoughingToolState("test","황삭",true,2),.05f,30,item.MachiningWorkpiece.Geometry.RoundProfile.TotalLength+1,15,null,true);
            Phase06RevisionTests.Part(c,new LatheInputRouter(c),q.GeneratedTargetGeometry.RequiredTotalLength);
            Assert.That(WorkpieceSplitter.TrySplit(inventory,c,out var piece,out _),Is.True);
            Assert.That(service.Assign(q.QuestInstanceId,piece,Definition),Is.True);
            Assert.That(Session(inventory).TryBegin(piece.InstanceId,_=>true),Is.True);
            return piece;
        }
        private static void Position(LatheController controller, LatheInputRouter router, float x, float z)
        {
            router.SetSpindleRunning(false);
            router.ApplyHandwheelRotation(Axis.X, (30-controller.ToolX)*90);
            router.ApplyHandwheelRotation(Axis.Z, (z-controller.ToolZ)*90);
            router.ApplyHandwheelRotation(Axis.X, (x-controller.ToolX)*90);
        }
        public static void Machine(WorkpieceState workpiece, PartRequirement target)
        {
            var c = new LatheController(workpiece, new RoughingToolState("test", "바이트", true, 2), .05f, 30, workpiece.Geometry.RoundProfile.TotalLength+1, 15,
                new ChamferToolState("chamfer", "모따기", 1));
            Machine(c, new LatheInputRouter(c), target);
        }
        public static void Machine(LatheController c, LatheInputRouter r, PartRequirement target)
        {
            r.SetActive(true); r.SetMovementScale(MovementScale.Fine);
            r.SelectTool(ToolKind.Roughing);
            float halfWidth = c.Tool.Width / 2;
            foreach (var d in target.Dimensions)
            {
                bool reverse = d.StartZ < 15;
                if ((c.Orientation == Orientation.Reversed) != reverse) r.RequestReclamp();
                float from = reverse ? c.Profile.TotalLength-d.EndZ : d.StartZ;
                float to = reverse ? c.Profile.TotalLength-d.StartZ : d.EndZ;
                Position(c, r, d.TargetDiameter/2, from+halfWidth);
                r.SetSpindleRunning(true);
                r.ApplyHandwheelRotation(Axis.Z, (to-halfWidth-c.ToolZ)*90);
                r.SetSpindleRunning(false);
            }
            if (c.Orientation == Orientation.Reversed) r.RequestReclamp();
            foreach (float end in target.RequiredChamferPositions)
            {
                r.SelectTool(ToolKind.Chamfer);
                Position(c, r, target.Dimensions.Last().TargetDiameter/2-.5f, end);
                r.SetSpindleRunning(true); r.SetSpindleRunning(false);
            }
        }
        [TestCase(0,0)] [TestCase(0,1)] [TestCase(1,0)] [TestCase(1,1)] [TestCase(2,0)]
        [TestCase(2,1)] [TestCase(3,0)] [TestCase(3,1)] [TestCase(4,0)] [TestCase(4,1)]
        public void EveryPresetCanBeCutThroughSharedCommands(int templateIndex, int variantIndex)
        {
            var inventory = new InventoryState(); var item = Add(inventory);
            var t = RepeatableQuestCatalog.Templates[templateIndex]; var v = t.AllowedVariants[variantIndex];
            var q = new RepeatableQuestInstance { QuestInstanceId = "test_order", TemplateId = t.TemplateId, GeneratedVariant = v.Id,
                GeneratedTargetGeometry = RepeatableQuestCatalog.Copy(v.Requirement) };
            var data = new RepeatableQuestState { Initialized = true }; data.Instances.Add(q);
            var service = new RepeatableQuestService(inventory, data);
            Assert.That(service.Assign(q.QuestInstanceId, item, Definition), Is.False);
            Assert.That(service.Accept(q.QuestInstanceId), Is.True);
            Assert.That(service.Assign(q.QuestInstanceId, item, Definition), Is.True);
            var session = Session(inventory); Assert.That(session.TryBegin(item.InstanceId, _ => true), Is.True);
            item=Trim(inventory,service,q,item);
            Machine(item.MachiningWorkpiece, q.GeneratedTargetGeometry);
            Assert.That(service.Deliver(q.QuestInstanceId), Is.False, "Mounted stock must not be delivered");
            session.End();
            Assert.That(service.Deliver(q.QuestInstanceId), Is.True, q.EvaluationFeedback);
            Assert.That(q.State, Is.EqualTo(RepeatableQuestStatus.Completed));
            Assert.That(inventory.FindWorkpiece(item.InstanceId), Is.Null);
            Assert.That(q.DeliveredWorkpiece.Conditions.CorrosionCondition, Is.EqualTo(71));
            Assert.That(service.Deliver(q.QuestInstanceId), Is.False);
            Assert.That(data.Instances.Count(x => x.State == RepeatableQuestStatus.Offered), Is.EqualTo(3));
            RepeatableQuestService.Validate(data, inventory);
        }
        [Test] public void FailedInspectionKeepsActiveAndReworkCanFinish()
        {
            var inventory = new InventoryState(); var item = Add(inventory); var service = new RepeatableQuestService(inventory);
            var q = service.Data.Instances[0]; service.Accept(q.QuestInstanceId); service.Assign(q.QuestInstanceId, item, Definition);
            var session = Session(inventory); session.TryBegin(item.InstanceId, _ => true); session.End();
            Assert.That(service.Deliver(q.QuestInstanceId), Is.False); Assert.That(q.State, Is.EqualTo(RepeatableQuestStatus.Active));
            Assert.That(q.EvaluationResult, Is.EqualTo(PartEvaluationResult.Reworkable));
            session.TryBegin(item.InstanceId,_=>true); item=Trim(inventory,service,q,item);
            Machine(item.MachiningWorkpiece, q.GeneratedTargetGeometry);
            session.End();
            Assert.That(service.Deliver(q.QuestInstanceId), Is.True);
        }
        [Test] public void SmallStockAndAlreadyAssignedStockAreRejected()
        {
            var inventory = new InventoryState(); var item = Add(inventory); var service = new RepeatableQuestService(inventory);
            var a = service.Data.Instances[0]; var b = service.Data.Instances[1]; service.Accept(a.QuestInstanceId); service.Accept(b.QuestInstanceId);
            var small = UnityEngine.Object.Instantiate(Definition);
            try {
                small.NominalDimensions.Diameter = 20;
                Assert.That(service.CanAssign(a.QuestInstanceId, item, small, out var reason), Is.False); Assert.That(reason, Does.Contain("크기가 부족"));
            } finally { UnityEngine.Object.DestroyImmediate(small); }
            Assert.That(service.Assign(a.QuestInstanceId, item, Definition), Is.True);
            Assert.That(service.Assign(b.QuestInstanceId, item, Definition), Is.False);
        }
        [Test] public void NewInstancesRotateVariantsAndNeverDuplicateLiveTemplates()
        {
            var inventory = new InventoryState(); var service = new RepeatableQuestService(inventory);
            for (int i=0;i<12;i++)
            {
                var q = service.Data.Instances.First(x => x.State == RepeatableQuestStatus.Offered);
                var item = Add(inventory); service.Accept(q.QuestInstanceId); service.Assign(q.QuestInstanceId, item, Definition);
                var session = Session(inventory); session.TryBegin(item.InstanceId, _ => true); item=Trim(inventory,service,q,item); Machine(item.MachiningWorkpiece, q.GeneratedTargetGeometry); session.End();
                Assert.That(service.Deliver(q.QuestInstanceId), Is.True, q.EvaluationFeedback);
                RepeatableQuestService.Validate(service.Data, inventory);
            }
            var completed = service.Data.Instances.Where(x=>x.State==RepeatableQuestStatus.Completed).ToArray();
            Assert.That(completed.Select(q=>q.TemplateId).Distinct().Count(), Is.EqualTo(5));
            Assert.That(completed.Select(q=>q.QuestInstanceId).Distinct().Count(), Is.EqualTo(12));
            foreach (var group in completed.GroupBy(q=>q.TemplateId)) Assert.That(group.Select(q=>q.GeneratedVariant).Distinct().Count(), Is.EqualTo(2));
        }
        [Test] public void SaveLoadPreservesActiveTargetsAssignmentsAndCompletedResults()
        {
            var integration = new Quest01Integration(new InventoryState(), new QuestRuntimeState { State = QuestState.Completed, PumpRepaired = true }); var service = integration.RepeatableQuests;
            var q = service.Data.Instances[0]; var item = Add(integration.Inventory);
            service.Accept(q.QuestInstanceId); service.Assign(q.QuestInstanceId, item, Definition);
            var session = Session(integration.Inventory); session.TryBegin(item.InstanceId, _ => true);
            item=Trim(integration.Inventory,service,q,item);
            Machine(item.MachiningWorkpiece, q.GeneratedTargetGeometry);
            string before = JsonUtility.ToJson(service.Data);
            var loaded = SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(SaveGameComposer.Capture(integration))));
            Assert.That(JsonUtility.ToJson(loaded.RepeatableQuests.Data), Is.EqualTo(before));
            Assert.That(loaded.RepeatableQuests.Deliver(q.QuestInstanceId), Is.True);
            var completed = SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(SaveGameComposer.Capture(loaded))));
            Assert.That(completed.RepeatableQuests.Find(q.QuestInstanceId).State, Is.EqualTo(RepeatableQuestStatus.Completed));
            Assert.That(completed.RepeatableQuests.Find(q.QuestInstanceId).DeliveredWorkpiece.Conditions.CorrosionCondition, Is.EqualTo(71));
        }
        [Test] public void OvercutRemainsLostAndReplacementCanBeAssigned()
        {
            var inventory = new InventoryState(); var item = Add(inventory); var service = new RepeatableQuestService(inventory);
            var q = service.Data.Instances[0]; service.Accept(q.QuestInstanceId); service.Assign(q.QuestInstanceId, item, Definition);
            var session = Session(inventory); session.TryBegin(item.InstanceId, _ => true);
            item.MachiningWorkpiece.Geometry.RoundProfile.RemoveMaterial(40, 60, 1); session.End();
            Assert.That(service.Deliver(q.QuestInstanceId), Is.False);
            Assert.That(q.EvaluationResult, Is.EqualTo(PartEvaluationResult.Rejected));
            Assert.That(service.CanAssign(q.QuestInstanceId, item, Definition, out _), Is.False);
            var replacement = Add(inventory);
            Assert.That(service.Assign(q.QuestInstanceId, replacement, Definition), Is.True);
            Assert.That(item.MachiningWorkpiece.Geometry.RoundProfile.RadiusAt(50), Is.EqualTo(1));
            Assert.That(inventory.Workpieces.Count, Is.EqualTo(2));
        }
        [Test] public void OldSavesGainOffersAndCorruptTargetsAreRejected()
        {
            var old = new SaveGameData();
            var restored = SaveGameComposer.Restore(old);
            Assert.That(restored.RepeatableQuests.Data.Instances.Count, Is.Zero);
            Assert.That(restored.RepeatableQuests.IsUnlocked, Is.False);
            Assert.That(restored.Inventory.Workpieces, Is.Empty);
            restored.Quest.Data.State = QuestState.Completed;
            restored.Quest.Data.PumpRepaired = true;
            restored.RepeatableQuests.Unlock();
            var save = SaveGameComposer.Capture(restored);
            save.RepeatableQuests.Instances[0].GeneratedTargetGeometry.Dimensions[0].TargetDiameter = float.NaN;
            Assert.Throws<ArgumentException>(() => save.Validate());
        }
        [Test] public void SmallTargetsRejectAttachedExcessStockAndLegacyTargetsSurviveLoad()
        {
            foreach(var t in RepeatableQuestCatalog.Templates) foreach(var v in t.AllowedVariants)
                Assert.That(v.Requirement.RequiredTotalLength,Is.InRange(40,120));
            var inventory=new InventoryState(); var item=Add(inventory); var service=new RepeatableQuestService(inventory);
            var q=service.Data.Instances[0];service.Accept(q.QuestInstanceId);service.Assign(q.QuestInstanceId,item,Definition);
            var session=Session(inventory);session.TryBegin(item.InstanceId,_=>true);
            Machine(item.MachiningWorkpiece,q.GeneratedTargetGeometry);session.End();
            Assert.That(service.Deliver(q.QuestInstanceId),Is.False);Assert.That(q.EvaluationFeedback,Does.Contain("절단"));
            var integration=new Quest01Integration(inventory, new QuestRuntimeState { State = QuestState.Completed, PumpRepaired = true });
            var legacy=integration.RepeatableQuests.Data.Instances[0];legacy.GeneratedVariant="A";
            legacy.GeneratedTargetGeometry=RepeatableQuestCatalog.SavedRequirement(RepeatableQuestCatalog.Find(legacy.TemplateId),"A");
            string old=JsonUtility.ToJson(legacy);
            var restored=SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(SaveGameComposer.Capture(integration))));
            Assert.That(JsonUtility.ToJson(restored.RepeatableQuests.Find(legacy.QuestInstanceId)),Is.EqualTo(old));
            Assert.That(restored.RepeatableQuests.Find(legacy.QuestInstanceId).GeneratedTargetGeometry.Dimensions.Last().EndZ,Is.EqualTo(300));
        }
    }
}
