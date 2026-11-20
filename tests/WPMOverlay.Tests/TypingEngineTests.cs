using System;
using System.Globalization;
using System.IO;
using WPMOverlay.Models;
using WPMOverlay.Services;
using WPMOverlay.Views;
using Xunit;

namespace WPMOverlay.Tests
{
    public class TypingEngineTests
    {
        /// <summary>
        /// Deterministic TimeProvider for zero-sleep unit tests of rolling windows,
        /// burst stitching, and session idle transitions.
        /// </summary>
        private sealed class ManualTimeProvider : TimeProvider
        {
            private DateTimeOffset _utcNow;
            private long _timestampTicks;

            public ManualTimeProvider(DateTimeOffset? start = null)
            {
                _utcNow = start ?? new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
                _timestampTicks = 0;
            }

            public override long TimestampFrequency => TimeSpan.TicksPerSecond;

            public override DateTimeOffset GetUtcNow() => _utcNow;

            public override long GetTimestamp() => _timestampTicks;

            public void Advance(TimeSpan delta)
            {
                _utcNow = _utcNow.Add(delta);
                _timestampTicks += delta.Ticks;
            }
        }

        [Fact]
        public void AppSettings_ThemePresets_ProduceValidHexTokensForAllNinePresets()
        {
            var settings = new AppSettings();

            settings.ApplyThemePreset("OLEDMinimal");
            Assert.Equal("#F8000000", settings.BackgroundHex);
            Assert.Equal("#FFFFFF", settings.TextPrimaryHex);

            settings.ApplyThemePreset("LightBlue");
            Assert.Equal("#F5F0F6FC", settings.BackgroundHex);
            Assert.Equal("#0284C7", settings.AccentHex);
            Assert.Equal("#0F172A", settings.TextPrimaryHex);

            settings.ApplyThemePreset("Glassmorphism");
            Assert.Equal("#BA1E293B", settings.BackgroundHex);
            Assert.Equal("#38BDF8", settings.AccentHex);

            settings.ApplyThemePreset("Obsidian");
            Assert.Equal("#F01E1E22", settings.BackgroundHex);
            Assert.Equal("#818CF8", settings.AccentHex);

            settings.ApplyThemePreset("Material3Cyan");
            Assert.Equal("#EE19222E", settings.BackgroundHex);
            Assert.Equal("#38BDF8", settings.AccentHex);

            settings.ApplyThemePreset("CyberpunkNeon");
            Assert.Equal("#EE110E1F", settings.BackgroundHex);
            Assert.Equal("#F43F5E", settings.AccentHex);

            settings.ApplyThemePreset("EmeraldTerminal");
            Assert.Equal("#F0051B11", settings.BackgroundHex);
            Assert.Equal("#10B981", settings.AccentHex);

            settings.ApplyThemePreset("MidnightBlue");
            Assert.Equal("#F00F172A", settings.BackgroundHex);
            Assert.Equal("#38BDF8", settings.AccentHex);

            settings.ApplyThemePreset("CyberDark");
            Assert.Equal("#EE12141A", settings.BackgroundHex);
            Assert.Equal("#2563EB", settings.BorderHex);
        }

        [Fact]
        public void AppSettings_Sanitize_ClampsOutOfRangeValuesAndFixesInvalidHex()
        {
            var settings = new AppSettings
            {
                BackgroundHex = "not-a-color",
                BorderHex = "",
                WindowOpacity = 0.0,
                ScaleFactor = 9.5,
                AutoHideDelaySeconds = -10.0,
                ErrorPenaltyMultiplier = -5.0,
                PositionX = double.NaN,
                PositionY = double.PositiveInfinity
            };

            settings.Sanitize();

            Assert.Equal("#EE12141A", settings.BackgroundHex);
            Assert.Equal("#2563EB", settings.BorderHex);
            Assert.Equal(0.2, settings.WindowOpacity);
            Assert.Equal(2.0, settings.ScaleFactor);
            Assert.Equal(1.0, settings.AutoHideDelaySeconds);
            Assert.Equal(0.5, settings.ErrorPenaltyMultiplier);
            Assert.Null(settings.PositionX);
            Assert.Null(settings.PositionY);
        }

