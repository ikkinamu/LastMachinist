using NUnit.Framework;
using LastMachinist.Quest;

namespace LastMachinist.Tests
{
    public sealed class V03AccessPolicyTests
    {
        [Test] public void MachiningAllowsMemoAndStoppedRemeasurement()
        {
            Assert.That(QuestAccessPolicy.CanOpenPage(QuestState.Machining, QuestPage.WorkMemo), Is.True);
            Assert.That(QuestAccessPolicy.CanUsePrecisionMeasurement(QuestState.Machining, true, true), Is.True);
            Assert.That(QuestAccessPolicy.CanUsePrecisionMeasurement(QuestState.Machining, false, true), Is.False);
            Assert.That(QuestAccessPolicy.CanUsePrecisionMeasurement(QuestState.Machining, true, false), Is.False);
            Assert.That(QuestAccessPolicy.CanStartMachining(QuestState.MaterialSelected, true, 0), Is.True);
        }
        [Test] public void MeasurementAndLathePagesAreStateGated()
        {
            Assert.That(QuestAccessPolicy.CanOpenPage(QuestState.ReturnedToWorkshop, QuestPage.Measurement), Is.False);
            Assert.That(QuestAccessPolicy.CanOpenPage(QuestState.InspectingBrokenShaft, QuestPage.Measurement), Is.True);
            Assert.That(QuestAccessPolicy.CanOpenPage(QuestState.InspectingBrokenShaft, QuestPage.Lathe), Is.False);
            Assert.That(QuestAccessPolicy.CanOpenPage(QuestState.Machining, QuestPage.Lathe), Is.True);
        }
        [Test] public void PrecisionMeasurementNeedsVernierAndStoppedSpindle()
        {
            Assert.That(QuestAccessPolicy.CanUsePrecisionMeasurement(QuestState.InspectingBrokenShaft, true, false), Is.False);
            Assert.That(QuestAccessPolicy.CanUsePrecisionMeasurement(QuestState.InspectingBrokenShaft, false, true), Is.False);
            Assert.That(QuestAccessPolicy.CanUsePrecisionMeasurement(QuestState.InspectingBrokenShaft, true, true), Is.True);
        }
    }
}
