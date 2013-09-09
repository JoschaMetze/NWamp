using System.Collections;
#if !WINDOWS_PHONE
using System.Collections.Concurrent;
#endif
using NWamp.Transport;
using System.Collections.Generic;

namespace NWamp
{
    /// <summary>
    /// Default implementation of <see cref="ISessionContainer"/> interface.
    /// </summary>
    internal class DictionarySessionContainer : ISessionContainer
    {
        /// <summary>
        /// Internal dictionary used for storing WAMP sessions.
        /// </summary>
#if !WINDOWS_PHONE
        private readonly IDictionary<string, IWampSession> _sessions = new ConcurrentDictionary<string, IWampSession>();
#else
        private readonly SynchronizedCache<string, IWampSession> _sessions = new SynchronizedCache<string, IWampSession>();
#endif

        /// <summary>
        /// Stores new WAMP session.
        /// </summary>
        /// <param name="session"></param>
        public void AddSession(IWampSession session)
        {
            _sessions.Add(session.SessionId, session);
        }

        /// <summary>
        /// Removes existing WAMP session.
        /// </summary>
        /// <param name="sessionId"></param>
        public void RemoveSession(string sessionId)
        {
            _sessions.Remove(sessionId);
        }

        /// <summary>
        /// Returns a WAMP session identified by provided <paramref name="sessionId"/> 
        /// or null if no matching session has been found.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public IWampSession GetSession(string sessionId)
        {
            IWampSession session;
            return _sessions.TryGetValue(sessionId, out session) ? session : null;
        }

        public IEnumerator<IWampSession> GetEnumerator()
        {
#if !WINDOWS_PHONE
            return _sessions.Values.GetEnumerator();
#else
            return _sessions.GetValueEnumerator();
#endif
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
