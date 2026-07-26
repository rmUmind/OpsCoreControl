using OpsCoreControl.HelperClasses;
using OpsCoreControl.WorkingСlasses;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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

        public MainWindow()
        {
            InitializeComponent();


            _dashBoard = new DashBoard();

            // RAM
            _dashBoard.totalRam += value => _ramLoadLabel.Content = (value / (1024 * 1024)).ToString() + " / ";
            _dashBoard.ramUsageUpdated += value => _ramLoadLabel.Content += Math.Round((float)value).ToString() + " MB";

            // VRAM
            _dashBoard.virtualRamTotalUpdated += value => _virtualRamLoadLabel.Content = Math.Round((float)(value / (1024 * 1024))).ToString() + " / ";
            _dashBoard.virtualRamUsageUpdated += value => _virtualRamLoadLabel.Content += Math.Round((float)value).ToString() + " MB";

            // CPU
            _dashBoard.cpUsageUpdated += value => _cpLoadLabel.Content = Math.Round((float)value).ToString() + "%";

            // Free spacce
            _dashBoard.freeSpaceUpdated += value => _freeSpaceLabel.Content = Math.Round((float)value).ToString() + "%";


            // Network console | Подписка на консоль во вкладке Network
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
            Log.LogMessage += message =>
            {
                Dispatcher.Invoke(() =>
                {
                    _mainChatListBox.Items.Add(message);
                });
            };
            Log.LogError += message =>
            {
                Dispatcher.Invoke(() =>
                {
                    _mainChatListBox.Items.Add(message);
                });
            };
            Log.LogInfo += message =>
            {
                Dispatcher.Invoke(() =>
                {
                    _mainChatListBox.Items.Add(message);
                });
            };
            Log.LogSuccess += message =>
            {
                Dispatcher.Invoke(() =>
                {
                    _mainChatListBox.Items.Add(message);
                });
            };
            Log.LogProfile += message =>
            {
                HashSet<string> ignoreList = new HashSet<string>() {"C:\\Users\\Default", "C:\\Users\\All Users", "C:\\Users\\Default User",
                    "C:\\Users\\DefaultAppPool", "C:\\Users\\Все пользователи", "C:\\Users\\Public"};
                if (!ignoreList.Contains(message))
                {
                    Dispatcher.Invoke(() =>
                    {
                        _usersProfilesListBox.Items.Add(message);
                    });
                }
            };
            Log.LogDebug += message =>
            {
                Dispatcher.Invoke(() =>
                {
                    _mainChatListBox.Items.Add(message);
                });
            };
        }
    }
    public static class AutoScrollBehavior
    {
        public static readonly DependencyProperty AutoScrollProperty =
            DependencyProperty.RegisterAttached(
                "AutoScroll",
                typeof(bool),
                typeof(AutoScrollBehavior),
                new PropertyMetadata(false, OnAutoScrollChanged));

        public static bool GetAutoScroll(DependencyObject obj) => (bool)obj.GetValue(AutoScrollProperty);
        public static void SetAutoScroll(DependencyObject obj, bool value) => obj.SetValue(AutoScrollProperty, value);

        private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue) return;

            if (d is ListBox listBox)
            {
                ((INotifyCollectionChanged)listBox.Items).CollectionChanged += (s, args) =>
                {
                    if (listBox.Items.Count > 0)
                        listBox.ScrollIntoView(listBox.Items[listBox.Items.Count - 1]);
                };
            }
            else if (d is TextBox textBox)
            {
                textBox.TextChanged += (s, args) => textBox.ScrollToEnd();
            }
        }
    }
}