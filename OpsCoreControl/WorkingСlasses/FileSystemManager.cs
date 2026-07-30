using Microsoft.Win32;
using OpsCoreControl.HelperClasses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shell;
using static OpsCoreControl.Log;

// Класс для работы с файловой системой и дисками:
// очистка папок (Загрузки, Temp), информация о дисках и SMART,
// управление файлом подкачки, открытие сетевых путей.
namespace OpsCoreControl.WorkingСlasses
{
    internal class FileSystemManager
    {
        // Модель состояния физического диска (SMART).
        public class DiskHealthInfo
        {
            public string Model { get; set; }
            public string InterfaceType { get; set; }
            public string SizeGb { get; set; }
            public string Status { get; set; }
            public string SerialNumber { get; set; }
            public override string ToString() => $"{Model}  [{InterfaceType}]  {SizeGb} ГБ  —  SMART: {Status}";
        }

        // Возвращает состояние физических дисков (аналог wmic diskdrive get status).
        public List<DiskHealthInfo> GetDiskHealth()
        {
            var result = new List<DiskHealthInfo>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Model, InterfaceType, Size, Status, SerialNumber FROM Win32_DiskDrive"))
                {
                    foreach (ManagementObject disk in searcher.Get())
                    {
                        string sizeGb = "0";
                        if (ulong.TryParse(disk["Size"]?.ToString(), out ulong bytes))
                            sizeGb = (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F0");

                        result.Add(new DiskHealthInfo
                        {
                            Model = disk["Model"]?.ToString() ?? "—",
                            InterfaceType = disk["InterfaceType"]?.ToString() ?? "—",
                            SizeGb = sizeGb,
                            Status = disk["Status"]?.ToString() ?? "—",
                            SerialNumber = disk["SerialNumber"]?.ToString()?.Trim() ?? "—"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка получения SMART-статуса: {ex.Message}", LogType.Error);
            }
            return result;
        }

        // Очищает папку "Загрузки". Занятые файлы пропускает, не прерывая очистку.
        public async Task<bool> CleanDownloadFolder()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

            if (!Directory.Exists(path))
            {
                Log.Add($"Папка не найдена: {path}", LogType.Error);
                return false;
            }

            int deleted = 0;
            int skipped = 0;

            await Task.Run(() =>
            {
                foreach (string file in Directory.GetFiles(path))
                {
                    try { File.Delete(file); deleted++; }
                    catch { skipped++; } // файл занят — пропускаем
                }
                foreach (string dir in Directory.GetDirectories(path))
                {
                    try { Directory.Delete(dir, true); deleted++; }
                    catch { skipped++; }
                }
            });

            Log.Add($"Очистка {path}: удалено {deleted}, пропущено {skipped}.", LogType.Success);
            return true;
        }

        // Очищает временные папки. Занятые файлы пропускает.
        public async Task<bool> CleanTempFolder()
        {
            var paths = new List<string>
            {
                Path.GetTempPath(),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")
            };

            await Task.Run(() =>
            {
                foreach (var path in paths)
                {
                    if (!Directory.Exists(path))
                    {
                        Log.Add($"Папка не найдена: {path}", LogType.Info);
                        continue;
                    }

                    int deleted = 0;
                    int skipped = 0;

                    foreach (string file in Directory.GetFiles(path))
                    {
                        try
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                            File.Delete(file);
                            deleted++;
                        }
                        catch { skipped++; } // файл занят — пропускаем
                    }

                    foreach (string dir in Directory.GetDirectories(path))
                    {
                        try
                        {
                            Directory.Delete(dir, true);
                            deleted++;
                        }
                        catch { skipped++; }
                    }

                    Log.Add($"Очистка {path}: удалено {deleted}, пропущено {skipped}.", LogType.Success);
                }
            });

            return true;
        }

        // Открывает сетевую папку в Проводнике. Сам дописывает \\, если их нет.
        public async Task<bool> OpenNetworkPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Log.Add("Не указан путь к сетевой папке.", LogType.Error);
                return false;
            }

            try
            {
                string uncPath = path;
                if (!uncPath.StartsWith(@"\\"))
                {
                    uncPath = @"\\" + uncPath.TrimStart('\\');
                }

                Log.Add($"Открываем сетевую папку: {uncPath}", LogType.Info);
                await Task.Run(() => Process.Start("explorer.exe", $"\"{uncPath}\""));
                Log.Add($"Проводник открыт по адресу: {uncPath}", LogType.Success);
                return true;
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка открытия сетевой папки: {ex.Message}", LogType.Error);
                return false;
            }
        }

