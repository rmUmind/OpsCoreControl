using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsCoreControl.WorkingСlasses
{
    internal class FileCleanupManager
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
                Logger.Log("Исключение удаление папки: " + ex.Message, Logger.LogEntryType.Error);
            }
            Logger.Log($"Папка {path} отчишена", Logger.LogEntryType.Success);
            return true;
        }
    }
}
