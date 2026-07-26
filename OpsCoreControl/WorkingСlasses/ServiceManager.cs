using Microsoft.Win32;
using OpsCoreControl.HelperClasses;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using static OpsCoreControl.Log;

namespace OpsCoreControl
{
    internal class ServiceManager
    {
        public async Task<bool> rebootPC()
        {
            var psi = ConsoleHelper.CmdConsoleCommand("$\"/c shutdown /r /t 0 /f\"");
            return await ConsoleHelper.LookForProcessEnd(psi, "Комптютер будет перезагружен.", "Не удалось перезагрузить компьютер");
        }

        public async Task<bool> shutdownPC()
        {
            var psi = ConsoleHelper.CmdConsoleCommand("$\"/c shutdown /s /t 0 /f\"");
            return await ConsoleHelper.LookForProcessEnd(psi, "Комптютер будет выключен.", "Не удалось перезагрузить компьютер");
        }

        public Task<bool> startCustomProcess(string processName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(processName))
                {
                    Log.Add("Не указано имя процесса.", LogType.Error);
                    return Task.FromResult(false);
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = processName.Trim(),
                    UseShellExecute = true
                });

                Log.Add($"Процесс запущен: {processName}", LogType.Success);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Log.Add($"Не удалось запустить процесс: {ex.Message}", LogType.Error);
                return Task.FromResult(false);
            }
        }

        public async Task<bool> RebootPrintSpooler(string serviceName)
        {
            try
            {
                await Task.Run(() => {
                    using (var svc = new ServiceController(serviceName))
                    {
                        if (svc.Status != ServiceControllerStatus.Stopped)
                        {
                            svc.Stop();
                            svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                        }
                        svc.Start();
                        svc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    }
                });
                Log.Add($"Служба {serviceName} успешно перезапущена", LogType.Success);
                return true;
            }
            catch (Exception ex)
            {
                Log.Add($"Исключение при перезапуске службы: {serviceName} - " + ex.Message, LogType.Error);
                return false;
            }  
        }
    }
}