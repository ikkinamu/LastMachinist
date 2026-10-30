using System;
using System.Collections.Generic;
using System.Linq;
using LastMachinist.Evaluation;
using LastMachinist.Inventory;
using LastMachinist.Measurement;

namespace LastMachinist.Quest
{
    public enum RepeatableQuestStatus { Offered, Active, Completed }
    [Serializable] public sealed class RepeatableQuestVariant
    {
        public string Id;
        public PartRequirement Requirement;
    }
    // Static, verified presets; runtime instances own independent copies of their targets.
    public sealed class RepeatableQuestTemplate
    {
        public string TemplateId, DisplayName, ClientType, ClientName, Description, PartType;
        public bool Repeatable = true;
        public RepeatableQuestVariant[] AllowedVariants;
        public string[] RequiredFeatures;
    }
    [Serializable] public sealed class RepeatableQuestInstance
    {
        public string QuestInstanceId, TemplateId, GeneratedVariant, AssignedWorkpieceInstanceId;
        public PartRequirement GeneratedTargetGeometry;
        public RepeatableQuestStatus State;
        public bool HasEvaluation;
        public PartEvaluationResult EvaluationResult;
        public string EvaluationFeedback;
        public WorkpieceInstance DeliveredWorkpiece;
    }
    [Serializable] public sealed class RepeatableQuestState
    {
        public bool Initialized;
        public int Generation;
        public List<RepeatableQuestInstance> Instances = new List<RepeatableQuestInstance>();
    }
    public static class RepeatableQuestCatalog
    {
        public static readonly RepeatableQuestTemplate[] Templates = {
            Make("pump_aux", "마모된 펌프 보조축", "농장", new[]{40f,46f,38f}, new[]{38f,44f,36f}, false),
            Make("connecting_pin", "교체용 연결핀", "작업장", new[]{38f,42f}, new[]{36f,40f}, true),
            Make("guide_roller", "가이드 롤러축", "이송 장치", new[]{38f,46f,38f}, new[]{36f,44f,36f}, false),
            Make("bearing_support", "베어링 지지축", "작업장", new[]{46f,40f,34f}, new[]{44f,38f,32f}, false),
            Make("valve_shaft", "밸브 조작축", "농장", new[]{44f,38f,32f}, new[]{42f,36f}, true)
        };
        public static RepeatableQuestTemplate Find(string id) => Array.Find(Templates, t => t.TemplateId == id);
        private static RepeatableQuestTemplate Make(string id, string name, string client, float[] a, float[] b, bool chamfer)
            => new RepeatableQuestTemplate { TemplateId = id, DisplayName = name, ClientType = client, ClientName = client + " 정비 의뢰",
                PartType = "단차 실축", Description = "도면의 외경과 구간 길이에 맞춰 교체 부품을 제작하고 측정·검사 후 납품하세요.",
                RequiredFeatures = chamfer ? new[]{ "외경", "단차", "외측 45° 모따기" } : new[]{ "외경", "단차" },
                AllowedVariants = new[]{ Variant("A3", a, chamfer, Length(id,false)), Variant("B3", b, chamfer, Length(id,true)) } };
        private static float Length(string id,bool second) => id=="connecting_pin" ? (second?60:40) : id=="guide_roller" ? (second?100:80) : id=="bearing_support" ? (second?120:90) : id=="valve_shaft" ? (second?100:70) : second?120:100;
        private static RepeatableQuestVariant Variant(string id, float[] diameters, bool chamfer, float length)
        {
            var r = new PartRequirement { AssessCurrentFeatureLengths = true, RequiredTotalLength=length, PerfectTolerance = .1f, FunctionalTolerance = .5f };
            float z = 0;
            for (int i = 0; i < diameters.Length; i++)
            {
                float end = i == diameters.Length - 1 ? length : (i + 1) * (float)Math.Floor(length/diameters.Length);
                r.Dimensions.Add(new DimensionRequirement("구간 " + (i + 1), z, end, diameters[i])); z = end;
            }
            if (chamfer) r.RequiredChamferPositions.Add(length);
            return new RepeatableQuestVariant { Id = id, Requirement = r };
        }
        // Existing A/B saves retain their exact accepted targets. Only new A3/B3 offers are generated.
        public static PartRequirement SavedRequirement(RepeatableQuestTemplate template,string variant)
        {
            var current=Array.Find(template.AllowedVariants,v=>v.Id==variant);
            if(current!=null) return current.Requirement;
            if(variant!="A" && variant!="B") return null;
            var r=Copy(template.AllowedVariants[variant=="A"?0:1].Requirement); r.RequiredTotalLength=0;
            for(int i=0;i<r.Dimensions.Count;i++)
            { r.Dimensions[i].StartZ=i*(variant=="A"?100:90);r.Dimensions[i].EndZ=i==r.Dimensions.Count-1?300:(i+1)*(variant=="A"?100:90); }
            if(r.RequiredChamferPositions.Count>0) r.RequiredChamferPositions[0]=300;
            return r;
        }
        public static PartRequirement Copy(PartRequirement r) => new PartRequirement { PerfectTolerance = r.PerfectTolerance,
            FunctionalTolerance = r.FunctionalTolerance, AssessCurrentFeatureLengths = r.AssessCurrentFeatureLengths, RequiredTotalLength=r.RequiredTotalLength,
            Dimensions = r.Dimensions.Select(d => new DimensionRequirement(d.Id, d.StartZ, d.EndZ, d.TargetDiameter)).ToList(),
            RequiredChamferPositions = new List<float>(r.RequiredChamferPositions) };
    }
    public sealed class RepeatableQuestService
    {
        public event Action Changed;
        public RepeatableQuestState Data { get; }
        private readonly InventoryState inventory;
        private readonly Func<bool> isUnlocked;
        public bool IsUnlocked => isUnlocked();
        public RepeatableQuestService(InventoryState inventory, RepeatableQuestState data = null, Func<bool> isUnlocked = null)
        {
            this.inventory = inventory; this.isUnlocked = isUnlocked ?? (() => true); Data = data ?? new RepeatableQuestState();
            Validate(Data, inventory);
            Unlock();
        }
        public void Unlock()
        {
            if (!IsUnlocked) return;
            Data.Initialized = true;
            Refill();
        }
        public RepeatableQuestInstance Find(string id) => Data.Instances.Find(q => q.QuestInstanceId == id);
        public RepeatableQuestInstance ForWorkpiece(string id) => string.IsNullOrEmpty(id) ? null : Data.Instances.Find(q => q.State == RepeatableQuestStatus.Active && q.AssignedWorkpieceInstanceId == id);
        private void Refill()
        {
            while (Data.Instances.Count(q => q.State != RepeatableQuestStatus.Completed) < 3)
            {
                var choices = RepeatableQuestCatalog.Templates;
                RepeatableQuestTemplate template = null;
                for (int i = 0; i < choices.Length; i++)
                {
                    var candidate = choices[(Data.Generation + i) % choices.Length];
                    if (!Data.Instances.Exists(q => q.TemplateId == candidate.TemplateId && q.State != RepeatableQuestStatus.Completed)) { template = candidate; break; }
                }
                if (template == null) break;
                int count = Data.Instances.Count(q => q.TemplateId == template.TemplateId);
                var variant = template.AllowedVariants[count % template.AllowedVariants.Length];
                Data.Instances.Add(new RepeatableQuestInstance { QuestInstanceId = Guid.NewGuid().ToString("N"), TemplateId = template.TemplateId,
                    GeneratedVariant = variant.Id, GeneratedTargetGeometry = RepeatableQuestCatalog.Copy(variant.Requirement) });
                Data.Generation++;
            }
        }
        public bool Accept(string id)
        {
            var q = Find(id); if (!IsUnlocked || q == null || q.State != RepeatableQuestStatus.Offered) return false;
            q.State = RepeatableQuestStatus.Active; Changed?.Invoke(); return true;
        }
        public bool CanAssign(string id, WorkpieceInstance item, WorkpieceDefinition definition, out string reason)
        {
            reason = "사용 가능한 공작물이 없습니다. 탐색에서 적합한 공작물을 확보하세요.";
            var q = Find(id);
            if (!IsUnlocked || q == null || q.State != RepeatableQuestStatus.Active || item == null || inventory.FindWorkpiece(item.InstanceId) != item ||
                item.Availability != WorkpieceAvailability.Available || !InventoryMachiningSession.SupportsTurning(definition)) return false;
            var linked = ForWorkpiece(item.InstanceId);
            if (linked != null && linked != q) { reason = "다른 진행 중인 의뢰에 연결된 공작물입니다."; return false; }
            var r = q.GeneratedTargetGeometry; var size = definition.NominalDimensions;
            if (!InventoryWorkpieceGeometry.TryGetActualLength(item, definition, out float actualLength))
            { reason = "공작물의 현재 형상을 확인할 수 없습니다."; return false; }
            if (size.Diameter < r.Dimensions.Max(d => d.TargetDiameter) + 1 || actualLength < r.Dimensions.Last().EndZ)
            { reason = "이 공작물은 목표 형상을 가공하기에 크기가 부족합니다."; return false; }
            if (item.HasMachiningGeometry && PartEvaluator.Evaluate(item.MachiningWorkpiece.Geometry.RoundProfile, r).Result == PartEvaluationResult.Rejected)
            { reason = "이미 제거된 재료가 부족합니다. 다른 공작물을 선택하세요."; return false; }
            reason = null; return true;
        }
        public bool Assign(string id, WorkpieceInstance item, WorkpieceDefinition definition)
        {
            if (!CanAssign(id, item, definition, out _)) return false;
            var q = Find(id); q.AssignedWorkpieceInstanceId = item.InstanceId; q.HasEvaluation = false; q.EvaluationFeedback = null; return true;
        }
        public string Objective(RepeatableQuestInstance q)
        {
            if (q.State != RepeatableQuestStatus.Active) return "";
            var item = inventory.FindWorkpiece(q.AssignedWorkpieceInstanceId);
            if (item == null) return "의뢰용 공작물을 선택하세요.";
            if (!item.HasMachiningGeometry) return "선택한 공작물을 선반에 장착하세요.";
            WorkpieceSplitter.MaterialBounds(item.MachiningWorkpiece.Geometry.RoundProfile,out float start,out float end);
            if(q.GeneratedTargetGeometry.RequiredTotalLength>0 && end-start>q.GeneratedTargetGeometry.RequiredTotalLength+q.GeneratedTargetGeometry.FunctionalTolerance)
                return "절단 바이트로 의뢰 길이의 소재를 분리하세요.";
            if (item.MachiningMeasurements.Count == 0) return "외경과 단차를 가공한 뒤 주요 치수를 측정·기록하세요.";
            return "가공을 종료하고 퀘스트 창에서 검사·납품하세요.";
        }
        public bool Deliver(string id)
        {
            var q = Find(id); if (!IsUnlocked || q == null || q.State != RepeatableQuestStatus.Active) return false;
            var item = inventory.FindWorkpiece(q.AssignedWorkpieceInstanceId);
            if (item == null || !item.HasMachiningGeometry || item.Availability != WorkpieceAvailability.Available)
            { q.EvaluationFeedback = "연결된 공작물의 가공을 종료한 뒤 검사하세요."; return false; }
            var result = PartEvaluator.Evaluate(item.MachiningWorkpiece.Geometry.RoundProfile, q.GeneratedTargetGeometry);
            q.HasEvaluation = true; q.EvaluationResult = result.Result; q.EvaluationFeedback = result.Reason;
            if (result.Result != PartEvaluationResult.Perfect && result.Result != PartEvaluationResult.Acceptable) return false;
            // Preserve provenance and conditions; the delivered item cannot be reused for another order.
            q.DeliveredWorkpiece = item.Copy();
            if (!inventory.RemoveWorkpiece(item.InstanceId)) return false;
            q.State = RepeatableQuestStatus.Completed; Refill(); Changed?.Invoke(); return true;
        }
        public static void Validate(RepeatableQuestState data, InventoryState inventory)
        {
            if (data == null) return; // Older saves migrate in the service constructor.
            if (data.Instances == null || data.Generation < 0) throw new ArgumentException("Invalid repeatable quests.");
            var ids = new HashSet<string>(); var templates = new HashSet<string>(); var assigned = new HashSet<string>();
            foreach (var q in data.Instances)
            {
                var template = q == null ? null : RepeatableQuestCatalog.Find(q.TemplateId);
                if (template == null || string.IsNullOrEmpty(q.QuestInstanceId) || !ids.Add(q.QuestInstanceId) || !Enum.IsDefined(typeof(RepeatableQuestStatus), q.State) ||
                    SavedTargetMissing(template,q) || q.GeneratedTargetGeometry == null)
                    throw new ArgumentException("Invalid repeatable quest identity.");
                var r = q.GeneratedTargetGeometry;
                var expected = RepeatableQuestCatalog.SavedRequirement(template,q.GeneratedVariant);
                if (r.Dimensions == null || r.RequiredChamferPositions == null || r.Dimensions.Count != expected.Dimensions.Count ||
                    r.FunctionalTolerance != expected.FunctionalTolerance || r.RequiredTotalLength!=expected.RequiredTotalLength || r.PerfectTolerance != expected.PerfectTolerance || !r.AssessCurrentFeatureLengths ||
                    !r.RequiredChamferPositions.SequenceEqual(expected.RequiredChamferPositions)) throw new ArgumentException("Invalid saved target.");
                for (int i = 0; i < r.Dimensions.Count; i++)
                { var a = r.Dimensions[i]; var b = expected.Dimensions[i]; if (a == null || a.Id != b.Id || a.StartZ != b.StartZ || a.EndZ != b.EndZ || a.TargetDiameter != b.TargetDiameter) throw new ArgumentException("Invalid saved dimension."); }
                if (q.State != RepeatableQuestStatus.Completed && !templates.Add(q.TemplateId)) throw new ArgumentException("Duplicate live template.");
                if (q.HasEvaluation && !Enum.IsDefined(typeof(PartEvaluationResult), q.EvaluationResult)) throw new ArgumentException("Invalid saved evaluation.");
                if (q.State == RepeatableQuestStatus.Offered && !string.IsNullOrEmpty(q.AssignedWorkpieceInstanceId)) throw new ArgumentException("Offered quest has assigned stock.");
                if (q.State == RepeatableQuestStatus.Active && !string.IsNullOrEmpty(q.AssignedWorkpieceInstanceId) &&
                    (!assigned.Add(q.AssignedWorkpieceInstanceId) || inventory.FindWorkpiece(q.AssignedWorkpieceInstanceId) == null)) throw new ArgumentException("Invalid assigned workpiece.");
                if (q.State == RepeatableQuestStatus.Completed)
                { if (!q.HasEvaluation || (q.EvaluationResult != PartEvaluationResult.Perfect && q.EvaluationResult != PartEvaluationResult.Acceptable) ||
                    q.DeliveredWorkpiece == null || q.DeliveredWorkpiece.InstanceId != q.AssignedWorkpieceInstanceId || inventory.FindWorkpiece(q.AssignedWorkpieceInstanceId) != null)
                    throw new ArgumentException("Missing or duplicated delivered result."); q.DeliveredWorkpiece.Validate(); }
            }
            if (data.Instances.Count(q => q.State == RepeatableQuestStatus.Offered) > 3) throw new ArgumentException("Too many offered quests.");
        }
        private static bool SavedTargetMissing(RepeatableQuestTemplate template,RepeatableQuestInstance q) => RepeatableQuestCatalog.SavedRequirement(template,q.GeneratedVariant)==null;
    }
}
