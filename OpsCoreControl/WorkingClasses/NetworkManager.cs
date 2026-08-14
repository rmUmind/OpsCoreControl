using Microsoft.Win32;
using OpsCoreControl.HelperClasses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using static OpsCoreControl.Log;

// Класс для работы с сетевыми дисками и сброса сети:
// подключение/отключение дисков (net use), смена метки, список дисков,
// видимость дисков между сессиями и сброс сети (winsock / IP / DNS).
namespace OpsCoreControl.WorkingClasses
{
    internal class NetworkManager
    {
        // Подключает сетевую шару как диск через net use.
        // Если буква не указана — подбирает свободную.
        public async Task<bool> MapNetworkDrive(string letter, string uncPath, bool persistent = true)
        {
            if (string.IsNullOrWhiteSpace(uncPath))
            {
                Log.Add("Не указан сетевой путь.", LogType.Error);
                return false;
            }

            // Приводим путь к виду \\server\share.
            if (!uncPath.StartsWith(@"\\"))
            {
                uncPath = @"\\" + uncPath.TrimStart('\\');
            }

            if (uncPath.IndexOf('"') >= 0 || uncPath.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                Log.Add("Сетевой путь содержит недопустимые символы.", LogType.Error);
                return false;
            }

            // Без имени шары подключить нельзя — только сервер не мапится.
            string withoutPrefix = uncPath.Substring(2);
            if (!withoutPrefix.Contains('\\'))
            {
                Log.Add($"Нужно имя шары: {uncPath}\\имя_шары. Сервер без шары подключить как диск нельзя.", LogType.Error);
                return false;
            }

            if (string.IsNullOrWhiteSpace(letter))
            {
                letter = GetFreeDriveLetter();
                if (letter == null)
                {
                    Log.Add("Нет свободных букв для дисков.", LogType.Error);
                    return false;
                }
            }
            letter = letter.Trim();
            if (letter.Length > 0 && (letter.Length > 2 || !char.IsLetter(letter[0]) || (letter.Length == 2 && letter[1] != ':')))
            {
                Log.Add("Буква диска должна иметь вид Z или Z:.", LogType.Error);
                return false;
            }
            string key = letter.EndsWith(":") ? letter : letter + ":";

            string command = $"/c net use {key} \"{uncPath}\" /persistent:{(persistent ? "yes" : "no")}";
            var psi = ConsoleHelper.CmdConsoleCommand(command);
            return await ConsoleHelper.LookForProcessEnd(psi,
                $"Шара {uncPath} подключена как диск {key}",
                $"Не удалось подключить {uncPath} как диск {key}",
                timeoutMs: 5000);
        }

        // Отключает сетевой диск.
        public async Task<bool> UnmapNetworkDrive(string letter)
        {
            if (string.IsNullOrWhiteSpace(letter) || letter.Length > 2 || !char.IsLetter(letter[0]))
            { Log.Add("Некорректная буква диска.", LogType.Error); return false; }
            string key = letter.EndsWith(":") ? letter : letter + ":";
            string command = $"/c net use {key} /delete /y";
            var psi = ConsoleHelper.CmdConsoleCommand(command);
            return await ConsoleHelper.LookForProcessEnd(psi,
                $"Диск {key} отключён",
                $"Не удалось отключить {key}",
                timeoutMs: 5000);
        }

        // Меняет метку диска (команда label).
        public async Task<bool> RenameLogicalDisk(string letter, string newName)
        {
            if (string.IsNullOrWhiteSpace(letter) || letter.Length > 2 || !char.IsLetter(letter[0]))
            { Log.Add("Некорректная буква диска.", LogType.Error); return false; }
            if (string.IsNullOrWhiteSpace(newName) || newName.IndexOf('"') >= 0 || newName.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            { Log.Add("Метка диска содержит недопустимые символы.", LogType.Error); return false; }
            string key = letter.EndsWith(":") ? letter : letter + ":";
            string command = $"/c label {key} \"{newName}\"";
            var psi = ConsoleHelper.CmdConsoleCommand(command);
            return await ConsoleHelper.LookForProcessEnd(psi,
                $"Метка диска {key} изменена на \"{newName}\"",
                $"Не удалось изменить метку {key}",
                timeoutMs: 5000);
        }

        // Возвращает список логических дисков с типом, меткой и UNC для сетевых.
        public List<string> GetLogicalDrives()
        {
            var result = new List<string>();
            try
            {
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
                        if (!string.IsNullOrEmpty(provider)) line += $" → {provider}";

                        result.Add(line);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Не удалось получить список логических дисков: {ex.Message}", LogType.Error);
            }
            return result;
        }

        // Включает видимость сетевых дисков между сессиями (идемпотентно).
        // Для применения нужна однократная перезагрузка.
        public void EnsureLinkedConnectionsEnabled()
        {
            const string regPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
            try
            {
                using (var key = Registry.LocalMachine.CreateSubKey(regPath))
                {
                    if (key == null) { Log.Add("Не удалось открыть системный раздел реестра.", LogType.Error); return; }
                    if (key.GetValue("EnableLinkedConnections") is int value && value == 1)
                    {
                        return;   // уже включено
                    }
                    key.SetValue("EnableLinkedConnections", 1, RegistryValueKind.DWord);
                    Log.Add("Включена видимость сетевых дисков между сессиями. Нужна однократная перезагрузка.", LogType.Info);
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Не удалось включить EnableLinkedConnections: {ex.Message}", LogType.Error);
            }
        }

        // Подбирает свободную букву диска, перебирая с Z вниз (A-C не трогаем).
        private string GetFreeDriveLetter()
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var drive in DriveInfo.GetDrives())
            {
                used.Add(drive.Name.TrimEnd('\\'));
            }
            for (char c = 'Z'; c >= 'D'; c--)
            {
                string letter = c + ":";
                if (!used.Contains(letter)) return letter;
            }
            return null;
        }

        // Перевод кода типа диска из WMI в понятное имя.
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

        // Сброс сети: winsock, TCP/IP и очистка DNS-кэша.
        // Название метода историческое — по смыслу это именно сброс сети, а не очистка пула.
        public async Task<bool> ResetNetwork()
        {
            var commands = new List<string>
            {
                "/c netsh winsock reset",
                "/c netsh int ip reset",
                "/c ipconfig /flushdns"
            };

            bool allOk = true;
            foreach (string command in commands)
            {
                var psi = ConsoleHelper.CmdConsoleCommand(command);
                bool ok = await ConsoleHelper.LookForProcessEnd(psi,
                    $"Выполнено: {command}",
                    $"Не удалось выполнить: {command}",
                    timeoutMs: 10000);
                if (!ok)
                {
                    allOk = false;
                }
            }

            if (allOk)
            {
                Log.Add("Сброс сети выполнен. Для полного применения рекомендуется перезагрузка.", LogType.Success);
            }
            return allOk;
        }
    }
}
