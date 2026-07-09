using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpsCoreControl.Logger;

namespace OpsCoreControl.WorkingСlasses
{
    internal class NetworkManager
    {
        public async Task<bool> ClearNonPagedPool()
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
                        Logger.Log("Невыгружаемый пул успешно удален", Logger.LogEntryType.Success);
                        return true;
                    }
                    else
                    {
                        Logger.Log($"Ошибка при удаление папки профился. Код {process.ExitCode}", Logger.LogEntryType.Error);
                        return false;
                    }
                }

            }
            catch (Exception ex)
            {
                Logger.Log($"Исключение при удаление невыгружаемого пула: {ex.Message}", LogEntryType.Error);
                return false;
            }
        }
    }
}
