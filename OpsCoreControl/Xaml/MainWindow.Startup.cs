using OpsCoreControl.WorkingСlasses;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using static OpsCoreControl.Log;

// Часть главного окна: обработка вкладки Startup —
// список программ автозагрузки с поиском, включение и выключение.
namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private List<StartupProgram> _allStartup = new List<StartupProgram>();

        // Перечитывает автозагрузку и применяет текущий фильтр.
        private void RefreshStartup()
        {
            _allStartup = _startupManager.GetStartupPrograms();
            FilterStartup();
        }

        private void _refreshStartupButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshStartup();
            Log.Add($"Автозагрузок: {_allStartup.Count}", LogType.Info);
        }

        // Фильтрует список автозагрузки при вводе.
        private void _searchStartupTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterStartup();
        }

        private void FilterStartup()
        {
            string filter = _searchStartupTextBox.Text.Trim();
            _startupListBox.Items.Clear();
            foreach (StartupProgram s in _allStartup)
            {
                if (string.IsNullOrEmpty(filter) || s.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    _startupListBox.Items.Add(s);
            }
        }

        private void _startupListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool selected = _startupListBox.SelectedItem != null;
            _enableStartupButton.IsEnabled = selected;
            _disableStartupButton.IsEnabled = selected;
        }

        private void _enableStartupButton_Click(object sender, RoutedEventArgs e) => SetStartupState(true);
        private void _disableStartupButton_Click(object sender, RoutedEventArgs e) => SetStartupState(false);

        // Включает или выключает выбранную программу в автозагрузке.
        private void SetStartupState(bool enabled)
        {
            if (!(_startupListBox.SelectedItem is StartupProgram program))
            {
                Log.Add("Выберите программу из списка.", LogType.Error);
                return;
            }
            _startupManager.SetEnabled(program, enabled);
            RefreshStartup();
        }
    }
}
