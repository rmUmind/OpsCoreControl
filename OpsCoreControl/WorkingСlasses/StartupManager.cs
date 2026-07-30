using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using static OpsCoreControl.Log;

// Класс для управления автозагрузкой: список программ из автозагрузки (HKCU/HKLM)
// и их включение/выключение через механизм StartupApproved — так же, как Диспетчер задач.
namespace OpsCoreControl.WorkingСlasses
{
    // Модель программы из автозагрузки.
    public class StartupProgram
    {
        public string Name { get; set; }
        public string Command { get; set; }
        public string Location { get; set; }   // HKCU / HKLM
        public bool Enabled { get; set; }
        public override string ToString() => $"{(Enabled ? "✓" : "✗")}  {Name}  [{Location}]  —  {Command}";
    }

    internal class StartupManager
    {
        // Раздел реестра с программами автозагрузки.
        private const string RunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        // Раздел с состоянием автозагрузки (включена/выключена).
        private const string ApprovedPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

        // Байты состояния для StartupApproved: первый байт 0x02 — включено, 0x03 — выключено.
        private static readonly byte[] EnabledBytes = { 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        private static readonly byte[] DisabledBytes = { 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        // Возвращает список программ автозагрузки из HKCU и HKLM, по имени.
        public List<StartupProgram> GetStartupPrograms()
        {
            var result = new List<StartupProgram>();
            CollectFromHive(Registry.CurrentUser, "HKCU", result);
            CollectFromHive(Registry.LocalMachine, "HKLM", result);
            return result.OrderBy(p => p.Name).ToList();
        }

        // Читает программы автозагрузки из одного куста реестра.
        private void CollectFromHive(RegistryKey root, string location, List<StartupProgram> list)
        {
            try
            {
                using (RegistryKey runKey = root.OpenSubKey(RunPath))
                using (RegistryKey approvedKey = root.OpenSubKey(ApprovedPath))
                {
                    if (runKey == null) return;
                    foreach (string name in runKey.GetValueNames())
                    {
                        list.Add(new StartupProgram
                        {
                            Name = name,
                            Command = runKey.GetValue(name)?.ToString() ?? "",
                            Location = location,
                            Enabled = IsEnabled(approvedKey, name)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Не удалось прочитать автозагрузку ({location}): {ex.Message}", LogType.Error);
            }
        }

        // Определяет, включена ли запись автозагрузки, по данным StartupApproved.
        private bool IsEnabled(RegistryKey approvedKey, string name)
        {
            if (approvedKey == null) return true; // раздела нет — считаем включённой
            byte[] data = approvedKey.GetValue(name) as byte[];
            if (data == null || data.Length == 0) return true; // записи нет — включена
            return data[0] != 0x03;   // первый байт: 0x03 — выключено
        }

        // Включает или выключает программу в автозагрузке (пишет состояние в StartupApproved).
        public bool SetEnabled(StartupProgram program, bool enabled)
        {
            try
            {
                RegistryKey root = program.Location == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
                // CreateSubKey откроет раздел или создаст его, если его ещё нет.
                using (RegistryKey approvedKey = root.CreateSubKey(ApprovedPath))
                {
                    approvedKey.SetValue(program.Name, enabled ? EnabledBytes : DisabledBytes, RegistryValueKind.Binary);
                }
                Log.Add($"Автозагрузка '{program.Name}' {(enabled ? "включена" : "выключена")}.", LogType.Success);
                return true;
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка изменения автозагрузки '{program.Name}': {ex.Message}", LogType.Error);
                return false;
            }
        }
    }
}