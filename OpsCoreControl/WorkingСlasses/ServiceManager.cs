using Microsoft.Win32;
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