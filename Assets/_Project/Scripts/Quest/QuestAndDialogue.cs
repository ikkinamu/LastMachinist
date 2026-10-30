using System;
using System.Collections.Generic;

namespace LastMachinist.Quest
{
    public enum TutorialDetailLevel { Brief, Standard, Detailed }
    public enum QuestState
    {
        NotStarted, FarmerArrived, GoingToFarm, InspectingPump, BrokenShaftFound,
        ReturnedToWorkshop, RulerAttempt, VernierReceived, InspectingBrokenShaft,
        ShapeInferred, MaterialSelected, Machining, FinalInspection, ReadyToDeliver,
        Installing, Completed,
        Offered // Appended: preserve numeric values in existing saves.
    }

    [Serializable]
    public sealed class QuestRuntimeState
    {
        public string QuestId = "quest_pump_01";
        public LastMachinist.Core.PrologueStatus Prologue;
        public QuestState State = QuestState.NotStarted;
        public bool HasReceivedVernier;
        public bool PumpShaftInferred;
        public bool PumpRepaired;
        public bool TurningSelected;
        public int StockCount = 3;
        public string WorkpieceInstanceId;
        public WorkMemoState WorkMemo = new WorkMemoState();
        public List<string> AcknowledgedDialogueIds = new List<string>();
        public TutorialDetailLevel TutorialDetail = TutorialDetailLevel.Standard;
    }

