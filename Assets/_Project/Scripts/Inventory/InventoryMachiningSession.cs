using System;
using System.Collections.Generic;
using LastMachinist.Machining.Core;
using LastMachinist.Machining.Turning;

namespace LastMachinist.Inventory
{
    public sealed class InventoryMachiningSession
    {
        private readonly InventoryState inventory;
        private readonly Func<string, WorkpieceDefinition> resolve;
        private bool starting;
        public WorkpieceInstance Active
        {
            get
            {
                var item = inventory.FindWorkpiece(inventory.ActiveMachiningInstanceId);
                return item?.MachiningWorkpiece?.Geometry?.Type == GeometryType.RoundProfile ? item : null;
            }
        }
        public InventoryMachiningSession(InventoryState inventory, Func<string, WorkpieceDefinition> resolve)
        { this.inventory = inventory; this.resolve = resolve; }
        public static bool SupportsTurning(WorkpieceDefinition definition)
        {
            if (definition == null || definition.ShapeType != WorkpieceShape.RoundBar || definition.NominalDimensions == null) return false;
            var d = definition.NominalDimensions;
            return d.Diameter > 0 && d.Length > 0 && !float.IsInfinity(d.Diameter) && !float.IsInfinity(d.Length);
        }
        public static string TurningCompatibilityReason(WorkpieceDefinition definition)
        {
            if (definition == null) return "공작물 정의를 찾을 수 없습니다.";
            if (definition.ShapeType == WorkpieceShape.Tube) return "현재 선반은 튜브 내경 형상을 보존하지 않아 지원하지 않습니다.";
            if (definition.ShapeType != WorkpieceShape.RoundBar) return "현재 선반은 환봉만 지원합니다. 각재·판재는 탁상드릴/밀링 설비가 필요합니다.";
            return SupportsTurning(definition) ? string.Empty : "환봉의 현재 치수 정보가 올바르지 않습니다.";
        }
        public List<WorkpieceInstance> Candidates()
        {
            var result = new List<WorkpieceInstance>();
            foreach (var item in inventory.ListWorkpieces)
                if (item.Availability == WorkpieceAvailability.Available && SupportsTurning(resolve(item.WorkpieceDefinitionId))) result.Add(item);
            return result;
        }
        public bool TryBegin(string instanceId, Func<WorkpieceState, bool> initialize)
        {
            if (starting || !string.IsNullOrEmpty(inventory.ActiveMachiningInstanceId) || initialize == null) return false;
            var item = inventory.FindWorkpiece(instanceId);
            if (item == null || item.Availability != WorkpieceAvailability.Available) return false;
            var definition = resolve(item.WorkpieceDefinitionId);
            if (!SupportsTurning(definition)) return false;
            starting = true;
            try
            {
                item.Validate();
                // Initialize from a working copy so a failed adapter cannot corrupt stored cuts.
                var state = item.MachiningWorkpiece?.Copy() ?? InventoryWorkpieceGeometry.CreateInitialState(item, definition);
                state.Validate();
                if (!initialize(state)) return false;
                item.MachiningWorkpiece = state;
                item.HasMachiningGeometry = true;
                item.Availability = WorkpieceAvailability.InMachining;
                inventory.ActiveMachiningInstanceId = item.InstanceId;
                return true;
            }
            catch (Exception) { return false; }
            finally { starting = false; }
        }
        public void End() { if (Active != null) inventory.ReleaseMachining(); }
    }
}
