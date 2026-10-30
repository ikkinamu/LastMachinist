using System;
using System.Collections.Generic;
using LastMachinist.Inventory;
using LastMachinist.Machining.Core;
using LastMachinist.Measurement;
using LastMachinist.Quest;
using UnityEngine;

namespace LastMachinist.Machining.Turning
{
    public sealed partial class LathePrototype
    {
        private InventoryMachiningSession inventoryMachining;
        private WorkpieceDefinition[] workpieceDefinitions;
        private bool tutorialPreparation, inventoryTurningSelected;
        private string selectedMachiningId, inventoryMachiningMessage;
        private Vector2 machiningListScroll, machiningDetailScroll;
        private LatheController previousController;
        private LatheInputRouter previousRouter;
        private MeasurementSession previousMeasurement;
        private MeasurementPoint[] previousMeasurementPoints;
        public WorkpieceInstance ActiveMachiningWorkpiece => inventoryMachining?.Active;
        private bool IsQuest01Machining => questIntegration != null && ActiveMachiningWorkpiece != null &&
            questIntegration.Quest.Data.WorkpieceInstanceId == ActiveMachiningWorkpiece.InstanceId;
        public MeasurementSession CurrentMeasurement => measurement;
        public string SelectedMachiningInstanceId => selectedMachiningId;
        public bool PrepareQuest01Workpiece()
        {
            if (DialogueBlocked || questIntegration == null || !questIntegration.Quest.Data.PumpShaftInferred ||
                questIntegration.Quest.Data.State < QuestState.ShapeInferred || questIntegration.Quest.Data.State > QuestState.FinalInspection) return false;
            OpenPage(HudPage.MachineSelect);
            tutorialPreparation = true; inventoryTurningSelected = true;
            questIntegration.Quest.SelectTurning();
            preparationQuestId = null; selectedMachiningId = null;
            return true;
        }