    [Serializable]
    public sealed class WorkMemoState
    {
        public string InferenceSourceId;
        public List<PlayerShapeInference> Inferences = new List<PlayerShapeInference>();
        public void BindInferenceSource(string sourceId, LastMachinist.Machining.Turning.TurningProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(sourceId)) return;
            Inferences ??= new List<PlayerShapeInference>();
            InferenceSourceId = sourceId;
            // Store identifiers and player text only; never copy true dimensions into notes.
            for (int i = 0; i < profile.Segments.Count; i++) AddInference(sourceId, "segment:" + i, "구간 " + (i + 1));
            for (int i = 0; i < profile.Chamfers.Count; i++) AddInference(sourceId, "chamfer:" + i, "모따기 " + (i + 1));
            for (int i = 0; i < profile.LinearCuts.Count; i++) AddInference(sourceId, "edge:" + i, "경사 구간 " + (i + 1));
        }
        private void AddInference(string sourceId, string key, string label)
        {
            if (!Inferences.Exists(x => x.SourceId == sourceId && x.Key == key))
                Inferences.Add(new PlayerShapeInference { SourceId = sourceId, Key = key, Label = label });
        }
        public List<PlayerShapeInference> CurrentInferences => (Inferences ??= new List<PlayerShapeInference>()).FindAll(x => x.SourceId == InferenceSourceId);
        public bool L1Measured;
        public bool L2Measured;
        public bool L3WearObserved;
        public List<MemoMeasurement> Measurements = new List<MemoMeasurement>();
        public bool TargetShapeUnlocked;
        public string Hint = "측정한 내용을 작업 노트에 기록하세요.";
        public bool CanRevealTarget => TargetShapeUnlocked;
        public bool CanInfer => HasRecord("L1_Diameter") && HasRecord("L1_Length") &&
            HasRecord("L2_Diameter") && HasRecord("L2_Length") &&
            (HasRecord("L3_Diameter") || HasRecord("L3_Length")) && L3WearObserved;
        public bool HasRecord(string pointId) => Measurements.Exists(m => m.PointId == pointId);
        public string RecordedText(string pointId) => Measurements.Find(m => m.PointId == pointId)?.PlayerValue.ToString("0.##") ?? "미기록";
        public void RegisterMeasurement(string pointId, float playerValue = 0)
        {
            if (float.IsNaN(playerValue) || float.IsInfinity(playerValue)) throw new ArgumentException("Invalid memo value.");
            Measurements.RemoveAll(m => m.PointId == pointId);
            Measurements.Add(new MemoMeasurement { PointId = pointId, PlayerValue = playerValue });
            L1Measured = HasRecord("L1_Diameter") && HasRecord("L1_Length");
            L2Measured = HasRecord("L2_Diameter") && HasRecord("L2_Length");
        }
        public bool TryUnlockTarget()
        {
            if (!CanInfer) return false;
            TargetShapeUnlocked = true; Hint = "원래 형상: 좌우 대칭 구조"; return true;
        }
    }

    [Serializable]
    public sealed class PlayerShapeInference
    {
        public string SourceId, Key, Label;
        public string DiameterText = "", LengthText = "", Note = "";
    }

    [Serializable]
    public sealed class MemoMeasurement
    {
        public string PointId;
        public float PlayerValue;
    }

    public sealed class QuestStateMachine
    {
        private static readonly QuestState[] Sequence = {
            QuestState.NotStarted, QuestState.FarmerArrived, QuestState.GoingToFarm, QuestState.InspectingPump,
            QuestState.BrokenShaftFound, QuestState.ReturnedToWorkshop, QuestState.RulerAttempt,
            QuestState.VernierReceived, QuestState.InspectingBrokenShaft, QuestState.ShapeInferred,
            QuestState.MaterialSelected, QuestState.Machining, QuestState.FinalInspection,
            QuestState.ReadyToDeliver, QuestState.Installing, QuestState.Completed };
        public QuestRuntimeState Data { get; }
        public bool IsActive => Data.State >= QuestState.FarmerArrived && Data.State < QuestState.Completed;
        public bool Offer()
        {
            if (Data.State != QuestState.NotStarted) return false;
            Data.State = QuestState.Offered;
            StateChanged?.Invoke(QuestState.NotStarted, Data.State);
            return true;
        }
        public bool Accept()
        {
            if (Data.State != QuestState.Offered) return false;
            Data.State = QuestState.FarmerArrived;
            StateChanged?.Invoke(QuestState.Offered, Data.State);
            return true;
        }
        public event Action<QuestState, QuestState> StateChanged;
        public QuestStateMachine(QuestRuntimeState data = null)
        { Data = data ?? new QuestRuntimeState(); if (Data.QuestId != "quest_pump_01") throw new ArgumentException("Unsupported quest."); }

        public bool TryAdvance(QuestState next)
        {
            if (!CanAdvance(next)) return false;
            QuestState old = Data.State; Data.State = next;
            if (next == QuestState.Completed) Data.PumpRepaired = true;
            StateChanged?.Invoke(old, next); return true;
        }

        public bool CanAdvance(QuestState next)
        {
            if (!IsActive) return false;
            if (!Enum.IsDefined(typeof(QuestState), next) || Data.State == QuestState.Completed) return false;
            if (next == QuestState.VernierReceived) return Data.State == QuestState.RulerAttempt && Data.HasReceivedVernier;
            if (next == QuestState.InspectingBrokenShaft) return Data.State == QuestState.VernierReceived && Data.HasReceivedVernier;
            if (next == QuestState.ShapeInferred) return Data.State == QuestState.InspectingBrokenShaft && Data.HasReceivedVernier && Data.PumpShaftInferred;
            if (next == QuestState.MaterialSelected) return false; // Only a material-consuming action may enter this state.
            if (next == QuestState.Machining && Data.State == QuestState.MaterialSelected) return true;
            if (next == QuestState.Machining && Data.State == QuestState.FinalInspection) return true;
            if (next == QuestState.FinalInspection && Data.State == QuestState.Machining) return true;
            if (next == QuestState.ReadyToDeliver && Data.State == QuestState.FinalInspection) return true;
            if (next == QuestState.Installing && Data.State == QuestState.ReadyToDeliver) return true;
            if (next == QuestState.Completed && Data.State == QuestState.Installing) return true;
            int current = Array.IndexOf(Sequence, Data.State), target = Array.IndexOf(Sequence, next);
            return target == current + 1;
        }

        public bool ReceiveVernier() { if (Data.State != QuestState.RulerAttempt) return false; Data.HasReceivedVernier = true; return TryAdvance(QuestState.VernierReceived); }
        public bool InferShape() { if (Data.State != QuestState.InspectingBrokenShaft || !Data.HasReceivedVernier || !Data.WorkMemo.TryUnlockTarget()) return false; Data.PumpShaftInferred = true; return TryAdvance(QuestState.ShapeInferred); }
        public bool ConsumeStockAndSelectMaterial() => SelectStock(false);
        public bool SelectInventoryStock(bool retry = false)
        {
            if (!Data.PumpShaftInferred || (!retry && !Data.TurningSelected)) return false;
            if (Data.State != QuestState.ShapeInferred && Data.State != QuestState.MaterialSelected && Data.State != QuestState.Machining && Data.State != QuestState.FinalInspection) return false;
            var previous = Data.State; Data.State = QuestState.MaterialSelected;
            StateChanged?.Invoke(previous, Data.State); return true;
        }
        public bool SelectTurning()
        {
            if (Data.State != QuestState.ShapeInferred) return false;
            Data.TurningSelected = true;
            return true;
        }
        public bool RetryWithNewStock() => SelectStock(true);
        private bool SelectStock(bool retry)
        {
            if (!retry && !Data.TurningSelected) return false;
            if (Data.State != (retry ? QuestState.FinalInspection : QuestState.ShapeInferred) ||
                !Data.PumpShaftInferred || Data.StockCount <= 0) return false;
            var previous = Data.State;
            Data.StockCount--;
            Data.State = QuestState.MaterialSelected;
            StateChanged?.Invoke(previous, Data.State);
            return true;
        }
    }

    public enum QuestPage { Workshop, Farm, Lathe, Measurement, WorkMemo, Inventory, Result }
    public static class QuestAccessPolicy
    {
        public static bool CanOpenPage(QuestState state, QuestPage page)
        {
            if (page == QuestPage.Workshop || page == QuestPage.Farm || page == QuestPage.Inventory) return true;
            if (page == QuestPage.WorkMemo) return state >= QuestState.InspectingBrokenShaft && state <= QuestState.Completed;
            if (page == QuestPage.Measurement)
                return state == QuestState.InspectingBrokenShaft || state == QuestState.Machining || state == QuestState.FinalInspection;
            if (page == QuestPage.Lathe) return state == QuestState.MaterialSelected || state == QuestState.Machining;
            if (page == QuestPage.Result) return state == QuestState.FinalInspection || state == QuestState.ReadyToDeliver || state == QuestState.Completed;
            return false;
        }

        public static bool CanUsePrecisionMeasurement(QuestState state, bool spindleStopped, bool vernierAcquired)
            => vernierAcquired && spindleStopped && CanOpenPage(state, QuestPage.Measurement);

        public static bool CanStartMachining(QuestState state, bool inferredShape, int stockCount)
            => inferredShape && stockCount >= 0 && state == QuestState.MaterialSelected;
    }

    [Serializable]
    public sealed class DialogueChoice
    {
        public string Id;
        public string Text;
        public string NextNodeId;
        public TutorialDetailLevel TutorialDetail = TutorialDetailLevel.Standard;
    }

    [Serializable]
    public sealed class DialogueNode
    {
        public string Id;
        public string SpeakerId;
        public string Text;
        public string NextNodeId;
        public string EventId;
        public List<DialogueChoice> Choices = new List<DialogueChoice>();
    }

    public sealed class DialogueCatalog
    {
        private readonly Dictionary<string, DialogueNode> nodes = new Dictionary<string, DialogueNode>();
        public void Add(DialogueNode node) { if (node == null || string.IsNullOrWhiteSpace(node.Id)) throw new ArgumentException("Invalid dialogue node."); nodes[node.Id] = node; }
        public DialogueNode Get(string id) { return id != null && nodes.TryGetValue(id, out var node) ? node : null; }
    }

    public sealed class QuestFlowController : IDisposable
    {
        private readonly QuestStateMachine quest;
        private readonly DialogueCatalog dialogue;
        public string CurrentObjective { get; private set; }
        public DialogueNode CurrentDialogue { get; private set; }
        public DialogueNode IntroDialogue => dialogue.Get("DAY1_SEONGCHEOL_INTRO");
        public bool CompleteIntro(string choiceId)
        {
            if (quest.Data.State != QuestState.NotStarted) return false;
            var choice = IntroDialogue?.Choices?.Find(c => c.Id == choiceId);
            if (choice == null || !Enum.IsDefined(typeof(TutorialDetailLevel), choice.TutorialDetail)) return false;
            quest.Data.TutorialDetail = choice.TutorialDetail;
            if (!quest.Data.AcknowledgedDialogueIds.Contains(IntroDialogue.Id)) quest.Data.AcknowledgedDialogueIds.Add(IntroDialogue.Id);
            return quest.Offer();
        }
        public event Action<string> ObjectiveChanged;
        public event Action<DialogueNode> DialogueChanged;
        public QuestFlowController(QuestStateMachine quest, DialogueCatalog dialogue)
        {
            this.quest = quest ?? throw new ArgumentNullException(nameof(quest));
            this.dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
            if (quest.Data.AcknowledgedDialogueIds == null) quest.Data.AcknowledgedDialogueIds = new List<string>();
            quest.StateChanged += OnStateChanged; OnStateChanged(quest.Data.State, quest.Data.State);
        }
        private void OnStateChanged(QuestState oldState, QuestState state)
        {
            CurrentObjective = ObjectiveFor(state); ObjectiveChanged?.Invoke(CurrentObjective);
            string id = state == QuestState.Offered || state == QuestState.FarmerArrived ? "DAY1_SEOYEON_ARRIVAL" :
                state == QuestState.InspectingPump ? "DAY1_PUMP_DIAGNOSIS" :
                state == QuestState.RulerAttempt ? "DAY1_RULER_ATTEMPT" :
                state == QuestState.VernierReceived ? (quest.Data.TutorialDetail == TutorialDetailLevel.Brief ? "DAY1_VERNIER_TUTORIAL_BRIEF" :
                    quest.Data.TutorialDetail == TutorialDetailLevel.Detailed ? "DAY1_VERNIER_TUTORIAL_DETAILED" : "DAY1_VERNIER_TUTORIAL") :
                state == QuestState.Installing ? "DAY1_PUMP_INSTALL" :
                state == QuestState.Completed ? "DAY1_PUMP_REPAIRED" : null;
            CurrentDialogue = quest.Data.AcknowledgedDialogueIds.Contains(id) ? null : dialogue.Get(id);
            DialogueChanged?.Invoke(CurrentDialogue);
        }
        public void AcknowledgeDialogue()
        {
            if (CurrentDialogue == null) return;
            if (CurrentDialogue.Choices != null && CurrentDialogue.Choices.Count > 0) return;
            // DAY1 NextNodeId links separate quest checkpoints, not consecutive lines.
            // Quest events retain ownership of those transitions.
            if (!quest.Data.AcknowledgedDialogueIds.Contains(CurrentDialogue.Id)) quest.Data.AcknowledgedDialogueIds.Add(CurrentDialogue.Id);
            CurrentDialogue = null; DialogueChanged?.Invoke(null);
        }
        public void ChooseDialogue(string choiceId)
        {
            var choice = CurrentDialogue?.Choices?.Find(x => x.Id == choiceId);
            if (choice == null) return;
            var next = dialogue.Get(choice.NextNodeId);
            if (!quest.Data.AcknowledgedDialogueIds.Contains(CurrentDialogue.Id))
                quest.Data.AcknowledgedDialogueIds.Add(CurrentDialogue.Id);
            CurrentDialogue = next; DialogueChanged?.Invoke(next);
        }
        public static string ObjectiveFor(QuestState state)
        {
            switch (state)
            {
                case QuestState.FarmerArrived:
                case QuestState.GoingToFarm: return "서연의 농장으로 이동하자.";
                case QuestState.InspectingPump: return "고장 난 펌프를 확인하자.";
                case QuestState.BrokenShaftFound: return "고장 난 축을 작업장으로 가져가자.";
                case QuestState.ReturnedToWorkshop: return "고장 난 축을 살펴보자.";
                case QuestState.RulerAttempt: return "고장 난 축의 치수를 확인할 방법을 찾아보자.";
                case QuestState.VernierReceived: return "버니어 캘리퍼스로 고장 난 축을 측정하자.";
                case QuestState.InspectingBrokenShaft: return "고장 난 축의 치수와 마모 상태를 확인하자.";
                case QuestState.ShapeInferred: return "가공 방식을 선택한 뒤 새 펌프 축의 재료를 준비하자.";
                case QuestState.MaterialSelected: return "소재를 선반에 장착하자.";
                case QuestState.Machining: return "새 펌프 축을 가공하자.";
                case QuestState.FinalInspection: return "완성한 펌프 축을 검사하자.";
                case QuestState.ReadyToDeliver: return "완성한 펌프 축을 서연의 농장으로 가져가자.";
                case QuestState.Installing: return "펌프 수리가 끝나는 것을 확인하자.";
                default: return string.Empty;
            }
        }
        public void Dispose() { quest.StateChanged -= OnStateChanged; }
    }
}
