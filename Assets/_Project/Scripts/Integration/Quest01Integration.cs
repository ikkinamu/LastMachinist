using System;
using System.Collections.Generic;
using System.Linq;
using LastMachinist.Evaluation;
using LastMachinist.Inventory;
using LastMachinist.Measurement;
using LastMachinist.Machining.Core;
using LastMachinist.Machining.Turning;
using LastMachinist.Quest;

namespace LastMachinist.Integration
{
    public sealed class Quest01Integration
    {
        public const string StockItemId = "structural_carbon_steel";
        public QuestStateMachine Quest { get; }
        public InventoryState Inventory { get; }
        public RepeatableQuestService RepeatableQuests { get; private set; }
        public bool EarlyGameUnlocked => Quest.Data.State == QuestState.Completed;
        public WorkshopStory Story { get; private set; }
        public void RestoreStory(WorkshopStoryState data)
        {
            Story = new WorkshopStory(Inventory, () => EarlyGameUnlocked, data);
            Story.Changed += () => ProgressionChanged?.Invoke();
        }
        public WorkpieceInstance ActiveWorkpieceInstance => Inventory.FindWorkpiece(Quest.Data.WorkpieceInstanceId);
        public LastMachinist.Core.RegionProgression Regions { get; private set; }
        public void RestoreRegions(LastMachinist.Core.RegionProgressionState data)
        {
            Regions = new LastMachinist.Core.RegionProgression(UnityEngine.Resources.LoadAll<LastMachinist.Core.ExplorationRegionDefinition>("ExplorationRegions"), data);
        }
        public void RestoreRepeatableQuests(RepeatableQuestState data) => RepeatableQuests = new RepeatableQuestService(Inventory, data, () => EarlyGameUnlocked);
        // A later information event reveals the workshop; quest completion alone does not.
        public bool LearnAbandonedWorkshopLocation()
        {
            if (!EarlyGameUnlocked || Regions.IsVisible(LastMachinist.Core.RegionProgression.AbandonedWorkshopId)) return false;
            Regions.ApplyQuestCompletion(LastMachinist.Core.RegionProgression.PumpQuestId);
            bool revealed = Regions.IsVisible(LastMachinist.Core.RegionProgression.AbandonedWorkshopId);
            if (revealed) Story.Offer02();
            if (revealed) ProgressionChanged?.Invoke();
            return revealed;
        }
        public WorkpieceState CurrentWorkpiece { get; private set; }
        public MeasurementSession Measurement { get; private set; }
        public PartEvaluation LastEvaluation { get; private set; }
        public bool PumpRepaired => Quest.Data.PumpRepaired;
        private readonly Func<WorkpieceState> createStock;
        public event Action<WorkpieceState> MachiningStockSelected;
        public event Action ProgressionChanged;

