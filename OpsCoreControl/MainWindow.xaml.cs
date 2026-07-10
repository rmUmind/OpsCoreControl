using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.IO;
using OpsCoreControl.WorkingСlasses;
using static OpsCoreControl.Log;

namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private DashBoard _dashBoard;
        private FileCleanupManager _fileCleanupManager;
        private NetworkManager _networkManager;
        private ServiceManager _serviceManager;
        private SoftwareUpdateManager _softwareUpdateManager;
        private UserProfileManager _userProfileManager;
        public MainWindow()
        {
            InitializeComponent();


            _dashBoard = new DashBoard();

            // RAM
            _dashBoard.totalRam += value => _ramLoadLabel.Content = (value / (1024 * 1024)).ToString() + " / ";
            _dashBoard.ramUsageUpdated += value => _ramLoadLabel.Content += value.ToString();

            // VRAM
            _dashBoard.virtualRamTotalUpdated += value => _virtualRamLoadLabel.Content = (value / (1024 * 1024)).ToString() + "MB / ";
            _dashBoard.virtualRamUsageUpdated += value => _virtualRamLoadLabel.Content += value.ToString() + "MB";

            // CPU
            _dashBoard.cpUsageUpdated += value => _cpLoadLabel.Content = value.ToString();

            // Free spacce
            _dashBoard.freeSpaceUpdated += value => _freeSpaceLabel.Content = value.ToString();

            this.Closed += (s, e) => _dashBoard.Dispose();

            _fileCleanupManager = new FileCleanupManager();
            _networkManager = new NetworkManager();
            _serviceManager = new ServiceManager();
            _softwareUpdateManager = new SoftwareUpdateManager();
            _userProfileManager = new UserProfileManager();

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

        private async void _showUsersProfiles_ClickAsync(object sender, RoutedEventArgs e)
        {
            _usersProfilesListBox.Items.Clear();
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _userProfileManager.LoadUserProfiles());
        }

        private async void _restartPrintSpoolerButton_Click(object sender, RoutedEventArgs e)
        {
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _serviceManager.RebootPrintSpooler("Spooler"));
        }
        private async void _cleanDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            await ButtonHelper.ExecuteWithColorAsync((Button)sender,  () => _fileCleanupManager.CleanDownloadFolder());
        }

        private async void _deleteProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var toDelete = _usersProfilesListBox.SelectedItems;
                foreach (string item in toDelete)
                {
                    await _userProfileManager.DeleteProfileFolderAsync(item);
                }
            }
            catch (Exception ex)
            {
                Log.Add(ex.Message, LogType.Error);
            }
            Log.Add("успешно удалено", LogType.Success);
        }

        private void _usersProfilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _userProfilesCountLabel.Content = "Count: " + _usersProfilesListBox.SelectedItems.Count.ToString();
        }

        private async void _clearNonRedeemablePool_Click(object sender, RoutedEventArgs e)
        {
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _networkManager.ClearNonPagedPool());
        }

        private async void _downloadCryptoPro_Click(object sender, RoutedEventArgs e)
        {
            await _softwareUpdateManager.RunEmbeddedInstallerAsync();
        }

        private void _tamplateButtonDesktopDirectroy_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _downloadDirectoryTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            });
        }

        private void _tamplateButtonDownloadDirectory_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
            _downloadDirectoryTextBox.Text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            });
        }
    }
}