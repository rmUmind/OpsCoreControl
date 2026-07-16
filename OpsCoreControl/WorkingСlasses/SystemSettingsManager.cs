using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using static OpsCoreControl.Log;
using System.Runtime.InteropServices;
using System.IO;

namespace OpsCoreControl.WorkingСlasses
{
    internal class SystemSettingsManager
    {
        private const string DesktopKey = @"Control Panel\Desktop";
        public bool SetScreenLockTimeout(int minutes)
        {
            try
            {
                int seconds = minutes * 60;
                if (seconds < 60) seconds = 60;

                // Путь к стандартной чёрной заставке (проверяем наличие)
                string screenSaverPath = Path.Combine(Environment.SystemDirectory, "scrnsave.scr");
                if (!File.Exists(screenSaverPath))
                {
                    // Если по какой-то причине нет, можно указать другой, но scrnsave.scr всегда есть
                    screenSaverPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "scrnsave.scr");
                }

                // Запись в реестр
                using (var key = Registry.CurrentUser.OpenSubKey(DesktopKey, writable: true))
                {
                    if (key == null)
                    {
                        Log.Add("Не удалось открыть раздел реестра для настроек рабочего стола", LogType.Error);
                        return false;
                    }

                    key.SetValue("SCRNSAVE.EXE", screenSaverPath, RegistryValueKind.String);
                    key.SetValue("ScreenSaveActive", "1", RegistryValueKind.String);
                    key.SetValue("ScreenSaverIsSecure", "1", RegistryValueKind.String);
                    key.SetValue("ScreenSaveTimeOut", seconds.ToString(), RegistryValueKind.String);
                }

                // Уведомление системы о необходимости перечитать настройки заставки
                // SPI_SETSCREENSAVEACTIVE включает заставку с новыми параметрами
                const int SPI_SETSCREENSAVEACTIVE = 0x0011;
                const int SPIF_UPDATEINIFILE = 0x01;
                const int SPIF_SENDWININICHANGE = 0x02;

                // Включаем заставку с обновлением из реестра
                SystemParametersInfo(SPI_SETSCREENSAVEACTIVE, 1, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);

                Log.Add($"Время до блокировки экрана установлено: {minutes} мин. ({seconds} сек.)", LogType.Success);
                return true;
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка установки времени блокировки экрана: {ex.Message}", LogType.Error);
                return false;
            }
        }

        // P/Invoke объявление
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(int uiAction, int uiParam, IntPtr pvParam, int fWinIni);
    }
}