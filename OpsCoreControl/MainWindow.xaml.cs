using System.Windows;


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