        [Fact]
        public void DatabaseService_SaveAndQuerySummary_WorksCorrectly()
        {
            string dbPath = CreateTempDbPath();
            string tempCsv = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid():N}.csv");
            try
            {
                var db = new DatabaseService(dbPath);

                var testSession = new TypingSession
                {
                    StartedAt = DateTime.UtcNow.AddMinutes(-5),
                    EndedAt = DateTime.UtcNow.AddMinutes(-4),
                    DurationSeconds = 60.0,
                    GrossKeystrokes = 300,
                    Backspaces = 10,
                    NetWords = 56.0,
                    AvgWpm = 56.0,
                    Peak15sWpm = 64.0,
                    Peak30sWpm = 60.0,
                    Peak60sWpm = 56.0,
                    AccuracyPercent = 93.3
                };

                db.SaveSession(testSession);
                Assert.True(testSession.Id > 0);

                var summary = db.GetStatsSummary();
                Assert.Equal(64.0, summary.AllTimeTopWpm, 1);
                Assert.Equal(56.0, summary.TotalWordsTyped, 1);
                Assert.Equal(1, summary.TotalSessions);

                db.ExportToCsv(tempCsv);
                Assert.True(File.Exists(tempCsv));
                string content = File.ReadAllText(tempCsv);
                Assert.Contains("DurationSeconds", content);
                Assert.Contains("56.0", content);
            }
            finally
            {
                if (File.Exists(tempCsv)) File.Delete(tempCsv);
                DeleteTestDatabase(dbPath);
            }
        }

        [Fact]
        public void DatabaseService_TopWpm_ConsidersPeak30sAndPeak60s()
        {
            string dbPath = CreateTempDbPath();
            try
            {
                var db = new DatabaseService(dbPath);

                db.SaveSession(new TypingSession
                {
                    StartedAt = DateTime.UtcNow.AddMinutes(-3),
                    EndedAt = DateTime.UtcNow.AddMinutes(-2),
                    DurationSeconds = 60.0,
                    GrossKeystrokes = 400,
                    Backspaces = 0,
                    NetWords = 80.0,
                    AvgWpm = 70.0,
                    Peak15sWpm = 75.0,
                    Peak30sWpm = 92.5,
                    Peak60sWpm = 88.0,
                    AccuracyPercent = 100.0
                });

                db.SaveSession(new TypingSession
                {
                    StartedAt = DateTime.UtcNow.AddMinutes(-1),
                    EndedAt = DateTime.UtcNow,
                    DurationSeconds = 60.0,
                    GrossKeystrokes = 450,
                    Backspaces = 0,
                    NetWords = 90.0,
                    AvgWpm = 80.0,
                    Peak15sWpm = 85.0,
                    Peak30sWpm = 86.0,
                    Peak60sWpm = 104.0,
                    AccuracyPercent = 100.0
                });

                var summary = db.GetStatsSummary();
                Assert.Equal(104.0, summary.AllTimeTopWpm, 1);
                Assert.Equal(104.0, summary.TodayTopWpm, 1);
            }
            finally
            {
                DeleteTestDatabase(dbPath);
            }
        }

        [Fact]
        public void DatabaseService_TodayTopWpm_RespectsLocalMidnightBoundary()
        {
            string dbPath = CreateTempDbPath();
            try
            {
                var db = new DatabaseService(dbPath);

                // Session strictly before local midnight (yesterday)
                DateTime yesterdayUtc = DateTime.Today.AddMinutes(-30).ToUniversalTime();
                db.SaveSession(new TypingSession
                {
                    StartedAt = yesterdayUtc,
                    EndedAt = yesterdayUtc.AddSeconds(30),
                    DurationSeconds = 30.0,
                    GrossKeystrokes = 300,
                    Backspaces = 0,
                    NetWords = 60.0,
                    AvgWpm = 120.0,
                    Peak15sWpm = 140.0,
                    Peak30sWpm = 130.0,
                    Peak60sWpm = 120.0,
                    AccuracyPercent = 100.0
                });

                // Session strictly after local midnight (today)
                DateTime todayUtc = DateTime.Today.AddMinutes(5).ToUniversalTime();
                db.SaveSession(new TypingSession
                {
                    StartedAt = todayUtc,
                    EndedAt = todayUtc.AddSeconds(30),
                    DurationSeconds = 30.0,
                    GrossKeystrokes = 200,
                    Backspaces = 0,
                    NetWords = 40.0,
                    AvgWpm = 72.0,
                    Peak15sWpm = 78.0,
                    Peak30sWpm = 82.0,
                    Peak60sWpm = 75.0,
                    AccuracyPercent = 100.0
                });

                var summary = db.GetStatsSummary();
                Assert.Equal(140.0, summary.AllTimeTopWpm, 1);
                Assert.Equal(82.0, summary.TodayTopWpm, 1);
            }
            finally
            {
                DeleteTestDatabase(dbPath);
            }
        }

