using OpsCoreControl.HelperClasses;
using System;
using System.Windows;
using static OpsCoreControl.Log;

// Часть главного окна: обработка вкладки Network —
// сетевые команды (диагностика, ipconfig, Wi-Fi, адаптеры) и сброс сети.
// Вывод команд идёт в потоковую консоль через ConsoleHelper.RunStreaming.
namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private bool TryGetNetworkTarget(string operation, out string host)
        {
            host = _ipAddressTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(host) || host.Length > 253 || host[0] == '-'
                || host.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '"' }) >= 0)
            {
                Log.Add($"Укажите корректный IP-адрес или имя узла для {operation}.", LogType.Error);
                return false;
            }
            return true;
        }

        // ── Диагностика (нужен адрес из поля) ──

        // DNS-запрос: имя ↔ IP.
        private void _nslookupButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetNetworkTarget("nslookup", out string host)) return;
            ConsoleHelper.RunStreaming("nslookup", host);
        }

        // Непрерывный пинг (останавливается кнопкой stop output).
        private void _pingContinuousButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetNetworkTarget("ping", out string host)) return;
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
        private async void _resetNetwork_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Сбросить сетевые настройки? Подключение к сети может временно пропасть.", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
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
            if (!TryGetNetworkTarget("ping", out string host)) return;
            ConsoleHelper.RunStreaming("ping", host);
        }

        // Трассировка маршрута до адреса из поля.
        private void _startTracertButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetNetworkTarget("tracert", out string host)) return;
            ConsoleHelper.RunStreaming("tracert", host);
        }

        // Очищает консоль вывода.
        private void _clearOutputNetworkConsoleTextBox_Click(object sender, RoutedEventArgs e)
        {
            _outputNetworkConsoleTextBox.Clear();
        }

        // Очищает поле с адресом.
        private void _clearIpAddressTextBoxButton_Click(object sender, RoutedEventArgs e)
        {
            _ipAddressTextBox.Clear();
        }
    }
}

