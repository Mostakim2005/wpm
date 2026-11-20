using System;
using System.Windows;
using System.Windows.Media;
using WpfApplication = System.Windows.Application;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WPMOverlay.Models;

namespace WPMOverlay.Services
{
    public static class ThemeManager
    {
        private static SolidColorBrush CreateFrozenBrush(WpfColor color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public static void ApplyTheme(AppSettings settings)
        {
            if (WpfApplication.Current == null) return;

            try
            {
                var bg = (WpfColor)WpfColorConverter.ConvertFromString(settings.BackgroundHex);
                var border = (WpfColor)WpfColorConverter.ConvertFromString(settings.BorderHex);
                var accent = (WpfColor)WpfColorConverter.ConvertFromString(settings.AccentHex);
                var textPrimary = (WpfColor)WpfColorConverter.ConvertFromString(settings.TextPrimaryHex);
                var textSecondary = (WpfColor)WpfColorConverter.ConvertFromString(settings.TextSecondaryHex);
                var highlight = (WpfColor)WpfColorConverter.ConvertFromString(settings.HighlightHex);

                // Compute perceived luminance (0 to 255) to guarantee WCAG contrast
                double luminance = (0.2126 * bg.R) + (0.7152 * bg.G) + (0.0722 * bg.B);
                bool isLightMode = luminance > 130;

                // Opaque window background for standard non-layered windows (Settings/History)
                WpfColor opaqueWindowBg = WpfColor.FromRgb(bg.R, bg.G, bg.B);

                // Check accent luminance in BOTH light and dark modes
                double accentLum = (0.2126 * accent.R) + (0.7152 * accent.G) + (0.0722 * accent.B);
                WpfColor textOnAccent = accentLum > 130
                    ? WpfColor.FromRgb(15, 23, 42)
                    : WpfColor.FromRgb(255, 255, 255);

                // WCAG AA danger token for destructive actions / strict penalty hints
                WpfColor danger = isLightMode
                    ? WpfColor.FromRgb(220, 38, 38)   // Red 600 on light surfaces
                    : WpfColor.FromRgb(248, 113, 113); // Red 400 on dark surfaces

                WpfColor surface;
                WpfColor surfaceElevated;
                WpfColor inputBg;
                WpfColor divider;

                if (isLightMode)
                {
                    surface = WpfColor.FromRgb(255, 255, 255);             // Pure white card
                    surfaceElevated = WpfColor.FromRgb(226, 232, 240);     // Slate 200
                    inputBg = WpfColor.FromRgb(255, 255, 255);
                    divider = WpfColor.FromArgb(100, 203, 213, 225);
                }
                else
                {
                    // Dark theme elevation
                    surface = WpfColor.FromRgb(
                        (byte)Math.Min(255, bg.R + 10),
                        (byte)Math.Min(255, bg.G + 12),
                        (byte)Math.Min(255, bg.B + 16));
                    surfaceElevated = WpfColor.FromRgb(
                        (byte)Math.Min(255, bg.R + 20),
                        (byte)Math.Min(255, bg.G + 24),
                        (byte)Math.Min(255, bg.B + 32));
                    inputBg = surface;
                    divider = WpfColor.FromArgb(90, border.R, border.G, border.B);
                }

                // Register/Update frozen brushes in Application.Current.Resources
                var res = WpfApplication.Current.Resources;
                res["AppBackgroundBrush"] = CreateFrozenBrush(bg);
                res["AppWindowBackgroundBrush"] = CreateFrozenBrush(opaqueWindowBg);
                res["AppBorderBrush"] = CreateFrozenBrush(border);
                res["AppAccentBrush"] = CreateFrozenBrush(accent);
                res["AppHighlightBrush"] = CreateFrozenBrush(highlight);
                res["AppDangerBrush"] = CreateFrozenBrush(danger);
                res["AppTextPrimaryBrush"] = CreateFrozenBrush(textPrimary);
                res["AppTextSecondaryBrush"] = CreateFrozenBrush(textSecondary);
                res["AppSurfaceBrush"] = CreateFrozenBrush(surface);
                res["AppSurfaceElevatedBrush"] = CreateFrozenBrush(surfaceElevated);
                res["AppInputBackgroundBrush"] = CreateFrozenBrush(inputBg);
                res["AppTextOnAccentBrush"] = CreateFrozenBrush(textOnAccent);
                res["AppDividerBrush"] = CreateFrozenBrush(divider);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply theme tokens: {ex.Message}");
            }
        }
    }
}
