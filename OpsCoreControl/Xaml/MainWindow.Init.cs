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
using System.Windows.Media;
using System.Security.Principal;
using Microsoft.Win32;
using System.IO;
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
        private bool _foldersLoaded;
        private bool _programsLoaded;
        private bool _isDarkTheme = Properties.Settings.Default.IsDarkTheme;
        private bool _useModernInterface = Properties.Settings.Default.UseModernInterface;
        private bool _profilesLoaded;
        private bool _systemSettingsLoaded;

        // Высота нижней панели логов в свёрнутом режиме (должна совпадать с Height в XAML).
        private const double LogRowCollapsed = 210;

        // Раскрыта ли панель логов на всю высоту (кнопка «Показать все логи»).
        private bool _logExpanded;
        private DashboardData _latestDashboardData;
        private SystemStatusWindow _systemStatusWindow;

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
                return; // окно ещё не создано — красить нечего
            }

            // Тёмный non-client area: красит заголовок и рамку в системный тёмный/светлый.
            int useDark = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));

            // Точный цвет заголовка и рамки (только Win11 22000+; на старых билдах вернёт ошибку — это нормально, игнорируем).
            int color = dark ? 0x001E1E1E : 0x00EFEFEF; // COLORREF 0x00BBGGRR
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref color, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref color, sizeof(int));

            // Пересчитываем рамку.
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

            // Имитируем уход и возврат фокуса, чтобы заголовок перекрасился сразу, без клика в другое окно.
            SendMessage(hwnd, WM_NCACTIVATE, IntPtr.Zero, IntPtr.Zero);
            SendMessage(hwnd, WM_NCACTIVATE, new IntPtr(1), IntPtr.Zero);
        }


        public MainWindow()
        {
            InitializeComponent();
            Width = Math.Max(MinWidth, Properties.Settings.Default.WindowWidth);
            Height = Math.Max(MinHeight, Properties.Settings.Default.WindowHeight);
            if (Properties.Settings.Default.WindowLeft >= 0 && Properties.Settings.Default.WindowTop >= 0)
            { WindowStartupLocation = WindowStartupLocation.Manual; Left = Properties.Settings.Default.WindowLeft; Top = Properties.Settings.Default.WindowTop; }
            _darkThemeMenuItem.Header = _isDarkTheme ? "Светлая тема" : "Тёмная тема";
            ApplyInterfaceMode(_useModernInterface);
            bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            _statusAdminText.Text = isAdmin ? "Администратор: да" : "Администратор: нет";
            if (isAdmin)
                _statusAdminText.SetResourceReference(TextBlock.ForegroundProperty, "TextFg");
            else
                _statusAdminText.Foreground = Brushes.IndianRed;
            Closing += MainWindow_Closing;

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
            ConsoleHelper.OnStreamingStateChanged += running => Dispatcher.BeginInvoke(new Action(() =>
            {
                _stopOutputButton.IsEnabled = running;
                _networkCommandStatusText.Text = running ? "Выполняется…" : "Готово";
            }));

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

            // Восстанавливаем вкладку только после создания менеджеров: смена SelectedIndex
            // синхронно вызывает SelectionChanged и может сразу начать загрузку данных.
            if (Properties.Settings.Default.SelectedTab >= 0 && Properties.Settings.Default.SelectedTab < _mainTabControl.Items.Count)
                _mainTabControl.SelectedIndex = Properties.Settings.Default.SelectedTab;

            // Красим заголовок/рамку после создания окна и после первой отрисовки.
            this.SourceInitialized += (s, e) => ApplyWindowChromeTheme(_isDarkTheme);
            this.ContentRendered += (s, e) => ApplyWindowChromeTheme(_isDarkTheme);

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



        // Подгружает данные при первом открытии вкладки (повторно не грузит).
        private async void _mainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Событие приходит и от внутренних списков/комбобоксов — реагируем только на смену вкладки.
            if (!(e.Source is TabControl)) return;
            if (_serviceManager == null) return; // защита от событий во время InitializeComponent

            if (_mainTabControl.SelectedItem == _servicesTabItem && !_servicesLoaded)
            {
                RefreshServices();
                _servicesLoaded = true;
            }
            else if (_mainTabControl.SelectedItem == _profilesTabItem && !_profilesLoaded)
            {
                _profilesLoaded = true;
                await LoadProfilesAsync();
            }
            else if (_mainTabControl.SelectedItem == _foldersTabItem && !_foldersLoaded)
            {
                RefreshLogicalDisks();
                _foldersLoaded = true;
            }
            else if (_mainTabControl.SelectedItem == _programsTabItem && !_programsLoaded)
            {
                RefreshPrograms();
                _programsLoaded = true;
            }
            else if (_mainTabControl.SelectedItem == _systemSettingsTabItem && !_systemSettingsLoaded)
            {
                _systemSettingsLoaded = true;
                await RefreshPageFileInfoAsync();
            }
            else if (_mainTabControl.SelectedItem == _processesTabItem && !_processesLoaded)
            {
                RefreshProcesses();
                _processesLoaded = true;
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
            _latestDashboardData = d;
            _statusPcText.Text = $"ПК: {d.System.PcName}";
            _statusCpuText.Text = $"CPU: {d.CpuPercent:F0}%";
            _statusRamText.Text = $"RAM: {d.RamPercent:F0}%";
            _statusCpuText.Foreground = LoadBrush(d.CpuPercent);
            _statusRamText.Foreground = LoadBrush(d.RamPercent);

            DiskSnapshot systemDisk = d.Disks.FirstOrDefault(x => x.Letter.Equals("C:", StringComparison.OrdinalIgnoreCase));
            _statusDiskText.Text = systemDisk == null ? "C: —" : $"C: {systemDisk.FreePercent:F0}% свободно";

            string wifi = d.Wifi.Connected ? $"Wi-Fi: {d.Wifi.Ssid} {d.Wifi.SignalPercent}%" : "Wi-Fi: нет";
            AdapterSnapshot active = d.Adapters.FirstOrDefault(a => a.Status == "Подключён" && a.Ip != "—");
            string ip = active != null ? active.Ip : "—";
            _statusNetworkText.Text = $"{wifi}, IP: {ip}";
            _statusUptimeText.Text = $"Uptime: {d.System.Uptime}";
            _systemStatusWindow?.UpdateData(d);
        }

        private Brush LoadBrush(double percent)
        {
            if (percent >= 90) return Brushes.IndianRed;
            if (percent >= 70) return Brushes.DarkOrange;
            return (Brush)FindResource("TextFg");
        }

        // После смены темы сразу заменяет сохранённые кисти метрик, не дожидаясь нового тика.
        private void RefreshStatusMetricColors()
        {
            if (_latestDashboardData == null) return;
            _statusCpuText.Foreground = LoadBrush(_latestDashboardData.CpuPercent);
            _statusRamText.Foreground = LoadBrush(_latestDashboardData.RamPercent);
        }

        private void _showSystemStatus_Click(object sender, RoutedEventArgs e)
        {
            if (_systemStatusWindow == null)
            {
                _systemStatusWindow = new SystemStatusWindow { Owner = this };
                _systemStatusWindow.Closed += (s, args) => _systemStatusWindow = null;
                if (_latestDashboardData != null) _systemStatusWindow.UpdateData(_latestDashboardData);
                _systemStatusWindow.Show();
            }
            else _systemStatusWindow.Activate();
        }

        // URL страницы создания issue на GitHub (для Bug Report).
        private const string GitHubNewIssueUrl = "https://github.com/rmUmind/OpsCoreControl/issues/new";

        // Открывает окно «О программе».
        private void _showAbout_Click(object sender, RoutedEventArgs e)
        {
            var about = new AboutWindow { Owner = this };
            about.ShowDialog();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (ConsoleHelper.IsStreaming && MessageBox.Show($"Команда «{ConsoleHelper.CurrentCommand}» ещё выполняется. Остановить её и закрыть приложение?", "Команда выполняется", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            { e.Cancel = true; return; }
            if (ConsoleHelper.IsStreaming) ConsoleHelper.StopStreaming();
            if (WindowState == WindowState.Normal) { Properties.Settings.Default.WindowLeft = Left; Properties.Settings.Default.WindowTop = Top; Properties.Settings.Default.WindowWidth = Width; Properties.Settings.Default.WindowHeight = Height; }
            Properties.Settings.Default.SelectedTab = _mainTabControl.SelectedIndex;
            Properties.Settings.Default.Save();
        }

        private void _runReadinessCheck_Click(object sender, RoutedEventArgs e)
        {
            var lines = new List<string>();
            bool admin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            lines.Add($"Права администратора: {(admin ? "OK" : "НЕТ")}");
            lines.Add($"WMI: {(CheckWmi() ? "OK" : "ОШИБКА")}");
            lines.Add($"PowerShell: {(FindExecutable("powershell.exe") ? "OK" : "НЕ НАЙДЕН")}");
            lines.Add($"CMD: {(FindExecutable("cmd.exe") ? "OK" : "НЕ НАЙДЕН")}");
            lines.Add($"Чтение HKLM: {(CheckRegistry() ? "OK" : "ОШИБКА")}");
            lines.Add($"Сетевое подключение: {(System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable() ? "есть" : "нет")}");
            string programs = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Programs");
            lines.Add($"Встроенные установщики: {(Directory.Exists(programs) && Directory.GetFiles(programs).Length > 0 ? "OK" : "НЕ НАЙДЕНЫ")}");
            string result = string.Join(Environment.NewLine, lines);
            MessageBox.Show(result, "Проверка готовности", MessageBoxButton.OK, lines.Any(x => x.Contains("ОШИБКА") || x.Contains("НЕ НАЙДЕН")) ? MessageBoxImage.Warning : MessageBoxImage.Information);
            Log.Add("Проверка готовности:" + Environment.NewLine + result, LogType.Info);
        }

        private bool CheckWmi() { try { using (var s = new System.Management.ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem")) { return s.Get().Count > 0; } } catch { return false; } }
        private bool CheckRegistry() { try { using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")) return k != null; } catch { return false; } }
        private bool FindExecutable(string name) { try { string p = Environment.GetEnvironmentVariable("PATH") ?? ""; return p.Split(';').Any(x => File.Exists(Path.Combine(x, name))); } catch { return false; } }

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

        // Раскрывает или сворачивает панель логов. В раскрытом виде лог и дашборд делят окно
        // пополам с вкладками и тянутся при изменении размера окна.
        private void _expandLogButton_Click(object sender, RoutedEventArgs e)
        {
            _logExpanded = _expandLogToggleButton.IsChecked == true;

            if (_logExpanded)
            {
                // * — делит окно пополам; высота окна уже учитывает раскрытый дашборд.
                _logRow.Height = new GridLength(1, GridUnitType.Star);
                _expandLogToggleButton.Content = "Свернуть логи";
            }
            else
            {
                _logRow.Height = new GridLength(LogRowCollapsed);
                _expandLogToggleButton.Content = "Показать все логи";
            }
        }
    }
}

