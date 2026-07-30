using OpsCoreControl.HelperClasses;
using System;
using System.IO;
using System.Threading.Tasks;
using static OpsCoreControl.Log;

// Класс для работы с профилями пользователей:
// список профилей (папки в C:\Users) и удаление папки профиля.
namespace OpsCoreControl.WorkingСlasses
{
    internal class UserProfileManager
    {
        // Удаляет папку профиля (rmdir /s /q).
        public async Task<bool> DeleteProfileFolderAsync(string profilePath)
        {
            var psi = ConsoleHelper.CmdConsoleCommand($"/c rmdir /s /q \"{profilePath}\"");
            return await ConsoleHelper.LookForProcessEnd(psi,
                $"Папка профиля удалена: {profilePath}",
                $"Ошибка удаления папки профиля: {profilePath}",
                "Исключение при удалении папки профиля.");
        }

        // Читает список профилей (папки в C:\Users) и шлёт их через лог типа Profile.
        public async Task<bool> LoadUserProfiles()
        {
            try
            {
                var path = "C:\\Users";
                string[] directories = await Task.Run(() => Directory.GetDirectories(path));
                foreach (var directory in directories)
                    Log.Add(directory, LogType.Profile);
                Log.Add("Профили успешно получены.", LogType.Success);
                return true;
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка при получении профилей пользователей: {ex.Message}", LogType.Error);
                return false;
            }
        }
    }
}