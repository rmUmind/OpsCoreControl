using OpsCoreControl.HelperClasses;
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
            var psi = ConsoleHelper.cmdConsoleCommand("/c rmdir /s /q \"{profilePath}\"");
            return await ConsoleHelper.LookForProcessEnd(psi, "Папка профиля удалена.", "Ошибка удаления папки профиля.", "Исключение при удалении папки профиля.");
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
