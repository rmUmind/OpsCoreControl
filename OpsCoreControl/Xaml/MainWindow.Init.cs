using OpsCoreControl.HelperClasses;
using OpsCoreControl.WorkingСlasses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using static OpsCoreControl.Log;

// Главная часть окна: создание менеджеров, подписка на дашборд и лог,
// отрисовка дашборда, меню (О программе / Bug Report) и копирование логов.
namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        // Менеджеры — в них вынесена вся бизнес-логика.
        private ProcessManager _processManager;
        private StartupManager _startupManager;
        private HostsManager _hostsManager;
        private DashBoard _dashBoard;
        private FileSystemManager _fileSystemManager;
        private NetworkManager _networkManager;
        private ServiceManager _serviceManager;
        private SoftwareManager _softwareManager;
        private UserProfileManager _userProfileManager;
        private SystemSettingsManager _systemSettingsManager;
        private PhysicalMonitorBrightnessController _monitorController;

        // Анти-мерцание: перерисовываем списки только при изменении.
        private List<string> _lastDisks = new List<string>();
        private List<string> _lastAdapters = new List<string>();
        private List<string> _lastUsb = new List<string>();

        public MainWindow()
        {
            InitializeComponent();

            // Дашборд: создаём и подписываемся на его обновления.
            _dashBoard = new DashBoard();
            _dashBoard.Updated += RenderDashboard;

            // Подписка на потоковый вывод консоли (вкладка Network).
            ConsoleHelper.OnOutputConsoleLine += line =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _outputNetworkConsoleTextBox.AppendText(line + Environment.NewLine);
                }));
            };

            // При закрытии окна останавливаем дашборд.
            this.Closed += (s, e) => _dashBoard.Dispose();

            // Создаём менеджеры.
            _fileSystemManager = new FileSystemManager();
            _networkManager = new NetworkManager();
            _networkManager.EnsureLinkedConnectionsEnabled();
            _serviceManager = new ServiceManager();
            _softwareManager = new SoftwareManager();
            _userProfileManager = new UserProfileManager();
            _systemSettingsManager = new SystemSettingsManager();
            _monitorController = new PhysicalMonitorBrightnessController();
            _processManager = new ProcessManager();
            _startupManager = new StartupManager();
            _hostsManager = new HostsManager();

            // Список оснасток для быстрого запуска (вкладка Services).
            var tools = new List<SystemTool>
            {
                new SystemTool { Name = "services.msc",  Description = "Службы" },
                new SystemTool { Name = "regedit",       Description = "Редактор реестра" },
                new SystemTool { Name = "eventvwr.msc",  Description = "Просмотр событий" },
                new SystemTool { Name = "appwiz.cpl",    Description = "Программы и компоненты" },
                new SystemTool { Name = "devmgmt.msc",   Description = "Диспетчер устройств" },
                new SystemTool { Name = "diskmgmt.msc",  Description = "Управление дисками" },
                new SystemTool { Name = "compmgmt.msc",  Description = "Управление компьютером" },
                new SystemTool { Name = "msconfig",      Description = "Конфигурация системы" },
                new SystemTool { Name = "taskmgr",       Description = "Диспетчер задач" },
                new SystemTool { Name = "lusrmgr.msc",   Description = "Локальные пользователи и группы" },
                new SystemTool { Name = "wf.msc",        Description = "Брандмауэр (расширенный)" },
                new SystemTool { Name = "resmon",        Description = "Монитор ресурсов" }
            };
            foreach (var tool in tools)
            {
                _startCustomProcessSelectItemListBox.Items.Add(tool);
            }

            // Подписка на лог: обычные сообщения идут в чат, профили — в отдельный список.
            Log.LogMessage += message => Dispatcher.Invoke(() => _mainChatListBox.Items.Add(message));
            Log.LogError += message => Dispatcher.Invoke(() => _mainChatListBox.Items.Add(message));
            Log.LogInfo += message => Dispatcher.Invoke(() => _mainChatListBox.Items.Add(message));
            Log.LogSuccess += message => Dispatcher.Invoke(() => _mainChatListBox.Items.Add(message));
            Log.LogProfile += message =>
            {
                // Системные профили в список не добавляем.
                HashSet<string> ignoreList = new HashSet<string>() {"C:\\Users\\Default", "C:\\Users\\All Users", "C:\\Users\\Default User",
                    "C:\\Users\\DefaultAppPool", "C:\\Users\\Все пользователи", "C:\\Users\\Public"};
                if (!ignoreList.Contains(message))
                {
                    Dispatcher.Invoke(() => _usersProfilesListBox.Items.Add(message));
                }
            };
            Log.LogDebug += message => Dispatcher.Invoke(() => _mainChatListBox.Items.Add(message));
        }

        // Отрисовывает дашборд из свежего снапшота (вызывается по событию Updated).
        private void RenderDashboard(DashboardData d)
        {
            // Краткая зона.
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

            // Расширенная зона.
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

        // Обновляет список, только если содержимое изменилось (чтобы не мерцало).
        private void UpdateListBox(ListBox lb, List<string> items, ref List<string> cache)
        {
            if (cache.SequenceEqual(items)) return;
            cache = items;
            lb.Items.Clear();
            foreach (string it in items) lb.Items.Add(it);
        }

        // Показывает или прячет расширенную зону дашборда.
        private void _dashboardToggleButton_Click(object sender, RoutedEventArgs e)
        {
            bool expand = _dashboardToggleButton.IsChecked == true;
            _extendedDashboardPanel.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
        }

        // URL страницы создания issue на GitHub (для Bug Report).
        private const string GitHubNewIssueUrl = "https://github.com/rmUmind/OpsCoreControl/issues/new";

        // Открывает окно «О программе».
        private void _showAbout_Click(object sender, RoutedEventArgs e)
        {
            var about = new AboutWindow { Owner = this };
            about.ShowDialog();
        }

        // Открывает в браузере страницу создания отчёта об ошибке.
        private void _showBugReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = GitHubNewIssueUrl,
                    UseShellExecute = true
                });
                Log.Add("Открыта страница создания отчёта об ошибке.", LogType.Info);
            }
            catch (Exception ex)
            {
                Log.Add($"Не удалось открыть страницу Bug Report: {ex.Message}", LogType.Error);
            }
        }

        // Копирует все сообщения лога (чат) в буфер обмена.
        private void _copyAllLogsButton_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            foreach (var item in _mainChatListBox.Items)
            {
                sb.AppendLine(item.ToString());
            }
            if (sb.Length == 0)
            {
                Log.Add("Лог пуст.", LogType.Info);
                return;
            }
            Clipboard.SetText(sb.ToString());
            Log.Add("Все логи скопированы в буфер обмена.", LogType.Success);
        }
    }
}