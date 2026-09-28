using NUnit.Framework;
using LastMachinist.Quest;

namespace LastMachinist.Tests
{
    public sealed class Phase6Tests
    {
        [Test] public void ShapeInferenceUsesGeometryStructureAndPreservesPlayerText()
        {
            var memo = new WorkMemoState();
            var profile = new LastMachinist.Machining.Turning.TurningProfile();
            profile.Segments.Add(new LastMachinist.Machining.Turning.TurningSegment { StartZ = 0, EndZ = 30, Radius = 20 });
            profile.Segments.Add(new LastMachinist.Machining.Turning.TurningSegment { StartZ = 30, EndZ = 60, Radius = 15 });
            memo.BindInferenceSource("part", profile);
            Assert.That(memo.CurrentInferences.Count, Is.EqualTo(2));
            Assert.That(memo.CurrentInferences[0].DiameterText, Is.Empty);
            memo.CurrentInferences[0].DiameterText = "wrong on purpose";
            memo.CurrentInferences[1].LengthText = "123";
            memo.BindInferenceSource("part", profile);
            Assert.That(memo.CurrentInferences[0].DiameterText, Is.EqualTo("wrong on purpose"));
            var restored = UnityEngine.JsonUtility.FromJson<WorkMemoState>(UnityEngine.JsonUtility.ToJson(memo));
            Assert.That(restored.CurrentInferences[1].LengthText, Is.EqualTo("123"));
            Assert.That(restored.Measurements.Count, Is.Zero);
            Assert.That(profile.Segments[0].Radius, Is.EqualTo(20));
            var other = new LastMachinist.Machining.Turning.TurningProfile();
            other.Segments.Add(new LastMachinist.Machining.Turning.TurningSegment());
            memo.BindInferenceSource("other", other);
            Assert.That(memo.CurrentInferences.Count, Is.EqualTo(1));
            memo.BindInferenceSource("part", profile);
            Assert.That(memo.CurrentInferences[0].DiameterText, Is.EqualTo("wrong on purpose"));
        }
        [Test] public void OfferedQuestRequiresAcceptanceAndSurvivesRestore()
        {
            var q = new QuestStateMachine();
            Assert.That(q.TryAdvance(QuestState.FarmerArrived), Is.False);
            Assert.That(q.Offer(), Is.True);
            foreach (QuestState next in System.Enum.GetValues(typeof(QuestState)))
                Assert.That(q.TryAdvance(next), Is.False, next.ToString());
            Assert.That(q.ReceiveVernier(), Is.False);
            Assert.That(q.ConsumeStockAndSelectMaterial(), Is.False);
            Assert.That(QuestFlowController.ObjectiveFor(q.Data.State), Is.Empty);
            var restored = UnityEngine.JsonUtility.FromJson<QuestRuntimeState>(UnityEngine.JsonUtility.ToJson(q.Data));
            var loaded = new QuestStateMachine(restored);
            Assert.That(loaded.Data.State, Is.EqualTo(QuestState.Offered));
            Assert.That(loaded.Accept(), Is.True);
            Assert.That(loaded.IsActive, Is.True);
            Assert.That(loaded.Accept(), Is.False);
            Assert.That(loaded.Offer(), Is.False);
            Assert.That(loaded.TryAdvance(QuestState.GoingToFarm), Is.True);
            var completed = new QuestStateMachine(new QuestRuntimeState { State = QuestState.Completed });
            Assert.That(completed.Offer(), Is.False);
            Assert.That(completed.Accept(), Is.False);
        }
        [Test] public void QuestFollowsExplicitSequenceAndRaisesEvent()
        {
            var q = new QuestStateMachine(); int changes = 0; q.StateChanged += (_, __) => changes++;
            Assert.That((q.Offer() && q.Accept()), Is.True);
            Assert.That(q.TryAdvance(QuestState.GoingToFarm), Is.True);
            Assert.That(q.TryAdvance(QuestState.InspectingPump), Is.True);
            Assert.That(changes, Is.EqualTo(4));
        }

        [Test] public void VernierAndInferenceAreGated()
        {
            var q = new QuestStateMachine();
            q.Offer(); q.Accept(); q.TryAdvance(QuestState.GoingToFarm); q.TryAdvance(QuestState.InspectingPump);
            q.TryAdvance(QuestState.BrokenShaftFound); q.TryAdvance(QuestState.ReturnedToWorkshop); q.TryAdvance(QuestState.RulerAttempt);
            Assert.That(q.InferShape(), Is.False); Assert.That(q.ReceiveVernier(), Is.True);
            Assert.That(q.TryAdvance(QuestState.InspectingBrokenShaft), Is.True);
            Assert.That(q.InferShape(), Is.False);
            V03MemoTests.CompleteMemo(q.Data.WorkMemo);
            Assert.That(q.InferShape(), Is.True);
        }

