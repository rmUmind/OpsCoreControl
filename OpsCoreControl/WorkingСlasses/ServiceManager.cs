using Microsoft.Win32;
using OpsCoreControl.HelperClasses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using static OpsCoreControl.Log;
using System.Linq;

namespace OpsCoreControl
{
    public class SystemTool
    {
        public string Name { get; set; }        // что запускать: "devmgmt.msc"
        public string Description { get; set; } // как показать: "Диспетчер устройств"
        public override string ToString() => $"{Description} ({Name})";
    }

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
        public class ServiceInfo
        {
            public string ServiceName { get; set; }
            public string DisplayName { get; set; }
            public string Status { get; set; }
            public string StartType { get; set; }
            public override string ToString() => $"{DisplayName}  [{ServiceName}]  —  {Status}  ({StartType})";
        }
        public async Task<bool> RebootPrintSpooler(string serviceName)
        {
            try
            {
                await Task.Run(() =>
                {
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

        public List<ServiceInfo> GetServices()
        {
            var result = new List<ServiceInfo>();
            foreach (ServiceController sc in ServiceController.GetServices())
            {
                string startType = "?";
                try { startType = sc.StartType.ToString(); } catch { }
                result.Add(new ServiceInfo
                {
                    ServiceName = sc.ServiceName,
                    DisplayName = sc.DisplayName,
                    Status = sc.Status.ToString(),
                    StartType = startType
                });
                sc.Dispose();
            }
            return result.OrderBy(s => s.DisplayName).ToList();
        }

        public bool StartService(string serviceName)
        {
            try
            {
                using (var sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running) { Log.Add($"Служба {serviceName} уже запущена.", LogType.Info); return true; }
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                    Log.Add($"Служба {serviceName} запущена.", LogType.Success);
                    return true;
                }
            }
            catch (Exception ex) { Log.Add($"Ошибка запуска {serviceName}: {ex.Message}", LogType.Error); return false; }
        }

        public bool StopService(string serviceName)
        {
            try
            {
                using (var sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped) { Log.Add($"Служба {serviceName} уже остановлена.", LogType.Info); return true; }
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                    Log.Add($"Служба {serviceName} остановлена.", LogType.Success);
                    return true;
                }
            }
            catch (Exception ex) { Log.Add($"Ошибка остановки {serviceName}: {ex.Message}", LogType.Error); return false; }
        }

        public bool RestartService(string serviceName)
        {
            StopService(serviceName);
            return StartService(serviceName);
        }

        // 2 = Automatic, 3 = Manual, 4 = Disabled
        public bool SetStartupType(string serviceName, int startType)
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}", writable: true))
                {
                    if (key == null) { Log.Add($"Служба {serviceName} не найдена.", LogType.Error); return false; }
                    key.SetValue("Start", startType, RegistryValueKind.DWord);
                }
                Log.Add($"Тип запуска {serviceName} изменён на {startType}.", LogType.Success);
                return true;
            }
            catch (Exception ex) { Log.Add($"Ошибка смены типа запуска {serviceName}: {ex.Message}", LogType.Error); return false; }
        }
    }
}