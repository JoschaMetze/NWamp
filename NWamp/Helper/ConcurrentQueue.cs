#if WINDOWS_PHONE
using System;
using System.Net;
using System.Collections.Generic;

namespace System.Collections.Concurrent
{
    public class ConcurrentQueue<T>
    {
        private Queue<T> m_Queue;

        private object m_SyncRoot = new object();

        public ConcurrentQueue()
        {
            m_Queue = new Queue<T>();
        }

        public ConcurrentQueue(int capacity)
        {
            m_Queue = new Queue<T>(capacity);
        }

        public ConcurrentQueue(IEnumerable<T> collection)
        {
            m_Queue = new Queue<T>(collection);
        }

        public bool TryAdd(T item)
        {
            lock (m_SyncRoot)
            {
                if (m_Queue.Contains(item))
                    return false;
                m_Queue.Enqueue(item);
                return true;
            }
        }
        public void Enqueue(T item)
        {
            lock (m_SyncRoot)
            {
                m_Queue.Enqueue(item);
            }
        }
        public T Take()
        {
            T result= default(T);
            TryDequeue(out result);
            return result;
        }
        public bool TryDequeue(out T item)
        {
            lock (m_SyncRoot)
            {
                if (m_Queue.Count <= 0)
                {
                    item = default(T);
                    return false;
                }

                item = m_Queue.Dequeue();
                return true;
            }
        }
    }
}
#endif