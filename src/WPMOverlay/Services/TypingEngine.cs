using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WPMOverlay.Models;

namespace WPMOverlay.Services
{
    public class TypingEngine : IDisposable
    {
        private readonly record struct RawKeyEvent(DateTime Timestamp, int VkCode, bool IsBackspace);

        private readonly KeyboardHook _hook;
        private readonly DatabaseService _database;
        private readonly SettingsService _settingsService;

        private readonly object _lock = new();
        private readonly ConcurrentQueue<RawKeyEvent> _pendingKeys = new();
        private readonly SemaphoreSlim _wakeSignal = new(0, 1);
        private int _isIdleSleeping;

        private CancellationTokenSource? _loopCts;
        private Task? _loopTask;
        private bool _disposed;

        private readonly List<KeystrokeRecord> _rollingBuffer = new();

        // Smart Filtering & Anti-Spam State
        private int _lastVkCode = -1;
        private int _consecutiveSameKeyCount = 0;
        private DateTime _lastRawKeystrokeTime = DateTime.MinValue;
        private int _unbrokenCharsWithoutBoundary = 0;
        private readonly Queue<int> _recentKeyHistory = new(6);

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
        public double LastFinished5mWpm { get; private set; } = 0;
        public double LastFinishedAccuracy { get; private set; } = 100;

        // Current Real-Time Metrics
        public double Current15sWpm { get; private set; } = 0;
        public double Current30sWpm { get; private set; } = 0;
        public double Current60sWpm { get; private set; } = 0;
        public double Current5mWpm { get; private set; } = 0;
        public double CurrentInstantWpm { get; private set; } = 0;
        public double CurrentAccuracy { get; private set; } = 100;
        public bool IsTypingActive => Volatile.Read(ref _isSessionActive);

        // Overall Records
        public StatsSummary CachedSummary { get; private set; } = new();

        // Events for UI (always invoked outside _lock)
        public event Action<MetricsSnapshot>? MetricsUpdated;
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
        }

        public void Start()
        {
            if (_disposed) return;

            _hook.Start();

            if (_loopCts == null)
            {
                _loopCts = new CancellationTokenSource();
                _loopTask = Task.Run(() => RunCalcLoopAsync(_loopCts.Token));
            }
        }

        public void Stop()
        {
            _hook.Stop();

            if (_loopCts != null)
            {
                _loopCts.Cancel();
                WakeLoop();
                try
                {
                    _loopTask?.Wait(TimeSpan.FromSeconds(2));
                }
                catch (AggregateException)
                {
                }
                _loopCts.Dispose();
                _loopCts = null;
                _loopTask = null;
            }

            TypingSession? completedSession = null;
            lock (_lock)
            {
                DrainPendingKeysLocked(out _);
                if (_isSessionActive)
                {
                    completedSession = FinalizeSessionLocked();
                }
            }

            if (completedSession != null)
            {
                _database.SaveSession(completedSession);
                RefreshStatsSummary();
            }
        }

        public void RefreshStatsSummary()
        {
            var summary = _database.GetStatsSummary();
            MetricsSnapshot snapshot;
            lock (_lock)
            {
                CachedSummary = summary;
                if (LastFinishedAvgWpm == 0 && CachedSummary.LastSessionWpm > 0)
                {
                    LastFinishedAvgWpm = CachedSummary.LastSessionWpm;
                    LastFinished15sWpm = CachedSummary.LastSession15sWpm;
                    LastFinished30sWpm = CachedSummary.LastSession30sWpm;
                    LastFinished60sWpm = CachedSummary.LastSession60sWpm;
                    LastFinished5mWpm = CachedSummary.LastSessionWpm;
                }
                snapshot = BuildSnapshotLocked();
            }
            MetricsUpdated?.Invoke(snapshot);
        }

        public MetricsSnapshot GetCurrentSnapshot()
        {
            lock (_lock)
            {
                return BuildSnapshotLocked();
            }
        }

