using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OpsCoreControl.WorkingСlasses;
using static OpsCoreControl.Log;

namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private async void _cleanDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _fileSystemManager.CleanDownloadFolder());
        }
        private async void _cleadTempFolderButton_Click(object sender, RoutedEventArgs e)
        {
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _fileSystemManager.CleanTempFolder());
        }
        private async void _mapLogicalDiskButton_Click(object sender, RoutedEventArgs e)
        {
            string path = _setNetworkPathTextBox.Text;            // 10.19.120.10\mintrans
            string letter = _setNameForNewLogicalDiskTextBox.Text; // пусто → авто
            await ButtonHelper.ExecuteWithColorAsync((Button)sender,
                () => _networkManager.MapNetworkDrive(letter, path));
            RefreshLogicalDisks();
        }

        private async void _openNetworkPathButton_Click(object sender, RoutedEventArgs e)
        {
            string path = _setNetworkPathTextBox.Text;
            await ButtonHelper.ExecuteWithColorAsync((Button)sender,
                () => _fileSystemManager.OpenNetworkPath(path));
        }

        private async void _deleteCurrentLogicaldiskButton_Click(object sender, RoutedEventArgs e)
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
            string letter = selected.Split(' ')[0];
            await ButtonHelper.ExecuteWithColorAsync((Button)sender,
                () => _networkManager.UnmapNetworkDrive(letter));
            RefreshLogicalDisks();
        }

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
            string letter = selected.Split(' ')[0];
            await ButtonHelper.ExecuteWithColorAsync((Button)sender,
                () => _networkManager.RenameLogicalDisk(letter, newName));
            RefreshLogicalDisks();
        }
        private void _checkSmartButton_Click(object sender, RoutedEventArgs e)
        {
            _diskHealthListBox.Items.Clear();
            foreach (var disk in _fileSystemManager.GetDiskHealth())
            {
                _diskHealthListBox.Items.Add(disk);
            }
            Log.Add("Проверка SMART выполнена.", LogType.Info);
        }
        private void _findCurrentLogicalDiskButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshLogicalDisks();
        }

        private void RefreshLogicalDisks()
        {
            _currentLogicalDiskListBox.Items.Clear();
            foreach (var disk in _networkManager.GetLogicalDrives())
            {
                _currentLogicalDiskListBox.Items.Add(disk);
            }
        }
    }
}