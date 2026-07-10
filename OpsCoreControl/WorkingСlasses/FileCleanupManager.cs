using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpsCoreControl.Log;

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
                Log.Add("Исключение удаление папки: " + ex.Message, LogType.Error);
            }
            Log.Add($"Папка {path} отчишена", LogType.Success);
            return true;
        }
    }
}