        [Fact]
        public void DatabaseService_ExportToCsv_IsLocaleInvariantUnderCommaDecimalCulture()
        {
            string dbPath = CreateTempDbPath();
            string tempCsv = Path.Combine(Path.GetTempPath(), $"test_de_export_{Guid.NewGuid():N}.csv");
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            try
            {
                var db = new DatabaseService(dbPath);
                db.SaveSession(new TypingSession
                {
                    StartedAt = DateTime.UtcNow.AddMinutes(-2),
                    EndedAt = DateTime.UtcNow.AddMinutes(-1),
                    DurationSeconds = 12.34,
                    GrossKeystrokes = 100,
                    Backspaces = 2,
                    NetWords = 18.8,
                    AvgWpm = 91.4,
                    Peak15sWpm = 98.6,
                    Peak30sWpm = 95.2,
                    Peak60sWpm = 91.4,
                    AccuracyPercent = 96.0
                });

                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                db.ExportToCsv(tempCsv);

                string[] lines = File.ReadAllLines(tempCsv);
                Assert.Equal(2, lines.Length);

                string[] columns = lines[1].Split(',');
                Assert.Equal(11, columns.Length);
                Assert.Equal("12.34", columns[2]);
                Assert.Equal("96.0", columns[5]);
                Assert.Equal("91.4", columns[7]);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                if (File.Exists(tempCsv)) File.Delete(tempCsv);
                DeleteTestDatabase(dbPath);
            }
        }

        [Fact]
        public void DatabaseService_DeleteSession_RemovesRecordAndRecalculatesStats()
        {
            string dbPath = CreateTempDbPath();
            try
            {
                var db = new DatabaseService(dbPath);

                var session1 = new TypingSession
                {
                    StartedAt = DateTime.UtcNow.AddMinutes(-10),
                    EndedAt = DateTime.UtcNow.AddMinutes(-9),
                    DurationSeconds = 60.0,
                    GrossKeystrokes = 200,
                    Backspaces = 0,
                    NetWords = 40.0,
                    AvgWpm = 40.0,
                    Peak15sWpm = 45.0,
                    Peak30sWpm = 42.0,
                    Peak60sWpm = 40.0,
                    AccuracyPercent = 100
                };

                var accidentalSpamSession = new TypingSession
                {
                    StartedAt = DateTime.UtcNow.AddMinutes(-5),
                    EndedAt = DateTime.UtcNow.AddMinutes(-4),
                    DurationSeconds = 10.0,
                    GrossKeystrokes = 500,
                    Backspaces = 0,
                    NetWords = 100.0,
                    AvgWpm = 300.0,
                    Peak15sWpm = 350.0,
                    Peak30sWpm = 300.0,
                    Peak60sWpm = 250.0,
                    AccuracyPercent = 100
                };

                db.SaveSession(session1);
                db.SaveSession(accidentalSpamSession);

                var summaryBefore = db.GetStatsSummary();
                Assert.Equal(350.0, summaryBefore.AllTimeTopWpm, 1);

                // Delete the accidental session
                bool deleted = db.DeleteSession(accidentalSpamSession.Id);
                Assert.True(deleted);

                var summaryAfter = db.GetStatsSummary();
                Assert.Equal(45.0, summaryAfter.AllTimeTopWpm, 1);
                Assert.Equal(1, summaryAfter.TotalSessions);

                var recent = db.GetRecentSessions(100);
                Assert.DoesNotContain(recent, s => s.Id == accidentalSpamSession.Id);
            }
            finally
            {
                DeleteTestDatabase(dbPath);
            }
        }