        private WorkpieceDefinition ResolveWorkpieceDefinition(string id)
        {
            workpieceDefinitions ??= Resources.LoadAll<WorkpieceDefinition>("WorkpieceDefinitions");
            return Array.Find(workpieceDefinitions, d => d.WorkpieceDefinitionId == id);
        }
        public List<WorkpieceInstance> MachiningCandidates() => inventoryMachining?.Candidates() ?? new List<WorkpieceInstance>();
        public bool SelectInventoryWorkpiece(string id)
        {
            if (ActiveMachiningWorkpiece != null || !MachiningCandidates().Exists(w => w.InstanceId == id)) return false;
            if (tutorialPreparation && !questIntegration.CanSelectMaterial(id, out inventoryMachiningMessage)) return false;
            if (!string.IsNullOrEmpty(preparationQuestId) && !RepeatableQuests.CanAssign(preparationQuestId, PlayerInventory.FindWorkpiece(id), ResolveWorkpieceDefinition(PlayerInventory.FindWorkpiece(id).WorkpieceDefinitionId), out inventoryMachiningMessage)) return false;
            selectedMachiningId = id; machiningDetailScroll = Vector2.zero; return true;
        }
        public void CancelInventoryPreparation()
        { preparationQuestId = null; selectedMachiningId = null; inventoryTurningSelected = false; inventoryMachiningMessage = null; }
        public bool StartInventoryMachining()
        {
            if (!initialized || DialogueBlocked || ActiveMachiningWorkpiece != null || inventoryMachining == null || string.IsNullOrEmpty(selectedMachiningId)) return false;
            var repeatQuest = RepeatableQuests?.Find(preparationQuestId) ?? RepeatableQuests?.ForWorkpiece(selectedMachiningId);
            var selectedItem = PlayerInventory.FindWorkpiece(selectedMachiningId);
            if (selectedItem == null) return false;
            if (tutorialPreparation || (questIntegration.Quest.Data.State <= QuestState.FinalInspection && questIntegration.Quest.Data.WorkpieceInstanceId == selectedMachiningId))
            {
                if (!questIntegration.CanSelectMaterial(selectedMachiningId, out inventoryMachiningMessage) || !questIntegration.SelectMaterial(selectedMachiningId)) return false;
                if (!questIntegration.BeginMachining()) return false;
                OpenPage(HudPage.Lathe); return true;
            }
            string oldAssignment = repeatQuest?.AssignedWorkpieceInstanceId;
            bool oldHasEvaluation = repeatQuest?.HasEvaluation ?? false;
            string oldFeedback = repeatQuest?.EvaluationFeedback;
            if (repeatQuest != null && !RepeatableQuests.CanAssign(repeatQuest.QuestInstanceId, selectedItem, ResolveWorkpieceDefinition(selectedItem.WorkpieceDefinitionId), out inventoryMachiningMessage)) return false;
            if (repeatQuest != null) RepeatableQuests.Assign(repeatQuest.QuestInstanceId, selectedItem, ResolveWorkpieceDefinition(selectedItem.WorkpieceDefinitionId));
            previousController = controller; previousRouter = router;
            previousMeasurement = measurement; previousMeasurementPoints = measurementPoints;
            bool success = inventoryMachining.TryBegin(selectedMachiningId, state =>
            {
                float length = state.Geometry.RoundProfile.TotalLength;
                float actualLength = state.Geometry.RoundProfile.GetAxialLength();
                float grip=Mathf.Min(settings.GripLength,actualLength*.15f);
                if (actualLength <= grip + settings.RoughingTool.Width || stock == null || tool == null || viewCamera == null) return false;
                float radius = 0;
                foreach (var segment in state.Geometry.RoundProfile.Segments) radius = Mathf.Max(radius, segment.Radius);
                var next = new LatheController(state, settings.RoughingTool.CreateState(), settings.BaseStep, radius + 5,
                    length + 1, grip, settings.ChamferTool != null ? settings.ChamferTool.CreateState() : new ChamferToolState("chamfer_tool", "모따기 바이트", 1),true);
                var nextMeasurement = new MeasurementSession(next.Profile, state.InstanceId);
                nextMeasurement.ImportRecords(PlayerInventory.FindWorkpiece(state.InstanceId).MachiningMeasurements);
                router.SetActive(false);
                controller.Profile.Changed -= OnCut;
                controller = next; router = new LatheInputRouter(next);
                measurement = nextMeasurement;
                measurementPoints = new[] {
                    new MeasurementPoint("StockDiameter", MeasurementPointType.Diameter, 0, length, "공작물 직경"),
                    new MeasurementPoint("StockLength", MeasurementPointType.Length, 0, length, "공작물 길이") };
                if (repeatQuest != null) measurementPoints = RepeatableMeasurementPoints(repeatQuest);
                controller.Profile.Changed += OnCut;
                stock.GetComponent<TurningWorkpieceView>().Bind(controller.Profile);
                return true;
            });
            if (!success)
            {
                if (repeatQuest != null)
                {
                    repeatQuest.AssignedWorkpieceInstanceId = oldAssignment;
                    repeatQuest.HasEvaluation = oldHasEvaluation;
                    repeatQuest.EvaluationFeedback = oldFeedback;
                }
                RestorePreviousStation();
                inventoryMachiningMessage = "가공을 시작하지 못했습니다. 공작물은 인벤토리에 보관되어 있습니다.";
                return false;
            }
            pendingMeasurementPoint = null; rulerAttached = false; measurementInput = "";
            contactUntil = 0; spindleAngle = 0;
            hudPage = HudPage.Lathe; router.SetActive(isActiveAndEnabled);
            inventoryMachiningMessage = null;
            UpdateVisuals();
            return true;
        }
        public void SyncInventoryMeasurements()
        {
            if (ActiveMachiningWorkpiece != null && measurement != null)
                ActiveMachiningWorkpiece.MachiningMeasurements = measurement.ExportRecords();
        }
        public bool CompleteParting()
        {
            if(ActiveMachiningWorkpiece==null || !controller.PartingComplete) return false;
            string sourceId = ActiveMachiningWorkpiece.InstanceId;
            var q=RepeatableQuests?.ForWorkpiece(ActiveMachiningWorkpiece.InstanceId);
            if(!WorkpieceSplitter.TrySplit(PlayerInventory,controller,out var a,out var b)) return false;
            questIntegration.AfterWorkpieceSplit(sourceId, a, b);
            if(q!=null)
            {
                float target=q.GeneratedTargetGeometry.Dimensions[q.GeneratedTargetGeometry.Dimensions.Count-1].EndZ;
                float la=a.MachiningWorkpiece.Geometry.RoundProfile.TotalLength, lb=b.MachiningWorkpiece.Geometry.RoundProfile.TotalLength;
                q.AssignedWorkpieceInstanceId=la>=target && (lb<target || la<=lb)?a.InstanceId:lb>=target?b.InstanceId:null;
                q.HasEvaluation=false;q.EvaluationFeedback="절단한 공작물을 다시 선택해 가공하세요.";
            }
            router.SetActive(false); RestorePreviousStation(); selectedMachiningId=null; preparationQuestId=null;
            pendingMeasurementPoint=null; rulerAttached=false; hudPage=HudPage.Inventory; UpdateVisuals();
            Notification?.Invoke("절단 완료 · 두 공작물을 인벤토리에 보관했습니다."); return true;
        }
        public void EndInventoryMachining()
        {
            if (ActiveMachiningWorkpiece == null) return;
            router.SetActive(false);
            SyncInventoryMeasurements();
            inventoryMachining.End();
            RestorePreviousStation();
            pendingMeasurementPoint = null; rulerAttached = false; measurementInput = "";
            hudPage = HudPage.Workshop;
            selectedMachiningId = null;
            UpdateVisuals();
        }
        private void RestorePreviousStation()
        {
            if (previousController == null) return;
            controller.Profile.Changed -= OnCut;
            controller = previousController; router = previousRouter;
            measurement = previousMeasurement; measurementPoints = previousMeasurementPoints;
            controller.Profile.Changed -= OnCut;
            controller.Profile.Changed += OnCut;
            stock.GetComponent<TurningWorkpieceView>().Bind(controller.Profile);
            router.SetActive(false);
            previousController = null; previousRouter = null; previousMeasurement = null; previousMeasurementPoints = null;
        }
        public string InventoryMaterialLabel()
        {
            var item = ActiveMachiningWorkpiece;
            if (item == null || item.IdentificationState == IdentificationState.Unknown) return "재료 미식별";
            var material = ResolveWorkpieceDefinition(item.WorkpieceDefinitionId)?.Material;
            return material != null && material.Id == item.MaterialId ? material.DisplayName : "식별됨";
        }
        private void DrawInventoryMachiningPreparation(GUIStyle title, GUIStyle label, GUIStyle button)
        {
            GUI.Label(new Rect(404, 35, 660, 48), "작업 준비 · 보유 공작물", title);
            if (!inventoryTurningSelected)
            {
                GUI.Label(new Rect(420, 112, 720, 32), "1. 가공 방식 선택", label);
                if (GUI.Button(new Rect(420, 160, 760, 52), "선반 · 회전체 가공", button)) {
                    inventoryTurningSelected = true;
                    if (tutorialPreparation) questIntegration.Quest.SelectTurning();
                }
                GUI.enabled = questIntegration?.Story.Data.BenchInstalled == true;
                if (GUI.Button(new Rect(420, 220, 760, 48), "탁상드릴 · 판재 / 각재 구멍 가공", button)) OpenPage(HudPage.BenchDrill);
                GUI.enabled = false;
                GUI.Button(new Rect(420, 280, 760, 48), "밀링 · 준비 중", button);
                GUI.enabled = true;
                bool tutorialAvailable = questIntegration != null && (questIntegration.Quest.Data.State == QuestState.ShapeInferred || CanOpenPage(QuestPage.Lathe));
                if (tutorialAvailable && GUI.Button(new Rect(420, 340, 760, 48), "QUEST 01 · 공작물 선택", button)) PrepareQuest01Workpiece();
                return;
            }
            var candidates = MachiningCandidates();
            GUI.Label(new Rect(404, 100, 800, 35), "2. 공작물 선택 · 선반" + (tutorialPreparation ? " · QUEST 01" : string.IsNullOrEmpty(preparationQuestId) ? "" : " · 반복 의뢰"), label);
            GUILayout.BeginArea(new Rect(404, 145, 330, 500), GUI.skin.box);
            machiningListScroll = GUILayout.BeginScrollView(machiningListScroll);
            if (candidates.Count == 0) GUILayout.Label(ExplorationUnlocked
                ? "사용 가능한 공작물이 없습니다. 탐색에서 적합한 공작물을 확보하세요."
                : "사용 가능한 공작물이 없습니다. 이전 저장을 불러오세요.", label);
            for (int i = 0; i < candidates.Count; i++)
            {
                var item = candidates[i]; var definition = ResolveWorkpieceDefinition(item.WorkpieceDefinitionId);
                string name = (selectedMachiningId == item.InstanceId ? "▶ " : "") + (i + 1) + ". " + WorkpieceInventoryPresentation.ListEntry(item, definition);
                string description = $"\n상태 {item.OverallConditionScore:0.##} · " +
                    (item.IdentificationState == IdentificationState.Unknown ? "재료 미식별" : "재료 식별됨");
                if (GUILayout.Button(name + description, new GUIStyle(button) { fixedHeight = 0, wordWrap = true })) SelectInventoryWorkpiece(item.InstanceId);
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
            var selected = candidates.Find(w => w.InstanceId == selectedMachiningId);
            GUILayout.BeginArea(new Rect(750, 145, 470, 500), GUI.skin.box);
            machiningDetailScroll = GUILayout.BeginScrollView(machiningDetailScroll);
            GUILayout.Label(selected == null ? "공작물을 선택하세요." : WorkpieceInventoryPresentation.Details(selected, ResolveWorkpieceDefinition(selected.WorkpieceDefinitionId)) +
                (selected.MachiningWorkpiece == null ? "\n\n첫 가공: 초기 소재 형상으로 장착합니다." : "\n\n기존 가공 형상을 유지합니다. 세부 치수는 측정으로 확인하세요."), label);
            GUILayout.EndScrollView(); GUILayout.EndArea();
            GUI.enabled = selected != null;
            if (GUI.Button(new Rect(930, 675, 290, 48), "가공 시작", button)) StartInventoryMachining();
            GUI.enabled = true;
            if (GUI.Button(new Rect(404, 675, 240, 48), "취소 · 방식 선택", button)) CancelInventoryPreparation();
            GUI.Label(new Rect(404, 736, 810, 50), inventoryMachiningMessage ?? "", label);
        }
    }
}