        public Quest01Integration(InventoryState inventory, QuestRuntimeState quest = null, Func<WorkpieceState> createStock = null)
        {
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory)); Inventory.Validate();
            this.createStock = createStock ?? CreatePrototypeStock;
            InitialWorkshopWorkpieces.MigrateLegacyStock(Inventory, this.createStock);
            Quest = new QuestStateMachine(quest); Quest.Data.StockCount = Inventory.Workpieces.Count;
            RepeatableQuests = new RepeatableQuestService(Inventory, null, () => EarlyGameUnlocked);
            RestoreRegions(null);
            RestoreStory(null);
            Quest.StateChanged += (_, next) => {
                if (next == QuestState.Completed) RepeatableQuests.Unlock();
            };
        }

        // Default playtest stock; the Unity composition supplies its configured factory.
        private static WorkpieceState CreatePrototypeStock() => new WorkpieceState {
            InstanceId = Guid.NewGuid().ToString("N"), MaterialId = StockItemId, SourceItemId = StockItemId,
            Geometry = new WorkpieceGeometry { Type = GeometryType.RoundProfile,
                RoundProfile = new TurningProfile { TotalLength = 100, Segments = new List<TurningSegment> {
                    new TurningSegment { StartZ = 0, EndZ = 100, Radius = 25 } } } } };

        public bool RecoverBrokenShaft(WorkpieceState shaft)
        {
            if (shaft == null || Quest.Data.State != QuestState.InspectingPump) return false;
            AttachWorkpiece(shaft);
            return Quest.TryAdvance(QuestState.BrokenShaftFound);
        }
        public bool RecoverPrototypeBrokenShaft()
        {
            var shaft = new WorkpieceState { InstanceId = "broken_pump_shaft", MaterialId = "unknown", SourceItemId = "pump_shaft_broken",
                Geometry = new WorkpieceGeometry { Type = GeometryType.RoundProfile, RoundProfile = new TurningProfile { TotalLength = 100,
                    Segments = new List<TurningSegment> { new TurningSegment { StartZ = 0, EndZ = 30, Radius = 20 },
                        new TurningSegment { StartZ = 30, EndZ = 70, Radius = 25 }, new TurningSegment { StartZ = 70, EndZ = 97.5f, Radius = 18 },
                        new TurningSegment { StartZ = 97.5f, EndZ = 100, Radius = 0 } } } } };
            return RecoverBrokenShaft(shaft);
        }
        public bool GoToFarm() => Quest.TryAdvance(QuestState.GoingToFarm);
        public bool InspectPump() => Quest.TryAdvance(QuestState.InspectingPump);
        public bool ReturnToWorkshop() => Quest.TryAdvance(QuestState.ReturnedToWorkshop);
        public bool AttemptRuler() => Quest.TryAdvance(QuestState.RulerAttempt);
        public bool AcquireVernier() => Quest.ReceiveVernier();
        public bool BeginBrokenShaftInspection() => Quest.TryAdvance(QuestState.InspectingBrokenShaft);
        public bool InferShape() => Quest.InferShape();
        public bool RecordMeasurement(RecordedMeasurement record)
        {
            if (record == null || Measurement == null || !QuestAccessPolicy.CanOpenPage(Quest.Data.State, QuestPage.Measurement) ||
                !Quest.Data.HasReceivedVernier || record.WorkpieceInstanceId != CurrentWorkpiece.InstanceId ||
                !Measurement.Records.TryGetValue(record.MeasurementPointId, out var stored) || !ReferenceEquals(stored, record)) return false;
            // The reference memo belongs to the broken shaft. New stock readings
            // stay in their own session and must not overwrite those clues.
            if (Quest.Data.State == QuestState.InspectingBrokenShaft)
                Quest.Data.WorkMemo.RegisterMeasurement(record.MeasurementPointId, record.PlayerValue);
            return true;
        }
        public bool InferShapeFromMemo()
        {
            if (Quest.Data.State != QuestState.InspectingBrokenShaft || !Quest.Data.HasReceivedVernier) return false;
            if (!Quest.Data.WorkMemo.TryUnlockTarget()) return false;
            return Quest.InferShape();
        }

        public bool ObserveL3Wear()
        {
            if (Quest.Data.State != QuestState.InspectingBrokenShaft || CurrentWorkpiece == null) return false;
            Quest.Data.WorkMemo.L3WearObserved = true;
            return true;
        }

        public static PartRequirement PumpRequirement() => new PartRequirement {
            PerfectTolerance = .25f, FunctionalTolerance = 1, AssessCurrentFeatureLengths = true,
            Dimensions = new List<DimensionRequirement> {
                new DimensionRequirement("L1", 0, 30, 40), new DimensionRequirement("L2", 30, 70, 50), new DimensionRequirement("L3", 70, 100, 40) },
            RequiredChamferPositions = new List<float> { 0 } };

        private static WorkpieceDefinition Definition(WorkpieceInstance item) => item == null ? null :
            UnityEngine.Resources.LoadAll<WorkpieceDefinition>("WorkpieceDefinitions").FirstOrDefault(d => d.WorkpieceDefinitionId == item.WorkpieceDefinitionId);
        public bool CanSelectMaterial(string id, out string reason)
        {
            reason = "현재 이 공작물을 QUEST 01에 사용할 수 없습니다.";
            if (!Quest.Data.PumpShaftInferred || !Quest.Data.TurningSelected || Quest.Data.State < QuestState.ShapeInferred || Quest.Data.State > QuestState.FinalInspection) return false;
            var item = Inventory.FindWorkpiece(id); var definition = Definition(item);
            if (item == null || item.Availability != WorkpieceAvailability.Available || !InventoryMachiningSession.SupportsTurning(definition)) return false;
            if (RepeatableQuests.ForWorkpiece(id) != null) { reason = "다른 의뢰에 연결된 공작물입니다."; return false; }
            var profile = item.HasMachiningGeometry ? item.MachiningWorkpiece?.Geometry?.RoundProfile : InventoryWorkpieceGeometry.CreateInitialState(item, definition).Geometry.RoundProfile;
            if (profile == null || profile.GetAxialLength() < 100 || PartEvaluator.Evaluate(profile, PumpRequirement()).Result == PartEvaluationResult.Rejected)
            { reason = "현재 형상의 길이 또는 직경이 펌프 축을 만들기에 부족합니다."; return false; }
            reason = null; return true;
        }
        // Compatibility for code callers. Player UI always supplies the selected instance ID.
        public bool SelectMaterial() => SelectMaterial(Inventory.Workpieces.FirstOrDefault(w => CanSelectMaterial(w.InstanceId, out _))?.InstanceId);
        public bool SelectMaterial(string instanceId)
        {
            if (!CanSelectMaterial(instanceId, out _)) return false;
            var session = new InventoryMachiningSession(Inventory, id => UnityEngine.Resources.LoadAll<WorkpieceDefinition>("WorkpieceDefinitions").FirstOrDefault(d => d.WorkpieceDefinitionId == id));
            if (!session.TryBegin(instanceId, _ => true)) return false;
            if (!Quest.SelectInventoryStock()) { session.End(); return false; }
            Quest.Data.WorkpieceInstanceId = instanceId;
            AttachWorkpiece(session.Active.MachiningWorkpiece);
            Measurement.ImportRecords(session.Active.MachiningMeasurements);
            MachiningStockSelected?.Invoke(CurrentWorkpiece);
            return true;
        }
        public void AfterWorkpieceSplit(string sourceId, WorkpieceInstance a, WorkpieceInstance b)
        {
            if (Quest.Data.WorkpieceInstanceId != sourceId) return;
            var next = new[] { a, b }.Where(w => CanSelectMaterial(w.InstanceId, out _)).OrderBy(w => w.MachiningWorkpiece.Geometry.RoundProfile.GetAxialLength()).FirstOrDefault();
            Quest.Data.WorkpieceInstanceId = next?.InstanceId;
            LastEvaluation = null;
            if (next != null) AttachWorkpiece(next.MachiningWorkpiece);
            else { CurrentWorkpiece = null; Measurement = null; }
        }
        public bool BeginMachining() => Quest.TryAdvance(QuestState.Machining);
        public void AttachWorkpiece(WorkpieceState workpiece)
        {
            if (workpiece == null) throw new ArgumentNullException(nameof(workpiece));
            workpiece.Validate();
            if (ReferenceEquals(CurrentWorkpiece, workpiece) && Measurement != null) return;
            CurrentWorkpiece = workpiece;
            if (Quest.IsActive && (Quest.Data.State <= QuestState.InspectingBrokenShaft || Quest.Data.WorkMemo.CurrentInferences.Count == 0))
                Quest.Data.WorkMemo.BindInferenceSource(workpiece.InstanceId, workpiece.Geometry.RoundProfile);
            Measurement = new MeasurementSession(workpiece.Geometry.RoundProfile, workpiece.InstanceId);
            LastEvaluation = null;
        }
        public bool BeginFinalInspection(bool spindleStopped) => spindleStopped && Quest.TryAdvance(QuestState.FinalInspection);

        public PartEvaluation Evaluate(PartRequirement requirement)
        {
            if (Quest.Data.State != QuestState.FinalInspection || CurrentWorkpiece == null) throw new InvalidOperationException("Final inspection is not available.");
            LastEvaluation = PartEvaluator.Evaluate(CurrentWorkpiece.Geometry.RoundProfile, requirement);
            if (LastEvaluation.Result == PartEvaluationResult.Perfect || LastEvaluation.Result == PartEvaluationResult.Acceptable)
                Quest.TryAdvance(QuestState.ReadyToDeliver);
            return LastEvaluation;
        }
        public bool Rework() => LastEvaluation != null && LastEvaluation.CanRework && Quest.TryAdvance(QuestState.Machining);
        public bool RetryRejected()
        {
            if (Quest.Data.State != QuestState.FinalInspection || !Quest.Data.PumpShaftInferred ||
                LastEvaluation == null || LastEvaluation.Result != PartEvaluationResult.Rejected) return false;
            Inventory.ReleaseMachining();
            return SelectMaterial();
        }
        // Retained API: retries use owned stock only; no free replacement grant.
        public bool RestartMachiningPractice() => RetryRejected();
        public bool DeliverAndInstall() => Quest.Data.State == QuestState.ReadyToDeliver && Quest.TryAdvance(QuestState.Installing);
        public bool CompleteInstallation()
        {
            if (Quest.Data.State != QuestState.Installing || !Quest.TryAdvance(QuestState.Completed)) return false;
            if (ActiveWorkpieceInstance != null)
            {
                ActiveWorkpieceInstance.Availability = WorkpieceAvailability.Available;
                if (Inventory.ActiveMachiningInstanceId == ActiveWorkpieceInstance.InstanceId) Inventory.ActiveMachiningInstanceId = null;
                Inventory.RemoveWorkpiece(ActiveWorkpieceInstance.InstanceId);
            }
            return true;
        }
    }
}
