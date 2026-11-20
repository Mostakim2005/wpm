using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WPMOverlay.Models;

namespace WPMOverlay.Services
{
    public class TypingEngine : IDisposable
    {
        private const double SessionIdleTimeoutSeconds = 5.0;

        private readonly record struct RawKeyEvent(long TimestampTicks, DateTime UtcTimestamp, int VkCode, bool IsBackspace);

        private readonly KeyboardHook _hook;
        private readonly DatabaseService _database;
        private readonly SettingsService _settingsService;
        private readonly TimeProvider _timeProvider;

        private readonly object _lock = new();
        private readonly ConcurrentQueue<RawKeyEvent> _pendingKeys = new();
        private readonly SemaphoreSlim _wakeSignal = new(0, 1);
        private int _isIdleSleeping;

        private CancellationTokenSource? _loopCts;
        private Task? _loopTask;
        private bool _disposed;

        private readonly List<KeystrokeRecord> _rollingBuffer = new();

        // Smart Filtering & Anti-Spam State (zero-allocation fixed ring buffer)
        private int _lastVkCode = -1;
        private int _consecutiveSameKeyCount = 0;
        private long _lastRawKeystrokeTicks = 0;
        private bool _hasLastRawKeystroke = false;
        private DateTime _lastRawKeystrokeUtc = DateTime.MinValue;
        private int _unbrokenCharsWithoutBoundary = 0;
        private readonly int[] _recentKeyHistory = new int[6];
        private int _recentKeyCount = 0;
        private int _recentKeyHead = 0;

        // Current Active Session State
        private bool _isSessionActive = false;
        private long _sessionStartTicks;
        private long _lastKeystrokeTicks;
        private DateTime _sessionStartUtc;
        private DateTime _lastKeystrokeUtc;
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
        public double CurrentAccuracy { get; private set; } = 100;
        public bool IsTypingActive => Volatile.Read(ref _isSessionActive);

        // Overall Records
        public StatsSummary CachedSummary { get; private set; } = new();

        // Events for UI (always invoked outside _lock)
        public event Action<MetricsSnapshot>? MetricsUpdated;
        public event Action? TypingStarted;
        public event Action? TypingIdle;

