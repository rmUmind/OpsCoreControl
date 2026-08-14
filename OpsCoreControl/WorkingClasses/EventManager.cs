using System;
using System.Collections.Generic;
using System.Diagnostics;
using static OpsCoreControl.Log;

// Классы для работы с журналом событий Windows (System, Application):
// модель записи и чтение последних записей с фильтром по типу.
namespace OpsCoreControl.WorkingClasses
{
    // Модель одной записи журнала событий.
    public class EventLogEntryInfo
    {
        public string Time { get; set; }
        public string Type { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }

        // Для списка обрезаем длинное сообщение; полный текст остаётся в Message.
        public override string ToString()
        {
            string msg = Message ?? "";
            if (msg.Length > 120) msg = msg.Substring(0, 120) + "…";
            return $"{Time}  [{Type}]  {Source}:  {msg}";
        }
    }

    // Класс для чтения журнала событий Windows.
    internal class EventLogManager
    {
        // Возвращает последние count записей журнала logName, можно отфильтровать по типу.
        public List<EventLogEntryInfo> GetRecentEventLog(string logName, int count, EventLogEntryType? filterType)
        {
            var result = new List<EventLogEntryInfo>();
            try
            {
                using (var log = new EventLog(logName))
                {
                    int total = log.Entries.Count;
                    int added = 0;
                    // Идём с конца — свежие записи первыми, останавливаемся, когда наберём count.
                    for (int i = total - 1; i >= 0 && added < count; i--)
                    {
                        EventLogEntry entry = log.Entries[i];
                        // Если задан фильтр — пропускаем записи не того типа.
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
