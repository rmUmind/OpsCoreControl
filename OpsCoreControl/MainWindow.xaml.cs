using OpsCoreControl.HelperClasses;
using OpsCoreControl.WorkingСlasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using static OpsCoreControl.Log;

namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        
        private void _setNewNameLogicalDiskButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void _findCurrentLogicalDiskButton_Click(object sender, RoutedEventArgs e)
        {
            _currentLogicalDiskListBox.Items.Clear();
            var disks = _networkManager.GetLogicalDrives();
            foreach (var disk in disks) { 
                _currentLogicalDiskListBox.Items.Add(disk);
            }
        }

        private void _deleteCurrentLogicaldiskButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void _mapLogicalDiskButton_Click(object sender, RoutedEventArgs e)
        {
            string path = _setNetworkPathTextBox.Text;
            string latter = _setNameForNewLogicalDiskTextBox.Text;
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _networkManager.MapNetworkDrive(path, latter));
        }
    }
}