        public TypingEngine(
            KeyboardHook hook,
            DatabaseService database,
            SettingsService settingsService,
            TimeProvider? timeProvider = null)
        {
            _hook = hook;
            _database = database;
            _settingsService = settingsService;
            _timeProvider = timeProvider ?? TimeProvider.System;

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

        public void RefreshStatsSummary(bool forceSyncLastSession = true)
        {
            var summary = _database.GetStatsSummary();
            MetricsSnapshot snapshot;
            lock (_lock)
            {
                CachedSummary = summary;

                if (!_isSessionActive && (forceSyncLastSession || LastFinishedAvgWpm == 0))
                {
                    if (CachedSummary.TotalSessions == 0)
                    {
                        LastFinishedAvgWpm = 0;
                        LastFinished15sWpm = 0;
                        LastFinished30sWpm = 0;
                        LastFinished60sWpm = 0;
                        LastFinished5mWpm = 0;
                        LastFinishedAccuracy = 100.0;
                    }
                    else
                    {
                        LastFinishedAvgWpm = CachedSummary.LastSessionWpm;
                        LastFinished15sWpm = CachedSummary.LastSession15sWpm;
                        LastFinished30sWpm = CachedSummary.LastSession30sWpm;
                        LastFinished60sWpm = CachedSummary.LastSession60sWpm;
                        LastFinished5mWpm = CachedSummary.LastSessionWpm;
                    }
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
        /// Smart Filtering (DateTime overload retained for unit tests and direct callers).
        /// </summary>
        public bool IsValidTypingKeystroke(int vkCode, bool isBackspace, DateTime now)
        {
            double deltaMs = _lastRawKeystrokeUtc == DateTime.MinValue
                ? double.PositiveInfinity
                : (now - _lastRawKeystrokeUtc).TotalMilliseconds;
            _lastRawKeystrokeUtc = now;

            return IsValidTypingKeystrokeCore(vkCode, isBackspace, deltaMs);
        }

        /// <summary>
        /// Smart Filtering using monotonic TimeProvider timestamps.
        /// </summary>
        public bool IsValidTypingKeystroke(int vkCode, bool isBackspace, long timestampTicks)
        {
            double deltaMs;
            if (!_hasLastRawKeystroke)
            {
                deltaMs = double.PositiveInfinity;
                _hasLastRawKeystroke = true;
            }
            else
            {
                deltaMs = _timeProvider.GetElapsedTime(_lastRawKeystrokeTicks, timestampTicks).TotalMilliseconds;
            }

            _lastRawKeystrokeTicks = timestampTicks;
            return IsValidTypingKeystrokeCore(vkCode, isBackspace, deltaMs);
        }

        private bool IsValidTypingKeystrokeCore(int vkCode, bool isBackspace, double deltaMs)
        {
            // 1. Hardware bounce / Simultaneous multi-key mash:
            // Human fingers cannot deliberately press two keys within 30ms of each other.
            if (deltaMs < 30.0)
            {
                return false; // Chatter / simultaneous mash (or backward clock anomaly)
            }

            // 2. Backspace handling: allow Backspace and reset streak & oscillation history
            if (isBackspace)
            {
                _consecutiveSameKeyCount = 0;
                _unbrokenCharsWithoutBoundary = 0;
                ClearRecentKeyHistory();
                _lastVkCode = vkCode;
                return true;
            }

            // 3. Word boundary detection (Space=0x20, Enter=0x0D, Tab=0x09)
            if (vkCode == 0x20 || vkCode == 0x0D || vkCode == 0x09)
            {
                _consecutiveSameKeyCount = 0;
                _unbrokenCharsWithoutBoundary = 0;
                ClearRecentKeyHistory();
                _lastVkCode = vkCode;
                return true;
            }

            // 4. Consecutive identical key auto-repeat / key holding suppression:
            // In genuine typing, no English/standard word contains > 2 identical consecutive characters.
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

            // 5. Unbroken character sequence without a word boundary (> 25 chars)
            _unbrokenCharsWithoutBoundary++;
            if (_unbrokenCharsWithoutBoundary > 25)
            {
                return false; // Suppress unspaced key mash
            }

            // 6. Rapid alternation spam detection (zero-allocation 6-slot ring buffer)
            PushRecentKey(vkCode);
            if (_recentKeyCount == 6)
            {
                int k0 = GetRecentKey(0);
                int k1 = GetRecentKey(1);
                int k2 = GetRecentKey(2);
                int k3 = GetRecentKey(3);
                int k4 = GetRecentKey(4);
                int k5 = GetRecentKey(5);

                if (k0 == k2 && k2 == k4 &&
                    k1 == k3 && k3 == k5 &&
                    k0 != k1)
                {
                    return false; // Suppress oscillation spam
                }
            }

            return true;
        }

        private void ClearRecentKeyHistory()
        {
            _recentKeyCount = 0;
            _recentKeyHead = 0;
        }

        private void PushRecentKey(int vkCode)
        {
            if (_recentKeyCount < 6)
            {
                int index = (_recentKeyHead + _recentKeyCount) % 6;
                _recentKeyHistory[index] = vkCode;
                _recentKeyCount++;
            }
            else
            {
                _recentKeyHistory[_recentKeyHead] = vkCode;
                _recentKeyHead = (_recentKeyHead + 1) % 6;
            }
        }

        private int GetRecentKey(int offset)
        {
            return _recentKeyHistory[(_recentKeyHead + offset) % 6];
        }

        /// <summary>
        /// Enqueues a keystroke using the configured TimeProvider (used by KeyboardHook and tests).
        /// Lock-free and O(1) (Trap 3).
        /// </summary>
        public void ProcessKeystroke(int vkCode, bool isBackspace)
        {
            _pendingKeys.Enqueue(new RawKeyEvent(
                _timeProvider.GetTimestamp(),
                _timeProvider.GetUtcNow().UtcDateTime,
                vkCode,
                isBackspace));
            WakeLoop();
        }

        /// <summary>
        /// Runs one calculation tick synchronously (useful for deterministic unit tests).
        /// </summary>
        public void FlushAndRecalculate()
        {
            ProcessTick();
        }

        private void OnKeyPressed(int vkCode, bool isBackspace)
        {
            ProcessKeystroke(vkCode, isBackspace);
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
                if (!IsValidTypingKeystroke(raw.VkCode, raw.IsBackspace, raw.TimestampTicks))
                {
                    continue;
                }

                bool wasIdle = !_isSessionActive;

                if (!_isSessionActive)
                {
                    _isSessionActive = true;
                    _sessionStartTicks = raw.TimestampTicks;
                    _sessionStartUtc = raw.UtcTimestamp;
                    _sessionGrossKeys = 0;
                    _sessionBackspaces = 0;
                    _sessionPeak15s = 0;
                    _sessionPeak30s = 0;
                    _sessionPeak60s = 0;
                }

                _lastKeystrokeTicks = raw.TimestampTicks;
                _lastKeystrokeUtc = raw.UtcTimestamp;
                _sessionGrossKeys++;
                if (raw.IsBackspace)
                {
                    _sessionBackspaces++;
                }

                // Store monotonic timestamp & backspace flag only (no VirtualKey retained in RAM)
                _rollingBuffer.Add(new KeystrokeRecord(raw.TimestampTicks, raw.IsBackspace));

                if (wasIdle)
                {
                    startedTyping = true;
                }
            }
        }

        private bool ProcessTick()
        {
            long nowTicks = _timeProvider.GetTimestamp();
            bool startedTyping;
            bool wentIdle = false;
            TypingSession? completedSession = null;
            MetricsSnapshot snapshot;
            bool canSleep;

            lock (_lock)
            {
                DrainPendingKeysLocked(out startedTyping);

                // Prune events older than 310 seconds (5 min + 10s buffer) in-place with zero allocations
                PruneRollingBufferLocked(nowTicks, 310.0);

                // Check for session idle transition (decoupled from UI AutoHideDelaySeconds)
                if (_isSessionActive && _timeProvider.GetElapsedTime(_lastKeystrokeTicks, nowTicks).TotalSeconds > SessionIdleTimeoutSeconds)
                {
                    completedSession = FinalizeSessionLocked();
                    wentIdle = true;
                }

                double penalty = _settingsService.CurrentSettings.ErrorPenaltyMultiplier;

                if (_isSessionActive)
                {
                    Current15sWpm = CalculateWindowWpmLocked(nowTicks, 15.0, penalty);
                    Current30sWpm = CalculateWindowWpmLocked(nowTicks, 30.0, penalty);
                    Current60sWpm = CalculateWindowWpmLocked(nowTicks, 60.0, penalty);
                    Current5mWpm = CalculateSustained5mWpmLocked(nowTicks, penalty);

                    // Require minimum session duration before recording longer-window peaks
                    // so a 2-second burst cannot inflate Peak30sWpm or Peak60sWpm.
                    double sessionElapsed = _timeProvider.GetElapsedTime(_sessionStartTicks, nowTicks).TotalSeconds;
                    if (sessionElapsed >= 5.0 && Current15sWpm > _sessionPeak15s) _sessionPeak15s = Current15sWpm;
                    if (sessionElapsed >= 15.0 && Current30sWpm > _sessionPeak30s) _sessionPeak30s = Current30sWpm;
                    if (sessionElapsed >= 30.0 && Current60sWpm > _sessionPeak60s) _sessionPeak60s = Current60sWpm;

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
                    // Idle mode: recalculate recent sustained WPM in background
                    Current5mWpm = CalculateSustained5mWpmLocked(nowTicks, penalty);
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

        private void PruneRollingBufferLocked(long nowTicks, double maxAgeSeconds)
        {
            int removeCount = 0;
            while (removeCount < _rollingBuffer.Count &&
                   _timeProvider.GetElapsedTime(_rollingBuffer[removeCount].TimestampTicks, nowTicks).TotalSeconds > maxAgeSeconds)
            {
                removeCount++;
            }

            if (removeCount > 0)
            {
                _rollingBuffer.RemoveRange(0, removeCount);
            }
        }

        private int FindWindowStartIndexLocked(long nowTicks, double windowSeconds)
        {
            int i = _rollingBuffer.Count - 1;
            while (i >= 0 && _timeProvider.GetElapsedTime(_rollingBuffer[i].TimestampTicks, nowTicks).TotalSeconds <= windowSeconds)
            {
                i--;
            }
            return i + 1;
        }

        private double CalculateWindowWpmLocked(long nowTicks, double windowSeconds, double penalty)
        {
            int startIndex = FindWindowStartIndexLocked(nowTicks, windowSeconds);
            if (startIndex >= _rollingBuffer.Count) return 0;

            int chars = 0;
            int backspaces = 0;
            for (int i = startIndex; i < _rollingBuffer.Count; i++)
            {
                if (_rollingBuffer[i].IsBackspace) backspaces++;
                else chars++;
            }

            double netChars = Math.Max(0, chars - (backspaces * (1.0 + penalty)));
            double words = netChars / 5.0;

            double elapsedSeconds = _timeProvider.GetElapsedTime(_rollingBuffer[startIndex].TimestampTicks, nowTicks).TotalSeconds;
            if (elapsedSeconds < 1.0) elapsedSeconds = 1.0;
            if (elapsedSeconds > windowSeconds) elapsedSeconds = windowSeconds;

            double minutes = elapsedSeconds / 60.0;
            return minutes > 0 ? (words / minutes) : 0;
        }

        private double CalculateSustained5mWpmLocked(long nowTicks, double penalty)
        {
            int startIndex = FindWindowStartIndexLocked(nowTicks, 300.0);
            int count = _rollingBuffer.Count - startIndex;

            if (count < 5) return LastFinished5mWpm > 0 ? LastFinished5mWpm : 0;

            int chars = 0;
            int backspaces = 0;
            double activeSeconds = 0;
            long burstStart = _rollingBuffer[startIndex].TimestampTicks;
            long burstLast = burstStart;

            if (_rollingBuffer[startIndex].IsBackspace) backspaces++;
            else chars++;

            // Stitch active typing time across bursts (<= 3.5s gap = continuous typing)
            for (int i = startIndex + 1; i < _rollingBuffer.Count; i++)
            {
                var rec = _rollingBuffer[i];
                if (rec.IsBackspace) backspaces++;
                else chars++;

                double gap = _timeProvider.GetElapsedTime(burstLast, rec.TimestampTicks).TotalSeconds;
                if (gap <= 3.5)
                {
                    burstLast = rec.TimestampTicks;
                }
                else
                {
                    double burstDuration = Math.Max(1.0, _timeProvider.GetElapsedTime(burstStart, burstLast).TotalSeconds);
                    activeSeconds += burstDuration;

                    burstStart = rec.TimestampTicks;
                    burstLast = rec.TimestampTicks;
                }
            }

            activeSeconds += Math.Max(1.0, _timeProvider.GetElapsedTime(burstStart, burstLast).TotalSeconds);

            if (activeSeconds < 2.0) activeSeconds = 2.0;

            double netChars = Math.Max(0, chars - (backspaces * (1.0 + penalty)));
            double netWords = netChars / 5.0;
            double activeMinutes = activeSeconds / 60.0;

            return activeMinutes > 0 ? (netWords / activeMinutes) : 0;
        }

        private TypingSession? FinalizeSessionLocked()
        {
            _isSessionActive = false;

            double duration = _timeProvider.GetElapsedTime(_sessionStartTicks, _lastKeystrokeTicks).TotalSeconds;

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
                    StartedAt = _sessionStartUtc,
                    EndedAt = _lastKeystrokeUtc,
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
                _sessionPeak15s = 0;
                _sessionPeak30s = 0;
                _sessionPeak60s = 0;

                Current15sWpm = 0;
                Current30sWpm = 0;
                Current60sWpm = 0;
                Current5mWpm = 0;
                CurrentAccuracy = 100.0;

                _lastVkCode = -1;
                _consecutiveSameKeyCount = 0;
                _unbrokenCharsWithoutBoundary = 0;
                ClearRecentKeyHistory();
                _hasLastRawKeystroke = false;
                _lastRawKeystrokeTicks = 0;
                _lastRawKeystrokeUtc = DateTime.MinValue;
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
