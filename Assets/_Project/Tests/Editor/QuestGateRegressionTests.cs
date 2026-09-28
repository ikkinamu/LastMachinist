using NUnit.Framework;
using LastMachinist.Quest;

namespace LastMachinist.Tests
{
    public sealed class QuestGateRegressionTests
    {
        [Test] public void SequentialTransitionCannotBypassRequiredActions()
        {
            var q = new QuestStateMachine(new QuestRuntimeState { State = QuestState.RulerAttempt });
            Assert.That(q.TryAdvance(QuestState.VernierReceived), Is.False);
            Assert.That(q.ReceiveVernier(), Is.True);
            Assert.That(q.TryAdvance(QuestState.InspectingBrokenShaft), Is.True);
            Assert.That(q.TryAdvance(QuestState.ShapeInferred), Is.False);
            Assert.That(q.InferShape(), Is.False);
            V03MemoTests.CompleteMemo(q.Data.WorkMemo);
            Assert.That(q.InferShape(), Is.True);
            Assert.That(q.TryAdvance(QuestState.MaterialSelected), Is.False);
        }

        [Test] public void LastStockCanBeConsumedAndFailedRetryDoesNotConsumeMore()
        {
            var q = new QuestStateMachine(new QuestRuntimeState {
                State = QuestState.FinalInspection, PumpShaftInferred = true, StockCount = 1 });
            Assert.That(q.RetryWithNewStock(), Is.True);
            Assert.That(q.Data.StockCount, Is.Zero);
            q.Data.StockCount = 2;
            Assert.That(q.RetryWithNewStock(), Is.False);
            Assert.That(q.Data.StockCount, Is.EqualTo(2));
        }
    }
}
