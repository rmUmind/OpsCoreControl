using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpsCoreControl.Log;

namespace OpsCoreControl.WorkingСlasses
{
    internal class NetworkManager
    {
        public async Task<bool> ClearNonPagedPool ()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c netsh winsock reset & netsh int ip reset & ipconfig /release & ipconfig /renew & ipconfig /flushdns",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Verb = "runas"
                };
                using (var process = Process.Start(psi))
                {
                    await Task.Run(() => process.WaitForExit());
                    if (process.ExitCode == 0)
                    {
                        Log.Add("Невыгружаемый пул успешно удален", LogType.Success);
                        return true;
                    }
                    else
                    {
                        Log.Add($"Ошибка при удаление папки профился. Код {process.ExitCode}", LogType.Error);
                        return false;
                    }
                }

            }
            catch (Exception ex)
            {
                Log.Add($"Исключение при удаление невыгружаемого пула: {ex.Message}", LogEntryType.Error);
                return false;
            }
        }
    }
}
