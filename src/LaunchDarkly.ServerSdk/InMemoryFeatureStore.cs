﻿using System;
using System.Collections.Generic;
using System.Threading;
using Common.Logging;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// In-memory, thread-safe implementation of <see cref="IFeatureStore"/>.
    /// </summary>
    /// <remarks>
    /// Referencing this class directly is deprecated; please use <see cref="Components.InMemoryFeatureStore"/>
    /// in <see cref="Components"/> instead.
    /// </remarks>
    public class InMemoryFeatureStore : IFeatureStore
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(InMemoryFeatureStore));
        private readonly ReaderWriterLockSlim RwLock = new ReaderWriterLockSlim();
        private readonly IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> Items =
            new Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>();
        private bool _initialized = false;

        /// <summary>
        /// Creates a new empty feature store instance. Constructing this class directly is deprecated;
        /// please use <see cref="Components.InMemoryFeatureStore"/> in <see cref="Components"/> instead.
        /// </summary>
        [Obsolete("Constructing this class directly is deprecated; please use Components.InMemoryFeatureStore")]
        public InMemoryFeatureStore() { }

        /// <inheritdoc/>
        public T Get<T>(VersionedDataKind<T> kind, string key) where T : class, IVersionedData
        {
            RwLock.EnterReadLock();
            try
            {
                IDictionary<string, IVersionedData> itemsOfKind;
                IVersionedData item;

                if (!Items.TryGetValue(kind, out itemsOfKind))
                {
                    Log.DebugFormat("Key {0} not found in '{1}'; returning null", key, kind.GetNamespace());
                    return null;
                }
                if (!itemsOfKind.TryGetValue(key, out item))
                {
                    Log.DebugFormat("Key {0} not found in '{1}'; returning null", key, kind.GetNamespace());
                    return null;
                }
                if (item.Deleted)
                {
                    Log.WarnFormat("Attempted to get deleted item with key {0} in '{1}'; returning null.",
                        key, kind.GetNamespace());
                    return null;
                }
                return (T)item;
            }
            finally
            {
                RwLock.ExitReadLock();
            }
        }

        /// <inheritdoc/>
        public IDictionary<string, T> All<T>(VersionedDataKind<T> kind) where T : class, IVersionedData
        {
            RwLock.EnterReadLock();
            try
            {
                IDictionary<string, T> ret = new Dictionary<string, T>();
                IDictionary<string, IVersionedData> itemsOfKind;
                if (Items.TryGetValue(kind, out itemsOfKind))
                {
                    foreach (var entry in itemsOfKind)
                    {
                        if (!entry.Value.Deleted)
                        {
                            ret[entry.Key] = (T)entry.Value;
                        }
                    }
                }
                return ret;
            }
            finally
            {
                RwLock.ExitReadLock();
            }
        }

        /// <inheritdoc/>
        public void Init(IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> items)
        {
            RwLock.EnterWriteLock();
            try
            {
                Items.Clear();
                foreach (var kindEntry in items)
                {
                    IDictionary<string, IVersionedData> itemsOfKind = new Dictionary<string, IVersionedData>();
                    foreach (var e1 in kindEntry.Value)
                    {
                        itemsOfKind[e1.Key] = e1.Value;
                    }
                    Items[kindEntry.Key] = itemsOfKind;
                }
                _initialized = true;
            }
            finally
            {
                RwLock.ExitWriteLock();
            }
        }

        /// <inheritdoc/>
        public void Delete<T>(VersionedDataKind<T> kind, string key, int version) where T : IVersionedData
        {
            RwLock.EnterWriteLock();
            try
            {
                IDictionary<string, IVersionedData> itemsOfKind;
                if (Items.TryGetValue(kind, out itemsOfKind))
                {
                    IVersionedData item;
                    if (!itemsOfKind.TryGetValue(key, out item) || item.Version < version)
                    {
                        itemsOfKind[key] = kind.MakeDeletedItem(key, version);
                    }
                }
            }
            finally
            {
                RwLock.ExitWriteLock();
            }
        }

        /// <inheritdoc/>
        public void Upsert<T>(VersionedDataKind<T> kind, T item) where T : IVersionedData
        {
            RwLock.EnterWriteLock();
            try
            {
                IDictionary<string, IVersionedData> itemsOfKind;
                if (!Items.TryGetValue(kind, out itemsOfKind))
                {
                    itemsOfKind = new Dictionary<string, IVersionedData>();
                    Items[kind] = itemsOfKind;
                }
                IVersionedData old;
                if (!itemsOfKind.TryGetValue(item.Key, out old) || old.Version < item.Version)
                {
                    itemsOfKind[item.Key] = item;
                }
            }
            finally
            {
                RwLock.ExitWriteLock();
            }
        }

        /// <inheritdoc/>
        public bool Initialized()
        {
            return _initialized;
        }

        /// <inheritdoc/>
        public void Dispose()
        { }
    }
}