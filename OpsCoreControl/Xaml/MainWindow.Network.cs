using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using OpsCoreControl.WorkingСlasses;
using static OpsCoreControl.Log;

namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private async void _clearNonRedeemablePool_Click(object sender, RoutedEventArgs e)
        {
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _networkManager.ClearNonPagedPool());
        }
    }
}