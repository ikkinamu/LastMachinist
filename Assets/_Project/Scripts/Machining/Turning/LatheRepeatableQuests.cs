using System.Linq;
using LastMachinist.Quest;
using LastMachinist.Measurement;
using UnityEngine;

namespace LastMachinist.Machining.Turning
{
    public sealed partial class LathePrototype
    {
        private bool repeatableQuestTab;
        private string selectedRepeatableQuestId, preparationQuestId;
        private Vector2 repeatableListScroll, repeatableDetailScroll;
        public RepeatableQuestService RepeatableQuests => questIntegration?.RepeatableQuests;
        public bool PrepareRepeatableQuest(string id)
        {
            if (DialogueBlocked || RepeatableQuests?.Find(id)?.State != RepeatableQuestStatus.Active) return false;
            OpenPage(HudPage.MachineSelect);
            preparationQuestId = id; inventoryTurningSelected = true; tutorialPreparation = false;
            selectedMachiningId = RepeatableQuests.Find(id).AssignedWorkpieceInstanceId;
            return true;
        }
        private MeasurementPoint[] RepeatableMeasurementPoints(RepeatableQuestInstance q)
            => q.GeneratedTargetGeometry.Dimensions.SelectMany((d, i) => new[] {
                new MeasurementPoint("repeat_d" + i, MeasurementPointType.Diameter, d.StartZ, d.EndZ, d.Id + " 외경"),
                new MeasurementPoint("repeat_l" + i, MeasurementPointType.Length, d.StartZ, d.EndZ, d.Id + " 길이") }).ToArray();
        private void DrawRepeatableQuestWindow(GUIStyle label, GUIStyle button)
        {
            var service = RepeatableQuests;
            if (service == null) return;
            GUILayout.BeginArea(new Rect(400, 150, 290, 600), GUI.skin.box);
            repeatableListScroll = GUILayout.BeginScrollView(repeatableListScroll);
            GUILayout.Label("반복 의뢰", label);
            foreach (var q in service.Data.Instances.AsEnumerable().Reverse())
            {
                string status = q.State == RepeatableQuestStatus.Offered ? "수락 가능" : q.State == RepeatableQuestStatus.Active ? "진행 중" : "완료";
                if (GUILayout.Button(RepeatableQuestCatalog.Find(q.TemplateId).DisplayName + "\n" + status,
                    new GUIStyle(button) { fixedHeight = 74, wordWrap = true })) { selectedRepeatableQuestId = q.QuestInstanceId; repeatableDetailScroll = Vector2.zero; }
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(706, 150, 514, 600), GUI.skin.box);
            repeatableDetailScroll = GUILayout.BeginScrollView(repeatableDetailScroll);
            var selected = service.Find(selectedRepeatableQuestId);
            if (selected == null) GUILayout.Label("목록에서 의뢰를 선택하세요.", label);
            else
            {
                var template = RepeatableQuestCatalog.Find(selected.TemplateId);
                GUILayout.Label(template.DisplayName + " · 규격 " + selected.GeneratedVariant, label);
                GUILayout.Label(template.ClientName + "\n" + template.Description, label);
                GUILayout.Space(12);
                GUILayout.Label("의뢰 도면 · 목표 치수 (실측값 아님)", label);
                if(selected.GeneratedTargetGeometry.RequiredTotalLength>0) GUILayout.Label($"완성품 전체 길이 {selected.GeneratedTargetGeometry.RequiredTotalLength:0.##} mm · 긴 소재는 절단 필요",label);
                foreach (var d in selected.GeneratedTargetGeometry.Dimensions)
                    GUILayout.Label($"{d.Id}: Ø{d.TargetDiameter:0.##} × 길이 {d.EndZ-d.StartZ:0.##} mm", label);
                GUILayout.Label($"허용 편차 ±{selected.GeneratedTargetGeometry.FunctionalTolerance:0.##} mm", label);
                if (selected.GeneratedTargetGeometry.RequiredChamferPositions.Count > 0) GUILayout.Label("도면 오른쪽 끝단: 외측 45° 모따기", label);
                if (selected.State == RepeatableQuestStatus.Offered)
                { if (GUILayout.Button("수락", button)) service.Accept(selected.QuestInstanceId); }
                else if (selected.State == RepeatableQuestStatus.Active)
                {
                    GUILayout.Label(service.Objective(selected), label);
                    if (!string.IsNullOrEmpty(selected.AssignedWorkpieceInstanceId)) GUILayout.Label("연결 공작물: " + selected.AssignedWorkpieceInstanceId, label);
                    if (GUILayout.Button("공작물 선택 / 가공 계속", button)) PrepareRepeatableQuest(selected.QuestInstanceId);
                    if (GUILayout.Button("실제 형상 검사 · 합격 시 납품", button)) service.Deliver(selected.QuestInstanceId);
                }
                else GUILayout.Label("납품 완료 · 새 의뢰가 목록에 추가되었습니다.", label);
                if (!string.IsNullOrEmpty(selected.EvaluationFeedback)) GUILayout.Label(selected.EvaluationFeedback, label);
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private void DrawRepeatableTracker(GUIStyle label)
        {
            var service = RepeatableQuests;
            var q = service?.ForWorkpiece(ActiveMachiningWorkpiece?.InstanceId) ?? service?.Data.Instances.Find(v => v.State == RepeatableQuestStatus.Active);
            if (q != null)
            {
                GUILayout.Label(RepeatableQuestCatalog.Find(q.TemplateId).DisplayName + "\n" + service.Objective(q), label);
                if (ActiveMachiningWorkpiece != null)
                {
                    GUILayout.Label("의뢰 도면 · 목표 치수", label);
                    foreach (var d in q.GeneratedTargetGeometry.Dimensions)
                        GUILayout.Label($"{d.Id}: Ø{d.TargetDiameter:0.##} × {d.EndZ-d.StartZ:0.##} mm", label);
                    if (q.GeneratedTargetGeometry.RequiredChamferPositions.Count > 0) GUILayout.Label("오른쪽 끝단 45° 모따기", label);
                }
            }
        }
    }
}
