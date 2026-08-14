using System;

// Класс для логирования. Принимает сообщение с типом и разносит его по событиям —
// каждый подписчик (например, чат в главном окне) слушает свой тип записей.
namespace OpsCoreControl
{
    public static class Log
    {
        private static readonly object FileLock = new object();
        private static readonly string LogDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        private static readonly string LogFile = System.IO.Path.Combine(LogDirectory, $"OpsCoreControl_{DateTime.Now:yyyy-MM-dd}.log");
        // Типы записей в логе.
        public enum LogEntryType
        {
            Message,
            Profile,
            Error,
            Success,
            Info,
            Debug
        }

        // Константы-сокращения, чтобы в коде писать LogType.Error вместо Log.LogEntryType.Error.
        public static class LogType
        {
            public const LogEntryType Message = LogEntryType.Message;
            public const LogEntryType Profile = LogEntryType.Profile;
            public const LogEntryType Error = LogEntryType.Error;
            public const LogEntryType Success = LogEntryType.Success;
            public const LogEntryType Info = LogEntryType.Info;
            public const LogEntryType Debug = LogEntryType.Debug;
        }

        // События по типам записей. На них подписывается UI и выводит сообщения.
        public static event Action<string> LogMessage;
        public static event Action<string> LogProfile;
        public static event Action<string> LogError;
        public static event Action<string> LogSuccess;
        public static event Action<string> LogInfo;
        public static event Action<string> LogDebug;

        // Добавляет запись в лог: вызывает событие, соответствующее типу.
        public static void Add(string message, LogEntryType type)
        {
            WriteToFile(message, type);
            switch (type)
            {
                case LogEntryType.Message:
                    LogMessage?.Invoke(message);
                    break;
                case LogEntryType.Profile:
                    LogProfile?.Invoke(message);
                    break;
                case LogEntryType.Error:
                    LogError?.Invoke(message);
                    break;
                case LogEntryType.Success:
                    LogSuccess?.Invoke(message);
                    break;
                case LogEntryType.Info:
                    LogInfo?.Invoke(message);
                    break;
                case LogEntryType.Debug:
                    LogDebug?.Invoke(message);
                    break;
                default:
                    break;
            }
        }

        public static string CurrentLogFile => LogFile;
        public static void InitializeFileLogging()
        {
            try
            {
                System.IO.Directory.CreateDirectory(LogDirectory);
                foreach (string file in System.IO.Directory.GetFiles(LogDirectory, "OpsCoreControl_*.log"))
                    if (System.IO.File.GetCreationTime(file) < DateTime.Now.AddDays(-30)) System.IO.File.Delete(file);
                WriteToFile("Запуск приложения", LogEntryType.Info);
            }
            catch { }
        }
        private static void WriteToFile(string message, LogEntryType type)
        {
            try { lock (FileLock) { System.IO.Directory.CreateDirectory(LogDirectory); System.IO.File.AppendAllText(LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{type}] {message}{Environment.NewLine}"); } }
            catch { }
        }
    }
}
