using OpsCoreControl.WorkingСlasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static OpsCoreControl.Log;
using static OpsCoreControl.ServiceManager;

namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private List<ServiceInfo> _allServices = new List<ServiceInfo>();

        private void _refreshServicesButton_Click(object sender, RoutedEventArgs e)
        {
            _allServices = _serviceManager.GetServices();
            FilterServices();
            Log.Add($"Служб загружено: {_allServices.Count}", LogType.Info);
        }

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
            _refreshServicesButton_Click(sender, e);
        }

        private void _stopServiceButton_Click(object sender, RoutedEventArgs e)
        {
            ServiceInfo svc = SelectedService();
            if (svc == null) return;
            _serviceManager.StopService(svc.ServiceName);
            _refreshServicesButton_Click(sender, e);
        }

        private void _restartServiceButton_Click(object sender, RoutedEventArgs e)
        {
            ServiceInfo svc = SelectedService();
            if (svc == null) return;
            _serviceManager.RestartService(svc.ServiceName);
            _refreshServicesButton_Click(sender, e);
        }

        private void _setStartupTypeButton_Click(object sender, RoutedEventArgs e)
        {
            ServiceInfo svc = SelectedService();
            if (svc == null) return;
            string tag = (_startupTypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (int.TryParse(tag, out int type))
            {
                _serviceManager.SetStartupType(svc.ServiceName, type);
                _refreshServicesButton_Click(sender, e);
            }
        }

        private async void _restartPrintSpoolerButton_Click(object sender, RoutedEventArgs e)
        {
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _serviceManager.RebootPrintSpooler("Spooler"));
        }
        private async void _rebootPC_Click(object sender, RoutedEventArgs e)
        {
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _serviceManager.rebootPC());
        }
        private async void _shutdownPC_Click(object sender, RoutedEventArgs e)
        {
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _serviceManager.shutdownPC());
        }
        private void _startCustomProcessSelectItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (_startCustomProcessSelectItemListBox.SelectedItem is SystemTool tool)
                _startCustomProcessTextBox.Text = tool.Name;
        }

        private void _startCustomProcessSelectItemListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_startCustomProcessSelectItemListBox.SelectedItem is SystemTool tool)
                _serviceManager.startCustomProcess(tool.Name);
        }

        private void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            if (_mainChatListBox.SelectedItems.Count == 0) return;

            var lines = _mainChatListBox.SelectedItems
                .Cast<object>()
                .Select(item => item.ToString());

            Clipboard.SetText(string.Join(Environment.NewLine, lines));
        }

        private async void _startCustomProcessButton_Click(object sender, RoutedEventArgs e)
        {
            await _serviceManager.startCustomProcess(_startCustomProcessTextBox.Text);
        }
    }
}