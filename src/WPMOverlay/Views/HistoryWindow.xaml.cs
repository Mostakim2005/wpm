using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WpfMessageBox = System.Windows.MessageBox;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WPMOverlay.Models;
using WPMOverlay.Services;

namespace WPMOverlay.Views
{
    public partial class HistoryWindow : Window
    {
        private readonly DatabaseService _databaseService;
        private readonly TypingEngine? _typingEngine;

        public HistoryWindow(DatabaseService databaseService, TypingEngine? typingEngine = null)
        {
            InitializeComponent();
            _databaseService = databaseService;
            _typingEngine = typingEngine;

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

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = GridSessions.SelectedItems.OfType<TypingSession>().ToList();

            if (selectedItems.Count == 0)
            {
                WpfMessageBox.Show(
                    "Please select one or more sessions from the table to delete.",
                    "No Entry Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            string confirmPrompt;
            if (selectedItems.Count == 1)
            {
                var s = selectedItems[0];
                confirmPrompt = $"Are you sure you want to permanently delete this typing session?\n\n" +
                                $"• Date: {s.FormattedDate}\n" +
                                $"• Speed: {s.FormattedAvgWpm} (Peak: {s.FormattedPeakWpm})\n" +
                                $"• Duration: {s.FormattedDuration}\n" +
                                $"• Net Words: {s.NetWords:F1}\n\n" +
                                $"⚠️ This action cannot be undone and will immediately recalculate your Top and Average metrics.";
            }
            else
            {
                confirmPrompt = $"Are you sure you want to permanently delete the {selectedItems.Count} selected typing sessions?\n\n" +
                                $"⚠️ This action cannot be undone and will immediately recalculate your Top and Average metrics.";
            }

            var result = WpfMessageBox.Show(
                confirmPrompt,
                "Confirm Permanent Deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var ids = selectedItems.Select(s => s.Id).ToList();
                int deleted = _databaseService.DeleteSessions(ids);

                LoadData();
                _typingEngine?.RefreshStatsSummary();

                WpfMessageBox.Show(
                    $"{deleted} session record{(deleted > 1 ? "s" : "")} removed successfully. Metrics updated.",
                    "Deletion Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void GridSessions_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                DeleteSelected_Click(sender, e);
                e.Handled = true;
            }
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var result = WpfMessageBox.Show(
                "Are you sure you want to clear ALL typing history? This cannot be undone.",
                "Confirm Clear All History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _databaseService.ClearHistory();
                LoadData();
                _typingEngine?.RefreshStatsSummary();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
