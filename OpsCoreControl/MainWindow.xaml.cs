using System;
using System.Collections.Generic;
using System.Linq;
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
using System.Diagnostics;
using System.Windows.Threading;
using System.Management;
using System.ServiceProcess;

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
        }

        private void _testButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void _restartPrintSpoolerButton_Click(object sender, RoutedEventArgs e)
        {
            _serviceManager = new ServiceManager();
            _serviceManager.rebootPrintSpooler("spooler", this._restartPrintSpoolerButton);
        }
    }
}