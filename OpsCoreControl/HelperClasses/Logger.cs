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
            Info
        }

        public static event Action<string> LogMessage;
        public static event Action<string> LogProfile;
        public static event Action<string> LogError;
        public static event Action<string> LogSuccess;
        public static event Action<string> LogInfo;
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
                default:
                    break;
            }
        }
    }
}
