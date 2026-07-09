using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsCoreControl
{
    public static class Logger
    {
        public enum LogEntryType
        {
            Message,
            Profile,
            Error,
            Success,
            Info,
            Debug
        }

        public static class LogType
        {
            public const LogEntryType Message = LogEntryType.Message;
            public const LogEntryType Profile = LogEntryType.Profile;
            public const LogEntryType Error = LogEntryType.Error;
            public const LogEntryType Success = LogEntryType.Success;
            public const LogEntryType Info = LogEntryType.Info;
            public const LogEntryType Debug = LogEntryType.Debug;
        }

        public static event Action<string> LogMessage;
        public static event Action<string> LogProfile;
        public static event Action<string> LogError;
        public static event Action<string> LogSuccess;
        public static event Action<string> LogInfo;
        public static event Action<string> LogDebug;
        public static void Log(string message, LogEntryType type)
        {
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
                        LogInfo?.Invoke(message);
                    break;
                default:
                    break;
            }
        }
    }
}
