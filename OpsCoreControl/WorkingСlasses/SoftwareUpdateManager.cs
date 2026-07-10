using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static OpsCoreControl.Log;

namespace OpsCoreControl.WorkingСlasses
{
    internal class SoftwareUpdateManager
    {
        public async Task<bool> RunEmbeddedInstallerAsync(string resourceName, string fileName)
        {
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
            { }
    ;
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
    }
}
