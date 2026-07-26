using OpsCoreControl.HelperClasses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using static OpsCoreControl.Log;

namespace OpsCoreControl.WorkingСlasses
{
    internal class NetworkManager
    {
        public async Task<bool> ClearNonPagedPool ()
        {
            var psi = ConsoleHelper.CmdConsoleCommand("\"/c netsh winsock reset & netsh int ip reset & ipconfig /release & ipconfig /renew & ipconfig /flushdns\"");
            return await ConsoleHelper.LookForProcessEnd(psi, "Невыгружаемый пул успешно удален", "Ошибка при удаление папки профился", "Исключение при удаление невыгружаемого пула: ");
        }

        // Подключить сетевой диск
        public async Task<bool> MapNetworkDrive(string letter, string uncPath, bool persistent = true)
        {
            string key = letter.EndsWith(":") ? letter : letter + ":";
            string command = $"/c net use {key} {uncPath} /persistent:{(persistent ? "yes" : "no")}";
            var psi = ConsoleHelper.CmdConsoleCommand(command);
            return await ConsoleHelper.LookForProcessEnd(psi,
                $"Диск {key} подключён к {uncPath}",
                $"Не удалось подключить {key}"
                ,timeoutMs: 5000);
        }

        // Отключить сетевой диск
        public async Task<bool> UnmapNetworkDrive(string letter)
        {
            string key = letter.EndsWith(":") ? letter : letter + ":";
            string command = $"/c net use {key} /delete /y";
            var psi = ConsoleHelper.CmdConsoleCommand(command);
            return await ConsoleHelper.LookForProcessEnd(psi,
                $"Диск {key} отключён",
                $"Не удалось отключить {key}");
        }

        // Список подключённых сетевых дисков (буква + UNC)
        public List<string> GetLogicalDrives()
        {
            var result = new List<string>();
            using (var searcher = new ManagementObjectSearcher(
                "SELECT Name, DriveType, ProviderName, VolumeName FROM Win32_LogicalDisk"))
            {
                foreach (ManagementObject drive in searcher.Get())
                {
                    string name = drive["Name"]?.ToString();
                    string type = GetDriveTypeName(drive["DriveType"]);
                    string label = drive["VolumeName"]?.ToString();
                    string provider = drive["ProviderName"]?.ToString();

                    string line = $"{name} [{type}]";
                    if (!string.IsNullOrEmpty(label)) line += $" \"{label}\"";
                    if (!string.IsNullOrEmpty(provider)) line += $" → {provider}";   // UNC для сетевых

                    result.Add(line);
                }
            }
            return result;
        }

        private string GetDriveTypeName(object driveType)
        {
            switch (driveType?.ToString())
            {
                case "2": return "Съёмный";
                case "3": return "Локальный";
                case "4": return "Сетевой";
                case "5": return "CD/DVD";
                case "6": return "RAM";
                default: return "Другой";
            }
        }
    }
}
