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

namespace OpsCoreControl
{
    public class DashBoard
    {
        private const int DashBoardIntervalRefresh = 2; // Интервал обнавления ДэшБорда
        private PerformanceCounter ramUsage = new PerformanceCounter("Memory", "Available MBytes");
        public Label _ramLoadLabel;
        public event Action<float> RamUsageUpdated;

        public DashBoard()
        {
            startDashBoard();
        }
        ~DashBoard()
        {
            ramUsage.Dispose();
        }
        public void startDashBoard()
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(DashBoardIntervalRefresh) };
            timer.Tick += (s, e) => RefreshData();
            timer.Start();
        }

        private void RefreshData()
        {
            RamUsageUpdated?.Invoke(ramUsage.NextValue());
        }
    }

    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();

            DashBoard dashBoard = new DashBoard();
            dashBoard.RamUsageUpdated += value => _ramLoadLabel.Content = value.ToString();
        }

        private void _testButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}