        private MetricsSnapshot BuildSnapshotLocked()
        {
            return new MetricsSnapshot(
                IsTypingActive: _isSessionActive,
                Current15sWpm: Current15sWpm,
                Current30sWpm: Current30sWpm,
                Current60sWpm: Current60sWpm,
                Current5mWpm: Current5mWpm,
                CurrentAccuracy: CurrentAccuracy,
                LastFinishedAvgWpm: LastFinishedAvgWpm,
                LastFinished15sWpm: LastFinished15sWpm,
                LastFinished30sWpm: LastFinished30sWpm,
                LastFinished60sWpm: LastFinished60sWpm,
                LastFinished5mWpm: LastFinished5mWpm,
                LastFinishedAccuracy: LastFinishedAccuracy,
                Summary: CachedSummary);
        }

        /// <summary>
        /// Smart Filtering: Detects and rejects accidental key mashing, key holding/autorepeat,
        /// mechanical chatter, and unnatural spam patterns so they do not inflate WPM.
        /// </summary>
        public bool IsValidTypingKeystroke(int vkCode, bool isBackspace, DateTime now)
        {
            // 1. Hardware bounce / Simultaneous multi-key mash:
            // Human fingers cannot deliberately press two keys within 30ms of each other.
            double deltaMs = (now - _lastRawKeystrokeTime).TotalMilliseconds;
            _lastRawKeystrokeTime = now;

            if (deltaMs >= 0 && deltaMs < 30)
            {
                return false; // Chatter / simultaneous mash
            }

            // 2. Backspace handling: allow Backspace but reset unbroken char streak
            if (isBackspace)
            {
                _consecutiveSameKeyCount = 0;
                _unbrokenCharsWithoutBoundary = 0;
                _lastVkCode = vkCode;
                return true;
            }

            // 3. Word boundary detection (Space=0x20, Enter=0x0D, Tab=0x09)
            if (vkCode == 0x20 || vkCode == 0x0D || vkCode == 0x09)
            {
                _consecutiveSameKeyCount = 0;
                _unbrokenCharsWithoutBoundary = 0;
                _lastVkCode = vkCode;
                return true;
            }

            // 4. Consecutive identical key auto-repeat / key holding suppression:
            // In genuine typing, no English/standard word contains > 2 identical consecutive characters.
            // 3 or more of the same key in a row ("aaaaa...", "11111...") is key holding / repeat.
            if (vkCode == _lastVkCode)
            {
                _consecutiveSameKeyCount++;
                if (_consecutiveSameKeyCount > 2)
                {
                    return false; // Suppress auto-repeat
                }
            }
            else
            {
                _consecutiveSameKeyCount = 1;
                _lastVkCode = vkCode;
            }

            // 5. Unbroken character sequence without a word boundary:
            // If someone mashes keys continuously for > 25 characters without space, enter, or backspace,
            // ignore the spam characters until a delimiter is entered.
            _unbrokenCharsWithoutBoundary++;
            if (_unbrokenCharsWithoutBoundary > 25)
            {
                return false; // Suppress unspaced key mash
            }

            // 6. Rapid alternation spam detection (e.g. mashing two keys back-and-forth like 'fjfjfjfjfj'):
            if (_recentKeyHistory.Count >= 6) _recentKeyHistory.Dequeue();
            _recentKeyHistory.Enqueue(vkCode);
            if (_recentKeyHistory.Count == 6)
            {
                var arr = _recentKeyHistory.ToArray();
                if (arr[0] == arr[2] && arr[2] == arr[4] &&
                    arr[1] == arr[3] && arr[3] == arr[5] &&
                    arr[0] != arr[1])
                {
                    return false; // Suppress oscillation spam
                }
            }

            return true;
        }

        /// <summary>
        /// Called directly from WH_KEYBOARD_LL HookCallback.
        /// Must remain lock-free, allocation-free, and O(1) (Trap 3).
        /// </summary>
        private void OnKeyPressed(int vkCode, bool isBackspace)
        {
            _pendingKeys.Enqueue(new RawKeyEvent(DateTime.UtcNow, vkCode, isBackspace));
            WakeLoop();
        }

        private void WakeLoop()
        {
            if (Interlocked.Exchange(ref _isIdleSleeping, 0) == 1)
            {
                _wakeSignal.Release();
            }
        }

