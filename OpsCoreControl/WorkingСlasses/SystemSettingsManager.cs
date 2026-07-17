using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static OpsCoreControl.Log;

namespace OpsCoreControl.WorkingСlasses
{
    internal class SystemSettingsManager
    {
        private const string DesktopKey = @"Control Panel\Desktop";

        // P/Invoke для SystemParametersInfo
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(int uiAction, int uiParam, IntPtr pvParam, int fWinIni);

        // Константы
        private const int SPI_SETSCREENSAVEACTIVE = 0x0011;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDWININICHANGE = 0x02;

        /// <summary>
        /// Устанавливает время бездействия до блокировки экрана (скринсейвер с паролем).
        /// Работает на Windows 7/10/11. Изменения применяются мгновенно.
        /// </summary>
        /// <param name="minutes">Время в минутах (минимум 1)</param>
        public bool SetScreenLockTimeout(int minutes)
        {
            try
            {
                int seconds = minutes * 60;
                if (seconds < 60) seconds = 60;

                // Путь к стандартной чёрной заставке в System32 (без проверки File.Exists)
                string screenSaverPath = @"C:\Windows\System32\scrnsave.scr";

                Log.Add($"Устанавливаем скринсейвер: {screenSaverPath}, таймаут: {seconds} сек.", LogType.Info);

                // Запись в реестр
                using (var key = Registry.CurrentUser.OpenSubKey(DesktopKey, writable: true))
                {
                    if (key == null)
                    {
                        Log.Add("Не удалось открыть раздел реестра Control Panel\\Desktop", LogType.Error);
                        return false;
                    }

                    key.SetValue("SCRNSAVE.EXE", screenSaverPath, RegistryValueKind.String);
                    key.SetValue("ScreenSaveActive", "1", RegistryValueKind.String);
                    key.SetValue("ScreenSaverIsSecure", "1", RegistryValueKind.String);
                    key.SetValue("ScreenSaveTimeOut", seconds.ToString(), RegistryValueKind.String);
                }

                // Включаем заставку с новыми параметрами
                bool success = SystemParametersInfo(SPI_SETSCREENSAVEACTIVE, 1, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
                if (!success)
                {
                    int error = Marshal.GetLastWin32Error();
                    Log.Add($"SystemParametersInfo не удался (код ошибки: {error})", LogType.Error);
                    return false;
                }

                // Принудительное обновление пользовательских параметров (работает сразу)
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