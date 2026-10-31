using System;
using System.Windows;
using WpfMessageBox = System.Windows.MessageBox;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WPMOverlay.Services;

namespace WPMOverlay.Views
{
    public partial class HistoryWindow : Window
    {
        private readonly DatabaseService _databaseService;

        public HistoryWindow(DatabaseService databaseService)
        {
            InitializeComponent();
            _databaseService = databaseService;

            Loaded += HistoryWindow_Loaded;
        }

        private void HistoryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void LoadData()
        {
            var summary = _databaseService.GetStatsSummary();
            TxtAllTimeTop.Text = $"{summary.AllTimeTopWpm:F0} WPM";
            TxtTodayTop.Text = $"{summary.TodayTopWpm:F0} WPM";
            TxtLifetimeAvg.Text = $"{summary.OverallAvgWpm:F0} WPM";
            TxtTotalWords.Text = $"{summary.TotalWordsTyped:N0}";
            TxtTotalSessions.Text = $"{summary.TotalSessions:N0}";

            var sessions = _databaseService.GetRecentSessions(250);
            GridSessions.ItemsSource = sessions;
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new WpfSaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"WPM_History_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    _databaseService.ExportToCsv(sfd.FileName);
                    WpfMessageBox.Show("Typing history exported successfully!", "Export Completed", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    WpfMessageBox.Show($"Failed to export CSV: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var result = WpfMessageBox.Show(
                "Are you sure you want to clear all typing history? This cannot be undone.",
                "Confirm Clear History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _databaseService.ClearHistory();
                LoadData();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
