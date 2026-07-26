using OpsCoreControl.HelperClasses;
using OpsCoreControl.WorkingСlasses;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using static OpsCoreControl.Log;

namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private async void _clearNonRedeemablePool_Click(object sender, RoutedEventArgs e)
        {
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _networkManager.ClearNonPagedPool());
        }
        private void _showIpconfigButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("ipconfig", "/all");
        }

        private void _stopOutputButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.StopStreaming();
        }

        private void _startPingButton_Click(object sender, RoutedEventArgs e)
        {

            ConsoleHelper.RunStreaming("ping", $"{_ipAdressTextBox.Text} -t");   // не "cmd /c ping", а сразу ping
        }

        private void _startTrecertButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.RunStreaming("tracert", _ipAdressTextBox.Text);
        }

        private void _clearOutputNetworkConsoleTextBox_Click(object sender, RoutedEventArgs e)
        {
            _outputNetworkConsoleTextBox.Clear();
        }

        private void _clearipAdressTextBoxButton_Click(object sender, RoutedEventArgs e)
        {
            _ipAdressTextBox.Clear();
        }

    }
}