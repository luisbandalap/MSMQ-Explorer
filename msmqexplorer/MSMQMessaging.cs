#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Messaging;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Transactions;
using System.Xml;

#endregion

namespace MSMQExplorer
{
    internal class MsmqMessaging
    {
        private readonly BinaryMessageFormatter _binaryFormatter = new BinaryMessageFormatter();
        private readonly StringMessageFormatter _stringFormatter = new StringMessageFormatter();
        private readonly XmlMessageFormatter _xmlFormatter = new XmlMessageFormatter(new[] {typeof (XmlDocument)});
        private string _currentRecQueue;
        public string SendHostName { get; set; }
        public string RecHostName { get; set; }

        public MessageQueue[] SendPrivateQueueList { get; set; }
        public MessageQueue[] SendPublicQueueList { get; set; }
        public MessageQueue[] RecPrivateQueueList { get; set; }
        public MessageQueue[] RecPublicQueueList { get; set; }
        public SystemMessageQueue[] RecSystemQueueList { get; set; }
        public List<Message> RecMessageList { get; set; }
        // Message formatters

        /// <summary>
        ///     Determine if MSMQ is installed on the local machine
        /// </summary>
        /// <returns>True if MSMQ is installed, false if it is not</returns>
        public bool IsMsmqInstalled()
        {
            List<ServiceController> services = ServiceController.GetServices().ToList();
            ServiceController msQue = services.Find(o => o.ServiceName == "MSMQ");
            if (msQue == null) return false;
            return msQue.Status == ServiceControllerStatus.Running;
        }

        public void MoveMessage()
        {
        }

        /// <summary>
        ///     Refreshes the 'Send' host queue list
        /// </summary>
        public void RefreshSendQueueList()
        {
            SendPrivateQueueList = GetPrivateQueues(SendHostName);
            SendPublicQueueList = GetPublicQueues(SendHostName);
        }

        /// <summary>
        ///     Refreshes the 'Receive' host queue list
        /// </summary>
        public void RefreshRecQueueList()
        {
            RecPrivateQueueList = GetPrivateQueues(RecHostName);
            RecPublicQueueList = GetPublicQueues(RecHostName);
            RecSystemQueueList = GetSystemQueues(RecHostName);
        }

        /// <summary>
        ///     Sends a message to the specified queue on the host machine
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="messageByteList"></param>
        /// <param name="msgLabel"></param>
        /// <param name="format"></param>
        /// <param name="useTransaction"></param>
        /// <param name="timeToReachQueue"></param>
        /// <param name="timeToReachQueuePeriod"></param>
        /// <param name="timeToReceive"></param>
        /// <param name="timeToRecievePeriod"></param>
        /// <param name="useDeadLetterQueue"></param>
        /// <param name="isPrivateQueue"></param>
        /// <param name="useEncryption"></param>
        /// <param name="useTracing"></param>
        /// <param name="useJournal"></param>
        /// <param name="priority"></param>
        public void SendMessage(string queueName, List<byte> messageByteList, string msgLabel, string format,
            bool useTransaction, bool isPrivateQueue, int timeToReachQueue,
            string timeToReachQueuePeriod, int timeToReceive, string timeToRecievePeriod, bool useDeadLetterQueue,
            bool useEncryption, bool useTracing, bool useJournal, string priority)
        {
            // Set up the queue
            string queuePath = "FormatName:DIRECT=OS:" + SendHostName + @"\" + queueName;
            MessageQueue mq = new MessageQueue(queuePath);
            Message message;

            // Use appropriate formatter
            switch (format)
            {
                case "XML":
                    // Create message using Xml Formatter
                    mq.Formatter = _xmlFormatter;
                    XmlDocument messageXml = new XmlDocument();
                    messageXml.LoadXml(StringTools.GetStringFromByteList(messageByteList));
                    message = new Message(messageXml);
                    break;
                case "Text":
                    // Create message using Text Formatter
                    message = new Message(StringTools.GetStringFromByteList(messageByteList), _stringFormatter);
                    break;
                default:
                    // Create message using Binary formatter
                    message = new Message {BodyStream = new MemoryStream(messageByteList.ToArray())};
                    break;
            }

            // Check for timeout and dead letter settings
            message.UseDeadLetterQueue = useDeadLetterQueue;

            if (timeToReceive != 0)
            {
                message.TimeToBeReceived = GetTimeSpan(timeToRecievePeriod, timeToReceive);
            }

            if (timeToReachQueue != 0)
            {
                message.TimeToReachQueue = GetTimeSpan(timeToReachQueuePeriod, timeToReachQueue);
            }

            bool isQTrans = false;
            try
            {
                isQTrans = mq.Transactional;
            }
            catch (Exception)
            {
                isQTrans = useTransaction;
            }

            // Set other message attributes
            message.UseEncryption = useEncryption;
            message.UseJournalQueue = useJournal;
            message.UseTracing = useTracing;
            message.Priority = GetMessagePriority(priority);
            // Check if transaction is required
            if ((!isPrivateQueue && useTransaction) || isQTrans)
            {
                // Create a message queuing transaction.
                MessageQueueTransaction transaction = new MessageQueueTransaction();
                // Process MQ transaction
                try
                {
                    transaction.Begin();
                    mq.Send(message, msgLabel, transaction);
                    transaction.Commit();
                }
                catch
                {
                    transaction.Abort();
                    throw;
                }
            }
            else
            {
                mq.Send(message, msgLabel);
            }
        }

