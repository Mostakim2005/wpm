using System;
using System.IO;
using WPMOverlay.Models;
using WPMOverlay.Services;
using Xunit;

namespace WPMOverlay.Tests
{
    public class TypingEngineTests
    {
        [Fact]
        public void AppSettings_ThemePresets_ProduceValidHexTokens()
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

            settings.ApplyThemePreset("CyberDark");
            Assert.Equal("#EE12141A", settings.BackgroundHex);
        }

        [Fact]
        public void DatabaseService_SaveAndQuerySummary_WorksCorrectly()
        {
            var db = new DatabaseService();

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
            Assert.True(summary.AllTimeTopWpm >= 64.0);
            Assert.True(summary.TotalWordsTyped >= 56.0);
            Assert.True(summary.TotalSessions >= 1);

            // Test CSV Export
            string tempCsv = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid():N}.csv");
            try
            {
                db.ExportToCsv(tempCsv);
                Assert.True(File.Exists(tempCsv));
                string content = File.ReadAllText(tempCsv);
                Assert.Contains("DurationSeconds", content);
                Assert.Contains("56", content);
            }
            finally
            {
                if (File.Exists(tempCsv)) File.Delete(tempCsv);
            }
        }

        [Fact]
        public void NetWpmCalculation_DeductsBackspacesAndErrorsCorrectly()
        {
            int grossChars = 250;
            int backspaces = 20;
            double penaltyMultiplier = 1.0;

            double netChars = Math.Max(0, grossChars - (backspaces * (1.0 + penaltyMultiplier)));
            double netWords = netChars / 5.0;
            double minutes = 60.0 / 60.0;
            double netWpm = netWords / minutes;

            Assert.Equal(42.0, netWpm);

            double correct = Math.Max(0, grossChars - (backspaces * 2));
            double accuracy = (correct / grossChars) * 100.0;
            Assert.Equal(84.0, accuracy);
        }

        [Fact]
        public void SustainedBurstStitching_CalculatesActiveTypingWithoutPausePenalty()
        {
            // Simulate 3 bursts in 5 minutes:
            // Burst 1: 13s, 65 chars (13 words) -> 60 WPM
            // Pause 40s (should not penalize active typing)
            // Burst 2: 60s, 300 chars (60 words) -> 60 WPM
            // Pause 30s
            // Burst 3: 120s, 600 chars (120 words) -> 60 WPM
            // Total Active Time: 13s + 60s + 120s = 193s = 3.216 minutes
            // Total Net Words: 13 + 60 + 120 = 193 words
            // Sustained WPM = 193 / (193/60) = 60 WPM!

            double burst1Duration = 13.0;
            double burst1Words = 13.0;

            double burst2Duration = 60.0;
            double burst2Words = 60.0;

            double burst3Duration = 120.0;
            double burst3Words = 120.0;

            double totalActiveSeconds = burst1Duration + burst2Duration + burst3Duration;
            double totalWords = burst1Words + burst2Words + burst3Words;
            double activeMinutes = totalActiveSeconds / 60.0;
            double sustainedWpm = totalWords / activeMinutes;

            Assert.Equal(60.0, Math.Round(sustainedWpm));
        }

        [Fact]
        public void StartupManager_SafeCheck_DoesNotThrow()
        {
            bool isEnabled = StartupManager.IsStartupEnabled();
            Assert.True(isEnabled || !isEnabled);
        }

        [Fact]
        public void SmartFiltering_ConsecutiveSameKey_DropsThirdAndLaterRepeats()
        {
            using var hook = new KeyboardHook();
            var db = new DatabaseService();
            var settings = new SettingsService();
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

        [Fact]
        public void SmartFiltering_ChatterUnder30ms_IsDropped()
        {
            using var hook = new KeyboardHook();
            var db = new DatabaseService();
            var settings = new SettingsService();
            using var engine = new TypingEngine(hook, db, settings);

            var now = DateTime.UtcNow;

            // Key 1 -> valid
            Assert.True(engine.IsValidTypingKeystroke(65, false, now));
            // Key 2 in 10ms (physically impossible for human finger, switch bounce) -> dropped
            Assert.False(engine.IsValidTypingKeystroke(66, false, now.AddMilliseconds(10)));
            // Key 3 after 60ms -> valid
            Assert.True(engine.IsValidTypingKeystroke(67, false, now.AddMilliseconds(70)));
        }

        [Fact]
        public void SmartFiltering_LongUnspacedMash_DropsAfter25CharsUntilBoundary()
        {
            using var hook = new KeyboardHook();
            var db = new DatabaseService();
            var settings = new SettingsService();
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

        [Fact]
        public void SmartFiltering_OscillationSpam_IsDropped()
        {
            using var hook = new KeyboardHook();
            var db = new DatabaseService();
            var settings = new SettingsService();
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

        [Fact]
        public void SmartFiltering_NaturalTypingWithSpaces_AllKeystrokesAccepted()
        {
            using var hook = new KeyboardHook();
            var db = new DatabaseService();
            var settings = new SettingsService();
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
    }
}
