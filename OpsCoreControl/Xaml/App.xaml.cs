using System;
using System.Threading.Tasks;
using System.Windows;
using static OpsCoreControl.Log;

namespace OpsCoreControl
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // UI-поток
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            // Фоновые потоки
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            // Необработанные исключения в Task
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Add($"Необработанное исключение (UI): {e.Exception}", LogType.Error);
            e.Handled = true;   // не даём приложению упасть
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            Log.Add($"Необработанное исключение (фон, {(e.IsTerminating ? "критично" : "не критично")}): {ex}", LogType.Error);
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Add($"Необработанное исключение (Task): {e.Exception}", LogType.Error);
            e.SetObserved();
        }
    }
}