        private static MessagePriority GetMessagePriority(string priority)
        {
            switch (priority)
            {
                case "AboveNormal":
                    return MessagePriority.AboveNormal;
                case "High":
                    return MessagePriority.High;
                case "Highest":
                    return MessagePriority.Highest;
                case "Low":
                    return MessagePriority.Low;
                case "Lowest":
                    return MessagePriority.Lowest;
                case "VeryHigh":
                    return MessagePriority.VeryHigh;
                case "VeryLow":
                    return MessagePriority.VeryLow;
                default:
                    return MessagePriority.Normal;
            }
        }

        private static TimeSpan GetTimeSpan(string period, int duration)
        {
            if (period == "Seconds")
            {
                return new TimeSpan(0, 0, 0, duration);
            }
            if (period == "Minutes")
            {
                return new TimeSpan(0, 0, duration, 0);
            }
            return period == "Hours" ? new TimeSpan(0, duration, 0, 0) : new TimeSpan(duration, 0, 0, 0);
        }

        /// <summary>
        ///     Recieves the next messages from the specified queue on the host machine
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="isJournal"></param>
        /// <param name="format"></param>
        /// <param name="useTransaction"></param>
        /// <param name="isPrivateQueue"></param>
        /// <returns></returns>
        public List<byte> ReceiveMessage(string queueName, bool isJournal, string format, bool useTransaction,
            bool isPrivateQueue)
        {
            string queuePath = GetQueuePath(RecHostName, queueName);

            // Set up MSMQ queue and message
            MessageQueue mq = new MessageQueue(queuePath, QueueAccessMode.ReceiveAndAdmin);

            bool isQTrans = false;
            bool isRemoteQueue = false;

            try
            {
                isQTrans = mq.Transactional;
            }
            catch (Exception)
            {
                isQTrans = useTransaction;
                isRemoteQueue = true;
            }

            // Determine if transaction is required
            bool requireTransaction = (!isJournal && (useTransaction && !isPrivateQueue)) || isQTrans;

            // Return message text
            List<byte> messageByteList = new List<byte>();
            messageByteList = null;
            Message message;

            if (isRemoteQueue)
            {
                TransactionScope dstTransactionScope = new TransactionScope();
                message = mq.Receive(MessageQueueTransactionType.Automatic);
                dstTransactionScope.Complete();
            }
            else
            {
                if (requireTransaction)
                { 
                    MessageQueueTransaction transaction = new MessageQueueTransaction();

                    transaction.Begin();
                    message = mq.Receive(TimeSpan.FromSeconds(2.0), transaction);
                    transaction.Commit();
                }
                else
                {
                    message = mq.Receive(TimeSpan.FromSeconds(2.0));
                }
            }
            if (message != null)
            {
                switch (format)
                {
                    case "XML":
                        // Format as XML
                        message.Formatter = _xmlFormatter;
                        // Extract the XML document from the message
                        XmlDocument messageXml = (XmlDocument) message.Body;
                        messageByteList = StringTools.GetByteListFromString(messageXml.OuterXml);
                        break;
                    case "Text":
                        // Format as Text
                        message.Formatter = _stringFormatter;
                        string messageText = message.Body.ToString();
                        messageByteList = StringTools.GetByteListFromString(messageText);
                        break;
                    default:
                        // Format as Binary
                        message.Formatter = _binaryFormatter;
                        BinaryReader reader = new BinaryReader(message.BodyStream, Encoding.Unicode);
                        messageByteList = reader.ReadAllBytes().ToList();
                        break;
                }
            }
            return messageByteList;
        }

        public MessageQueue GetQueueByName(string host, string queueName)
        {
            string queuePath = GetQueuePath(RecHostName, queueName);

            // Set up MSMQ queue and message
            MessageQueue mq = new MessageQueue(queuePath);
            return mq;
        }

