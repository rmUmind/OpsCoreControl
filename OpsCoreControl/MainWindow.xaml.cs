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


namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private DashBoard _dashBoard;
        private ServiceManager _serviceManager;
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

            _serviceManager = new ServiceManager();

            _mainChatListBox.Items.Add("test");
            _serviceManager.ErrorMessage += message =>
            {
                Dispatcher.Invoke(() =>{
                    _mainChatListBox.Items.Add(message);
                });
            };
        }

        private void _testButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void _restartPrintSpoolerButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                await btn.ExecuteWithColorAsync(() => _serviceManager.RebootPrintSpooler("Spooler"));
        }
        private async void _cleanDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            await ButtonHelper.ExecuteWithColorAsync((Button)sender,  () => _serviceManager.CleanDownloadFolder());
        }
    }
}