using OpsCoreControl.WorkingСlasses;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static OpsCoreControl.Log;

namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private async void _setScreenLockTimerButton_Click(object sender, RoutedEventArgs e)
        {
            int mitutes;
            if (!int.TryParse(_timeToScreenLockTimerTextBox.Text, out mitutes)) { mitutes = 10; }
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => Task.Run(() => _systemSettingsManager.SetScreenLockTimeout(Convert.ToInt32(mitutes))));
        }
        private async void _setMonitorBrightnessButton_Click(object sender, RoutedEventArgs e)
        {
            uint brightness;
            if (!uint.TryParse(_setMonitorBrightnessTextBox.Text, out  brightness)) { brightness = 100; };
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => Task.Run(() => _monitorController.Set(brightness)));
        }
        private async void _setVirtualRamFindDisks_Click(object sender, RoutedEventArgs e)
        {
            var disks = await _fileSystemManager.GetDiskInfo();
            foreach (var disk in disks)
            {
                _setVirualRamSelectedDiskListBox.Items.Add(disk.Name);
            }
            await RefreshPageFileInfoAsync();
        }

        private async void _setVirtualRamButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedDisk = _setVirualRamSelectedDiskListBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedDisk))
            {
                Log.Add("Диск не выбран.", LogType.Error);
                return;
            }

            if (!int.TryParse(_minimumVirtualRamTextBox.Text, out int minMB) ||
                !int.TryParse(_maximumVirtualRamTextBox.Text, out int maxMB))
            {
                Log.Add("Некорректные значения размера.", LogType.Error);
                return;
            }

            if (minMB <= 0 || maxMB <= 0 || minMB > maxMB)
            {
                Log.Add("Проверьте: min > 0, max > 0, min ≤ max.", LogType.Error);
                return;
            }

            await ButtonHelper.ExecuteWithColorAsync((Button)sender,
                () => _fileSystemManager.SetPageFileSize(selectedDisk, minMB, maxMB));

            // Обновляем список после установки
            await RefreshPageFileInfoAsync();
        }
        private async Task RefreshPageFileInfoAsync()
        {
            _pageFileInfoListBox.Items.Clear();
            var info = await _fileSystemManager.GetPageFileInfo();
            foreach (string line in info)
            {
                _pageFileInfoListBox.Items.Add(line);
            }
        }

        private async void _refreshPageFileInfoButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshPageFileInfoAsync();
        }

        private async void _clearPageFileButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedDisk = _setVirualRamSelectedDiskListBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedDisk))
            {
                Log.Add("Диск не выбран.", LogType.Error);
                return;
            }

            await ButtonHelper.ExecuteWithColorAsync((Button)sender,
                () => _fileSystemManager.ClearPageFile(selectedDisk));

            await RefreshPageFileInfoAsync();
        }

        private async void _setPageFileAutoButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedDisk = _setVirualRamSelectedDiskListBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedDisk))
            {
                Log.Add("Диск не выбран.", LogType.Error);
                return;
            }

            await ButtonHelper.ExecuteWithColorAsync((Button)sender,
                () => _fileSystemManager.SetPageFileAuto(selectedDisk));

            await RefreshPageFileInfoAsync();
        }
    }
}