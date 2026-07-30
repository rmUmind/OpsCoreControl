using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using static OpsCoreControl.Log;

// Класс приложения. Подключает глобальные обработчики исключений
// и управляет темой оформления (светлая / тёмная).
namespace OpsCoreControl
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Глобальные обработчики исключений.
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            SetTheme(false); // по умолчанию светлая тема
        }

        // Меняет палитру приложения. Стили ссылаются на эти кисти через DynamicResource,
        // поэтому вся разметка перекрашивается автоматически.
        public static void SetTheme(bool dark)
        {
            var r = Current.Resources;
            if (dark)
            {
                r["WindowBg"] = Brush("#1E1E1E");
                r["PanelBg"] = Brush("#252526");
                r["TextFg"] = Brush("#E8E8E8");
                r["BorderBr"] = Brush("#3F3F46");
                r["ButtonBg"] = Brush("#333337");
                r["ButtonHover"] = Brush("#3E3E42");
                r["ButtonPressed"] = Brush("#2A2A2E");
                r["InputBg"] = Brush("#2D2D30");
                r["ListBg"] = Brush("#252526");
                r["AccentFg"] = Brush("#4CC2FF");
            }
            else
            {
                r["WindowBg"] = Brush("#EFEFEF");
                r["PanelBg"] = Brush("#FFFFFF");
                r["TextFg"] = Brush("#1A1A1A");
                r["BorderBr"] = Brush("#C8C8C8");
                r["ButtonBg"] = Brush("#E4E4E4");
                r["ButtonHover"] = Brush("#D6D6D6");
                r["ButtonPressed"] = Brush("#C4C4C4");
                r["InputBg"] = Brush("#FFFFFF");
                r["ListBg"] = Brush("#FFFFFF");
                r["AccentFg"] = Brush("#0067C0");
            }
        }

        // Кисть из HEX-строки.
        private static SolidColorBrush Brush(string hex) =>
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

        // Ловит исключения UI-потока и не даёт приложению закрыться.
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Add($"Необработанное исключение (UI): {e.Exception}", LogType.Error);
            e.Handled = true;
        }

        // Ловит исключения фоновых потоков (отменить падение здесь нельзя, только залогировать).
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Add($"Необработанное исключение (фон, {(e.IsTerminating ? "критично" : "не критично")}): {e.ExceptionObject}", LogType.Error);
        }

        // Ловит необработанные исключения в Task.
        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Add($"Необработанное исключение (Task): {e.Exception}", LogType.Error);
            e.SetObserved();
        }
    }
}