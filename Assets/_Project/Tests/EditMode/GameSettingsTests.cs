using NUnit.Framework;

namespace HandSeals.Core.Tests
{
    [TestFixture]
    public class GameSettingsTests
    {
        [Test]
        public void Defaults_AreSensible()
        {
            var settings = new GameSettings();

            Assert.AreEqual(GameSettings.CurrentVersion, settings.Version);
            Assert.AreEqual(0.6, settings.ConfirmScore, 1e-6);
            Assert.AreEqual(0.4, settings.HoldSeconds, 1e-6);
            Assert.IsTrue(settings.SoundOn);
            Assert.IsTrue(settings.UseGpu);
            Assert.IsFalse(settings.ShowModelInput);
        }

        [Test]
        public void Normalize_ResetsSettingsThatWereNeverSaved()
        {
            // What a serializer produces when the file has no settings and no constructor runs.
            var settings = new GameSettings
            {
                Version = 0, ConfirmScore = 0f, HoldSeconds = 0f, SoundOn = false, UseGpu = false, ShowModelInput = true
            };

            settings.Normalize();

            Assert.AreEqual(GameSettings.CurrentVersion, settings.Version);
            Assert.AreEqual(0.6, settings.ConfirmScore, 1e-6);
            Assert.AreEqual(0.4, settings.HoldSeconds, 1e-6);
            Assert.IsTrue(settings.SoundOn);
            Assert.IsTrue(settings.UseGpu);
            Assert.IsFalse(settings.ShowModelInput);
        }

        [Test]
        public void Normalize_KeepsSavedChoices()
        {
            var settings = new GameSettings { ConfirmScore = 0.75f, HoldSeconds = 0.7f, SoundOn = false, UseGpu = false };

            settings.Normalize();

            Assert.AreEqual(0.75, settings.ConfirmScore, 1e-6);
            Assert.AreEqual(0.7, settings.HoldSeconds, 1e-6);
            Assert.IsFalse(settings.SoundOn);
            Assert.IsFalse(settings.UseGpu);
        }

        // value, expected after normalizing
        [TestCase(0.1f, 0.3f)]
        [TestCase(2f, 0.9f)]
        [TestCase(0.62f, 0.6f)]
        [TestCase(0.63f, 0.65f)]
        [TestCase(float.NaN, 0.6f)]
        [TestCase(float.PositiveInfinity, 0.6f)]
        public void Normalize_SnapsAndClampsTheConfirmScore(float value, float expected)
        {
            var settings = new GameSettings { ConfirmScore = value };

            settings.Normalize();

            Assert.AreEqual(expected, settings.ConfirmScore, 1e-6);
        }

        [TestCase(0f, 0.2f)]
        [TestCase(5f, 1.0f)]
        [TestCase(0.44f, 0.4f)]
        [TestCase(-1f, 0.2f)]
        public void Normalize_SnapsAndClampsTheHoldTime(float value, float expected)
        {
            var settings = new GameSettings { HoldSeconds = value };

            settings.Normalize();

            Assert.AreEqual(expected, settings.HoldSeconds, 1e-6);
        }

        [Test]
        public void Steps_GiveExactValues_AndStopAtTheLimits()
        {
            var settings = new GameSettings();

            settings.StepConfirmScore(1);
            Assert.AreEqual(0.65f, settings.ConfirmScore);
            settings.StepConfirmScore(-3);
            Assert.AreEqual(0.5f, settings.ConfirmScore);
            settings.StepConfirmScore(100);
            Assert.AreEqual(0.9f, settings.ConfirmScore);
            settings.StepConfirmScore(-100);
            Assert.AreEqual(0.3f, settings.ConfirmScore);

            settings.StepHoldSeconds(2);
            Assert.AreEqual(0.6f, settings.HoldSeconds);
            settings.StepHoldSeconds(-10);
            Assert.AreEqual(0.2f, settings.HoldSeconds);
            settings.StepHoldSeconds(10);
            Assert.AreEqual(1.0f, settings.HoldSeconds);
        }

        [Test]
        public void ManySmallSteps_DoNotDrift()
        {
            var settings = new GameSettings();

            for (int i = 0; i < 50; i++)
            {
                settings.StepConfirmScore(1);
                settings.StepConfirmScore(-1);
            }

            Assert.AreEqual(0.6f, settings.ConfirmScore);
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var settings = new GameSettings();
            GameSettings copy = settings.Clone();

            copy.SoundOn = false;
            copy.StepHoldSeconds(1);

            Assert.IsTrue(settings.SoundOn);
            Assert.AreEqual(0.4f, settings.HoldSeconds);
        }
    }
}