        // Возвращает список готовых логических дисков с информацией об объёме.
        public async Task<List<DriveInfo>> GetDiskInfo()
        {
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                if (drives.Length == 0)
                {
                    Log.Add("Логические диски не обнаружены.", LogType.Info);
                    return new List<DriveInfo>();
                }

                List<DriveInfo> disks = new List<DriveInfo>();
                foreach (DriveInfo drive in drives)
                {
                    if (!drive.IsReady)
                    {
                        Log.Add($"{drive.Name} - не готов (тип: {drive.DriveType})", LogType.Info);
                        continue;
                    }

                    string volumeLabel = string.IsNullOrEmpty(drive.VolumeLabel) ? "Без метки" : drive.VolumeLabel;
                    string driveType = GetDriveTypeDescription(drive.DriveType);
                    long totalSize = drive.TotalSize;
                    long freeSpace = drive.TotalFreeSpace;
                    double freePercent = totalSize > 0 ? (double)freeSpace / totalSize * 100.0 : 0;

                    string totalGB = (totalSize / (1024.0 * 1024.0 * 1024.0)).ToString("F1");
                    string freeGB = (freeSpace / (1024.0 * 1024.0 * 1024.0)).ToString("F1");

                    string message = $"{drive.Name} [{volumeLabel}] {driveType}: {totalGB} ГБ всего, {freeGB} ГБ свободно ({freePercent:F1}%)";
                    disks.Add(drive);
                    Log.Add(message, LogType.Info);
                }
                return disks;
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка при получении информации о дисках: {ex.Message}", LogType.Error);
            }
            return new List<DriveInfo>();
        }

        // Устанавливает размер файла подкачки на диске. Другие диски не трогает.
        public async Task<bool> SetPageFileSize(string driveLetter, int minMB, int maxMB)
        {
            try
            {
                // Проверяем, что диск подходит для pagefile.
                var drive = new DriveInfo(driveLetter.TrimEnd('\\'));

                if (!drive.IsReady)
                {
                    Log.Add($"Диск {driveLetter} не готов.", LogType.Error);
                    return false;
                }
                if (drive.DriveType != DriveType.Fixed)
                {
                    Log.Add($"Диск {driveLetter} не является жёстким (тип: {drive.DriveType}).", LogType.Error);
                    return false;
                }
                if (drive.DriveFormat != "NTFS")
                {
                    Log.Add($"Диск {driveLetter}: файловая система {drive.DriveFormat}. Нужна NTFS.", LogType.Error);
                    return false;
                }
                long freeMB = drive.TotalFreeSpace / (1024 * 1024);
                if (maxMB > freeMB)
                {
                    Log.Add($"Диск {driveLetter}: свободно {freeMB} МБ, запрошено max {maxMB} МБ.", LogType.Error);
                    return false;
                }

                await Task.Run(() =>
                {
                    // 1. Отключаем автоуправление файлом подкачки (WMI).
                    using (var cs = new ManagementObject(
                        "Win32_ComputerSystem.Name=\"" + Environment.MachineName + "\""))
                    {
                        cs["AutomaticManagedPagefile"] = false;
                        cs.Put();
                    }

                    // 2. Пишем в реестр — то же, что делает Windows UI.
                    string pagefilePath = driveLetter.TrimEnd('\\') + "\\pagefile.sys";
                    string regPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";

                    using (var key = Registry.LocalMachine.OpenSubKey(regPath, writable: true))
                    {
                        var entries = new List<string>();

                        // Читаем существующие записи, пропускаем наш диск.
                        if (key.GetValue("PagingFiles") is string[] existing)
                        {
                            foreach (string entry in existing)
                            {
                                if (!entry.StartsWith(pagefilePath, StringComparison.OrdinalIgnoreCase))
                                {
                                    entries.Add(entry);
                                }
                            }
                        }

                        // Добавляем нашу запись: "D:\pagefile.sys 10000 20000".
                        entries.Add($"{pagefilePath} {minMB} {maxMB}");

                        key.SetValue("PagingFiles", entries.ToArray(), RegistryValueKind.MultiString);
                    }
                });

                Log.Add($"Файл подкачки установлен: {driveLetter} min={minMB} МБ, max={maxMB} МБ.", LogType.Success);
                return true;
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка настройки файла подкачки: {ex.Message}", LogType.Error);
                return false;
            }
        }

