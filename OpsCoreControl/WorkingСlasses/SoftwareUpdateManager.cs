using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using static OpsCoreControl.Log;

// Класс для работы с программами: список установленных (из реестра), удаление,
// запуск встроенных установщиков и скачивание файлов.
namespace OpsCoreControl.WorkingСlasses
{
    // Модель установленной программы.
    public class InstalledProgram
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Publisher { get; set; }
        public string UninstallString { get; set; }
        public override string ToString() => $"{Name}  {Version}  —  {Publisher}";
    }

    internal class SoftwareManager
    {
        // Читает установленные программы из реестра (HKLM, WOW6432Node, HKCU) и убирает дубли.
        public List<InstalledProgram> GetInstalledPrograms()
        {
            var result = new List<InstalledProgram>();
            CollectFromRegistry(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", result);
            CollectFromRegistry(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", result);
            CollectFromRegistry(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", result);

            return result
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .GroupBy(p => p.Name + "|" + p.Version)
                .Select(g => g.First())
                .OrderBy(p => p.Name)
                .ToList();
        }

        // Читает программы из одного раздела реестра.
        private void CollectFromRegistry(RegistryKey root, string path, List<InstalledProgram> list)
        {
            try
            {
                using (RegistryKey key = root.OpenSubKey(path))
                {
                    if (key == null) return;
                    foreach (string subName in key.GetSubKeyNames())
                    {
                        using (RegistryKey sub = key.OpenSubKey(subName))
                        {
                            if (sub == null) continue;
                            string name = sub.GetValue("DisplayName")?.ToString();
                            if (string.IsNullOrWhiteSpace(name)) continue; // служебные записи без имени пропускаем
                            list.Add(new InstalledProgram
                            {
                                Name = name,
                                Version = sub.GetValue("DisplayVersion")?.ToString() ?? "",
                                Publisher = sub.GetValue("Publisher")?.ToString() ?? "",
                                UninstallString = sub.GetValue("UninstallString")?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Не удалось прочитать раздел реестра {path}: {ex.Message}", LogType.Error);
            }
        }

        // Запускает удаление программы через её UninstallString.
        public bool UninstallProgram(InstalledProgram program)
        {
            if (string.IsNullOrWhiteSpace(program.UninstallString))
            {
                Log.Add($"Для '{program.Name}' не найдена строка удаления.", LogType.Error);
                return false;
            }
            try
            {
                string uninstall = program.UninstallString;
                if (uninstall.IndexOf("msiexec", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    uninstall = uninstall.Replace("/I", "/X").Replace("/i", "/x");   // MSI: install → uninstall
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {uninstall}",
                    UseShellExecute = false,
                    CreateNoWindow = false
                });
                Log.Add($"Запущено удаление: {program.Name}", LogType.Info);
                return true;
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка удаления '{program.Name}': {ex.Message}", LogType.Error);
                return false;
            }
        }

        // Достаёт встроенный установщик из ресурсов сборки и запускает его (с UAC).
        // .exe запускается напрямую; .msi — через msiexec.exe /i (иначе verb runas не поднимает установку).
        public async Task<bool> RunEmbeddedInstallerAsync(string resourceName, string fileName)
        {
            if (!IsEmbeddedResourceAvailable(resourceName))
            {
                Log.Add($"Встроенный ресурс не найден: {resourceName}. Возможно, файл не добавлен в проект.", LogType.Error);
                return false;
            }

            // Расширение оригинала определяет тип установщика (.exe или .msi).
            string originalExt = Path.GetExtension(resourceName);
            bool isMsi = string.Equals(originalExt, ".msi", StringComparison.OrdinalIgnoreCase);

            // Временный файл сохраняем с тем же расширением, что оригинал (для msi это критично).
            string tempName = fileName;
            if (!string.Equals(Path.GetExtension(tempName), originalExt, StringComparison.OrdinalIgnoreCase))
                tempName = Path.ChangeExtension(tempName, originalExt);

            string tempFilePath = null;
            try
            {
                // 1. Получаем поток ресурса из сборки.
                var assembly = Assembly.GetExecutingAssembly();
                using (var resourceStream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (resourceStream == null)
                    {
                        Log.Add($"Ресурс не найден: {resourceName}", LogType.Error);
                        return false;
                    }

                    // 2. Сохраняем во временный файл.
                    tempFilePath = Path.Combine(Path.GetTempPath(), tempName);
                    Log.Add($"Извлечение ресурса во временный файл: {tempFilePath}", LogType.Info);

                    using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await resourceStream.CopyToAsync(fileStream);
                    }
                }

                Log.Add($"Ресурс успешно извлечён: {tempFilePath}", LogType.Success);

                // 3. Готовим запуск с правами администратора.
                var psi = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    Verb = "runas" // запрос UAC
                };

                if (isMsi)
                {
                    // msi ставим через msiexec — это даёт валидный процесс и корректный UAC.
                    psi.FileName = "msiexec.exe";
                    psi.Arguments = $"/i \"{tempFilePath}\"";
                }
                else
                {
                    psi.FileName = tempFilePath;
                }

                Log.Add($"Запуск установщика: {tempName}", LogType.Info);

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        Log.Add("Не удалось запустить установщик (process == null)", LogType.Error);
                        return false;
                    }

                    // Ждём завершения в фоновом потоке, чтобы не вешать UI.
                    await Task.Run(() => process.WaitForExit());

                    int code = process.ExitCode;
                    // Для msi код 3010 = установка прошла, но нужна перезагрузка — это успех.
                    bool ok = (code == 0) || (isMsi && code == 3010);

                    if (ok)
                    {
                        string note = (isMsi && code == 3010) ? " (требуется перезагрузка)" : "";
                        Log.Add($"Установщик успешно завершён. Код: {code}{note}", LogType.Success);
                        return true;
                    }
                    else
                    {
                        Log.Add($"Установщик завершился с ошибкой. Код: {code}", LogType.Error);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка при запуске установщика: {ex.Message}", LogType.Error);
                return false;
            }
        }

        // Скачивает файл по URL в указанную папку.
        public async Task<bool> DownloadFileAsync(string url, string directory, string fileName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                    fileName = Path.GetFileName(new Uri(url).AbsolutePath);

                string destinationPath = Path.Combine(directory, fileName);

                Log.Add($"Начинаем скачивание: {url} -> {destinationPath}", LogType.Info);

                using (var client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    // Читаем из сети и пишем в файл потоком, не грузя всё в память.
                    using (var networkStream = await response.Content.ReadAsStreamAsync())
                    {
                        using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await networkStream.CopyToAsync(fileStream);
                        }
                    }
                }

                Log.Add($"Файл успешно сохранён: {destinationPath}", LogType.Success);
                return true;
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка при скачивании файла: {ex.Message}", LogType.Error);
                return false;
            }
        }

        // Проверяет, есть ли встроенный ресурс в сборке.
        public bool IsEmbeddedResourceAvailable(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                return stream != null;
            }
        }
    }
}