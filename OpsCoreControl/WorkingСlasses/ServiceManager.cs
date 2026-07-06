using Microsoft.Win32;
using System;
using System.IO;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

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
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Logger.LogEntryType.Error);
                return false;
            }
            Logger.Log($"Служба {serviceName} успешно перезапущена", Logger.LogEntryType.Success);
            return true;
        }

        public async Task<bool> CleanDownloadFolder()
        {
            string path = "";
            try
            {
                await Task.Run(() =>
                {
                    path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
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
            Logger.Log($"Папка {path} отчишена", Logger.LogEntryType.Success);
            return true;
        }

        public async Task GetUserProfiles()
        {
            var path = "C:\\Users";
            var directorys = Directory.GetDirectories(path);
            foreach (var directory in directorys)
                Logger.Log(directory, Logger.LogEntryType.Profile);

            //const string profileListKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList";
            //var profilesKey = Registry.LocalMachine.OpenSubKey(profileListKey);
            //foreach (var sidString in profilesKey.GetSubKeyNames())
            //{
            //    var sid = new System.Security.Principal.SecurityIdentifier(sidString);
            //    var ntAccount = (NTAccount)sid.Translate(typeof(NTAccount));
            //    string userName = ntAccount.Value.ToString();
            //    Logger.Log(userName);
            //}
        }
    }
}