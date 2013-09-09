using NWamp.Messages;
using System;
using System.Collections.Concurrent;

namespace NWamp.Transport
{
    /// <summary>
    /// Implementation of <see cref="IResponseQueue"/> using web socket as message transport medium.
    /// </summary>
    public class SocketResponseQueue : IResponseQueue
    {
        /// <summary>
        /// Internal queue used for storing and retrieving connections.
        /// </summary>
#if !WINDOWS_PHONE
        private readonly BlockingCollection<Tuple<string, IMessage>> _queue;
#else
        private readonly ConcurrentQueue<Tuple<string, IMessage>> _queue;
#endif
        /// <summary>
        /// Initializes a new instance of the <see cref="SocketResponseQueue"/> class.
        /// </summary>
        public SocketResponseQueue()
        {
#if !WINDOWS_PHONE
            _queue = new BlockingCollection<Tuple<string, IMessage>>();
#else
            _queue = new ConcurrentQueue<Tuple<string, IMessage>>();
#endif
        }

        /// <summary>
        /// Queues a new <paramref name="message"/> to be sent to 
        /// WAMP client identified by <paramref name="sessionId"/>.
        /// </summary>
        /// <param name="sessionId">WAMP client sesssion identifier</param>
        /// <param name="message">WAMP message to send</param>
        public void Send(string sessionId, IMessage message)
        {
            _queue.TryAdd(Tuple.Create(sessionId, message));
        }

        public Tuple<string, IMessage> Receive()
        {
            return _queue.Take();
        } 
    }
}
