using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using static OpsCoreControl.Log;

// Класс для работы с консольными командами.
// Умеет: собирать ProcessStartInfo для cmd, запускать команды с потоковым выводом,
// останавливать их и ждать завершения с проверкой кода выхода.
namespace OpsCoreControl.HelperClasses
{
    internal static class ConsoleHelper
    {
        // Собирает ProcessStartInfo для выполнения команды через cmd.exe.
        // stderr перехватывается в кодировке OEM, чтобы русский текст не был кракозябрами.
        public static ProcessStartInfo CmdConsoleCommand(string command)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage),
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            return psi;
        }

        public static event Action<string> OnOutputConsoleLine;    // пришла строка вывода
        public static event Action OnOutputConsoleComplete;        // команда завершилась

        private static Process _currentProcess;        // текущий процесс, активный всегда один
        private static volatile bool _stopRequested;   // флаг досрочной остановки

        // === ОПТИМИЗАЦИЯ: Синхронизация событий на UI поток ===
        private static Dispatcher _dispatcher;
        private static readonly object _processLock = new object();  // Синхронизация доступа к _currentProcess

        // Запускает команду и стримит её вывод построчно через OnOutputConsoleLine.
        // === ОПТИМИЗАЦИЯ: Синхронизация событий, защита от race conditions ===
        public static void RunStreaming(string fileName, string arguments)
        {
            // === FIX: Получаем UI Dispatcher правильно (не Dispatcher.CurrentDispatcher!) ===
            // Dispatcher.CurrentDispatcher может вернуть dispatcher другого потока, если метод вызван не из UI
            _dispatcher = Application.Current?.Dispatcher;
            if (_dispatcher == null)
            {
                Log.Add("Ошибка: UI Dispatcher недоступен. RunStreaming должна быть вызвана из UI потока или иметь доступ к Application.Current.", LogType.Error);
                return;
            }

            KillCurrentProcess();          // сначала убиваем прошлый процесс
            _stopRequested = false;

            Log.Add($"Запуск команды: {fileName} {arguments}", LogType.Info);

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage)
            };

            lock (_processLock)  // === Синхронизация доступа к _currentProcess ===
            {
                _currentProcess = new Process { StartInfo = psi };
                _currentProcess.EnableRaisingEvents = true;

                // срабатывает, когда процесс завершился
                // === ОПТИМИЗАЦИЯ: Событие синхронизируется на UI поток ===
                _currentProcess.Exited += (s, e) =>
                {
                    int exitCode = -1;
                    try { exitCode = ((Process)s).ExitCode; } catch { }
                    string msg = $"Команда завершена: {fileName} (код выхода: {exitCode})";

                    // Синхронизируем на UI поток через Dispatcher
                    _dispatcher?.Invoke(DispatcherPriority.Normal, new Action(() =>
                    {
                        Log.Add(msg, LogType.Info);
                        OnOutputConsoleComplete?.Invoke();
                    }));
                };

                // срабатывает на каждую строку вывода
                // === ОПТИМИЗАЦИЯ: Событие синхронизируется на UI поток ===
                _currentProcess.OutputDataReceived += (s, e) =>
                {
                    if (_stopRequested) return; // остановку запросили — вывод больше не шлём
                    if (e.Data != null)
                    {
                        // Синхронизируем на UI поток
                        _dispatcher?.Invoke(DispatcherPriority.Normal, new Action(() =>
                        {
                            OnOutputConsoleLine?.Invoke(e.Data);
                        }));
                    }
                };

                try
                {
                    _currentProcess.Start();
                    _currentProcess.BeginOutputReadLine(); // включаем асинхронное чтение вывода
                    Log.Add($"Процесс запущен, PID: {_currentProcess.Id}", LogType.Debug);
                }
                catch (Exception ex)
                {
                    Log.Add($"Не удалось запустить команду {fileName}: {ex.Message}", LogType.Error);
                }
            }
        }

        // Останавливает текущий процесс по запросу пользователя.
        // === ОПТИМИЗАЦИЯ: Защита от race conditions через lock ===
        public static void StopStreaming()
        {
            lock (_processLock)
            {
                if (_currentProcess == null)
                {
                    Log.Add("Остановка: активный процесс отсутствует.", LogType.Debug);
                    return;
                }

                _stopRequested = true;
                int pid = _currentProcess.Id;
                Log.Add($"Остановка процесса PID: {pid}...", LogType.Info);

                try
                {
                    if (!_currentProcess.HasExited)
                    {
                        // бьём дерево процессов через taskkill, /T — вместе с потомками
                        var killPsi = new ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = $"/F /T /PID {pid}",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using (Process killProcess = Process.Start(killPsi))
                        {
                            killProcess?.WaitForExit(5000);  // === Добавлен таймаут ===
                        }
                        Log.Add($"Процесс PID {pid} остановлен.", LogType.Success);
                    }
                    else
                    {
                        Log.Add("Процесс уже завершён.", LogType.Debug);
                    }
                }
                catch (Exception ex)
                {
                    Log.Add($"Ошибка остановки процесса: {ex.Message}", LogType.Error);
                }
            }
        }

        // Внутренняя остановка процесса перед запуском нового.
        // === ОПТИМИЗАЦИЯ: Защита от race conditions, улучшенная обработка ===
        private static void KillCurrentProcess()
        {
            lock (_processLock)  // Синхронизация доступа
            {
                if (_currentProcess == null) return;

                _stopRequested = true;
                try
                {
                    if (!_currentProcess.HasExited)
                    {
                        Log.Add($"Останавливаем предыдущий процесс PID {_currentProcess.Id}.", LogType.Debug);
                        _currentProcess.Kill();              // убиваем сам процесс

                        // === ОПТИМИЗАЦИЯ: WaitForExit всегда должен иметь таймаут ===
                        if (!_currentProcess.WaitForExit(3000))  // ждём реального выхода, макс 3 сек
                        {
                            Log.Add("Предупреждение: процесс не завершился в течение 3 сек.", LogType.Info);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Add($"Ошибка при остановке процесса: {ex.Message}", LogType.Error);
                }
                finally
                {
                    try { _currentProcess?.Dispose(); } catch { }  // освобождаем дескрипторы
                    _currentProcess = null;
                }
            }
        }

        // Собирает ProcessStartInfo для запуска процесса с перехватом ошибок.
        // Повышение прав (runas) тут невозможно: перехват stderr требует UseShellExecute = false,
        // а runas работает только при UseShellExecute = true. Поэтому запускаем без повышения.
        public static ProcessStartInfo StartProcess(string processName)
        {
            var psi = new ProcessStartInfo
            {
                FileName = processName,
                UseShellExecute = false,       // нужно для перехвата stderr
                RedirectStandardError = true   // перехватываем ошибки
            };
            return psi;
        }

        // Запускает процесс, ждёт завершения и логирует результат по коду выхода.
        // === ОПТИМИЗАЦИЯ: Всегда используется таймаут, даже если не указан ===
        // timeoutMs = -1 — ждать 30 сек (разумный максимум), иначе таймаут указанный
        public static async Task<bool> LookForProcessEnd(
            ProcessStartInfo psi, string goodOutcome, string badOutcome,
            string exceptionOutcome = "Исключение работы процесса.",
            int timeoutMs = -1)
        {
            try
            {
                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        Log.Add($"Не удалось запустить процесс: {psi.FileName}", LogType.Error);
                        return false;
                    }

                    // === ОПТИМИЗАЦИЯ: Всегда используется разумный таймаут ===
                    // Если не указан (-1), используем 30 сек
                    int actualTimeout = timeoutMs > 0 ? timeoutMs : 30000;

                    // ждём выхода с таймаутом
                    bool exited = await Task.Run(() => process.WaitForExit(actualTimeout));

                    if (!exited) // не дождались — таймаут
                    {
                        try { process.Kill(); } catch { }
                        Log.Add($"Таймаут ({actualTimeout} мс): {badOutcome}", LogType.Error);
                        return false;
                    }

                    if (process.ExitCode == 0)
                    {
                        Log.Add(goodOutcome, LogType.Success);
                        return true;
                    }

                    // код выхода не 0 — читаем текст ошибки из stderr
                    string error = process.StandardError.ReadToEnd();
                    Log.Add($"{badOutcome}. {error}", LogType.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Add($"{exceptionOutcome} {ex.Message}", LogType.Error);
                return false;
            }
        }
    }
}