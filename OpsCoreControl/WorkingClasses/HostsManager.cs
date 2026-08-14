using System;
using System.Diagnostics;
using System.IO;
using static OpsCoreControl.Log;

// Класс для работы с файлом hosts: чтение содержимого и открытие папки с файлом.
// Приложение не пишет в hosts напрямую — его защищает «Контролируемый доступ к папкам»,
// поэтому правка идёт вручную через Блокнот (отсюда только чтение и открытие папки).
namespace OpsCoreControl.WorkingClasses
{
    internal class HostsManager
    {
        // Путь к файлу hosts (через SystemDirectory, без жёсткой привязки к диску C:).
        private static readonly string HostsPath = Path.Combine(Environment.SystemDirectory, "drivers", "etc", "hosts");

        // Читает и возвращает содержимое hosts. При ошибке возвращает пустую строку.
        public string ReadHosts()
        {
            try { return File.ReadAllText(HostsPath); }
            catch (Exception ex)
            {
                Log.Add($"Ошибка чтения hosts: {ex.Message}", LogType.Error);
                return "";
            }
        }

        // Открывает папку etc в Проводнике с выделенным файлом hosts (для ручной правки).
        public void OpenHostsFolder()
        {
            try
            {
                // /select — открыть папку и сразу выделить файл hosts.
                Process.Start("explorer.exe", $"/select,\"{HostsPath}\"");
                Log.Add("Открыта папка с файлом hosts.", LogType.Info);
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка открытия папки hosts: {ex.Message}", LogType.Error);
            }
        }
    }
}
