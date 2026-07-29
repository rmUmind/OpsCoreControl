using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static OpsCoreControl.Log;

namespace OpsCoreControl.WorkingСlasses
{
    public class ProcessInfo
    {
        public int Pid { get; set; }
        public string Name { get; set; }
        public long MemoryMb { get; set; }
        public override string ToString() => $"{Name}  (PID: {Pid})  —  {MemoryMb} МБ";
    }

    internal class ProcessManager
    {
        public List<ProcessInfo> GetProcesses()
        {
            var result = new List<ProcessInfo>();
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    long memMb = 0;
                    try { memMb = p.WorkingSet64 / (1024 * 1024); } catch { }
                    result.Add(new ProcessInfo { Pid = p.Id, Name = p.ProcessName, MemoryMb = memMb });
                }
                catch { }
                finally { p.Dispose(); }
            }
            return result.OrderBy(x => x.Name).ToList();
        }

        public bool KillProcess(int pid)
        {
            try
            {
                using (Process p = Process.GetProcessById(pid))
                {
                    p.Kill();
                    p.WaitForExit(5000);
                }
                Log.Add($"Процесс PID {pid} завершён.", LogType.Success);
                return true;
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка завершения процесса PID {pid}: {ex.Message}", LogType.Error);
                return false;
            }
        }
    }
}