        [Fact]
        public void TypingEngine_NetWpmAndAccuracy_DeductsBackspacesCorrectly()
        {
            string dbPath = CreateTempDbPath();
            string settingsPath = CreateTempSettingsPath();
            try
            {
                var time = new ManualTimeProvider();
                using var hook = new KeyboardHook();
                var db = new DatabaseService(dbPath);
                var settings = new SettingsService(settingsPath);
                using var engine = new TypingEngine(hook, db, settings, time);

                // Type 25 characters with spaces (5 words) plus 2 backspaces over 5 seconds
                // Net chars = 25 - (2 * 2.0) = 21 => 4.2 words over 5s => ~50.4 WPM
                int[] sequence = { 65, 66, 67, 68, 0x20, 69, 70, 71, 72, 0x20, 73, 74, 75, 76, 0x20, 77, 78, 79, 80, 0x20, 81, 82, 83, 84, 0x20 };
                foreach (int vk in sequence)
                {
                    engine.ProcessKeystroke(vk, isBackspace: false);
                    time.Advance(TimeSpan.FromMilliseconds(180));
                }

                engine.ProcessKeystroke(0x08, isBackspace: true);
                time.Advance(TimeSpan.FromMilliseconds(250));
                engine.ProcessKeystroke(0x08, isBackspace: true);
                time.Advance(TimeSpan.FromMilliseconds(250));

                engine.FlushAndRecalculate();

                Assert.True(engine.IsTypingActive);
                Assert.InRange(engine.Current15sWpm, 45.0, 56.0);
                Assert.InRange(engine.CurrentAccuracy, 84.0, 87.0);
            }
            finally
            {
                DeleteTestDatabase(dbPath);
                DeleteTestSettings(settingsPath);
            }
        }

        [Fact]
        public void TypingEngine_Sustained5mBurstStitching_IgnoresLongPauses()
        {
            string dbPath = CreateTempDbPath();
            string settingsPath = CreateTempSettingsPath();
            try
            {
                var time = new ManualTimeProvider();
                using var hook = new KeyboardHook();
                var db = new DatabaseService(dbPath);
                var settings = new SettingsService(settingsPath);
                using var engine = new TypingEngine(hook, db, settings, time);

                // Burst 1: 25 chars over 5s (~60 WPM)
                for (int i = 0; i < 25; i++)
                {
                    int vk = (i % 5 == 4) ? 0x20 : 65 + (i % 4);
                    engine.ProcessKeystroke(vk, isBackspace: false);
                    time.Advance(TimeSpan.FromMilliseconds(200));
                }
                engine.FlushAndRecalculate();

                // 40-second reading pause (should NOT penalize sustained 5m active time)
                time.Advance(TimeSpan.FromSeconds(40));
                engine.FlushAndRecalculate();

                // Burst 2: 25 chars over 5s (~60 WPM)
                for (int i = 0; i < 25; i++)
                {
                    int vk = (i % 5 == 4) ? 0x20 : 70 + (i % 4);
                    engine.ProcessKeystroke(vk, isBackspace: false);
                    time.Advance(TimeSpan.FromMilliseconds(200));
                }
                engine.FlushAndRecalculate();

                // If the 40s pause were counted, 10 words / 50s would be ~12 WPM;
                // stitched active time (~10s) yields ~60 WPM.
                Assert.InRange(engine.Current5mWpm, 50.0, 70.0);
            }
            finally
            {
                DeleteTestDatabase(dbPath);
                DeleteTestSettings(settingsPath);
            }
        }

