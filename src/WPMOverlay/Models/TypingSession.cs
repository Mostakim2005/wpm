using System;

namespace WPMOverlay.Models
{
    public class TypingSession
    {
        public long Id { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
        public double DurationSeconds { get; set; }
        public int GrossKeystrokes { get; set; }
        public int Backspaces { get; set; }
        public double NetWords { get; set; }
        public double AvgWpm { get; set; }
        public double Peak15sWpm { get; set; }
        public double Peak30sWpm { get; set; }
        public double Peak60sWpm { get; set; }
        public double AccuracyPercent { get; set; }

        public string FormattedDate => StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        public string FormattedDuration => $"{DurationSeconds:F1}s";
        public string FormattedAvgWpm => $"{AvgWpm:F0} WPM";
        public string FormattedPeakWpm => $"{Math.Max(Peak15sWpm, Math.Max(Peak30sWpm, Peak60sWpm)):F0} WPM";
        public string FormattedAccuracy => $"{AccuracyPercent:F1}%";
    }
}
