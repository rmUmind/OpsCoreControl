using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static OpsCoreControl.Log;

namespace OpsCoreControl.WorkingСlasses
{
    internal class UserProfileManager
    {

        public async Task<bool> DeleteProfileFolderAsync(string profilePath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c rmdir /s /q \"{profilePath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Verb = "runas"
                };

                using (var process = Process.Start(psi))
                {
                    await Task.Run(() => process.WaitForExit());
                    if (process.ExitCode == 0)
                    {
                        Log.Add($"Папка профиля удалена: {profilePath}", LogEntryType.Success);
                        return true;
                    }
                    else
                    {
                        Log.Add($"Ошибка удаления папки профиля (код {process.ExitCode}): {profilePath}", LogEntryType.Error);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Исключение при удалении папки профиля {profilePath}: {ex.Message}", LogEntryType.Error);
                return false;
            }
        }
        public async Task<bool> LoadUserProfiles()
        {
            try
            {
                var path = "C:\\Users";
                var directorys = Directory.GetDirectories(path);
                foreach (var directory in directorys)
                    Log.Add(directory, LogType.Profile);
                Log.Add("Профили успешно получены", LogEntryType.Success);
                return true;
            }
            catch (Exception)
            {
                Log.Add("Исключение при получение профиля пользователя: ", LogType.Success);
                return false;
            }
        }
    }
}