        [Fact]
        public void TypingEngine_RefreshStatsSummary_ClearsLastFinishedAfterHistoryCleared()
        {
            string dbPath = CreateTempDbPath();
            string settingsPath = CreateTempSettingsPath();
            try
            {
                var time = new ManualTimeProvider();
                using var hook = new KeyboardHook();
                var db = new DatabaseService(dbPath);
                var settings = new SettingsService(settingsPath);
                using var engine = new TypingEngine(hook, db, settings, time);

                // Type a valid session and let it finalize
                for (int i = 0; i < 15; i++)
                {
                    int vk = (i % 5 == 4) ? 0x20 : 65 + (i % 4);
                    engine.ProcessKeystroke(vk, isBackspace: false);
                    time.Advance(TimeSpan.FromMilliseconds(200));
                }
                engine.FlushAndRecalculate();

                // Exceed session idle timeout (5s) so session saves to SQLite
                time.Advance(TimeSpan.FromSeconds(6));
                engine.FlushAndRecalculate();

                Assert.True(engine.LastFinishedAvgWpm > 0);

                // Clear history and refresh stats — LastFinished* must reset to 0
                db.ClearHistory();
                engine.RefreshStatsSummary(forceSyncLastSession: true);

                Assert.Equal(0, engine.LastFinishedAvgWpm);
                Assert.Equal(0, engine.LastFinished15sWpm);
                Assert.Equal(0, engine.CachedSummary.TotalSessions);
            }
            finally
            {
                DeleteTestDatabase(dbPath);
                DeleteTestSettings(settingsPath);
            }
        }

        [Fact]
        public void TypingEngine_ResetSession_ClearsPeaksAndResetsAccuracy()
        {
            string dbPath = CreateTempDbPath();
            string settingsPath = CreateTempSettingsPath();
            try
            {
                var time = new ManualTimeProvider();
                using var hook = new KeyboardHook();
                var db = new DatabaseService(dbPath);
                var settings = new SettingsService(settingsPath);
                using var engine = new TypingEngine(hook, db, settings, time);

                for (int i = 0; i < 10; i++)
                {
                    engine.ProcessKeystroke(65 + (i % 4), isBackspace: false);
                    time.Advance(TimeSpan.FromMilliseconds(150));
                }
                engine.ProcessKeystroke(0x08, isBackspace: true);
                time.Advance(TimeSpan.FromMilliseconds(150));
                engine.FlushAndRecalculate();

                Assert.True(engine.CurrentAccuracy < 100.0);

                engine.ResetSession();

                Assert.False(engine.IsTypingActive);
                Assert.Equal(0, engine.Current15sWpm);
                Assert.Equal(100.0, engine.CurrentAccuracy);
            }
            finally
            {
                DeleteTestDatabase(dbPath);
                DeleteTestSettings(settingsPath);
            }
        }

        [Fact]
        public void SettingsWindow_Instantiation_DoesNotThrowNullReferenceException()
        {
            string settingsPath = CreateTempSettingsPath();
            try
            {
                Exception? caught = null;
                var thread = new System.Threading.Thread(() =>
                {
                    try
                    {
                        var settingsService = new SettingsService(settingsPath);
                        var win = new SettingsWindow(settingsService);
                        Assert.NotNull(win);
                    }
                    catch (Exception ex)
                    {
                        caught = ex;
                    }
                });
                thread.SetApartmentState(System.Threading.ApartmentState.STA);
                thread.Start();
                thread.Join();
                Assert.Null(caught);
            }
            finally
            {
                DeleteTestSettings(settingsPath);
            }
        }

        [Fact]
        public void KeyboardHook_IsIgnoredKey_FiltersSystemNavigationFunctionAndMediaKeys()
        {
            Assert.True(KeyboardHook.IsIgnoredKey(0x1B)); // Escape
            Assert.True(KeyboardHook.IsIgnoredKey(0x13)); // Pause
            Assert.True(KeyboardHook.IsIgnoredKey(0x70)); // F1
            Assert.True(KeyboardHook.IsIgnoredKey(0x7B)); // F12
            Assert.True(KeyboardHook.IsIgnoredKey(0x25)); // Left Arrow
            Assert.True(KeyboardHook.IsIgnoredKey(0x28)); // Down Arrow
            Assert.True(KeyboardHook.IsIgnoredKey(0x2D)); // Insert
            Assert.True(KeyboardHook.IsIgnoredKey(0x90)); // NumLock
            Assert.True(KeyboardHook.IsIgnoredKey(0xAD)); // Volume Mute

            Assert.False(KeyboardHook.IsIgnoredKey(0x41)); // 'A'
            Assert.False(KeyboardHook.IsIgnoredKey(0x20)); // Space
            Assert.False(KeyboardHook.IsIgnoredKey(0x08)); // Backspace
            Assert.False(KeyboardHook.IsIgnoredKey(0x2E)); // Delete
        }

