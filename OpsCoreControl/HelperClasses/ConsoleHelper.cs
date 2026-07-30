using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        // Запускает команду и стримит её вывод построчно через OnOutputConsoleLine.
        public static void RunStreaming(string fileName, string arguments)
        {
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

            _currentProcess = new Process { StartInfo = psi };
            _currentProcess.EnableRaisingEvents = true;

            // срабатывает, когда процесс завершился
            _currentProcess.Exited += (s, e) =>
            {
                int exitCode = -1;
                try { exitCode = ((Process)s).ExitCode; } catch { }
                Log.Add($"Команда завершена: {fileName} (код выхода: {exitCode})", LogType.Info);
                OnOutputConsoleComplete?.Invoke();
            };

            // срабатывает на каждую строку вывода
            _currentProcess.OutputDataReceived += (s, e) =>
            {
                if (_stopRequested) return; // остановку запросили — вывод больше не шлём
                if (e.Data != null) OnOutputConsoleLine?.Invoke(e.Data);
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

        // Останавливает текущий процесс по запросу пользователя.
        public static void StopStreaming()
        {
            if (_currentProcess == null)
            {
                Log.Add("Остановка: активный процесс отсутствует.", LogType.Debug);
                return;
            }

            _stopRequested = true;
            Log.Add($"Остановка процесса PID: {_currentProcess.Id}...", LogType.Info);

            try
            {
                if (!_currentProcess.HasExited)
                {
                    // бьём дерево процессов через taskkill, /T — вместе с потомками
                    var killPsi = new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/F /T /PID {_currentProcess.Id}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using (Process killProcess = Process.Start(killPsi))
                    {
                        killProcess?.WaitForExit();
                    }
                    Log.Add($"Процесс PID {_currentProcess.Id} остановлен.", LogType.Success);
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

        // Внутренняя остановка процесса перед запуском нового.
        private static void KillCurrentProcess()
        {
            if (_currentProcess == null) return;

            _stopRequested = true;
            try
            {
                if (!_currentProcess.HasExited)
                {
                    Log.Add($"Останавливаем предыдущий процесс PID {_currentProcess.Id}.", LogType.Debug);
                    _currentProcess.Kill();              // убиваем сам процесс
                    _currentProcess.WaitForExit(2000);   // ждём реального выхода, чтобы дочитался вывод
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка при остановке процесса: {ex.Message}", LogType.Error);
            }
            finally
            {
                _currentProcess.Dispose();               // освобождаем дескрипторы
                _currentProcess = null;
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
        // timeoutMs = -1 — ждать вечно, иначе убиваем процесс по таймауту.
        public static async Task<bool> LookForProcessEnd(
            ProcessStartInfo psi, string goodOutcome, string badOutcome,
            string exceptionOutcome = "Исключение работы процесса.",
            int timeoutMs = -1)
        {
            try
            {
                using (var process = Process.Start(psi))
                {
                    // ждём выхода: с таймаутом или бесконечно
                    bool exited = timeoutMs > 0
                        ? await Task.Run(() => process.WaitForExit(timeoutMs))
                        : await Task.Run(() => { process.WaitForExit(); return true; });

                    if (!exited) // не дождались — таймаут
                    {
                        try { process.Kill(); } catch { }
                        Log.Add($"Таймаут ({timeoutMs} мс): {badOutcome}", LogType.Error);
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