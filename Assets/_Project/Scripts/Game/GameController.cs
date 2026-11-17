using System;
using System.Collections.Generic;
using HandSeals.Core;
using UnityEngine;

namespace HandSeals.Game
{
    public enum GameMode
    {
        /// <summary>The main menu (and the settings screen); recognition is paused.</summary>
        Menu = 0,

        /// <summary>Practising single seals without a timer.</summary>
        Training = 1,

        /// <summary>Performing techniques against the clock.</summary>
        Challenge = 2
    }

    /// <summary>
    /// Owns the game flow: which mode is active, the training session, the technique session,
    /// the saved progress and settings. Confirmed seals from the recognition go to whichever mode is running.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        private const double ResultSeconds = 3.0;

        private CameraFeed _camera;
        private RecognitionController _recognition;
        private IProgressRepository _repository;
        private PlayerProgress _progress;
        private IReadOnlyList<Jutsu> _jutsu = Array.Empty<Jutsu>();
        private int _index;
        private double _resultTimeLeft;
        private bool _appPaused;

        public GameMode Mode { get; private set; } = GameMode.Menu;

        /// <summary>All techniques, in the order of the data file.</summary>
        public IReadOnlyList<Jutsu> Techniques => _jutsu;

        public GameSession Session { get; private set; }

        /// <summary>The running practice, or null outside the training mode.</summary>
        public TrainingSession Training { get; private set; }

        /// <summary>The current settings. Change them through <see cref="ApplySettings"/>, not directly.</summary>
        public GameSettings Settings => _progress.Settings;

        /// <summary>True when completing a technique moves on to the next one instead of repeating it.</summary>
        public bool PlayAll { get; private set; }

        /// <summary>Set when the techniques could not be loaded.</summary>
        public string Error { get; private set; }

        /// <summary>Completion time of the last finished jutsu, in seconds.</summary>
        public double LastTime { get; private set; }

        public bool LastWasRecord { get; private set; }

        /// <summary>Outcome of the most recent confirmed seal, for UI feedback.</summary>
        public MatchResult LastMatch { get; private set; } = MatchResult.Ignored;

        /// <summary>The seal that produced <see cref="LastMatch"/>.</summary>
        public SealType LastSeal { get; private set; } = SealType.None;

        /// <summary>Unscaled time of <see cref="LastMatch"/>.</summary>
        public double LastMatchTime { get; private set; } = double.NegativeInfinity;

        /// <summary>Raised after the settings changed and were saved.</summary>
        public event Action<GameSettings> SettingsChanged;

        /// <summary>Raised for every confirmed seal that counted as right or wrong, in training and in techniques.</summary>
        public event Action<SealType, MatchResult> Judged;

        /// <summary>Raised when a technique ends: true when completed, false when the time ran out.</summary>
        public event Action<bool> ChallengeEnded;

