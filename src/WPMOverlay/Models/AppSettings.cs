using System;

namespace WPMOverlay.Models
{
    public class AppSettings
    {
        public string ThemePreset { get; set; } = "CyberDark";

        // Color Tokens (using color theory principles)
        public string BackgroundHex { get; set; } = "#EE12141A"; // Deep Charcoal with 93% opacity
        public string BorderHex { get; set; } = "#3B82F6";       // Vibrant Blue border
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
        public string IdleDisplayMode { get; set; } = "ShowLastSession";

        // Metric Toggles (Matching hand-drawn sketch)
        public bool Show15s { get; set; } = true;
        public bool Show30s { get; set; } = true;
        public bool Show60s { get; set; } = true;
        public bool Show5m { get; set; } = true;
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
                case "LightBlue":
                    BackgroundHex = "#F5F0F6FC"; // Crisp Ice Blue
                    BorderHex = "#38BDF8";       // Sky Blue edge
                    AccentHex = "#0284C7";       // Deep Ocean Blue
                    TextPrimaryHex = "#0F172A";  // Dark Slate
                    TextSecondaryHex = "#475569";// Medium Slate
                    HighlightHex = "#059669";    // Forest Emerald
                    break;

                case "Glassmorphism":
                    BackgroundHex = "#BA1E293B"; // Translucent Frosted Glass (73% alpha)
                    BorderHex = "#60A5FA";       // Radiant Edge Glow
                    AccentHex = "#38BDF8";       // Electric Cyan
                    TextPrimaryHex = "#FFFFFF";  // Pure White
                    TextSecondaryHex = "#CBD5E1";// Frosted Mist
                    HighlightHex = "#4ADE80";    // Bright Lime
                    break;

                case "Obsidian":
                    BackgroundHex = "#F01E1E22"; // Graphite Vault
                    BorderHex = "#3E3E48";       // Cool Zinc
                    AccentHex = "#818CF8";       // Soft Indigo
                    TextPrimaryHex = "#F4F4F5";  // Crisp White
                    TextSecondaryHex = "#A1A1AA";// Muted Zinc
                    HighlightHex = "#A78BFA";    // Violet Glow
                    break;

                case "Material3Cyan":
                    BackgroundHex = "#EE19222E"; // M3 Tonal Surface
                    BorderHex = "#0284C7";       // M3 Primary Container Stroke
                    AccentHex = "#38BDF8";       // M3 Primary Cyan
                    TextPrimaryHex = "#E2E8F0";  // On-Surface White
                    TextSecondaryHex = "#94A3B8";// On-Surface Variant
                    HighlightHex = "#34D399";    // M3 Tertiary Mint
                    break;

                case "CyberpunkNeon":
                    BackgroundHex = "#EE110E1F"; // Deep Neon Plum
                    BorderHex = "#F43F5E";       // Hot Rose
                    AccentHex = "#F43F5E";       // Hot Rose
                    TextPrimaryHex = "#FFF1F2";  // Blush White
                    TextSecondaryHex = "#FB7185";// Soft Rose
                    HighlightHex = "#F59E0B";    // Neon Amber
                    break;

                case "OLEDMinimal":
                    BackgroundHex = "#F8000000"; // Absolute Black
                    BorderHex = "#27272A";       // Subtle Zinc Edge
                    AccentHex = "#E4E4E7";       // Platinum
                    TextPrimaryHex = "#FFFFFF";  // Pure White
                    TextSecondaryHex = "#71717A";// Muted Gray
                    HighlightHex = "#A1A1AA";    // Silver
                    break;

                case "MidnightBlue":
                    BackgroundHex = "#F00F172A"; // Navy Sapphire
                    BorderHex = "#1E293B";       // Slate Blue Edge
                    AccentHex = "#38BDF8";       // Sky Blue
                    TextPrimaryHex = "#F8FAFC";  // Bright Slate
                    TextSecondaryHex = "#64748B";// Slate Neutral
                    HighlightHex = "#34D399";    // Mint Green
                    break;

                case "EmeraldTerminal":
                    BackgroundHex = "#F0051B11"; // Hacker Dark Green
                    BorderHex = "#064E3B";       // Deep Pine
                    AccentHex = "#10B981";       // Emerald Mint
                    TextPrimaryHex = "#ECFDF5";  // Phosphor Green
                    TextSecondaryHex = "#6EE7B7";// Soft Matrix
                    HighlightHex = "#34D399";    // Neon Lime
                    break;

                case "CyberDark":
                default:
                    BackgroundHex = "#EE12141A"; // Balanced Slate
                    BorderHex = "#2563EB";       // Vibrant Blue
                    AccentHex = "#38BDF8";       // Cyan
                    TextPrimaryHex = "#F8FAFC";  // Light Gray
                    TextSecondaryHex = "#94A3B8";// Muted Gray
                    HighlightHex = "#10B981";    // Emerald
                    break;
            }
        }
    }
}
