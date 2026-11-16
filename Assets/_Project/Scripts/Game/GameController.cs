using System;
using System.Collections.Generic;
using HandSeals.Core;
using UnityEngine;

namespace HandSeals.Game
{
    /// <summary>
    /// Drives the game loop: starts a jutsu when the camera is ready, forwards confirmed seals to the session,
    /// shows the result for a moment and moves on to the next jutsu (or repeats a failed one).
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

        public GameSession Session { get; private set; }

        /// <summary>Set when the techniques could not be loaded.</summary>
        public string Error { get; private set; }

        /// <summary>Completion time of the last finished jutsu, in seconds.</summary>
        public double LastTime { get; private set; }

        public bool LastWasRecord { get; private set; }

        /// <summary>Outcome of the most recent confirmed seal, for UI feedback.</summary>
        public MatchResult LastMatch { get; private set; } = MatchResult.Ignored;

        /// <summary>Unscaled time of <see cref="LastMatch"/>.</summary>
        public double LastMatchTime { get; private set; } = double.NegativeInfinity;

        public void Initialize(
            CameraFeed cameraFeed,
            RecognitionController recognition,
            Func<IJutsuCatalog> loadCatalog,
            IProgressRepository repository,
            double countdownSeconds)
        {
            _camera = cameraFeed;
            _recognition = recognition;
            _repository = repository;
            _progress = repository.Load();

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
        }

        /// <summary>Best completion time for the current jutsu, if the player has one.</summary>
        public bool TryGetBestTime(out double seconds)
        {
            seconds = 0;
            return Session != null
                && Session.Current != null
                && _progress.TryGetBestTime(Session.Current.Id, out seconds);
        }

        private void Update()
        {
            if (Session == null || Error != null)
            {
                return;
            }

            bool ready = _camera.IsRunning && _recognition.Error == null && !_appPaused;
            if (!ready)
            {
                Session.Pause();
                return;
            }

            switch (Session.State)
            {
                case SessionState.Idle:
                    Session.Start(_jutsu[_index]);
                    break;

                case SessionState.Paused:
                    Session.Resume();
                    break;

                case SessionState.Completed:
                case SessionState.Failed:
                    _resultTimeLeft -= Time.unscaledDeltaTime;
                    if (_resultTimeLeft <= 0)
                    {
                        if (Session.State == SessionState.Completed)
                        {
                            _index = (_index + 1) % _jutsu.Count;
                        }

                        // The player is probably still holding the last seal; it must not count for the next jutsu.
                        _recognition.Stabilizer.Reset();
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
            Session.OnSealConfirmed(seal);
        }

        private void HandleSealApplied(SealType seal, MatchResult result)
        {
            LastMatch = result;
            LastMatchTime = Time.unscaledTimeAsDouble;
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
            }
            else if (state == SessionState.Failed)
            {
                _resultTimeLeft = ResultSeconds;
            }
        }
    }
}
