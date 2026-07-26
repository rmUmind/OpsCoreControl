using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OpsCoreControl.WorkingСlasses;
using static OpsCoreControl.Log;

namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private async void _cleanDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _fileSystemManager.CleanDownloadFolder());
        }

        private async void _openNetworkPathButton_Click(object sender, RoutedEventArgs e)
        {
            string path = _openNetworkPathTextBox.Text;
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _fileSystemManager.OpenNetworkPath(path));
        }
        private async void _cleadTempFolderButton_Click(object sender, RoutedEventArgs e)
        {
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _fileSystemManager.CleanTempFolder());
        }
    }
}