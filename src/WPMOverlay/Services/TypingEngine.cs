using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using WPMOverlay.Models;

namespace WPMOverlay.Services
{
    public class TypingEngine : IDisposable
    {
        private readonly KeyboardHook _hook;
        private readonly DatabaseService _database;
        private readonly SettingsService _settingsService;
        private readonly System.Timers.Timer _calcTimer;

        private readonly object _lock = new();
        private readonly List<KeystrokeRecord> _rollingBuffer = new();

        // Current Active Session State
        private bool _isSessionActive = false;
        private DateTime _sessionStartTime;
        private DateTime _lastKeystrokeTime;
        private int _sessionGrossKeys = 0;
        private int _sessionBackspaces = 0;
        private double _sessionPeak15s = 0;
        private double _sessionPeak30s = 0;
        private double _sessionPeak60s = 0;

        // Last Finished Session Metrics (for Idle Retention)
        public double LastFinishedAvgWpm { get; private set; } = 0;
        public double LastFinished15sWpm { get; private set; } = 0;
        public double LastFinished30sWpm { get; private set; } = 0;
        public double LastFinished60sWpm { get; private set; } = 0;
        public double LastFinishedAccuracy { get; private set; } = 100;

        // Current Real-Time Metrics
        public double Current15sWpm { get; private set; } = 0;
        public double Current30sWpm { get; private set; } = 0;
        public double Current60sWpm { get; private set; } = 0;
        public double CurrentInstantWpm { get; private set; } = 0;
        public double CurrentAccuracy { get; private set; } = 100;
        public bool IsTypingActive => _isSessionActive;

        // Overall Records
        public StatsSummary CachedSummary { get; private set; } = new();

        // Events for UI
        public event Action? MetricsUpdated;
        public event Action? TypingStarted;
        public event Action? TypingIdle;

        public TypingEngine(KeyboardHook hook, DatabaseService database, SettingsService settingsService)
        {
            _hook = hook;
            _database = database;
            _settingsService = settingsService;

            // Load initial records from SQLite
            RefreshStatsSummary();

            _hook.KeyPressed += OnKeyPressed;

            _calcTimer = new System.Timers.Timer(150); // Refresh ~6.6 times/sec
            _calcTimer.Elapsed += OnCalcTimerElapsed;
            _calcTimer.AutoReset = true;
        }

        public void Start()
        {
            _hook.Start();
            _calcTimer.Start();
        }

        public void Stop()
        {
            _calcTimer.Stop();
            _hook.Stop();

            lock (_lock)
            {
                if (_isSessionActive)
                {
                    EndSession();
                }
            }
        }

        public void RefreshStatsSummary()
        {
            CachedSummary = _database.GetStatsSummary();
            if (LastFinishedAvgWpm == 0 && CachedSummary.LastSessionWpm > 0)
            {
                LastFinishedAvgWpm = CachedSummary.LastSessionWpm;
                LastFinished15sWpm = CachedSummary.LastSession15sWpm;
                LastFinished30sWpm = CachedSummary.LastSession30sWpm;
                LastFinished60sWpm = CachedSummary.LastSession60sWpm;
            }
        }

        private void OnKeyPressed(int vkCode, bool isBackspace)
        {
            DateTime now = DateTime.UtcNow;

            lock (_lock)
            {
                bool wasIdle = !_isSessionActive;

                if (!_isSessionActive)
                {
                    _isSessionActive = true;
                    _sessionStartTime = now;
                    _sessionGrossKeys = 0;
                    _sessionBackspaces = 0;
                    _sessionPeak15s = 0;
                    _sessionPeak30s = 0;
                    _sessionPeak60s = 0;
                }

                _lastKeystrokeTime = now;
                _sessionGrossKeys++;
                if (isBackspace)
                {
                    _sessionBackspaces++;
                }

                _rollingBuffer.Add(new KeystrokeRecord(now, isBackspace, vkCode));

                if (wasIdle)
                {
                    TypingStarted?.Invoke();
                }
            }
        }

        private void OnCalcTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            DateTime now = DateTime.UtcNow;

