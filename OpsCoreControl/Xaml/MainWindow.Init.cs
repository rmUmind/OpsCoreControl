using OpsCoreControl.HelperClasses;
using OpsCoreControl.WorkingСlasses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
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

        // Флажки однократной автозагрузки данных вкладок.
        private bool _eventLogLoaded;
        private bool _processesLoaded;
        private bool _startupLoaded;
        private bool _hostsLoaded;
        private bool _servicesLoaded;

        // Анти-мерцание: перерисовываем списки только при изменении.
        private List<string> _lastDisks = new List<string>();
        private List<string> _lastAdapters = new List<string>();
        private List<string> _lastUsb = new List<string>();

        // Перекрашивает заголовок и рамку окна под тему через DWM.
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        // Шлёт окну системное сообщение (нужно, чтобы имитировать смену фокуса для заголовка).
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        // Принудительная перерисовка non-client области (рамки) без смены размера и фокуса.
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint uFlags);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20; // тёмный заголовок и кнопки (Win10 1809+ / Win11)
        private const int DWMWA_BORDER_COLOR = 34;            // цвет рамки (Win11 build 22000+)
        private const int DWMWA_CAPTION_COLOR = 35;           // цвет заголовка (Win11 build 22000+)
        private const uint WM_NCACTIVATE = 0x0086;            // сообщение «изменилось активное состояние заголовка»

        // Флаги SetWindowPos: не двигать/не менять размер/не трогать z-order/не активировать, но пересчитать рамку.
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private void ApplyWindowChromeTheme(bool dark)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                Log.Add("DWM: handle окна ещё не создан.", LogType.Debug);
                return;
            }

            // Тёмный non-client area: красит заголовок и рамку в системный тёмный/светлый.
            int useDark = dark ? 1 : 0;
            int hrDark = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));

            // Точный цвет заголовка и рамки (только Win11 22000+; на старых билдах вернёт E_INVALIDARG — это нормально).
            int color = dark ? 0x001E1E1E : 0x00EFEFEF; // COLORREF 0x00BBGGRR
            int hrCaption = DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref color, sizeof(int));
            int hrBorder = DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref color, sizeof(int));

            // Пересчитываем рамку.
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

            // Заставляем DWM пересчитать палитру заголовка прямо сейчас: имитируем уход и возврат фокуса.
            // Без этого заголовок меняет цвет только когда пользователь реально кликнет в другое окно.
            SendMessage(hwnd, WM_NCACTIVATE, IntPtr.Zero, IntPtr.Zero);   // «стало неактивным»
            SendMessage(hwnd, WM_NCACTIVATE, new IntPtr(1), IntPtr.Zero); // «стало активным» — с новым цветом

            Log.Add($"DWM тема={(dark ? "тёмная" : "светлая")}: dark={hrDark}, caption={hrCaption}, border={hrBorder}.", LogType.Debug);
        }

        public MainWindow()
        {
            InitializeComponent();

            // === ОПТИМИЗАЦИЯ: Быстрая инициализация, отложенное создание менеджеров ===

            // Дашборд: создаём сразу (он небольшой, нужен в loop).
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

            // Подписка на лог: используем BeginInvoke вместо Invoke для неблокирующести.
            // === ОПТИМИЗАЦИЯ: BeginInvoke вместо Invoke для неблокирующести ===
            Log.LogMessage += message => Dispatcher.BeginInvoke(new Action(() => _mainChatListBox.Items.Add(message)));
            Log.LogError += message => Dispatcher.BeginInvoke(new Action(() => _mainChatListBox.Items.Add(message)));
            Log.LogInfo += message => Dispatcher.BeginInvoke(new Action(() => _mainChatListBox.Items.Add(message)));
            Log.LogSuccess += message => Dispatcher.BeginInvoke(new Action(() => _mainChatListBox.Items.Add(message)));
            Log.LogProfile += message =>
            {
                HashSet<string> ignoreList = new HashSet<string>() {"C:\\Users\\Default", "C:\\Users\\All Users", "C:\\Users\\Default User",
                    "C:\\Users\\DefaultAppPool", "C:\\Users\\Все пользователи", "C:\\Users\\Public"};
                if (!ignoreList.Contains(message))
                {
                    Dispatcher.BeginInvoke(new Action(() => _usersProfilesListBox.Items.Add(message)));
                }
            };
            Log.LogDebug += message => Dispatcher.BeginInvoke(new Action(() => _mainChatListBox.Items.Add(message)));

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

            // Красим заголовок/рамку после создания окна и после первой отрисовки.
            this.SourceInitialized += (s, e) => ApplyWindowChromeTheme(_darkThemeMenuItem.IsChecked == true);
            this.ContentRendered += (s, e) => ApplyWindowChromeTheme(_darkThemeMenuItem.IsChecked == true);

            // === ОПТИМИЗАЦИЯ: Отложенная инициализация менеджеров (фоновый Task) ===
            // Окно откроется мгновенно, а менеджеры создадутся в фоне.
            this.Loaded += async (s, e) =>
            {
                await InitializeManagersAsync();
            };
        }

        // === ОПТИМИЗАЦИЯ: Асинхронная инициализация менеджеров ===
        private async System.Threading.Tasks.Task InitializeManagersAsync()
        {
            try
            {
                // Создаём менеджеры в фоновом потоке, с минимальной задержкой между ними
                // (чтобы UI могла обновляться).

                // Быстрые менеджеры — создаём вместе
                await System.Threading.Tasks.Task.Run(() =>
                {
                    _fileSystemManager = new FileSystemManager();
                });

                await System.Threading.Tasks.Task.Run(() =>
                {
                    _networkManager = new NetworkManager();
                });

                // === ОПТИМИЗАЦИЯ: EnsureLinkedConnectionsEnabled в фоне ===
                await System.Threading.Tasks.Task.Run(() =>
                {
                    _networkManager.EnsureLinkedConnectionsEnabled();  // Может быть медленной
                });

                await System.Threading.Tasks.Task.Run(() =>
                {
                    _serviceManager = new ServiceManager();
                });

                await System.Threading.Tasks.Task.Run(() =>
                {
                    _softwareManager = new SoftwareManager();
                });

                await System.Threading.Tasks.Task.Run(() =>
                {
                    _userProfileManager = new UserProfileManager();
                });

                await System.Threading.Tasks.Task.Run(() =>
                {
                    _systemSettingsManager = new SystemSettingsManager();
                });

                await System.Threading.Tasks.Task.Run(() =>
                {
                    _monitorController = new PhysicalMonitorBrightnessController();
                });

                await System.Threading.Tasks.Task.Run(() =>
                {
                    _processManager = new ProcessManager();
                });

                await System.Threading.Tasks.Task.Run(() =>
                {
                    _startupManager = new StartupManager();
                });

                await System.Threading.Tasks.Task.Run(() =>
                {
                    _hostsManager = new HostsManager();
                });

                Log.Add("Все менеджеры инициализированы.", LogType.Info);
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка инициализации менеджеров: {ex.Message}", LogType.Error);
            }
        }

        // Подгружает данные при первом открытии вкладки (повторно не грузит).
        private void _mainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Событие приходит и от внутренних списков/комбобоксов — реагируем только на смену вкладки.
            if (!(e.Source is TabControl)) return;

            if (_mainTabControl.SelectedItem == _processesTabItem && !_processesLoaded)
            {
                RefreshProcesses();
                _processesLoaded = true;
            }
            else if (_mainTabControl.SelectedItem == _servicesTabItem && !_servicesLoaded)
            {
                RefreshServices();
                _servicesLoaded = true;
            }
            else if (_mainTabControl.SelectedItem == _startupTabItem && !_startupLoaded)
            {
                RefreshStartup();
                _startupLoaded = true;
            }
            else if (_mainTabControl.SelectedItem == _eventLogTabItem && !_eventLogLoaded)
            {
                LoadEventLog();
                _eventLogLoaded = true;
            }
            else if (_mainTabControl.SelectedItem == _hostsTabItem && !_hostsLoaded)
            {
                _hostsTextBox.Text = _hostsManager.ReadHosts();
                _hostsLoaded = true;
            }
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