        private async Task RunCalcLoopAsync(CancellationToken ct)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(150));

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    bool shouldSleep = ProcessTick();

                    if (shouldSleep)
                    {
                        Interlocked.Exchange(ref _isIdleSleeping, 1);
                        if (!_pendingKeys.IsEmpty)
                        {
                            if (Interlocked.Exchange(ref _isIdleSleeping, 0) == 1)
                            {
                                _wakeSignal.Release();
                            }
                            continue;
                        }

                        await _wakeSignal.WaitAsync(ct).ConfigureAwait(false);
                        continue;
                    }

                    if (!await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void DrainPendingKeysLocked(out bool startedTyping)
        {
            startedTyping = false;

            while (_pendingKeys.TryDequeue(out var raw))
            {
                if (!IsValidTypingKeystroke(raw.VkCode, raw.IsBackspace, raw.Timestamp))
                {
                    continue;
                }

                bool wasIdle = !_isSessionActive;

                if (!_isSessionActive)
                {
                    _isSessionActive = true;
                    _sessionStartTime = raw.Timestamp;
                    _sessionGrossKeys = 0;
                    _sessionBackspaces = 0;
                    _sessionPeak15s = 0;
                    _sessionPeak30s = 0;
                    _sessionPeak60s = 0;
                }

                _lastKeystrokeTime = raw.Timestamp;
                _sessionGrossKeys++;
                if (raw.IsBackspace)
                {
                    _sessionBackspaces++;
                }

                _rollingBuffer.Add(new KeystrokeRecord(raw.Timestamp, raw.IsBackspace, raw.VkCode));

                if (wasIdle)
                {
                    startedTyping = true;
                }
            }
        }

        private bool ProcessTick()
        {
            DateTime now = DateTime.UtcNow;
            bool startedTyping;
            bool wentIdle = false;
            TypingSession? completedSession = null;
            MetricsSnapshot snapshot;
            bool canSleep;

            lock (_lock)
            {
                DrainPendingKeysLocked(out startedTyping);

                // Prune events older than 310 seconds (5 min + 10s buffer)
                DateTime cutoff = now.AddSeconds(-310);
                _rollingBuffer.RemoveAll(k => k.Timestamp < cutoff);

                double idleThreshold = _settingsService.CurrentSettings.AutoHideDelaySeconds;
                if (idleThreshold < 1.0) idleThreshold = 1.0;

                // Check for idle transition
                if (_isSessionActive && (now - _lastKeystrokeTime).TotalSeconds > idleThreshold)
                {
                    completedSession = FinalizeSessionLocked();
                    wentIdle = true;
                }

                double penalty = _settingsService.CurrentSettings.ErrorPenaltyMultiplier;

                if (_isSessionActive)
                {
                    Current15sWpm = CalculateWindowWpm(now, 15, penalty);
                    Current30sWpm = CalculateWindowWpm(now, 30, penalty);
                    Current60sWpm = CalculateWindowWpm(now, 60, penalty);
                    Current5mWpm = CalculateSustained5mWpm(now, penalty);
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
                    // Recalculate recent sustained WPM in background
                    Current5mWpm = CalculateSustained5mWpm(now, penalty);
                }

                snapshot = BuildSnapshotLocked();
                canSleep = !_isSessionActive && _rollingBuffer.Count == 0 && _pendingKeys.IsEmpty;
            }

            // Perform all SQLite I/O and event dispatches strictly OUTSIDE _lock
            if (startedTyping)
            {
                TypingStarted?.Invoke();
            }

            if (completedSession != null)
            {
                _database.SaveSession(completedSession);
                var updatedSummary = _database.GetStatsSummary();
                lock (_lock)
                {
                    CachedSummary = updatedSummary;
                    snapshot = BuildSnapshotLocked();
                }
            }

            if (wentIdle)
            {
                TypingIdle?.Invoke();
            }

            MetricsUpdated?.Invoke(snapshot);
            return canSleep;
        }

        private double CalculateWindowWpm(DateTime now, double windowSeconds, double penalty)
        {
            DateTime windowStart = now.AddSeconds(-windowSeconds);
            var windowKeys = _rollingBuffer.Where(k => k.Timestamp >= windowStart).ToList();

            if (windowKeys.Count == 0) return 0;

            int chars = windowKeys.Count(k => !k.IsBackspace);
            int backspaces = windowKeys.Count(k => k.IsBackspace);

            // Net characters calculation
            double netChars = Math.Max(0, chars - (backspaces * (1.0 + penalty)));
            double words = netChars / 5.0;

            double elapsedSeconds = (now - windowKeys.First().Timestamp).TotalSeconds;
            if (elapsedSeconds < 1.0) elapsedSeconds = 1.0;
            if (elapsedSeconds > windowSeconds) elapsedSeconds = windowSeconds;

            double minutes = elapsedSeconds / 60.0;
            return minutes > 0 ? (words / minutes) : 0;
        }

        private double CalculateSustained5mWpm(DateTime now, double penalty)
        {
            DateTime windowStart = now.AddSeconds(-300); // 5-minute sustained window
            var windowKeys = _rollingBuffer.Where(k => k.Timestamp >= windowStart).OrderBy(k => k.Timestamp).ToList();

            if (windowKeys.Count < 5) return LastFinished5mWpm > 0 ? LastFinished5mWpm : 0;

            int chars = windowKeys.Count(k => !k.IsBackspace);
            int backspaces = windowKeys.Count(k => k.IsBackspace);

            double netChars = Math.Max(0, chars - (backspaces * (1.0 + penalty)));
            double netWords = netChars / 5.0;

            // Stitch active typing time across bursts:
            // Any consecutive keystrokes separated by <= 3.5 seconds count as continuous typing.
            // Gaps larger than 3.5 seconds represent pauses/reading and are NOT counted as active typing.
            double activeSeconds = 0;
            DateTime burstStart = windowKeys[0].Timestamp;
            DateTime burstLast = windowKeys[0].Timestamp;

            for (int i = 1; i < windowKeys.Count; i++)
            {
                var currentKeyTime = windowKeys[i].Timestamp;
                double gap = (currentKeyTime - burstLast).TotalSeconds;

                if (gap <= 3.5)
                {
                    burstLast = currentKeyTime;
                }
                else
                {
                    double burstDuration = Math.Max(1.0, (burstLast - burstStart).TotalSeconds);
                    activeSeconds += burstDuration;

                    burstStart = currentKeyTime;
                    burstLast = currentKeyTime;
                }
            }

            activeSeconds += Math.Max(1.0, (burstLast - burstStart).TotalSeconds);

            if (activeSeconds < 2.0) activeSeconds = 2.0;
            double activeMinutes = activeSeconds / 60.0;

            return activeMinutes > 0 ? (netWords / activeMinutes) : 0;
        }

        private TypingSession? FinalizeSessionLocked()
        {
            _isSessionActive = false;

            double duration = (_lastKeystrokeTime - _sessionStartTime).TotalSeconds;

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

                // Update Idle Retention metrics
                LastFinishedAvgWpm = avgWpm;
                LastFinished15sWpm = _sessionPeak15s;
                LastFinished30sWpm = _sessionPeak30s;
                LastFinished60sWpm = _sessionPeak60s;
                LastFinished5mWpm = Current5mWpm > 0 ? Current5mWpm : avgWpm;
                LastFinishedAccuracy = accuracy;

                return new TypingSession
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
            }

            return null;
        }

        public void ResetSession()
        {
            MetricsSnapshot snapshot;
            lock (_lock)
            {
                while (_pendingKeys.TryDequeue(out _)) { }
                _rollingBuffer.Clear();
                _isSessionActive = false;
                _sessionGrossKeys = 0;
                _sessionBackspaces = 0;
                Current15sWpm = 0;
                Current30sWpm = 0;
                Current60sWpm = 0;
                Current5mWpm = 0;
                CurrentInstantWpm = 0;

                _lastVkCode = -1;
                _consecutiveSameKeyCount = 0;
                _unbrokenCharsWithoutBoundary = 0;
                _recentKeyHistory.Clear();
                _lastRawKeystrokeTime = DateTime.MinValue;
                snapshot = BuildSnapshotLocked();
            }
            MetricsUpdated?.Invoke(snapshot);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _hook.KeyPressed -= OnKeyPressed;
            Stop();
            _wakeSignal.Dispose();
            _hook.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
