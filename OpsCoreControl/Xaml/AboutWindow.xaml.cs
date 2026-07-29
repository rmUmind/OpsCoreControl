using System.Diagnostics;
using System.Windows;

namespace OpsCoreControl
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        private void _githubLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/rmUmind",
                UseShellExecute = true
            });
        }
    }
}