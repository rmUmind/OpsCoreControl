using OpsCoreControl.WorkingСlasses;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using static OpsCoreControl.Log;

// Часть главного окна: обработка вкладки Software —
// установка CryptoPro, список установленных программ с поиском и удалением,
// шаблоны путей для сохранения.
namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private List<InstalledProgram> _allPrograms = new List<InstalledProgram>();

        // Ставит CryptoPro из встроенного в сборку установщика.
        private async void _downloadCryptoPro_Click(object sender, RoutedEventArgs e)
        {
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, async () =>
            {
                return await _softwareManager.RunEmbeddedInstallerAsync(
                    "OpsCoreControl.Programs.CryptoPro-5.0.13800.exe",   // точное имя ресурса
                    "CryptoProCSP_installer.exe");                       // имя временного файла
            });
        }

        // Подставляет в поле путь к рабочему столу.
        private void _templateButtonDesktopDirectroy_Click  (object sender, RoutedEventArgs e)
        {
            _downloadDirectoryTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        // Подставляет в поле путь к папке "Загрузки".
        private void _templateButtonDownloadDirectory_Click(object sender, RoutedEventArgs e)
        {
            _downloadDirectoryTextBox.Text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }

        private void _refreshInstalledProgramsButton_Click(object sender, RoutedEventArgs e)
        {
            _allPrograms = _softwareManager.GetInstalledPrograms();
            FilterPrograms();
            Log.Add($"Найдено программ: {_allPrograms.Count}", LogType.Info);
        }

        // Фильтрует список программ при вводе.
        private void _searchProgramTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterPrograms();
        }

        private void FilterPrograms()
        {
            string filter = _searchProgramTextBox.Text.Trim();
            _installedProgramsListBox.Items.Clear();
            foreach (InstalledProgram p in _allPrograms)
            {
                if (string.IsNullOrEmpty(filter) || p.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    _installedProgramsListBox.Items.Add(p);
            }
        }

        // Удаляет выбранную программу (с подтверждением).
        private void _uninstallProgramButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(_installedProgramsListBox.SelectedItem is InstalledProgram program))
            {
                Log.Add("Выберите программу для удаления.", LogType.Error);
                return;
            }
            MessageBoxResult confirm = MessageBox.Show($"Удалить '{program.Name}'?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;
            _softwareManager.UninstallProgram(program);
        }
    }
}