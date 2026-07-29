using System;
using System.Diagnostics;
using System.IO;
using static OpsCoreControl.Log;

namespace OpsCoreControl.WorkingСlasses
{
    internal class HostsManager
    {
        private static readonly string HostsPath = Path.Combine(Environment.SystemDirectory, "drivers", "etc", "hosts");

        public string ReadHosts()
        {
            try { return File.ReadAllText(HostsPath); }
            catch (Exception ex)
            {
                Log.Add($"Ошибка чтения hosts: {ex.Message}", LogType.Error);
                return "";
            }
        }

        public void OpenHostsFolder()
        {
            try
            {
                // Откроет папку etc с выделенным файлом hosts
                Process.Start("explorer.exe", $"/select,\"{HostsPath}\"");
                Log.Add("Открыта папка с файлом hosts.", LogType.Info);
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка открытия папки hosts: {ex.Message}", LogType.Error);
            }
        }
    }
}