        /// <summary>
        ///     Delete the specified message from the specified queue on the host machine
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="messageId">ID of the message to delete</param>
        public void DeleteMessage(string queueName, string messageId)
        {
            // Open the Queue
            string queuePath = GetQueuePath(RecHostName, queueName);
            MessageQueue popQ = new MessageQueue(queuePath);

            // Set this property to read all properties
            popQ.MessageReadPropertyFilter.SetAll();

            // Get a message enumerator we can use to go through all the messages
            MessageEnumerator enumerator = popQ.GetMessageEnumerator2();

            // Loop through all the messages 
            while (enumerator.MoveNext(new TimeSpan(0, 0, 1)))
            {
                // Get a reference to the current message
                Message message = enumerator.Current;
                if (message == null) continue;
                if (message.Id.Equals(messageId))
                {
                    popQ.ReceiveById(message.Id);
                    // listViewMessages.Items.RemoveAt(listViewMessages.SelectedItems[0].Index); 
                }
            }
        }

        /// <summary>
        ///     Gets details of the specified message from the specified queue host machine
        /// </summary>
        /// <param name="format"></param>
        /// <param name="messageId"></param>
        /// <returns></returns>
        public List<byte> PreviewMessage(string format, string messageId)
        {
            List<byte> messageByteList;
            Message message = GetMessageById(messageId);

            switch (format)
            {
                case "XML":
                    // Receive as XML
                    message.Formatter = _xmlFormatter;
                    XmlDocument messageXml = (XmlDocument) message.Body;
                    messageByteList = StringTools.GetByteListFromString(messageXml.OuterXml);
                    break;
                case "Text":
                    // Receive as text
                    message.Formatter = _stringFormatter;
                    string msgBody = message.Body.ToString();
                    messageByteList = StringTools.GetByteListFromString(msgBody);
                    break;
                default:
                    // Receive as Binary
                    message.Formatter = _binaryFormatter;
                    BinaryReader reader = new BinaryReader(message.BodyStream);
                    messageByteList = reader.ReadAllBytes().ToList();
                    break;
            }

            return messageByteList;
        }

        /// <summary>
        ///     Get a message from the current queue
        /// </summary>
        /// <param name="messageId"></param>
        /// <returns></returns>
        public Message GetMessageById(string messageId)
        {
            RefreshRecMessageList(_currentRecQueue);
            Message foundMessage = new Message();
            // loop through all the messages 
            foreach (Message message in RecMessageList.Where(message => message.Id.Equals(messageId)))
            {
                foundMessage = message;
            }

            return foundMessage;
        }

        /// <summary>
        ///     Helper function to count the number of messages in a queue
        /// </summary>
        /// <param name="q"></param>
        /// <param name="cursor"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        protected Message PeekWithoutTimeout(MessageQueue q, Cursor cursor, PeekAction action)
        {
            Message ret = null;
            try
            {
                ret = q.Peek(new TimeSpan(1), cursor, action);
            }
            catch (MessageQueueException mqe)
            {
                if (mqe.MessageQueueErrorCode != MessageQueueErrorCode.IOTimeout 
                    && mqe.MessageQueueErrorCode != MessageQueueErrorCode.IllegalCursorAction)
                {
                    throw;
                }
            }

            return ret;
        }

