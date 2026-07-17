using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using OpsCoreControl.WorkingСlasses;
using static OpsCoreControl.Log;

namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private async void _restartPrintSpoolerButton_Click(object sender, RoutedEventArgs e)
        {
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _serviceManager.RebootPrintSpooler("Spooler"));
        }
        private async void _rebootPC_Click(object sender, RoutedEventArgs e)
        {
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _serviceManager.rebootPC());
        }
        private async void _shutdownPC_Click(object sender, RoutedEventArgs e)
        {
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _serviceManager.shutdownPC());
        }
    }
}