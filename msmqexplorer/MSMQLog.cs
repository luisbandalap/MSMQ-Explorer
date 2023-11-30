using System;

namespace MSMQExplorer
{
    class MSMQLog
    {
        public DateTime TimeStamp;
        public string LogType;
        public string LogEntry;

        public MSMQLog(DateTime timeStamp, string logType, string logEntry)
        {
            TimeStamp = timeStamp;
            LogType = logType;
            LogEntry = logEntry;
        }
    }
}
