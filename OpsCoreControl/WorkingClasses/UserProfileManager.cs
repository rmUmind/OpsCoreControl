using OpsCoreControl.HelperClasses;
using System;
using System.IO;
using System.Threading.Tasks;
using static OpsCoreControl.Log;

// Класс для работы с профилями пользователей:
// список профилей (папки в C:\Users) и удаление папки профиля.
namespace OpsCoreControl.WorkingClasses
{
    internal class UserProfileManager
    {
        private static bool IsProtectedProfile(string profilePath)
        {
            string fullPath;
            try { fullPath = Path.GetFullPath(profilePath).TrimEnd(Path.DirectorySeparatorChar); }
            catch { return true; }

            string usersRoot = Path.GetFullPath(Path.Combine(Environment.SystemDirectory, "..", "..", "Users")).TrimEnd(Path.DirectorySeparatorChar);
            string currentProfile = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).TrimEnd(Path.DirectorySeparatorChar);
            string name = Path.GetFileName(fullPath);
            string[] protectedNames = { "Default", "Default User", "All Users", "Public", "DefaultAppPool", "Все пользователи" };
            return !fullPath.StartsWith(usersRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || fullPath.Equals(currentProfile, StringComparison.OrdinalIgnoreCase)
                || Array.Exists(protectedNames, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        // Удаляет папку профиля (rmdir /s /q).
        public async Task<bool> DeleteProfileFolderAsync(string profilePath)
        {
            if (string.IsNullOrWhiteSpace(profilePath) || IsProtectedProfile(profilePath))
            {
                Log.Add($"Удаление защищённого или некорректного профиля запрещено: {profilePath}", LogType.Error);
                return false;
            }
            try
            {
                await Task.Run(() => Directory.Delete(profilePath, true));
                Log.Add($"Папка профиля удалена: {profilePath}", LogType.Success);
                return true;
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка удаления папки профиля {profilePath}: {ex.Message}", LogType.Error);
                return false;
            }
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
