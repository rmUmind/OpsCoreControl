using System.Windows;
using static OpsCoreControl.Log;

namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private void _loadHostsButton_Click(object sender, RoutedEventArgs e)
        {
            _hostsTextBox.Text = _hostsManager.ReadHosts();
            Log.Add("Файл hosts загружен.", LogType.Info);
        }

        private void _openHostsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            _hostsManager.OpenHostsFolder();
        }
    }
}