        /// <summary>
        ///     Return the total number of messages on the specified queue on the host machine
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        public int GetMessageCount(string queueName, string hostname)
        {
            try
            {
                string queuePath = GetQueuePath(hostname, queueName);
                MessageQueue queue = new MessageQueue(queuePath, QueueAccessMode.PeekAndAdmin);
                if (!queue.CanRead)
                {
                    return -1;
                }
                int count = GetMessageCount(queue);
                return count;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>
        ///     Get the number of messages in the specified queue
        /// </summary>
        /// <param name="queue">MSMQ queue object</param>
        /// <returns></returns>
        private int GetMessageCount(MessageQueue queue)
        {
            try
            {
                int count = 0;
                if (queue == null)
                {
                    return -1;
                }

                Cursor cursor = queue.CreateCursor();

                Message m = PeekWithoutTimeout(queue, cursor, PeekAction.Current);
                if (m == null) return count;
                count = 1;
                while ((m = PeekWithoutTimeout(queue, cursor, PeekAction.Next)) != null)
                {
                    count++;
                }
                return count;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        [DllImport("mqrt.dll")]
        public static extern int MQMoveMessage(IntPtr sourceQueue, IntPtr targetQueue, long lookupId,
            IDtcTransaction transaction);


        /// <summary>
        ///     Returns a list of all private queues on the specified host
        /// </summary>
        /// <param name="hostName"></param>
        /// <returns></returns>
        private static MessageQueue[] GetPrivateQueues(string hostName)
        {
            MessageQueue[] queueList;
            // Get a list of queues on the specified computer.
            try
            {
                queueList =
                    MessageQueue.GetPrivateQueuesByMachine("FormatName:DIRECT=OS:" + hostName);
            }
            catch
            {
                queueList =
                    MessageQueue.GetPrivateQueuesByMachine(hostName);
            }

            return queueList;
        }

        /// <summary>
        ///     Returns a list of all public queues on the specified host
        /// </summary>
        /// <param name="hostName"></param>
        /// <returns></returns>
        private static MessageQueue[] GetPublicQueues(string hostName)
        {
            try
            {
                MessageQueue[] queueList = MessageQueue.GetPublicQueuesByMachine(hostName);
                return queueList;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        ///     Populates the system queues (Journal, Dead Letter and Dead Letter X
        /// </summary>
        /// <param name="hostName"></param>
        /// <returns></returns>
        private static SystemMessageQueue[] GetSystemQueues(string hostName)
        {
            // Get a list of queues on the specified computer.
            SystemMessageQueue[] queueList = GetSystemQueuesByMachine(hostName);
            return queueList;
        }

        public void RefreshRecMessageList(string queueName)
        {
            if (string.IsNullOrEmpty(queueName)) return;
            string queuePath = GetQueuePath(RecHostName, queueName);
            // Connect to a queue
            MessageQueue queue = new MessageQueue(queuePath, QueueAccessMode.SendAndReceive);
            queue.MessageReadPropertyFilter.SetAll();

            // Populate an array with copies of all the messages in the queue.
            Message[] messages = queue.GetAllMessages();
            RecMessageList = messages.ToList();
            _currentRecQueue = queueName;
        }

        /// <summary>
        ///     Returns the FormatName format of the specified queue on the given host
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="queueName"></param>
        /// <returns></returns>
        public static string GetQueuePath(string hostName, string queueName)
        {
            return "FormatName:DIRECT=OS:" + hostName + @"\" + queueName;
        }

        /// <summary>
        ///     Purges the specified queue
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="queueName"></param>
        public void PurgeQueue(string hostName, string queueName)
        {
            if (string.IsNullOrEmpty(queueName)) return;
            string queuePath = GetQueuePath(hostName, queueName);
            // Connect to a queue
            MessageQueue queue = new MessageQueue(queuePath);
            queue.Purge();
        }

        public static string GetSimpleQueuePath(string hostName, string queueName)
        {
            return (hostName + @"\" + queueName);
        }

        public static string GetNiceQueueName(string queueName)
        {
            switch (queueName)
            {
                case "system$;JOURNAL":
                    return "Journal messages";
                case "system$;DEADLETTER":
                    return "Dead-letter messages";
                case "system$;DEADXACT":
                    return "Transactional dead-letter messages";
                 default:
                    return queueName.Substring(queueName.LastIndexOf("$", StringComparison.Ordinal) + 2);
            }
        }

        public MessageQueue CreateQueue(string hostName, string queueType, string queueName, bool isTransactional)
        {
            if (string.IsNullOrEmpty(hostName) || string.IsNullOrEmpty(queueName)) return null;
            string queuePath = queueType == @"public$\"
                ? GetSimpleQueuePath(hostName, queueName)
                : GetSimpleQueuePath(hostName, queueType + queueName);
            return MessageQueue.Create(queuePath, isTransactional);
        }

        public void CreateSendQueue(string queueType, string queueName, bool isTransactional)
        {
            if (string.IsNullOrEmpty(queueType) || string.IsNullOrEmpty(queueName)) return;
            string queuePath = GetSimpleQueuePath(SendHostName, queueType + queueName);
            MessageQueue.Create(queuePath, isTransactional);
        }

        public void CreateRecQueue(string queueType, string queueName, bool isTransactional)
        {
            if (string.IsNullOrEmpty(queueType) || string.IsNullOrEmpty(queueName)) return;
            string queuePath = GetSimpleQueuePath(SendHostName, queueType + queueName);
            MessageQueue.Create(queuePath, isTransactional);
        }

        public void DeleteQueue(string hostName, string queueName)
        {
            if (string.IsNullOrEmpty(hostName) || string.IsNullOrEmpty(queueName)) return;
            string queuePath = GetSimpleQueuePath(hostName, queueName);
            MessageQueue.Delete(queuePath);
        }

        public static SystemMessageQueue[] GetSystemQueuesByMachine(string hostName)
        {
            SystemMessageQueue[] queueList = new SystemMessageQueue[3];

            try
            {
                // Get a list of queues on the specified computer.
                // System Journal
                queueList[0] = new SystemMessageQueue(hostName, "system$;JOURNAL", "Journal messages");

                // Get the Dead Letter queue
                queueList[1] = new SystemMessageQueue(hostName, "system$;DEADLETTER", "Dead-letter messages");

                // Transactional Dead Letter Queue
                queueList[2] = new SystemMessageQueue(hostName, "system$;DEADXACT", "Transactional dead-letter messages");
            }
            catch (Exception)
            {
                return queueList;
            }

            return queueList;
        }
    }
}