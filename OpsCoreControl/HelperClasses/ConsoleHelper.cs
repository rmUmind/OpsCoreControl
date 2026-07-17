using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpsCoreControl.Log;

namespace OpsCoreControl.HelperClasses
{
    internal static class ConsoleHelper
    {
        public static ProcessStartInfo cmdConsoleCommand(string command)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                Verb = "runas"
            };
            return psi;
        }

        public static async Task<bool> LookForProcessEnd(ProcessStartInfo psi, string goodOutcome, string badOutcome, string exceptionOutcome = "Исключение работы процесса.")
        {
            try
            {
                using (var process = Process.Start(psi))
                {
                    await Task.Run(() => process.WaitForExit());
                    if (process.ExitCode == 0)
                    {
                        Log.Add(goodOutcome, LogEntryType.Success);
                        return true;
                    }
                    else
                    {
                        Log.Add(badOutcome + $" (код {process.ExitCode} | {process.StandardError.ReadToEnd()})", LogEntryType.Error);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Add(exceptionOutcome + " " + ex.Message, LogType.Error);
                return false;
            }
        }
    }
}
