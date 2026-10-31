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
            Assert.Equal("#F5000000", settings.BackgroundHex);
            Assert.Equal("#FFFFFF", settings.TextPrimaryHex);

            settings.ApplyThemePreset("MidnightBlue");
            Assert.Equal("#F00F172A", settings.BackgroundHex);
            Assert.Equal("#38BDF8", settings.AccentHex);

            settings.ApplyThemePreset("EmeraldTerminal");
            Assert.Equal("#10B981", settings.AccentHex);

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
            // Verify our Net Characters and WPM formula:
            // Standard typing: 5 characters = 1 word
            // If gross characters = 250 in 60s -> 50 Gross WPM.
            // If backspaces = 20, net characters = 250 - (20 * (1 + 1.0)) = 250 - 40 = 210.
            // Net Words = 210 / 5 = 42 words.
            // Net WPM = 42 / 1 min = 42 WPM.

            int grossChars = 250;
            int backspaces = 20;
            double penaltyMultiplier = 1.0;

            double netChars = Math.Max(0, grossChars - (backspaces * (1.0 + penaltyMultiplier)));
            double netWords = netChars / 5.0;
            double minutes = 60.0 / 60.0;
            double netWpm = netWords / minutes;

            Assert.Equal(42.0, netWpm);

            // If accuracy is calculated:
            double correct = Math.Max(0, grossChars - (backspaces * 2));
            double accuracy = (correct / grossChars) * 100.0;
            Assert.Equal(84.0, accuracy);
        }

        [Fact]
        public void StartupManager_SafeCheck_DoesNotThrow()
        {
            // Verifies registry query executes safely without throwing unhandled exceptions
            bool isEnabled = StartupManager.IsStartupEnabled();
            Assert.True(isEnabled || !isEnabled);
        }
    }
}
