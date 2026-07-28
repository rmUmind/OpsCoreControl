using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpsCoreControl.Log;

namespace OpsCoreControl.HelperClasses
{
    internal static class ConsoleHelper
    {
        public static ProcessStartInfo CmdConsoleCommand(string command)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage), // ← фикс кракозябр
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            return psi;
        }

        public static event Action<string> OnOutputConsoleLine;    // пришла строка
        public static event Action OnOutputConsoleComplete;        // команда завершилась

        private static Process _currentProcess;
        private static volatile bool _stopRequested;

        public static void RunStreaming(string fileName, string arguments)
        {
            KillCurrentProcess();          // ← ОБЯЗАТЕЛЬНО первым: убиваем прошлый процесс
            _stopRequested = false;

            Log.Add($"Запуск команды: {fileName} {arguments}", LogEntryType.Info);

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
            _currentProcess.Exited += (s, e) =>
            {
                int exitCode = -1;
                try { exitCode = ((Process)s).ExitCode; } catch { }
                Log.Add($"Команда завершена: {fileName} (код выхода: {exitCode})", LogEntryType.Info);
                OnOutputConsoleComplete?.Invoke();
            };
            _currentProcess.OutputDataReceived += (s, e) =>
            {
                if (_stopRequested) return;
                if (e.Data != null) OnOutputConsoleLine?.Invoke(e.Data);
            };

            try
            {
                _currentProcess.Start();
                _currentProcess.BeginOutputReadLine();
                Log.Add($"Процесс запущен, PID: {_currentProcess.Id}", LogEntryType.Debug);
            }
            catch (Exception ex)
            {
                Log.Add($"Не удалось запустить команду {fileName}: {ex.Message}", LogEntryType.Error);
            }
        }

        public static void StopStreaming()
        {
            if (_currentProcess == null)
            {
                Log.Add("Остановка: активный процесс отсутствует.", LogEntryType.Debug);
                return;
            }

            _stopRequested = true;
            Log.Add($"Остановка процесса PID: {_currentProcess.Id}...", LogEntryType.Info);

            try
            {
                if (!_currentProcess.HasExited)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/F /T /PID {_currentProcess.Id}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    })?.WaitForExit();

                    Log.Add($"Процесс PID {_currentProcess.Id} остановлен.", LogEntryType.Success);
                }
                else
                {
                    Log.Add("Процесс уже завершён.", LogEntryType.Debug);
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка остановки процесса: {ex.Message}", LogEntryType.Error);
            }
        }
        private static void KillCurrentProcess()
        {
            if (_currentProcess == null) return;

            _stopRequested = true;
            try
            {
                if (!_currentProcess.HasExited)
                {
                    _currentProcess.Kill();              // убиваем сам процесс, без внешнего taskkill
                    _currentProcess.WaitForExit(2000);   // ждём реального выхода → async-чтение завершится
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка при остановке процесса: {ex.Message}", LogEntryType.Error);
            }
            finally
            {
                _currentProcess.Dispose();               // освобождаем дескрипторы
                _currentProcess = null;
            }
        }

        public static ProcessStartInfo StartProcess(string processName)
        {
            var psi = new ProcessStartInfo
            {
                FileName = processName,
                UseShellExecute = false,      // нужно для RedirectStandardError
                RedirectStandardError = true, // нужно для UseShellExecute = false
                Verb = "runas"                // нужно для UseShellExecute = true  ← конфликт
            };
            return psi;
        }

        public static async Task<bool> LookForProcessEnd(
        ProcessStartInfo psi, string goodOutcome, string badOutcome,
        string exceptionOutcome = "Исключение работы процесса.",
        int timeoutMs = -1)                                   // ← новый параметр, -1 = ждать вечно
        {
            try
            {
                using (var process = Process.Start(psi))
                {
                    bool exited = timeoutMs > 0
                        ? await Task.Run(() => process.WaitForExit(timeoutMs))
                        : await Task.Run(() => { process.WaitForExit(); return true; });

                    if (!exited)                                  // сработал таймаут
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
