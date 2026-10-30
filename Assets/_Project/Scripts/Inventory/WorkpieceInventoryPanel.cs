using System;
using UnityEngine;

namespace LastMachinist.Inventory
{
    // The same safe projection is used by the UI and by information-disclosure tests.
    public static class WorkpieceInventoryPresentation
    {
        public static string Name(WorkpieceDefinition definition) => definition != null ? definition.DisplayName : "공작물 (정의 정보 없음)";
        public static string Dimensions(WorkpieceInstance instance, WorkpieceDefinition definition)
        {
            var geometry = instance?.MachiningWorkpiece?.Geometry;
            if (geometry != null && geometry.Type != LastMachinist.Machining.Core.GeometryType.RoundProfile && geometry.General != null)
            {
                var g = geometry.General;
                switch (geometry.Type)
                {
                    case LastMachinist.Machining.Core.GeometryType.SquareBar:
                    case LastMachinist.Machining.Core.GeometryType.RectangularBar: return $"현재 {g.Width:0.##} × {g.Height:0.##} × {g.Length:0.##} mm";
                    case LastMachinist.Machining.Core.GeometryType.Plate: return $"현재 {g.Width:0.##} × {g.Length:0.##} × t{g.Thickness:0.##} mm";
                    case LastMachinist.Machining.Core.GeometryType.Tube: return $"현재 Ø{g.OuterDiameter:0.##} × t{g.Thickness:0.##} × {g.Length:0.##} mm";
                }
            }
            return CurrentLength(instance, definition) + " / " + CurrentDiameter(instance, definition);
        }
        public static string CurrentLength(WorkpieceInstance instance, WorkpieceDefinition definition)
            => InventoryWorkpieceGeometry.TryGetActualLength(instance, definition, out float length)
                ? "현재 길이: " + length.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " mm"
                : "현재 길이: 형상 정보 없음";

        public static string ListEntry(WorkpieceInstance instance, WorkpieceDefinition definition)
            => (string.IsNullOrEmpty(instance.ParentInstanceId) ? "" : "절단 조각 · ") + Name(definition) + "\n" + Dimensions(instance, definition);
        public static string CurrentDiameter(WorkpieceInstance instance, WorkpieceDefinition definition)
            => InventoryWorkpieceGeometry.TryGetRepresentativeDiameter(instance, definition, out float diameter)
                ? "현재 최대 외경: Ø" + diameter.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " mm"
                : "현재 형상 정보 없음";
        public static string Details(WorkpieceInstance instance, WorkpieceDefinition definition)
        {
            string shape = "명목 규격 정보 없음";
            if (definition != null && definition.NominalDimensions != null)
            {
                var size = definition.NominalDimensions;
                shape = definition.ShapeType == WorkpieceShape.RoundBar
                    ? $"원형 봉재 · 기준 직경 Ø{size.Diameter:0.##} mm\n기준 길이: {size.Length:0.0} mm"
                    : definition.ShapeType == WorkpieceShape.Plate
                        ? $"판재 · 기준 {size.Width:0.##} × {size.Length:0.##} × t{size.Thickness:0.##} mm"
                        : $"{definition.ShapeType} · 기준 {size.Width:0.##} × {size.Height:0.##} × {size.Length:0.##} mm";
            }
            string materialText = "재료: 미식별\n\n[재료 특성]\n경도: ???\n강도: ???\n절삭성: ???\n내식성: ???";
            if(!string.IsNullOrEmpty(instance.ParentInstanceId)) shape="절단 조각 · 기준 치수는 원소재 정보입니다.\n"+shape;
            if (instance.IdentificationState == IdentificationState.Identified)
            {
                var material = definition != null ? definition.Material : null;
                materialText = material != null && material.Id == instance.MaterialId
                    ? $"재료: {material.DisplayName} (식별됨)\n\n[재료 특성 · 게임 점수 / 100]\n경도: {material.HardnessRating}\n강도: {material.StrengthRating}\n절삭성: {material.MachinabilityRating}\n내식성: {material.CorrosionResistanceRating}"
                    : "재료: 식별됨 (정의 정보 없음)";
            }
            var c = instance.Conditions;
            return Name(definition) + "\n" + Dimensions(instance, definition) + "\n" + shape + "\n보유 상태: " + (instance.Availability == WorkpieceAvailability.InMachining ? "가공 중" : "사용 가능") + "\n\n" + materialText +
                $"\n\n[공작물 상태 · 높을수록 양호 / 100]\n부식 상태: {c.CorrosionCondition}\n표면 상태: {c.SurfaceCondition}\n형상 건전성: {c.ShapeIntegrity}\n결함 건전성: {c.DefectIntegrity}\n\n종합 상태: {instance.OverallConditionScore:0.##}\n종합 점수는 비교용이며 가공 적합 판정이 아닙니다.";
        }
    }

    public sealed class WorkpieceInventoryPanel
    {
        private string selectedId;
        private Vector2 listScroll, detailScroll;
        private WorkpieceDefinition[] definitions;
        public string SelectedId => selectedId;
        public bool Select(InventoryState inventory, string instanceId)
        {
            if (inventory == null || inventory.FindWorkpiece(instanceId) == null) return false;
            selectedId = instanceId; detailScroll = Vector2.zero; return true;
        }
        public void Draw(InventoryState inventory, GUIStyle label, GUIStyle button)
        {
            if (inventory == null) return;
            definitions ??= Resources.LoadAll<WorkpieceDefinition>("WorkpieceDefinitions");
            if (inventory.FindWorkpiece(selectedId) == null) selectedId = null;
            GUILayout.BeginArea(new Rect(400, 210, 290, 540), GUI.skin.box);
            listScroll = GUILayout.BeginScrollView(listScroll);
            GUILayout.Label("보유 공작물", label);
            if (inventory.ListWorkpieces.Count == 0) GUILayout.Label("보유한 공작물이 없습니다.", label);
            for (int i = 0; i < inventory.ListWorkpieces.Count; i++)
            {
                var piece = inventory.ListWorkpieces[i];
                var definition = Array.Find(definitions, d => d.WorkpieceDefinitionId == piece.WorkpieceDefinitionId);
                if (GUILayout.Button((selectedId == piece.InstanceId ? "▶ " : "") + (i + 1) + ". " + WorkpieceInventoryPresentation.ListEntry(piece, definition) + "\n종합 상태 " + piece.OverallConditionScore.ToString("0.##"), new GUIStyle(button) { fixedHeight = 0, wordWrap = true })) Select(inventory, piece.InstanceId);
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(710, 120, 510, 630), GUI.skin.box);
            detailScroll = GUILayout.BeginScrollView(detailScroll);
            var selected = inventory.FindWorkpiece(selectedId);
            GUILayout.Label(selected == null ? "목록에서 공작물을 선택하세요."
                : WorkpieceInventoryPresentation.Details(selected, Array.Find(definitions, d => d.WorkpieceDefinitionId == selected.WorkpieceDefinitionId)), label);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
    }
}