        [Test] public void MaterialSelectionConsumesStockAndCompletionRepairsPump()
        {
            var q = new QuestStateMachine(new QuestRuntimeState { StockCount = 3 });
            q.Offer(); q.Accept(); q.TryAdvance(QuestState.GoingToFarm); q.TryAdvance(QuestState.InspectingPump);
            q.TryAdvance(QuestState.BrokenShaftFound); q.TryAdvance(QuestState.ReturnedToWorkshop); q.TryAdvance(QuestState.RulerAttempt);
            q.ReceiveVernier(); q.TryAdvance(QuestState.InspectingBrokenShaft);
            V03MemoTests.CompleteMemo(q.Data.WorkMemo); q.InferShape();
            Assert.That(q.ConsumeStockAndSelectMaterial(), Is.False); Assert.That(q.SelectTurning(), Is.True); Assert.That(q.ConsumeStockAndSelectMaterial(), Is.True); Assert.That(q.Data.StockCount, Is.EqualTo(2));
            q.TryAdvance(QuestState.Machining); q.TryAdvance(QuestState.FinalInspection); q.TryAdvance(QuestState.ReadyToDeliver); q.TryAdvance(QuestState.Installing);
            Assert.That(q.TryAdvance(QuestState.Completed), Is.True); Assert.That(q.Data.PumpRepaired, Is.True);
        }

        [Test] public void DialogueCatalogIsDataDriven()
        {
            var c = new DialogueCatalog(); c.Add(new DialogueNode { Id = "intro", SpeakerId = "park", Text = "hello" });
            Assert.That(c.Get("intro").SpeakerId, Is.EqualTo("park")); Assert.That(c.Get("missing"), Is.Null);
        }
        [Test] public void InstallationAndCompletionDialogueUseSavedAcknowledgements()
        {
            var catalog = new DialogueCatalog();
            catalog.Add(new DialogueNode { Id = "DAY1_PUMP_INSTALL", Text = "install" });
            catalog.Add(new DialogueNode { Id = "DAY1_PUMP_REPAIRED", Text = "repaired" });
            var quest = new QuestStateMachine(new QuestRuntimeState { State = QuestState.Installing });
            using (var flow = new QuestFlowController(quest, catalog))
            {
                Assert.That(flow.CurrentDialogue.Text, Is.EqualTo("install"));
                flow.AcknowledgeDialogue();
                Assert.That(flow.CurrentDialogue, Is.Null);
            }
            using (var restored = new QuestFlowController(quest, catalog))
            {
                Assert.That(restored.CurrentDialogue, Is.Null);
                quest.TryAdvance(QuestState.Completed);
                Assert.That(restored.CurrentDialogue.Text, Is.EqualTo("repaired"));
                restored.AcknowledgeDialogue();
                Assert.That(quest.Data.AcknowledgedDialogueIds.Count, Is.EqualTo(2));
            }
        }
        [TestCase(TutorialDetailLevel.Brief)] [TestCase(TutorialDetailLevel.Standard)] [TestCase(TutorialDetailLevel.Detailed)]
        public void IntroChoiceChangesOnlyTutorialDetail(TutorialDetailLevel level)
        {
            var catalog = new DialogueCatalog();
            catalog.Add(new DialogueNode { Id = "DAY1_SEONGCHEOL_INTRO", Choices = new System.Collections.Generic.List<DialogueChoice> {
                new DialogueChoice { Id = "choice", TutorialDetail = level } } });
            var quest = new QuestStateMachine();
            using (var flow = new QuestFlowController(quest, catalog))
            {
                Assert.That(flow.CompleteIntro("unknown"), Is.False);
                Assert.That(quest.Data.State, Is.EqualTo(QuestState.NotStarted));
                Assert.That(flow.CompleteIntro("choice"), Is.True);
                Assert.That(quest.Data.State, Is.EqualTo(QuestState.Offered));
                Assert.That(quest.Data.TutorialDetail, Is.EqualTo(level));
                Assert.That(quest.Data.StockCount, Is.EqualTo(3));
                Assert.That(quest.Data.PumpShaftInferred, Is.False);
                Assert.That(flow.CompleteIntro("choice"), Is.False);
            }
        }
    }
}
