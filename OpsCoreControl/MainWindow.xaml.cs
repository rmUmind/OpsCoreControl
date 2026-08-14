using System.Windows;


namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        // Переключает тему по нажатию пункта меню (без галочки — инвертируем состояние сами).
        private void _toggleTheme_Click(object sender, RoutedEventArgs e)
        {
            _isDarkTheme = !_isDarkTheme;
            App.SetTheme(_isDarkTheme);
            RefreshStatusMetricColors();
            ApplyWindowChromeTheme(_isDarkTheme);
            // Подпись показывает следующее действие; если хочешь статичный текст — убери эту строку.
            _darkThemeMenuItem.Header = _isDarkTheme ? "Светлая тема" : "Тёмная тема";
            Properties.Settings.Default.IsDarkTheme = _isDarkTheme;
            Properties.Settings.Default.Save();
        }
    }
}
