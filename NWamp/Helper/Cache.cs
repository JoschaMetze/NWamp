using System;
using System.Threading;
using System.Collections.Generic;

public class SynchronizedCache<TKey,TValue>
{
    private ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim();
    private Dictionary<TKey, TValue> innerCache = new Dictionary<TKey, TValue>();

    public bool TryGetValue(TKey key,out TValue value)
    {
        cacheLock.EnterReadLock();
        try
        {
            bool bValue = innerCache.ContainsKey(key);
            value = innerCache[key];
            return bValue;
        }
        finally
        {
            cacheLock.ExitReadLock();
        }
        
    }
    public TValue Read(TKey key)
    {
        cacheLock.EnterReadLock();
        try
        {
            return innerCache[key];
        }
        finally
        {
            cacheLock.ExitReadLock();
        }
    }
    public bool TryAdd(TKey key, TValue value)
    {
        cacheLock.EnterWriteLock();
        try
        {
            bool bValue = innerCache.ContainsKey(key);
            innerCache.Add(key, value);
            return bValue;
        }
        finally
        {
            cacheLock.ExitWriteLock();
            
        }
        
    }
    public void Add(TKey key, TValue value)
    {
        cacheLock.EnterWriteLock();
        try
        {
            innerCache.Add(key, value);
        }
        finally
        {
            cacheLock.ExitWriteLock();
        }
    }

    public bool AddWithTimeout(TKey key, TValue value, int timeout)
    {
        if (cacheLock.TryEnterWriteLock(timeout))
        {
            try
            {
                innerCache.Add(key, value);
            }
            finally
            {
                cacheLock.ExitWriteLock();
            }
            return true;
        }
        else
        {
            return false;
        }
    }
    public IEnumerator<TValue> GetValueEnumerator()
    {
        return innerCache.Values.GetEnumerator();
    }
    public AddOrUpdateStatus AddOrUpdate(TKey key, TValue value)
    {
        cacheLock.EnterUpgradeableReadLock();
        try
        {
            TValue result = default(TValue);
            if (innerCache.TryGetValue(key, out result))
            {
                if (result.Equals(value))
                {
                    return AddOrUpdateStatus.Unchanged;
                }
                else
                {
                    cacheLock.EnterWriteLock();
                    try
                    {
                        innerCache[key] = value;
                    }
                    finally
                    {
                        cacheLock.ExitWriteLock();
                    }
                    return AddOrUpdateStatus.Updated;
                }
            }
            else
            {
                cacheLock.EnterWriteLock();
                try
                {
                    innerCache.Add(key, value);
                }
                finally
                {
                    cacheLock.ExitWriteLock();
                }
                return AddOrUpdateStatus.Added;
            }
        }
        finally
        {
            cacheLock.ExitUpgradeableReadLock();
        }
    }
    public bool  TryRemove(TKey key,out TValue value)
    {
        cacheLock.EnterWriteLock();
        try
        {
            bool bValue = innerCache.ContainsKey(key);
            value = innerCache[key];
            innerCache.Remove(key);
            return bValue;
        }
        finally
        {
            cacheLock.ExitWriteLock();
        }
        
    }
    public void Remove(TKey key)
    {
        cacheLock.EnterWriteLock();
        try
        {
            innerCache.Remove(key);
        }
        finally
        {
            cacheLock.ExitWriteLock();
        }
    }

    public enum AddOrUpdateStatus
    {
        Added,
        Updated,
        Unchanged
    };
}