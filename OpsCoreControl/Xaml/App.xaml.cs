using System;
using System.Threading.Tasks;
using System.Windows;
using static OpsCoreControl.Log;

// Класс приложения. Подключает глобальные обработчики исключений, чтобы приложение
// не падало молча, а записывало ошибку в лог.
namespace OpsCoreControl
{
    public partial class App : Application
    {
        // При старте подписываемся на все источники необработанных исключений.
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

        // Ловит исключения UI-потока. Помечает как обработанное, чтобы приложение не закрылось.
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Add($"Необработанное исключение (UI): {e.Exception}", LogType.Error);
            e.Handled = true;   // не даём приложению упасть
        }

        // Ловит исключения фоновых потоков. Отменить падение здесь нельзя — только залогировать.
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Add($"Необработанное исключение (фон, {(e.IsTerminating ? "критично" : "не критично")}): {e.ExceptionObject}", LogType.Error);
        }

        // Ловит необработанные исключения в Task. Помечает как наблюденное, чтобы не всплыло в финализаторе.
        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Add($"Необработанное исключение (Task): {e.Exception}", LogType.Error);
            e.SetObserved();
        }
    }
}