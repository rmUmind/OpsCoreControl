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
        // Переключает светлую / тёмную тему по флажку в меню.
        private void _toggleTheme_Click(object sender, RoutedEventArgs e)
        {
            bool dark = _darkThemeMenuItem.IsChecked == true;
            App.SetTheme(dark);
            ApplyWindowChromeTheme(dark);
        }
    }
}