        [Fact]
        public void SmartFiltering_ConsecutiveSameKey_DropsThirdAndLaterRepeats()
        {
            string dbPath = CreateTempDbPath();
            string settingsPath = CreateTempSettingsPath();
            try
            {
                using var hook = new KeyboardHook();
                var db = new DatabaseService(dbPath);
                var settings = new SettingsService(settingsPath);
                using var engine = new TypingEngine(hook, db, settings);

                var start = DateTime.UtcNow;

                // 1st 'A' (VK 65) -> valid
                Assert.True(engine.IsValidTypingKeystroke(65, false, start.AddMilliseconds(50)));
                // 2nd 'A' (VK 65) -> valid (e.g. double letter like in "book")
                Assert.True(engine.IsValidTypingKeystroke(65, false, start.AddMilliseconds(150)));
                // 3rd 'A' (VK 65) -> invalid (auto-repeat / key hold)
                Assert.False(engine.IsValidTypingKeystroke(65, false, start.AddMilliseconds(250)));
                // 4th 'A' (VK 65) -> invalid (auto-repeat / key hold)
                Assert.False(engine.IsValidTypingKeystroke(65, false, start.AddMilliseconds(350)));

                // Switching to 'B' (VK 66) -> valid
                Assert.True(engine.IsValidTypingKeystroke(66, false, start.AddMilliseconds(450)));
            }
            finally
            {
                DeleteTestDatabase(dbPath);
                DeleteTestSettings(settingsPath);
            }
        }

        [Fact]
        public void SmartFiltering_ChatterUnder30ms_IsDropped()
        {
            string dbPath = CreateTempDbPath();
            string settingsPath = CreateTempSettingsPath();
            try
            {
                using var hook = new KeyboardHook();
                var db = new DatabaseService(dbPath);
                var settings = new SettingsService(settingsPath);
                using var engine = new TypingEngine(hook, db, settings);

                var now = DateTime.UtcNow;

                // Key 1 -> valid
                Assert.True(engine.IsValidTypingKeystroke(65, false, now));
                // Key 2 in 10ms (physically impossible for human finger, switch bounce) -> dropped
                Assert.False(engine.IsValidTypingKeystroke(66, false, now.AddMilliseconds(10)));
                // Key 3 after 60ms -> valid
                Assert.True(engine.IsValidTypingKeystroke(67, false, now.AddMilliseconds(70)));
            }
            finally
            {
                DeleteTestDatabase(dbPath);
                DeleteTestSettings(settingsPath);
            }
        }

        [Fact]
        public void SmartFiltering_LongUnspacedMash_DropsAfter25CharsUntilBoundary()
        {
            string dbPath = CreateTempDbPath();
            string settingsPath = CreateTempSettingsPath();
            try
            {
                using var hook = new KeyboardHook();
                var db = new DatabaseService(dbPath);
                var settings = new SettingsService(settingsPath);
                using var engine = new TypingEngine(hook, db, settings);

                var now = DateTime.UtcNow;

                // Type 25 characters without space using 4 different keys (A, B, C, D)
                for (int i = 0; i < 25; i++)
                {
                    int key = 65 + (i % 4);
                    now = now.AddMilliseconds(50);
                    Assert.True(engine.IsValidTypingKeystroke(key, false, now), $"Failed at key {i}");
                }

                // 26th character with no space -> dropped as key mashing streak
                now = now.AddMilliseconds(50);
                Assert.False(engine.IsValidTypingKeystroke(70, false, now));

                // Space (VK 0x20) -> resets streak and is accepted
                now = now.AddMilliseconds(50);
                Assert.True(engine.IsValidTypingKeystroke(0x20, false, now));

                // Next character after space -> accepted
                now = now.AddMilliseconds(50);
                Assert.True(engine.IsValidTypingKeystroke(70, false, now));
            }
            finally
            {
                DeleteTestDatabase(dbPath);
                DeleteTestSettings(settingsPath);
            }
        }

