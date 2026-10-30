using System;
using LastMachinist.Inventory;
using LastMachinist.Machining.Core;
using LastMachinist.Measurement;
using UnityEngine;

namespace LastMachinist.Machining.BenchDrill
{
    public sealed class BenchDrillBit
    {
        public string Id { get; }
        public float Diameter { get; }
        public BenchDrillBit(string id, float diameter) { Id = id; Diameter = diameter; }
        public static readonly BenchDrillBit[] Presets = {
            new BenchDrillBit("bench_drill_4", 4), new BenchDrillBit("bench_drill_6", 6),
            new BenchDrillBit("bench_drill_8", 8), new BenchDrillBit("bench_drill_10", 10) };
    }
    public enum BenchDrillCommand { MoveX, MoveY, Feed, ZeroX, ZeroY, Clamp, Unclamp, SpindleOn, SpindleOff, SelectBit, Finish }
    public sealed class BenchDrillSession
    {
        private readonly InventoryState inventory;
        private readonly Func<string, WorkpieceDefinition> resolve;
        public event Action Changed;
        public WorkpieceInstance Active { get; private set; }
        public bool Fixed { get; private set; }
        public bool SpindleRunning { get; private set; }
        public BenchDrillBit Bit { get; private set; }
        public float X { get; private set; }
        public float Y { get; private set; }
        public float QuillDepth { get; private set; } = -5; // 0 = top face; negative = clear.
        public float ZeroX { get; private set; }
        public float ZeroY { get; private set; }
        public float ReadingX => X - ZeroX;
        public float ReadingY => Y - ZeroY;
        public float Thickness => Active?.MachiningWorkpiece.Geometry.Type == GeometryType.Plate
            ? Active.MachiningWorkpiece.Geometry.General.Thickness : Active?.MachiningWorkpiece.Geometry.General.Height ?? 0;
        public string Message { get; private set; }
        public BenchDrillSession(InventoryState inventory, Func<string, WorkpieceDefinition> resolve)
        { this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory)); this.resolve = resolve; }
        public static bool Compatible(GeometryType type) => type == GeometryType.Plate || type == GeometryType.SquareBar || type == GeometryType.RectangularBar;
        private bool Fail(string message) { Message = message; return false; }
        private bool Done(string message) { Message = message; Changed?.Invoke(); return true; }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        public bool TryLoad(WorkpieceInstance item)
        {
            if (Active != null || item == null || !ReferenceEquals(inventory.FindWorkpiece(item.InstanceId), item) ||
                item.Availability != WorkpieceAvailability.Available || !string.IsNullOrEmpty(inventory.ActiveMachiningInstanceId))
                return Fail("사용 가능한 보유 공작물을 선택하세요. 다른 가공을 먼저 종료하세요.");
            WorkpieceState state = item.MachiningWorkpiece;
            if (!item.HasMachiningGeometry)
            {
                try { state = InventoryWorkpieceGeometry.CreateInitialState(item, resolve?.Invoke(item.WorkpieceDefinitionId)); }
                catch (ArgumentException) { return Fail("공작물 형상을 읽을 수 없습니다."); }
            }
            if (state == null || !Compatible(state.Geometry.Type)) return Fail("원형봉·튜브용 고정구가 없습니다. 판재·각재·직사각 봉재를 선택하세요.");
            state.Validate();
            item.MachiningWorkpiece = state; item.HasMachiningGeometry = true;
            Active = item; item.Availability = WorkpieceAvailability.InMachining; inventory.ActiveMachiningInstanceId = item.InstanceId;
            Fixed = false; SpindleRunning = false; QuillDepth = -5; X = Y = ZeroX = ZeroY = 0;
            return Done("장착했습니다. 바이스를 고정하고 공구와 위치를 정하세요.");
        }
        public bool Execute(BenchDrillCommand command, float amount = 0)
        {
            if (!Finite(amount)) return Fail("유효하지 않은 이동량입니다.");
            switch (command)
            {
                case BenchDrillCommand.MoveX: return Move(amount, 0);
                case BenchDrillCommand.MoveY: return Move(0, amount);
                case BenchDrillCommand.Feed: return Feed(amount);
                case BenchDrillCommand.ZeroX: ZeroX = X; return Done("X 상대 눈금을 0으로 설정했습니다.");
                case BenchDrillCommand.ZeroY: ZeroY = Y; return Done("Y 상대 눈금을 0으로 설정했습니다.");
                case BenchDrillCommand.Clamp: return Clamp();
                case BenchDrillCommand.Unclamp:
                    if (SpindleRunning || QuillDepth > 0) return Fail("주축을 정지하고 공구를 올리세요.");
                    Fixed = false; return Done("고정을 풀었습니다.");
                case BenchDrillCommand.SpindleOn: return SetSpindle(true);
                case BenchDrillCommand.SpindleOff: return SetSpindle(false);
                case BenchDrillCommand.SelectBit:
                    int i = (int)amount;
                    return i >= 0 && i < BenchDrillBit.Presets.Length && SelectBit(BenchDrillBit.Presets[i]);
                case BenchDrillCommand.Finish: return Finish();
                default: return false;
            }
        }
        public bool Clamp()
        {
            if (Active == null || SpindleRunning || QuillDepth > 0) return Fail("공작물을 장착하고 공구를 올리세요.");
            Fixed = true; return Done("바이스 고정 완료.");
        }
        public bool SelectBit(BenchDrillBit bit)
        {
            if (SpindleRunning || QuillDepth > 0 || bit == null || !Array.Exists(BenchDrillBit.Presets, b => b.Id == bit.Id && b.Diameter == bit.Diameter))
                return Fail("공구 교환은 주축 정지·공구 상승 후 가능합니다.");
            Bit = bit; return Done("드릴 Ø" + bit.Diameter + " 장착.");
        }
        public bool SetSpindle(bool running)
        {
            if (running && (Active == null || !Fixed || Bit == null)) return Fail("공작물·바이스·드릴을 확인하세요.");
            SpindleRunning = running; return Done(running ? "주축 회전 중" : "주축 정지");
        }
        private bool Move(float dx, float dy)
        {
            if (Active == null || QuillDepth >= 0) return Fail("공구를 공작물 위로 올린 뒤 테이블을 이동하세요.");
            var g = Active.MachiningWorkpiece.Geometry.General;
            X = Mathf.Clamp(X + dx, 0, g.Width); Y = Mathf.Clamp(Y + dy, 0, g.Length);
            return Done("테이블 위치 조정");
        }
        private bool Feed(float delta)
        {
            if (Active == null) return Fail("공작물을 장착하세요.");
            float next = Mathf.Clamp(QuillDepth + delta, -5, Thickness + 1);
            if (delta <= 0 || next <= 0) { QuillDepth = next; return Done(next < 0 ? "공구 상승 / 접근" : "공구 후퇴"); }
            if (!Fixed || !SpindleRunning || Bit == null) return Fail("절삭하려면 바이스 고정·드릴 장착·주축 회전이 필요합니다.");
            var geometry = Active.MachiningWorkpiece.Geometry;
            float r = Bit.Diameter / 2;
            if (X - r < 5 || X + r > geometry.General.Width - 5 || Y - r < 0 || Y + r > geometry.General.Length)
                return Fail("공구가 공작물 밖 또는 바이스 영역에 있습니다.");
            var hole = geometry.HoleFeatures.Find(h => Math.Abs(h.CenterX - X) < .001f && Math.Abs(h.CenterY - Y) < .001f);
            foreach (var other in geometry.HoleFeatures)
                if (other != hole && Vector2.Distance(new Vector2(X, Y), new Vector2(other.CenterX, other.CenterY)) < (other.Diameter + Bit.Diameter) / 2)
                    return Fail("기존 구멍과 겹치는 가공은 현재 지원하지 않습니다.");
            if (hole != null && Bit.Diameter != hole.Diameter) return Fail("기존 구멍의 확공은 현재 지원하지 않습니다. 다른 위치나 새 소재를 사용하세요.");
            QuillDepth = next;
            if (hole == null)
            {
                hole = new HoleFeature { FeatureId = Guid.NewGuid().ToString("N"), CenterX = X, CenterY = Y, Diameter = Bit.Diameter, ToolId = Bit.Id };
                geometry.HoleFeatures.Add(hole);
            }
            hole.Depth = Math.Max(hole.Depth, Math.Min(next, Thickness));
            hole.IsThrough = hole.Depth >= Thickness;
            hole.OperationHistory.Add("quill_feed:" + next.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            Active.MachiningWorkpiece.ProcessHistory.Add("bench_drill:" + hole.FeatureId);
            return Done(hole.IsThrough ? "관통했습니다. 공구를 올린 뒤 다음 위치로 이동하세요." : "절삭 중");
        }
        public bool Finish()
        {
            if (Active == null) return false;
            SpindleRunning = false; Fixed = false; QuillDepth = -5;
            Active.Availability = WorkpieceAvailability.Available; inventory.ActiveMachiningInstanceId = null; Active = null;
            return Done("가공 종료 · 같은 공작물을 인벤토리에 보관했습니다.");
        }
        public InstrumentReading Inspect(int index, int dimension)
        {
            if (Active == null || SpindleRunning || QuillDepth >= 0 || index < 0 || index >= Active.MachiningWorkpiece.Geometry.HoleFeatures.Count || dimension < 0 || dimension > 2) return null;
            var h = Active.MachiningWorkpiece.Geometry.HoleFeatures[index];
            return new VernierScale().Read(dimension == 0 ? h.Diameter : dimension == 1 ? h.CenterX : h.CenterY, "bench_inspection_scale");
        }
        public bool Record(int index, int dimension, float value)
        {
            var reading = Inspect(index, dimension);
            if (reading == null || !Finite(value)) return false;
            string key = Active.MachiningWorkpiece.Geometry.HoleFeatures[index].FeatureId + ":" + dimension;
            Active.MachiningMeasurements.RemoveAll(m => m.MeasurementPointId == key);
            Active.MachiningMeasurements.Add(new RecordedMeasurement { WorkpieceInstanceId = Active.InstanceId,
                MeasurementPointId = key, InstrumentId = reading.InstrumentId, InstrumentReadingValue = reading.Value,
                PlayerValue = value, RecordedAtTicks = DateTime.UtcNow.Ticks });
            return Done("입력한 측정값을 기록했습니다.");
        }
    }
}
