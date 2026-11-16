using NUnit.Framework;

namespace HandSeals.Core.Tests
{
    [TestFixture]
    public class PlayerProgressTests
    {
        [Test]
        public void FirstTime_IsARecord()
        {
            var progress = new PlayerProgress();

            Assert.IsTrue(progress.ReportTime("ember", 6.5));
            Assert.IsTrue(progress.TryGetBestTime("ember", out double best));
            Assert.AreEqual(6.5, best, 1e-9);
        }

        [Test]
        public void FasterTime_ReplacesTheRecord()
        {
            var progress = new PlayerProgress();
            progress.ReportTime("ember", 6.5);

            Assert.IsTrue(progress.ReportTime("ember", 5.0));
            progress.TryGetBestTime("ember", out double best);
            Assert.AreEqual(5.0, best, 1e-9);
            Assert.AreEqual(1, progress.BestTimes.Count);
        }

        [Test]
        public void SlowerTime_KeepsTheRecord()
        {
            var progress = new PlayerProgress();
            progress.ReportTime("ember", 5.0);

            Assert.IsFalse(progress.ReportTime("ember", 7.0));
            progress.TryGetBestTime("ember", out double best);
            Assert.AreEqual(5.0, best, 1e-9);
        }

        [Test]
        public void UnknownJutsu_HasNoRecord()
        {
            var progress = new PlayerProgress();

            Assert.IsFalse(progress.TryGetBestTime("missing", out double best));
            Assert.AreEqual(0.0, best, 1e-9);
        }

        [Test]
        public void Records_AreKeptPerJutsu()
        {
            var progress = new PlayerProgress();
            progress.ReportTime("ember", 5.0);
            progress.ReportTime("tide", 9.0);

            progress.TryGetBestTime("tide", out double best);
            Assert.AreEqual(9.0, best, 1e-9);
            Assert.AreEqual(2, progress.BestTimes.Count);
        }
    }
}
