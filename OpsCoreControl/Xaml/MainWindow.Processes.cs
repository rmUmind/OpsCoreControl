using OpsCoreControl.WorkingСlasses;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using static OpsCoreControl.Log;

namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private List<ProcessInfo> _allProcesses = new List<ProcessInfo>();

        private void RefreshProcesses()
        {
            _allProcesses = _processManager.GetProcesses();
            FilterProcesses();
        }

        private void _refreshProcessesButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshProcesses();
            Log.Add($"Процессов: {_allProcesses.Count}", LogType.Info);
        }

        private void _searchProcessTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterProcesses();
        }

        private void FilterProcesses()
        {
            string filter = _searchProcessTextBox.Text.Trim();
            _processesListBox.Items.Clear();
            foreach (ProcessInfo p in _allProcesses)
            {
                if (string.IsNullOrEmpty(filter)
                    || p.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || p.Pid.ToString().Contains(filter))
                {
                    _processesListBox.Items.Add(p);
                }
            }
        }

        private void _killProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(_processesListBox.SelectedItem is ProcessInfo proc))
            {
                Log.Add("Выберите процесс.", LogType.Error);
                return;
            }
            MessageBoxResult confirm = MessageBox.Show($"Завершить '{proc.Name}' (PID {proc.Pid})?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;
            _processManager.KillProcess(proc.Pid);
            RefreshProcesses();
        }
    }
}