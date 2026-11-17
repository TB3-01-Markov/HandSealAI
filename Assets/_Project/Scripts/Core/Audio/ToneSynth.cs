using System;
using System.Collections.Generic;

namespace HandSeals.Core
{
    /// <summary>One note of a sound effect. A frequency of zero is a pause.</summary>
    public readonly struct Tone
    {
        public Tone(double frequency, double seconds, float volume)
        {
            if (frequency < 0 || double.IsNaN(frequency))
            {
                throw new ArgumentOutOfRangeException(nameof(frequency));
            }

            if (seconds <= 0 || double.IsNaN(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            if (volume < 0f || volume > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(volume), "Volume must be between 0 and 1.");
            }

            Frequency = frequency;
            Seconds = seconds;
            Volume = volume;
        }

        public double Frequency { get; }

        public double Seconds { get; }

        public float Volume { get; }
    }

    /// <summary>
    /// Renders short sound effects from notes, so the game needs no audio files.
    /// Each note is a sine with a little second harmonic, a soft decay and a click-free start and end.
    /// </summary>
    public static class ToneSynth
    {
        private const double EdgeSeconds = 0.004;
        private const double Decay = 3.0;

        public static int SampleCount(IReadOnlyList<Tone> tones, int sampleRate)
        {
            Validate(tones, sampleRate);

            int total = 0;
            foreach (Tone tone in tones)
            {
                total += Samples(tone, sampleRate);
            }

            return total;
        }

        /// <summary>Mono samples in the range -1..1.</summary>
        public static float[] Render(IReadOnlyList<Tone> tones, int sampleRate)
        {
            var samples = new float[SampleCount(tones, sampleRate)];
            int offset = 0;

            foreach (Tone tone in tones)
            {
                int count = Samples(tone, sampleRate);
                int edge = Math.Max(1, Math.Min(count / 2, (int)(EdgeSeconds * sampleRate)));
                double phaseStep = 2.0 * Math.PI * tone.Frequency / sampleRate;

                for (int i = 0; i < count; i++)
                {
                    double envelope = Math.Exp(-Decay * i / count);

                    if (i < edge)
                    {
                        envelope *= i / (double)edge;
                    }

                    int fromEnd = count - 1 - i;
                    if (fromEnd < edge)
                    {
                        envelope *= fromEnd / (double)edge;
                    }

                    double phase = phaseStep * i;
                    double wave = 0.8 * Math.Sin(phase) + 0.2 * Math.Sin(2.0 * phase);
                    samples[offset + i] = (float)(wave * envelope * tone.Volume);
                }

                offset += count;
            }

            return samples;
        }

        private static int Samples(Tone tone, int sampleRate)
        {
            return Math.Max(1, (int)Math.Round(tone.Seconds * sampleRate));
        }

        private static void Validate(IReadOnlyList<Tone> tones, int sampleRate)
        {
            if (tones == null)
            {
                throw new ArgumentNullException(nameof(tones));
            }

            if (tones.Count == 0)
            {
                throw new ArgumentException("A sound needs at least one tone.", nameof(tones));
            }

            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            }
        }
    }
}
