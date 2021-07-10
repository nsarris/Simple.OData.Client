using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Simple.OData.Client.Extensions;

namespace Simple.OData.Client
{
    class EdmMetadataCache
    {
        static readonly ConcurrentDictionary<string, EdmMetadataCache> _instances = new ConcurrentDictionary<string, EdmMetadataCache>();
        static readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);

        public static void Clear()
        {
            _instances.Clear();
        }

        public static void Clear(string key)
        {
            _instances.TryRemove(key, out var md);
        }

        public static EdmMetadataCache GetOrAdd(string key, Func<string, EdmMetadataCache> valueFactory)
        {
            return _instances.GetOrAdd(key, valueFactory);
        }

        public static async Task<EdmMetadataCache> GetOrAddAsync(string key, Func<string, Task<EdmMetadataCache>> valueFactory)
        {
            // Cheaper to check first before we do the remote call
            if (_instances.TryGetValue(key, out var found))
            {
                return found;
            }

            // Just allow one schema request at a time, unlikely to be much contention but avoids multiple requests for same endpoint.
            await semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                // Can't easily lock, could introduce a semaphoreSlim but not sure if it's worth it.
                found = await valueFactory(key).ConfigureAwait(false);

                return _instances.GetOrAdd(key, found);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public EdmMetadataCache(string key, string metadataDocument)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));
            if (string.IsNullOrWhiteSpace(metadataDocument))
                throw new ArgumentNullException(nameof(metadataDocument));
            
            Key = key;
            MetadataDocument = metadataDocument;
        }

        public string Key { get; }

        public string MetadataDocument { get; }

        public IODataAdapter GetODataAdapter(ISession session)
        {
            return session.Settings.AdapterFactory.CreateAdapterLoader(MetadataDocument, session.Settings.TypeCache)(session);
        }
    }
}
