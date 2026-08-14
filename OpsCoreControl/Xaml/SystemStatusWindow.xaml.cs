using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using System.IO;
using System.Text;

namespace OpsCoreControl
{
    public partial class SystemStatusWindow : Window
    {
        private DashboardData _latestData;
        public bool IsTopmostEnabled => _topmostCheckBox.IsChecked == true;

        public SystemStatusWindow()
        {
            InitializeComponent();
            Width = Math.Max(MinWidth, Properties.Settings.Default.DashboardWidth);
            Height = Math.Max(MinHeight, Properties.Settings.Default.DashboardHeight);
            _topmostCheckBox.IsChecked = Properties.Settings.Default.DashboardTopmost;
            Closing += (s, e) => { Properties.Settings.Default.DashboardWidth = Width; Properties.Settings.Default.DashboardHeight = Height; Properties.Settings.Default.DashboardTopmost = Topmost; Properties.Settings.Default.Save(); };
        }

        public void UpdateData(DashboardData d)
        {
            if (d == null) return;
            _latestData = d;
            if (_autoUpdateCheckBox.IsChecked == true) Render(d);
        }

        private void Render(DashboardData d)
        {
            double freeMb = Math.Max(0, d.RamTotalMb - d.RamUsedMb);
            string localIp = d.Adapters.FirstOrDefault(x => x.Status == "Подключён" && x.Ip != "—")?.Ip ?? "—";

            _pcText.Text = $"ПК: {d.System.PcName}";
            _userText.Text = $"Пользователь: {d.System.UserName}";
            _uptimeText.Text = $"Uptime: {d.System.Uptime}";
            _processText.Text = $"Процессов: {d.System.ProcessCount}";
            _batteryText.Text = $"Батарея: {d.System.Battery}";
            _osText.Text = $"Windows: {d.System.OsVersion} ({d.System.Architecture})";
            _cpuText.Text = $"CPU: {d.CpuPercent:F0}%";
            _ramUsedText.Text = $"Использовано: {d.RamUsedMb / 1024.0:F1} ГБ";
            _ramFreeText.Text = $"Свободно: {freeMb / 1024.0:F1} ГБ";
            _ramTotalText.Text = $"Всего: {d.RamTotalMb / 1024.0:F1} ГБ";
            _ramPercentText.Text = $"Загрузка: {d.RamPercent:F0}%";
            _virtualMemoryText.Text = $"Виртуальная память: {d.VramPercent:F0}%";
            _diskActivityText.Text = $"Чтение: {d.DiskReadMbSec:F1} МБ/с  •  Запись: {d.DiskWriteMbSec:F1} МБ/с";
            _wifiText.Text = $"Wi-Fi: {(d.Wifi.Connected ? d.Wifi.Ssid + " (сигнал " + d.Wifi.SignalPercent + "%)" : "нет подключения")}";
            _localIpText.Text = $"Локальный IP: {localIp}";
            _publicIpText.Text = $"Публичный IP: {d.System.PublicIp}";
            _updatedText.Text = "Обновлено: " + DateTime.Now.ToString("HH:mm:ss");

            _disksList.ItemsSource = d.Disks.Select(x =>
                $"{x.Letter} [{x.Type}]" + (string.IsNullOrEmpty(x.Label) ? "" : $" \"{x.Label}\"") +
                $" — всего {x.TotalGb:F1} ГБ, свободно {x.FreeGb:F1} ГБ ({x.FreePercent:F0}%)" +
                (string.IsNullOrEmpty(x.Unc) ? "" : $"  •  {x.Unc}")).ToList();
            _pageFilesList.ItemsSource = d.PageFiles.Count == 0
                ? new System.Collections.Generic.List<string> { "Файл подкачки не найден или сведения недоступны" }
                : d.PageFiles.Select(x => $"{x.Path} — выделено {x.AllocatedMb} МБ, используется {x.CurrentUsageMb} МБ, пик {x.PeakUsageMb} МБ").ToList();
            _adaptersList.ItemsSource = d.Adapters.Select(x => $"{x.Name} [{x.Type}] {x.Status}  •  IP: {x.Ip}  •  {x.SpeedMbps} Мбит/с").ToList();
            _usbList.ItemsSource = d.Usb.Select(x => string.IsNullOrEmpty(x.Description) ? x.Name : $"{x.Name} — {x.Description}").ToList();
        }

