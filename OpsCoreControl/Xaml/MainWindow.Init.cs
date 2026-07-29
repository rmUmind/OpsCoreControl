using OpsCoreControl.HelperClasses;
using OpsCoreControl.WorkingСlasses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using static OpsCoreControl.Log;

namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private DashBoard _dashBoard;
        private FileSystemManager _fileSystemManager;
        private NetworkManager _networkManager;
        private ServiceManager _serviceManager;
        private SoftwareManager _softwareManager;
        private UserProfileManager _userProfileManager;
        private SystemSettingsManager _systemSettingsManager;
        private PhysicalMonitorBrightnessController _monitorController;

        // анти-мерцание: перерисовываем списки только при изменении
        private List<string> _lastDisks = new List<string>();
        private List<string> _lastAdapters = new List<string>();
        private List<string> _lastUsb = new List<string>();

        public MainWindow()
        {
            InitializeComponent();

            _dashBoard = new DashBoard();
            _dashBoard.Updated += RenderDashboard;

            // Network console
            ConsoleHelper.OnOutputConsoleLine += line =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _outputNetworkConsoleTextBox.AppendText(line + Environment.NewLine);
                }));
            };

            this.Closed += (s, e) => _dashBoard.Dispose();

            _fileSystemManager = new FileSystemManager();
            _networkManager = new NetworkManager();
            _networkManager.EnsureLinkedConnectionsEnabled();
            _serviceManager = new ServiceManager();
            _softwareManager = new SoftwareManager();
            _userProfileManager = new UserProfileManager();
            _systemSettingsManager = new SystemSettingsManager();
            _monitorController = new PhysicalMonitorBrightnessController();

            List<string> proceses = new List<string>() { "services.msc", "regedit", "eventvwr.msc", "appwiz.cpl" };
            foreach (string procese in proceses)
            {
                _startCustomProcessSelectItemListBox.Items.Add(procese);
            }

            // Chat
            Log.LogMessage += message => Dispatcher.Invoke(() => _mainChatListBox.Items.Add(message));
            Log.LogError += message => Dispatcher.Invoke(() => _mainChatListBox.Items.Add(message));
            Log.LogInfo += message => Dispatcher.Invoke(() => _mainChatListBox.Items.Add(message));
            Log.LogSuccess += message => Dispatcher.Invoke(() => _mainChatListBox.Items.Add(message));
            Log.LogProfile += message =>
            {
                HashSet<string> ignoreList = new HashSet<string>() {"C:\\Users\\Default", "C:\\Users\\All Users", "C:\\Users\\Default User",
                    "C:\\Users\\DefaultAppPool", "C:\\Users\\Все пользователи", "C:\\Users\\Public"};
                if (!ignoreList.Contains(message))
                {
                    Dispatcher.Invoke(() => _usersProfilesListBox.Items.Add(message));
                }
            };
            Log.LogDebug += message => Dispatcher.Invoke(() => _mainChatListBox.Items.Add(message));
        }

        private void RenderDashboard(DashboardData d)
        {
            // Краткая зона
            _dashPcUserText.Text = $"ПК: {d.System.PcName}  •  Пользователь: {d.System.UserName}  •  Uptime: {d.System.Uptime}";

            string wifi = d.Wifi.Connected ? $"WiFi: {d.Wifi.Ssid} ({d.Wifi.SignalPercent}%)" : "WiFi: нет";
            AdapterSnapshot active = d.Adapters.FirstOrDefault(a => a.Status == "Up" && a.Ip != "—");
            string ip = active != null ? active.Ip : "—";
            string link = d.Adapters.Any(a => a.Status == "Up") ? "Up" : "Down";
            _dashNetworkText.Text = $"{wifi}  •  IP: {ip}  •  Линк: {link}";

            _dashPerfText.Text = $"CPU: {d.CpuPercent:F0}%  •  RAM: {d.RamPercent:F0}% ({d.RamUsedMb:F0}/{d.RamTotalMb:F0} МБ)  •  VRAM: {d.VramPercent:F0}%";

            var diskLines = new List<string>();
            foreach (DiskSnapshot disk in d.Disks)
            {
                string label = string.IsNullOrEmpty(disk.Label) ? "" : $" \"{disk.Label}\"";
                string unc = (disk.Type == "Сетевой" && !string.IsNullOrEmpty(disk.Unc)) ? $"  {disk.Unc}" : "";
                diskLines.Add($"{disk.Letter} [{disk.Type}]{label} — {disk.FreeGb:F0}/{disk.TotalGb:F0} ГБ ({disk.FreePercent:F0}%){unc}");
            }
            UpdateListBox(_dashDisksListBox, diskLines, ref _lastDisks);

            // Расширенная зона
            _extSystemText.Text = $"Батарея: {d.System.Battery}  •  Процессов: {d.System.ProcessCount}  •  Публичный IP: {d.System.PublicIp}";
            _extDiskActivityText.Text = $"Диск: чтение {d.DiskReadMbSec:F1} МБ/с, запись {d.DiskWriteMbSec:F1} МБ/с";

            List<string> adapterLines = d.Adapters
                .Select(a => $"{a.Name} [{a.Type}] {a.Status}  IP: {a.Ip}  {a.SpeedMbps} Мбит/с")
                .ToList();
            UpdateListBox(_extAdaptersListBox, adapterLines, ref _lastAdapters);

            List<string> usbLines = d.Usb
                .Select(u => string.IsNullOrEmpty(u.Description) ? u.Name : $"{u.Name} — {u.Description}")
                .ToList();
            UpdateListBox(_extUsbListBox, usbLines, ref _lastUsb);
        }

        private void UpdateListBox(ListBox lb, List<string> items, ref List<string> cache)
        {
            if (cache.SequenceEqual(items)) return;   // не изменилось — не дёргаем список
            cache = items;
            lb.Items.Clear();
            foreach (string it in items) lb.Items.Add(it);
        }

        private void _dashboardToggleButton_Click(object sender, RoutedEventArgs e)
        {
            bool expand = _dashboardToggleButton.IsChecked == true;
            _extendedDashboardPanel.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
        }
        // ВАЖНО: если репозиторий называется не OpsCoreControl — поправь имя в URL
        private const string GitHubNewIssueUrl = "https://github.com/rmUmind/OpsCoreControl/issues/new";

        private void _showAbout_Click(object sender, RoutedEventArgs e)
        {
            var about = new AboutWindow { Owner = this };
            about.ShowDialog();
        }

        private void _showBugReport_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = GitHubNewIssueUrl,
                UseShellExecute = true
            });
        }
    }
}