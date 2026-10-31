using System;

namespace WPMOverlay.Models
{
    public class AppSettings
    {
        public string ThemePreset { get; set; } = "CyberDark";

        // Color Tokens (using color theory principles)
        public string BackgroundHex { get; set; } = "#EE12141A"; // Deep Charcoal with 93% opacity
        public string BorderHex { get; set; } = "#3B82F6";       // Vibrant Blue border or subtle stroke
        public string AccentHex { get; set; } = "#38BDF8";       // Sky Blue (high contrast accent)
        public string TextPrimaryHex { get; set; } = "#F8FAFC";  // Slate 50
        public string TextSecondaryHex { get; set; } = "#94A3B8";// Slate 400
        public string HighlightHex { get; set; } = "#10B981";    // Emerald Green for high metrics

        // Dimensions and Opacity
        public double WindowOpacity { get; set; } = 0.95;
        public double ScaleFactor { get; set; } = 1.0;
        public double PositionX { get; set; } = -1; // -1 means default positioning
        public double PositionY { get; set; } = -1;

        // Auto-Hide & Idle Settings
        public bool AutoHideEnabled { get; set; } = true;
        public double AutoHideDelaySeconds { get; set; } = 4.0;
        public string IdleDisplayMode { get; set; } = "ShowLastSession"; // "ShowLastSession" or "FadeOut"

        // Metric Toggles (Matching hand-drawn sketch)
        public bool Show15s { get; set; } = true;
        public bool Show30s { get; set; } = true;
        public bool Show60s { get; set; } = true;
        public bool ShowTop { get; set; } = true;
        public bool ShowAvg { get; set; } = true;
        public bool ShowLast { get; set; } = true;

        // General
        public bool AlwaysOnTop { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public double ErrorPenaltyMultiplier { get; set; } = 1.0; // Net WPM penalty multiplier

        public static AppSettings CreateDefault()
        {
            return new AppSettings();
        }

        public void ApplyThemePreset(string preset)
        {
            ThemePreset = preset;
            switch (preset)
            {
                case "OLEDMinimal":
                    BackgroundHex = "#F5000000";
                    BorderHex = "#27272A";
                    AccentHex = "#E4E4E7";
                    TextPrimaryHex = "#FFFFFF";
                    TextSecondaryHex = "#71717A";
                    HighlightHex = "#A1A1AA";
                    break;

                case "MidnightBlue":
                    BackgroundHex = "#F00F172A";
                    BorderHex = "#1E293B";
                    AccentHex = "#38BDF8";
                    TextPrimaryHex = "#F8FAFC";
                    TextSecondaryHex = "#64748B";
                    HighlightHex = "#34D399";
                    break;

                case "EmeraldTerminal":
                    BackgroundHex = "#F0051B11";
                    BorderHex = "#064E3B";
                    AccentHex = "#10B981";
                    TextPrimaryHex = "#ECFDF5";
                    TextSecondaryHex = "#6EE7B7";
                    HighlightHex = "#34D399";
                    break;

                case "SunsetViolet":
                    BackgroundHex = "#F0190D2E";
                    BorderHex = "#3B1B54";
                    AccentHex = "#A855F7";
                    TextPrimaryHex = "#FAF5FF";
                    TextSecondaryHex = "#C084FC";
                    HighlightHex = "#F43F5E";
                    break;

                case "CyberDark":
                default:
                    BackgroundHex = "#EE12141A";
                    BorderHex = "#2563EB";
                    AccentHex = "#38BDF8";
                    TextPrimaryHex = "#F8FAFC";
                    TextSecondaryHex = "#94A3B8";
                    HighlightHex = "#10B981";
                    break;
            }
        }
    }
}
