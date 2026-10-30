using System;
using System.Linq;
using LastMachinist.Inventory;
using LastMachinist.Machining.Core;

namespace LastMachinist.Quest
{
    public enum WorkshopQuestStatus { NotStarted, Offered, Active, Completed }
    [Serializable]
    public sealed class WorkshopStoryState
    {
        public WorkshopQuestStatus Quest02, Quest03;
        public bool BenchDiscovered, BenchAcquired, BenchInstalled;
        public string DeliveredInstanceId;
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(WorkshopQuestStatus), Quest02) || !Enum.IsDefined(typeof(WorkshopQuestStatus), Quest03) ||
                (BenchInstalled && !BenchAcquired) || (BenchAcquired && !BenchDiscovered) ||
                (Quest02 == WorkshopQuestStatus.Completed && !BenchInstalled) ||
                (Quest03 != WorkshopQuestStatus.NotStarted && Quest02 != WorkshopQuestStatus.Completed))
                throw new ArgumentException("Invalid workshop story state.");
        }
    }
    public sealed class WorkshopStory
    {
        public const string BenchDiscoveryId = "bench_drill";
        private readonly InventoryState inventory;
        private readonly Func<bool> pumpComplete;
        public WorkshopStoryState Data { get; }
        public event Action Changed;
        public string Feedback { get; private set; }
        public WorkshopStory(InventoryState inventory, Func<bool> pumpComplete, WorkshopStoryState state = null)
        { this.inventory = inventory; this.pumpComplete = pumpComplete; Data = state ?? new WorkshopStoryState(); Data.Validate(); }
        public bool Offer02()
        {
            if (!pumpComplete() || Data.Quest02 != WorkshopQuestStatus.NotStarted) return false;
            Data.Quest02 = WorkshopQuestStatus.Offered; Changed?.Invoke(); return true;
        }
        public bool Accept02()
        {
            if (!pumpComplete() || Data.Quest02 != WorkshopQuestStatus.Offered) return false;
            Data.Quest02 = WorkshopQuestStatus.Active; Changed?.Invoke(); return true;
        }
        public bool DiscoverBench()
        {
            if (!pumpComplete() || Data.Quest02 != WorkshopQuestStatus.Active) return false;
            Data.BenchDiscovered = true; Changed?.Invoke(); return true;
        }
        public bool RecoverBench()
        {
            if (!Data.BenchDiscovered || Data.BenchAcquired || Data.Quest02 != WorkshopQuestStatus.Active) return false;
            Data.BenchAcquired = true; Changed?.Invoke(); return true;
        }
        public bool InstallBench()
        {
            if (!Data.BenchAcquired || Data.BenchInstalled || Data.Quest02 != WorkshopQuestStatus.Active) return false;
            Data.BenchInstalled = true; Data.Quest02 = WorkshopQuestStatus.Completed; Changed?.Invoke(); return true;
        }
        public bool Offer03()
        {
            if (!pumpComplete() || Data.Quest02 != WorkshopQuestStatus.Completed || !Data.BenchInstalled || Data.Quest03 != WorkshopQuestStatus.NotStarted) return false;
            Data.Quest03 = WorkshopQuestStatus.Offered; Changed?.Invoke(); return true;
        }
        public bool Accept03()
        {
            if (!pumpComplete() || !Data.BenchInstalled || Data.Quest03 != WorkshopQuestStatus.Offered) return false;
            Data.Quest03 = WorkshopQuestStatus.Active; Changed?.Invoke(); return true;
        }
        public bool Deliver(string id)
        {
            var item = inventory.FindWorkpiece(id);
            if (Data.Quest03 != WorkshopQuestStatus.Active || item == null || item.Availability != WorkpieceAvailability.Available)
            { Feedback = "가공을 종료한 보유 공작물을 선택하세요."; return false; }
            if (!Evaluate(item.MachiningWorkpiece?.Geometry, out var reason)) { Feedback = reason; return false; }
            if (!inventory.RemoveWorkpiece(id)) return false;
            Data.DeliveredInstanceId = id; Data.Quest03 = WorkshopQuestStatus.Completed;
            Feedback = "납품 완료. 고정판을 설치했습니다. 이 작업장에서도 할 수 있는 일이 늘었군요.";
            Changed?.Invoke(); return true;
        }
        public const string Drawing = "의뢰 도면 (실측값 아님)\n판재 100 × 80 × 5 mm\nA: X20 Y20 / B: X70 Y50\n둘 다 Ø6 관통 · 허용 편차 ±0.5 mm\n원점: 판재 왼쪽 아래 모서리";
        public static bool Evaluate(WorkpieceGeometry geometry, out string reason)
        {
            reason = "판재 형상이 필요합니다.";
            if (geometry == null || geometry.Type != GeometryType.Plate || geometry.General == null) return false;
            var g = geometry.General;
            if (Math.Abs(g.Width - 100) > .5f || Math.Abs(g.Length - 80) > .5f || Math.Abs(g.Thickness - 5) > .5f)
            { reason = "판재 기본 치수가 도면과 맞지 않습니다."; return false; }
            var holes = geometry.HoleFeatures;
            if (holes == null || holes.Count != 2) { reason = holes != null && holes.Count > 2 ? "불합격: 추가 구멍이 있습니다." : "구멍 2개가 필요합니다."; return false; }
            foreach (var target in new[] { new[] { 20f, 20f }, new[] { 70f, 50f } })
            {
                var h = holes.FirstOrDefault(v => Math.Abs(v.CenterX - target[0]) <= .5f && Math.Abs(v.CenterY - target[1]) <= .5f);
                if (h == null) { reason = "불합격: 구멍 위치가 도면과 다릅니다."; return false; }
                if (Math.Abs(h.Diameter - 6) > .5f) { reason = "불합격: 구멍 직경이 도면과 다릅니다."; return false; }
                if (!h.IsThrough || h.Depth < g.Thickness) { reason = "아직 관통하지 않은 구멍이 있습니다."; return false; }
            }
            reason = "합격 · 실제 형상이 도면에 맞습니다."; return true;
        }
    }
}
