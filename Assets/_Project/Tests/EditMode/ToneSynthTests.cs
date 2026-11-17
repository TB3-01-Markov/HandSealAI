using System;
using NUnit.Framework;

namespace HandSeals.Core.Tests
{
    [TestFixture]
    public class ToneSynthTests
    {
        private const int Rate = 44100;

        [Test]
        public void Length_IsTheSumOfTheNotes()
        {
            var tones = new[] { new Tone(440, 0.1, 0.5f), new Tone(0, 0.05, 0f), new Tone(880, 0.2, 0.5f) };

            float[] samples = ToneSynth.Render(tones, Rate);

            Assert.AreEqual(4410 + 2205 + 8820, samples.Length);
            Assert.AreEqual(samples.Length, ToneSynth.SampleCount(tones, Rate));
        }

        [Test]
        public void Samples_StayWithinTheVolume()
        {
            var tones = new[] { new Tone(523.25, 0.3, 0.7f), new Tone(1046.5, 0.3, 1f) };

            float[] samples = ToneSynth.Render(tones, Rate);

            float peak = 0f;
            foreach (float sample in samples)
            {
                Assert.IsFalse(float.IsNaN(sample));
                peak = Math.Max(peak, Math.Abs(sample));
            }

            Assert.IsTrue(peak <= 1f);
            Assert.IsTrue(peak > 0.3f);
        }

        [Test]
        public void EveryNote_StartsAndEndsInSilence_SoThereAreNoClicks()
        {
            var tones = new[] { new Tone(440, 0.1, 1f), new Tone(660, 0.1, 1f) };

            float[] samples = ToneSynth.Render(tones, Rate);

            Assert.AreEqual(0f, samples[0], 1e-6f);
            Assert.AreEqual(0f, samples[4409], 1e-6f);
            Assert.AreEqual(0f, samples[4410], 1e-6f);
            Assert.AreEqual(0f, samples[samples.Length - 1], 1e-6f);
        }

        [Test]
        public void Pause_IsSilent()
        {
            float[] samples = ToneSynth.Render(new[] { new Tone(0, 0.05, 1f) }, Rate);

            foreach (float sample in samples)
            {
                Assert.AreEqual(0f, sample, 1e-6f);
            }
        }

        [Test]
        public void VeryShortNote_HasAtLeastOneSample()
        {
            float[] samples = ToneSynth.Render(new[] { new Tone(440, 1e-6, 1f) }, Rate);

            Assert.AreEqual(1, samples.Length);
        }

        [Test]
        public void Render_RejectsBadInput()
        {
            Assert.Throws<ArgumentNullException>(() => ToneSynth.Render(null, Rate));
            Assert.Throws<ArgumentException>(() => ToneSynth.Render(new Tone[0], Rate));
            Assert.Throws<ArgumentOutOfRangeException>(() => ToneSynth.Render(new[] { new Tone(440, 0.1, 1f) }, 0));
        }

        [Test]
        public void Tone_RejectsBadValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Tone(-1, 0.1, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Tone(440, 0, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Tone(440, 0.1, 1.5f));
        }
    }
}
