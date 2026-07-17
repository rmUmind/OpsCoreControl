using OpsCoreControl.WorkingСlasses;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using static OpsCoreControl.Log;

namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private async void _downloadCryptoPro_Click(object sender, RoutedEventArgs e)
        {
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, async () =>
            {
                return await _softwareManager.RunEmbeddedInstallerAsync(
                    "OpsCoreControl.Programs.CryptoPro-5.0.13800.exe",   // точное имя ресурса
                    "CryptoProCSP_installer.exe");                 // имя временного файла
            });
        }
        private void _tamplateButtonDesktopDirectroy_Click(object sender, RoutedEventArgs e)
        {
            _downloadDirectoryTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }
        private void _tamplateButtonDownloadDirectory_Click(object sender, RoutedEventArgs e)
        {
            _downloadDirectoryTextBox.Text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
    }
}