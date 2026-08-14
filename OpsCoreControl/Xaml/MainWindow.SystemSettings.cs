using OpsCoreControl.HelperClasses;
using System.Threading.Tasks;
using System.Windows;
using static OpsCoreControl.Log;

// Часть главного окна: обработка вкладки System settings —
// восстановление системы (SFC/DISM), блокировка экрана, яркость, файл подкачки.
namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        // ── Восстановление системы (вывод идёт в консоль вкладки Network) ──

        private void _sfcScannowButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("sfc", "/scannow");
        }

        private void _dismCheckHealthButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("DISM", "/Online /Cleanup-Image /CheckHealth");
        }

        private void _dismScanHealthButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("DISM", "/Online /Cleanup-Image /ScanHealth");
        }

        private void _dismRestoreHealthButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("DISM", "/Online /Cleanup-Image /RestoreHealth");
        }

        // Ставит таймаут блокировки экрана (в минутах); при некорректном вводе — 10.
        private async void _setScreenLockTimerButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(_timeToScreenLockTimerTextBox.Text, out int minutes) || minutes <= 0 || minutes > 1440)
            { Log.Add("Введите время блокировки от 1 до 1440 минут.", LogType.Error); _timeToScreenLockTimerTextBox.Focus(); return; }
            _systemSettingsManager.SetScreenLockTimeout(minutes);
        }

        // Ставит яркость монитора (0-100); при некорректном вводе — 100.
        private async void _setMonitorBrightnessButton_Click(object sender, RoutedEventArgs e)
        {
            if (!uint.TryParse(_setMonitorBrightnessTextBox.Text, out uint brightness) || brightness > 100)
            { Log.Add("Введите яркость от 0 до 100.", LogType.Error); _setMonitorBrightnessTextBox.Focus(); return; }
            _monitorController.Set(brightness);
        }

        // Наполняет список дисков для выбора под файл подкачки.
        private async void _setVirtualRamFindDisks_Click(object sender, RoutedEventArgs e)
        {
            _setVirtualRamSelectedDiskListBox.Items.Clear();
            var disks = await _fileSystemManager.GetDiskInfo();
            foreach (var disk in disks)
            {
                _setVirtualRamSelectedDiskListBox.Items.Add(disk.Name);
            }
            await RefreshPageFileInfoAsync();
        }

        // Устанавливает размер файла подкачки на выбранном диске.
        private async void _setVirtualRamButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedDisk = _setVirtualRamSelectedDiskListBox.SelectedItem?.ToString();
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

            if (MessageBox.Show($"Установить файл подкачки на {selectedDisk}: {minMB}–{maxMB} МБ?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            await _fileSystemManager.SetPageFileSize(selectedDisk, minMB, maxMB);

            // Обновляем список после установки.
            await RefreshPageFileInfoAsync();
        }

        // Перечитывает сведения о файле подкачки и перерисовывает список.
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

        // Удаляет файл подкачки с выбранного диска.
        private async void _clearPageFileButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedDisk = _setVirtualRamSelectedDiskListBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedDisk))
            {
                Log.Add("Диск не выбран.", LogType.Error);
                return;
            }

            if (MessageBox.Show($"Удалить файл подкачки с диска {selectedDisk}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            await _fileSystemManager.ClearPageFile(selectedDisk);

            await RefreshPageFileInfoAsync();
        }

        // Переводит файл подкачки на выбранном диске в режим "по выбору системы".
        private async void _setPageFileAutoButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedDisk = _setVirtualRamSelectedDiskListBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedDisk))
            {
                Log.Add("Диск не выбран.", LogType.Error);
                return;
            }

            if (MessageBox.Show($"Включить системное управление файлом подкачки на диске {selectedDisk}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await _fileSystemManager.SetPageFileAuto(selectedDisk);

            await RefreshPageFileInfoAsync();
        }

        private void _pageFileDisk_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            bool selected = _setVirtualRamSelectedDiskListBox.SelectedItem != null;
            _setVirtualRamButton.IsEnabled = selected;
            _clearPageFileButton.IsEnabled = selected;
            _setPageFileAutoButton.IsEnabled = selected;
        }
    }
}
