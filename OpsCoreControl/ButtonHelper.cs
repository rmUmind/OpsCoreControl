using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

public static class ButtonHelper
{
    public static async Task ExecuteWithColorAsync(this Button button, Func<Task<bool>> operation)
    {
        button.Background = Brushes.Yellow;
        bool success = await operation();
        button.Background = success ? Brushes.Green : Brushes.Red;
    }
}