        [Fact]
        public void SmartFiltering_OscillationSpam_IsDropped()
        {
            string dbPath = CreateTempDbPath();
            string settingsPath = CreateTempSettingsPath();
            try
            {
                using var hook = new KeyboardHook();
                var db = new DatabaseService(dbPath);
                var settings = new SettingsService(settingsPath);
                using var engine = new TypingEngine(hook, db, settings);

                var now = DateTime.UtcNow;

                // Alternate A B A B A
                Assert.True(engine.IsValidTypingKeystroke(65, false, now.AddMilliseconds(50)));
                Assert.True(engine.IsValidTypingKeystroke(66, false, now.AddMilliseconds(100)));
                Assert.True(engine.IsValidTypingKeystroke(65, false, now.AddMilliseconds(150)));
                Assert.True(engine.IsValidTypingKeystroke(66, false, now.AddMilliseconds(200)));
                Assert.True(engine.IsValidTypingKeystroke(65, false, now.AddMilliseconds(250)));

                // 6th alternating key: A B A B A B -> triggers oscillation detector
                Assert.False(engine.IsValidTypingKeystroke(66, false, now.AddMilliseconds(300)));
            }
            finally
            {
                DeleteTestDatabase(dbPath);
                DeleteTestSettings(settingsPath);
            }
        }

        [Fact]
        public void SmartFiltering_WordBoundary_ClearsOscillationHistory()
        {
            string dbPath = CreateTempDbPath();
            string settingsPath = CreateTempSettingsPath();
            try
            {
                using var hook = new KeyboardHook();
                var db = new DatabaseService(dbPath);
                var settings = new SettingsService(settingsPath);
                using var engine = new TypingEngine(hook, db, settings);

                var now = DateTime.UtcNow;
                // Typing "ab ab ab" with spaces between short words must not false-positive
                int[] keys = { 65, 66, 0x20, 65, 66, 0x20, 65, 66 };
                foreach (int vk in keys)
                {
                    now = now.AddMilliseconds(80);
                    Assert.True(engine.IsValidTypingKeystroke(vk, false, now), $"Unexpected drop for VK {vk}");
                }
            }
            finally
            {
                DeleteTestDatabase(dbPath);
                DeleteTestSettings(settingsPath);
            }
        }

        [Fact]
        public void SmartFiltering_NaturalTypingWithSpaces_AllKeystrokesAccepted()
        {
            string dbPath = CreateTempDbPath();
            string settingsPath = CreateTempSettingsPath();
            try
            {
                using var hook = new KeyboardHook();
                var db = new DatabaseService(dbPath);
                var settings = new SettingsService(settingsPath);
                using var engine = new TypingEngine(hook, db, settings);

                var now = DateTime.UtcNow;
                string text = "the quick brown fox";

                foreach (char c in text)
                {
                    now = now.AddMilliseconds(80);
                    int vk = (c == ' ') ? 0x20 : (int)char.ToUpper(c);
                    Assert.True(engine.IsValidTypingKeystroke(vk, false, now), $"Failed for char '{c}'");
                }
            }
            finally
            {
                DeleteTestDatabase(dbPath);
                DeleteTestSettings(settingsPath);
            }
        }

        private static string CreateTempDbPath() =>
            Path.Combine(Path.GetTempPath(), $"wpm_test_{Guid.NewGuid():N}.db");

        private static string CreateTempSettingsPath() =>
            Path.Combine(Path.GetTempPath(), $"wpm_settings_{Guid.NewGuid():N}.json");

        private static void DeleteTestDatabase(string path)
        {
            foreach (string file in new[] { path, path + "-wal", path + "-shm" })
            {
                try
                {
                    if (File.Exists(file)) File.Delete(file);
                }
                catch (IOException)
                {
                }
            }
        }

        private static void DeleteTestSettings(string path)
        {
            foreach (string file in new[] { path, path + ".tmp" })
            {
                try
                {
                    if (File.Exists(file)) File.Delete(file);
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
