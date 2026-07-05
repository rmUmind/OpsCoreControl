using System;
using System.IO;
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

        public event Action<string> ErrorMessage;
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
            }
            catch (Exception ex)
            {
                ErrorMessage?.Invoke(ex.Message);
                return false;
            }
            await Task.Delay(1000);
            return true;
        }

        public async Task<bool> CleanDownloadFolder()
        {
            try
            {
                await Task.Run(() =>
                {
                    var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                    var files = Directory.GetFiles(path);
                    var directorys = Directory.GetDirectories(path);
                    foreach (var file in files)
                    {
                        File.Delete(file);
                    }
                    foreach (var directory in directorys)
                    {
                        Directory.Delete(directory, true);
                    }
                });
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }
    }
}