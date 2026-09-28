using NUnit.Framework;
using LastMachinist.Quest;

namespace LastMachinist.Tests
{
    public sealed class V03QuestFlowTests
    {
        [Test] public void StateEventUpdatesObjectiveAndDialogue()
        {
            var catalog = new DialogueCatalog();
            catalog.Add(new DialogueNode { Id = "DAY1_SEOYEON_ARRIVAL", SpeakerId = "lee", Text = "도와줘요" });
            var quest = new QuestStateMachine(); using (var flow = new QuestFlowController(quest, catalog))
            {
                Assert.That((quest.Offer() && quest.Accept()), Is.True);
                Assert.That(flow.CurrentObjective, Is.EqualTo("서연의 농장으로 이동하자."));
                Assert.That(flow.CurrentDialogue.SpeakerId, Is.EqualTo("lee"));
            }
        }
    }
}
