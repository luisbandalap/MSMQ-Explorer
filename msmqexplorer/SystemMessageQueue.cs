#region

using System;

#endregion

namespace MSMQExplorer
{
    internal class SystemMessageQueue
    {
        public SystemMessageQueue(string hostName, string queueName, string displayName)
        {
            QueueName = queueName;
            QueuePath = MsmqMessaging.GetQueuePath(hostName, queueName);
            DisplayName = displayName;
        }

        public string QueueName { get; set; }
        public string QueuePath { get; set; }

        public string DisplayName { get; set; }
    }
}