        public void Initialize(
            CameraFeed cameraFeed,
            RecognitionController recognition,
            Func<IJutsuCatalog> loadCatalog,
            IProgressRepository repository,
            PlayerProgress progress,
            double countdownSeconds)
        {
            _camera = cameraFeed;
            _recognition = recognition;
            _repository = repository;
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));

            try
            {
                _jutsu = loadCatalog().GetAll();
                if (_jutsu.Count == 0)
                {
                    Error = "The jutsu file is empty.";
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Error = exception.Message;
            }

            Session = new GameSession(new ScoreCalculator(), countdownSeconds);
            Session.StateChanged += HandleStateChanged;
            Session.SealApplied += HandleSealApplied;
            _recognition.SealConfirmed += HandleSealConfirmed;

            SetMode(GameMode.Menu);
        }

        public void OpenMenu()
        {
            Session.Abort();
            Training = null;
            SetMode(GameMode.Menu);
        }

        public void StartTraining()
        {
            Session.Abort();
            Training = new TrainingSession(SealSets.Zodiac);
            SetMode(GameMode.Training);
        }

        /// <summary>Starts a technique. With <paramref name="playAll"/> the techniques follow one another.</summary>
        public void StartChallenge(int index, bool playAll)
        {
            if (_jutsu.Count == 0)
            {
                return;
            }

            _index = Math.Min(Math.Max(index, 0), _jutsu.Count - 1);
            PlayAll = playAll;
            Training = null;
            Session.Abort();
            SetMode(GameMode.Challenge);
        }

        /// <summary>In training, replaces the current seal with another one.</summary>
        public void SkipTrainingSeal()
        {
            if (Mode == GameMode.Training && Training != null)
            {
                Training.Skip();
                _recognition.ResetTracking();
            }
        }

        /// <summary>Validates, stores and saves new settings, then tells everyone who applies them.</summary>
        public void ApplySettings(GameSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            GameSettings copy = settings.Clone();
            copy.Normalize();
            _progress.Settings = copy;
            _repository.Save(_progress);
            SettingsChanged?.Invoke(copy);
        }

        public bool TryGetBestTime(Jutsu jutsu, out double seconds)
        {
            seconds = 0;
            return jutsu != null && _progress.TryGetBestTime(jutsu.Id, out seconds);
        }

        private void SetMode(GameMode mode)
        {
            Mode = mode;
            LastMatch = MatchResult.Ignored;
            LastSeal = SealType.None;
            LastMatchTime = double.NegativeInfinity;

            // A seal still held from the previous screen must not count on the new one.
            _recognition.Paused = mode == GameMode.Menu;
            _recognition.ResetTracking();
        }

        private void Update()
        {
            if (Session == null || Mode == GameMode.Menu)
            {
                return;
            }

            bool ready = _camera.IsRunning && _recognition.Error == null && !_appPaused;

            if (Mode == GameMode.Training)
            {
                if (ready && Training != null)
                {
                    Training.Tick(Time.deltaTime);
                }

                return;
            }

            if (!ready)
            {
                Session.Pause();
                return;
            }

            switch (Session.State)
            {
                case SessionState.Idle:
                    Session.Start(_jutsu[_index]);
                    _recognition.ResetTracking();
                    break;

                case SessionState.Paused:
                    Session.Resume();
                    break;

                case SessionState.Completed:
                case SessionState.Failed:
                    _resultTimeLeft -= Time.unscaledDeltaTime;
                    if (_resultTimeLeft <= 0)
                    {
                        if (Session.State == SessionState.Completed && PlayAll)
                        {
                            _index = (_index + 1) % _jutsu.Count;
                        }

                        // The player is probably still holding the last seal; it must not count for the next jutsu.
                        _recognition.ResetTracking();
                        Session.Start(_jutsu[_index]);
                    }

                    break;

                default:
                    Session.Tick(Time.deltaTime);
                    break;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            _appPaused = paused;
            if (paused && Session != null)
            {
                Session.Pause();
            }
        }

        private void OnDestroy()
        {
            if (Session != null)
            {
                Session.StateChanged -= HandleStateChanged;
                Session.SealApplied -= HandleSealApplied;
            }

            if (_recognition != null)
            {
                _recognition.SealConfirmed -= HandleSealConfirmed;
            }
        }

        private void HandleSealConfirmed(SealType seal)
        {
            if (Mode == GameMode.Challenge)
            {
                Session.OnSealConfirmed(seal);
            }
            else if (Mode == GameMode.Training && Training != null)
            {
                MatchResult result = Training.OnSealConfirmed(seal);
                if (result != MatchResult.Ignored)
                {
                    RememberMatch(seal, result);
                }
            }
        }

        private void HandleSealApplied(SealType seal, MatchResult result)
        {
            RememberMatch(seal, result);
        }

        private void RememberMatch(SealType seal, MatchResult result)
        {
            LastSeal = seal;
            LastMatch = result;
            LastMatchTime = Time.unscaledTimeAsDouble;
            Judged?.Invoke(seal, result);
        }

        private void HandleStateChanged(SessionState state)
        {
            if (state == SessionState.Completed)
            {
                LastTime = Session.Elapsed;
                LastWasRecord = _progress.ReportTime(Session.Current.Id, LastTime);
                if (LastWasRecord)
                {
                    _repository.Save(_progress);
                }

                _resultTimeLeft = ResultSeconds;
                ChallengeEnded?.Invoke(true);
            }
            else if (state == SessionState.Failed)
            {
                _resultTimeLeft = ResultSeconds;
                ChallengeEnded?.Invoke(false);
            }
        }
    }
}
