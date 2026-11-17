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
        public void Normalize_RepairsDataReadFromDisk()
        {
            var progress = new PlayerProgress
            {
                BestTimes = new System.Collections.Generic.List<BestTime>
                {
                    null,
                    new BestTime { JutsuId = "", Seconds = 3 },
                    new BestTime { JutsuId = "ember", Seconds = double.NaN },
                    new BestTime { JutsuId = "ember", Seconds = 6 },
                    new BestTime { JutsuId = "ember", Seconds = 4 },
                    new BestTime { JutsuId = "tide", Seconds = -2 },
                    new BestTime { JutsuId = "tide", Seconds = 9 }
                },
                Settings = null
            };

            progress.Normalize();

            Assert.AreEqual(2, progress.BestTimes.Count);
            progress.TryGetBestTime("ember", out double ember);
            progress.TryGetBestTime("tide", out double tide);
            Assert.AreEqual(4.0, ember, 1e-9);
            Assert.AreEqual(9.0, tide, 1e-9);
            Assert.IsNotNull(progress.Settings);
            Assert.AreEqual(0.6, progress.Settings.ConfirmScore, 1e-6);
        }

        [Test]
        public void Normalize_HandlesAMissingList()
        {
            var progress = new PlayerProgress { BestTimes = null };

            progress.Normalize();

            Assert.IsNotNull(progress.BestTimes);
            Assert.IsTrue(progress.ReportTime("ember", 5));
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