            lock (_lock)
            {
                // Prune events older than 65 seconds
                DateTime cutoff = now.AddSeconds(-65);
                _rollingBuffer.RemoveAll(k => k.Timestamp < cutoff);

                double idleThreshold = _settingsService.CurrentSettings.AutoHideDelaySeconds;
                if (idleThreshold < 2.0) idleThreshold = 2.0;

                // Check for idle transition
                if (_isSessionActive && (now - _lastKeystrokeTime).TotalSeconds > idleThreshold)
                {
                    EndSession();
                    TypingIdle?.Invoke();
                }

                if (_isSessionActive)
                {
                    double penalty = _settingsService.CurrentSettings.ErrorPenaltyMultiplier;

                    Current15sWpm = CalculateWindowWpm(now, 15, penalty);
                    Current30sWpm = CalculateWindowWpm(now, 30, penalty);
                    Current60sWpm = CalculateWindowWpm(now, 60, penalty);
                    CurrentInstantWpm = CalculateWindowWpm(now, 5, penalty);

                    if (Current15sWpm > _sessionPeak15s) _sessionPeak15s = Current15sWpm;
                    if (Current30sWpm > _sessionPeak30s) _sessionPeak30s = Current30sWpm;
                    if (Current60sWpm > _sessionPeak60s) _sessionPeak60s = Current60sWpm;

                    // Accuracy %
                    if (_sessionGrossKeys > 0)
                    {
                        double correct = Math.Max(0, _sessionGrossKeys - (_sessionBackspaces * 2));
                        CurrentAccuracy = Math.Min(100.0, (correct / _sessionGrossKeys) * 100.0);
                    }
                    else
                    {
                        CurrentAccuracy = 100.0;
                    }
                }
                else
                {
                    // Idle mode
                    CurrentInstantWpm = 0;
                }
            }

            MetricsUpdated?.Invoke();
        }

        private double CalculateWindowWpm(DateTime now, double windowSeconds, double penalty)
        {
            DateTime windowStart = now.AddSeconds(-windowSeconds);
            var windowKeys = _rollingBuffer.Where(k => k.Timestamp >= windowStart).ToList();

            if (windowKeys.Count == 0) return 0;

            int chars = windowKeys.Count(k => !k.IsBackspace);
            int backspaces = windowKeys.Count(k => k.IsBackspace);

            // Net characters calculation:
            // Each backspace removes a mistyped character plus applies the penalty
            double netChars = Math.Max(0, chars - (backspaces * (1.0 + penalty)));

            // Standard typing rate: 5 keystrokes = 1 word
            double words = netChars / 5.0;

            // Determine actual time span to avoid over-estimating at the very beginning of typing
            double elapsedSeconds = (now - windowKeys.First().Timestamp).TotalSeconds;
            if (elapsedSeconds < 1.0) elapsedSeconds = 1.0;
            if (elapsedSeconds > windowSeconds) elapsedSeconds = windowSeconds;

            double minutes = elapsedSeconds / 60.0;
            return minutes > 0 ? (words / minutes) : 0;
        }

        private void EndSession()
        {
            _isSessionActive = false;

            double duration = (_lastKeystrokeTime - _sessionStartTime).TotalSeconds;

            // Only record sessions that have meaningful typing (at least 5 keystrokes and 1.5s)
            if (_sessionGrossKeys >= 5 && duration >= 1.5)
            {
                double penalty = _settingsService.CurrentSettings.ErrorPenaltyMultiplier;
                int normalChars = Math.Max(0, _sessionGrossKeys - _sessionBackspaces);
                double netChars = Math.Max(0, normalChars - (_sessionBackspaces * (1.0 + penalty)));
                double netWords = netChars / 5.0;
                double minutes = duration / 60.0;
                double avgWpm = minutes > 0 ? (netWords / minutes) : 0;

                double accuracy = 100.0;
                if (_sessionGrossKeys > 0)
                {
                    double correct = Math.Max(0, _sessionGrossKeys - (_sessionBackspaces * 2));
                    accuracy = Math.Min(100.0, (correct / _sessionGrossKeys) * 100.0);
                }

                var session = new TypingSession
                {
                    StartedAt = _sessionStartTime,
                    EndedAt = _lastKeystrokeTime,
                    DurationSeconds = duration,
                    GrossKeystrokes = _sessionGrossKeys,
                    Backspaces = _sessionBackspaces,
                    NetWords = netWords,
                    AvgWpm = avgWpm,
                    Peak15sWpm = _sessionPeak15s,
                    Peak30sWpm = _sessionPeak30s,
                    Peak60sWpm = _sessionPeak60s,
                    AccuracyPercent = accuracy
                };

                _database.SaveSession(session);

                // Update Idle Retention metrics
                LastFinishedAvgWpm = avgWpm;
                LastFinished15sWpm = _sessionPeak15s;
                LastFinished30sWpm = _sessionPeak30s;
                LastFinished60sWpm = _sessionPeak60s;
                LastFinishedAccuracy = accuracy;

                RefreshStatsSummary();
            }
        }

        public void ResetSession()
        {
            lock (_lock)
            {
                _rollingBuffer.Clear();
                _isSessionActive = false;
                _sessionGrossKeys = 0;
                _sessionBackspaces = 0;
                Current15sWpm = 0;
                Current30sWpm = 0;
                Current60sWpm = 0;
                CurrentInstantWpm = 0;
            }
            MetricsUpdated?.Invoke();
        }

        public void Dispose()
        {
            Stop();
            _calcTimer.Dispose();
            _hook.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
