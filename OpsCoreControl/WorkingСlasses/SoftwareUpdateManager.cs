using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static OpsCoreControl.Log;

namespace OpsCoreControl.WorkingСlasses
{
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
                            if (string.IsNullOrWhiteSpace(name)) continue;
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
            catch { }
        }



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



        public async Task<bool> RunEmbeddedInstallerAsync(string resourceName, string fileName)
        {
            if (!IsEmbeddedResourceAvailable(resourceName))
            {
                Log.Add($"Встроенный ресурс не найден: {resourceName}. Возможно, файл не добавлен в проект.", LogType.Error);
                return false;
            }

            string tempFilePath = null;
            try
            {
                // 1. Получаем поток ресурса из сборки
                var assembly = Assembly.GetExecutingAssembly();
                using (var resourceStream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (resourceStream == null)
                    {
                        Log.Add($"Ресурс не найден: {resourceName}", LogType.Error);
                        return false;
                    }

                    // 2. Сохраняем во временный файл
                    tempFilePath = Path.Combine(Path.GetTempPath(), fileName);
                    Log.Add($"Извлечение ресурса во временный файл: {tempFilePath}", LogType.Info);

                    using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await resourceStream.CopyToAsync(fileStream);
                    }
                }

                Log.Add($"Ресурс успешно извлечён: {tempFilePath}", LogType.Success);

                // 3. Запускаем установщик с правами администратора
                var psi = new ProcessStartInfo
                {
                    FileName = tempFilePath,
                    UseShellExecute = true,
                    Verb = "runas", // запуск от администратора (подтверждение UAC)
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Log.Add($"Запуск установщика: {fileName}", LogType.Info);

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        Log.Add("Не удалось запустить установщик (process == null)", LogType.Error);
                        return false;
                    }

                    // Дожидаемся завершения установки в фоновом потоке, чтобы не вешать UI
                    await Task.Run(() => process.WaitForExit());

                    if (process.ExitCode == 0)
                    {
                        Log.Add($"Установщик успешно завершён. Код: {process.ExitCode}", LogType.Success);
                        return true;
                    }
                    else
                    {
                        Log.Add($"Установщик завершился с ошибкой. Код: {process.ExitCode}", LogType.Error);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка при запуске установщика: {ex.Message}", LogType.Error);
                return false;
            }
            finally
            { };
        }
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

                    // Открываем поток для чтения данных из интернета
                    using (var networkStream = await response.Content.ReadAsStreamAsync())
                    {
                        // Создаём файл на диске и открываем поток для записи
                        using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            // Копируем все байты из интернета в файл
                            await networkStream.CopyToAsync(fileStream);
                        }
                    }
                }

                Log.Add($"Файл успешно сохранён: {destinationPath}", LogType.Success);
                return true;
            }
            catch (Exception ex)
            {
                Log.Add($"Исключение при скачивании файла: {ex.Message}", LogType.Error);
                return false;
            }
        }
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
