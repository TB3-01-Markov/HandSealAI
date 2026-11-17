using HandSeals.Core;
using UnityEngine;

namespace HandSeals.Game
{
    public enum FeedbackSound
    {
        Correct = 0,
        Wrong = 1,
        Complete = 2,
        Fail = 3,
        Tick = 4,
        Go = 5
    }

    /// <summary>
    /// Short sound effects, synthesised at start-up from notes (see <see cref="ToneSynth"/>),
    /// so the project ships no audio files.
    /// </summary>
    public sealed class FeedbackAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;

        // Note frequencies in Hz.
        private const double A3 = 220.0;
        private const double D4 = 293.66;
        private const double G4 = 392.0;
        private const double C5 = 523.25;
        private const double E5 = 659.25;
        private const double G5 = 783.99;
        private const double C6 = 1046.5;

        private AudioSource _source;
        private AudioClip[] _clips;

        public bool SoundOn { get; set; } = true;

        public void Play(FeedbackSound sound)
        {
            if (!SoundOn || _source == null || _clips == null)
            {
                return;
            }

            AudioClip clip = _clips[(int)sound];
            if (clip != null)
            {
                _source.PlayOneShot(clip, 1f);
            }
        }

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;

            _clips = new AudioClip[6];
            _clips[(int)FeedbackSound.Correct] = Build("Correct", new Tone(C6, 0.09, 0.45f), new Tone(G5 * 2, 0.07, 0.3f));
            _clips[(int)FeedbackSound.Wrong] = Build("Wrong", new Tone(196.0, 0.12, 0.5f), new Tone(155.56, 0.2, 0.5f));
            _clips[(int)FeedbackSound.Complete] = Build(
                "Complete",
                new Tone(C5, 0.09, 0.45f),
                new Tone(E5, 0.09, 0.45f),
                new Tone(G5, 0.09, 0.45f),
                new Tone(C6, 0.3, 0.5f));
            _clips[(int)FeedbackSound.Fail] = Build("Fail", new Tone(G4, 0.16, 0.45f), new Tone(D4, 0.16, 0.45f), new Tone(A3, 0.32, 0.45f));
            _clips[(int)FeedbackSound.Tick] = Build("Tick", new Tone(G5, 0.05, 0.3f));
            _clips[(int)FeedbackSound.Go] = Build("Go", new Tone(G5 * 2, 0.14, 0.4f));
        }

        private void OnDestroy()
        {
            if (_clips == null)
            {
                return;
            }

            foreach (AudioClip clip in _clips)
            {
                if (clip != null)
                {
                    Destroy(clip);
                }
            }
        }

        private static AudioClip Build(string clipName, params Tone[] tones)
        {
            float[] samples = ToneSynth.Render(tones, SampleRate);
            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
