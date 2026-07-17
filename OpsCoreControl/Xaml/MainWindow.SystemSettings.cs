using OpsCoreControl.WorkingСlasses;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static OpsCoreControl.Log;

namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private async void _setScreenLockTimerButton_Click(object sender, RoutedEventArgs e)
        {
            int mitutes;
            if (!int.TryParse(_timeToScreenLockTimerTextBox.Text, out mitutes)) { mitutes = 10; }
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => Task.Run(() => _systemSettingsManager.SetScreenLockTimeout(Convert.ToInt32(mitutes))));
        }
        private async void _setMonitorBrightnessButton_Click(object sender, RoutedEventArgs e)
        {
            uint brightness;
            if (!uint.TryParse(_setMonitorBrightnessTextBox.Text, out  brightness)) { brightness = 100; };
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => Task.Run(() => _monitorController.Set(brightness)));
        }
    }
}