        // Перевод типа диска в понятное описание.
        private string GetDriveTypeDescription(DriveType driveType)
        {
            switch (driveType)
            {
                case DriveType.Fixed: return "Жёсткий диск";
                case DriveType.Removable: return "Съёмный диск";
                case DriveType.CDRom: return "CD/DVD-ROM";
                case DriveType.Network: return "Сетевой диск";
                case DriveType.Ram: return "RAM диск";
                default: return "Неизвестный тип";
            }
        }

        // Читает текущие записи pagefile из реестра и свободное место на дисках.
        public async Task<List<string>> GetPageFileInfo()
        {
            var result = new List<string>();

            await Task.Run(() =>
            {
                string regPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";

                using (var key = Registry.LocalMachine.OpenSubKey(regPath))
                {
                    if (key.GetValue("PagingFiles") is string[] entries)
                    {
                        foreach (string entry in entries)
                        {
                            // "D:\pagefile.sys 10000 20000" → parts[0]=путь, parts[1]=min, parts[2]=max.
                            string[] parts = entry.Split(' ');
                            string path = parts[0];

                            string sizes = parts.Length >= 3
                                ? $"min={parts[1]} МБ, max={parts[2]} МБ"
                                : "по выбору системы";

                            string freeSpace = "диск недоступен";
                            try
                            {
                                string root = Path.GetPathRoot(path);
                                var drive = new DriveInfo(root);
                                if (drive.IsReady)
                                {
                                    long freeMB = drive.TotalFreeSpace / (1024 * 1024);
                                    freeSpace = $"свободно {freeMB} МБ";
                                }
                            }
                            catch { } // диск мог быть недоступен — оставим "диск недоступен"

                            result.Add($"{path} | {sizes} | {freeSpace}");
                        }
                    }
                }
            });

            return result;
        }

        // Удаляет запись pagefile для диска. Другие диски не трогает.
        public async Task<bool> ClearPageFile(string driveLetter)
        {
            try
            {
                await Task.Run(() =>
                {
                    string pagefilePath = driveLetter.TrimEnd('\\') + "\\pagefile.sys";
                    string regPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";

                    using (var key = Registry.LocalMachine.OpenSubKey(regPath, writable: true))
                    {
                        var entries = new List<string>();

                        if (key.GetValue("PagingFiles") is string[] existing)
                        {
                            foreach (string entry in existing)
                            {
                                if (!entry.StartsWith(pagefilePath, StringComparison.OrdinalIgnoreCase))
                                {
                                    entries.Add(entry);
                                }
                            }
                        }

                        key.SetValue("PagingFiles", entries.ToArray(), RegistryValueKind.MultiString);
                    }
                });

                Log.Add($"Файл подкачки удалён с диска {driveLetter}.", LogType.Success);
                return true;
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка удаления файла подкачки: {ex.Message}", LogType.Error);
                return false;
            }
        }

        // Переводит pagefile на диске в режим "по выбору системы".
        public async Task<bool> SetPageFileAuto(string driveLetter)
        {
            try
            {
                var drive = new DriveInfo(driveLetter.TrimEnd('\\'));
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed || drive.DriveFormat != "NTFS")
                {
                    Log.Add($"Диск {driveLetter} не подходит для pagefile.", LogType.Error);
                    return false;
                }

                await Task.Run(() =>
                {
                    using (var cs = new ManagementObject(
                        "Win32_ComputerSystem.Name=\"" + Environment.MachineName + "\""))
                    {
                        cs["AutomaticManagedPagefile"] = false;
                        cs.Put();
                    }

                    string pagefilePath = driveLetter.TrimEnd('\\') + "\\pagefile.sys";
                    string regPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";

                    using (var key = Registry.LocalMachine.OpenSubKey(regPath, writable: true))
                    {
                        var entries = new List<string>();

                        if (key.GetValue("PagingFiles") is string[] existing)
                        {
                            foreach (string entry in existing)
                            {
                                if (!entry.StartsWith(pagefilePath, StringComparison.OrdinalIgnoreCase))
                                {
                                    entries.Add(entry);
                                }
                            }
                        }

                        // Без min/max — значит "по выбору системы".
                        entries.Add(pagefilePath);

                        key.SetValue("PagingFiles", entries.ToArray(), RegistryValueKind.MultiString);
                    }
                });

                Log.Add($"Файл подкачки на {driveLetter}: режим 'по выбору системы'.", LogType.Success);
                return true;
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка установки режима 'по выбору системы': {ex.Message}", LogType.Error);
                return false;
            }
        }
    }
}