        private void _autoUpdateChanged(object sender, RoutedEventArgs e)
        {
            if (_autoUpdateCheckBox.IsChecked == true && _latestData != null) Render(_latestData);
        }

        private void _topmostChanged(object sender, RoutedEventArgs e)
        {
            Topmost = _topmostCheckBox.IsChecked == true;
            Properties.Settings.Default.DashboardTopmost = Topmost;
        }

        private void _refreshNow_Click(object sender, RoutedEventArgs e)
        {
            if (_latestData != null) Render(_latestData);
        }

        private string BuildSummary()
        {
            if (_latestData == null) return "Данные дашборда ещё не получены.";
            DashboardData d = _latestData;
            double freeMb = Math.Max(0, d.RamTotalMb - d.RamUsedMb);
            var sb = new StringBuilder();
            sb.AppendLine($"OpsCoreControl — диагностический отчёт ({DateTime.Now:yyyy-MM-dd HH:mm:ss})");
            sb.AppendLine($"ПК: {d.System.PcName}; пользователь: {d.System.UserName}; uptime: {d.System.Uptime}");
            sb.AppendLine($"Windows: {d.System.OsVersion} ({d.System.Architecture}); процессов: {d.System.ProcessCount}; батарея: {d.System.Battery}");
            sb.AppendLine($"CPU: {d.CpuPercent:F0}%");
            sb.AppendLine($"RAM: {d.RamUsedMb / 1024:F1} ГБ использовано, {freeMb / 1024:F1} ГБ свободно, {d.RamTotalMb / 1024:F1} ГБ всего ({d.RamPercent:F0}%)");
            sb.AppendLine($"Виртуальная память: {d.VramPercent:F0}%");
            sb.AppendLine($"Диск: чтение {d.DiskReadMbSec:F1} МБ/с, запись {d.DiskWriteMbSec:F1} МБ/с");
            sb.AppendLine("Диски:");
            foreach (var x in d.Disks) sb.AppendLine($"  {x.Letter} [{x.Type}] всего {x.TotalGb:F1} ГБ, свободно {x.FreeGb:F1} ГБ ({x.FreePercent:F0}%)");
            sb.AppendLine("Файл подкачки:");
            foreach (var x in d.PageFiles) sb.AppendLine($"  {x.Path}: выделено {x.AllocatedMb} МБ, используется {x.CurrentUsageMb} МБ, пик {x.PeakUsageMb} МБ");
            sb.AppendLine($"Wi-Fi: {(d.Wifi.Connected ? d.Wifi.Ssid + " " + d.Wifi.SignalPercent + "%" : "нет подключения")}; публичный IP: {d.System.PublicIp}");
            sb.AppendLine("Адаптеры:");
            foreach (var x in d.Adapters) sb.AppendLine($"  {x.Name} [{x.Type}] {x.Status}, IP {x.Ip}, {x.SpeedMbps} Мбит/с");
            sb.AppendLine("USB:");
            foreach (var x in d.Usb) sb.AppendLine($"  {x.Name}" + (string.IsNullOrEmpty(x.Description) ? "" : $" — {x.Description}"));
            sb.AppendLine($"Лог приложения: {Log.CurrentLogFile}");
            return sb.ToString();
        }

        private void _copySummary_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(BuildSummary());
            Log.Add("Диагностическая сводка скопирована.", Log.LogType.Success);
        }

        private void _saveReport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Filter = "Текстовый файл (*.txt)|*.txt", FileName = $"OpsCoreControl_{DateTime.Now:yyyyMMdd_HHmm}.txt" };
            if (dialog.ShowDialog(this) != true) return;
            File.WriteAllText(dialog.FileName, BuildSummary(), Encoding.UTF8);
            Log.Add($"Диагностический отчёт сохранён: {dialog.FileName}", Log.LogType.Success);
        }

        // Один щелчок колеса прокручивает небольшой фиксированный шаг вместо целой страницы.
        private void _listPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!(sender is DependencyObject source)) return;
            ScrollViewer viewer = FindChild<ScrollViewer>(source);
            if (viewer == null) return;

            double step = 28;
            double target = viewer.VerticalOffset - Math.Sign(e.Delta) * step;
            viewer.ScrollToVerticalOffset(Math.Max(0, Math.Min(viewer.ScrollableHeight, target)));
            e.Handled = true;
        }

        private static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match) return match;
                T nested = FindChild<T>(child);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}

