using OpsCoreControl.WorkingСlasses;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using static OpsCoreControl.Log;

namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private EventLogManager _eventManager = new EventLogManager();

        private void _loadEventLogButton_Click(object sender, RoutedEventArgs e)
        {
            string logName = (_eventLogNameComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System";

            EventLogEntryType? filter = null;
            string filterTag = (_eventLogFilterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (filterTag == "Error") filter = EventLogEntryType.Error;
            else if (filterTag == "Warning") filter = EventLogEntryType.Warning;
            else if (filterTag == "Information") filter = EventLogEntryType.Information;

            int count = 50;
            int.TryParse(_eventLogCountTextBox.Text, out count);
            if (count <= 0) count = 50;

            var entries = _eventManager.GetRecentEventLog(logName, count, filter);
            _eventLogListBox.Items.Clear();
            foreach (var entry in entries) _eventLogListBox.Items.Add(entry);
            _eventLogDetailTextBox.Clear();
            Log.Add($"Загружено {entries.Count} записей из '{logName}'.", LogType.Info);
        }

        private void _eventLogListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_eventLogListBox.SelectedItem is EventLogEntryInfo entry)
            {
                _eventLogDetailTextBox.Text = entry.Message;
            }
        }
    }
}