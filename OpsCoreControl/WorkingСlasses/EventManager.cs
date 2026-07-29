using System;
using System.Collections.Generic;
using System.Diagnostics;
using static OpsCoreControl.Log;

namespace OpsCoreControl.WorkingСlasses   // ← тот же namespace, что у остальных менеджеров
{
    public class EventLogEntryInfo
    {
        public string Time { get; set; }
        public string Type { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }
        public override string ToString()
        {
            string msg = Message ?? "";
            if (msg.Length > 120) msg = msg.Substring(0, 120) + "…";
            return $"{Time}  [{Type}]  {Source}:  {msg}";
        }
    }

    internal class EventLogManager   // было EventManager
    {
        public List<EventLogEntryInfo> GetRecentEventLog(string logName, int count, EventLogEntryType? filterType)
        {
            var result = new List<EventLogEntryInfo>();
            try
            {
                using (var log = new EventLog(logName))
                {
                    int total = log.Entries.Count;
                    int added = 0;
                    for (int i = total - 1; i >= 0 && added < count; i--)
                    {
                        EventLogEntry entry = log.Entries[i];
                        if (filterType.HasValue && entry.EntryType != filterType.Value) continue;
                        result.Add(new EventLogEntryInfo
                        {
                            Time = entry.TimeGenerated.ToString("yyyy-MM-dd HH:mm:ss"),
                            Type = entry.EntryType.ToString(),
                            Source = entry.Source,
                            Message = entry.Message ?? ""
                        });
                        added++;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка чтения журнала '{logName}': {ex.Message}", LogType.Error);
            }
            return result;
        }
    }
}