using OpsCoreControl.WorkingСlasses;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using static OpsCoreControl.Log;

// Часть главного окна: обработка вкладки Программы —
// установка CryptoPro и плагинов, список установленных программ с поиском и удалением.
namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private List<InstalledProgram> _allPrograms = new List<InstalledProgram>();

        // Перечитывает установленные программы и применяет фильтр.
        private void RefreshPrograms()
        {
            _allPrograms = _softwareManager.GetInstalledPrograms();
            FilterPrograms();
            Log.Add($"Найдено программ: {_allPrograms.Count}", LogType.Info);
        }

        private void _refreshInstalledProgramsButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshPrograms();
        }

        // Ставит CryptoPro CSP из встроенного в сборку установщика.
        private async void _downloadCryptoPro_Click(object sender, RoutedEventArgs e)
        {
            await _softwareManager.RunEmbeddedInstallerAsync(
                "OpsCoreControl.Programs.CryptoPro.exe",   // точное имя ресурса
                "CryptoProCSP_installer.exe");                       // имя временного файла
        }

        // Ставит CryptoPro Plugin из встроенного установщика.
        private async void _installCryptoProPlugin_Click(object sender, RoutedEventArgs e)
        {
            await _softwareManager.RunEmbeddedInstallerAsync(
                "OpsCoreControl.Programs.CryptoProPlugin.exe",   // точное имя ресурса 
                "CryptoProPlugin_installer.exe");                // имя временного файла
        }

        // Ставит Cisco AnyConnect из встроенного установщика.
        private async void _installCiscoAnyConnect_Click(object sender, RoutedEventArgs e)
        {
            await _softwareManager.RunEmbeddedInstallerAsync(
                "OpsCoreControl.Programs.CiscoAnyConnect.msi",   // точное имя ресурса 
                "CiscoAnyConnect_installer.exe");                // имя временного файла
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

        // Извлекает и запускает встроенный установщик Assistant.
        private async void _installAssistant_Click(object sender, RoutedEventArgs e)
        {
            await _softwareManager.RunEmbeddedInstallerAsync(
                "OpsCoreControl.Programs.assistant.exe",
                "assistant_installer.exe");
        }

        private void _installedProgramsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _uninstallProgramButton.IsEnabled = _installedProgramsListBox.SelectedItem != null;
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
