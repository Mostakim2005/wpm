using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
        private bool _isBusy;

        public HistoryWindow(DatabaseService databaseService, TypingEngine? typingEngine = null)
        {
            InitializeComponent();
            _databaseService = databaseService;
            _typingEngine = typingEngine;

            Loaded += HistoryWindow_Loaded;
        }

        private async void HistoryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            Cursor = busy ? System.Windows.Input.Cursors.Wait : null;
            BtnExportCsv.IsEnabled = !busy;
            BtnRefresh.IsEnabled = !busy;
            BtnDeleteSelected.IsEnabled = !busy;
            BtnClearHistory.IsEnabled = !busy;
        }

        private async Task LoadDataAsync()
        {
            if (_isBusy) return;
            SetBusy(true);
            try
            {
                var (summary, sessions) = await Task.Run(() =>
                {
                    var stats = _databaseService.GetStatsSummary();
                    var recent = _databaseService.GetRecentSessions(250);
                    return (stats, recent);
                });

                TxtAllTimeTop.Text = $"{summary.AllTimeTopWpm:F0} WPM";
                TxtTodayTop.Text = $"{summary.TodayTopWpm:F0} WPM";
                TxtLifetimeAvg.Text = $"{summary.OverallAvgWpm:F0} WPM";
                TxtTotalWords.Text = $"{summary.TotalWordsTyped:N0}";
                TxtTotalSessions.Text = $"{summary.TotalSessions:N0}";
                GridSessions.ItemsSource = sessions;
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Failed to load typing history: {ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;

            var sfd = new WpfSaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"WPM_History_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                SetBusy(true);
                try
                {
                    await Task.Run(() => _databaseService.ExportToCsv(sfd.FileName));
                    WpfMessageBox.Show("Typing history exported successfully!", "Export Completed", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    WpfMessageBox.Show($"Failed to export CSV: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    SetBusy(false);
                }
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;

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
                int deleted = 0;

                SetBusy(true);
                try
                {
                    deleted = await Task.Run(() =>
                    {
                        int count = _databaseService.DeleteSessions(ids);
                        _typingEngine?.RefreshStatsSummary(forceSyncLastSession: true);
                        return count;
                    });
                }
                finally
                {
                    SetBusy(false);
                }

                await LoadDataAsync();

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

        private async void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;

            var result = WpfMessageBox.Show(
                "Are you sure you want to clear ALL typing history? This cannot be undone.",
                "Confirm Clear All History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                SetBusy(true);
                try
                {
                    await Task.Run(() =>
                    {
                        _databaseService.ClearHistory();
                        _typingEngine?.RefreshStatsSummary(forceSyncLastSession: true);
                    });
                }
                finally
                {
                    SetBusy(false);
                }

                await LoadDataAsync();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
