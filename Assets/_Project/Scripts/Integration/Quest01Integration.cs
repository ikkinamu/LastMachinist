using System;
using System.Collections.Generic;
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
        public LastMachinist.Core.RegionProgression Regions { get; private set; }
        public void RestoreRegions(LastMachinist.Core.RegionProgressionState data)
        {
            Regions = new LastMachinist.Core.RegionProgression(UnityEngine.Resources.LoadAll<LastMachinist.Core.ExplorationRegionDefinition>("ExplorationRegions"), data);
            if (Quest.Data.State == QuestState.Completed) Regions.ApplyQuestCompletion(LastMachinist.Core.RegionProgression.PumpQuestId);
        }
        public void RestoreRepeatableQuests(RepeatableQuestState data) => RepeatableQuests = new RepeatableQuestService(Inventory, data);
        public WorkpieceState CurrentWorkpiece { get; private set; }
        public MeasurementSession Measurement { get; private set; }
        public PartEvaluation LastEvaluation { get; private set; }
        public bool PumpRepaired => Quest.Data.PumpRepaired;
        private readonly Func<WorkpieceState> createStock;
        public event Action<WorkpieceState> MachiningStockSelected;

        public Quest01Integration(InventoryState inventory, QuestRuntimeState quest = null, Func<WorkpieceState> createStock = null)
        {
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory)); Inventory.Validate();
            Quest = new QuestStateMachine(quest); Quest.Data.StockCount = Inventory.GetQuantity(StockItemId);
            this.createStock = createStock ?? CreatePrototypeStock;
            RepeatableQuests = new RepeatableQuestService(Inventory);
            RestoreRegions(null);
            Quest.StateChanged += (_, next) => {
                if (next == QuestState.Completed) Regions.ApplyQuestCompletion(LastMachinist.Core.RegionProgression.PumpQuestId);
            };
        }

        // Default playtest stock; the Unity composition supplies its configured factory.
        private static WorkpieceState CreatePrototypeStock() => new WorkpieceState {
            InstanceId = Guid.NewGuid().ToString("N"), MaterialId = StockItemId, SourceItemId = StockItemId,
            Geometry = new WorkpieceGeometry { Type = GeometryType.RoundProfile,
                RoundProfile = new TurningProfile { TotalLength = 100, Segments = new List<TurningSegment> {
                    new TurningSegment { StartZ = 0, EndZ = 100, Radius = 25 } } } } };

        private bool ConsumeStock(bool retry)
        {
            var stock = createStock();
            if (stock == null) throw new InvalidOperationException("Stock factory returned no workpiece.");
            stock.Validate();
            if (stock.InstanceId == CurrentWorkpiece?.InstanceId ||
                ReferenceEquals(stock.Geometry.RoundProfile, CurrentWorkpiece?.Geometry.RoundProfile))
                throw new InvalidOperationException("Replacement stock must have independent identity and geometry.");
            Quest.Data.StockCount = Inventory.GetQuantity(StockItemId);
            if (!Inventory.TryRemove(StockItemId, 1)) return false;
            bool selected = retry ? Quest.RetryWithNewStock() : Quest.ConsumeStockAndSelectMaterial();
            if (!selected) { Inventory.Add(StockItemId, 1); return false; }
            AttachWorkpiece(stock);
            MachiningStockSelected?.Invoke(stock);
            return true;
        }

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

        public bool SelectMaterial()
        {
            if (!Quest.Data.TurningSelected) return false;
            if (Quest.Data.State != QuestState.ShapeInferred || !Quest.Data.PumpShaftInferred || Inventory.GetQuantity(StockItemId) <= 0) return false;
            return ConsumeStock(false);
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
                LastEvaluation == null || LastEvaluation.Result != PartEvaluationResult.Rejected || Inventory.GetQuantity(StockItemId) <= 0) return false;
            return ConsumeStock(true);
        }
        // Explicit MVP practice recovery, not a normal resource-granting action.
        // Keeps the rejected object unchanged and starts with independent stock.
        public bool RestartMachiningPractice()
        {
            if (Quest.Data.State != QuestState.FinalInspection || !Quest.Data.PumpShaftInferred ||
                LastEvaluation == null || LastEvaluation.Result != PartEvaluationResult.Rejected ||
                Inventory.GetQuantity(StockItemId) != 0) return false;
            Inventory.Add(StockItemId, 3);
            try
            {
                if (RetryRejected()) return true;
            }
            catch
            {
                // Factory validation runs before consumption; undo the recovery grant.
                if (Inventory.GetQuantity(StockItemId) == 3) Inventory.TryRemove(StockItemId, 3);
                throw;
            }
            Inventory.TryRemove(StockItemId, 3);
            return false;
        }
        public bool DeliverAndInstall() => Quest.Data.State == QuestState.ReadyToDeliver && Quest.TryAdvance(QuestState.Installing);
        public bool CompleteInstallation() => Quest.Data.State == QuestState.Installing && Quest.TryAdvance(QuestState.Completed);
    }
}
