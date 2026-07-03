using System;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace OpsCoreControl
{
    internal class ServiceManager
    {
        public ServiceManager() { }
        ~ServiceManager() { }

        public async Task rebootPrintSpooler(string serviceName, Button btn)
        {
            btn.Background = new SolidColorBrush(Colors.Yellow);
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
                    btn.Background = new SolidColorBrush(Colors.Green);
                });
            }
            catch (Exception)
            {
                btn.Background = new SolidColorBrush(Colors.Red);
                throw;
            }
        }
    }
}