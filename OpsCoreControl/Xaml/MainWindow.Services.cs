using OpsCoreControl.WorkingСlasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using static OpsCoreControl.Log;
using static OpsCoreControl.ServiceManager;

// Часть главного окна: обработка вкладки Службы —
// управление службами (старт/стоп/рестарт/тип запуска), перезагрузка и выключение ПК,
// запуск оснасток и процессов, копирование выбранных строк лога.
namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private List<ServiceInfo> _allServices = new List<ServiceInfo>();

        // Перечитывает службы и применяет текущий фильтр.
        private void RefreshServices()
        {
            _allServices = _serviceManager.GetServices();
            FilterServices();
            Log.Add($"Служб загружено: {_allServices.Count}", LogType.Info);
        }

        private void _refreshServicesButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshServices();
        }

        // Фильтрует список служб при вводе.
        private void _searchServiceTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterServices();
        }

        private void FilterServices()
        {
            string filter = _searchServiceTextBox.Text.Trim();
            _servicesListBox.Items.Clear();
            foreach (ServiceInfo s in _allServices)
            {
                if (string.IsNullOrEmpty(filter)
                    || s.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || s.ServiceName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _servicesListBox.Items.Add(s);
                }
            }
        }

        // Возвращает выбранную службу или null (с подсказкой в лог).
        private ServiceInfo SelectedService()
        {
            if (_servicesListBox.SelectedItem is ServiceInfo svc) return svc;
            Log.Add("Выберите службу.", LogType.Error);
            return null;
        }

        private void _startServiceButton_Click(object sender, RoutedEventArgs e)
        {
            ServiceInfo svc = SelectedService();
            if (svc == null) return;
            _serviceManager.StartService(svc.ServiceName);
            RefreshServices();
        }

        private void _stopServiceButton_Click(object sender, RoutedEventArgs e)
        {
            ServiceInfo svc = SelectedService();
            if (svc == null) return;
            _serviceManager.StopService(svc.ServiceName);
            RefreshServices();
        }

        private void _restartServiceButton_Click(object sender, RoutedEventArgs e)
        {
            ServiceInfo svc = SelectedService();
            if (svc == null) return;
            _serviceManager.RestartService(svc.ServiceName);
            RefreshServices();
        }

        // Меняет тип запуска выбранной службы (Automatic / Manual / Disabled).
        private void _setStartupTypeButton_Click(object sender, RoutedEventArgs e)
        {
            ServiceInfo svc = SelectedService();
            if (svc == null) return;
            string tag = (_startupTypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (int.TryParse(tag, out int type))
            {
                _serviceManager.SetStartupType(svc.ServiceName, type);
                RefreshServices();
            }
        }

        // Перезапуск службы печати.
        private async void _restartPrintSpoolerButton_Click(object sender, RoutedEventArgs e)
        {
            await _serviceManager.RebootPrintSpooler("Spooler");
        }

        // Перезагрузка ПК (с подтверждением).
        private async void _rebootPC_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult confirm = MessageBox.Show("Перезагрузить компьютер сейчас?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;
            await _serviceManager.RebootPC();
        }

        // Выключение ПК (с подтверждением).
        private async void _shutdownPC_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult confirm = MessageBox.Show("Выключить компьютер сейчас? Несохранённые данные будут потеряны.", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;
            await _serviceManager.ShutdownPC();
        }

        // Кладёт имя выбранной оснастки в поле ввода.
        private void _startCustomProcessSelectItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (_startCustomProcessSelectItemListBox.SelectedItem is SystemTool tool)
                _startCustomProcessTextBox.Text = tool.Name;
        }

        // Двойной клик по оснастке запускает её сразу.
        private void _startCustomProcessSelectItemListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_startCustomProcessSelectItemListBox.SelectedItem is SystemTool tool)
                _serviceManager.StartCustomProcess(tool.Name);
        }

        // Копирует выбранные строки лога (контекстное меню чата).
        private void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            if (_mainChatListBox.SelectedItems.Count == 0) return;

            var lines = _mainChatListBox.SelectedItems
                .Cast<object>()
                .Select(item => item.ToString());

            Clipboard.SetText(string.Join(Environment.NewLine, lines));
        }

        // Запускает процесс из поля ввода.
        private async void _startCustomProcessButton_Click(object sender, RoutedEventArgs e)
        {
            await _serviceManager.StartCustomProcess(_startCustomProcessTextBox.Text);
        }
    }
}