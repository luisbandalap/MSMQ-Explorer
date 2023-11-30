using System;
using System.Collections.Generic;
using System.Linq;
using System.Messaging;
using System.Text;

namespace MSMQExplorer
{
    class MessageQueueWrapper
    {
        public MessageQueue MessageQueue;
        public int MessageCount { get; set; }

        public MessageQueueWrapper()
        {
            MessageQueue = new MessageQueue();
        }

        public MessageQueueWrapper(string path)
        {
            MessageQueue = new MessageQueue();
        }

        /// <summary>
        ///     Get the number of messages in the specified queue
        /// </summary>
        /// <param name="queue">MSMQ queue object</param>
        /// <returns></returns>
        private void UpdateMessageCount(MessageQueue queue)
        {
            try
            {
                int count = 0;
                if (queue == null)
                {
                    MessageCount = -1;
                }

                Cursor cursor = MessageQueue.CreateCursor();

                Message m = PeekWithoutTimeout(MessageQueue, cursor, PeekAction.Current);
                if (m == null) MessageCount = count;
                count = 1;
                while ((m = PeekWithoutTimeout(MessageQueue, cursor, PeekAction.Next)) != null)
                {
                    count++;
                }
                MessageCount = count;
            }
            catch (Exception)
            {
                MessageCount = -1;
            }
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
            catch (Exception mqe)
            {
                if (mqe is MessageQueueException)
                {
                    if (!mqe.Message.ToLower().Contains("timeout"))
                    {
                        throw;
                    }
                }
                else
                {
                    throw;
                }
            }

            return ret;
        }
    }


}
