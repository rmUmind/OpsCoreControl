using OpsCoreControl.HelperClasses;
using OpsCoreControl.WorkingСlasses;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using static OpsCoreControl.Log;

namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        // ── Диагностика (нужен адрес из поля) ──
        private void _nslookupButton_Click(object sender, RoutedEventArgs e)
        {
            string host = _ipAdressTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(host)) { Log.Add("Укажите адрес для nslookup.", LogType.Error); return; }
            ConsoleHelper.RunStreaming("nslookup", host);
        }

        private void _pingContinuousButton_Click(object sender, RoutedEventArgs e)
        {
            string host = _ipAdressTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(host)) { Log.Add("Укажите адрес для ping.", LogType.Error); return; }
            ConsoleHelper.RunStreaming("ping", $"{host} -t");
        }

        // ── IP-конфигурация ──
        private void _ipReleaseButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("ipconfig", "/release");
        }

        private void _ipRenewButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("ipconfig", "/renew");
        }

        private void _ipFlushDnsButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("ipconfig", "/flushdns");
        }

        // ── Состояние сети ──
        private void _arpButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("arp", "-a");
        }

        private void _routePrintButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("route", "print");
        }

        private void _getmacButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("getmac", "/v");
        }

        // ── Wi-Fi ──
        private void _wlanInterfacesButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("netsh", "wlan show interfaces");
        }

        private void _wlanProfilesButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("netsh", "wlan show profiles");
        }

        private void _wlanDriversButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("netsh", "wlan show drivers");
        }

        // ── Адаптеры ──
        private void _showNetInterfacesButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("netsh", "interface show interface");
        }
        private async void _clearNonRedeemablePool_Click(object sender, RoutedEventArgs e)
        {
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _networkManager.ClearNonPagedPool());
        }
        private void _showIpconfigButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("ipconfig", "/all");
        }

        private void _stopOutputButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.StopStreaming();
        }

        private void _startPingButton_Click(object sender, RoutedEventArgs e)
        {

            ConsoleHelper.RunStreaming("ping", $"{_ipAdressTextBox.Text} -t");   // не "cmd /c ping", а сразу ping
        }

        private void _startTrecertButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("tracert", _ipAdressTextBox.Text);
        }

        private void _clearOutputNetworkConsoleTextBox_Click(object sender, RoutedEventArgs e)
        {
            _outputNetworkConsoleTextBox.Clear();
        }

        private void _clearipAdressTextBoxButton_Click(object sender, RoutedEventArgs e)
        {
            _ipAdressTextBox.Clear();
        }

    }
}