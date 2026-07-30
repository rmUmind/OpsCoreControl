using OpsCoreControl.HelperClasses;
using OpsCoreControl.WorkingСlasses;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using static OpsCoreControl.Log;

// Часть главного окна: обработка вкладки Network —
// сетевые команды (диагностика, ipconfig, Wi-Fi, адаптеры) и сброс сети.
// Вывод команд идёт в потоковую консоль через ConsoleHelper.RunStreaming.
namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        // ── Диагностика (нужен адрес из поля) ──

        // DNS-запрос: имя ↔ IP.
        private void _nslookupButton_Click(object sender, RoutedEventArgs e)
        {
            string host = _ipAdressTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(host)) { Log.Add("Укажите адрес для nslookup.", LogType.Error); return; }
            ConsoleHelper.RunStreaming("nslookup", host);
        }

        // Непрерывный пинг (останавливается кнопкой stop output).
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

        // Сброс сети (winsock / IP / DNS) — выполняет менеджер, кнопка красится по итогу.
        private async void _ResetNetwork_Click(object sender, RoutedEventArgs e)
        {
            await _networkManager.ResetNetwork();
        }

        // Полная информация по IP (ipconfig /all).
        private void _showIpconfigButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("ipconfig", "/all");
        }

        // Останавливает текущую потоковую команду.
        private void _stopOutputButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.StopStreaming();
        }

        // Пинг адреса из поля (запускаем ping напрямую, не через cmd /c).
        private void _startPingButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("ping", $"{_ipAdressTextBox.Text} -t");
        }

        // Трассировка маршрута до адреса из поля.
        private void _startTrecertButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("tracert", _ipAdressTextBox.Text);
        }

        // Очищает консоль вывода.
        private void _clearOutputNetworkConsoleTextBox_Click(object sender, RoutedEventArgs e)
        {
            _outputNetworkConsoleTextBox.Clear();
        }

        // Очищает поле с адресом.
        private void _clearIpAddressTextBoxButton_Click(object sender, RoutedEventArgs e)
        {
            _ipAdressTextBox.Clear();
        }
    }
}