using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static OpsCoreControl.Log;

// Класс для настройки параметров системы.
// Пока умеет устанавливать время до блокировки экрана (скринсейвер с паролем).
namespace OpsCoreControl.WorkingСlasses
{
    internal class SystemSettingsManager
    {
        // Раздел реестра с настройками рабочего стола (скринсейвер и т.п.).
        private const string DesktopKey = @"Control Panel\Desktop";

        // P/Invoke для SystemParametersInfo — применяет параметры системы.
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(int uiAction, int uiParam, IntPtr pvParam, int fWinIni);

        // Константы для SystemParametersInfo.
        private const int SPI_SETSCREENSAVEACTIVE = 0x0011;  // включить/выключить скринсейвер
        private const int SPIF_UPDATEINIFILE = 0x01;         // сохранить в профиль пользователя
        private const int SPIF_SENDWININICHANGE = 0x02;      // разослать уведомление об изменении

        // Устанавливает время бездействия до блокировки экрана (скринсейвер с паролем).
        // Работает на Windows 7/10/11, применяется мгновенно. minutes — время в минутах (минимум 1).
        public bool SetScreenLockTimeout(int minutes)
        {
            try
            {
                int seconds = minutes * 60;
                if (seconds < 60) seconds = 60; // минимум 1 минута

                // Путь к стандартной чёрной заставке в System32 (без проверки File.Exists).
                string screenSaverPath = @"C:\Windows\System32\scrnsave.scr";

                Log.Add($"Устанавливаем скринсейвер: {screenSaverPath}, таймаут: {seconds} сек.", LogType.Info);

                // Пишем параметры скринсейвера в реестр.
                using (var key = Registry.CurrentUser.OpenSubKey(DesktopKey, writable: true))
                {
                    if (key == null)
                    {
                        Log.Add("Не удалось открыть раздел реестра Control Panel\\Desktop", LogType.Error);
                        return false;
                    }

                    key.SetValue("SCRNSAVE.EXE", screenSaverPath, RegistryValueKind.String);
                    key.SetValue("ScreenSaveActive", "1", RegistryValueKind.String);
                    key.SetValue("ScreenSaverIsSecure", "1", RegistryValueKind.String);   // требовать пароль при выходе из заставки
                    key.SetValue("ScreenSaveTimeOut", seconds.ToString(), RegistryValueKind.String);
                }

                // Включаем заставку с новыми параметрами.
                bool success = SystemParametersInfo(SPI_SETSCREENSAVEACTIVE, 1, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
                if (!success)
                {
                    int error = Marshal.GetLastWin32Error();
                    Log.Add($"SystemParametersInfo не удался (код ошибки: {error})", LogType.Error);
                    return false;
                }

                // Принудительно обновляем пользовательские параметры, чтобы применилось сразу.
                Process.Start("rundll32.exe", "user32.dll,UpdatePerUserSystemParameters");

                Log.Add($"Время до блокировки экрана установлено: {minutes} мин.", LogType.Success);
                return true;
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка установки времени блокировки: {ex.Message}", LogType.Error);
                return false;
            }
        }
    }
}