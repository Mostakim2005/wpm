using System.Windows;
using WpfApplication = System.Windows.Application;
using WPMOverlay.Services;

namespace WPMOverlay
{
    public partial class App : WpfApplication
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var settingsService = new SettingsService();
            ThemeManager.ApplyTheme(settingsService.CurrentSettings);
        }
    }
}
