using System.Windows;
using static OpsCoreControl.Log;

// Часть главного окна: обработка вкладки Folder's work —
// очистка папок, сетевые диски (подключение/отключение/переименование), SMART и список дисков.
namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        // Очищает папку «Загрузки».
        private async void _cleanDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Удалить содержимое папки «Загрузки»?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            await _fileSystemManager.CleanDownloadFolder();
        }

        // Очищает временные папки.
        private async void _cleanTempFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Очистить временные папки? Используемые файлы будут пропущены.", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            await _fileSystemManager.CleanTempFolder();
        }

        // Подключает сетевую шару как диск. Буква необязательна: если пусто — подберётся автоматически.
        private async void _mapLogicalDiskButton_Click(object sender, RoutedEventArgs e)
        {
            string path = _setNetworkPathTextBox.Text;            // например, \\server\share
            string letter = _setNameForNewLogicalDiskTextBox.Text; // пусто → авто
            await _networkManager.MapNetworkDrive(letter, path);
            RefreshLogicalDisks();
        }

        // Открывает сетевую папку в Проводнике.
        private async void _openNetworkPathButton_Click(object sender, RoutedEventArgs e)
        {
            string path = _setNetworkPathTextBox.Text;
            await _fileSystemManager.OpenNetworkPath(path);
        }

        // Отключает выбранный сетевой диск.
        private async void _deleteCurrentLogicalDiskButton_Click(object sender, RoutedEventArgs e)
        {
            string selected = _currentLogicalDiskListBox.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(selected))
            {
                Log.Add("Диск не выбран.", LogType.Error);
                return;
            }
            if (!selected.Contains("[Сетевой]"))
            {
                Log.Add("Отключить можно только сетевой диск.", LogType.Error);
                return;
            }
            string letter = selected.Split(' ')[0]; // первый токен — буква диска ("Z:")
            await _networkManager.UnmapNetworkDrive(letter);
            RefreshLogicalDisks();
        }

        // Меняет метку выбранного диска.
        private async void _setNewNameLogicalDiskButton_Click(object sender, RoutedEventArgs e)
        {
            string selected = _currentLogicalDiskListBox.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(selected))
            {
                Log.Add("Диск не выбран.", LogType.Error);
                return;
            }
            string newName = _nameForOldLogicalDiskTextBox.Text;
            if (string.IsNullOrWhiteSpace(newName))
            {
                Log.Add("Не указано новое имя.", LogType.Error);
                return;
            }
            string letter = selected.Split(' ')[0]; // первый токен — буква диска
            await _networkManager.RenameLogicalDisk(letter, newName);
            RefreshLogicalDisks();
        }

        // Проверяет SMART-состояние дисков и выводит результат в список.
        private void _checkSmartButton_Click(object sender, RoutedEventArgs e)
        {
            _diskHealthListBox.Items.Clear();
            var disks = _fileSystemManager.GetDiskHealth();
            foreach (var disk in disks)
            {
                _diskHealthListBox.Items.Add(disk);
            }
            Log.Add($"Проверка SMART выполнена, дисков: {disks.Count}.", LogType.Info);
        }

        // Обновляет список логических дисков.
        private void _findCurrentLogicalDiskButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshLogicalDisks();
        }

        // Перечитывает список дисков и перерисовывает его.
        private void RefreshLogicalDisks()
        {
            _currentLogicalDiskListBox.Items.Clear();
            foreach (var disk in _networkManager.GetLogicalDrives())
            {
                _currentLogicalDiskListBox.Items.Add(disk);
            }
        }

        private void _currentLogicalDiskListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            bool selected = _currentLogicalDiskListBox.SelectedItem != null;
            _deleteCurrentLogicaldiskButton.IsEnabled = selected;
            _setNewNameLogicalDiskButton.IsEnabled = selected;
        }
    }
}
