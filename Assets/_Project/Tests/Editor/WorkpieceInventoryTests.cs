using System;
using System.IO;
using LastMachinist.Core;
using LastMachinist.Inventory;
using LastMachinist.Integration;
using LastMachinist.Save;
using NUnit.Framework;
using UnityEngine;

namespace LastMachinist.Tests
{
    public sealed class WorkpieceInventoryTests
    {
        private static WorkpieceDefinition Definition => Resources.Load<WorkpieceDefinition>("WorkpieceDefinitions/RoundBar50");
        private static ConditionProperties A() => new ConditionProperties { CorrosionCondition = 82, SurfaceCondition = 64, ShapeIntegrity = 95, DefectIntegrity = 91 };
        private static ConditionProperties B() => new ConditionProperties { CorrosionCondition = 43, SurfaceCondition = 88, ShapeIntegrity = 71, DefectIntegrity = 84 };

        [Test] public void SameDefinitionKeepsIndependentInstancesAndScores()
        {
            var source = A();
            var a = Definition.CreateInstance(source, "yard", "session");
            var b = Definition.CreateInstance(B(), "yard", "session");
            source.CorrosionCondition = 0;
            Assert.That(a.InstanceId, Is.Not.EqualTo(b.InstanceId));
            Assert.That(a.WorkpieceDefinitionId, Is.EqualTo(b.WorkpieceDefinitionId));
            Assert.That(a.Conditions.CorrosionCondition, Is.EqualTo(82));
            Assert.That(a.OverallConditionScore, Is.EqualTo(83));
            Assert.That(b.OverallConditionScore, Is.EqualTo(71.5f));
            Assert.That(b.Conditions.SurfaceCondition, Is.EqualTo(88));
            var inventory = new InventoryState();
            Assert.That(inventory.AddWorkpiece(a) && inventory.AddWorkpiece(b), Is.True);
            a.Conditions.CorrosionCondition = 1;
            Assert.That(inventory.FindWorkpiece(a.InstanceId).Conditions.CorrosionCondition, Is.EqualTo(82));
            Assert.That(inventory.ListWorkpieces.Count, Is.EqualTo(2));
            Assert.That(inventory.AddWorkpiece(b), Is.False);
            Assert.That(inventory.RemoveWorkpiece(a.InstanceId), Is.True);
            Assert.That(inventory.FindWorkpiece(b.InstanceId), Is.Not.Null);
            Assert.That(inventory.RemoveWorkpiece(a.InstanceId), Is.False);
        }

        [TestCase(-1)] [TestCase(101)]
        public void RejectsOutOfRangeConditionWithoutInventoryMutation(int score)
        {
            foreach (var field in typeof(ConditionProperties).GetFields())
            {
                var item = Definition.CreateInstance(A(), "yard", "session");
                field.SetValue(item.Conditions, score);
                var inventory = new InventoryState();
                Assert.That(inventory.AddWorkpiece(item), Is.False);
                Assert.That(inventory.ListWorkpieces.Count, Is.Zero);
            }
        }

        [Test] public void ConditionEndpointsAndMaterialRatingsHaveValidRanges()
        {
            new ConditionProperties { CorrosionCondition = 0, SurfaceCondition = 100, ShapeIntegrity = 0, DefectIntegrity = 100 }.Validate();
            Definition.Material.Validate();
            var material = UnityEngine.Object.Instantiate(Definition.Material);
            try { material.HardnessRating = 101; Assert.Throws<ArgumentOutOfRangeException>(() => material.Validate()); }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }

        [Test] public void SelectionAndProjectionKeepUnknownMaterialSecret()
        {
            var a = Definition.CreateInstance(A(), "yard", "session");
            var b = Definition.CreateInstance(B(), "yard", "session");
            var inventory = new InventoryState(); inventory.AddWorkpiece(a); inventory.AddWorkpiece(b);
            var panel = new WorkpieceInventoryPanel();
            Assert.That(panel.Select(inventory, a.InstanceId), Is.True);
            Assert.That(panel.SelectedId, Is.EqualTo(a.InstanceId));
            string detailA = WorkpieceInventoryPresentation.Details(inventory.FindWorkpiece(panel.SelectedId), Definition);
            Assert.That(detailA, Does.Contain("부식 상태: 82").And.Contain("종합 상태: 83"));
            Assert.That(detailA, Does.Contain("재료: 미식별").And.Contain("경도: ???").And.Contain("강도: ???").And.Contain("절삭성: ???").And.Contain("내식성: ???"));
            Assert.That(detailA, Does.Not.Contain(Definition.Material.DisplayName).And.Not.Contain(Definition.Material.Id));
            Assert.That(panel.Select(inventory, b.InstanceId), Is.True);
            string detailB = WorkpieceInventoryPresentation.Details(inventory.FindWorkpiece(panel.SelectedId), Definition);
            Assert.That(detailB, Does.Contain("부식 상태: 43").And.Contain("종합 상태: 71.5"));
            b.IdentificationState = IdentificationState.Identified;
            Assert.That(WorkpieceInventoryPresentation.Details(b, Definition), Does.Contain(Definition.Material.DisplayName).And.Contain("경도: 60"));
        }

        [Test] public void ExistingSaveStoreRoundTripsUniqueStockAndIdentification()
        {
            var inventory = new InventoryState();
            var a = Definition.CreateInstance(A(), "yard", "session");
            var b = Definition.CreateInstance(B(), "yard", "session");
            b.IdentificationState = IdentificationState.Identified;
            inventory.AddWorkpiece(a); inventory.AddWorkpiece(b);
            inventory.Add(Quest01Integration.StockItemId, 3);
            var integration = new Quest01Integration(inventory);
            var store = new SaveGameStore(Path.Combine(Application.temporaryCachePath, "inventory-test-" + Guid.NewGuid().ToString("N") + ".json"));
            try
            {
                store.Save(SaveGameComposer.Capture(integration, "Workshop"));
                var restored = SaveGameComposer.Restore(store.Load());
                Assert.That(restored.Inventory.ListWorkpieces.Count, Is.EqualTo(2));
                foreach (var original in inventory.ListWorkpieces)
                    Assert.That(JsonUtility.ToJson(restored.Inventory.FindWorkpiece(original.InstanceId)), Is.EqualTo(JsonUtility.ToJson(original)));
                Assert.That(restored.Inventory.GetQuantity(Quest01Integration.StockItemId), Is.EqualTo(3));
                Assert.That(restored.Inventory.AddWorkpiece(a), Is.False);
            }
            finally { store.Delete(); }
        }

        [Test] public void OldVersionOneInventoryWithoutWorkpiecesStillLoads()
        {
            var data = new SaveGameData();
            string oldJson = JsonUtility.ToJson(data).Replace(",\"Workpieces\":[]", "");
            Assert.That(oldJson, Does.Not.Contain("\"Workpieces\""));
            var restored = SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(oldJson));
            Assert.That(restored.Inventory.ListWorkpieces.Count, Is.Zero);
        }
    }
}
