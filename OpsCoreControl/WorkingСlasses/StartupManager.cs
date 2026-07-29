using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using static OpsCoreControl.Log;

namespace OpsCoreControl.WorkingСlasses
{
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
        private const string RunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string ApprovedPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

        private static readonly byte[] EnabledBytes = { 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        private static readonly byte[] DisabledBytes = { 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        public List<StartupProgram> GetStartupPrograms()
        {
            var result = new List<StartupProgram>();
            CollectFromHive(Registry.CurrentUser, "HKCU", result);
            CollectFromHive(Registry.LocalMachine, "HKLM", result);
            return result.OrderBy(p => p.Name).ToList();
        }

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
            catch { }
        }

        private bool IsEnabled(RegistryKey approvedKey, string name)
        {
            if (approvedKey == null) return true;
            byte[] data = approvedKey.GetValue(name) as byte[];
            if (data == null || data.Length == 0) return true;
            return data[0] != 0x03;   // 0x03 = выключено
        }

        public bool SetEnabled(StartupProgram program, bool enabled)
        {
            try
            {
                RegistryKey root = program.Location == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
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