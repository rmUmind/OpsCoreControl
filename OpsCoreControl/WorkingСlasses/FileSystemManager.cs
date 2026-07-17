using OpsCoreControl.HelperClasses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpsCoreControl.Log;

namespace OpsCoreControl.WorkingСlasses
{
    internal class FileSystemManager
    {
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
            catch (Exception ex)
            {
                Log.Add("Исключение удаление папки: " + ex.Message, LogType.Error);
            }
            Log.Add($"Папка {path} отчишена", LogType.Success);
            return true;
        }
        public async Task<bool> OpenNetworkPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Log.Add("Не указан путь к сетевой папке.", LogType.Error);
                return false;
            }

            try
            {
                // Автоматически добавляем слеши, если пользователь ввёл просто IP
                string uncPath = path;
                if (!uncPath.StartsWith(@"\\"))
                {
                    // Добавляем "\\" в начало. Если был просто IP (например "10.10.10.10"), 
                    uncPath = @"\\" + uncPath.TrimStart('\\');
                }

                Log.Add($"Открываем сетевую папку: {uncPath}", LogType.Info);

                var psi = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = uncPath,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = false
                };

                if (!await ConsoleHelper.LookForProcessEnd(psi, $"Проводник успешно открыт по адресу: {uncPath}", "Ошибка процесса при открытие проводника."))
                    return false;
                return true;
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка открытия сетевой папки: {ex.Message}", LogType.Error);
                return false;
            }
        }
    }
}
