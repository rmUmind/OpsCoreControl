using System;
using System.Diagnostics;
using System.Windows;
using static OpsCoreControl.Log;

// Код окна «О программе»: показывает автора и открывает ссылку на GitHub.
namespace OpsCoreControl
{
    public partial class AboutWindow : Window
    {
        // Ссылка на GitHub автора. Если менял имя профиля — поправь здесь.
        private const string GitHubUrl = "https://github.com/rmUmind";

        public AboutWindow()
        {
            InitializeComponent();
        }

        // Открывает GitHub автора в браузере по умолчанию.
        private void _githubLink_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = GitHubUrl,
                    UseShellExecute = true
                });
                Log.Add("Открыта ссылка на GitHub.", LogType.Info);
            }
            catch (Exception ex)
            {
                Log.Add($"Не удалось открыть ссылку на GitHub: {ex.Message}", LogType.Error);
            }
        }
    }
}